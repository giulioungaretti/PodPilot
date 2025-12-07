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
public sealed partial class NotificationWindow : WinUIEx.WindowEx
{
    private readonly AirPodsDeviceViewModel _deviceViewModel;

    public event EventHandler? OpenMainWindowRequested;

    public NotificationWindow(AirPodsDeviceInfo deviceInfo, BluetoothConnectionService connectionService)
    {
        InitializeComponent();

        // Configure window size using WindowEx properties
        Width = 450;
        Height = 300;
        
        // Configure window behavior using WindowEx properties
        IsAlwaysOnTop = true;
        IsResizable = false;
        IsMinimizable = false;
        IsMaximizable = false;
        IsTitleBarVisible = false;
        IsShownInSwitchers = false;
        
        // Position window after it's activated
        Activated += OnFirstActivated;
        
        // Dismiss window when user clicks elsewhere
        Activated += OnWindowActivated;

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

    private void OnFirstActivated(object sender, WindowActivatedEventArgs e)
    {
        // Unsubscribe - only need to position once
        Activated -= OnFirstActivated;
        
        // Position window at center-bottom of screen, just above taskbar
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea; // WorkArea excludes taskbar area
        
        // Get actual window size in physical pixels (accounts for DPI scaling)
        var windowWidth = AppWindow.Size.Width;
        var windowHeight = AppWindow.Size.Height;
        
        var x = workArea.X + (workArea.Width - windowWidth) / 2; // Center horizontally
        var y = workArea.Y + workArea.Height - windowHeight - 20; // 20px margin above taskbar
        
        this.Move(x, y);
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
    {
        // Close window when it loses focus (user clicks elsewhere)
        if (e.WindowActivationState == WindowActivationState.Deactivated)
        {
            Close();
        }
    }
}

