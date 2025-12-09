using System.Diagnostics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PodPilot.Core.Models;
using PodPilot.Core.Services;

namespace PodPilot.Core.ViewModels;

/// <summary>
/// ViewModel wrapper for AirPodsDeviceInfo that provides property change notifications.
/// Uses a simplified state machine based on DeviceStatus for clear button logic.
/// </summary>
public partial class AirPodsDeviceViewModel : ObservableObject
{
    private readonly IBluetoothConnectionService _connectionService;
    private readonly IDeviceStateManager? _stateManager;
    private readonly IAudioOutputService? _audioOutputService;
    private readonly ISystemLauncherService? _systemLauncherService;

    [Conditional("DEBUG")]
    private static void LogDebug(string message) => Debug.WriteLine($"[AirPodsDeviceVM] {message}");

    public ulong Address { get; private set; }
    
    /// <summary>
    /// The Product ID that uniquely identifies the device model.
    /// </summary>
    public ushort ProductId { get; private set; }
    
    /// <summary>
    /// The Windows device ID of the paired device (used for connections).
    /// </summary>
    public string? PairedDeviceId { get; private set; }
    
    /// <summary>
    /// The Bluetooth Classic address of the paired device (used for audio output checks).
    /// This is distinct from Address which is the rotating BLE advertisement address.
    /// </summary>
    public ulong? PairedBluetoothAddress { get; private set; }

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

    [ObservableProperty]
    private int _signalStrength;

    [ObservableProperty]
    private DateTime _lastSeen;

    // ===== Core State Properties =====
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status))]
    [NotifyPropertyChangedFor(nameof(ShowPairingWarning))]
    [NotifyPropertyChangedFor(nameof(PrimaryButtonIcon))]
    [NotifyPropertyChangedFor(nameof(PrimaryButtonTooltip))]
    [NotifyPropertyChangedFor(nameof(CanExecutePrimaryAction))]
    [NotifyPropertyChangedFor(nameof(PrimaryActionCommand))]
    private bool _isConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExecutePrimaryAction))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status))]
    [NotifyPropertyChangedFor(nameof(PrimaryButtonIcon))]
    [NotifyPropertyChangedFor(nameof(PrimaryButtonTooltip))]
    [NotifyPropertyChangedFor(nameof(PrimaryActionCommand))]
    private bool _isDefaultAudioOutput;

    // ===== Computed Properties =====

    /// <summary>
    /// Gets whether the device is currently active (seen within 5 seconds).
    /// </summary>
    public bool IsActive => (DateTime.Now - LastSeen).TotalSeconds < 5;

    /// <summary>
    /// Gets the current device status based on pairing, connection, and audio state.
    /// This is the single source of truth for UI logic.
    /// Note: IsDefaultAudioOutput implies IsConnected, so we check it first to handle
    /// race conditions where audio routing events fire before BLE discovery updates.
    /// </summary>
    public DeviceStatus Status
    {
        get
        {
            if (string.IsNullOrEmpty(PairedDeviceId))
                return DeviceStatus.Unpaired;
            if (IsDefaultAudioOutput)
                return DeviceStatus.AudioActive;
            if (!IsConnected)
                return DeviceStatus.Disconnected;
            return DeviceStatus.Connected;
        }
    }

    /// <summary>
    /// Gets whether to show the pairing warning icon (??).
    /// </summary>
    public bool ShowPairingWarning => Status == DeviceStatus.Unpaired;

    /// <summary>
    /// Gets the icon for the primary action button.
    /// </summary>
    public string PrimaryButtonIcon => Status switch
    {
        DeviceStatus.Unpaired => "🔗",
        DeviceStatus.Disconnected => "🛜 ",
        DeviceStatus.Connected => "🔉",
        DeviceStatus.AudioActive => "⛔",
        _ => "?"
    };

    /// <summary>
    /// Gets the tooltip for the primary action button.
    /// </summary>
    public string PrimaryButtonTooltip => Status switch
    {
        DeviceStatus.Unpaired => "Device not paired. Click to open Bluetooth settings.",
        DeviceStatus.Disconnected => "Connect to device",
        DeviceStatus.Connected => "Route audio to this device",
        DeviceStatus.AudioActive => "Disconnect from device",
        _ => "Connect"
    };

    /// <summary>
    /// Gets whether the primary action can be executed.
    /// Disabled when busy or when unpaired (but still clickable to open settings).
    /// </summary>
    public bool CanExecutePrimaryAction => !IsBusy && Status != DeviceStatus.Unpaired;

    /// <summary>
    /// Gets the command for the primary action based on current status.
    /// </summary>
    public ICommand PrimaryActionCommand => Status switch
    {
        DeviceStatus.Unpaired => OpenBluetoothSettingsCommand,
        DeviceStatus.Disconnected => ConnectCommand,
        DeviceStatus.Connected => ConnectAudioCommand,
        DeviceStatus.AudioActive => DisconnectCommand,
        _ => ConnectCommand
    };

    public AirPodsDeviceViewModel(
        AirPodsState state, 
        IBluetoothConnectionService connectionService)
        : this(state, connectionService, null, null, null)
    {
    }

    public AirPodsDeviceViewModel(
        AirPodsState state, 
        IBluetoothConnectionService connectionService, 
        IDeviceStateManager? stateManager)
        : this(state, connectionService, stateManager, null, null)
    {
    }

    public AirPodsDeviceViewModel(
        AirPodsState state, 
        IBluetoothConnectionService connectionService, 
        IDeviceStateManager? stateManager,
        IAudioOutputService? audioOutputService,
        ISystemLauncherService? systemLauncherService)
    {
        Address = state.BleAddress ?? 0;
        ProductId = state.ProductId;
        PairedDeviceId = state.PairedDeviceId;
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _stateManager = stateManager;
        _audioOutputService = audioOutputService;
        _systemLauncherService = systemLauncherService;
        
        UpdateFromState(state);
    }

    /// <summary>
    /// Refreshes the IsActive property to reflect current time.
    /// </summary>
    public void RefreshIsActive() => OnPropertyChanged(nameof(IsActive));

    /// <summary>
    /// Updates the ViewModel from the centralized AirPodsState.
    /// This is the primary update path when using the new architecture.
    /// </summary>
    public void UpdateFromState(AirPodsState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        
        PairedDeviceId = state.PairedDeviceId;
        PairedBluetoothAddress = state.BluetoothAddress;
        Address = state.BleAddress ?? 0;
        Model = state.ModelName;
        DeviceName = state.Name;
        LeftBattery = state.LeftBattery;
        RightBattery = state.RightBattery;
        CaseBattery = state.CaseBattery;
        IsLeftCharging = state.IsLeftCharging;
        IsRightCharging = state.IsRightCharging;
        IsCaseCharging = state.IsCaseCharging;
        IsLeftInEar = state.IsLeftInEar;
        IsRightInEar = state.IsRightInEar;
        IsLidOpen = state.IsLidOpen;
        SignalStrength = state.SignalStrength;
        LastSeen = state.LastSeen;
        
        // State service handles lockout periods, so we trust its state
        IsConnected = state.IsConnected;
        IsDefaultAudioOutput = state.IsAudioConnected;
        
        // Notify computed properties
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(ShowPairingWarning));
        OnPropertyChanged(nameof(PrimaryButtonIcon));
        OnPropertyChanged(nameof(PrimaryButtonTooltip));
        OnPropertyChanged(nameof(CanExecutePrimaryAction));
        OnPropertyChanged(nameof(PrimaryActionCommand));
        OnPropertyChanged(nameof(IsActive));
    }

    /// <summary>
    /// Refreshes the IsDefaultAudioOutput property by checking the current Windows audio output.
    /// </summary>
    public async Task RefreshDefaultAudioOutputStatusAsync()
    {
        // Use PairedBluetoothAddress (Bluetooth Classic address) for audio checks,
        // not Address (rotating BLE advertisement address)
        if (!PairedBluetoothAddress.HasValue || PairedBluetoothAddress.Value == 0)
        {
            IsDefaultAudioOutput = false;
            return;
        }

        if (_audioOutputService == null)
        {
            IsDefaultAudioOutput = false;
            return;
        }

        try
        {
            IsDefaultAudioOutput = await _audioOutputService.IsDefaultAudioOutputAsync(PairedBluetoothAddress.Value);
        }
        catch
        {
            IsDefaultAudioOutput = false;
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (IsBusy || IsConnected || string.IsNullOrEmpty(PairedDeviceId))
            return;

        try
        {
            IsBusy = true;
            _stateManager?.BeginConnectionOperation(ProductId);
            LogDebug($"Connecting to {Model}...");
            
            var result = await _connectionService.ConnectByDeviceIdAsync(PairedDeviceId);
            
            switch (result)
            {
                case ConnectionResult.Connected:
                    IsConnected = true;
                    await Task.Delay(1500);
                    await RefreshDefaultAudioOutputStatusAsync();
                    _stateManager?.EndConnectionOperation(ProductId, success: true, IsConnected, IsDefaultAudioOutput);
                    LogDebug($"Connected to {Model}");
                    break;
                    
                case ConnectionResult.DeviceNotFound:
                    LogDebug($"{Model} not found (unpaired?)");
                    PairedDeviceId = null;
                    _stateManager?.EndConnectionOperation(ProductId, success: false, isConnected: false, isAudioConnected: false);
                    OnPropertyChanged(nameof(Status));
                    break;
                    
                case ConnectionResult.Failed:
                    LogDebug($"Connect failed for {Model}, opening settings");
                    _stateManager?.EndConnectionOperation(ProductId, success: false, IsConnected, IsDefaultAudioOutput);
                    if (_systemLauncherService != null)
                    {
                        await _systemLauncherService.OpenBluetoothSettingsAsync();
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            LogDebug($"Connect error: {ex.Message}");
            _stateManager?.EndConnectionOperation(ProductId, success: false, IsConnected, IsDefaultAudioOutput);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenBluetoothSettingsAsync()
    {
        if (_systemLauncherService == null)
        {
            LogDebug("System launcher service not available");
            return;
        }

        try
        {
            await _systemLauncherService.OpenBluetoothSettingsAsync();
        }
        catch (Exception ex)
        {
            LogDebug($"Settings launch error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ConnectAudioAsync()
    {
        if (IsBusy || string.IsNullOrEmpty(PairedDeviceId))
            return;

        try
        {
            IsBusy = true;
            _stateManager?.BeginConnectionOperation(ProductId);
            LogDebug($"Activating audio for {Model}...");
            
            var result = await _connectionService.ConnectByDeviceIdAsync(PairedDeviceId);
            
            switch (result)
            {
                case ConnectionResult.Connected:
                    IsConnected = true;
                    await Task.Delay(1500);
                    await RefreshDefaultAudioOutputStatusAsync();
                    _stateManager?.EndConnectionOperation(ProductId, success: true, IsConnected, IsDefaultAudioOutput);
                    LogDebug($"Audio activated for {Model}");
                    break;
                    
                case ConnectionResult.DeviceNotFound:
                    LogDebug($"{Model} not found (unpaired?)");
                    PairedDeviceId = null;
                    _stateManager?.EndConnectionOperation(ProductId, success: false, isConnected: false, isAudioConnected: false);
                    OnPropertyChanged(nameof(Status));
                    break;
                    
                case ConnectionResult.Failed:
                    LogDebug($"Audio activation failed for {Model}");
                    _stateManager?.EndConnectionOperation(ProductId, success: false, IsConnected, IsDefaultAudioOutput);
                    break;
            }
        }
        catch (Exception ex)
        {
            LogDebug($"Audio error: {ex.Message}");
            _stateManager?.EndConnectionOperation(ProductId, success: false, IsConnected, IsDefaultAudioOutput);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        if (!IsConnected || IsBusy || string.IsNullOrEmpty(PairedDeviceId))
            return;

        try
        {
            IsBusy = true;
            _stateManager?.BeginConnectionOperation(ProductId);
            LogDebug($"Disconnecting from {Model}...");
            
            var success = await _connectionService.DisconnectByDeviceIdAsync(PairedDeviceId);
            
            if (success)
            {
                IsConnected = false;
                IsDefaultAudioOutput = false;
                _stateManager?.EndConnectionOperation(ProductId, success: true, isConnected: false, isAudioConnected: false);
                await Task.Delay(1000);
                LogDebug($"Disconnected from {Model}");
            }
            else
            {
                LogDebug($"Disconnect failed for {Model}");
                _stateManager?.EndConnectionOperation(ProductId, success: false, IsConnected, IsDefaultAudioOutput);
            }
        }
        catch (Exception ex)
        {
            LogDebug($"Disconnect error: {ex.Message}");
            _stateManager?.EndConnectionOperation(ProductId, success: false, IsConnected, IsDefaultAudioOutput);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
