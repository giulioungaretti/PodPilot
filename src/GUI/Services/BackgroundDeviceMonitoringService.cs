using System;
using System.Collections.Concurrent;
using DeviceCommunication.Models;
using DeviceCommunication.Services;
using Microsoft.UI.Dispatching;

namespace GUI.Services;

/// <summary>
/// Background service that monitors for paired AirPods devices being advertised.
/// Uses DeviceStateManager as the single source of truth for device state.
/// Tracks device connection state and allows re-notification after disconnect/reconnect cycles.
/// </summary>
public sealed class BackgroundDeviceMonitoringService : IBackgroundDeviceMonitoringService
{
    private readonly IAirPodsDiscoveryService _discoveryService;
    private readonly IDeviceStateManager _stateManager;
    private readonly ConcurrentDictionary<ushort, DeviceConnectionState> _deviceStates;
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _disposed;
    
    /// <summary>
    /// Minimum time between notifications for the same device to prevent rapid oscillation.
    /// </summary>
    private static readonly TimeSpan MinNotificationInterval = TimeSpan.FromSeconds(5);

    public event EventHandler<AirPodsDeviceInfo>? PairedDeviceDetected;

    public BackgroundDeviceMonitoringService(
        DispatcherQueue dispatcherQueue, 
        IAirPodsDiscoveryService discoveryService,
        IDeviceStateManager stateManager)
    {
        ArgumentNullException.ThrowIfNull(dispatcherQueue);
        ArgumentNullException.ThrowIfNull(discoveryService);
        ArgumentNullException.ThrowIfNull(stateManager);
        
        _dispatcherQueue = dispatcherQueue;
        _discoveryService = discoveryService;
        _stateManager = stateManager;
        _deviceStates = new ConcurrentDictionary<ushort, DeviceConnectionState>();
        
        // Subscribe to DeviceStateManager events (single source of truth)
        _stateManager.DeviceStateChanged += OnDeviceStateChanged;
    }

    public void Start()
    {
        _discoveryService.StartScanning();
    }

    public void Stop()
    {
        _discoveryService.StopScanning();
    }

    private void OnDeviceStateChanged(object? sender, DeviceStateChangedEventArgs args)
    {
        var state = args.State;
        
        // Only track paired devices
        if (string.IsNullOrEmpty(state.PairedDeviceId))
            return;

        // Get or create device tracking state
        var trackingState = _deviceStates.GetOrAdd(state.ProductId, _ => new DeviceConnectionState());

        bool wasConnected = trackingState.IsConnected;
        bool isConnected = state.IsConnected;

        // Update tracking state
        trackingState.IsConnected = isConnected;

        // Detect connection transitions
        bool shouldNotify = false;
        
        if (args.Reason == DeviceStateChangeReason.Discovered && isConnected)
        {
            // New device discovered and connected
            shouldNotify = true;
        }
        else if (!wasConnected && isConnected)
        {
            // Disconnect -> reconnect transition
            trackingState.HasNotified = false;
            shouldNotify = true;
        }
        else if (wasConnected && !isConnected)
        {
            // Device disconnected - reset notification flag for next connection
            trackingState.HasNotified = false;
        }

        if (shouldNotify)
        {
            NotifyDeviceDetected(state, trackingState);
        }
    }

    private void NotifyDeviceDetected(DeviceState state, DeviceConnectionState trackingState)
    {
        // Only notify once per connection session
        if (trackingState.HasNotified)
            return;

        // Debounce: Don't notify if we notified recently for this device
        var now = DateTime.UtcNow;
        if (trackingState.LastNotificationTime.HasValue && 
            now - trackingState.LastNotificationTime.Value < MinNotificationInterval)
        {
            return;
        }

        trackingState.HasNotified = true;
        trackingState.LastNotificationTime = now;

        // Create DeviceInfo from state
        var deviceInfo = new AirPodsDeviceInfo
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

        // Already on UI thread (DeviceStateManager marshals events)
        PairedDeviceDetected?.Invoke(this, deviceInfo);
    }

    /// <summary>
    /// Resets notification tracking, allowing devices to trigger notifications again.
    /// </summary>
    public void ResetNotifications()
    {
        foreach (var state in _deviceStates.Values)
        {
            state.HasNotified = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _stateManager.DeviceStateChanged -= OnDeviceStateChanged;
        _deviceStates.Clear();
        _disposed = true;
    }

    private sealed class DeviceConnectionState
    {
        public bool IsConnected { get; set; }
        public bool HasNotified { get; set; }
        public DateTime? LastNotificationTime { get; set; }
    }
}

