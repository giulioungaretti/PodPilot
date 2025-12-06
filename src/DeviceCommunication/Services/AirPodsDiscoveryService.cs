using DeviceCommunication.Advertisement;
using DeviceCommunication.Apple;
using DeviceCommunication.Models;

namespace DeviceCommunication.Services;

/// <summary>
/// Provides functionality to discover AirPods devices via Bluetooth advertisements.
/// Raises events when devices are discovered or updated, and allows scanning control.
/// </summary>
public class AirPodsDiscoveryService : IDisposable
{
    private readonly IAdvertisementWatcher _watcher;
    private readonly Dictionary<ulong, AirPodsDeviceInfo> _discoveredDevices;
    private bool _disposed;

    public event EventHandler<AirPodsDeviceInfo>? DeviceDiscovered;
    public event EventHandler<AirPodsDeviceInfo>? DeviceUpdated;

    /// <summary>
    /// Initializes a new instance of the <see cref="AirPodsDiscoveryService"/> class using a default <see cref="AdvertisementWatcher"/>.
    /// </summary>
    public AirPodsDiscoveryService() : this(new AdvertisementWatcher())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AirPodsDiscoveryService"/> class with a specified advertisement watcher.
    /// </summary>
    /// <param name="watcher">The <see cref="IAdvertisementWatcher"/> to use for discovering AirPods devices.</param>
    /// <remarks>
    /// This constructor is primarily intended for testing purposes, allowing injection of a mock or custom advertisement watcher.
    /// </remarks>  
    public AirPodsDiscoveryService(IAdvertisementWatcher watcher)
    {
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        _discoveredDevices = new Dictionary<ulong, AirPodsDeviceInfo>();
        _watcher.AdvertisementReceived += OnAdvertisementReceived;
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
            IsConnected = true
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

        _watcher.Stop();
        _watcher.Dispose();
        _discoveredDevices.Clear();
        _disposed = true;
    }
}
