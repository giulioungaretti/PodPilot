using System.Windows.Input;
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

    public static readonly DependencyProperty ShowActionsProperty =
        DependencyProperty.Register(
            nameof(ShowActions),
            typeof(bool),
            typeof(AirPodsCard),
            new PropertyMetadata(true));

    public static readonly DependencyProperty SaveCommandProperty =
        DependencyProperty.Register(
            nameof(SaveCommand),
            typeof(ICommand),
            typeof(AirPodsCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ForgetCommandProperty =
        DependencyProperty.Register(
            nameof(ForgetCommand),
            typeof(ICommand),
            typeof(AirPodsCard),
            new PropertyMetadata(null));

    public AirPodsDeviceViewModel? Device
    {
        get => (AirPodsDeviceViewModel?)GetValue(DeviceProperty);
        set => SetValue(DeviceProperty, value);
    }

    public bool ShowActions
    {
        get => (bool)GetValue(ShowActionsProperty);
        set => SetValue(ShowActionsProperty, value);
    }

    public ICommand? SaveCommand
    {
        get => (ICommand?)GetValue(SaveCommandProperty);
        set => SetValue(SaveCommandProperty, value);
    }

    public ICommand? ForgetCommand
    {
        get => (ICommand?)GetValue(ForgetCommandProperty);
        set => SetValue(ForgetCommandProperty, value);
    }

    public AirPodsCard()
    {
        InitializeComponent();
    }
}
