using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GUI.Controls;

/// <summary>
/// Empty state component for when no devices are found.
/// </summary>
public sealed partial class EmptyState : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(EmptyState),
            new PropertyMetadata("No devices found"));

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(
            nameof(Message),
            typeof(string),
            typeof(EmptyState),
            new PropertyMetadata("Open your AirPods case to discover"));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
            nameof(Icon),
            typeof(string),
            typeof(EmptyState),
            new PropertyMetadata("??"));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public EmptyState()
    {
        InitializeComponent();
    }
}
