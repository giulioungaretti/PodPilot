using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DeviceCommunication.Advertisement;
using DeviceCommunication.Apple;
using DeviceCommunication.Models;

namespace DeviceCommunication.Services;

/// <summary>
/// Aggregates raw BLE advertisements into logical AirPods devices.
/// Uses simple grouping by device model - all advertisements from the same model are treated as one device.
/// This approach handles rotating MAC addresses naturally and updates with the latest signal strength and battery info.
/// </summary>
/// <remarks>
/// This is the middle layer that maintains device grouping state.
/// Input: Raw advertisement stream from <see cref="IAdvertisementStream"/>
/// Output: Observable stream of <see cref="DeviceStateChange"/> events
/// Grouping Strategy: One logical device per AirPods model (e.g., all "AirPods Pro" broadcasts = one device)
/// </remarks>
public class AirPodsDeviceAggregator : IDisposable
{
    private readonly IAdvertisementStream _advertisementStream;
    private readonly Subject<DeviceStateChange> _deviceChangesSubject;
    private readonly Dictionary<string, DeviceGroup> _deviceGroups; // Key: ModelName
    private readonly TimeSpan _deviceTimeout;
    private readonly System.Threading.Timer _cleanupTimer;
    private readonly IDisposable _advertisementSubscription;
    private bool _disposed;

    public IObservable<DeviceStateChange> DeviceChanges { get; }

    public AirPodsDeviceAggregator(IAdvertisementStream advertisementStream)
        : this(advertisementStream, TimeSpan.FromMinutes(5)) // 5-minute timeout for device removal
    {
    }

    public AirPodsDeviceAggregator(
        IAdvertisementStream advertisementStream,
        TimeSpan deviceTimeout)
    {
        _advertisementStream = advertisementStream ?? throw new ArgumentNullException(nameof(advertisementStream));
        _deviceTimeout = deviceTimeout;
        
        _deviceChangesSubject = new Subject<DeviceStateChange>();
        DeviceChanges = _deviceChangesSubject.AsObservable();
        
        _deviceGroups = new Dictionary<string, DeviceGroup>();

        // Subscribe to advertisement stream
        _advertisementSubscription = _advertisementStream.Advertisements
            .Subscribe(
                onNext: OnAdvertisementReceived,
                onError: ex => _deviceChangesSubject.OnError(ex),
                onCompleted: () => _deviceChangesSubject.OnCompleted()
            );

        // Start cleanup timer
        _cleanupTimer = new System.Threading.Timer(
            CleanupExpiredDevices,
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1));
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _advertisementStream.Start();
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _advertisementStream.Stop();
    }

    public IReadOnlyList<AirPodsDeviceInfo> GetCurrentDevices()
    {
        var now = DateTime.Now;
        return _deviceGroups.Values
            .Where(group => now - group.LastSeen <= _deviceTimeout)
            .Select(group => group.DeviceInfo)
            .ToList();
    }

    private void OnAdvertisementReceived(AdvertisementReceivedData data)
    {
        // Filter for Apple devices
        if (!data.ManufacturerData.TryGetValue(AppleConstants.VENDOR_ID, out var appleData))
            return;

        // Parse proximity pairing message
        var message = ProximityPairingMessage.FromManufacturerData(appleData);
        if (!message.HasValue)
            return;

        var airPods = message.Value;
        var model = airPods.GetModel();

        if (model == AppleDeviceModel.Unknown)
            return;

        var modelName = GetModelDisplayName(model);
        var now = DateTime.Now;

        // Simple grouping: one device per model name
        bool isNewDevice = !_deviceGroups.ContainsKey(modelName);
        
        DeviceGroup group;
        if (isNewDevice)
        {
            group = new DeviceGroup
            {
                DeviceId = Guid.NewGuid(),
                ModelName = modelName,
                LastSeen = now
            };
            _deviceGroups[modelName] = group;
        }
        else
        {
            group = _deviceGroups[modelName];
            group.LastSeen = now;
        }

        // Update device info
        group.DeviceInfo = new AirPodsDeviceInfo
        {
            Address = data.Address,
            Model = modelName,
            DeviceName = modelName,
            LeftBattery = airPods.GetLeftBattery() * 10,
            RightBattery = airPods.GetRightBattery() * 10,
            CaseBattery = airPods.GetCaseBattery() * 10,
            IsLeftCharging = airPods.IsLeftCharging(),
            IsRightCharging = airPods.IsRightCharging(),
            IsCaseCharging = airPods.IsCaseCharging(),
            IsLeftInEar = airPods.IsLeftInEar(),
            IsRightInEar = airPods.IsRightInEar(),
            IsLidOpen = airPods.IsLidOpened(),
            SignalStrength = data.Rssi,
            LastSeen = now,
            IsConnected = false
        };

        // Emit state change
        var changeType = isNewDevice ? DeviceChangeType.Added : DeviceChangeType.Updated;
        var stateChange = new DeviceStateChange
        {
            ChangeType = changeType,
            Device = group.DeviceInfo,
            DeviceId = group.DeviceId
        };

        _deviceChangesSubject.OnNext(stateChange);
    }

    private void CleanupExpiredDevices(object? state)
    {
        if (_disposed) return;

        var now = DateTime.Now;
        var expiredGroups = _deviceGroups
            .Where(kvp => now - kvp.Value.LastSeen > _deviceTimeout)
            .Select(kvp => kvp.Value)
            .ToList();

        foreach (var group in expiredGroups)
        {
            _deviceGroups.Remove(group.ModelName);
            
            var stateChange = new DeviceStateChange
            {
                ChangeType = DeviceChangeType.Removed,
                Device = group.DeviceInfo,
                DeviceId = group.DeviceId
            };

            _deviceChangesSubject.OnNext(stateChange);
        }
    }

    private string GetModelDisplayName(AppleDeviceModel model)
    {
        return model switch
        {
            AppleDeviceModel.AirPods1 => "AirPods (1st generation)",
            AppleDeviceModel.AirPods2 => "AirPods (2nd generation)",
            AppleDeviceModel.AirPods3 => "AirPods (3rd generation)",
            AppleDeviceModel.AirPodsPro => "AirPods Pro",
            AppleDeviceModel.AirPodsPro2 => "AirPods Pro (2nd generation)",
            AppleDeviceModel.AirPodsPro2UsbC => "AirPods Pro (2nd gen, USB-C)",
            AppleDeviceModel.AirPodsMax => "AirPods Max",
            AppleDeviceModel.BeatsFitPro => "Beats Fit Pro",
            _ => "Unknown AirPods"
        };
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        
        _cleanupTimer.Dispose();
        _advertisementSubscription.Dispose();
        
        _deviceChangesSubject.OnCompleted();
        _deviceChangesSubject.Dispose();
        
        _deviceGroups.Clear();
        
        _advertisementStream.Dispose();
    }

    private class DeviceGroup
    {
        public Guid DeviceId { get; init; }
        public string ModelName { get; set; } = string.Empty;
        public AirPodsDeviceInfo DeviceInfo { get; set; } = null!;
        public DateTime LastSeen { get; set; }
    }
}
