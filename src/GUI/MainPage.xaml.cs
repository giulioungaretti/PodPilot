using System;
using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using GUI.ViewModels;
using GUI.Services;
using DeviceCommunication.Services;
using DeviceCommunication.Models;
using GUI.Controls;

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

        // Initialize services
        var settingsService = new SettingsService();
        var discoveryService = new AirPodsDiscoveryService();

        // Initialize ViewModel with DispatcherQueue for thread-safe operations
        ViewModel = new MainPageViewModel(discoveryService, settingsService, DispatcherQueue);

        // Subscribe to ViewModel changes
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.DiscoveredDevices.CollectionChanged += DiscoveredDevices_CollectionChanged;

        // Defer service initialization until after page is loaded
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // No need for DispatcherQueue here - ViewModel already marshals to UI thread
        switch (e.PropertyName)
        {
            case nameof(ViewModel.SavedDevice):
                UpdateSavedDeviceUI();
                break;
            case nameof(ViewModel.HasSavedDevice):
                UpdateSavedDeviceUI();
                break;
            case nameof(ViewModel.IsScanning):
                ScanningIndicator.Visibility = ViewModel.IsScanning ? Visibility.Visible : Visibility.Collapsed;
                break;
            case nameof(ViewModel.HasDiscoveredDevices):
                UpdateDiscoveredDevicesUI();
                break;
        }
    }

    private void DiscoveredDevices_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // No need for DispatcherQueue here - ViewModel already marshals to UI thread
        UpdateDiscoveredDevicesUI();
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Initialize();
        UpdateSavedDeviceUI();
        UpdateDiscoveredDevicesUI();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.DiscoveredDevices.CollectionChanged -= DiscoveredDevices_CollectionChanged;
        ViewModel.Dispose();
    }

    private void UpdateSavedDeviceUI()
    {
        SavedDeviceContainer.Child = null;

        if (ViewModel.HasSavedDevice && ViewModel.SavedDevice != null)
        {
            var card = new AirPodsCard
            {
                Device = ViewModel.SavedDevice,
                ShowActions = true
            };
            card.ForgetRequested += OnForgetDeviceRequested;
            SavedDeviceContainer.Child = card;
        }
        else
        {
            var placeholder = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(40, 32, 40, 32),
                BorderThickness = new Thickness(1)
            };

            var placeholderStack = new StackPanel
            {
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            placeholderStack.Children.Add(new TextBlock
            {
                Text = "??",
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.5
            });

            placeholderStack.Children.Add(new TextBlock
            {
                Text = "No saved device",
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            placeholderStack.Children.Add(new TextBlock
            {
                Text = "Save a device from the discovered list below",
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            placeholder.Child = placeholderStack;
            SavedDeviceContainer.Child = placeholder;
        }
    }

    private void UpdateDiscoveredDevicesUI()
    {
        DiscoveredDevicesContainer.Children.Clear();

        if (ViewModel.HasDiscoveredDevices)
        {
            foreach (var device in ViewModel.DiscoveredDevices)
            {
                var card = new AirPodsCard
                {
                    Device = device,
                    ShowActions = true,
                    Margin = new Thickness(0, 0, 0, 16)
                };
                card.SaveRequested += OnSaveDeviceRequested;
                DiscoveredDevicesContainer.Children.Add(card);
            }
        }
        else
        {
            var emptyState = new EmptyState
            {
                Icon = "??",
                Title = "No devices found",
                Message = "Open your AirPods case to discover devices nearby"
            };
            DiscoveredDevicesContainer.Children.Add(emptyState);
        }
    }

    private void OnSaveDeviceRequested(object? sender, AirPodsDeviceInfo device)
    {
        ViewModel.SaveDeviceCommand.Execute(device);
    }

    private void OnForgetDeviceRequested(object? sender, AirPodsDeviceInfo device)
    {
        ViewModel.ForgetDeviceCommand.Execute(null);
    }
}
