using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceCommunication.Models;
using DeviceCommunication.Services;

namespace GUI.ViewModels;

/// <summary>
/// Immutable state object that encapsulates connection-related state and computed properties.
/// Using a record reduces multiple PropertyChanged events to a single state update.
/// </summary>
/// <param name="IsConnected">Whether the device is currently connected.</param>
/// <param name="IsConnecting">Whether a connection attempt is in progress.</param>
/// <param name="IsDefaultAudioOutput">Whether this device is the current default audio output.</param>
/// <param name="PairedDeviceId">The Windows device ID of the paired device (null if not paired).</param>
public sealed record ConnectionState(
    bool IsConnected,
    bool IsConnecting,
    bool IsDefaultAudioOutput,
    string? PairedDeviceId)
{
    /// <summary>
    /// Gets whether the Connect button should be enabled.
    /// Disabled when connecting, already connected, or device not paired in Windows.
    /// </summary>
    public bool CanConnectButton => !IsConnected && !IsConnecting && !string.IsNullOrEmpty(PairedDeviceId);

    /// <summary>
    /// Gets whether to show the Connect button (show when not connected and not connecting).
    /// </summary>
    public bool ShowConnectButton => !IsConnected && !IsConnecting;

    /// <summary>
    /// Gets whether the toggle connection button can be used.
    /// Disabled when connecting or when device is not paired in Windows.
    /// </summary>
    public bool CanToggleConnection => !IsConnecting && !string.IsNullOrEmpty(PairedDeviceId);

    /// <summary>
    /// Gets the text to display on the connection toggle button.
    /// Shows "Connect" when disconnected, "Disconnect" when connected.
    /// </summary>
    public string ConnectionButtonText => IsConnected ? "Disconnect" : "Connect";

    /// <summary>
    /// Gets the tooltip text for the connection toggle button.
    /// Provides context-specific help text based on connection state.
    /// </summary>
    public string ConnectionButtonTooltip => IsConnected
        ? "Disconnect from device"
        : string.IsNullOrEmpty(PairedDeviceId)
            ? "Device not paired. Click to open Bluetooth settings."
            : "Connect to device";

    /// <summary>
    /// Gets whether the device needs pairing (shows warning icon on header).
    /// True when device is not found in Windows paired devices list.
    /// </summary>
    public bool ShowPairingWarning => string.IsNullOrEmpty(PairedDeviceId) && !IsConnected;

    /// <summary>
    /// Gets whether to show the "Connect Audio" button.
    /// Shows when device is paired, not currently connecting, and not already the default audio output.
    /// </summary>
    public bool ShowConnectAudioButton => !string.IsNullOrEmpty(PairedDeviceId) && !IsConnecting && !IsDefaultAudioOutput;

    /// <summary>
    /// Gets whether the "Connect Audio" button should be enabled.
    /// </summary>
    public bool CanConnectAudio => !string.IsNullOrEmpty(PairedDeviceId) && !IsConnecting && !IsDefaultAudioOutput;

    /// <summary>
    /// Creates a default disconnected state with no paired device.
    /// </summary>
    public static ConnectionState Default => new(false, false, false, null);
}

/// <summary>
/// ViewModel wrapper for AirPodsDeviceInfo that provides property change notifications.
/// This allows the UI to update only when properties actually change.
/// </summary>
public partial class AirPodsDeviceViewModel : ObservableObject
{
    private readonly IBluetoothConnectionService _connectionService;

    public ulong Address { get; private set; }
    
    /// <summary>
    /// The Product ID that uniquely identifies the device model.
    /// </summary>
    public ushort ProductId { get; private set; }
    
    /// <summary>
    /// The Windows device ID of the paired device (used for connections).
    /// </summary>
    public string? PairedDeviceId { get; private set; }

    [ObservableProperty]
    private string _model = string.Empty;

    [ObservableProperty]
    private string _deviceName = string.Empty;

    [ObservableProperty]
    private int? _leftBattery;

    [ObservableProperty]
    private int? _rightBattery;

    [ObservableProperty]
    private int? _caseBattery;

    [ObservableProperty]
    private bool _isLeftCharging;

    [ObservableProperty]
    private bool _isRightCharging;

    [ObservableProperty]
    private bool _isCaseCharging;

    [ObservableProperty]
    private bool _isLeftInEar;

    [ObservableProperty]
    private bool _isRightInEar;

    [ObservableProperty]
    private bool _isLidOpen;

    /// <summary>
    /// Connection state containing IsConnected, IsConnecting, IsDefaultAudioOutput, and PairedDeviceId.
    /// All connection-related computed properties are derived from this single state object.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConnectAudioCommand))]
    private ConnectionState _connection = ConnectionState.Default;

    [ObservableProperty]
    private int _signalStrength;

    [ObservableProperty]
    private DateTime _lastSeen;

    /// <summary>
    /// Gets whether the device is currently active (seen within last 5 seconds).
    /// UI can bind to this to show visual inactive state.
    /// </summary>
    public bool IsActive => (DateTime.Now - LastSeen).TotalSeconds < 5;

    // Connection-related computed properties delegate to ConnectionState
    /// <summary>Gets whether the Connect button should be enabled.</summary>
    public bool CanConnectButton => Connection.CanConnectButton;

    /// <summary>Gets whether to show the Connect button.</summary>
    public bool ShowConnectButton => Connection.ShowConnectButton;

    /// <summary>
    /// Gets whether to show the Disconnect button.
    /// Always false - Windows doesn't provide an API to disconnect without unpairing.
    /// </summary>
    public bool ShowDisconnectButton => false;

    /// <summary>Gets whether the toggle connection button can be used.</summary>
    public bool CanToggleConnection => Connection.CanToggleConnection;

    /// <summary>Gets the text to display on the connection toggle button.</summary>
    public string ConnectionButtonText => Connection.ConnectionButtonText;

    /// <summary>Gets the tooltip text for the connection toggle button.</summary>
    public string ConnectionButtonTooltip => Connection.ConnectionButtonTooltip;

    /// <summary>Gets whether the device needs pairing (shows warning icon on header).</summary>
    public bool ShowPairingWarning => Connection.ShowPairingWarning;

    /// <summary>Gets whether to show the "Connect Audio" button.</summary>
    public bool ShowConnectAudioButton => Connection.ShowConnectAudioButton;

    /// <summary>Gets whether the "Connect Audio" button should be enabled.</summary>
    public bool CanConnectAudio => Connection.CanConnectAudio;

    // Expose individual connection properties for backward compatibility with existing bindings
    /// <summary>Gets whether the device is currently connected.</summary>
    public bool IsConnected => Connection.IsConnected;

    /// <summary>Gets whether a connection attempt is in progress.</summary>
    public bool IsConnecting => Connection.IsConnecting;

    /// <summary>Gets whether this device is the current default audio output.</summary>
    public bool IsDefaultAudioOutput => Connection.IsDefaultAudioOutput;

    public AirPodsDeviceViewModel(AirPodsDeviceInfo deviceInfo, IBluetoothConnectionService connectionService)
    {
        Address = deviceInfo.Address;
        ProductId = deviceInfo.ProductId;
        PairedDeviceId = deviceInfo.PairedDeviceId;
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        
        UpdateFrom(deviceInfo);
    }

    /// <summary>
    /// Updates properties from a device info object.
    /// The [ObservableProperty] generated setters already handle equality checks,
    /// so PropertyChanged only fires when values actually change.
    /// </summary>
    public void UpdateFrom(AirPodsDeviceInfo deviceInfo)
    {
        Address = deviceInfo.Address; // Update address in case MAC rotated
        ProductId = deviceInfo.ProductId;
        PairedDeviceId = deviceInfo.PairedDeviceId; // Update paired device ID
        Model = deviceInfo.Model;
        DeviceName = deviceInfo.DeviceName;
        LeftBattery = deviceInfo.LeftBattery;
        RightBattery = deviceInfo.RightBattery;
        CaseBattery = deviceInfo.CaseBattery;
        IsLeftCharging = deviceInfo.IsLeftCharging;
        IsRightCharging = deviceInfo.IsRightCharging;
        IsCaseCharging = deviceInfo.IsCaseCharging;
        IsLeftInEar = deviceInfo.IsLeftInEar;
        IsRightInEar = deviceInfo.IsRightInEar;
        IsLidOpen = deviceInfo.IsLidOpen;
        SignalStrength = deviceInfo.SignalStrength;
        LastSeen = deviceInfo.LastSeen;
        
        // Update connection state - single property change notification for all connection-related properties
        Connection = Connection with 
        { 
            IsConnected = deviceInfo.IsConnected,
            PairedDeviceId = deviceInfo.PairedDeviceId
        };
        
        // Notify time-based computed property
        OnPropertyChanged(nameof(IsActive));
    }

    /// <summary>
    /// Refreshes the IsActive property to reflect current time.
    /// Call this periodically to update the active/inactive visual state.
    /// </summary>
    public void RefreshIsActive()
    {
        OnPropertyChanged(nameof(IsActive));
    }

    /// <summary>
    /// Refreshes the IsDefaultAudioOutput property by checking the current Windows audio output.
    /// Call this after connection attempts or periodically to keep the UI in sync.
    /// </summary>
    public async Task RefreshDefaultAudioOutputStatusAsync()
    {
        if (Address == 0)
        {
            Connection = Connection with { IsDefaultAudioOutput = false };
            return;
        }

        try
        {
            var isDefault = await Win32BluetoothConnector.IsDefaultAudioOutputAsync(Address);
            Connection = Connection with { IsDefaultAudioOutput = isDefault };
        }
        catch
        {
            Connection = Connection with { IsDefaultAudioOutput = false };
        }
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        if (IsConnecting || IsConnected || string.IsNullOrEmpty(Connection.PairedDeviceId))
            return;

        try
        {
            Connection = Connection with { IsConnecting = true };
            
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Connecting to {Model} via device ID {Connection.PairedDeviceId}...");
            
            var result = await _connectionService.ConnectByDeviceIdAsync(Connection.PairedDeviceId);
            
            switch (result)
            {
                case ConnectionResult.Connected:
                    System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Connection initiated for {Model} using Win32 Bluetooth APIs");
                    System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Connection should establish within 1-2 seconds");
                    
                    // Wait a bit for connection to establish
                    await Task.Delay(1500);
                    
                    // Check connection status
                    if (IsConnected)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Successfully connected to {Model}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Connection may still be establishing. Status will update when connected.");
                    }
                    break;
                    
                case ConnectionResult.DeviceNotFound:
                    System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] {Model} not found (may have been unpaired)");
                    Connection = Connection with { PairedDeviceId = null };
                    PairedDeviceId = null; // Keep backward-compatible property in sync
                    break;
                    
                case ConnectionResult.Failed:
                    System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Failed to connect to {Model}. Opening Bluetooth settings as fallback...");
                    // Open settings as fallback for failed connections
                    await BluetoothConnectionService.OpenBluetoothSettingsForDeviceAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Error connecting: {ex.Message}");
        }
        finally
        {
            Connection = Connection with { IsConnecting = false };
        }
    }

    private bool CanConnect() => !IsConnecting && !IsConnected && !string.IsNullOrEmpty(Connection.PairedDeviceId);

    [RelayCommand]
    private static async Task OpenBluetoothSettingsAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[AirPodsDeviceViewModel] Opening Windows Bluetooth settings...");
            await global::Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:bluetooth"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Error opening Bluetooth settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Explicitly connects the audio profiles (A2DP, HFP) for this device.
    /// Use this when the device appears connected but audio isn't routing to it.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanConnectAudio))]
    private async Task ConnectAudioAsync()
    {
        if (IsConnecting || string.IsNullOrEmpty(Connection.PairedDeviceId))
            return;

        try
        {
            Connection = Connection with { IsConnecting = true };
            
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Activating A2DP/HFP audio for {Model}...");
            
            var result = await _connectionService.ConnectByDeviceIdAsync(Connection.PairedDeviceId);
            
            switch (result)
            {
                case ConnectionResult.Connected:
                    System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] A2DP/HFP audio profiles activated for {Model}");
                    System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Audio should now route to device within 1-2 seconds");
                    
                    // Wait for audio to route
                    await Task.Delay(1500);
                    
                    // Refresh audio output status
                    await RefreshDefaultAudioOutputStatusAsync();
                    break;
                    
                case ConnectionResult.DeviceNotFound:
                    System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] {Model} not found (may have been unpaired)");
                    Connection = Connection with { PairedDeviceId = null };
                    PairedDeviceId = null; // Keep backward-compatible property in sync
                    break;
                    
                case ConnectionResult.Failed:
                    System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Failed to activate audio for {Model}. Try Windows Bluetooth settings.");
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Error activating audio: {ex.Message}");
        }
        finally
        {
            Connection = Connection with { IsConnecting = false };
        }
    }

    [RelayCommand(CanExecute = nameof(CanToggleConnection))]
    private async Task ToggleConnectionAsync()
    {
        if (IsConnected)
        {
            await DisconnectAsync();
        }
        else
        {
            await ConnectAsync();
        }
    }

    private async Task DisconnectAsync()
    {
        if (!IsConnected || IsConnecting || string.IsNullOrEmpty(Connection.PairedDeviceId))
            return;

        try
        {
            Connection = Connection with { IsConnecting = true };
            
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Disconnecting from {Model} via device ID {Connection.PairedDeviceId}...");
            
            var success = await _connectionService.DisconnectByDeviceIdAsync(Connection.PairedDeviceId);
            
            if (success)
            {
                System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Disconnect initiated for {Model}");
                
                // Wait a bit for disconnection to complete
                await Task.Delay(1000);
                
                if (!IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Successfully disconnected from {Model}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Disconnection may still be processing. Status will update when disconnected.");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Failed to disconnect from {Model}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Error disconnecting: {ex.Message}");
        }
        finally
        {
            Connection = Connection with { IsConnecting = false };
        }
    }
}
