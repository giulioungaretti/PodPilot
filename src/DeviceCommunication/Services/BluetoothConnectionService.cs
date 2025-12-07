using System.Collections.Concurrent;
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
public sealed class BluetoothConnectionService : IDisposable
{
    private readonly ConcurrentDictionary<string, BluetoothDevice> _connectedDevices = new();
    private readonly Win32BluetoothConnector _win32Connector = new();
    private bool _disposed;

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

        if (string.IsNullOrEmpty(deviceId))
        {
            return ConnectionResult.DeviceNotFound;
        }

        try
        {
            var device = await BluetoothDevice.FromIdAsync(deviceId);

            if (device == null)
            {
                return ConnectionResult.DeviceNotFound;
            }

            // Verify pairing status
            var isPaired = await CheckPairingStatusAsync(device);

            if (!isPaired)
            {
                device.Dispose();
                return ConnectionResult.NeedsPairing;
            }

            var deviceAddress = device.BluetoothAddress;
            System.Diagnostics.Debug.WriteLine($"[BluetoothConnectionService] Connecting to device {deviceAddress:X12}...");

            // Strategy 1: Try Win32 Bluetooth APIs first (most reliable for audio devices)
            bool win32Connected = false;
            try
            {
                System.Diagnostics.Debug.WriteLine($"[BluetoothConnectionService] Attempting Win32 API connection...");
                win32Connected = await _win32Connector.ConnectAudioDeviceAsync(deviceAddress);
                
                if (win32Connected)
                {
                    System.Diagnostics.Debug.WriteLine($"[BluetoothConnectionService] Win32 connection successful!");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[BluetoothConnectionService] Win32 connection failed, trying fallback method...");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BluetoothConnectionService] Win32 connection error: {ex.Message}");
            }

            // Strategy 2: Fallback to WinRT property access (may trigger connection in some cases)
            if (!win32Connected)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[BluetoothConnectionService] Attempting WinRT property access fallback...");
                    
                    var properties = new[]
                    {
                        "System.Devices.Aep.IsConnected",
                        "System.Devices.Aep.SignalStrength",
                        "System.Devices.Aep.ContainerId",
                        "System.DeviceInterface.Bluetooth.VendorId",
                        "System.DeviceInterface.Bluetooth.ProductId",
                    };
                    
                    var detailedInfo = await DeviceInformation.CreateFromIdAsync(deviceId, properties);
                    
                    if (detailedInfo.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var isConnectedObj))
                    {
                        var isConnected = isConnectedObj as bool? ?? false;
                        System.Diagnostics.Debug.WriteLine($"[BluetoothConnectionService] Device AEP connection status: {isConnected}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BluetoothConnectionService] WinRT fallback failed: {ex.Message}");
                }
            }

            // Give Windows a moment to establish the connection
            await Task.Delay(win32Connected ? 1000 : 2000);

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
            if (device.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                System.Diagnostics.Debug.WriteLine($"[BluetoothConnectionService] Device connected successfully via {(win32Connected ? "Win32 API" : "WinRT API")}");
                return ConnectionResult.Connected;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[BluetoothConnectionService] Connection attempt completed. Status: {device.ConnectionStatus}");
                // Return success if Win32 reported success, even if WinRT status not updated yet
                return win32Connected ? ConnectionResult.Connected : ConnectionResult.Failed;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BluetoothConnectionService] Connection failed with exception: {ex.Message}");
            return ConnectionResult.Failed;
        }
    }

    /// <summary>
    /// Legacy method - connects using Bluetooth address.
    /// Prefer ConnectByDeviceIdAsync for more reliable connections.
    /// </summary>
    /// <param name="address">The Bluetooth MAC address.</param>
    /// <returns>The connection result.</returns>
    [Obsolete("Use ConnectByDeviceIdAsync instead for more reliable connections")]
    public async Task<ConnectionResult> ConnectAsync(ulong address)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (address == 0)
        {
            return ConnectionResult.NeedsPairing;
        }

        try
        {
            var device = await BluetoothDevice.FromBluetoothAddressAsync(address);

            if (device == null)
            {
                return ConnectionResult.DeviceNotFound;
            }

            var isPaired = await CheckPairingStatusAsync(device);

            if (!isPaired)
            {
                device.Dispose();
                return ConnectionResult.NeedsPairing;
            }

            _connectedDevices.AddOrUpdate(
                device.DeviceId,
                device,
                (_, existing) =>
                {
                    existing.Dispose();
                    return device;
                });

            return ConnectionResult.Connected;
        }
        catch
        {
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
            System.Diagnostics.Debug.WriteLine($"[BluetoothConnectionService] Device {deviceId} not found in connected devices");
            return false;
        }

        try
        {
            var deviceAddress = device.BluetoothAddress;
            System.Diagnostics.Debug.WriteLine($"[BluetoothConnectionService] Disconnecting from device {deviceAddress:X12}...");

            // Use Win32 APIs to properly disconnect from audio services
            bool win32Disconnected = await _win32Connector.DisconnectAudioDeviceAsync(deviceAddress);
            
            if (win32Disconnected)
            {
                System.Diagnostics.Debug.WriteLine($"[BluetoothConnectionService] Win32 disconnection successful");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[BluetoothConnectionService] Win32 disconnection failed");
            }

            // Wait a moment for disconnection to complete
            await Task.Delay(500);

            // Remove from tracking and dispose
            if (_connectedDevices.TryRemove(deviceId, out var removedDevice))
            {
                removedDevice.Dispose();
            }

            return win32Disconnected;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BluetoothConnectionService] Disconnection error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Legacy synchronous disconnect method.
    /// </summary>
    /// <remarks>
    /// This method only disposes the device object without properly disconnecting from services.
    /// Use <see cref="DisconnectByDeviceIdAsync"/> for proper Win32-based disconnection.
    /// </remarks>
    [Obsolete("Use DisconnectByDeviceIdAsync for proper Win32-based disconnection")]
    public void DisconnectByDeviceId(string deviceId)
    {
        if (_connectedDevices.TryRemove(deviceId, out var device))
        {
            device.Dispose();
        }
    }

    /// <summary>
    /// Legacy method - disconnects by address.
    /// </summary>
    [Obsolete("Use DisconnectByDeviceId instead")]
    public void Disconnect(ulong address)
    {
        // Try to find the device by address
        var deviceToRemove = _connectedDevices
            .FirstOrDefault(kvp => kvp.Value.BluetoothAddress == address);

        if (deviceToRemove.Key != null)
        {
            DisconnectByDeviceId(deviceToRemove.Key);
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

    /// <summary>
    /// Legacy method - gets connection status by address.
    /// </summary>
    [Obsolete("Use IsConnectedByDeviceId instead")]
    public bool IsConnected(ulong address)
    {
        return _connectedDevices.Values.Any(d =>
            d.BluetoothAddress == address &&
            d.ConnectionStatus == BluetoothConnectionStatus.Connected);
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
            System.Diagnostics.Debug.WriteLine($"[BluetoothConnectionService] Failed to open Bluetooth settings: {ex.Message}");
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
}
