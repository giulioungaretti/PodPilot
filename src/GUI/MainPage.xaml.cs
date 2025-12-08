using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using GUI.ViewModels;
using Microsoft.UI.Dispatching;
using System;

namespace GUI;

/// <summary>
/// Main page containing the AirPods Monitor UI.
/// </summary>
public sealed partial class MainPage : Page
{

    private readonly DispatcherQueueTimer _cleanupTimer;
    public MainPageViewModel ViewModel { get; }

    public MainPage()
    {
        InitializeComponent();

        // Resolve ViewModel with all dependencies from DI container
        ViewModel = App.Current.Services.GetRequiredService<MainPageViewModel>();

        // Initialize when page loads 
        Loaded += async (_, _) =>
        {
            await ViewModel.InitializeAsync();
            _cleanupTimer?.Start();
        };
        Unloaded += (_, _) =>
        {
            ViewModel.Dispose();
            _cleanupTimer?.Stop();
        };

        _cleanupTimer = DispatcherQueue.CreateTimer();
        _cleanupTimer.Interval = TimeSpan.FromSeconds(5);
        _cleanupTimer.Tick += OnCleanupTimerTick;
    }

    private void OnCleanupTimerTick(DispatcherQueueTimer sender, object args)
    {
        ViewModel.OnCleanupTimerTick(sender, args);
    }
}

