using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace DeviceCommunication.Diagnostics;

/// <summary>
/// Provides diagnostic utilities for investigating Bluetooth device states.
/// </summary>
public static class BluetoothDiagnostics
{
    // AQS (Advanced Query Syntax) selectors for Bluetooth devices
    private const string PairedBluetoothDevicesSelector =
        "(System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\" OR " +
        "System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\") AND " +
        "System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True";

    private const string AudioBluetoothDevicesSelector =
        "System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\"";

    /// <summary>
    /// Information about a paired Bluetooth device.
    /// </summary>
    public record BluetoothDeviceDetails
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required bool IsPaired { get; init; }
        public required bool IsConnected { get; init; }
        public required bool IsPresent { get; init; }
        public required ulong BluetoothAddress { get; init; }
        public required BluetoothConnectionStatus ConnectionStatus { get; init; }
        public required string DeviceClass { get; init; }
        public required IReadOnlyDictionary<string, object> Properties { get; init; }
    }

    /// <summary>
    /// Gets all paired Bluetooth devices with their connection status.
    /// </summary>
    public static async Task<List<BluetoothDeviceDetails>> GetAllPairedDevicesAsync()
    {
        var devices = new List<BluetoothDeviceDetails>();

        // Query additional properties we want
        string[] requestedProperties =
        [
            "System.Devices.Aep.IsConnected",
            "System.Devices.Aep.IsPresent",
            "System.Devices.Aep.DeviceAddress",
            "System.Devices.Aep.Bluetooth.Le.IsConnectable",
            "System.Devices.Aep.Category",
            "System.Devices.DeviceInstanceId",
            "System.Devices.Aep.CanPair",
            "System.Devices.Aep.IsPaired",
            "System.Devices.Aep.ContainerId"
        ];

        var deviceInfos = await DeviceInformation.FindAllAsync(
            PairedBluetoothDevicesSelector,
            requestedProperties,
            DeviceInformationKind.AssociationEndpoint);

        foreach (var deviceInfo in deviceInfos)
        {
            try
            {
                var details = await GetDeviceDetailsAsync(deviceInfo);
                if (details is not null)
                {
                    devices.Add(details);
                }
            }
            catch
            {
                // Skip devices that fail to query
            }
        }

        return devices;
    }

    /// <summary>
    /// Gets all paired Bluetooth Classic devices (including audio devices like AirPods).
    /// </summary>
    public static async Task<List<BluetoothDeviceDetails>> GetPairedClassicDevicesAsync()
    {
        var devices = new List<BluetoothDeviceDetails>();

        string[] requestedProperties =
        [
            "System.Devices.Aep.IsConnected",
            "System.Devices.Aep.IsPresent",
            "System.Devices.Aep.DeviceAddress"
        ];

        // Use BluetoothDevice selector for Classic Bluetooth
        var selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
        var deviceInfos = await DeviceInformation.FindAllAsync(selector, requestedProperties);

        foreach (var deviceInfo in deviceInfos)
        {
            try
            {
                var device = await BluetoothDevice.FromIdAsync(deviceInfo.Id);
                if (device is not null)
                {
                    var isConnected = deviceInfo.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var connVal) 
                        && connVal is bool b && b;
                    var isPresent = deviceInfo.Properties.TryGetValue("System.Devices.Aep.IsPresent", out var presVal) 
                        && presVal is bool p && p;

                    devices.Add(new BluetoothDeviceDetails
                    {
                        Id = deviceInfo.Id,
                        Name = device.Name,
                        IsPaired = true,
                        IsConnected = isConnected || device.ConnectionStatus == BluetoothConnectionStatus.Connected,
                        IsPresent = isPresent,
                        BluetoothAddress = device.BluetoothAddress,
                        ConnectionStatus = device.ConnectionStatus,
                        DeviceClass = device.ClassOfDevice.MajorClass.ToString(),
                        Properties = deviceInfo.Properties.ToDictionary(p => p.Key, p => p.Value)
                    });
                }
            }
            catch
            {
                // Skip devices that fail to query
            }
        }

        return devices;
    }

    /// <summary>
    /// Gets the connection status for a specific Bluetooth address.
    /// </summary>
    public static async Task<(bool IsPaired, bool IsConnected, string? DeviceName)> GetDeviceStatusAsync(ulong bluetoothAddress)
    {
        try
        {
            var device = await BluetoothDevice.FromBluetoothAddressAsync(bluetoothAddress);
            if (device is null)
            {
                return (false, false, null);
            }

            var deviceInfo = await DeviceInformation.CreateFromIdAsync(device.DeviceId);
            var isPaired = deviceInfo?.Pairing?.IsPaired ?? false;
            var isConnected = device.ConnectionStatus == BluetoothConnectionStatus.Connected;

            return (isPaired, isConnected, device.Name);
        }
        catch
        {
            return (false, false, null);
        }
    }

    /// <summary>
    /// Finds a paired device by name pattern (case-insensitive contains).
    /// </summary>
    public static async Task<BluetoothDeviceDetails?> FindDeviceByNameAsync(string namePattern)
    {
        var devices = await GetPairedClassicDevicesAsync();
        return devices.FirstOrDefault(d => 
            d.Name.Contains(namePattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all audio-related Bluetooth devices (headphones, speakers, etc.).
    /// </summary>
    public static async Task<List<BluetoothDeviceDetails>> GetAudioDevicesAsync()
    {
        var allDevices = await GetPairedClassicDevicesAsync();
        return allDevices
            .Where(d => d.DeviceClass is "AudioVideo" or "Audio" or "Peripheral")
            .ToList();
    }

    /// <summary>
    /// Generates a diagnostic report of all Bluetooth devices.
    /// </summary>
    public static async Task<string> GenerateDiagnosticReportAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Bluetooth Diagnostic Report ===");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // Get adapter state
        var adapter = await BluetoothAdapter.GetDefaultAsync();
        if (adapter is null)
        {
            sb.AppendLine("ERROR: No Bluetooth adapter found!");
            return sb.ToString();
        }

        var radio = await adapter.GetRadioAsync();
        sb.AppendLine($"Adapter Address: {adapter.BluetoothAddress:X12}");
        sb.AppendLine($"Radio State: {radio?.State.ToString() ?? "Unknown"}");
        sb.AppendLine($"LE Supported: {adapter.IsLowEnergySupported}");
        sb.AppendLine($"Classic Supported: {adapter.IsClassicSupported}");
        sb.AppendLine();

        // Get paired devices
        sb.AppendLine("=== Paired Classic Bluetooth Devices ===");
        var classicDevices = await GetPairedClassicDevicesAsync();
        if (classicDevices.Count == 0)
        {
            sb.AppendLine("No paired Classic Bluetooth devices found.");
        }
        else
        {
            foreach (var device in classicDevices)
            {
                sb.AppendLine($"  [{(device.IsConnected ? "CONNECTED" : "disconnected")}] {device.Name}");
                sb.AppendLine($"    Address: {device.BluetoothAddress:X12}");
                sb.AppendLine($"    Class: {device.DeviceClass}");
                sb.AppendLine($"    Is Present: {device.IsPresent}");
                sb.AppendLine($"    Connection Status: {device.ConnectionStatus}");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static async Task<BluetoothDeviceDetails?> GetDeviceDetailsAsync(DeviceInformation deviceInfo)
    {
        // Try to get as Classic Bluetooth device first
        try
        {
            var classicDevice = await BluetoothDevice.FromIdAsync(deviceInfo.Id);
            if (classicDevice is not null)
            {
                var isConnected = deviceInfo.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var connVal) 
                    && connVal is bool b && b;
                var isPresent = deviceInfo.Properties.TryGetValue("System.Devices.Aep.IsPresent", out var presVal) 
                    && presVal is bool p && p;

                return new BluetoothDeviceDetails
                {
                    Id = deviceInfo.Id,
                    Name = classicDevice.Name,
                    IsPaired = true,
                    IsConnected = isConnected || classicDevice.ConnectionStatus == BluetoothConnectionStatus.Connected,
                    IsPresent = isPresent,
                    BluetoothAddress = classicDevice.BluetoothAddress,
                    ConnectionStatus = classicDevice.ConnectionStatus,
                    DeviceClass = classicDevice.ClassOfDevice.MajorClass.ToString(),
                    Properties = deviceInfo.Properties.ToDictionary(p => p.Key, p => p.Value)
                };
            }
        }
        catch
        {
            // Not a classic device, try BLE
        }

        // Try BLE device
        try
        {
            var bleDevice = await BluetoothLEDevice.FromIdAsync(deviceInfo.Id);
            if (bleDevice is not null)
            {
                var isConnected = deviceInfo.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var connVal) 
                    && connVal is bool b && b;
                var isPresent = deviceInfo.Properties.TryGetValue("System.Devices.Aep.IsPresent", out var presVal) 
                    && presVal is bool p && p;

                return new BluetoothDeviceDetails
                {
                    Id = deviceInfo.Id,
                    Name = bleDevice.Name,
                    IsPaired = true,
                    IsConnected = isConnected || bleDevice.ConnectionStatus == BluetoothConnectionStatus.Connected,
                    IsPresent = isPresent,
                    BluetoothAddress = bleDevice.BluetoothAddress,
                    ConnectionStatus = bleDevice.ConnectionStatus,
                    DeviceClass = "BLE",
                    Properties = deviceInfo.Properties.ToDictionary(p => p.Key, p => p.Value)
                };
            }
        }
        catch
        {
            // Skip
        }

        return null;
    }
}
