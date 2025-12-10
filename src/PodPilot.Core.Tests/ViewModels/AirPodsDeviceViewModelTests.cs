using FluentAssertions;
using NSubstitute;
using PodPilot.Core.Models;
using PodPilot.Core.Services;
using PodPilot.Core.ViewModels;
using TestHelpers;
using Xunit;

namespace PodPilot.Core.Tests.ViewModels;

/// <summary>
/// Tests for AirPodsDeviceViewModel.
/// Uses real DeviceStateManager with mocked device watchers to verify integration
/// with state management. External I/O services remain mocked.
/// </summary>
public class AirPodsDeviceViewModelTests : IAsyncLifetime, IDisposable
{
    private readonly IBluetoothConnectionService _fakeConnectionService;
    private readonly IAudioOutputService _fakeAudioOutputService;
    private readonly ISystemLauncherService _fakeSystemLauncherService;
    private readonly DeviceStateManagerTestFixture _fixture;

    public AirPodsDeviceViewModelTests()
    {
        _fakeConnectionService = Substitute.For<IBluetoothConnectionService>();
        _fakeAudioOutputService = Substitute.For<IAudioOutputService>();
        _fakeSystemLauncherService = Substitute.For<ISystemLauncherService>();

        // Create real services with mocked device watchers
        _fixture = DeviceStateManagerTestHelpers.CreateTestFixture();
    }

    public async Task InitializeAsync()
    {
        await _fixture.StartAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _fixture.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Fact]
    public void Constructor_SetsPropertiesFromState()
    {
        // Arrange
        var state = CreateTestState(productId: 0x2014, leftBattery: 75);

        // Act
        var vm = CreateViewModel(state);

        // Assert
        vm.ProductId.Should().Be(0x2014);
        vm.LeftBattery.Should().Be(75);
        vm.Model.Should().Be("AirPods Pro (2nd generation)");
    }

    [Fact]
    public void Status_WhenUnpaired_ReturnsUnpaired()
    {
        // Arrange
        var state = CreateTestState(pairedDeviceId: null);

        // Act
        var vm = CreateViewModel(state);

        // Assert
        vm.Status.Should().Be(DeviceStatus.Unpaired);
        vm.ShowPairingWarning.Should().BeTrue();
    }

    [Fact]
    public void Status_WhenPairedButDisconnected_ReturnsDisconnected()
    {
        // Arrange
        var state = CreateTestState(pairedDeviceId: "device-123", isConnected: false);

        // Act
        var vm = CreateViewModel(state);

        // Assert
        vm.Status.Should().Be(DeviceStatus.Disconnected);
    }

    [Fact]
    public void Status_WhenConnectedButNotAudioActive_ReturnsConnected()
    {
        // Arrange
        var state = CreateTestState(
            pairedDeviceId: "device-123",
            isConnected: true,
            isAudioConnected: false);

        // Act
        var vm = CreateViewModel(state);

        // Assert
        vm.Status.Should().Be(DeviceStatus.Connected);
    }

    [Fact]
    public void Status_WhenAudioActive_ReturnsAudioActive()
    {
        // Arrange
        var state = CreateTestState(
            pairedDeviceId: "device-123",
            isConnected: true,
            isAudioConnected: true);

        // Act
        var vm = CreateViewModel(state);

        // Assert
        vm.Status.Should().Be(DeviceStatus.AudioActive);
    }

    [Fact]
    public void PrimaryButtonIcon_ChangesBasedOnStatus()
    {
        // Unpaired
        var unpairedVm = CreateViewModel(CreateTestState(pairedDeviceId: null));
        unpairedVm.PrimaryButtonIcon.Should().Be("🔗");

        // Disconnected
        var disconnectedVm = CreateViewModel(CreateTestState(pairedDeviceId: "id", isConnected: false));
        disconnectedVm.PrimaryButtonIcon.Should().Be("🔌");

        // Connected
        var connectedVm = CreateViewModel(CreateTestState(pairedDeviceId: "id", isConnected: true));
        connectedVm.PrimaryButtonIcon.Should().Be("🔉");

        // Audio Active
        var audioActiveVm = CreateViewModel(CreateTestState(pairedDeviceId: "id", isConnected: true, isAudioConnected: true));
        audioActiveVm.PrimaryButtonIcon.Should().Be("⛔");
    }

    [Fact]
    public void CanExecutePrimaryAction_WhenBusy_ReturnsFalse()
    {
        // Arrange
        var state = CreateTestState(pairedDeviceId: "device-123");
        var vm = CreateViewModel(state);

        // Act
        vm.IsBusy = true;

        // Assert
        vm.CanExecutePrimaryAction.Should().BeFalse();
    }

    [Fact]
    public void CanExecutePrimaryAction_WhenUnpaired_ReturnsFalse()
    {
        // Arrange
        var state = CreateTestState(pairedDeviceId: null);
        var vm = CreateViewModel(state);

        // Assert
        vm.CanExecutePrimaryAction.Should().BeFalse();
    }

    [Fact]
    public void UpdateFromState_UpdatesAllProperties()
    {
        // Arrange
        var initialState = CreateTestState(leftBattery: 50);
        var vm = CreateViewModel(initialState);

        var updatedState = CreateTestState(leftBattery: 80, isConnected: true);

        // Act
        vm.UpdateFromState(updatedState);

        // Assert
        vm.LeftBattery.Should().Be(80);
        vm.IsConnected.Should().BeTrue();
    }

    [Fact]
    public void IsActive_WhenSeenRecently_ReturnsTrue()
    {
        // Arrange
        var state = CreateTestState();

        // Act
        var vm = CreateViewModel(state);

        // Assert
        vm.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectAsync_WhenSuccessful_SetsIsConnected()
    {
        // Arrange: Create a paired device via real event flow
        var vm = CreateViewModelFromPairedDevice();
        vm.IsConnected.Should().BeFalse();
        
        _fakeConnectionService.ConnectByDeviceIdAsync(Arg.Any<string>())
            .Returns(ConnectionResult.Connected);

        // Act
        await vm.ConnectCommand.ExecuteAsync(null);

        // Assert
        vm.IsConnected.Should().BeTrue();
        // The real DeviceStateManager received the operation signals through the event flow
    }

    [Fact]
    public async Task ConnectAsync_WhenDeviceNotFound_ClearsPairedDeviceId()
    {
        // Arrange: Create a paired device via real event flow
        var vm = CreateViewModelFromPairedDevice();
        vm.PairedDeviceId.Should().NotBeNullOrEmpty();
        
        _fakeConnectionService.ConnectByDeviceIdAsync(Arg.Any<string>())
            .Returns(ConnectionResult.DeviceNotFound);

        // Act
        await vm.ConnectCommand.ExecuteAsync(null);

        // Assert
        vm.PairedDeviceId.Should().BeNull();
        vm.Status.Should().Be(DeviceStatus.Unpaired);
    }

    [Fact]
    public async Task OpenBluetoothSettingsAsync_CallsSystemLauncherService()
    {
        // Arrange
        var state = CreateTestState(pairedDeviceId: null);
        var vm = CreateViewModel(state);
        _fakeSystemLauncherService.OpenBluetoothSettingsAsync().Returns(true);

        // Act
        await vm.OpenBluetoothSettingsCommand.ExecuteAsync(null);

        // Assert
        await _fakeSystemLauncherService.Received(1).OpenBluetoothSettingsAsync();
    }

    [Fact]
    public async Task DisconnectAsync_WhenSuccessful_SetsIsConnectedFalse()
    {
        // Arrange: Create a connected paired device via real event flow
        var pairedDevice = AirPodsStateServiceTestHelpers.CreatePairedDevice(isConnected: true);
        AirPodsStateServiceTestHelpers.RaisePairedDeviceAdded(_fixture.PairedDeviceWatcher, pairedDevice);
        
        var state = _fixture.DeviceStateManager.GetDevice(pairedDevice.ProductId);
        state.Should().NotBeNull();
        var vm = CreateViewModel(state!);
        vm.IsConnected.Should().BeTrue();
        
        _fakeConnectionService.DisconnectByDeviceIdAsync(Arg.Any<string>()).Returns(true);

        // Act
        await vm.DisconnectCommand.ExecuteAsync(null);

        // Assert
        vm.IsConnected.Should().BeFalse();
        vm.IsDefaultAudioOutput.Should().BeFalse();
    }

    private AirPodsDeviceViewModel CreateViewModel(AirPodsState state)
        {
            return new AirPodsDeviceViewModel(
                state,
                _fakeConnectionService,
                _fixture.DeviceStateManager,
                _fakeAudioOutputService,
                _fakeSystemLauncherService);
        }

        private AirPodsDeviceViewModel CreateViewModelFromPairedDevice(ushort productId = AirPodsStateServiceTestHelpers.AirPodsPro2ProductId)
        {
            // Add the device via the real event flow
            var pairedDevice = AirPodsStateServiceTestHelpers.CreatePairedDevice(productId: productId);
            AirPodsStateServiceTestHelpers.RaisePairedDeviceAdded(_fixture.PairedDeviceWatcher, pairedDevice);

            // Get the state from the real DeviceStateManager
            var state = _fixture.DeviceStateManager.GetDevice(productId);
            state.Should().NotBeNull("device should exist after being added");
            return CreateViewModel(state!);
        }

        private static AirPodsState CreateTestState(
            ushort productId = 0x2014,
            string? pairedDeviceId = "device-123",
            bool isConnected = false,
            bool isAudioConnected = false,
            int? leftBattery = 80)
        {
            return new AirPodsState
            {
                ProductId = productId,
                PairedDeviceId = pairedDeviceId,
                BluetoothAddress = 0xAABBCCDDEEFF,
                Name = "Test AirPods",
                ModelName = "AirPods Pro (2nd generation)",
                IsPaired = pairedDeviceId != null,
                IsConnected = isConnected,
                IsAudioConnected = isAudioConnected,
                LeftBattery = leftBattery,
                RightBattery = 90,
                CaseBattery = 100,
                LastSeen = DateTime.Now
            };
        }
    }
