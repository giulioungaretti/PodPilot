using System;
using DeviceCommunication.Services;
using DeviceCommunication.Advertisement;
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

        // Infrastructure
        services.AddSingleton(dispatcherQueue);

        // Low-level services (no dependencies on other services)
        services.AddSingleton<IAdvertisementWatcher, AdvertisementWatcher>();
        services.AddSingleton<IPairedDeviceLookupService, PairedDeviceLookupService>();
        services.AddSingleton<IGlobalMediaController, GlobalMediaController>();
        services.AddSingleton<IDefaultAudioOutputMonitorService, DefaultAudioOutputMonitorService>();

        // Mid-level services
        services.AddSingleton<IAirPodsDiscoveryService, SimpleAirPodsDiscoveryService>();
        services.AddSingleton<IBluetoothConnectionService, BluetoothConnectionService>();

        // High-level services (depend on discovery service)
        services.AddSingleton<EarDetectionService>();
        services.AddSingleton<IBackgroundDeviceMonitoringService, BackgroundDeviceMonitoringService>();

        // ViewModels
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

        // Initialize tray icon service (requires MainWindow, so created manually)
        _trayIconService = new TrayIconService(_mainWindow);

        // Get and start background monitoring service from DI
        var backgroundMonitoringService = Services.GetRequiredService<IBackgroundDeviceMonitoringService>();
        backgroundMonitoringService.PairedDeviceDetected += OnPairedDeviceDetected;
        backgroundMonitoringService.Start();

        // Initialize ear detection service for auto-pause/resume
        var earDetectionService = Services.GetRequiredService<EarDetectionService>();
        await earDetectionService.InitializeAsync();

        // Start default audio output monitoring
        var audioOutputMonitor = Services.GetRequiredService<IDefaultAudioOutputMonitorService>();
        audioOutputMonitor.Start();

        _mainWindow.Activate();
    }

    private void OnMainWindowMinimizeRequested(object? sender, EventArgs e)
    {
        // When main window is minimized, hide it
        _trayIconService?.Hide();
    }

    private void OnPairedDeviceDetected(object? sender, DeviceCommunication.Models.AirPodsDeviceInfo deviceInfo)
    {
        var connectionService = Services.GetRequiredService<IBluetoothConnectionService>();

        // Show notification window
        var notificationWindow = new NotificationWindow(deviceInfo, connectionService);
        notificationWindow.OpenMainWindowRequested += (s, e) =>
        {
            _trayIconService?.Show();
        };
        notificationWindow.Activate();
    }
}
