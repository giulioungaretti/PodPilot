using Microsoft.UI.Xaml.Controls;
namespace GUI;

/// <summary>
/// Main page containing the AirPods Monitor UI.
/// </summary>
public sealed partial class MainPage : Page
{

    public MainPage()
    {
        InitializeComponent();

        // Defer service initialization until after page is loaded
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
    }
}
