using System.Diagnostics;
using System.Runtime.InteropServices;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;

namespace DeviceCommunication.Services;

/// <summary>
/// Native Win32 Bluetooth connector using P/Invoke for BluetoothSetServiceState.
/// Provides direct access to Windows Bluetooth stack for reliable audio device connections.
/// </summary>
/// <remarks>
/// <para>
/// This connector uses the Win32 <c>BluetoothSetServiceState</c> API via P/Invoke to
/// activate Bluetooth profiles (A2DP, HFP, HSP). This is the same API that Windows Settings
/// uses when you click "Connect" on a Bluetooth audio device.
/// </para>
/// <para>
/// Unlike RFCOMM socket connections, <c>BluetoothSetServiceState</c> properly activates
/// audio profiles that use L2CAP/AVDTP protocols (like A2DP for audio streaming).
/// </para>
/// </remarks>
public sealed class Win32BluetoothConnector
{
    #region Win32 P/Invoke Declarations

    /// <summary>
    /// Bluetooth device info structure for Win32 APIs.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BLUETOOTH_DEVICE_INFO
    {
        public uint dwSize;
        public ulong Address;
        public uint ulClassofDevice;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fConnected;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fRemembered;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAuthenticated;
        public SYSTEMTIME stLastSeen;
        public SYSTEMTIME stLastUsed;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
        public string szName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEMTIME
    {
        public ushort wYear;
        public ushort wMonth;
        public ushort wDayOfWeek;
        public ushort wDay;
        public ushort wHour;
        public ushort wMinute;
        public ushort wSecond;
        public ushort wMilliseconds;
    }

    /// <summary>
    /// Enables or disables services for a Bluetooth device.
    /// </summary>
    /// <param name="hRadio">Handle to a local radio. Can be IntPtr.Zero to use any available radio.</param>
    /// <param name="pbtdi">Pointer to a BLUETOOTH_DEVICE_INFO structure.</param>
    /// <param name="pGuidService">Pointer to the service GUID.</param>
    /// <param name="dwServiceFlags">BLUETOOTH_SERVICE_ENABLE (1) to enable, BLUETOOTH_SERVICE_DISABLE (0) to disable.</param>
    /// <returns>ERROR_SUCCESS (0) if successful, otherwise a Win32 error code.</returns>
    [DllImport("bthprops.cpl", SetLastError = true)]
    private static extern uint BluetoothSetServiceState(
        IntPtr hRadio,
        ref BLUETOOTH_DEVICE_INFO pbtdi,
        ref Guid pGuidService,
        uint dwServiceFlags);

    /// <summary>
    /// Retrieves information about a remote Bluetooth device.
    /// The device must have been previously discovered or paired.
    /// </summary>
    /// <param name="hRadio">Handle to a local radio. Can be IntPtr.Zero to use any available radio.</param>
    /// <param name="pbtdi">Pointer to a BLUETOOTH_DEVICE_INFO structure. Must have dwSize and Address set.</param>
    /// <returns>ERROR_SUCCESS (0) if successful, otherwise a Win32 error code.</returns>
    [DllImport("bthprops.cpl", SetLastError = true)]
    private static extern uint BluetoothGetDeviceInfo(
        IntPtr hRadio,
        ref BLUETOOTH_DEVICE_INFO pbtdi);

    private const uint BLUETOOTH_SERVICE_DISABLE = 0x00;
    private const uint BLUETOOTH_SERVICE_ENABLE = 0x01;
    private const uint ERROR_SUCCESS = 0;

    #endregion

    // Common Bluetooth audio service UUIDs
    // Note: AirPods may register as either Source or Sink depending on the direction
    private static readonly Guid AudioSinkServiceGuid = BluetoothService.AudioSink;      // A2DP Sink - 0x110B
    private static readonly Guid AudioSourceServiceGuid = BluetoothService.AudioSource;  // A2DP Source - 0x110A
    private static readonly Guid AdvancedAudioDistributionGuid = BluetoothService.AdvancedAudioDistribution; // A2DP base - 0x110D
    private static readonly Guid HandsfreeServiceGuid = BluetoothService.Handsfree;      // HFP - 0x111E
    private static readonly Guid HandsfreeAudioGatewayGuid = BluetoothService.HandsfreeAudioGateway; // HFP AG - 0x111F
    private static readonly Guid HeadsetServiceGuid = BluetoothService.Headset;          // HSP - 0x1108
    private static readonly Guid HeadsetAudioGatewayGuid = BluetoothService.HeadsetAudioGateway; // HSP AG - 0x1112
    private static readonly Guid AvRemoteControlGuid = new("0000110E-0000-1000-8000-00805F9B34FB"); // AVRCP
    private static readonly Guid AvRemoteControlTargetGuid = new("0000110C-0000-1000-8000-00805F9B34FB"); // AVRCP Target

    /// <summary>
    /// Attempts to connect to a Bluetooth audio device using its MAC address.
    /// </summary>
    /// <param name="address">The Bluetooth MAC address as a ulong.</param>
    /// <returns>True if connection was successful; otherwise, false.</returns>
    /// <remarks>
    /// <para>
    /// Uses a multi-tier connection strategy:
    /// </para>
    /// <list type="number">
    /// <item><description>Primary: <c>AudioPlaybackConnection</c> API (Windows 10 2004+) - same as Windows Settings "Connect" button</description></item>
    /// <item><description>Fallback: <c>BluetoothSetServiceState</c> Win32 API to enable audio profiles</description></item>
    /// </list>
    /// </remarks>
    public async Task<bool> ConnectAudioDeviceAsync(ulong address)
    {
        try
        {
            Debug.WriteLine($"");
            Debug.WriteLine($"[Win32BluetoothConnector] ========================================");
            Debug.WriteLine($"[Win32BluetoothConnector] === CONNECT AUDIO DEVICE START ===");
            Debug.WriteLine($"[Win32BluetoothConnector] Target address: {address:X12}");
            Debug.WriteLine($"[Win32BluetoothConnector] Timestamp: {DateTime.Now:HH:mm:ss.fff}");
            Debug.WriteLine($"[Win32BluetoothConnector] ========================================");

            // Strategy 1: Try AudioPlaybackConnection API (most reliable, same as Windows Settings)
            Debug.WriteLine($"[Win32BluetoothConnector] ");
            Debug.WriteLine($"[Win32BluetoothConnector] >>> STRATEGY 1: AudioPlaybackConnection API <<<");
            bool audioPlaybackConnected = await TryConnectViaAudioPlaybackConnectionAsync(address);
            if (audioPlaybackConnected)
            {
                Debug.WriteLine($"[Win32BluetoothConnector] RESULT: Strategy 1 succeeded - AudioPlaybackConnection established");
                Debug.WriteLine($"[Win32BluetoothConnector] ========================================");
                return true;
            }

            // Strategy 2: Fallback to BluetoothSetServiceState to activate audio profiles
            Debug.WriteLine($"[Win32BluetoothConnector] ");
            Debug.WriteLine($"[Win32BluetoothConnector] >>> STRATEGY 2: BluetoothSetServiceState API <<<");
            Debug.WriteLine($"[Win32BluetoothConnector] AudioPlaybackConnection failed, attempting Win32 API fallback...");
            var (serviceResult, likelyConnectedElsewhere) = await Task.Run(() => TryEnableAudioServicesWithDiagnostics(address));
            
            if (serviceResult)
            {
                Debug.WriteLine($"[Win32BluetoothConnector] RESULT: Strategy 2 succeeded - Audio services enabled via Win32 API");
                Debug.WriteLine($"[Win32BluetoothConnector] NOTE: Audio may take 1-2 seconds to route to the device");
                Debug.WriteLine($"[Win32BluetoothConnector] ========================================");
                return true;
            }

            // Strategy 3: Try RFCOMM socket connection to trigger device switch
            // This can sometimes cause the device to disconnect from another host
            if (likelyConnectedElsewhere)
            {
                Debug.WriteLine($"[Win32BluetoothConnector] ");
                Debug.WriteLine($"[Win32BluetoothConnector] >>> STRATEGY 3: RFCOMM Socket Connection (Device Switch Trigger) <<<");
                Debug.WriteLine($"[Win32BluetoothConnector] Device appears connected to another device. Attempting RFCOMM connection to trigger switch...");
                
                var rfcommResult = await TryConnectViaRfcommAsync(address);
                if (rfcommResult)
                {
                    // RFCOMM connected - now retry the service state
                    Debug.WriteLine($"[Win32BluetoothConnector] RFCOMM connection established! Retrying BluetoothSetServiceState...");
                    await Task.Delay(500); // Brief delay for device to fully switch
                    
                    var (retryResult, _) = TryEnableAudioServicesWithDiagnostics(address);
                    if (retryResult)
                    {
                        Debug.WriteLine($"[Win32BluetoothConnector] RESULT: Strategy 3 succeeded - Device switched and audio services enabled");
                        Debug.WriteLine($"[Win32BluetoothConnector] ========================================");
                        return true;
                    }
                }
            }

            Debug.WriteLine($"[Win32BluetoothConnector] RESULT: All strategies failed for {address:X12}");
            Debug.WriteLine($"[Win32BluetoothConnector] TROUBLESHOOTING:");
            Debug.WriteLine($"[Win32BluetoothConnector]   1. Ensure device is paired in Windows Settings first");
            Debug.WriteLine($"[Win32BluetoothConnector]   2. Try connecting manually once via Windows Settings");
            if (likelyConnectedElsewhere)
            {
                Debug.WriteLine($"[Win32BluetoothConnector]   3. >>> LIKELY ISSUE: Device is connected to another device (phone/tablet)");
                Debug.WriteLine($"[Win32BluetoothConnector]      Disconnect from other device first, or put AirPods in case briefly");
            }
            else
            {
                Debug.WriteLine($"[Win32BluetoothConnector]   3. Ensure AirPods are not connected to another device");
            }
            Debug.WriteLine($"[Win32BluetoothConnector]   4. Try opening/closing the AirPods case");
            Debug.WriteLine($"[Win32BluetoothConnector] ========================================");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Win32BluetoothConnector] EXCEPTION in ConnectAudioDeviceAsync: {ex.Message}");
            Debug.WriteLine($"[Win32BluetoothConnector] Exception type: {ex.GetType().Name}");
            Debug.WriteLine($"[Win32BluetoothConnector] Stack trace: {ex.StackTrace}");
            Debug.WriteLine($"[Win32BluetoothConnector] ========================================");
            return false;
        }
    }

    /// <summary>
    /// Attempts to connect using the AudioPlaybackConnection API.
    /// This is the same API that Windows Settings uses when you click "Connect" on a Bluetooth audio device.
    /// </summary>
    /// <param name="address">The Bluetooth MAC address.</param>
    /// <returns>True if connection was successful; otherwise, false.</returns>
    private async Task<bool> TryConnectViaAudioPlaybackConnectionAsync(ulong address)
    {
        try
        {
            Debug.WriteLine($"[Win32BluetoothConnector] === BEGIN TryConnectViaAudioPlaybackConnectionAsync ===");
            Debug.WriteLine($"[Win32BluetoothConnector] Target address: {address:X12}");

            // Find the audio endpoint device ID for this Bluetooth address
            var audioDeviceId = await FindAudioPlaybackDeviceIdAsync(address);
            
            if (string.IsNullOrEmpty(audioDeviceId))
            {
                Debug.WriteLine($"[Win32BluetoothConnector] FAILED: No audio endpoint found for device {address:X12}");
                Debug.WriteLine($"[Win32BluetoothConnector] This means Windows hasn't created an audio endpoint yet.");
                Debug.WriteLine($"[Win32BluetoothConnector] The device may need to be connected once manually via Windows Settings first.");
                return false;
            }

            Debug.WriteLine($"[Win32BluetoothConnector] Found audio device ID: {audioDeviceId}");

            // Try to create an AudioPlaybackConnection
            Debug.WriteLine($"[Win32BluetoothConnector] Creating AudioPlaybackConnection...");
            var connectionResult = AudioPlaybackConnection.TryCreateFromId(audioDeviceId);
            
            if (connectionResult == null)
            {
                Debug.WriteLine($"[Win32BluetoothConnector] FAILED: AudioPlaybackConnection.TryCreateFromId returned null");
                Debug.WriteLine($"[Win32BluetoothConnector] The audio device may not support AudioPlaybackConnection (non-Bluetooth device?)");
                return false;
            }

            Debug.WriteLine($"[Win32BluetoothConnector] AudioPlaybackConnection created successfully");
            Debug.WriteLine($"[Win32BluetoothConnector] Calling StartAsync to begin monitoring...");

            // First call StartAsync to begin monitoring the connection
            await connectionResult.StartAsync();
            
            Debug.WriteLine($"[Win32BluetoothConnector] StartAsync completed. Now calling OpenAsync to establish audio connection...");
            
            // Then call OpenAsync to actually open the audio connection
            var openResult = await connectionResult.OpenAsync();
            
            Debug.WriteLine($"[Win32BluetoothConnector] OpenAsync completed with status: {openResult.Status}");

            if (openResult.Status == AudioPlaybackConnectionOpenResultStatus.Success)
            {
                Debug.WriteLine($"[Win32BluetoothConnector] SUCCESS: Audio connection established via AudioPlaybackConnection!");
                Debug.WriteLine($"[Win32BluetoothConnector] Audio should now route to the device.");
                Debug.WriteLine($"[Win32BluetoothConnector] === END TryConnectViaAudioPlaybackConnectionAsync (success) ===");
                return true;
            }
            else
            {
                Debug.WriteLine($"[Win32BluetoothConnector] FAILED: AudioPlaybackConnection.OpenAsync returned status: {openResult.Status}");
                switch (openResult.Status)
                {
                    case AudioPlaybackConnectionOpenResultStatus.RequestTimedOut:
                        Debug.WriteLine($"[Win32BluetoothConnector] The connection request timed out. Device may be out of range.");
                        break;
                    case AudioPlaybackConnectionOpenResultStatus.DeniedBySystem:
                        Debug.WriteLine($"[Win32BluetoothConnector] Connection denied by system. Check Bluetooth permissions.");
                        break;
                    case AudioPlaybackConnectionOpenResultStatus.UnknownFailure:
                        Debug.WriteLine($"[Win32BluetoothConnector] Unknown failure. Device may be in use by another application.");
                        break;
                }
                connectionResult.Dispose();
                Debug.WriteLine($"[Win32BluetoothConnector] === END TryConnectViaAudioPlaybackConnectionAsync (failed) ===");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Win32BluetoothConnector] EXCEPTION in TryConnectViaAudioPlaybackConnectionAsync: {ex.Message}");
            Debug.WriteLine($"[Win32BluetoothConnector] Exception type: {ex.GetType().Name}");
            Debug.WriteLine($"[Win32BluetoothConnector] Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Finds the audio playback device ID for a Bluetooth device by its MAC address.
    /// </summary>
    /// <param name="address">The Bluetooth MAC address.</param>
    /// <returns>The audio device ID suitable for AudioPlaybackConnection, or null if not found.</returns>
    private static async Task<string?> FindAudioPlaybackDeviceIdAsync(ulong address)
    {
        try
        {
            Debug.WriteLine($"[Win32BluetoothConnector] === BEGIN FindAudioPlaybackDeviceIdAsync for {address:X12} ===");
            
            // Query for audio output devices (speakers/headphones)
            // The AudioPlaybackConnection requires a device ID from audio endpoints, not Bluetooth devices
            var audioSelector = Windows.Media.Devices.MediaDevice.GetAudioRenderSelector();
            Debug.WriteLine($"[Win32BluetoothConnector] Audio selector: {audioSelector}");
            
            var additionalProperties = new[]
            {
                "System.Devices.ContainerId",
                "System.Devices.Aep.ContainerId",
                "System.Devices.DeviceInstanceId"
            };
            
            var audioDevices = await DeviceInformation.FindAllAsync(audioSelector, additionalProperties);

            Debug.WriteLine($"[Win32BluetoothConnector] Found {audioDevices.Count} audio render devices:");
            foreach (var ad in audioDevices)
            {
                var containerId = ad.Properties.TryGetValue("System.Devices.ContainerId", out var cid) ? cid?.ToString() : "N/A";
                var aepContainerId = ad.Properties.TryGetValue("System.Devices.Aep.ContainerId", out var acid) ? acid?.ToString() : "N/A";
                Debug.WriteLine($"[Win32BluetoothConnector]   - '{ad.Name}' | ContainerId={containerId} | AepContainerId={aepContainerId} | Id={ad.Id}");
            }

            // Also get the Bluetooth device to find its container ID
            Debug.WriteLine($"[Win32BluetoothConnector] Getting Bluetooth device from address {address:X12}...");
            var btDevice = await Windows.Devices.Bluetooth.BluetoothDevice.FromBluetoothAddressAsync(address);
            if (btDevice == null)
            {
                Debug.WriteLine($"[Win32BluetoothConnector] ERROR: Could not find Bluetooth device for address {address:X12}");
                return null;
            }
            Debug.WriteLine($"[Win32BluetoothConnector] Found Bluetooth device: '{btDevice.Name}' | DeviceId={btDevice.DeviceId} | ConnectionStatus={btDevice.ConnectionStatus}");

            // Get the container ID of the Bluetooth device
            var btProperties = new[] { "System.Devices.Aep.ContainerId", "System.Devices.ContainerId" };
            var btDeviceInfo = await DeviceInformation.CreateFromIdAsync(btDevice.DeviceId, btProperties);
            
            Guid? btContainerId = null;
            if (btDeviceInfo.Properties.TryGetValue("System.Devices.Aep.ContainerId", out var containerIdObj))
            {
                btContainerId = containerIdObj as Guid?;
                Debug.WriteLine($"[Win32BluetoothConnector] Bluetooth device AEP container ID: {btContainerId}");
            }
            else
            {
                Debug.WriteLine($"[Win32BluetoothConnector] Bluetooth device has no AEP container ID");
            }
            
            if (btDeviceInfo.Properties.TryGetValue("System.Devices.ContainerId", out var containerId2Obj))
            {
                var containerId2 = containerId2Obj as Guid?;
                Debug.WriteLine($"[Win32BluetoothConnector] Bluetooth device ContainerId: {containerId2}");
                if (!btContainerId.HasValue && containerId2.HasValue)
                {
                    btContainerId = containerId2;
                }
            }

            Debug.WriteLine($"[Win32BluetoothConnector] Searching for matching audio device...");
            
            // Find an audio device that matches the Bluetooth device's container ID
            foreach (var audioDevice in audioDevices)
            {
                // Check container ID match
                if (btContainerId.HasValue)
                {
                    if (audioDevice.Properties.TryGetValue("System.Devices.ContainerId", out var audioContainerIdObj))
                    {
                        if (audioContainerIdObj is Guid audioContainerId)
                        {
                            Debug.WriteLine($"[Win32BluetoothConnector] Comparing ContainerId: {audioContainerId} vs BT {btContainerId.Value} for '{audioDevice.Name}'");
                            if (audioContainerId == btContainerId.Value)
                            {
                                Debug.WriteLine($"[Win32BluetoothConnector] MATCH via ContainerId: '{audioDevice.Name}' ({audioDevice.Id})");
                                return audioDevice.Id;
                            }
                        }
                    }
                    
                    if (audioDevice.Properties.TryGetValue("System.Devices.Aep.ContainerId", out var aepContainerIdObj))
                    {
                        if (aepContainerIdObj is Guid aepContainerId)
                        {
                            Debug.WriteLine($"[Win32BluetoothConnector] Comparing AepContainerId: {aepContainerId} vs BT {btContainerId.Value} for '{audioDevice.Name}'");
                            if (aepContainerId == btContainerId.Value)
                            {
                                Debug.WriteLine($"[Win32BluetoothConnector] MATCH via AepContainerId: '{audioDevice.Name}' ({audioDevice.Id})");
                                return audioDevice.Id;
                            }
                        }
                    }
                }

                // Also try name-based matching as fallback
                if (audioDevice.Name.Contains("AirPods", StringComparison.OrdinalIgnoreCase) ||
                    audioDevice.Name.Contains(btDevice.Name, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"[Win32BluetoothConnector] MATCH via name fallback: '{audioDevice.Name}' ({audioDevice.Id})");
                    return audioDevice.Id;
                }
            }

            Debug.WriteLine($"[Win32BluetoothConnector] NO MATCH: No audio device found for Bluetooth device '{btDevice.Name}'");
            Debug.WriteLine($"[Win32BluetoothConnector] === END FindAudioPlaybackDeviceIdAsync (no match) ===");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Win32BluetoothConnector] ERROR in FindAudioPlaybackDeviceIdAsync: {ex.Message}");
            Debug.WriteLine($"[Win32BluetoothConnector] Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Enables audio services on a Bluetooth device using BluetoothSetServiceState.
    /// Uses BluetoothGetDeviceInfo to properly populate the BLUETOOTH_DEVICE_INFO struct.
    /// Returns both success status and whether the device appears to be connected elsewhere.
    /// </summary>
    private (bool success, bool likelyConnectedElsewhere) TryEnableAudioServicesWithDiagnostics(ulong address)
    {
        Debug.WriteLine($"[Win32BluetoothConnector] TryEnableAudioServices for {address:X12}");
        
        // First, get the full device info from Windows using BluetoothGetDeviceInfo
        // This properly populates the BLUETOOTH_DEVICE_INFO struct which is required for BluetoothSetServiceState
        var deviceInfo = new BLUETOOTH_DEVICE_INFO
        {
            dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>(),
            Address = address
        };
        
        uint getInfoResult = BluetoothGetDeviceInfo(IntPtr.Zero, ref deviceInfo);
        if (getInfoResult != ERROR_SUCCESS)
        {
            Debug.WriteLine($"[Win32BluetoothConnector] BluetoothGetDeviceInfo failed with error: {getInfoResult}");
            Debug.WriteLine($"[Win32BluetoothConnector] Device may not be paired. Please pair the device in Windows Settings first.");
            return (false, false);
        }
        
        Debug.WriteLine($"[Win32BluetoothConnector] Device info retrieved successfully:");
        Debug.WriteLine($"[Win32BluetoothConnector]   Name: {deviceInfo.szName}");
        Debug.WriteLine($"[Win32BluetoothConnector]   Authenticated: {deviceInfo.fAuthenticated}");
        Debug.WriteLine($"[Win32BluetoothConnector]   Connected: {deviceInfo.fConnected}");
        Debug.WriteLine($"[Win32BluetoothConnector]   Remembered: {deviceInfo.fRemembered}");
        Debug.WriteLine($"[Win32BluetoothConnector]   Class of Device: 0x{deviceInfo.ulClassofDevice:X6}");
        
        // If device is already connected, audio services are likely already enabled
        if (deviceInfo.fConnected)
        {
            Debug.WriteLine($"[Win32BluetoothConnector] Device is already connected - audio services should be active");
            // Still try to enable services in case they're not fully activated
        }
        
        if (!deviceInfo.fAuthenticated && !deviceInfo.fRemembered)
        {
            Debug.WriteLine($"[Win32BluetoothConnector] WARNING: Device is not authenticated/remembered. It may need to be paired first.");
        }
        
        bool anyEnabled = false;
        int alreadyEnabledCount = 0;
        int error87Count = 0; // Track ERROR_INVALID_PARAMETER which often means connected elsewhere

        // Define all audio service GUIDs to try, in priority order
        var servicesToTry = new (Guid guid, string name)[]
        {
            (AudioSinkServiceGuid, "A2DP Sink"),
            (AudioSourceServiceGuid, "A2DP Source"),
            (AdvancedAudioDistributionGuid, "A2DP Base"),
            (HandsfreeServiceGuid, "HFP"),
            (HandsfreeAudioGatewayGuid, "HFP AG"),
            (HeadsetServiceGuid, "HSP"),
            (HeadsetAudioGatewayGuid, "HSP AG"),
            (AvRemoteControlGuid, "AVRCP"),
            (AvRemoteControlTargetGuid, "AVRCP Target"),
        };

        Debug.WriteLine($"[Win32BluetoothConnector] Trying {servicesToTry.Length} standard audio service GUIDs...");

        foreach (var (guid, name) in servicesToTry)
        {
            var result = TrySetServiceStateWithDeviceInfo(ref deviceInfo, guid, name, enable: true);
            if (result == ServiceEnableResult.Success)
            {
                anyEnabled = true;
            }
            else if (result == ServiceEnableResult.AlreadyEnabled)
            {
                alreadyEnabledCount++;
            }
            else if (result == ServiceEnableResult.Error87)
            {
                error87Count++;
            }
        }

        // If standard GUIDs didn't work, try to query device's actual SDP services
        if (!anyEnabled && alreadyEnabledCount == 0)
        {
            Debug.WriteLine($"[Win32BluetoothConnector] Standard GUIDs failed, querying device's SDP records...");
            anyEnabled = TryEnableDeviceSdpServices(address, ref deviceInfo);
        }

        // If device is already connected and we got "already enabled" results, consider it success
        if (deviceInfo.fConnected && alreadyEnabledCount > 0)
        {
            Debug.WriteLine($"[Win32BluetoothConnector] Device is connected and {alreadyEnabledCount} service(s) appear to be already enabled");
            return (true, false);
        }

        // Detect "connected elsewhere" pattern: multiple error 87s on known-good services
        // Error 87 on A2DP Sink, HFP, AVRCP typically means device is busy with another connection
        bool likelyConnectedElsewhere = error87Count >= 3 && !deviceInfo.fConnected;
        if (likelyConnectedElsewhere)
        {
            Debug.WriteLine($"[Win32BluetoothConnector] DIAGNOSTIC: {error87Count} services returned error 87 - device is likely connected to another device");
        }

        Debug.WriteLine($"[Win32BluetoothConnector] TryEnableAudioServices result: {(anyEnabled ? "At least one service enabled" : "No services enabled")}");
        return (anyEnabled, likelyConnectedElsewhere);
    }

    /// <summary>
    /// Attempts to connect via RFCOMM socket to trigger a device connection switch.
    /// This can cause AirPods to disconnect from another device (like an iPhone) and connect to Windows.
    /// </summary>
    private async Task<bool> TryConnectViaRfcommAsync(ulong address)
    {
        try
        {
            Debug.WriteLine($"[Win32BluetoothConnector] === BEGIN TryConnectViaRfcommAsync ===");
            Debug.WriteLine($"[Win32BluetoothConnector] Target address: {address:X12}");
            
            var btAddress = BluetoothAddress.Parse(ConvertAddressToString(address));
            
            // Try connecting to common audio service endpoints
            // Even if the connection fails, the attempt may trigger the device to switch
            var servicesToTry = new (Guid guid, string name)[]
            {
                (BluetoothService.Handsfree, "Handsfree"),
                (BluetoothService.AudioSink, "A2DP Sink"),
                (BluetoothService.Headset, "Headset"),
            };

            foreach (var (serviceGuid, serviceName) in servicesToTry)
            {
                try
                {
                    Debug.WriteLine($"[Win32BluetoothConnector] Attempting RFCOMM connection to {serviceName} ({serviceGuid})...");
                    
                    using var client = new BluetoothClient();
                    var endpoint = new BluetoothEndPoint(btAddress, serviceGuid);
                    
                    // Use a timeout to avoid hanging
                    var connectTask = Task.Run(() =>
                    {
                        try
                        {
                            client.Connect(endpoint);
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    });
                    
                    var completedTask = await Task.WhenAny(connectTask, Task.Delay(3000));
                    
                    if (completedTask == connectTask && await connectTask)
                    {
                        Debug.WriteLine($"[Win32BluetoothConnector] RFCOMM connection to {serviceName} succeeded!");
                        // Don't close immediately - let the connection trigger the switch
                        await Task.Delay(500);
                        Debug.WriteLine($"[Win32BluetoothConnector] === END TryConnectViaRfcommAsync (success) ===");
                        return true;
                    }
                    else
                    {
                        Debug.WriteLine($"[Win32BluetoothConnector] RFCOMM connection to {serviceName} failed or timed out");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Win32BluetoothConnector] RFCOMM {serviceName} exception: {ex.Message}");
                }
            }
            
            Debug.WriteLine($"[Win32BluetoothConnector] === END TryConnectViaRfcommAsync (all failed) ===");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Win32BluetoothConnector] Error in TryConnectViaRfcommAsync: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Enables audio services on a Bluetooth device using BluetoothSetServiceState.
    /// Uses BluetoothGetDeviceInfo to properly populate the BLUETOOTH_DEVICE_INFO struct.
    /// </summary>
    private bool TryEnableAudioServices(ulong address)
    {
        var (success, _) = TryEnableAudioServicesWithDiagnostics(address);
        return success;
    }

    /// <summary>
    /// Logs additional device information when standard audio GUIDs don't work.
    /// This is a diagnostic fallback - the InTheHand.Net library doesn't support
    /// querying installed services on all platforms.
    /// </summary>
    private static bool TryEnableDeviceSdpServices(ulong address, ref BLUETOOTH_DEVICE_INFO deviceInfo)
    {
        try
        {
            Debug.WriteLine($"[Win32BluetoothConnector] === BEGIN TryEnableDeviceSdpServices ===");
            Debug.WriteLine($"[Win32BluetoothConnector] Standard audio GUIDs failed. Logging device info for diagnostics...");
            
            var btAddress = BluetoothAddress.Parse(ConvertAddressToString(address));
            var btDeviceInfo = new BluetoothDeviceInfo(btAddress);
            
            Debug.WriteLine($"[Win32BluetoothConnector] Device: {btDeviceInfo.DeviceName}");
            Debug.WriteLine($"[Win32BluetoothConnector] Class of Device: {btDeviceInfo.ClassOfDevice}");
            Debug.WriteLine($"[Win32BluetoothConnector] Authenticated: {btDeviceInfo.Authenticated}");
            Debug.WriteLine($"[Win32BluetoothConnector] Connected: {btDeviceInfo.Connected}");
            
            // Note: InTheHand.Net doesn't support InstalledServices on all platforms.
            // The Class of Device can help identify if this is an audio device:
            // - Major Class 4 (0x04) = Audio/Video
            // - Minor Class: 0x01=Wearable headset, 0x04=Microphone, 0x05=Loudspeaker, 0x06=Headphones
            var classOfDevice = btDeviceInfo.ClassOfDevice;
            Debug.WriteLine($"[Win32BluetoothConnector] Major Service Class: {classOfDevice.MajorDevice}");
            Debug.WriteLine($"[Win32BluetoothConnector] Device Class: {classOfDevice.Device}");
            Debug.WriteLine($"[Win32BluetoothConnector] Service Class: {classOfDevice.Service}");
            
            Debug.WriteLine($"[Win32BluetoothConnector] === END TryEnableDeviceSdpServices ===");
            Debug.WriteLine($"[Win32BluetoothConnector] RECOMMENDATION: The device may need to be connected manually via Windows Settings first.");
            Debug.WriteLine($"[Win32BluetoothConnector] After the first manual connection, programmatic connections should work.");
            
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Win32BluetoothConnector] Error in TryEnableDeviceSdpServices: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets a human-readable name for a Bluetooth service GUID.
    /// </summary>
    private static string GetBluetoothServiceName(Guid service)
    {
        // Check against known audio services
        if (service == BluetoothService.AudioSink) return "A2DP Sink";
        if (service == BluetoothService.AudioSource) return "A2DP Source";
        if (service == BluetoothService.AdvancedAudioDistribution) return "A2DP";
        if (service == BluetoothService.Handsfree) return "Handsfree";
        if (service == BluetoothService.HandsfreeAudioGateway) return "Handsfree AG";
        if (service == BluetoothService.Headset) return "Headset";
        if (service == BluetoothService.HeadsetAudioGateway) return "Headset AG";
        if (service == AvRemoteControlGuid) return "AVRCP";
        if (service == AvRemoteControlTargetGuid) return "AVRCP Target";
        if (service == BluetoothService.GenericAudio) return "Generic Audio";
        if (service == BluetoothService.SerialPort) return "Serial Port";
        if (service == BluetoothService.Panu) return "PANU";
        if (service == BluetoothService.Nap) return "NAP";
        if (service == BluetoothService.ObexObjectPush) return "OBEX Object Push";
        if (service == BluetoothService.ObexFileTransfer) return "OBEX File Transfer";
        
        // Return the short UUID portion for standard services
        var uuidStr = service.ToString().ToUpperInvariant();
        if (uuidStr.EndsWith("-0000-1000-8000-00805F9B34FB"))
        {
            // Standard Bluetooth UUID, extract the short form
            var shortUuid = uuidStr.Substring(4, 4);
            return $"BT 0x{shortUuid}";
        }
        
        return "Custom Service";
    }
    
    private enum ServiceEnableResult
    {
        Success,
        AlreadyEnabled,
        Failed,
        NotSupported,
        Error87 // ERROR_INVALID_PARAMETER - often means device is connected to another host
    }

    /// <summary>
    /// Checks if a Bluetooth device is the current default audio output.
    /// </summary>
    /// <param name="address">The Bluetooth MAC address.</param>
    /// <returns>True if the device is the default audio output; otherwise, false.</returns>
    public static async Task<bool> IsDefaultAudioOutputAsync(ulong address)
    {
        try
        {
            var audioDeviceId = await FindAudioPlaybackDeviceIdAsync(address);
            if (string.IsNullOrEmpty(audioDeviceId))
            {
                return false;
            }

            var defaultAudioId = Windows.Media.Devices.MediaDevice.GetDefaultAudioRenderId(Windows.Media.Devices.AudioDeviceRole.Default);
            return audioDeviceId == defaultAudioId;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Win32BluetoothConnector] Error checking default audio output: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sets a Bluetooth device as the default audio output.
    /// </summary>
    /// <param name="address">The Bluetooth MAC address.</param>
    /// <returns>True if the device was successfully set as default; otherwise, false.</returns>
    /// <remarks>
    /// This uses the undocumented IPolicyConfig COM interface, which works on Windows 7-11
    /// but is not officially supported by Microsoft and may break in future updates.
    /// </remarks>
    public static async Task<bool> SetDefaultAudioOutputAsync(ulong address)
    {
        try
        {
            Debug.WriteLine($"[Win32BluetoothConnector] === SET DEFAULT AUDIO OUTPUT ===");
            Debug.WriteLine($"[Win32BluetoothConnector] Setting default audio output to device {address:X12}...");

            var audioDeviceId = await FindAudioPlaybackDeviceIdAsync(address);
            if (string.IsNullOrEmpty(audioDeviceId))
            {
                Debug.WriteLine($"[Win32BluetoothConnector] FAILED: No audio endpoint found for device {address:X12}");
                Debug.WriteLine($"[Win32BluetoothConnector] Device must be connected before it can be set as default.");
                return false;
            }

            Debug.WriteLine($"[Win32BluetoothConnector] Found audio device ID: {audioDeviceId}");
            Debug.WriteLine($"[Win32BluetoothConnector] Calling PolicyConfigClient.SetDefaultEndpointForAllRoles...");

            bool result = PolicyConfigClient.SetDefaultEndpointForAllRoles(audioDeviceId);

            if (result)
            {
                Debug.WriteLine($"[Win32BluetoothConnector] SUCCESS: Device {address:X12} set as default audio output");
            }
            else
            {
                Debug.WriteLine($"[Win32BluetoothConnector] FAILED: Could not set device as default audio output");
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Win32BluetoothConnector] Error setting default audio output: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Verifies that an audio endpoint exists for a device after connection.
    /// Useful for debugging audio routing issues.
    /// </summary>
    /// <param name="address">The Bluetooth MAC address.</param>
    /// <returns>True if an audio endpoint was found; otherwise, false.</returns>
    public static async Task<bool> VerifyAudioEndpointExistsAsync(ulong address)
    {
        try
        {
            Debug.WriteLine($"[Win32BluetoothConnector] === VERIFY AUDIO ENDPOINT ===");
            Debug.WriteLine($"[Win32BluetoothConnector] Checking for audio endpoint for {address:X12}...");
            
            var audioDeviceId = await FindAudioPlaybackDeviceIdAsync(address);
            
            if (!string.IsNullOrEmpty(audioDeviceId))
            {
                Debug.WriteLine($"[Win32BluetoothConnector] VERIFIED: Audio endpoint exists: {audioDeviceId}");
                
                // Check if this is the default audio device
                var defaultAudioId = Windows.Media.Devices.MediaDevice.GetDefaultAudioRenderId(Windows.Media.Devices.AudioDeviceRole.Default);
                var isDefault = audioDeviceId == defaultAudioId;
                Debug.WriteLine($"[Win32BluetoothConnector] Is default audio device: {isDefault}");
                Debug.WriteLine($"[Win32BluetoothConnector] Default audio device ID: {defaultAudioId}");
                
                return true;
            }
            else
            {
                Debug.WriteLine($"[Win32BluetoothConnector] NOT FOUND: No audio endpoint exists for {address:X12}");
                Debug.WriteLine($"[Win32BluetoothConnector] This usually means:");
                Debug.WriteLine($"[Win32BluetoothConnector]   1. Device has never been connected (needs first manual connection)");
                Debug.WriteLine($"[Win32BluetoothConnector]   2. Device is not paired");
                Debug.WriteLine($"[Win32BluetoothConnector]   3. Windows hasn't created the audio endpoint yet");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Win32BluetoothConnector] Error verifying audio endpoint: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Enables or disables a specific Bluetooth service using BluetoothSetServiceState with a properly populated device info.
    /// </summary>
    /// <remarks>
    /// This method uses the Win32 BluetoothSetServiceState API via P/Invoke,
    /// which is the proper way to activate Bluetooth profiles like A2DP, HFP, and AVRCP.
    /// This is the same API that Windows Settings uses when you click "Connect".
    /// The deviceInfo struct must have been populated by BluetoothGetDeviceInfo first.
    /// </remarks>
    private static ServiceEnableResult TrySetServiceStateWithDeviceInfo(ref BLUETOOTH_DEVICE_INFO deviceInfo, Guid serviceGuid, string serviceName, bool enable)
    {
        try
        {
            Debug.WriteLine($"[Win32BluetoothConnector] {(enable ? "Enabling" : "Disabling")} {serviceName} service ({serviceGuid})...");

            var serviceGuidCopy = serviceGuid;
            uint flags = enable ? BLUETOOTH_SERVICE_ENABLE : BLUETOOTH_SERVICE_DISABLE;
            
            uint result = BluetoothSetServiceState(
                IntPtr.Zero,        // Use any available radio
                ref deviceInfo,
                ref serviceGuidCopy,
                flags);

            if (result == ERROR_SUCCESS)
            {
                Debug.WriteLine($"[Win32BluetoothConnector] Successfully {(enable ? "enabled" : "disabled")} {serviceName} service");
                return ServiceEnableResult.Success;
            }
            else
            {
                Debug.WriteLine($"[Win32BluetoothConnector] BluetoothSetServiceState returned error code: {result} for {serviceName}");
                // Common error codes:
                // 87 = ERROR_INVALID_PARAMETER - could mean service is already enabled, or device is connected to another host
                // 1060 = ERROR_SERVICE_DOES_NOT_EXIST - service not supported by device
                // 1062 = ERROR_SERVICE_NOT_ACTIVE
                
                if (result == 87)
                {
                    if (deviceInfo.fConnected)
                    {
                        // Error 87 on a connected device likely means the service is already enabled
                        Debug.WriteLine($"[Win32BluetoothConnector] {serviceName}: Error 87 on connected device - service may already be enabled");
                        return ServiceEnableResult.AlreadyEnabled;
                    }
                    else
                    {
                        // Error 87 on a disconnected device often means it's connected to another host
                        Debug.WriteLine($"[Win32BluetoothConnector] {serviceName}: Error 87 on disconnected device - may be connected to another device");
                        return ServiceEnableResult.Error87;
                    }
                }
                else if (result == 1060)
                {
                    Debug.WriteLine($"[Win32BluetoothConnector] {serviceName}: Service not supported by this device");
                    return ServiceEnableResult.NotSupported;
                }
                
                return ServiceEnableResult.Failed;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Win32BluetoothConnector] Failed to {(enable ? "enable" : "disable")} {serviceName}: {ex.Message}");
            return ServiceEnableResult.Failed;
        }
    }

    /// <summary>
    /// Enables or disables a specific Bluetooth service using BluetoothSetServiceState.
    /// This method first calls BluetoothGetDeviceInfo to properly populate the device info.
    /// </summary>
    private static bool TrySetServiceState(ulong address, Guid serviceGuid, string serviceName, bool enable)
    {
        try
        {
            Debug.WriteLine($"[Win32BluetoothConnector] {(enable ? "Enabling" : "Disabling")} {serviceName} service ({serviceGuid})...");
            
            var deviceInfo = new BLUETOOTH_DEVICE_INFO
            {
                dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>(),
                Address = address
            };

            // First get the device info to properly populate the struct
            uint getInfoResult = BluetoothGetDeviceInfo(IntPtr.Zero, ref deviceInfo);
            if (getInfoResult != ERROR_SUCCESS)
            {
                Debug.WriteLine($"[Win32BluetoothConnector] BluetoothGetDeviceInfo failed with error: {getInfoResult} for {serviceName}");
                return false;
            }

            var result = TrySetServiceStateWithDeviceInfo(ref deviceInfo, serviceGuid, serviceName, enable);
            return result == ServiceEnableResult.Success || result == ServiceEnableResult.AlreadyEnabled;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Win32BluetoothConnector] Failed to {(enable ? "enable" : "disable")} {serviceName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets information about a Bluetooth device including its supported services.
    /// </summary>
    public BluetoothDeviceInfo? GetDeviceInfo(ulong address)
    {
        try
        {
            var btAddress = BluetoothAddress.Parse(ConvertAddressToString(address));
            return new BluetoothDeviceInfo(btAddress);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if a device supports audio services (A2DP, HSP, or HFP).
    /// </summary>
    /// <remarks>
    /// Note: This method attempts connection to determine service support
    /// since InstalledServices may not be available on all platforms.
    /// </remarks>
    public bool SupportsAudioServices(ulong address)
    {
        try
        {
            var btAddress = BluetoothAddress.Parse(ConvertAddressToString(address));
            
            // Try to query device for services (may not work on all platforms)
            using var client = new BluetoothClient();
            
            // If we can get device info, it's likely an audio device if paired
            var device = new BluetoothDeviceInfo(btAddress);
            return device.Authenticated; // If authenticated, assume it has services
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Converts a ulong Bluetooth address to a formatted string.
    /// </summary>
    private static string ConvertAddressToString(ulong address)
    {
        // Convert to hex string with colons: XX:XX:XX:XX:XX:XX
        var bytes = BitConverter.GetBytes(address);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        
        // Take only the last 6 bytes (Bluetooth address is 48-bit)
        return string.Join(":", bytes.Skip(2).Select(b => b.ToString("X2")));
    }

    /// <summary>
    /// Attempts to disconnect from a Bluetooth audio device using its MAC address.
    /// </summary>
    /// <param name="address">The Bluetooth MAC address as a ulong.</param>
    /// <returns>True if disconnection was successful; otherwise, false.</returns>
    /// <remarks>
    /// Uses <c>BluetoothSetServiceState</c> Win32 API to deactivate audio profiles,
    /// which is the proper Windows API for disconnecting from A2DP/HFP audio devices.
    /// </remarks>
    public async Task<bool> DisconnectAudioDeviceAsync(ulong address)
    {
        try
        {
            Debug.WriteLine($"[Win32BluetoothConnector] Attempting to disconnect from device: {address:X12}");

            // Use BluetoothSetServiceState to deactivate audio profiles
            var disconnected = await Task.Run(() => TryDisableAudioServices(address));
            
            if (disconnected)
            {
                Debug.WriteLine($"[Win32BluetoothConnector] Successfully disconnected from {address:X12}");
                return true;
            }
            else
            {
                Debug.WriteLine($"[Win32BluetoothConnector] Failed to disconnect from {address:X12}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Win32BluetoothConnector] Disconnection error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Disables audio services on a Bluetooth device using BluetoothSetServiceState.
    /// </summary>
    private bool TryDisableAudioServices(ulong address)
    {
        bool anyDisabled = false;

        // Disable each audio service
        anyDisabled |= TrySetServiceState(address, AudioSinkServiceGuid, "A2DP", enable: false);
        anyDisabled |= TrySetServiceState(address, HandsfreeServiceGuid, "HFP", enable: false);
        anyDisabled |= TrySetServiceState(address, HeadsetServiceGuid, "HSP", enable: false);
        anyDisabled |= TrySetServiceState(address, AvRemoteControlGuid, "AVRCP", enable: false);

        return anyDisabled;
    }

    /// <summary>
    /// Discovers all paired Bluetooth devices using Win32 APIs.
    /// </summary>
    public static IReadOnlyList<BluetoothDeviceInfo> DiscoverPairedDevices()
    {
        try
        {
            using var client = new BluetoothClient();
            // Discover devices with correct parameter order: maxDevices, authenticated, remembered, unknown
            var devices = client.DiscoverDevices();
            // Filter for authenticated (paired) devices only
            return devices.Where(d => d.Authenticated).ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Win32BluetoothConnector] Failed to discover devices: {ex.Message}");
            return Array.Empty<BluetoothDeviceInfo>();
        }
    }
}
