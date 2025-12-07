using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceCommunication.Models;
using DeviceCommunication.Services;
using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;

namespace GUI.ViewModels;

/// <summary>
/// ViewModel for the main page displaying AirPods devices.
/// </summary>
public partial class MainPageViewModel : ObservableObject, IDisposable
{
    private readonly IAirPodsDiscoveryService _discoveryService;
    private readonly IBluetoothConnectionService _connectionService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _cleanupTimer;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _hasDiscoveredDevices;

    public ObservableCollection<AirPodsDeviceViewModel> DiscoveredDevices { get; }

    public MainPageViewModel(
        IAirPodsDiscoveryService discoveryService,
        IBluetoothConnectionService connectionService,
        DispatcherQueue dispatcherQueue)
    {
        _discoveryService = discoveryService;
        _connectionService = connectionService;
        _dispatcherQueue = dispatcherQueue;
        DiscoveredDevices = new ObservableCollection<AirPodsDeviceViewModel>();

        _discoveryService.DeviceDiscovered += OnDeviceDiscovered;
        _discoveryService.DeviceUpdated += OnDeviceUpdated;

        _cleanupTimer = _dispatcherQueue.CreateTimer();
        _cleanupTimer.Interval = TimeSpan.FromSeconds(1);
        _cleanupTimer.Tick += OnCleanupTimerTick;
    }

    public void Initialize()
    {
        StartScanning();
        _cleanupTimer.Start();
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

    private void OnDeviceDiscovered(object? sender, AirPodsDeviceInfo device)
    {
        // Marshal to UI thread to ensure thread-safe collection modification
        _dispatcherQueue.TryEnqueue(() =>
        {
            var deviceViewModel = new AirPodsDeviceViewModel(device, _connectionService);
            DiscoveredDevices.Add(deviceViewModel);
            HasDiscoveredDevices = DiscoveredDevices.Count > 0;
        });
    }

    private void OnDeviceUpdated(object? sender, AirPodsDeviceInfo device)
    {
        // Marshal to UI thread to ensure thread-safe collection modification
        _dispatcherQueue.TryEnqueue(() =>
        {
            // Update in discovered list - find by Product ID
            var existing = DiscoveredDevices.FirstOrDefault(d => d.ProductId == device.ProductId);
            if (existing != null)
            {
                existing.UpdateFrom(device);
            }
        });
    }

    private void OnCleanupTimerTick(DispatcherQueueTimer sender, object args)
    {
        var now = DateTime.Now;
        var devicesToRemove = new System.Collections.Generic.List<AirPodsDeviceViewModel>();

        foreach (var device in DiscoveredDevices)
        {
            var timeSinceLastSeen = (now - device.LastSeen).TotalSeconds;
            
            if (timeSinceLastSeen >= 10)
            {
                devicesToRemove.Add(device);
            }
            else
            {
                device.RefreshIsActive();
                // Refresh audio output status periodically (fire and forget since we're in a timer)
                _ = device.RefreshDefaultAudioOutputStatusAsync();
            }
        }

        foreach (var device in devicesToRemove)
        {
            DiscoveredDevices.Remove(device);
        }
        
        HasDiscoveredDevices = DiscoveredDevices.Count > 0;
    }

    public void Dispose()
    {
        _cleanupTimer.Stop();
        _discoveryService.DeviceDiscovered -= OnDeviceDiscovered;
        _discoveryService.DeviceUpdated -= OnDeviceUpdated;
        _discoveryService.Dispose();
    }
}
