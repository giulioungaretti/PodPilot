using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceCommunication.Models;
using DeviceCommunication.Device;
using DeviceCommunication.Core;

namespace GUI.ViewModels;

/// <summary>
/// ViewModel wrapper for AirPodsDeviceInfo that provides property change notifications.
/// This allows the UI to update only when properties actually change.
/// </summary>
public partial class AirPodsDeviceViewModel : ObservableObject
{
    /// <summary>
    /// Unique identifier for this logical device (from aggregator).
    /// </summary>
    public Guid DeviceId { get; }

    public ulong Address { get; private set; }

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
    private bool _isConnected;

    [ObservableProperty]
    private int _signalStrength;

    [ObservableProperty]
    private DateTime _lastSeen;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _needsPairing;

    private Device? _device;

    /// <summary>
    /// Gets whether the device is currently active (seen within last 2 seconds).
    /// UI can bind to this to show visual inactive state.
    /// </summary>
    public bool IsActive => (DateTime.Now - LastSeen).TotalSeconds < 2;

    /// <summary>
    /// Gets whether the Connect button should be shown (not connected and not connecting).
    /// </summary>
    public bool ShowConnectButton => !IsConnected && !IsConnecting && !NeedsPairing;

    /// <summary>
    /// Gets whether the "Please pair" message should be shown.
    /// </summary>
    public bool ShowPairMessage => NeedsPairing && !IsConnected && !IsConnecting;

    public AirPodsDeviceViewModel(Guid deviceId, AirPodsDeviceInfo deviceInfo)
    {
        DeviceId = deviceId;
        Address = deviceInfo.Address;
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
        IsConnected = deviceInfo.IsConnected;
        SignalStrength = deviceInfo.SignalStrength;
        LastSeen = deviceInfo.LastSeen;
        
        // Notify IsActive changed (computed from LastSeen)
        OnPropertyChanged(nameof(IsActive));
    }

    /// <summary>
    /// Converts this ViewModel back to a device info object for saving.
    /// </summary>
    public AirPodsDeviceInfo ToDeviceInfo()
    {
        return new AirPodsDeviceInfo
        {
            Address = Address,
            Model = Model,
            DeviceName = DeviceName,
            LeftBattery = LeftBattery,
            RightBattery = RightBattery,
            CaseBattery = CaseBattery,
            IsLeftCharging = IsLeftCharging,
            IsRightCharging = IsRightCharging,
            IsCaseCharging = IsCaseCharging,
            IsLeftInEar = IsLeftInEar,
            IsRightInEar = IsRightInEar,
            IsLidOpen = IsLidOpen,
            IsConnected = IsConnected,
            SignalStrength = SignalStrength,
            LastSeen = LastSeen
        };
    }

    /// <summary>
    /// Refreshes the IsActive property to reflect current time.
    /// Call this periodically to update the active/inactive visual state.
    /// </summary>
    public void RefreshIsActive()
    {
        OnPropertyChanged(nameof(IsActive));
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        if (IsConnecting || IsConnected || Address == 0)
            return;

        try
        {
            IsConnecting = true;
            OnPropertyChanged(nameof(ShowConnectButton));
            OnPropertyChanged(nameof(ShowPairMessage));
            
            NeedsPairing = false;
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Attempting to connect to device {Address:X12}...");
            
            _device = await Device.FromBluetoothAddressAsync(Address);
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Device object created. Current connection state: {_device.GetConnectionState()}");
            
            _device.ConnectionStateChanged += OnDeviceConnectionStateChanged;
            
            // Check if already connected
            IsConnected = _device.IsConnected();
            
            if (IsConnected)
            {
                System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Device {Address:X12} is already connected!");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Device {Address:X12} is not connected (but paired).");
            }
            
            // Only stop connecting state after we've determined the connection status
            IsConnecting = false;
            OnPropertyChanged(nameof(ShowConnectButton));
            OnPropertyChanged(nameof(ShowPairMessage));
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Connect attempt completed. IsConnected={IsConnected}, NeedsPairing={NeedsPairing}");
        }
        catch (BluetoothException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] BluetoothException connecting to device {Address:X12}: {ex.Error} - {ex.Message}");
            
            // Device not found likely means it's not paired
            if (ex.Error == BluetoothError.DeviceNotFound)
            {
                NeedsPairing = true;
                System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Device needs to be paired first.");
            }
            
            IsConnecting = false;
            OnPropertyChanged(nameof(ShowConnectButton));
            OnPropertyChanged(nameof(ShowPairMessage));
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Connect attempt failed. IsConnected={IsConnected}, NeedsPairing={NeedsPairing}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Unexpected error connecting to device {Address:X12}: {ex.GetType().Name} - {ex.Message}");
            NeedsPairing = true; // Assume needs pairing on any error
            
            IsConnecting = false;
            OnPropertyChanged(nameof(ShowConnectButton));
            OnPropertyChanged(nameof(ShowPairMessage));
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Connect attempt failed with exception. IsConnected={IsConnected}, NeedsPairing={NeedsPairing}");
        }
    }

    private bool CanConnect() => !IsConnecting && !IsConnected && Address != 0;

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private void Disconnect()
    {
        if (_device == null)
            return;

        try
        {
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Disconnecting from device {Address:X12}...");
            _device.ConnectionStateChanged -= OnDeviceConnectionStateChanged;
            _device.Dispose();
            _device = null;
            
            IsConnected = false;
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Disconnected from device {Address:X12}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Error disconnecting from device {Address:X12}: {ex.Message}");
        }
    }

    private bool CanDisconnect() => IsConnected && _device != null;

    private void OnDeviceConnectionStateChanged(object? sender, DeviceConnectionState state)
    {
        System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Device {Address:X12} connection state changed to: {state}");
        // MVVM Toolkit property setters automatically marshal to UI thread when needed
        IsConnected = state == DeviceConnectionState.Connected;
        OnPropertyChanged(nameof(ShowConnectButton));
        OnPropertyChanged(nameof(ShowPairMessage));
    }

    [RelayCommand]
    private async Task OpenBluetoothSettingsAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Opening Windows Bluetooth settings...");
            // Open Windows Bluetooth settings to add a device
            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:bluetooth"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AirPodsDeviceViewModel] Error opening Bluetooth settings: {ex.Message}");
        }
    }

    public void Cleanup()
    {
        if (_device != null)
        {
            _device.ConnectionStateChanged -= OnDeviceConnectionStateChanged;
            _device.Dispose();
            _device = null;
        }
    }
}
