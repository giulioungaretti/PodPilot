using PodPilot.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GUI.Controls;

/// <summary>
/// Minimal AirPods card for notification popups showing battery status only.
/// </summary>
public sealed partial class MinimalAirPodsCard : UserControl
{
    public static readonly DependencyProperty DeviceProperty =
        DependencyProperty.Register(
            nameof(Device),
            typeof(AirPodsDeviceViewModel),
            typeof(MinimalAirPodsCard),
            new PropertyMetadata(null));

    public AirPodsDeviceViewModel? Device
    {
        get => (AirPodsDeviceViewModel?)GetValue(DeviceProperty);
        set => SetValue(DeviceProperty, value);
    }

    public MinimalAirPodsCard()
    {
        InitializeComponent();
    }
}
