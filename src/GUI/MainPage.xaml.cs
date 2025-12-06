using Microsoft.UI.Xaml.Controls;
using GUI.ViewModels;
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

        // Initialize service
        var discoveryService = new GroupedAirPodsDiscoveryService();

        // Initialize ViewModel
        ViewModel = new MainPageViewModel(discoveryService);

        // Initialize when page loads
        Loaded += (_, _) => ViewModel.Initialize();
        Unloaded += (_, _) => ViewModel.Dispose();
    }
}
