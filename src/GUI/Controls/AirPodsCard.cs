using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using GUI.ViewModels;

namespace GUI.Controls;

/// <summary>
/// Card component for displaying detailed AirPods information.
/// </summary>
public sealed partial class AirPodsCard : UserControl
{
    public static readonly DependencyProperty DeviceProperty =
        DependencyProperty.Register(
            nameof(Device),
            typeof(AirPodsDeviceViewModel),
            typeof(AirPodsCard),
            new PropertyMetadata(null));

    public AirPodsDeviceViewModel? Device
    {
        get => (AirPodsDeviceViewModel?)GetValue(DeviceProperty);
        set => SetValue(DeviceProperty, value);
    }

    public AirPodsCard()
    {
        InitializeComponent();
    }
}
