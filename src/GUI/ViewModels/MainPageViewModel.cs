using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceCommunication.Services;

namespace GUI.ViewModels;

/// <summary>
/// ViewModel for the main page displaying AirPods devices.
/// Uses reactive pattern with MVVM Toolkit for clean, simple state management.
/// Implements smart device timeout: inactive after 60s, removed after 5min.
/// </summary>
public partial class MainPageViewModel : ObservableObject, IDisposable
{
    private readonly AirPodsDeviceAggregator _aggregator;
    private readonly IDisposable _deviceChangesSubscription;
    private readonly IDisposable _isActiveRefreshSubscription;

    [ObservableProperty]
    private bool _isScanning;

    /// <summary>
    /// Gets whether any devices have been discovered.
    /// Computed automatically from DiscoveredDevices.Count.
    /// </summary>
    public bool HasDiscoveredDevices => DiscoveredDevices.Count > 0;

    public ObservableCollection<AirPodsDeviceViewModel> DiscoveredDevices { get; }

    public MainPageViewModel(AirPodsDeviceAggregator aggregator)
    {
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
        DiscoveredDevices = new ObservableCollection<AirPodsDeviceViewModel>();

        // Notify when collection changes
        DiscoveredDevices.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasDiscoveredDevices));

        // Subscribe to device state changes on UI thread automatically
        var syncContext = SynchronizationContext.Current;
        _deviceChangesSubscription = _aggregator.DeviceChanges
            .ObserveOn(syncContext != null ? new SynchronizationContextScheduler(syncContext) : Scheduler.CurrentThread)
            .Subscribe(
                onNext: ProcessDeviceStateChange,
                onError: ex => Console.Error.WriteLine($"[MainPageViewModel] Error in device stream: {ex}")
            );

        // Timer to refresh IsActive property for all devices
        _isActiveRefreshSubscription = Observable
            .Interval(TimeSpan.FromSeconds(1))
            .ObserveOn(syncContext != null ? new SynchronizationContextScheduler(syncContext) : Scheduler.CurrentThread)
            .Subscribe(_ => RefreshIsActiveForAllDevices());
    }

    public void Initialize()
    {
        StartScanning();
    }

    [RelayCommand]
    private void StartScanning()
    {
        IsScanning = true;
        _aggregator.Start();
    }

    [RelayCommand]
    private void StopScanning()
    {
        IsScanning = false;
        _aggregator.Stop();
    }

    private void ProcessDeviceStateChange(DeviceStateChange change)
    {
        switch (change.ChangeType)
        {
            case DeviceChangeType.Added:
                AddDevice(change);
                break;

            case DeviceChangeType.Updated:
                UpdateDevice(change);
                break;

            case DeviceChangeType.Removed:
                RemoveDevice(change);
                break;
        }
    }

    private void AddDevice(DeviceStateChange change)
    {
        var deviceViewModel = new AirPodsDeviceViewModel(change.DeviceId, change.Device);
        
        // Subscribe to property changes to re-sort when connection state changes
        deviceViewModel.PropertyChanged += OnDevicePropertyChanged;

        // Insert in sorted order: connected devices first, then by signal strength
        var index = DiscoveredDevices
            .TakeWhile(d => ShouldComeBefore(d, deviceViewModel))
            .Count();

        DiscoveredDevices.Insert(index, deviceViewModel);
    }

    private bool ShouldComeBefore(AirPodsDeviceViewModel a, AirPodsDeviceViewModel b)
    {
        // Connected devices always come before disconnected ones
        if (a.IsConnected != b.IsConnected)
            return a.IsConnected;
            
        // If both connected or both disconnected, sort by signal strength
        return a.SignalStrength > b.SignalStrength;
    }

    private void UpdateDevice(DeviceStateChange change)
    {
        // Find existing device by DeviceId (not Address - address can change!)
        var existing = DiscoveredDevices.FirstOrDefault(d => d.DeviceId == change.DeviceId);
        
        if (existing == null)
        {
            AddDevice(change);
            return;
        }

        var oldIndex = DiscoveredDevices.IndexOf(existing);
        var oldIsConnected = existing.IsConnected;
        var oldSignalStrength = existing.SignalStrength;
        
        // Update properties (MVVM Toolkit handles change notifications)
        existing.UpdateFrom(change.Device);

        // Re-sort if connection state changed or signal strength changed significantly
        var connectionStateChanged = existing.IsConnected != oldIsConnected;
        const int SignalStrengthThreshold = 5;
        var signalStrengthChanged = Math.Abs(existing.SignalStrength - oldSignalStrength) >= SignalStrengthThreshold;
        
        if (connectionStateChanged || signalStrengthChanged)
        {
            // Find new position
            var newIndex = 0;
            for (int i = 0; i < DiscoveredDevices.Count; i++)
            {
                if (i == oldIndex) continue;
                if (ShouldComeBefore(DiscoveredDevices[i], existing))
                    newIndex++;
                else
                    break;
            }
            
            // Adjust for removal from old position
            if (newIndex > oldIndex)
                newIndex--;
                
            if (newIndex != oldIndex)
            {
                DiscoveredDevices.Move(oldIndex, newIndex);
            }
        }
    }

    private void RemoveDevice(DeviceStateChange change)
    {
        var existing = DiscoveredDevices.FirstOrDefault(d => d.DeviceId == change.DeviceId);
        
        if (existing != null)
        {
            existing.PropertyChanged -= OnDevicePropertyChanged;
            existing.Cleanup();
            DiscoveredDevices.Remove(existing);
        }
    }

    private void OnDevicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Re-sort when connection state changes
        if (e.PropertyName == nameof(AirPodsDeviceViewModel.IsConnected) && sender is AirPodsDeviceViewModel device)
        {
            var oldIndex = DiscoveredDevices.IndexOf(device);
            if (oldIndex == -1) return;
            
            // Find new position based on connection state and signal strength
            var newIndex = 0;
            for (int i = 0; i < DiscoveredDevices.Count; i++)
            {
                if (i == oldIndex) continue;
                if (ShouldComeBefore(DiscoveredDevices[i], device))
                    newIndex++;
                else
                    break;
            }
            
            // Adjust for removal from old position
            if (newIndex > oldIndex)
                newIndex--;
                
            if (newIndex != oldIndex)
            {
                DiscoveredDevices.Move(oldIndex, newIndex);
            }
        }
    }

    /// <summary>
    /// Refreshes the IsActive property for all devices to update the visual inactive state.
    /// Device removal is handled by the AirPodsDeviceAggregator.
    /// </summary>
    private void RefreshIsActiveForAllDevices()
    {
        foreach (var device in DiscoveredDevices)
        {
            device.RefreshIsActive();
        }
    }

    public void Dispose()
    {
        // Cleanup all devices
        foreach (var device in DiscoveredDevices)
        {
            device.PropertyChanged -= OnDevicePropertyChanged;
            device.Cleanup();
        }
        
        _isActiveRefreshSubscription?.Dispose();
        _deviceChangesSubscription?.Dispose();
        _aggregator?.Dispose();
    }
}

