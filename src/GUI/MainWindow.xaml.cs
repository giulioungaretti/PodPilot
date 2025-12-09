using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using WinUIEx;

namespace GUI;

/// <summary>
/// Main application window that hosts the MainPage.
/// </summary>
public sealed partial class MainWindow : WindowEx
{
    private readonly WindowManager _windowManager;

    public MainWindow()
    {
        InitializeComponent();

        // Navigate to MainPage
        RootFrame.Navigate(typeof(MainPage));

        // Set up window properties
        Title = "PodPilot";
        ExtendsContentIntoTitleBar = true;
        
        // Set window size
        this.SetWindowSize(500, 900);
        this.CenterOnScreen();


        _windowManager = WindowManager.Get(this);
        _windowManager.IsVisibleInTray = true;

        // Setup context menu for tray icon
        _windowManager.TrayIconContextMenu += WindowManager_TrayIconContextMenu;

        // Minimize to tray behavior
        _windowManager.WindowStateChanged += (s, state) =>
        {
            _windowManager.AppWindow.IsShownInSwitchers = state != WindowState.Minimized;
        };


    }


    private void WindowManager_TrayIconContextMenu(object? sender, TrayIconEventArgs e)
    {
        var flyout = new MenuFlyout();

        var showItem = new MenuFlyoutItem { Text = "Show Window" };
        showItem.Click += (s, args) => ShowWindowFromTray();
        flyout.Items.Add(showItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        var exitItem = new MenuFlyoutItem { Text = "Exit" };
        exitItem.Click += (s, args) => ExitApplication();
        flyout.Items.Add(exitItem);

        e.Flyout = flyout;
    }

    private void ShowWindowFromTray()
    {
        _windowManager.WindowState = WindowState.Normal;
        this.Activate();
    }

    private void ExitApplication()
    {
        //ViewModel.Cleanup();
        Application.Current.Exit();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        //ViewModel.Cleanup();
    }

}
