using DeviceCommunication.Advertisement;
using DeviceCommunication.Apple;
using DeviceCommunication.Models;

namespace DeviceCommunication.Services;

/// <summary>
/// Simple address-based implementation of <see cref="IAirPodsDiscoveryService"/>.
/// Tracks devices by their Bluetooth address without grouping multiple addresses from the same device.
/// </summary>
public class AirPodsDiscoveryService : IAirPodsDiscoveryService
{
    private readonly IAdvertisementWatcher _watcher;
    private readonly Dictionary<ulong, AirPodsDeviceInfo> _discoveredDevices;
    private readonly TimeSpan _deviceTimeout = TimeSpan.FromSeconds(15);
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public event EventHandler<AirPodsDeviceInfo>? DeviceDiscovered;
    public event EventHandler<AirPodsDeviceInfo>? DeviceUpdated;
    public event EventHandler<AirPodsDeviceInfo>? DeviceRemoved;

    public AirPodsDiscoveryService() : this(new AdvertisementWatcher())
    {
    }

    public AirPodsDiscoveryService(IAdvertisementWatcher watcher)
    {
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        _discoveredDevices = new Dictionary<ulong, AirPodsDeviceInfo>();
        _watcher.AdvertisementReceived += OnAdvertisementReceived;
        _cleanupTimer = new Timer(CleanupExpiredDevices, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public void StartScanning()
    {
        _watcher.Start();
    }

    public void StopScanning()
    {
        _watcher.Stop();
    }

    public IReadOnlyList<AirPodsDeviceInfo> GetDiscoveredDevices()
    {
        return _discoveredDevices.Values.ToList();
    }

    private void CleanupExpiredDevices(object? state)
    {
        var now = DateTime.Now;
        var expiredDevices = _discoveredDevices
            .Where(kvp => now - kvp.Value.LastSeen > _deviceTimeout)
            .Select(kvp => kvp.Value)
            .ToList();

        foreach (var device in expiredDevices)
        {
            _discoveredDevices.Remove(device.Address);
            DeviceRemoved?.Invoke(this, device);
        }
    }

    private void OnAdvertisementReceived(object? sender, AdvertisementReceivedData data)
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

        var deviceInfo = new AirPodsDeviceInfo
        {
            Address = data.Address,
            Model = GetModelDisplayName(model),
            DeviceName = GetModelDisplayName(model),
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
            LastSeen = DateTime.Now,
            IsConnected = false  // BLE advertisement presence doesn't indicate Bluetooth connection
        };

        bool isNew = !_discoveredDevices.ContainsKey(data.Address);
        _discoveredDevices[data.Address] = deviceInfo;

        if (isNew)
            DeviceDiscovered?.Invoke(this, deviceInfo);
        else
            DeviceUpdated?.Invoke(this, deviceInfo);
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
        if (_disposed)
            return;

        _cleanupTimer.Dispose();
        _watcher.Stop();
        _watcher.Dispose();
        _discoveredDevices.Clear();
        _disposed = true;
    }
}
