using System.Collections.Concurrent;
using System.Diagnostics;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace DeviceCommunication.Services;

/// <summary>
/// Result of a connection attempt.
/// </summary>
public enum ConnectionResult
{
    /// <summary>Connection established successfully.</summary>
    Connected,
    /// <summary>Device is not paired with Windows.</summary>
    NeedsPairing,
    /// <summary>Device was not found.</summary>
    DeviceNotFound,
    /// <summary>Connection failed due to an error.</summary>
    Failed
}

/// <summary>
/// Service for managing Bluetooth device connections using Windows device IDs.
/// </summary>
/// <remarks>
/// <para><strong>Hybrid Connection Strategy:</strong></para>
/// <para>
/// This service uses a hybrid approach combining Win32 and WinRT APIs for maximum reliability:
/// </para>
/// <list type="number">
/// <item><description><strong>Win32 Bluetooth APIs</strong> (InTheHand.Net.Bluetooth) - Primary method that directly
/// activates Bluetooth profiles (A2DP, HFP, HSP) similar to Windows Settings</description></item>
/// <item><description><strong>Windows Runtime APIs</strong> - Fallback method that triggers connections indirectly
/// via device property access</description></item>
/// </list>
/// <para>
/// The Win32 approach provides reliability comparable to the Windows Settings "Connect" button
/// by directly interfacing with the Windows Bluetooth stack. For best results, ensure devices
/// are already paired via Windows Settings before attempting connection.
/// </para>
/// </remarks>
public sealed class BluetoothConnectionService : IBluetoothConnectionService
{
    private readonly ConcurrentDictionary<string, BluetoothDevice> _connectedDevices = new();
    private readonly Win32BluetoothConnector _win32Connector = new();
    private bool _disposed;

    [Conditional("DEBUG")]
    private static void LogDebug(string message) => Debug.WriteLine($"[BtConnection] {message}");

    /// <summary>
    /// Attempts to connect to a Bluetooth device using its Windows device ID.
    /// </summary>
    /// <param name="deviceId">The Windows device ID (from paired device enumeration).</param>
    /// <returns>The connection result.</returns>
    /// <remarks>
    /// <para><strong>Connection Strategy:</strong></para>
    /// <para>
    /// This method uses a hybrid approach for maximum reliability:
    /// </para>
    /// <list type="number">
    /// <item><description>First attempts connection using Win32 Bluetooth APIs (InTheHand.Net.Bluetooth)</description></item>
    /// <item><description>Falls back to Windows Runtime API property access if Win32 fails</description></item>
    /// <item><description>Monitors connection status to verify success</description></item>
    /// </list>
    /// <para>
    /// The Win32 approach directly activates Bluetooth audio profiles (A2DP, HFP) providing
    /// reliability comparable to the Windows Settings "Connect" button.
    /// </para>
    /// </remarks>
    public async Task<ConnectionResult> ConnectByDeviceIdAsync(string deviceId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        LogDebug($"Connect starting: {deviceId}");

        if (string.IsNullOrEmpty(deviceId))
        {
            return ConnectionResult.DeviceNotFound;
        }

        try
        {
            var device = await BluetoothDevice.FromIdAsync(deviceId);

            if (device == null)
            {
                LogDebug($"Device not found: {deviceId}");
                return ConnectionResult.DeviceNotFound;
            }

            LogDebug($"Found {device.Name} ({device.BluetoothAddress:X12}), status={device.ConnectionStatus}");

            var isPaired = await CheckPairingStatusAsync(device);
            if (!isPaired)
            {
                LogDebug("Device not paired");
                device.Dispose();
                return ConnectionResult.NeedsPairing;
            }

            var deviceAddress = device.BluetoothAddress;

            // Strategy 1: Try Win32 Bluetooth APIs first (most reliable for audio devices)
            bool win32Connected = false;
            try
            {
                win32Connected = await _win32Connector.ConnectAudioDeviceAsync(deviceAddress);
                LogDebug($"Win32 connect: {(win32Connected ? "OK" : "failed")}");
            }
            catch (Exception ex)
            {
                LogDebug($"Win32 exception: {ex.Message}");
            }

            // Strategy 2: Fallback to WinRT property access (may trigger connection in some cases)
            if (!win32Connected)
            {
                try
                {
                    var properties = new[]
                    {
                        "System.Devices.Aep.IsConnected",
                        "System.Devices.Aep.SignalStrength",
                        "System.Devices.Aep.ContainerId",
                        "System.DeviceInterface.Bluetooth.VendorId",
                        "System.DeviceInterface.Bluetooth.ProductId",
                    };

                    var detailedInfo = await DeviceInformation.CreateFromIdAsync(deviceId, properties);
                    var isConnected = detailedInfo.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var obj)
                        && obj is true;
                    LogDebug($"WinRT fallback, IsConnected={isConnected}");
                }
                catch (Exception ex)
                {
                    LogDebug($"WinRT fallback exception: {ex.Message}");
                }
            }

            // Give Windows a moment to establish the connection
            var waitTime = win32Connected ? 1000 : 2000;
            await Task.Delay(waitTime);

            // Verify audio endpoint exists after connection
            var audioEndpointExists = await Win32BluetoothConnector.VerifyAudioEndpointExistsAsync(deviceAddress);
            if (!audioEndpointExists && win32Connected)
            {
                LogDebug("Warning: connected but no audio endpoint found");
            }

            // Store reference (dispose any existing)
            _connectedDevices.AddOrUpdate(
                deviceId,
                device,
                (_, existing) =>
                {
                    existing.Dispose();
                    return device;
                });

            // Check if connection was established
            var result = device.ConnectionStatus == BluetoothConnectionStatus.Connected || win32Connected
                ? ConnectionResult.Connected
                : ConnectionResult.Failed;

            LogDebug($"Result: {result}, status={device.ConnectionStatus}");
            return result;
        }
        catch (Exception ex)
        {
            LogDebug($"Exception: {ex.Message}");
            return ConnectionResult.Failed;
        }
    }

    /// <summary>
    /// Disconnects from a device by Windows device ID using Win32 APIs.
    /// </summary>
    /// <param name="deviceId">The Windows device ID.</param>
    /// <returns>True if disconnection was successful; otherwise, false.</returns>
    /// <remarks>
    /// <para>
    /// This method uses Win32 Bluetooth APIs to properly disconnect from audio profiles,
    /// similar to how Windows Settings disconnects devices. Simply disposing the device
    /// object may not always trigger a clean disconnection.
    /// </para>
    /// </remarks>
    public async Task<bool> DisconnectByDeviceIdAsync(string deviceId)
    {
        if (!_connectedDevices.TryGetValue(deviceId, out var device))
        {
            return false;
        }

        try
        {
            var deviceAddress = device.BluetoothAddress;
            bool win32Disconnected = await _win32Connector.DisconnectAudioDeviceAsync(deviceAddress);
            LogDebug($"Disconnect {deviceAddress:X12}: {(win32Disconnected ? "OK" : "failed")}");

            await Task.Delay(500);

            if (_connectedDevices.TryRemove(deviceId, out var removedDevice))
            {
                removedDevice.Dispose();
            }

            return win32Disconnected;
        }
        catch (Exception ex)
        {
            LogDebug($"Disconnect error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets the connection status of a device by Windows device ID.
    /// </summary>
    public bool IsConnectedByDeviceId(string deviceId)
    {
        return _connectedDevices.TryGetValue(deviceId, out var device) &&
               device.ConnectionStatus == BluetoothConnectionStatus.Connected;
    }


    private static async Task<bool> CheckPairingStatusAsync(BluetoothDevice device)
    {
        try
        {
            var properties = new[]
            {
                "System.Devices.Aep.ContainerId",
                "System.Devices.Aep.IsPaired"
            };

            var deviceInfo = await DeviceInformation.CreateFromIdAsync(device.DeviceId, properties);

            if (deviceInfo.Properties.TryGetValue("System.Devices.Aep.IsPaired", out var isPairedObj) &&
                isPairedObj is bool aepIsPaired)
            {
                return aepIsPaired;
            }

            return deviceInfo.Pairing.IsPaired;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var device in _connectedDevices.Values)
        {
            device.Dispose();
        }
        _connectedDevices.Clear();
    }
    #region Static Methods
    /// <summary>
    /// Opens the Windows Bluetooth settings page where users can connect devices reliably.
    /// </summary>
    /// <returns>True if the settings page was launched successfully; otherwise, false.</returns>
    /// <remarks>
    /// <para>
    /// This method provides the most reliable way to connect Bluetooth audio devices
    /// by leveraging the native Windows Settings interface, which has full access to
    /// system-level Bluetooth services.
    /// </para>
    /// <para>
    /// Use this as a fallback when <see cref="ConnectByDeviceIdAsync"/> doesn't establish
    /// a connection or when you want to provide users with the most reliable connection experience.
    /// </para>
    /// </remarks>
    public static async Task<bool> OpenBluetoothSettingsForDeviceAsync()
    {
        try
        {
            // Opens the Bluetooth devices page in Windows Settings
            var uri = new Uri("ms-settings:bluetooth");
            return await Windows.System.Launcher.LaunchUriAsync(uri);
        }
        catch (Exception ex)
        {
            LogDebug($"Failed to open settings: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Opens the Windows Quick Settings panel (Windows 11+) which provides quick access to Bluetooth devices.
    /// </summary>
    /// <returns>True if Quick Settings was launched successfully; otherwise, false.</returns>
    /// <remarks>
    /// <para>
    /// On Windows 11, this opens the Quick Settings panel where users can quickly connect/disconnect
    /// Bluetooth devices. This provides a more streamlined experience than the full Settings app.
    /// </para>
    /// <para>
    /// Falls back gracefully on older Windows versions where Quick Settings may not be available.
    /// </para>
    /// </remarks>
    public static async Task<bool> OpenBluetoothQuickSettingsAsync()
    {
        try
        {
            // Try to open Quick Settings (Windows 11+)
            // Falls back to regular settings if not available
            var uri = new Uri("ms-quick-actions:bluetooth");
            var launched = await Windows.System.Launcher.LaunchUriAsync(uri);
            
            if (!launched)
            {
                // Fallback to regular Bluetooth settings
                return await OpenBluetoothSettingsForDeviceAsync();
            }
            
            return true;
        }
        catch
        {
            // Fallback to regular Bluetooth settings
            return await OpenBluetoothSettingsForDeviceAsync();
        }
    }
    #endregion
}
