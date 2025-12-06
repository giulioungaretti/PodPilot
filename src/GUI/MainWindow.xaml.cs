using System;
using Microsoft.UI.Xaml;

namespace GUI;

/// <summary>
/// Main application window that hosts the MainPage.
/// </summary>
public sealed partial class MainWindow : Window
{
    public event EventHandler? MinimizeRequested;

    public MainWindow()
    {
        InitializeComponent();

        // Navigate to MainPage
        RootFrame.Navigate(typeof(MainPage));

        // Set up window event handlers
        Closed += OnWindowClosed;

        // Set title bar
        Title = "PodPilot";
        ExtendsContentIntoTitleBar = true;
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        // Cleanup handled in MainPage
    }
}
