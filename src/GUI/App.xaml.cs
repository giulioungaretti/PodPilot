using System;
using DeviceCommunication.Services;
using GUI.Services;
using GUI.ViewModels;
using GUI.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace GUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private MainWindow? _mainWindow;
    private TrayIconService? _trayIconService;
    private BackgroundDeviceMonitoringService? _backgroundMonitoringService;

    /// <summary>
    /// Gets the current <see cref="App"/> instance in use.
    /// </summary>
    public new static App Current => (App)Application.Current;

    /// <summary>
    /// Gets the <see cref="IServiceProvider"/> instance to resolve application services.
    /// </summary>
    public IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Configures the services for the application.
    /// </summary>
    /// <param name="dispatcherQueue">The UI thread's dispatcher queue.</param>
    private static IServiceProvider ConfigureServices(DispatcherQueue dispatcherQueue)
    {
        var services = new ServiceCollection();

        // Register DispatcherQueue (must be captured on UI thread)
        services.AddSingleton(dispatcherQueue);

        // Register services as singletons (shared across the application)
        services.AddSingleton<IPairedDeviceLookupService, PairedDeviceLookupService>();
        services.AddSingleton<IAirPodsDiscoveryService, SimpleAirPodsDiscoveryService>();
        services.AddSingleton<BluetoothConnectionService>();
        services.AddSingleton<GlobalMediaController>();
        services.AddSingleton<EarDetectionService>();

        // Register ViewModels as transient (new instance each time)
        services.AddTransient<MainPageViewModel>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Configure services (must be done on UI thread to capture DispatcherQueue)
        Services = ConfigureServices(DispatcherQueue.GetForCurrentThread());

        _mainWindow = new MainWindow();

        // Get services from DI container
        var discoveryService = Services.GetRequiredService<IAirPodsDiscoveryService>();

        // Initialize tray icon service (simplified - just manages window visibility)
        _trayIconService = new TrayIconService(_mainWindow);

        // Initialize background monitoring service with shared discovery service
        _backgroundMonitoringService = new BackgroundDeviceMonitoringService(
            DispatcherQueue.GetForCurrentThread(),
            discoveryService);
        _backgroundMonitoringService.PairedDeviceDetected += OnPairedDeviceDetected;
        _backgroundMonitoringService.Start();

        // Initialize ear detection service for auto-pause/resume
        var earDetectionService = Services.GetRequiredService<EarDetectionService>();
        await earDetectionService.InitializeAsync();

        _mainWindow.Activate();
    }

    private void OnMainWindowMinimizeRequested(object? sender, EventArgs e)
    {
        // When main window is minimized, hide it
        _trayIconService?.Hide();
    }

    private void OnPairedDeviceDetected(object? sender, DeviceCommunication.Models.AirPodsDeviceInfo deviceInfo)
    {
        var connectionService = Services.GetRequiredService<BluetoothConnectionService>();

        // Show notification window
        var notificationWindow = new NotificationWindow(deviceInfo, connectionService);
        notificationWindow.OpenMainWindowRequested += (s, e) =>
        {
            _trayIconService?.Show();
        };
        notificationWindow.Activate();
    }
}
