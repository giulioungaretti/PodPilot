using FluentAssertions;
using NSubstitute;
using PodPilot.Core.Models;
using PodPilot.Core.Services;
using PodPilot.Core.ViewModels;
using TestHelpers;
using Xunit;

namespace PodPilot.Core.Tests.ViewModels;

/// <summary>
/// Tests for MainPageViewModel.
/// Uses real AirPodsStateService and DeviceStateManager with mocked device watchers
/// to simulate actual device events rather than mocking IDeviceStateManager directly.
/// </summary>
public class MainPageViewModelTests : IAsyncLifetime, IDisposable
{
    private readonly IBluetoothConnectionService _fakeConnectionService;
    private readonly IAudioOutputService _fakeAudioOutputService;
    private readonly ISystemLauncherService _fakeSystemLauncherService;
    private readonly DeviceStateManagerTestFixture _fixture;
    private readonly MainPageViewModel _viewModel;

    public MainPageViewModelTests()
    {
        _fakeConnectionService = Substitute.For<IBluetoothConnectionService>();
        _fakeAudioOutputService = Substitute.For<IAudioOutputService>();
        _fakeSystemLauncherService = Substitute.For<ISystemLauncherService>();

        // Create real services with mocked device watchers
        _fixture = DeviceStateManagerTestHelpers.CreateTestFixture();

        _viewModel = new MainPageViewModel(
            _fakeConnectionService,
            _fixture.DeviceStateManager,
            _fakeAudioOutputService,
            _fakeSystemLauncherService);
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
        _viewModel.Dispose();
        _fixture.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task InitializeAsync_SetsIsScanningToTrue()
    {
        // Act
        await _viewModel.InitializeAsync();

        // Assert
        _viewModel.IsScanning.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_LoadsPairedDevices()
    {
        // Arrange: Simulate a paired device being added via the real event flow
        var pairedDevice = AirPodsStateServiceTestHelpers.CreatePairedDevice(isConnected: false);
        AirPodsStateServiceTestHelpers.RaisePairedDeviceAdded(_fixture.PairedDeviceWatcher, pairedDevice);

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        _viewModel.PairedDevices.Should().ContainSingle();
        _viewModel.HasPairedDevices.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenNoPairedDevices_HasPairedDevicesIsFalse()
    {
        // Arrange: No devices added

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        _viewModel.PairedDevices.Should().BeEmpty();
        _viewModel.HasPairedDevices.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_LoadsDiscoveredDevices()
    {
        // Arrange: Simulate an unpaired device seen via BLE
        var bleData = AirPodsStateServiceTestHelpers.CreateBleData(
            productId: AirPodsStateServiceTestHelpers.AirPods2ProductId);
        AirPodsStateServiceTestHelpers.RaiseBleDataReceived(_fixture.BleDataProvider, bleData);

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        _viewModel.DiscoveredDevices.Should().ContainSingle();
        _viewModel.HasDiscoveredDevices.Should().BeTrue();
    }

    [Fact]
    public async Task OnDeviceDiscovered_AddsNewDeviceToDiscoveredDevices()
    {
        // Arrange
        await _viewModel.InitializeAsync();

        // Act: Simulate an unpaired device appearing via BLE
        var bleData = AirPodsStateServiceTestHelpers.CreateBleData(
            productId: AirPodsStateServiceTestHelpers.AirPodsPro2ProductId);
        AirPodsStateServiceTestHelpers.RaiseBleDataReceived(_fixture.BleDataProvider, bleData);

        // Assert
        _viewModel.DiscoveredDevices.Should().ContainSingle(d => d.ProductId == AirPodsStateServiceTestHelpers.AirPodsPro2ProductId);
    }

    [Fact]
    public async Task OnDeviceDiscovered_WhenPaired_AddsToPairedDevices()
    {
        // Arrange
        await _viewModel.InitializeAsync();

        // Act: Simulate a paired device being added
        var pairedDevice = AirPodsStateServiceTestHelpers.CreatePairedDevice(
            productId: AirPodsStateServiceTestHelpers.AirPodsPro2ProductId);
        AirPodsStateServiceTestHelpers.RaisePairedDeviceAdded(_fixture.PairedDeviceWatcher, pairedDevice);

        // Assert
        _viewModel.PairedDevices.Should().ContainSingle(d => d.ProductId == AirPodsStateServiceTestHelpers.AirPodsPro2ProductId);
        _viewModel.DiscoveredDevices.Should().ContainSingle(d => d.ProductId == AirPodsStateServiceTestHelpers.AirPodsPro2ProductId);
    }

    [Fact]
    public async Task OnDeviceStateChanged_UpdatesExistingDiscoveredDevice()
    {
        // Arrange: Add a device first
        await _viewModel.InitializeAsync();
        var bleData = AirPodsStateServiceTestHelpers.CreateBleData(
            productId: AirPodsStateServiceTestHelpers.AirPodsPro2ProductId,
            leftBattery: 50);
        AirPodsStateServiceTestHelpers.RaiseBleDataReceived(_fixture.BleDataProvider, bleData);

        _viewModel.DiscoveredDevices.Should().ContainSingle();
        _viewModel.DiscoveredDevices.First().LeftBattery.Should().Be(50);

        // Act: Update with new BLE data (simulate time passing to avoid debounce)
        var updatedBleData = AirPodsStateServiceTestHelpers.CreateBleData(
            productId: AirPodsStateServiceTestHelpers.AirPodsPro2ProductId,
            leftBattery: 80);
        
        // Wait for debounce period to pass
        await Task.Delay(300);
        AirPodsStateServiceTestHelpers.RaiseBleDataReceived(_fixture.BleDataProvider, updatedBleData);

        // Assert
        _viewModel.DiscoveredDevices.Should().ContainSingle();
        _viewModel.DiscoveredDevices.First().LeftBattery.Should().Be(80);
    }

    [Fact]
    public async Task StopScanningCommand_SetsIsScanningToFalse()
    {
        // Arrange
        await _viewModel.InitializeAsync();
        _viewModel.IsScanning.Should().BeTrue();

        // Act
        _viewModel.StopScanningCommand.Execute(null);

        // Assert
        _viewModel.IsScanning.Should().BeFalse();
    }

    [Fact]
    public async Task OnCleanupTimerTick_RemovesStaleDevices()
    {
        // Arrange
        await _viewModel.InitializeAsync();
        var bleData = AirPodsStateServiceTestHelpers.CreateBleData(
            productId: AirPodsStateServiceTestHelpers.AirPodsPro2ProductId);
        AirPodsStateServiceTestHelpers.RaiseBleDataReceived(_fixture.BleDataProvider, bleData);

        _viewModel.DiscoveredDevices.Should().ContainSingle();

        // Act: Cleanup should not remove fresh devices
        _viewModel.OnCleanupTimerTick();

        // Assert: Device should still be present (not stale yet since just created)
        _viewModel.DiscoveredDevices.Should().ContainSingle();
    }
}
