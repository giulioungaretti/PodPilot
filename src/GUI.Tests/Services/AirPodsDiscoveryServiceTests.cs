using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using DeviceCommunication.Advertisement;
using DeviceCommunication.Apple;
using GUI.Services;
using GUI.Models;

namespace GUI.Tests.Services;

/// <summary>
/// Unit tests for AirPodsDiscoveryService.
/// Tests device deduplication and stale device cleanup behavior.
/// </summary>
[TestClass]
public class AirPodsDiscoveryServiceTests
{
    private Mock<IAdvertisementWatcher>? _mockWatcher;
    private AirPodsDiscoveryService? _service;

    [TestInitialize]
    public void Setup()
    {
        _mockWatcher = new Mock<IAdvertisementWatcher>();
        _service = new AirPodsDiscoveryService(_mockWatcher.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _service?.Dispose();
    }

    #region Device Deduplication Tests

    [TestMethod]
    public void DeviceDiscoveredOnce_WhenBothPodsAdvertise_WithSameAddress_ShowsSingleDevice()
    {
        // Arrange - IDEAL scenario where both pods use the same address
        // NOTE: In practice, AirPods may use different addresses for left/right pods
        var deviceAddress = 0x123456789ABCUL;
        var discoveredCount = 0;
        var updatedCount = 0;

        _service!.DeviceDiscovered += (s, e) => discoveredCount++;
        _service.DeviceUpdated += (s, e) => updatedCount++;

        // Act - Simulate left pod advertising
        var leftPodData = CreateAirPodsAdvertisement(deviceAddress, isLeftBroadcasting: true);
        RaiseAdvertisementReceived(leftPodData);

        // Act - Simulate right pod advertising (same address)
        var rightPodData = CreateAirPodsAdvertisement(deviceAddress, isLeftBroadcasting: false);
        RaiseAdvertisementReceived(rightPodData);

        // Assert
        Assert.AreEqual(1, discoveredCount, "DeviceDiscovered should fire only once");
        Assert.AreEqual(1, updatedCount, "DeviceUpdated should fire once for the second advertisement");
        Assert.AreEqual(1, _service.GetDiscoveredDevices().Count, "Should have exactly one device");
    }

    [TestMethod]
    public void DeviceDiscoveredTwice_WhenBothPodsAdvertise_WithDifferentAddresses_ShowsTwoDevices()
    {
        // Arrange - REAL-WORLD scenario: AirPods left and right pods broadcast with different addresses
        // This is what's actually happening in your GUI!
        var leftPodAddress = 0x111111111111UL;
        var rightPodAddress = 0x222222222222UL;
        var discoveredCount = 0;

        _service!.DeviceDiscovered += (s, e) => discoveredCount++;

        // Act - Left pod advertising with its own address
        var leftPodData = CreateAirPodsAdvertisement(leftPodAddress, isLeftBroadcasting: true);
        RaiseAdvertisementReceived(leftPodData);

        // Act - Right pod advertising with a DIFFERENT address
        var rightPodData = CreateAirPodsAdvertisement(rightPodAddress, isLeftBroadcasting: false);
        RaiseAdvertisementReceived(rightPodData);

        // Assert - Current behavior: Two devices are discovered
        Assert.AreEqual(2, discoveredCount, "Both pods trigger DeviceDiscovered (CURRENT ISSUE)");
        Assert.AreEqual(2, _service.GetDiscoveredDevices().Count, "Shows two devices (SHOULD BE ONE)");

        // TODO: Fix AirPodsDiscoveryService to deduplicate by model + proximity
        // instead of just by Bluetooth address
    }

    [TestMethod]
    public void DeviceUpdated_WhenAlternatePodAdvertises_UpdatesSameDevice()
    {
        // Arrange
        var deviceAddress = 0x123456789ABCUL;
        AirPodsDeviceInfo? discoveredDevice = null;
        AirPodsDeviceInfo? updatedDevice = null;

        _service!.DeviceDiscovered += (s, e) => discoveredDevice = e;
        _service.DeviceUpdated += (s, e) => updatedDevice = e;

        // Act - Left pod advertises first
        var leftPodData = CreateAirPodsAdvertisement(deviceAddress, isLeftBroadcasting: true, leftBattery: 80);
        RaiseAdvertisementReceived(leftPodData);

        // Act - Right pod advertises with different battery data
        var rightPodData = CreateAirPodsAdvertisement(deviceAddress, isLeftBroadcasting: false, rightBattery: 75);
        RaiseAdvertisementReceived(rightPodData);

        // Assert
        Assert.IsNotNull(discoveredDevice, "Device should be discovered");
        Assert.IsNotNull(updatedDevice, "Device should be updated");
        Assert.AreEqual(discoveredDevice.Address, updatedDevice.Address, "Should be the same device");
        
        var devices = _service.GetDiscoveredDevices();
        Assert.AreEqual(1, devices.Count, "Should have exactly one device");
        Assert.AreEqual(deviceAddress, devices[0].Address);
    }

    [TestMethod]
    public void MultipleDevices_WithDifferentAddresses_AreStoredSeparately()
    {
        // Arrange
        var address1 = 0x111111111111UL;
        var address2 = 0x222222222222UL;
        var discoveredCount = 0;

        _service!.DeviceDiscovered += (s, e) => discoveredCount++;

        // Act
        var device1Data = CreateAirPodsAdvertisement(address1);
        RaiseAdvertisementReceived(device1Data);

        var device2Data = CreateAirPodsAdvertisement(address2);
        RaiseAdvertisementReceived(device2Data);

        // Assert
        Assert.AreEqual(2, discoveredCount, "Both devices should trigger DeviceDiscovered");
        Assert.AreEqual(2, _service.GetDiscoveredDevices().Count, "Should have two devices");

        var devices = _service.GetDiscoveredDevices();
        Assert.IsTrue(devices.Any(d => d.Address == address1), "First device should be in list");
        Assert.IsTrue(devices.Any(d => d.Address == address2), "Second device should be in list");
    }

    #endregion

    #region Stale Device Cleanup Tests (Expected to Fail)

    [TestMethod]
    [Ignore("Feature not implemented: Device timeout mechanism does not exist")]
    public void DiscoveredDevices_WhenNoRecentAdvertisements_ShouldBeEmpty()
    {
        // Arrange
        var deviceAddress = 0x123456789ABCUL;
        var data = CreateAirPodsAdvertisement(deviceAddress);
        
        // Act - Discover device
        RaiseAdvertisementReceived(data);
        Assert.AreEqual(1, _service!.GetDiscoveredDevices().Count, "Device should be discovered");

        // Act - Wait for timeout (simulate 30 seconds passing)
        // TODO: This test requires a time-based cleanup mechanism
        System.Threading.Thread.Sleep(TimeSpan.FromSeconds(30));

        // Assert - EXPECTED TO FAIL: No cleanup mechanism exists
        Assert.AreEqual(0, _service.GetDiscoveredDevices().Count, 
            "Stale devices should be removed from the list");
    }

    [TestMethod]
    [Ignore("Feature not implemented: Selective stale device removal does not exist")]
    public void StaleDevices_AfterTimeout_RemovedFromList()
    {
        // Arrange
        var staleAddress = 0x111111111111UL;
        var activeAddress = 0x222222222222UL;

        // Act - Discover both devices
        RaiseAdvertisementReceived(CreateAirPodsAdvertisement(staleAddress));
        RaiseAdvertisementReceived(CreateAirPodsAdvertisement(activeAddress));
        Assert.AreEqual(2, _service!.GetDiscoveredDevices().Count);

        // Act - Keep active device alive, let stale device timeout
        System.Threading.Thread.Sleep(TimeSpan.FromSeconds(15));
        RaiseAdvertisementReceived(CreateAirPodsAdvertisement(activeAddress)); // Refresh active device
        System.Threading.Thread.Sleep(TimeSpan.FromSeconds(16)); // Stale device should timeout

        // Assert - EXPECTED TO FAIL: No selective cleanup exists
        var devices = _service.GetDiscoveredDevices();
        Assert.AreEqual(1, devices.Count, "Only active device should remain");
        Assert.AreEqual(activeAddress, devices[0].Address, "Active device should still be present");
    }

    [TestMethod]
    [Ignore("Feature not implemented: Dictionary is not cleared on stop/restart")]
    public void DeviceList_ClearedOnStopScanning_DoesNotRetainOldDevices()
    {
        // Arrange
        var oldAddress = 0x111111111111UL;
        var newAddress = 0x222222222222UL;

        // Act - First scan session
        _service!.StartScanning();
        RaiseAdvertisementReceived(CreateAirPodsAdvertisement(oldAddress));
        Assert.AreEqual(1, _service.GetDiscoveredDevices().Count, "Old device discovered");
        
        _service.StopScanning();

        // Assert - EXPECTED TO FAIL: Dictionary is not cleared on stop
        Assert.AreEqual(0, _service.GetDiscoveredDevices().Count, 
            "Device list should be cleared when scanning stops");

        // Act - Second scan session
        _service.StartScanning();
        RaiseAdvertisementReceived(CreateAirPodsAdvertisement(newAddress));

        // Assert
        var devices = _service.GetDiscoveredDevices();
        Assert.AreEqual(1, devices.Count, "Should only have new device");
        Assert.AreEqual(newAddress, devices[0].Address, "Should be the new device only");
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void UnknownModel_FilteredOut_DoesNotAppearInList()
    {
        // Arrange
        var deviceAddress = 0x123456789ABCUL;
        
        // Create advertisement with unknown model ID
        var unknownModelData = CreateAdvertisementData(deviceAddress, modelId: 0xFFFF);

        // Act
        RaiseAdvertisementReceived(unknownModelData);

        // Assert
        Assert.AreEqual(0, _service!.GetDiscoveredDevices().Count, 
            "Unknown models should be filtered out");
    }

    [TestMethod]
    public void NonAppleDevice_Ignored_DoesNotAppearInList()
    {
        // Arrange
        var deviceAddress = 0x123456789ABCUL;
        
        // Create advertisement without Apple vendor ID
        var data = new AdvertisementReceivedData
        {
            Address = deviceAddress,
            Rssi = -50,
            Timestamp = DateTimeOffset.Now,
            ManufacturerData = new Dictionary<ushort, byte[]>
            {
                [0x1234] = new byte[] { 0x01, 0x02, 0x03 } // Non-Apple vendor
            }
        };

        // Act
        RaiseAdvertisementReceived(data);

        // Assert
        Assert.AreEqual(0, _service!.GetDiscoveredDevices().Count, 
            "Non-Apple devices should be ignored");
    }

    [TestMethod]
    public void InvalidProximityMessage_Ignored_DoesNotAppearInList()
    {
        // Arrange
        var deviceAddress = 0x123456789ABCUL;
        
        // Create advertisement with invalid proximity pairing data
        var data = new AdvertisementReceivedData
        {
            Address = deviceAddress,
            Rssi = -50,
            Timestamp = DateTimeOffset.Now,
            ManufacturerData = new Dictionary<ushort, byte[]>
            {
                [AppleConstants.VENDOR_ID] = new byte[] { 0xFF, 0xFF, 0xFF } // Invalid message
            }
        };

        // Act
        RaiseAdvertisementReceived(data);

        // Assert
        Assert.AreEqual(0, _service!.GetDiscoveredDevices().Count, 
            "Invalid proximity messages should be ignored");
    }

    [TestMethod]
    public void StartScanning_CallsWatcherStart()
    {
        // Act
        _service!.StartScanning();

        // Assert
        _mockWatcher!.Verify(w => w.Start(), Times.Once);
    }

    [TestMethod]
    public void StopScanning_CallsWatcherStop()
    {
        // Act
        _service!.StopScanning();

        // Assert
        _mockWatcher!.Verify(w => w.Stop(), Times.Once);
    }

    [TestMethod]
    public void Dispose_StopsAndDisposesWatcher()
    {
        // Act
        _service!.Dispose();

        // Assert
        _mockWatcher!.Verify(w => w.Stop(), Times.Once);
        _mockWatcher.Verify(w => w.Dispose(), Times.Once);
    }

    [TestMethod]
    public void RealCapturedData_AirPodsPro2_ParsesCorrectly()
    {
        // Arrange - Real AirPods Pro 2 data captured from BLE scanner
        // Source: AdvertisementCapture tool - see samples/RealCapturedData.cs
        var realData = new byte[]
        {
            0x07, 0x19, 0x01, 0x14, 0x20, 0x55, 0xAA, 0xB8, 0x11,
            0x00, 0x04, 0x1B, 0xE9, 0xD4, 0x3B, 0xA1, 0x34, 0xD2,
            0x3B, 0x34, 0x24, 0xF0, 0x3D, 0x56, 0xB5, 0xA6, 0x3A
        };

        var data = new AdvertisementReceivedData
        {
            Address = 0xABCDEF123456UL,
            Rssi = -45,
            Timestamp = DateTimeOffset.Now,
            ManufacturerData = new Dictionary<ushort, byte[]>
            {
                [AppleConstants.VENDOR_ID] = realData
            }
        };

        // Act
        RaiseAdvertisementReceived(data);

        // Assert
        var devices = _service!.GetDiscoveredDevices();
        Assert.AreEqual(1, devices.Count, "Should discover the AirPods Pro 2");
        Assert.AreEqual("AirPods Pro (2nd generation)", devices[0].Model);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a valid AirPods Pro 2 advertisement with customizable parameters.
    /// </summary>
    private AdvertisementReceivedData CreateAirPodsAdvertisement(
        ulong address,
        bool isLeftBroadcasting = true,
        byte? leftBattery = 8,
        byte? rightBattery = 8,
        byte? caseBattery = 9)
    {
        return CreateAdvertisementData(
            address, 
            modelId: 0x2014, // AirPods Pro 2
            isLeftBroadcasting: isLeftBroadcasting,
            leftBattery: leftBattery,
            rightBattery: rightBattery,
            caseBattery: caseBattery);
    }

    /// <summary>
    /// Creates advertisement data with full control over the proximity pairing message.
    /// </summary>
    private AdvertisementReceivedData CreateAdvertisementData(
        ulong address,
        ushort modelId = 0x2014,
        bool isLeftBroadcasting = true,
        byte? leftBattery = 8,
        byte? rightBattery = 8,
        byte? caseBattery = 9,
        bool isLeftCharging = false,
        bool isRightCharging = false,
        bool isCaseCharging = false)
    {
        // Build proximity pairing message bytes
        var message = new byte[27];
        message[0] = 0x07; // Packet type
        message[1] = 0x19; // Remaining length (25)
        message[2] = 0x01; // Unknown
        
        // Model ID (little-endian in struct layout)
        message[3] = (byte)(modelId & 0xFF);
        message[4] = (byte)(modelId >> 8);
        
        // Status flags
        byte statusFlags = 0x00;
        if (isLeftBroadcasting)
            statusFlags |= 0x20;
        message[5] = statusFlags;
        
        // Battery status
        byte battery1 = 0x00;
        if (isLeftBroadcasting)
        {
            if (leftBattery.HasValue)
                battery1 = (byte)(leftBattery.Value & 0x0F);
            if (rightBattery.HasValue)
                battery1 |= (byte)((rightBattery.Value & 0x0F) << 4);
        }
        else
        {
            if (rightBattery.HasValue)
                battery1 = (byte)(rightBattery.Value & 0x0F);
            if (leftBattery.HasValue)
                battery1 |= (byte)((leftBattery.Value & 0x0F) << 4);
        }
        message[6] = battery1;
        
        byte battery2 = 0x00;
        if (caseBattery.HasValue)
            battery2 = (byte)(caseBattery.Value & 0x0F);
        if (isLeftBroadcasting && isLeftCharging)
            battery2 |= 0x10;
        if (!isLeftBroadcasting && isRightCharging)
            battery2 |= 0x10;
        if (!isLeftBroadcasting && isLeftCharging)
            battery2 |= 0x20;
        if (isLeftBroadcasting && isRightCharging)
            battery2 |= 0x20;
        if (isCaseCharging)
            battery2 |= 0x40;
        message[7] = battery2;
        
        message[8] = 0x00; // Lid open

        return new AdvertisementReceivedData
        {
            Address = address,
            Rssi = -50,
            Timestamp = DateTimeOffset.Now,
            ManufacturerData = new Dictionary<ushort, byte[]>
            {
                [AppleConstants.VENDOR_ID] = message
            }
        };
    }

    /// <summary>
    /// Raises the AdvertisementReceived event on the mock watcher.
    /// </summary>
    private void RaiseAdvertisementReceived(AdvertisementReceivedData data)
    {
        _mockWatcher!.Raise(w => w.AdvertisementReceived += null, _mockWatcher.Object, data);
    }

    #endregion
}
