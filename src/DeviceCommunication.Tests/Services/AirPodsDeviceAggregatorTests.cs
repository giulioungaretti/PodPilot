using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DeviceCommunication.Advertisement;
using DeviceCommunication.Apple;
using DeviceCommunication.Models;
using DeviceCommunication.Services;
using Xunit;

namespace DeviceCommunication.Tests.Services;

public class AirPodsDeviceAggregatorTests
{
    [Fact]
    public void Constructor_WithNullStream_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AirPodsDeviceAggregator(null!));
    }

    [Fact]
    public async Task DeviceChanges_EmitsAddedEvent_WhenNewDeviceDiscovered()
    {
        // Arrange
        var mockStream = new MockAdvertisementStream();
        using var aggregator = new AirPodsDeviceAggregator(mockStream);

        var receivedChanges = new List<DeviceStateChange>();
        var subscription = aggregator.DeviceChanges.Subscribe(receivedChanges.Add);

        aggregator.Start();

        // Act
        var advertisement = CreateAirPodsAdvertisement(address: 0x123456789ABC);
        mockStream.EmitAdvertisement(advertisement);

        await Task.Delay(100); // Give time for processing

        // Assert
        Assert.Single(receivedChanges);
        Assert.Equal(DeviceChangeType.Added, receivedChanges[0].ChangeType);
        Assert.Equal(0x123456789ABCul, receivedChanges[0].Device.Address);

        subscription.Dispose();
    }

    [Fact]
    public async Task DeviceChanges_EmitsUpdatedEvent_WhenExistingDeviceSeenAgain()
    {
        // Arrange
        var mockStream = new MockAdvertisementStream();
        using var aggregator = new AirPodsDeviceAggregator(mockStream);

        var receivedChanges = new List<DeviceStateChange>();
        var subscription = aggregator.DeviceChanges.Subscribe(receivedChanges.Add);

        aggregator.Start();

        // Act - First advertisement
        var advertisement1 = CreateAirPodsAdvertisement(address: 0x123456789ABC, leftBattery: 10);
        mockStream.EmitAdvertisement(advertisement1);
        await Task.Delay(100);

        // Act - Second advertisement (same device, different signal)
        var advertisement2 = CreateAirPodsAdvertisement(address: 0x123456789ABC, leftBattery: 10, rssi: -60);
        mockStream.EmitAdvertisement(advertisement2);
        await Task.Delay(100);

        // Assert
        Assert.Equal(2, receivedChanges.Count);
        Assert.Equal(DeviceChangeType.Added, receivedChanges[0].ChangeType);
        Assert.Equal(DeviceChangeType.Updated, receivedChanges[1].ChangeType);
        Assert.Equal(receivedChanges[0].DeviceId, receivedChanges[1].DeviceId); // Same device ID

        subscription.Dispose();
    }

    [Fact]
    public async Task DeviceChanges_GroupsByModelName_WhenDifferentAddressesSameModel()
    {
        // Arrange
        var mockStream = new MockAdvertisementStream();
        using var aggregator = new AirPodsDeviceAggregator(
            mockStream,
            connectionMonitor: null,
            deviceTimeout: TimeSpan.FromSeconds(15),
            createConnectionMonitor: false);

        var receivedChanges = new List<DeviceStateChange>();
        var subscription = aggregator.DeviceChanges.Subscribe(receivedChanges.Add);

        aggregator.Start();

        // Act - Two advertisements with different addresses, same model (simplified grouping)
        var advertisement1 = CreateAirPodsAdvertisement(address: 0x111111111111, leftBattery: 8, rightBattery: 9);
        mockStream.EmitAdvertisement(advertisement1);
        await Task.Delay(100);

        var advertisement2 = CreateAirPodsAdvertisement(address: 0x222222222222, leftBattery: 7, rightBattery: 8);
        mockStream.EmitAdvertisement(advertisement2);
        await Task.Delay(100);

        // Assert - Should be treated as same device (Updated, not Added) because same model
        Assert.Equal(2, receivedChanges.Count);
        Assert.Equal(DeviceChangeType.Added, receivedChanges[0].ChangeType);
        Assert.Equal(DeviceChangeType.Updated, receivedChanges[1].ChangeType);
        Assert.Equal(receivedChanges[0].DeviceId, receivedChanges[1].DeviceId); // Same device ID

        subscription.Dispose();
    }

    [Fact]
    public void GetCurrentDevices_ReturnsEmptyList_WhenNoDevicesDiscovered()
    {
        // Arrange
        var mockStream = new MockAdvertisementStream();
        using var aggregator = new AirPodsDeviceAggregator(mockStream);

        // Act
        var devices = aggregator.GetCurrentDevices();

        // Assert
        Assert.Empty(devices);
    }

    private static AdvertisementReceivedData CreateAirPodsAdvertisement(
        ulong address = 0x123456789ABC,
        int leftBattery = 10,
        int rightBattery = 10,
        int caseBattery = 10,
        short rssi = -50)
    {
        // Create AirPods Pro advertisement data
        var manufacturerData = CreateAirPodsProManufacturerData(leftBattery, rightBattery, caseBattery);

        return new AdvertisementReceivedData
        {
            Address = address,
            Rssi = rssi,
            Timestamp = DateTime.Now,
            ManufacturerData = new Dictionary<ushort, byte[]>
            {
                { AppleConstants.VENDOR_ID, manufacturerData }
            }
        };
    }

    private static byte[] CreateAirPodsProManufacturerData(int leftBattery, int rightBattery, int caseBattery)
    {
        // Simplified AirPods Pro advertisement structure
        var data = new byte[27];
        data[0] = 0x07; // Length
        data[1] = 0x19; // Type: Proximity Pairing
        data[2] = 0x01; // Sub-type
        data[3] = 0x0E; // AirPods Pro model
        data[4] = 0x20; // Status
        
        // Battery levels (simplified)
        data[7] = (byte)((leftBattery << 4) | rightBattery);
        data[8] = (byte)(caseBattery << 4);

        return data;
    }

    private class MockAdvertisementStream : IAdvertisementStream
    {
        private readonly List<IObserver<AdvertisementReceivedData>> _observers = new();
        private bool _disposed;

        public IObservable<AdvertisementReceivedData> Advertisements { get; }
        public AdvertisementWatcherStatus Status { get; private set; }

        public MockAdvertisementStream()
        {
            Advertisements = Observable.Create<AdvertisementReceivedData>(observer =>
            {
                _observers.Add(observer);
                return () => _observers.Remove(observer);
            });
        }

        public void Start()
        {
            Status = AdvertisementWatcherStatus.Started;
        }

        public void Stop()
        {
            Status = AdvertisementWatcherStatus.Stopped;
        }

        public void EmitAdvertisement(AdvertisementReceivedData data)
        {
            foreach (var observer in _observers.ToList())
            {
                observer.OnNext(data);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var observer in _observers.ToList())
            {
                observer.OnCompleted();
            }
            _observers.Clear();
        }
    }
}
