using System;
using CommunityToolkit.Mvvm.ComponentModel;
using DeviceCommunication.Models;

namespace GUI.ViewModels;

/// <summary>
/// ViewModel wrapper for AirPodsDeviceInfo that provides property change notifications.
/// This allows the UI to update only when properties actually change.
/// </summary>
public partial class AirPodsDeviceViewModel : ObservableObject
{
    public ulong Address { get; }

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

    public AirPodsDeviceViewModel(AirPodsDeviceInfo deviceInfo)
    {
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
}
