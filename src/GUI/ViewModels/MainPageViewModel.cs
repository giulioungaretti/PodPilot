using System;
using System.Linq;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceCommunication.Models;
using DeviceCommunication.Services;
using System.Collections.ObjectModel;

namespace GUI.ViewModels;

/// <summary>
/// ViewModel for the main page displaying AirPods devices.
/// </summary>
public partial class MainPageViewModel : ObservableObject, IDisposable
{
    private readonly IAirPodsDiscoveryService _discoveryService;
    private readonly SynchronizationContext? _syncContext;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _hasDiscoveredDevices;

    public ObservableCollection<AirPodsDeviceViewModel> DiscoveredDevices { get; }

    public MainPageViewModel(IAirPodsDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
        _syncContext = SynchronizationContext.Current;
        DiscoveredDevices = new ObservableCollection<AirPodsDeviceViewModel>();

        DiscoveredDevices.CollectionChanged += (s, e) =>
        {
            HasDiscoveredDevices = DiscoveredDevices.Count > 0;
        };

        _discoveryService.DeviceDiscovered += OnDeviceDiscovered;
        _discoveryService.DeviceUpdated += OnDeviceUpdated;
        _discoveryService.DeviceRemoved += OnDeviceRemoved;
    }

    public void Initialize()
    {
        StartScanning();
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
        if (_syncContext == null)
        {
            UpdateDeviceDiscovered(device);
        }
        else
        {
            _syncContext.Post(_ => UpdateDeviceDiscovered(device), null);
        }
    }

    private void UpdateDeviceDiscovered(AirPodsDeviceInfo device)
    {
        var deviceViewModel = new AirPodsDeviceViewModel(device);
        
        // Insert in sorted order by signal strength (strongest first)
        int index = 0;
        while (index < DiscoveredDevices.Count && 
               DiscoveredDevices[index].SignalStrength > deviceViewModel.SignalStrength)
        {
            index++;
        }
        DiscoveredDevices.Insert(index, deviceViewModel);
    }

    private void OnDeviceUpdated(object? sender, AirPodsDeviceInfo device)
    {
        if (_syncContext == null)
        {
            UpdateDevice(device);
        }
        else
        {
            _syncContext.Post(_ => UpdateDevice(device), null);
        }
    }

    private void UpdateDevice(AirPodsDeviceInfo device)
    {
        var existing = DiscoveredDevices.FirstOrDefault(d => d.Address == device.Address);
        if (existing != null)
        {
            int oldIndex = DiscoveredDevices.IndexOf(existing);
            existing.UpdateFrom(device);
            
            // Re-sort if signal strength changed significantly
            int newIndex = 0;
            while (newIndex < DiscoveredDevices.Count && 
                   newIndex != oldIndex &&
                   DiscoveredDevices[newIndex].SignalStrength > existing.SignalStrength)
            {
                newIndex++;
            }
            
            if (newIndex != oldIndex && newIndex != oldIndex + 1)
            {
                DiscoveredDevices.Move(oldIndex, newIndex > oldIndex ? newIndex - 1 : newIndex);
            }
        }
    }

    private void OnDeviceRemoved(object? sender, AirPodsDeviceInfo device)
    {
        if (_syncContext == null)
        {
            RemoveDevice(device);
        }
        else
        {
            _syncContext.Post(_ => RemoveDevice(device), null);
        }
    }

    private void RemoveDevice(AirPodsDeviceInfo device)
    {
        var existing = DiscoveredDevices.FirstOrDefault(d => d.Address == device.Address);
        if (existing != null)
        {
            DiscoveredDevices.Remove(existing);
        }
    }

    public void Dispose()
    {
        _discoveryService.DeviceDiscovered -= OnDeviceDiscovered;
        _discoveryService.DeviceUpdated -= OnDeviceUpdated;
        _discoveryService.DeviceRemoved -= OnDeviceRemoved;
        _discoveryService.Dispose();
    }
}
