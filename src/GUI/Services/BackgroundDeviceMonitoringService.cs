using System;
using System.Collections.Generic;
using DeviceCommunication.Models;
using DeviceCommunication.Services;
using Microsoft.UI.Dispatching;

namespace GUI.Services;

/// <summary>
/// Background service that monitors for paired AirPods devices being advertised.
/// Tracks device connection state and allows re-notification after disconnect/reconnect cycles.
/// </summary>
internal sealed class BackgroundDeviceMonitoringService : IDisposable
{
    private readonly IAirPodsDiscoveryService _discoveryService;
    private readonly Dictionary<string, DeviceConnectionState> _deviceStates;
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _disposed;

    public event EventHandler<AirPodsDeviceInfo>? PairedDeviceDetected;

    public BackgroundDeviceMonitoringService(DispatcherQueue dispatcherQueue)
    {
        ArgumentNullException.ThrowIfNull(dispatcherQueue);
        
        _dispatcherQueue = dispatcherQueue;
        _discoveryService = new SimpleAirPodsDiscoveryService();
        _deviceStates = new Dictionary<string, DeviceConnectionState>();
        
        _discoveryService.DeviceDiscovered += OnDeviceDiscovered;
        _discoveryService.DeviceUpdated += OnDeviceUpdated;
    }

    public void Start()
    {
        _discoveryService.StartScanning();
    }

    public void Stop()
    {
        _discoveryService.StopScanning();
    }

    private void OnDeviceDiscovered(object? sender, AirPodsDeviceInfo device)
    {
        if (string.IsNullOrEmpty(device.PairedDeviceId))
            return;

        // Track the new device state
        var state = new DeviceConnectionState
        {
            IsConnected = device.IsConnected,
            HasNotified = false
        };
        _deviceStates[device.PairedDeviceId] = state;

        // Notify if device is connected
        if (device.IsConnected)
        {
            NotifyDeviceDetected(device);
        }
    }

    private void OnDeviceUpdated(object? sender, AirPodsDeviceInfo device)
    {
        if (string.IsNullOrEmpty(device.PairedDeviceId))
            return;

        // Get or create device state
        if (!_deviceStates.TryGetValue(device.PairedDeviceId, out var state))
        {
            state = new DeviceConnectionState();
            _deviceStates[device.PairedDeviceId] = state;
        }

        bool wasConnected = state.IsConnected;
        bool isConnected = device.IsConnected;

        // Update connection state
        state.IsConnected = isConnected;

        // Detect disconnect -> reconnect transition
        if (!wasConnected && isConnected)
        {
            // Device reconnected - reset notification flag and notify
            state.HasNotified = false;
            NotifyDeviceDetected(device);
        }
        else if (wasConnected && !isConnected)
        {
            // Device disconnected - reset notification flag for next connection
            state.HasNotified = false;
        }
    }

    private void NotifyDeviceDetected(AirPodsDeviceInfo device)
    {
        if (string.IsNullOrEmpty(device.PairedDeviceId))
            return;

        if (!_deviceStates.TryGetValue(device.PairedDeviceId, out var state))
            return;

        // Only notify once per connection session
        if (state.HasNotified)
            return;

        state.HasNotified = true;

        // Marshal to UI thread and raise event
        _dispatcherQueue.TryEnqueue(() =>
        {
            PairedDeviceDetected?.Invoke(this, device);
        });
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

        _discoveryService.DeviceDiscovered -= OnDeviceDiscovered;
        _discoveryService.DeviceUpdated -= OnDeviceUpdated;
        _discoveryService.Dispose();
        _deviceStates.Clear();
        _disposed = true;
    }

    private sealed class DeviceConnectionState
    {
        public bool IsConnected { get; set; }
        public bool HasNotified { get; set; }
    }
}
