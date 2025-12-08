using DeviceCommunication.Apple;
using DeviceCommunication.Models;
//using DeviceCommunication.Services;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace MinimalCLI;

/// <summary>
/// Minimal console app to list paired AirPods, show info, and connect with audio.
/// Uses Win32BluetoothConnector which calls BluetoothSetServiceState to enable A2DP/HFP profiles.
/// </summary>
class Program
{
    private static readonly string[] RequestedProperties =
    [
        "System.Devices.Aep.IsConnected",
        "System.Devices.Aep.ContainerId",
        "System.Devices.ContainerId"
    ];

    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=== Minimal AirPods CLI ===\n");

        // List paired AirPods devices
        var devices = await GetPairedAirPodsAsync();

        if (devices.Count == 0)
        {
            Console.WriteLine("No paired AirPods found.");
            Console.WriteLine("Make sure your AirPods are paired in Windows Settings.");
            return 1;
        }

        Console.WriteLine($"Found {devices.Count} paired AirPods device(s):\n");

        for (int i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            var (connectionStatus, audioStatus) = await GetDeviceStatusAsync(device.Address, device.Name);
            
            Console.WriteLine($"  [{i + 1}] {device.Name}");
            Console.WriteLine($"      Model: {AppleDeviceModelHelper.GetModel(device.ProductId).GetDisplayName()}");
            Console.WriteLine($"      Bluetooth: {connectionStatus}");
            Console.WriteLine($"      Audio: {audioStatus}");
            Console.WriteLine($"      Address: {device.Address:X12}");
            Console.WriteLine();
        }

        // Prompt user for action
        Console.WriteLine("Actions:");
        Console.WriteLine("  [number]   - Connect to device");
        Console.WriteLine("  d[number]  - Disconnect device (e.g., d1)");
        Console.WriteLine("  0          - Exit");
        Console.Write("\nChoice: ");
        var input = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(input) || input == "0")
        {
            Console.WriteLine("Exiting.");
            return 0;
        }

        // Check if disconnect action
        bool isDisconnect = input.StartsWith('d');
        if (isDisconnect)
        {
            input = input[1..]; // Remove 'd' prefix
        }

        if (!int.TryParse(input, out var selection) || selection < 1 || selection > devices.Count)
        {
            Console.WriteLine("Invalid selection.");
            return 1;
        }

        var selected = devices[selection - 1];

        if (isDisconnect)
        {
            Console.WriteLine($"\nDisconnecting from {selected.Name}...");
            var disconnected = await DisconnectAsync(selected.Address);

            if (disconnected)
            {
                Console.WriteLine($"? Successfully disconnected from {selected.Name}!");
            }
            else
            {
                Console.WriteLine($"? Failed to disconnect from {selected.Name}.");
            }

            return disconnected ? 0 : 1;
        }
        else
        {
            Console.WriteLine($"\nConnecting to {selected.Name}...");
            var connected = await ConnectWithAudioAsync(selected.Address);

            if (connected)
            {
                Console.WriteLine($"? Successfully connected to {selected.Name} with audio!");
                Console.WriteLine("Audio should now route to your AirPods.");
            }
            else
            {
                Console.WriteLine($"? Failed to connect to {selected.Name}.");
                Console.WriteLine("\nTroubleshooting:");
                Console.WriteLine("  1. Ensure AirPods are not connected to another device");
                Console.WriteLine("  2. Try opening/closing the AirPods case");
                Console.WriteLine("  3. Try connecting manually via Windows Settings once first");
            }

            return connected ? 0 : 1;
        }
    }

    /// <summary>
    /// Gets all paired AirPods devices from Windows.
    /// </summary>
    private static async Task<List<PairedDeviceInfo>> GetPairedAirPodsAsync()
    {
        var result = new List<PairedDeviceInfo>();

        try
        {
            // Get all paired Bluetooth devices
            var selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
            var devices = await DeviceInformation.FindAllAsync(selector, RequestedProperties);

            foreach (var deviceInfo in devices)
            {
                try
                {
                    using var btDevice = await BluetoothDevice.FromIdAsync(deviceInfo.Id);
                    if (btDevice == null) continue;

                    // Get Product ID to identify Apple devices
                    var props = await DeviceInformation.CreateFromIdAsync(
                        btDevice.DeviceId,
                        ["System.DeviceInterface.Bluetooth.ProductId"]);

                    if (!props.Properties.TryGetValue("System.DeviceInterface.Bluetooth.ProductId", out var pidObj))
                        continue;

                    var productId = (ushort)pidObj;
                    var model = AppleDeviceModelHelper.GetModel(productId);

                    // Skip non-Apple devices
                    if (model == AppleDeviceModel.Unknown)
                        continue;

                    var isConnected = deviceInfo.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var connObj)
                        && connObj is true;

                    result.Add(new PairedDeviceInfo
                    {
                        Id = deviceInfo.Id,
                        ProductId = productId,
                        Name = btDevice.Name,
                        Address = btDevice.BluetoothAddress,
                        IsConnected = isConnected
                    });
                }
                catch
                {
                    // Skip devices that can't be queried
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error enumerating devices: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Connects to an AirPods device with audio using Win32BluetoothConnector.
    /// Uses BluetoothSetServiceState to enable A2DP/HFP audio profiles - the same API Windows Settings uses.
    /// </summary>
    private static async Task<bool> ConnectWithAudioAsync(ulong bluetoothAddress)
    {
        var connector = new DeviceCommunication.Services.Win32BluetoothConnector();
        var result = await connector.ConnectAudioDeviceAsync(bluetoothAddress);
        
        if (!result)
        {
            return false;
        }

        // Monitor connection status - the API returns before audio is fully established
        Console.WriteLine("  Waiting for audio connection to establish...");
        
        var timeout = TimeSpan.FromSeconds(10);
        var pollInterval = TimeSpan.FromMilliseconds(250);
        var elapsed = TimeSpan.Zero;
        var lastStatus = "";

        while (elapsed < timeout)
        {
            var (isConnected, isAudioReady, status) = await GetConnectionStatusAsync(bluetoothAddress);
            
            if (status != lastStatus)
            {
                Console.WriteLine($"  Status: {status}");
                lastStatus = status;
            }

            if (isConnected && isAudioReady)
            {
                Console.WriteLine("  Audio endpoint is ready!");
                return true;
            }

            await Task.Delay(pollInterval);
            elapsed += pollInterval;
        }

        // Timed out but services were enabled - may still work
        Console.WriteLine("  Timeout waiting for audio endpoint, but services were enabled.");
        return true;
    }

    /// <summary>
    /// Gets the current connection status of a Bluetooth device.
    /// </summary>
    private static async Task<(bool isConnected, bool isAudioReady, string status)> GetConnectionStatusAsync(ulong bluetoothAddress)
    {
        try
        {
            var btDevice = await BluetoothDevice.FromBluetoothAddressAsync(bluetoothAddress);
            if (btDevice == null)
            {
                return (false, false, "Device not found");
            }

            var isConnected = btDevice.ConnectionStatus == BluetoothConnectionStatus.Connected;
            
            // Check if audio endpoint exists and is ready
            var isAudioReady = await IsAudioEndpointReadyAsync(bluetoothAddress, btDevice.Name);
            
            var status = (isConnected, isAudioReady) switch
            {
                (false, _) => "Bluetooth connecting...",
                (true, false) => "Bluetooth connected, waiting for audio...",
                (true, true) => "Audio connected!"
            };

            return (isConnected, isAudioReady, status);
        }
        catch
        {
            return (false, false, "Error checking status");
        }
    }

    /// <summary>
    /// Gets detailed status for a device including Bluetooth connection and audio status.
    /// </summary>
    private static async Task<(string connectionStatus, string audioStatus)> GetDeviceStatusAsync(ulong bluetoothAddress, string deviceName)
    {
        try
        {
            var btDevice = await BluetoothDevice.FromBluetoothAddressAsync(bluetoothAddress);
            if (btDevice == null)
            {
                return ("Unknown", "Unknown");
            }

            var isConnected = btDevice.ConnectionStatus == BluetoothConnectionStatus.Connected;
            var connectionStatus = isConnected ? "CONNECTED" : "disconnected";

            // Check audio endpoint status
            var audioEndpointId = await FindAudioEndpointIdAsync(bluetoothAddress, deviceName);
            
            if (string.IsNullOrEmpty(audioEndpointId))
            {
                return (connectionStatus, "No audio endpoint");
            }

            // Check if this is the default audio device
            var defaultAudioId = Windows.Media.Devices.MediaDevice.GetDefaultAudioRenderId(
                Windows.Media.Devices.AudioDeviceRole.Default);
            
            var isDefaultAudio = audioEndpointId == defaultAudioId;

            var audioStatus = isDefaultAudio 
                ? "🔊 DEFAULT OUTPUT" 
                : isConnected 
                    ? "Available (not default)" 
                    : "Endpoint exists";

            return (connectionStatus, audioStatus);
        }
        catch
        {
            return ("Error", "Error");
        }
    }

    /// <summary>
    /// Finds the audio endpoint ID for a Bluetooth device.
    /// </summary>
    private static async Task<string?> FindAudioEndpointIdAsync(ulong bluetoothAddress, string deviceName)
    {
        try
        {
            var btDevice = await BluetoothDevice.FromBluetoothAddressAsync(bluetoothAddress);
            if (btDevice == null) return null;

            var btProps = await DeviceInformation.CreateFromIdAsync(
                btDevice.DeviceId,
                ["System.Devices.Aep.ContainerId", "System.Devices.ContainerId"]);

            Guid? btContainerId = null;
            if (btProps.Properties.TryGetValue("System.Devices.Aep.ContainerId", out var cid))
                btContainerId = cid as Guid?;
            if (!btContainerId.HasValue && btProps.Properties.TryGetValue("System.Devices.ContainerId", out var cid2))
                btContainerId = cid2 as Guid?;

            var audioSelector = Windows.Media.Devices.MediaDevice.GetAudioRenderSelector();
            var audioDevices = await DeviceInformation.FindAllAsync(
                audioSelector,
                ["System.Devices.ContainerId", "System.Devices.Aep.ContainerId"]);

            foreach (var audio in audioDevices)
            {
                if (btContainerId.HasValue)
                {
                    if (audio.Properties.TryGetValue("System.Devices.ContainerId", out var aCid) && aCid is Guid aGuid)
                    {
                        if (aGuid == btContainerId.Value)
                            return audio.Id;
                    }

                    if (audio.Properties.TryGetValue("System.Devices.Aep.ContainerId", out var aAepCid) && aAepCid is Guid aAepGuid)
                    {
                        if (aAepGuid == btContainerId.Value)
                            return audio.Id;
                    }
                }

                if (audio.Name.Contains(deviceName, StringComparison.OrdinalIgnoreCase))
                    return audio.Id;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if the audio endpoint for a device is ready.
    /// </summary>
    private static async Task<bool> IsAudioEndpointReadyAsync(ulong bluetoothAddress, string deviceName)
    {
        var endpointId = await FindAudioEndpointIdAsync(bluetoothAddress, deviceName);
        return !string.IsNullOrEmpty(endpointId);
    }

    /// <summary>
    /// Disconnects from an AirPods device by disabling all audio profiles.
    /// Uses BluetoothSetServiceState to disable A2DP/HFP/HSP/AVRCP services.
    /// </summary>
    private static async Task<bool> DisconnectAsync(ulong bluetoothAddress)
    {
        var connector = new DeviceCommunication.Services.Win32BluetoothConnector();
        return await connector.DisconnectAudioDeviceAsync(bluetoothAddress);
    }
}
