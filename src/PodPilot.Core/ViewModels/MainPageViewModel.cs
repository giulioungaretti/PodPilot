using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PodPilot.Core.Models;
using PodPilot.Core.Services;

namespace PodPilot.Core.ViewModels;

/// <summary>
/// ViewModel for the main page displaying AirPods devices.
/// Uses DeviceStateManager as the single source of truth for device state.
/// </summary>
public partial class MainPageViewModel : ObservableObject, IDisposable
{
    private readonly IBluetoothConnectionService _connectionService;
    private readonly IDeviceStateManager _stateManager;
    private readonly IAudioOutputService? _audioOutputService;
    private readonly ISystemLauncherService? _systemLauncherService;
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
        IDeviceStateManager stateManager)
        : this(connectionService, stateManager, null, null)
    {
    }

    public MainPageViewModel(
        IBluetoothConnectionService connectionService,
        IDeviceStateManager stateManager,
        IAudioOutputService? audioOutputService,
        ISystemLauncherService? systemLauncherService)
    {
        _connectionService = connectionService;
        _stateManager = stateManager;
        _audioOutputService = audioOutputService;
        _systemLauncherService = systemLauncherService;
        PairedDevices = new ObservableCollection<AirPodsDeviceViewModel>();
        DiscoveredDevices = new ObservableCollection<AirPodsDeviceViewModel>();

        // Subscribe to DeviceStateManager events (single source of truth)
        _stateManager.DeviceDiscovered += OnDeviceDiscovered;
        _stateManager.DeviceStateChanged += OnDeviceStateChanged;
    }

    public async Task InitializeAsync()
    {
        // State manager is already started by App.xaml.cs
        IsScanning = true;
        
        // Load paired devices from state manager
        foreach (var state in _stateManager.GetPairedDevices())
        {
            var existing = PairedDevices.FirstOrDefault(d => d.ProductId == state.ProductId);
            if (existing == null)
            {
                var deviceViewModel = CreateDeviceViewModel(state);
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
                var deviceViewModel = CreateDeviceViewModel(state);
                DiscoveredDevices.Add(deviceViewModel);
            }
        }
        HasDiscoveredDevices = DiscoveredDevices.Count > 0;

        await Task.CompletedTask;
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
            var deviceViewModel = CreateDeviceViewModel(state);
            DiscoveredDevices.Add(deviceViewModel);
            HasDiscoveredDevices = DiscoveredDevices.Count > 0;
        }
        
        // If this is a paired device, also add to paired devices list
        if (state.IsPaired)
        {
            var existingPaired = PairedDevices.FirstOrDefault(d => d.ProductId == state.ProductId);
            if (existingPaired == null)
            {
                var pairedDeviceViewModel = CreateDeviceViewModel(state);
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
                var pairedDeviceViewModel = CreateDeviceViewModel(state);
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

    /// <summary>
    /// Called by the UI layer to clean up stale devices.
    /// </summary>
    public void OnCleanupTimerTick()
    {
        if (_isProcessingCleanup)
        {
            return;
        }
        
        _isProcessingCleanup = true;
        
        try
        {
            var now = DateTime.Now;
            var devicesToRemove = new List<AirPodsDeviceViewModel>();

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

    private AirPodsDeviceViewModel CreateDeviceViewModel(AirPodsState state)
    {
        return new AirPodsDeviceViewModel(
            state, 
            _connectionService, 
            _stateManager, 
            _audioOutputService, 
            _systemLauncherService);
    }

    public void Dispose()
    {
        _stateManager.DeviceDiscovered -= OnDeviceDiscovered;
        _stateManager.DeviceStateChanged -= OnDeviceStateChanged;
    }
}
