using DeviceCommunication.Apple;
using DeviceCommunication.Models;
using DeviceCommunication.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PodPilot.Core.Models;
using TestHelpers;
using Xunit;

namespace DeviceCommunication.Tests.Services;

/// <summary>
/// Tests for AirPodsStateService to verify device deduplication behavior.
/// These tests verify the service correctly handles devices that appear via both
/// BLE advertisements AND Windows paired device APIs.
/// </summary>
public class AirPodsStateServiceTests : IAsyncLifetime
{
    private readonly ILogger<AirPodsStateService> _logger;
    private readonly IPairedDeviceWatcher _pairedDeviceWatcher;
    private readonly IBleDataProvider _bleDataProvider;
    private readonly IDefaultAudioOutputMonitorService _audioOutputMonitor;
    private readonly AirPodsStateService _sut;

    public AirPodsStateServiceTests()
    {
        _logger = Substitute.For<ILogger<AirPodsStateService>>();
        _pairedDeviceWatcher = Substitute.For<IPairedDeviceWatcher>();
        _bleDataProvider = Substitute.For<IBleDataProvider>();
        _audioOutputMonitor = Substitute.For<IDefaultAudioOutputMonitorService>();

        // Setup: Return empty list initially
        _pairedDeviceWatcher.GetPairedDevices().Returns(new List<PairedDeviceInfo>());

        _sut = new AirPodsStateService(
            _logger,
            _pairedDeviceWatcher,
            _bleDataProvider,
            _audioOutputMonitor);
    }

    public async Task InitializeAsync()
    {
        await _sut.StartAsync();
    }

    public Task DisposeAsync()
    {
        _sut.Dispose();
        return Task.CompletedTask;
    }

    #region Duplication Tests

    /// <summary>
    /// Verifies that a device appearing via BOTH paired device API AND BLE
    /// results in a SINGLE entry in GetAllDevices(), not two.
    /// 
    /// This is the core test for the deduplication issue.
    /// </summary>
    [Fact]
    public void GetAllDevices_WhenDeviceIsPairedAndSeenViaBle_ReturnsSingleDevice()
    {
        // Arrange: Simulate paired device being added
        var pairedDevice = AirPodsStateServiceTestHelpers.CreatePairedDevice();
        AirPodsStateServiceTestHelpers.RaisePairedDeviceAdded(_pairedDeviceWatcher, pairedDevice);

        // Act: Simulate BLE data arriving for the same ProductId
        var bleData = AirPodsStateServiceTestHelpers.CreateBleData();
        AirPodsStateServiceTestHelpers.RaiseBleDataReceived(_bleDataProvider, bleData);

        // Assert: Should have exactly ONE device
        var allDevices = _sut.GetAllDevices();
        allDevices.Should().HaveCount(1, "paired device + BLE data for same ProductId should be merged");
        
        var device = allDevices[0];
        device.ProductId.Should().Be(AirPodsStateServiceTestHelpers.AirPodsPro2ProductId);
        device.IsPaired.Should().BeTrue("device is from paired API");
        device.Name.Should().Be(AirPodsStateServiceTestHelpers.TestDeviceName, "should use paired device name");
        device.LeftBattery.Should().Be(80, "should have BLE enrichment data");
    }

    /// <summary>
    /// Verifies that BLE data arriving BEFORE paired device enumeration
    /// is correctly merged when the paired device appears.
    /// </summary>
    [Fact]
    public void GetAllDevices_WhenBleDataArrivesBeforePairing_MergesIntoSingleDevice()
    {
        // Arrange: BLE data arrives first (unpaired)
        var bleData = AirPodsStateServiceTestHelpers.CreateBleData();
        AirPodsStateServiceTestHelpers.RaiseBleDataReceived(_bleDataProvider, bleData);

        // Initially should have one unpaired device
        _sut.GetAllDevices().Should().HaveCount(1);
        _sut.GetAllDevices()[0].IsPaired.Should().BeFalse();

        // Act: Then paired device is enumerated
        var pairedDevice = AirPodsStateServiceTestHelpers.CreatePairedDevice();
        AirPodsStateServiceTestHelpers.RaisePairedDeviceAdded(_pairedDeviceWatcher, pairedDevice);

        // Assert: Should STILL have exactly one device, now paired
        var allDevices = _sut.GetAllDevices();
        allDevices.Should().HaveCount(1, "BLE device should be promoted to paired, not duplicated");
        
        var device = allDevices[0];
        device.IsPaired.Should().BeTrue("device should now be marked as paired");
        device.Name.Should().Be(AirPodsStateServiceTestHelpers.TestDeviceName, "should use paired device name");
    }

    /// <summary>
    /// Verifies that GetPairedDevices only returns devices that are paired,
    /// and GetAllDevices returns both paired and unpaired.
    /// </summary>
    [Fact]
    public void GetPairedDevices_OnlyReturnsPairedDevices()
    {
        // Arrange: Add one paired device
        var pairedDevice = AirPodsStateServiceTestHelpers.CreatePairedDevice();
        AirPodsStateServiceTestHelpers.RaisePairedDeviceAdded(_pairedDeviceWatcher, pairedDevice);

        // Add one unpaired device (different product ID)
        var unpairedBleData = AirPodsStateServiceTestHelpers.CreateBleData(productId: AirPodsStateServiceTestHelpers.AirPods2ProductId);
        AirPodsStateServiceTestHelpers.RaiseBleDataReceived(_bleDataProvider, unpairedBleData);

        // Assert
        var allDevices = _sut.GetAllDevices();
        var pairedDevices = _sut.GetPairedDevices();

        allDevices.Should().HaveCount(2, "should have paired + unpaired");
        pairedDevices.Should().HaveCount(1, "should only have paired device");
        pairedDevices[0].ProductId.Should().Be(AirPodsStateServiceTestHelpers.AirPodsPro2ProductId);
    }

    /// <summary>
    /// Documents the scenario where the same physical device with
    /// the same ProductId should NOT create duplicates when seen via
    /// multiple event sources.
    /// </summary>
    [Fact]
    public void StateChangedEvents_WhenBothSourcesReport_FiresForSameDevice()
    {
        // Arrange
        var receivedStates = new List<AirPodsStateChangedEventArgs>();
        _sut.StateChanged += (_, e) => receivedStates.Add(e);

        // Act: Paired device added
        var pairedDevice = AirPodsStateServiceTestHelpers.CreatePairedDevice();
        AirPodsStateServiceTestHelpers.RaisePairedDeviceAdded(_pairedDeviceWatcher, pairedDevice);

        // Act: BLE data for same device
        var bleData = AirPodsStateServiceTestHelpers.CreateBleData();
        AirPodsStateServiceTestHelpers.RaiseBleDataReceived(_bleDataProvider, bleData);

        // Assert: Events should all be for the SAME ProductId
        receivedStates.Should().HaveCountGreaterThan(0);
        receivedStates.All(e => e.State.ProductId == AirPodsStateServiceTestHelpers.AirPodsPro2ProductId)
            .Should().BeTrue("all events should be for the same device");
    }

    #endregion
}
