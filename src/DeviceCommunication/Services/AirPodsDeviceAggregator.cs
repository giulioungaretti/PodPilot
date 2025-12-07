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
/// [LEGACY ARCHITECTURE] Aggregates raw BLE advertisements into logical AirPods devices.
/// Uses simple grouping by device model - all advertisements from the same model are treated as one device.
/// This approach handles rotating MAC addresses naturally and updates with the latest signal strength and battery info.
/// </summary>
/// <remarks>
/// <para><strong>?? LEGACY - This class is part of the legacy architecture using battery signatures and address rotation tracking.</strong></para>
/// <para>Kept for educational purposes and CLI Example 6 demonstrating the old approach.</para>
/// <para>For new code, use <see cref="SimpleAirPodsDiscoveryService"/> which uses Product ID-based identification.</para>
/// <para>This is the middle layer that maintains device grouping state.</para>
/// <para>Input: Raw advertisement stream from <see cref="IAdvertisementStream"/></para>
/// <para>Output: Observable stream of <see cref="DeviceStateChange"/> events</para>
/// <para>Grouping Strategy: One logical device per AirPods model (e.g., all "AirPods Pro" broadcasts = one device)</para>
/// </remarks>
[Obsolete("This class uses legacy battery signature grouping and address tracking. Use SimpleAirPodsDiscoveryService with Product ID-based identification instead. Kept for CLI Example 6.")]
public class AirPodsDeviceAggregator : IDisposable
{
    private readonly IAdvertisementStream _advertisementStream;
    private readonly BluetoothConnectionMonitor? _connectionMonitor;
    private readonly bool _ownsConnectionMonitor;
    private readonly Subject<DeviceStateChange> _deviceChangesSubject;
    private readonly Dictionary<string, DeviceGroup> _deviceGroups; // Key: ModelName
    private readonly TimeSpan _deviceTimeout;
    private readonly System.Threading.Timer _cleanupTimer;
    private readonly IDisposable _advertisementSubscription;
    private readonly IDisposable? _connectionSubscription;
    private bool _disposed;

    public IObservable<DeviceStateChange> DeviceChanges { get; }

    public AirPodsDeviceAggregator(IAdvertisementStream advertisementStream)
        : this(advertisementStream, null, TimeSpan.FromMinutes(5))
    {
    }

    /// <summary>
    /// Creates an aggregator with connection status monitoring.
    /// </summary>
    /// <param name="advertisementStream">The BLE advertisement stream.</param>
    /// <param name="connectionMonitor">Optional connection monitor for accurate IsConnected status.</param>
    public AirPodsDeviceAggregator(
        IAdvertisementStream advertisementStream,
        BluetoothConnectionMonitor? connectionMonitor)
        : this(advertisementStream, connectionMonitor, TimeSpan.FromMinutes(5))
    {
    }

    /// <summary>
    /// Creates an aggregator with full configuration options.
    /// </summary>
    /// <param name="advertisementStream">The BLE advertisement stream.</param>
    /// <param name="connectionMonitor">Optional connection monitor for accurate IsConnected status. If null, one will be created.</param>
    /// <param name="deviceTimeout">How long before a device is considered removed after last advertisement.</param>
    /// <param name="createConnectionMonitor">If true and connectionMonitor is null, creates an internal one.</param>
    public AirPodsDeviceAggregator(
        IAdvertisementStream advertisementStream,
        BluetoothConnectionMonitor? connectionMonitor,
        TimeSpan deviceTimeout,
        bool createConnectionMonitor = true)
    {
        _advertisementStream = advertisementStream ?? throw new ArgumentNullException(nameof(advertisementStream));
        _deviceTimeout = deviceTimeout;
        
        // Set up connection monitor
        if (connectionMonitor is not null)
        {
            _connectionMonitor = connectionMonitor;
            _ownsConnectionMonitor = false;
        }
        else if (createConnectionMonitor)
        {
            _connectionMonitor = new BluetoothConnectionMonitor();
            _ownsConnectionMonitor = true;
        }
        
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

        // Subscribe to connection changes if monitor is available
        if (_connectionMonitor is not null)
        {
            _connectionSubscription = _connectionMonitor.ConnectionChanges
                .Subscribe(OnConnectionChanged);
        }

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
        _connectionMonitor?.Start();
        _advertisementStream.Start();
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _advertisementStream.Stop();
        _connectionMonitor?.Stop();
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

        // Match to paired device using the connection monitor's device list
        var pairedDevices = _connectionMonitor?.GetAllPairedDevices() ?? [];
        var match = PairedDeviceMatcher.FindBestMatch(modelName, pairedDevices);
        
        // Use matched paired name if found
        var pairedDeviceName = match?.PairedName;
        var isConnected = match?.IsConnected ?? false;
        
        group.DeviceInfo = new AirPodsDeviceInfo
        {
            Address = data.Address,
            ProductId = 0, // Legacy: Product ID not available in this architecture
            PairedDeviceId = null, // Legacy: Not using device ID-based connections
            Model = modelName,
            DeviceName = pairedDeviceName ?? modelName,
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
            IsConnected = isConnected
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

    private void OnConnectionChanged(BluetoothConnectionChange change)
    {
        if (_disposed) return;

        // Find matching device group using the matcher logic
        foreach (var group in _deviceGroups.Values)
        {
            // Check if this connection change matches this device group
            var score = 0;
            
            if (change.DeviceName.Contains(group.ModelName, StringComparison.OrdinalIgnoreCase) ||
                group.ModelName.Contains(change.DeviceName, StringComparison.OrdinalIgnoreCase))
            {
                score = 100;
            }
            else if (change.DeviceName.Contains("AirPods", StringComparison.OrdinalIgnoreCase) &&
                     group.ModelName.Contains("AirPods", StringComparison.OrdinalIgnoreCase))
            {
                score = 50;
            }
            
            if (score < 50) continue;
            if (group.DeviceInfo.IsConnected == change.IsConnected) continue;

            // Update connection status
            group.DeviceInfo = group.DeviceInfo with 
            { 
                IsConnected = change.IsConnected,
                DeviceName = change.DeviceName
            };

            _deviceChangesSubject.OnNext(new DeviceStateChange
            {
                ChangeType = DeviceChangeType.Updated,
                Device = group.DeviceInfo,
                DeviceId = group.DeviceId
            });
        }
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
        _connectionSubscription?.Dispose();
        
        _deviceChangesSubject.OnCompleted();
        _deviceChangesSubject.Dispose();
        
        _deviceGroups.Clear();
        
        _advertisementStream.Dispose();
        
        if (_ownsConnectionMonitor)
        {
            _connectionMonitor?.Dispose();
        }
    }

    private class DeviceGroup
    {
        public Guid DeviceId { get; init; }
        public string ModelName { get; set; } = string.Empty;
        public AirPodsDeviceInfo DeviceInfo { get; set; } = null!;
        public DateTime LastSeen { get; set; }
    }
}
