using System;
using DeviceCommunication.Services;
using GUI.Services;
using GUI.Windows;
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
    private BluetoothConnectionService? _connectionService;

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainWindow = new MainWindow();
        
        // Initialize Bluetooth connection service
        _connectionService = new BluetoothConnectionService();
        
        // Initialize tray icon service (simplified - just manages window visibility)
        _trayIconService = new TrayIconService(_mainWindow);
        
        // Initialize background monitoring service
        _backgroundMonitoringService = new BackgroundDeviceMonitoringService(
            DispatcherQueue.GetForCurrentThread());
        _backgroundMonitoringService.PairedDeviceDetected += OnPairedDeviceDetected;
        _backgroundMonitoringService.Start();
        
        _mainWindow.Activate();
    }

    private void OnMainWindowMinimizeRequested(object? sender, EventArgs e)
    {
        // When main window is minimized, hide it
        _trayIconService?.Hide();
    }

    private void OnPairedDeviceDetected(object? sender, DeviceCommunication.Models.AirPodsDeviceInfo deviceInfo)
    {
        if (_connectionService == null)
            return;

        // Show notification window
        var notificationWindow = new NotificationWindow(deviceInfo, _connectionService);
        notificationWindow.OpenMainWindowRequested += (s, e) =>
        {
            _trayIconService?.Show();
        };
        notificationWindow.Activate();
    }
}
