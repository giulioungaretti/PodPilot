using System.Diagnostics;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;

namespace DeviceCommunication.Services;

/// <summary>
/// Simple Bluetooth connector using only the AudioPlaybackConnection API.
/// This is the cleanest approach for modern Windows (10 2004+) and modern Bluetooth audio devices.
/// </summary>
/// <remarks>
/// <para>
/// This connector uses only the <c>AudioPlaybackConnection</c> API, which is the same API
/// that Windows Settings uses when you click "Connect" on a Bluetooth audio device.
/// </para>
/// <para>
/// <strong>When to use this connector:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Modern Windows 10 (2004+) or Windows 11</description></item>
/// <item><description>Modern Bluetooth audio devices that work well with Windows</description></item>
/// <item><description>When you prefer simplicity over maximum compatibility</description></item>
/// </list>
/// <para>
/// <strong>Limitations:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>May not work on older Windows versions</description></item>
/// <item><description>Won't force devices to switch from other hosts (e.g., AirPods connected to iPhone)</description></item>
/// <item><description>Requires the device to already have an audio endpoint registered</description></item>
/// </list>
/// </remarks>
public sealed class SimpleBluetoothConnector : IBluetoothConnector
{
    private static void Log(string message) => Debug.WriteLine($"[SimpleBluetoothConnector] {message}");

    /// <inheritdoc />
    public async Task<bool> ConnectAudioDeviceAsync(ulong address)
    {
        try
        {
            Log($"Connecting to {address:X12}...");

            var audioDeviceId = await FindAudioPlaybackDeviceIdAsync(address);
            
            if (string.IsNullOrEmpty(audioDeviceId))
            {
                Log($"No audio endpoint found for {address:X12}. Device may need initial pairing via Windows Settings.");
                return false;
            }

            Log($"Found audio device, creating connection...");
            var connection = AudioPlaybackConnection.TryCreateFromId(audioDeviceId);
            
            if (connection == null)
            {
                Log("AudioPlaybackConnection.TryCreateFromId returned null");
                return false;
            }

            await connection.StartAsync();
            var openResult = await connection.OpenAsync();
            
            if (openResult.Status == AudioPlaybackConnectionOpenResultStatus.Success)
            {
                Log($"Connected successfully to {address:X12}");
                return true;
            }

            Log($"Connection failed with status: {openResult.Status}");
            connection.Dispose();
            return false;
        }
        catch (Exception ex)
        {
            Log($"Exception: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public Task<bool> DisconnectAudioDeviceAsync(ulong address)
    {
        // AudioPlaybackConnection doesn't have a direct "disconnect" - 
        // the connection is released when the object is disposed.
        // For a proper disconnect, we'd need to track connections or use Win32 APIs.
        Log($"Disconnect requested for {address:X12} - simple connector has limited disconnect support");
        
        // Return true since we don't actively maintain connections
        // The device will naturally disconnect when audio stops or the connection is disposed
        return Task.FromResult(true);
    }

    /// <summary>
    /// Finds the audio playback device ID for a Bluetooth device by its MAC address.
    /// </summary>
    private static async Task<string?> FindAudioPlaybackDeviceIdAsync(ulong address)
    {
        try
        {
            var audioSelector = Windows.Media.Devices.MediaDevice.GetAudioRenderSelector();
            var additionalProperties = new[]
            {
                "System.Devices.ContainerId",
                "System.Devices.Aep.ContainerId"
            };
            
            var audioDevices = await DeviceInformation.FindAllAsync(audioSelector, additionalProperties);

            // Get the Bluetooth device to find its container ID
            var btDevice = await Windows.Devices.Bluetooth.BluetoothDevice.FromBluetoothAddressAsync(address);
            if (btDevice == null)
            {
                Log($"Bluetooth device not found for address {address:X12}");
                return null;
            }

            // Get container ID of the Bluetooth device
            var btProperties = new[] { "System.Devices.Aep.ContainerId", "System.Devices.ContainerId" };
            var btDeviceInfo = await DeviceInformation.CreateFromIdAsync(btDevice.DeviceId, btProperties);
            
            Guid? btContainerId = null;
            if (btDeviceInfo.Properties.TryGetValue("System.Devices.Aep.ContainerId", out var containerIdObj))
            {
                btContainerId = containerIdObj as Guid?;
            }
            if (!btContainerId.HasValue && btDeviceInfo.Properties.TryGetValue("System.Devices.ContainerId", out var containerId2Obj))
            {
                btContainerId = containerId2Obj as Guid?;
            }

            // Find matching audio device by container ID
            foreach (var audioDevice in audioDevices)
            {
                if (btContainerId.HasValue)
                {
                    if (audioDevice.Properties.TryGetValue("System.Devices.ContainerId", out var audioContainerIdObj) &&
                        audioContainerIdObj is Guid audioContainerId &&
                        audioContainerId == btContainerId.Value)
                    {
                        return audioDevice.Id;
                    }
                    
                    if (audioDevice.Properties.TryGetValue("System.Devices.Aep.ContainerId", out var aepContainerIdObj) &&
                        aepContainerIdObj is Guid aepContainerId &&
                        aepContainerId == btContainerId.Value)
                    {
                        return audioDevice.Id;
                    }
                }

                // Name-based fallback
                if (audioDevice.Name.Contains(btDevice.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return audioDevice.Id;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Log($"Error finding audio device: {ex.Message}");
            return null;
        }
    }
}
