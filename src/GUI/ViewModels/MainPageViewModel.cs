using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceCommunication.Models;
using DeviceCommunication.Services;
using System.Collections.ObjectModel;
using GUI.Services;
using Microsoft.UI.Dispatching;

namespace GUI.ViewModels;

/// <summary>
/// ViewModel for the main page displaying AirPods devices.
/// Uses DeviceStateManager as the single source of truth for device state.
/// </summary>
public partial class MainPageViewModel : ObservableObject, IDisposable
{
    private readonly IBluetoothConnectionService _connectionService;
    private readonly IDeviceStateManager _stateManager;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _cleanupTimer;
    private bool _isProcessingCleanup;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _hasDiscoveredDevices;

    [ObservableProperty]
    private bool _hasPairedDevices;

    /// <summary>
    /// Collection of paired devices from Windows Bluetooth settings.
    /// </summary>
    public ObservableCollection<AirPodsDeviceViewModel> PairedDevices { get; }

    /// <summary>
    /// Collection of nearby discovered devices (from BLE advertisements).
    /// </summary>
    public ObservableCollection<AirPodsDeviceViewModel> DiscoveredDevices { get; }

    public MainPageViewModel(
        IBluetoothConnectionService connectionService,
        IDeviceStateManager stateManager,
        DispatcherQueue dispatcherQueue)
    {
        _connectionService = connectionService;
        _stateManager = stateManager;
        _dispatcherQueue = dispatcherQueue;
        PairedDevices = new ObservableCollection<AirPodsDeviceViewModel>();
        DiscoveredDevices = new ObservableCollection<AirPodsDeviceViewModel>();

        // Subscribe to DeviceStateManager events (single source of truth)
        _stateManager.DeviceDiscovered += OnDeviceDiscovered;
        _stateManager.DeviceStateChanged += OnDeviceStateChanged;

        _cleanupTimer = _dispatcherQueue.CreateTimer();
        _cleanupTimer.Interval = TimeSpan.FromSeconds(5);
        _cleanupTimer.Tick += OnCleanupTimerTick;
    }

    public async Task InitializeAsync()
    {
        // State manager is already started by App.xaml.cs
        IsScanning = true;
        _cleanupTimer.Start();
        
        // Load paired devices from state manager
        foreach (var state in _stateManager.GetPairedDevices())
        {
            var existing = PairedDevices.FirstOrDefault(d => d.ProductId == state.ProductId);
            if (existing == null)
            {
                var deviceViewModel = new AirPodsDeviceViewModel(state, _connectionService, _stateManager);
                PairedDevices.Add(deviceViewModel);
            }
        }
        HasPairedDevices = PairedDevices.Count > 0;
        
        // Load any existing discovered devices from state manager
        foreach (var state in _stateManager.GetAllDevices())
        {
            var existing = DiscoveredDevices.FirstOrDefault(d => d.ProductId == state.ProductId);
            if (existing == null)
            {
                var deviceViewModel = new AirPodsDeviceViewModel(state, _connectionService, _stateManager);
                DiscoveredDevices.Add(deviceViewModel);
            }
        }
        HasDiscoveredDevices = DiscoveredDevices.Count > 0;
    }

    [RelayCommand]
    private void StopScanning()
    {
        IsScanning = false;
        _stateManager.Stop();
    }

    private void OnDeviceDiscovered(object? sender, AirPodsState state)
    {
        // Already on UI thread (DeviceStateManager marshals events)
        
        // Add to discovered devices list
        var existingDiscovered = DiscoveredDevices.FirstOrDefault(d => d.ProductId == state.ProductId);
        if (existingDiscovered == null)
        {
            var deviceViewModel = new AirPodsDeviceViewModel(state, _connectionService, _stateManager);
            DiscoveredDevices.Add(deviceViewModel);
            HasDiscoveredDevices = DiscoveredDevices.Count > 0;
        }
        
        // If this is a paired device, also add to paired devices list
        if (state.IsPaired)
        {
            var existingPaired = PairedDevices.FirstOrDefault(d => d.ProductId == state.ProductId);
            if (existingPaired == null)
            {
                var pairedDeviceViewModel = new AirPodsDeviceViewModel(state, _connectionService, _stateManager);
                PairedDevices.Add(pairedDeviceViewModel);
                HasPairedDevices = PairedDevices.Count > 0;
            }
        }
    }

    private void OnDeviceStateChanged(object? sender, DeviceStateChangedEventArgs args)
    {
        // Already on UI thread (DeviceStateManager marshals events)
        var state = args.State;
        
        // Update discovered devices
        var existingDiscovered = DiscoveredDevices.FirstOrDefault(d => d.ProductId == state.ProductId);
        if (existingDiscovered != null)
        {
            existingDiscovered.UpdateFromState(state);
        }
        
        // Handle paired devices
        var existingPaired = PairedDevices.FirstOrDefault(d => d.ProductId == state.ProductId);
        
        if (state.IsPaired)
        {
            // Device is paired - add or update
            if (existingPaired == null)
            {
                var pairedDeviceViewModel = new AirPodsDeviceViewModel(state, _connectionService, _stateManager);
                PairedDevices.Add(pairedDeviceViewModel);
                HasPairedDevices = PairedDevices.Count > 0;
            }
            else
            {
                existingPaired.UpdateFromState(state);
            }
        }
        else
        {
            // Device is no longer paired - remove if present
            if (existingPaired != null)
            {
                PairedDevices.Remove(existingPaired);
                HasPairedDevices = PairedDevices.Count > 0;
            }
        }
    }

    private void OnCleanupTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (_isProcessingCleanup)
        {
            return;
        }
        
        _isProcessingCleanup = true;
        
        try
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
                }
            }

            foreach (var device in devicesToRemove)
            {
                DiscoveredDevices.Remove(device);
            }
            
            HasDiscoveredDevices = DiscoveredDevices.Count > 0;
        }
        finally
        {
            _isProcessingCleanup = false;
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Stop();
        _stateManager.DeviceDiscovered -= OnDeviceDiscovered;
        _stateManager.DeviceStateChanged -= OnDeviceStateChanged;
    }
}
