using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DeviceCommunication.Models;

namespace GUI.Controls;

/// <summary>
/// Card component for displaying detailed AirPods information.
/// </summary>
public sealed partial class AirPodsCard : UserControl
{
    public static readonly DependencyProperty DeviceProperty =
        DependencyProperty.Register(
            nameof(Device),
            typeof(AirPodsDeviceInfo),
            typeof(AirPodsCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ShowActionsProperty =
        DependencyProperty.Register(
            nameof(ShowActions),
            typeof(bool),
            typeof(AirPodsCard),
            new PropertyMetadata(true));

    public AirPodsDeviceInfo? Device
    {
        get => (AirPodsDeviceInfo?)GetValue(DeviceProperty);
        set => SetValue(DeviceProperty, value);
    }

    public bool ShowActions
    {
        get => (bool)GetValue(ShowActionsProperty);
        set => SetValue(ShowActionsProperty, value);
    }

    public event EventHandler<AirPodsDeviceInfo>? SaveRequested;
    public event EventHandler<AirPodsDeviceInfo>? ForgetRequested;

    public AirPodsCard()
    {
        InitializeComponent();
    }

    private void OnSaveButtonClick(object sender, RoutedEventArgs e)
    {
        if (Device != null)
            SaveRequested?.Invoke(this, Device);
    }

    private void OnForgetButtonClick(object sender, RoutedEventArgs e)
    {
        if (Device != null)
            ForgetRequested?.Invoke(this, Device);
    }
}
