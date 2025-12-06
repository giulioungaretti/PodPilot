using System;
using System.Collections.Generic;
using DeviceCommunication.Advertisement;
using DeviceCommunication.Apple;
using DeviceCommunication.Models;
using DeviceCommunication.Services;
using NSubstitute;
using Xunit;

namespace DeviceCommunication.Tests.Services;

public class GroupedAirPodsDiscoveryServiceTests
{
    private readonly IAdvertisementWatcher _mockWatcher;
    private readonly GroupedAirPodsDiscoveryService _service;

    public GroupedAirPodsDiscoveryServiceTests()
    {
        _mockWatcher = Substitute.For<IAdvertisementWatcher>();
        _service = new GroupedAirPodsDiscoveryService(_mockWatcher);
    }

    [Fact]
    public void SameAddress_BothPods_ShowsSingleDevice()
    {
        var discoveredCount = 0;
        var updatedCount = 0;
        _service.DeviceDiscovered += (s, e) => discoveredCount++;
        _service.DeviceUpdated += (s, e) => updatedCount++;

        RaiseAdvertisement(0x123456789ABCUL, isLeftBroadcasting: true);
        RaiseAdvertisement(0x123456789ABCUL, isLeftBroadcasting: false);

        Assert.Equal(1, discoveredCount);
        Assert.Equal(1, updatedCount);
        Assert.Single(_service.GetDiscoveredDevices());
    }

    [Fact]
    public void DifferentAddresses_BothPods_ShouldShowOneDevice()
    {
        RaiseAdvertisement(0x111111111111UL, isLeftBroadcasting: true);
        RaiseAdvertisement(0x222222222222UL, isLeftBroadcasting: false);

        Assert.Single(_service.GetDiscoveredDevices());
    }

    [Fact]
    public void DifferentModels_ShowsSeparately()
    {
        RaiseAdvertisement(0x111111111111UL, modelId: 0x2014);
        RaiseAdvertisement(0x222222222222UL, modelId: 0x2013);

        Assert.Equal(2, _service.GetDiscoveredDevices().Count);
    }

    [Fact]
    public void UnknownModel_FilteredOut()
    {
        RaiseAdvertisement(0x123456789ABCUL, modelId: 0xFFFF);

        Assert.Empty(_service.GetDiscoveredDevices());
    }

    [Fact]
    public void StartScanning_CallsWatcherStart()
    {
        _service.StartScanning();

        _mockWatcher.Received(1).Start();
    }

    [Fact]
    public void StopScanning_CallsWatcherStop()
    {
        _service.StopScanning();

        _mockWatcher.Received(1).Stop();
    }

    [Fact]
    public void Dispose_StopsAndDisposesWatcher()
    {
        _service.Dispose();

        _mockWatcher.Received(1).Stop();
        _mockWatcher.Received(1).Dispose();
    }

    [Fact]
    public void DifferentBatteryLevels_SameModel_ShowsSeparately()
    {
        RaiseAdvertisement(0x111111111111UL, leftBattery: 10, rightBattery: 10, caseBattery: 10);
        RaiseAdvertisement(0x222222222222UL, leftBattery: 5, rightBattery: 5, caseBattery: 5);

        Assert.Equal(2, _service.GetDiscoveredDevices().Count);
    }

    [Fact]
    public void SameDevice_BatteryChange_UpdatesDevice()
    {
        var discoveredCount = 0;
        var updatedCount = 0;
        _service.DeviceDiscovered += (s, e) => discoveredCount++;
        _service.DeviceUpdated += (s, e) => updatedCount++;

        RaiseAdvertisement(0x111111111111UL, leftBattery: 10, rightBattery: 10, caseBattery: 10);
        RaiseAdvertisement(0x111111111111UL, leftBattery: 9, rightBattery: 9, caseBattery: 9);

        Assert.Equal(1, discoveredCount);
        Assert.Equal(1, updatedCount);
        Assert.Single(_service.GetDiscoveredDevices());
    }

    [Fact]
    public void MultipleDevicesSameModel_DifferentBatteries_TrackedSeparately()
    {
        RaiseAdvertisement(0x111111111111UL, leftBattery: 10, rightBattery: 10, caseBattery: 10);
        RaiseAdvertisement(0x222222222222UL, leftBattery: 8, rightBattery: 8, caseBattery: 8);
        RaiseAdvertisement(0x333333333333UL, leftBattery: 5, rightBattery: 5, caseBattery: 5);

        Assert.Equal(3, _service.GetDiscoveredDevices().Count);
    }

    [Fact]
    public async Task DeviceTimeout_RaisesDeviceRemovedEvent()
    {
        AirPodsDeviceInfo? removedDevice = null;
        _service.DeviceRemoved += (s, e) => removedDevice = e;

        RaiseAdvertisement(0x123456789ABCUL);

        await Task.Delay(TimeSpan.FromSeconds(20));

        Assert.NotNull(removedDevice);
        Assert.Equal(0x123456789ABCUL, removedDevice.Address);
        Assert.Empty(_service.GetDiscoveredDevices());
    }

    [Fact]
    public async Task GroupedDevicesTimeout_RemovesEntireGroup()
    {
        var removedCount = 0;
        _service.DeviceRemoved += (s, e) => removedCount++;

        RaiseAdvertisement(0x111111111111UL, leftBattery: 10, rightBattery: 10);
        RaiseAdvertisement(0x222222222222UL, leftBattery: 10, rightBattery: 10);

        await Task.Delay(TimeSpan.FromSeconds(20));

        Assert.Equal(1, removedCount);
        Assert.Empty(_service.GetDiscoveredDevices());
    }

    private void RaiseAdvertisement(
        ulong address,
        bool isLeftBroadcasting = true,
        ushort modelId = 0x2014,
        byte? leftBattery = 8,
        byte? rightBattery = 8,
        byte? caseBattery = 9)
    {
        var message = new byte[27];
        message[0] = 0x07;
        message[1] = 0x19;
        message[2] = 0x01;
        message[3] = (byte)(modelId & 0xFF);
        message[4] = (byte)(modelId >> 8);
        message[5] = (byte)(isLeftBroadcasting ? 0x20 : 0x00);

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
        message[7] = battery2;
        message[8] = 0x00;

        var data = new AdvertisementReceivedData
        {
            Address = address,
            Rssi = -50,
            Timestamp = DateTimeOffset.Now,
            ManufacturerData = new Dictionary<ushort, byte[]>
            {
                [AppleConstants.VENDOR_ID] = message
            }
        };

        _mockWatcher.AdvertisementReceived +=
            Raise.Event<EventHandler<AdvertisementReceivedData>>(_mockWatcher, data);
    }
}
