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
    private readonly IAirPodsDiscoveryService _discoveryService;
    private readonly IBluetoothConnectionService _connectionService;
    private readonly IDeviceStateManager _stateManager;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _cleanupTimer;
    private bool _isProcessingCleanup;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _hasDiscoveredDevices;

    public ObservableCollection<AirPodsDeviceViewModel> DiscoveredDevices { get; }

    public MainPageViewModel(
        IAirPodsDiscoveryService discoveryService,
        IBluetoothConnectionService connectionService,
        IDeviceStateManager stateManager,
        DispatcherQueue dispatcherQueue)
    {
        _discoveryService = discoveryService;
        _connectionService = connectionService;
        _stateManager = stateManager;
        _dispatcherQueue = dispatcherQueue;
        DiscoveredDevices = new ObservableCollection<AirPodsDeviceViewModel>();

        // Subscribe to DeviceStateManager events (single source of truth)
        _stateManager.DeviceDiscovered += OnDeviceDiscovered;
        _stateManager.DeviceStateChanged += OnDeviceStateChanged;

        _cleanupTimer = _dispatcherQueue.CreateTimer();
        _cleanupTimer.Interval = TimeSpan.FromSeconds(5);
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

    private void OnDeviceDiscovered(object? sender, DeviceState state)
    {
        // Already on UI thread (DeviceStateManager marshals events)
        var existing = DiscoveredDevices.FirstOrDefault(d => d.ProductId == state.ProductId);
        if (existing == null)
        {
            // Create new ViewModel from DeviceState
            var deviceInfo = CreateDeviceInfoFromState(state);
            var deviceViewModel = new AirPodsDeviceViewModel(deviceInfo, _connectionService, _stateManager);
            DiscoveredDevices.Add(deviceViewModel);
            HasDiscoveredDevices = DiscoveredDevices.Count > 0;
        }
    }

    private void OnDeviceStateChanged(object? sender, DeviceStateChangedEventArgs args)
    {
        // Already on UI thread (DeviceStateManager marshals events)
        var existing = DiscoveredDevices.FirstOrDefault(d => d.ProductId == args.State.ProductId);
        if (existing != null)
        {
            existing.UpdateFromState(args.State);
        }
    }

    private static AirPodsDeviceInfo CreateDeviceInfoFromState(DeviceState state)
    {
        return new AirPodsDeviceInfo
        {
            Address = state.BleAddress,
            ProductId = state.ProductId,
            Model = state.Model,
            DeviceName = state.DeviceName,
            PairedDeviceId = state.PairedDeviceId,
            PairedBluetoothAddress = state.PairedBluetoothAddress,
            LeftBattery = state.LeftBattery,
            RightBattery = state.RightBattery,
            CaseBattery = state.CaseBattery,
            IsLeftCharging = state.IsLeftCharging,
            IsRightCharging = state.IsRightCharging,
            IsCaseCharging = state.IsCaseCharging,
            IsLeftInEar = state.IsLeftInEar,
            IsRightInEar = state.IsRightInEar,
            IsLidOpen = state.IsLidOpen,
            SignalStrength = state.SignalStrength,
            LastSeen = state.LastSeen,
            IsConnected = state.IsConnected
        };
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
        _discoveryService.Dispose();
    }
}
