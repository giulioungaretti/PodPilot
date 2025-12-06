using Microsoft.UI.Xaml.Controls;
using GUI.ViewModels;
using GUI.Services;
using DeviceCommunication.Services;

namespace GUI;

/// <summary>
/// Main page containing the AirPods Monitor UI.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; }

    public MainPage()
    {
        InitializeComponent();

        // Initialize services
        var settingsService = new SettingsService();
        var discoveryService = new GroupedAirPodsDiscoveryService();

        // Initialize ViewModel with DispatcherQueue for thread-safe operations
        ViewModel = new MainPageViewModel(discoveryService, settingsService, DispatcherQueue);

        // Initialize when page loads
        Loaded += (_, _) => ViewModel.Initialize();
        Unloaded += (_, _) => ViewModel.Dispose();
    }
}
