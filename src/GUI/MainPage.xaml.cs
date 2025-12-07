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

        // Initialize services with simplified Product ID-based discovery
        var discoveryService = new SimpleAirPodsDiscoveryService();
        var connectionService = new BluetoothConnectionService();

        // Initialize ViewModel with dependencies
        ViewModel = new MainPageViewModel(
            discoveryService, 
            connectionService,
            DispatcherQueue);

        // Initialize when page loads
        Loaded += (_, _) => ViewModel.Initialize();
        Unloaded += (_, _) => ViewModel.Dispose();
    }
}

