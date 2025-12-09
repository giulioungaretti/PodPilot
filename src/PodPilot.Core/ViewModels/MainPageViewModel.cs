using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PodPilot.Core.Models;
using PodPilot.Core.Services;
using System.Collections.ObjectModel;

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
        foreach (var state in _stateManager.GetDiscoveredDevices())
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

    [RelayCommand]
    private async Task OpenBluetoothSettingsAsync()
    {
        if (_systemLauncherService == null) return;
        await _systemLauncherService.OpenBluetoothSettingsAsync();
    }

    private void OnDeviceDiscovered(object? sender, AirPodsState state)
    {
        var existingPaired = PairedDevices.FirstOrDefault(d => d.ProductId == state.ProductId);
        var existingDiscovered = DiscoveredDevices.FirstOrDefault(d => d.ProductId == state.ProductId);

        switch ((state.IsPaired, existingPaired, existingDiscovered))
        {
            // Paired device not yet in the paired list -> add
            case (true, null, _):
            {
                var pairedDeviceViewModel = CreateDeviceViewModel(state);
                PairedDevices.Add(pairedDeviceViewModel);
                HasPairedDevices = PairedDevices.Count > 0;
                // If we already had this device in the discovered list (from BLE), remove it
                if (existingDiscovered != null)
                {
                    DiscoveredDevices.Remove(existingDiscovered);
                    HasDiscoveredDevices = DiscoveredDevices.Count > 0;
                }
                break;
            }

            // Paired device already present -> update
            case (true, AirPodsDeviceViewModel paired, _):
            {
                paired.UpdateFromState(state);
                // If we also have a discovered entry for the same product id, remove it to avoid duplicates
                if (existingDiscovered != null)
                {
                    DiscoveredDevices.Remove(existingDiscovered);
                    HasDiscoveredDevices = DiscoveredDevices.Count > 0;
                }
                break;
            }

            // Unpaired device not yet discovered -> add
            case (false, _, null):
            {
                var deviceViewModel = CreateDeviceViewModel(state);
                DiscoveredDevices.Add(deviceViewModel);
                HasDiscoveredDevices = DiscoveredDevices.Count > 0;
                break;
            }

            // Unpaired device already discovered -> update
            case (false, _, AirPodsDeviceViewModel discovered):
            {
                discovered.UpdateFromState(state);
                break;
            }

            default:
        }
    }

    private void OnDeviceStateChanged(object? sender, DeviceStateChangedEventArgs args)
    {
        var state = args.State;

        var existingDiscovered = DiscoveredDevices.FirstOrDefault(d => d.ProductId == state.ProductId);
        var existingPaired = PairedDevices.FirstOrDefault(d => d.ProductId == state.ProductId);

        // Use tuple pattern matching to make the branching explicit and easier to follow
        switch ((state.IsPaired, existingPaired, existingDiscovered))
        {
            // Paired device that is not yet in the paired list -> add
            case (true, null, _):
            {
                var pairedDeviceViewModel = CreateDeviceViewModel(state);
                PairedDevices.Add(pairedDeviceViewModel);
                HasPairedDevices = PairedDevices.Count > 0;
                break;
            }

            // Paired device already present -> update
            case (true, AirPodsDeviceViewModel paired, _):
            {
                paired.UpdateFromState(state);
                break;
            }

            // Unpaired device that was discovered -> update discovery entry
            case (false, _, AirPodsDeviceViewModel discovered) when discovered != null:
            {
                discovered.UpdateFromState(state);
                break;
            }

            // Fallback: device is no longer paired -> remove from paired list if present
            default:
            {
                if (existingPaired != null)
                {
                    PairedDevices.Remove(existingPaired);
                    HasPairedDevices = PairedDevices.Count > 0;
                }

                break;
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
