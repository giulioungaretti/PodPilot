using Microsoft.UI.Xaml.Controls;
using GUI.ViewModels;
using DeviceCommunication.Advertisement;
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

        // Initialize services with new layered architecture
        var advertisementStream = new AdvertisementStream();
        var aggregator = new AirPodsDeviceAggregator(advertisementStream);

        // Initialize ViewModel
        ViewModel = new MainPageViewModel(aggregator);

        // Initialize when page loads
        Loaded += (_, _) => ViewModel.Initialize();
        Unloaded += (_, _) => ViewModel.Dispose();
    }
}

