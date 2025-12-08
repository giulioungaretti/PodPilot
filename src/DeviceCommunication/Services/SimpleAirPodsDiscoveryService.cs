using System.Diagnostics;
using DeviceCommunication.Advertisement;
using DeviceCommunication.Apple;
using DeviceCommunication.Models;

namespace DeviceCommunication.Services;

/// <summary>
/// Simplified implementation of <see cref="IAirPodsDiscoveryService"/> that uses Product ID for device identification.
/// Eliminates complex battery signature grouping and MAC address tracking by leveraging stable Product IDs.
/// </summary>
public class SimpleAirPodsDiscoveryService : IAirPodsDiscoveryService
{
    private readonly IAdvertisementWatcher _watcher;
    private readonly IPairedDeviceLookupService _pairedDeviceLookup;
    private readonly Dictionary<ushort, AirPodsDeviceInfo> _devicesByProductId;
    private readonly TimeSpan _deviceTimeout = TimeSpan.FromMinutes(5);
    private bool _disposed;

    public event EventHandler<AirPodsDeviceInfo>? DeviceDiscovered;
    public event EventHandler<AirPodsDeviceInfo>? DeviceUpdated;

    /// <summary>
    /// Creates a new instance with the specified dependencies.
    /// </summary>
    /// <param name="watcher">The BLE advertisement watcher.</param>
    /// <param name="pairedDeviceLookup">The paired device lookup service with caching.</param>
    public SimpleAirPodsDiscoveryService(IAdvertisementWatcher watcher, IPairedDeviceLookupService pairedDeviceLookup)
    {
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        _pairedDeviceLookup = pairedDeviceLookup ?? throw new ArgumentNullException(nameof(pairedDeviceLookup));
        _devicesByProductId = new Dictionary<ushort, AirPodsDeviceInfo>();
        _watcher.AdvertisementReceived += OnAdvertisementReceived;
    }

    public void StartScanning()
    {
        LogDebug("StartScanning called");
        _watcher.Start();
    }

    public void StopScanning()
    {
        LogDebug("StopScanning called");
        _watcher.Stop();
    }

    public IReadOnlyList<AirPodsDeviceInfo> GetDiscoveredDevices()
    {
        var now = DateTime.Now;
        return _devicesByProductId.Values
            .Where(device => now - device.LastSeen <= _deviceTimeout)
            .ToList();
    }

    private async void OnAdvertisementReceived(object? sender, AdvertisementReceivedData data)
    {
        if (!data.ManufacturerData.TryGetValue(AppleConstants.VENDOR_ID, out var appleData))
            return;

        var message = ProximityPairingMessage.FromManufacturerData(appleData);
        if (!message.HasValue)
            return;

        var airPods = message.Value;
        var model = airPods.GetModel();

        if (model == AppleDeviceModel.Unknown)
            return;

        var productId = model.GetProductId();
        if (!productId.HasValue)
            return;

        // Check if this is a new device or an update
        bool isNewDevice = !_devicesByProductId.ContainsKey(productId.Value);
        
        if (isNewDevice)
        {
            LogDebug($"=== NEW DEVICE DETECTED ===");
            LogDebug($"Model: {model}");
            LogDebug($"ProductId: 0x{productId.Value:X4}");
            LogDebug($"BLE Address: {data.Address:X12}");
            LogDebug($"Signal: {data.Rssi} dBm");
        }

        // Try to find paired device with matching Product ID (cached lookup)
        var pairedDevice = await _pairedDeviceLookup.FindByProductIdAsync(productId.Value);
        
        if (isNewDevice)
        {
            if (pairedDevice != null)
            {
                LogDebug($"Paired device found: {pairedDevice.Name} (Connected={pairedDevice.IsConnected})");
            }
            else
            {
                LogDebug($"WARNING: No paired device found for ProductId 0x{productId.Value:X4}");
            }
        }

        var deviceInfo = new AirPodsDeviceInfo
        {
            Address = data.Address,
            ProductId = productId.Value,
            Model = model.GetDisplayName(),
            DeviceName = pairedDevice?.Name ?? model.GetDisplayName(),
            PairedDeviceId = pairedDevice?.Id,
            PairedBluetoothAddress = pairedDevice?.Address,
            LeftBattery = airPods.GetLeftBattery() * 10,
            RightBattery = airPods.GetRightBattery() * 10,
            CaseBattery = airPods.GetCaseBattery() * 10,
            IsLeftCharging = airPods.IsLeftCharging(),
            IsRightCharging = airPods.IsRightCharging(),
            IsCaseCharging = airPods.IsCaseCharging(),
            IsLeftInEar = airPods.IsLeftInEar(),
            IsRightInEar = airPods.IsRightInEar(),
            IsLidOpen = airPods.IsLidOpened(),
            IsBothPodsInCase = airPods.IsBothPodsInCase(),
            SignalStrength = data.Rssi,
            LastSeen = DateTime.Now,
            IsConnected = pairedDevice?.IsConnected ?? false
        };

        _devicesByProductId[productId.Value] = deviceInfo;

        if (isNewDevice)
        {
            DeviceDiscovered?.Invoke(this, deviceInfo);
        }
        else
        {
            DeviceUpdated?.Invoke(this, deviceInfo);
        }
    }

    [Conditional("DEBUG")]
    private static void LogDebug(string message) => Debug.WriteLine($"[SimpleAirPodsDiscoveryService] {message}");

    public void Dispose()
    {
        if (_disposed)
            return;

        _watcher.Stop();
        _watcher.Dispose();
        _devicesByProductId.Clear();
        _disposed = true;
    }
}
