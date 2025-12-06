using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceCommunication.Models;
using DeviceCommunication.Services;
using GUI.Services;
using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;

namespace GUI.ViewModels;

/// <summary>
/// ViewModel for the main page displaying AirPods devices.
/// </summary>
public partial class MainPageViewModel : ObservableObject, IDisposable
{
    private readonly IAirPodsDiscoveryService _discoveryService;
    private readonly SettingsService _settingsService;
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    private AirPodsDeviceInfo? _savedDevice;

    [ObservableProperty]
    private bool _hasSavedDevice;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _hasDiscoveredDevices;

    public ObservableCollection<AirPodsDeviceInfo> DiscoveredDevices { get; }

    public MainPageViewModel(IAirPodsDiscoveryService discoveryService, SettingsService settingsService, DispatcherQueue dispatcherQueue)
    {
        _discoveryService = discoveryService;
        _settingsService = settingsService;
        _dispatcherQueue = dispatcherQueue;
        DiscoveredDevices = new ObservableCollection<AirPodsDeviceInfo>();

        _discoveryService.DeviceDiscovered += OnDeviceDiscovered;
        _discoveryService.DeviceUpdated += OnDeviceUpdated;
    }

    public void Initialize()
    {
        LoadSavedDevice();
        StartScanning();
    }

    private void LoadSavedDevice()
    {
        var savedAddress = _settingsService.GetSavedDeviceAddress();
        HasSavedDevice = savedAddress.HasValue;
    }

    [RelayCommand]
    private void StartScanning()
    {
        IsScanning = true;
        _discoveryService.StartScanning();
    }

    [RelayCommand]
    private void StopScanning()
    {
        IsScanning = false;
        _discoveryService.StopScanning();
    }

    [RelayCommand]
    private void SaveDevice(AirPodsDeviceInfo device)
    {
        _settingsService.SaveDeviceAddress(device.Address);
        device.IsSaved = true;
        SavedDevice = device;
        HasSavedDevice = true;
    }

    [RelayCommand]
    private void ForgetDevice()
    {
        _settingsService.ClearSavedDevice();
        if (SavedDevice != null)
            SavedDevice.IsSaved = false;
        SavedDevice = null;
        HasSavedDevice = false;
    }

    private void OnDeviceDiscovered(object? sender, AirPodsDeviceInfo device)
    {
        // Marshal to UI thread to ensure thread-safe collection modification
        _dispatcherQueue.TryEnqueue(() =>
        {
            // Check if this is the saved device
            var savedAddress = _settingsService.GetSavedDeviceAddress();
            if (savedAddress.HasValue && device.Address == savedAddress.Value)
            {
                device.IsSaved = true;
                SavedDevice = device;
                HasSavedDevice = true;
            }

            DiscoveredDevices.Add(device);
            HasDiscoveredDevices = DiscoveredDevices.Count > 0;
        });
    }

    private void OnDeviceUpdated(object? sender, AirPodsDeviceInfo device)
    {
        // Marshal to UI thread to ensure thread-safe collection modification
        _dispatcherQueue.TryEnqueue(() =>
        {
            // Update saved device if it matches
            if (SavedDevice?.Address == device.Address)
            {
                device.IsSaved = true;
                SavedDevice = device;
            }

            // Update in discovered list
            var existing = DiscoveredDevices.FirstOrDefault(d => d.Address == device.Address);
            if (existing != null)
            {
                var index = DiscoveredDevices.IndexOf(existing);
                DiscoveredDevices[index] = device;
            }
        });
    }

    public void Dispose()
    {
        _discoveryService.DeviceDiscovered -= OnDeviceDiscovered;
        _discoveryService.DeviceUpdated -= OnDeviceUpdated;
        _discoveryService.Dispose();
    }
}
