using DeviceCommunication.Advertisement;
using DeviceCommunication.Apple;
using DeviceCommunication.Models;

namespace DeviceCommunication.Services;

/// <summary>
/// [LEGACY ARCHITECTURE] Advanced implementation of <see cref="IAirPodsDiscoveryService"/> that groups broadcasts from the same device.
/// Uses battery signature and temporal proximity to merge broadcasts from different Bluetooth addresses
/// (left/right pods) into a single logical device.
/// </summary>
/// <remarks>
/// <para><strong>?? LEGACY - Use <see cref="SimpleAirPodsDiscoveryService"/> instead</strong></para>
/// <para>This implementation demonstrates the old complex grouping architecture that has been replaced
/// by a much simpler Product ID-based approach. Kept for educational purposes.</para>
/// <para><strong>Why this approach was replaced:</strong></para>
/// <list type="bullet">
/// <item>Required complex battery signature matching</item>
/// <item>Needed temporal proximity windows (30-second grouping)</item>
/// <item>Tracked multiple rotating addresses per device</item>
/// <item>Required cleanup of expired signatures</item>
/// <item>Generated GUIDs for device identification</item>
/// </list>
/// <para><strong>New approach (SimpleAirPodsDiscoveryService):</strong></para>
/// <list type="bullet">
/// <item>Uses stable Product ID from BLE advertisement</item>
/// <item>Single-pass lookup: Product ID ? Windows paired device</item>
/// <item>No signature matching or temporal windows needed</item>
/// <item>No address rotation tracking required</item>
/// <item>Significantly simpler code and logic</item>
/// </list>
/// </remarks>
[Obsolete("This class uses legacy battery signature grouping. Use SimpleAirPodsDiscoveryService with Product ID-based identification instead. Kept for educational purposes.")]
public class GroupedAirPodsDiscoveryService : IAirPodsDiscoveryService
{
    private readonly IAdvertisementWatcher _watcher;
    private readonly Dictionary<Guid, DeviceGroup> _deviceGroups;
    private readonly Dictionary<DeviceSignature, Guid> _signatureToDeviceId;
    private readonly TimeSpan _groupingWindow = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _deviceTimeout = TimeSpan.FromMinutes(5);
    private bool _disposed;

    public event EventHandler<AirPodsDeviceInfo>? DeviceDiscovered;
    public event EventHandler<AirPodsDeviceInfo>? DeviceUpdated;

    public GroupedAirPodsDiscoveryService() : this(new AdvertisementWatcher())
    {
    }

    public GroupedAirPodsDiscoveryService(IAdvertisementWatcher watcher)
    {
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        _deviceGroups = new Dictionary<Guid, DeviceGroup>();
        _signatureToDeviceId = new Dictionary<DeviceSignature, Guid>();
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
        var activeDevices = _deviceGroups.Values
            .Where(group => now - group.LastSeen <= _deviceTimeout)
            .Select(group => group.DeviceInfo)
            .ToList();

        return activeDevices;
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

        var signature = new DeviceSignature
        {
            Model = model,
            LeftBattery = airPods.GetLeftBattery(),
            RightBattery = airPods.GetRightBattery(),
            CaseBattery = airPods.GetCaseBattery()
        };

        CleanupExpiredSignatures();

        Guid deviceId;
        DeviceGroup? group;
        bool isNewDevice = false;

        if (_signatureToDeviceId.TryGetValue(signature, out var existingDeviceId))
        {
            deviceId = existingDeviceId;
            group = _deviceGroups[deviceId];
            group.SeenAddresses.Add(data.Address);
            group.LastSeen = DateTime.Now;
        }
        else
        {
            group = FindGroupByRecentAddress(data.Address);

            if (group != null)
            {
                deviceId = group.DeviceId;
                RemoveSignatureForDevice(deviceId);
                _signatureToDeviceId[signature] = deviceId;
                group.LastSeen = DateTime.Now;
            }
            else
            {
                deviceId = Guid.NewGuid();
                group = new DeviceGroup
                {
                    DeviceId = deviceId,
                    LastSeen = DateTime.Now
                };
                group.SeenAddresses.Add(data.Address);

                _deviceGroups[deviceId] = group;
                _signatureToDeviceId[signature] = deviceId;
                isNewDevice = true;
            }
        }

        group.DeviceInfo = new AirPodsDeviceInfo
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

        if (isNewDevice)
            DeviceDiscovered?.Invoke(this, group.DeviceInfo);
        else
            DeviceUpdated?.Invoke(this, group.DeviceInfo);
    }

    private void CleanupExpiredSignatures()
    {
        var now = DateTime.Now;
        var expiredSignatures = _signatureToDeviceId
            .Where(kvp => _deviceGroups.TryGetValue(kvp.Value, out var group) &&
                         now - group.LastSeen > _groupingWindow)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var signature in expiredSignatures)
        {
            _signatureToDeviceId.Remove(signature);
        }
    }

    private void RemoveSignatureForDevice(Guid deviceId)
    {
        var signatureToRemove = _signatureToDeviceId
            .FirstOrDefault(kvp => kvp.Value == deviceId)
            .Key;

        if (signatureToRemove != null)
        {
            _signatureToDeviceId.Remove(signatureToRemove);
        }
    }

    private DeviceGroup? FindGroupByRecentAddress(ulong address)
    {
        var now = DateTime.Now;

        return _deviceGroups.Values
            .FirstOrDefault(group =>
                group.SeenAddresses.Contains(address) &&
                now - group.LastSeen <= _groupingWindow);
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
        _deviceGroups.Clear();
        _signatureToDeviceId.Clear();
        _disposed = true;
    }

    private class DeviceSignature
    {
        public AppleDeviceModel Model { get; init; }
        public int? LeftBattery { get; init; }
        public int? RightBattery { get; init; }
        public int? CaseBattery { get; init; }

        public override bool Equals(object? obj)
        {
            if (obj is not DeviceSignature other)
                return false;

            return Model == other.Model &&
                   LeftBattery == other.LeftBattery &&
                   RightBattery == other.RightBattery &&
                   CaseBattery == other.CaseBattery;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Model, LeftBattery, RightBattery, CaseBattery);
        }
    }

    private class DeviceGroup
    {
        public Guid DeviceId { get; init; }
        public HashSet<ulong> SeenAddresses { get; init; } = new();
        public AirPodsDeviceInfo DeviceInfo { get; set; } = null!;
        public DateTime LastSeen { get; set; }
    }
}
