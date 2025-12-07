using System;
using DeviceCommunication.Models;
using DeviceCommunication.Services;
using GUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using WinUIEx;

namespace GUI.Windows;

/// <summary>
/// Small notification window that displays when a paired AirPods device is detected.
/// </summary>
public sealed partial class NotificationWindow : Window
{
    private readonly AirPodsDeviceViewModel _deviceViewModel;

    public event EventHandler? OpenMainWindowRequested;

    public NotificationWindow(AirPodsDeviceInfo deviceInfo, BluetoothConnectionService connectionService)
    {
        InitializeComponent();

        // Configure window appearance using WinUIEx extensions
        this.SetWindowSize(450, 250);
        this.CenterOnScreen();
        this.SetIsAlwaysOnTop(true);
        
        // Remove title bar and borders completely using OverlappedPresenter
        var presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsMinimizable = false;
        presenter.IsMaximizable = false;
        presenter.IsResizable = false;
        
        // Hide from task switchers (Alt+Tab)
        AppWindow.IsShownInSwitchers = false;

        // Create device view model and bind to card
        _deviceViewModel = new AirPodsDeviceViewModel(deviceInfo, connectionService);
        DeviceCard.Device = _deviceViewModel;

        // Auto-close after 15 seconds
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(15);
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            Close();
        };
        timer.Start();
    }
}
