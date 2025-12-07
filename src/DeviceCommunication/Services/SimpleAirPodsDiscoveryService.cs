using DeviceCommunication.Advertisement;
using DeviceCommunication.Apple;
using DeviceCommunication.Models;
using Windows.Devices.Bluetooth;

namespace DeviceCommunication.Services;

/// <summary>
/// Simplified implementation of <see cref="IAirPodsDiscoveryService"/> that uses Product ID for device identification.
/// Eliminates complex battery signature grouping and MAC address tracking by leveraging stable Product IDs.
/// </summary>
public class SimpleAirPodsDiscoveryService : IAirPodsDiscoveryService
{
    private readonly IAdvertisementWatcher _watcher;
    private readonly Dictionary<ushort, DeviceState> _devicesByProductId;
    private readonly TimeSpan _deviceTimeout = TimeSpan.FromMinutes(5);
    private bool _disposed;

    public event EventHandler<AirPodsDeviceInfo>? DeviceDiscovered;
    public event EventHandler<AirPodsDeviceInfo>? DeviceUpdated;

    public SimpleAirPodsDiscoveryService() : this(new AdvertisementWatcher())
    {
    }

    public SimpleAirPodsDiscoveryService(IAdvertisementWatcher watcher)
    {
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        _devicesByProductId = new Dictionary<ushort, DeviceState>();
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
        var now = DateTime.Now;
        var activeDevices = _devicesByProductId.Values
            .Where(state => now - state.LastSeen <= _deviceTimeout)
            .Select(state => state.DeviceInfo)
            .ToList();

        return activeDevices;
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

        var productId = GetProductIdFromModel(model);
        if (!productId.HasValue)
            return;

        // Check if this is a new device or an update
        bool isNewDevice = !_devicesByProductId.ContainsKey(productId.Value);

        // Try to find paired device with matching Product ID
        var pairedDevice = await FindPairedDeviceByProductIdAsync(productId.Value);

        var deviceInfo = new AirPodsDeviceInfo
        {
            Address = data.Address,
            ProductId = productId.Value,
            Model = GetModelDisplayName(model),
            DeviceName = pairedDevice?.Name ?? GetModelDisplayName(model),
            PairedDeviceId = pairedDevice?.Id,
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
            IsConnected = pairedDevice?.IsConnected ?? false
        };

        _devicesByProductId[productId.Value] = new DeviceState
        {
            DeviceInfo = deviceInfo,
            LastSeen = DateTime.Now
        };

        if (isNewDevice)
            DeviceDiscovered?.Invoke(this, deviceInfo);
        else
            DeviceUpdated?.Invoke(this, deviceInfo);
    }

    /// <summary>
    /// Finds a paired Bluetooth device with the specified Product ID.
    /// </summary>
    private static async Task<PairedDeviceInfo?> FindPairedDeviceByProductIdAsync(ushort targetProductId)
    {
        try
        {
            // Get all paired Bluetooth devices
            var selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
            var deviceInfos = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(selector);

            foreach (var deviceInfo in deviceInfos)
            {
                try
                {
                    using var device = await DeviceCommunication.Device.Device.FromDeviceIdAsync(deviceInfo.Id);
                    
                    // Try to get Product ID
                    var productId = await device.GetProductIdAsync();
                    
                    if (productId == targetProductId)
                    {
                        return new PairedDeviceInfo
                        {
                            Id = deviceInfo.Id,
                            Name = device.GetName(),
                            Address = device.GetAddress(),
                            IsConnected = device.IsConnected()
                        };
                    }
                }
                catch
                {
                    // Skip devices that don't have Product ID or fail to query
                    continue;
                }
            }
        }
        catch
        {
            // Failed to enumerate devices
        }

        return null;
    }

    private static ushort? GetProductIdFromModel(AppleDeviceModel model)
    {
        return model switch
        {
            AppleDeviceModel.AirPods1 => 0x2002,
            AppleDeviceModel.AirPods2 => 0x200F,
            AppleDeviceModel.AirPods3 => 0x2013,
            AppleDeviceModel.AirPodsPro => 0x200E,
            AppleDeviceModel.AirPodsPro2 => 0x2014,
            AppleDeviceModel.AirPodsPro2UsbC => 0x2024,
            AppleDeviceModel.AirPodsMax => 0x200A,
            AppleDeviceModel.BeatsFitPro => 0x2012,
            _ => null
        };
    }

    private static string GetModelDisplayName(AppleDeviceModel model)
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
        _devicesByProductId.Clear();
        _disposed = true;
    }

    private class DeviceState
    {
        public required AirPodsDeviceInfo DeviceInfo { get; init; }
        public DateTime LastSeen { get; set; }
    }

    private class PairedDeviceInfo
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required ulong Address { get; init; }
        public required bool IsConnected { get; init; }
    }
}
