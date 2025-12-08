using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DeviceCommunication.Models;
using DeviceCommunication.Services;
using Microsoft.UI.Dispatching;

namespace GUI.Services;

/// <summary>
/// Centralized device state manager - single source of truth for all device states.
/// Handles state updates from multiple sources (BLE, audio, user actions) with proper prioritization.
/// </summary>
public sealed class DeviceStateManager : IDeviceStateManager
{
    private readonly ConcurrentDictionary<ushort, DeviceState> _devices = new();
    private readonly IAirPodsDiscoveryService _discoveryService;
    private readonly IDefaultAudioOutputMonitorService _audioOutputMonitor;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly object _eventLock = new();
    private bool _disposed;
    
    /// <summary>
    /// How long to ignore external state updates after a user operation completes.
    /// This prevents stale cached data from overwriting user-initiated state changes.
    /// </summary>
    private static readonly TimeSpan OperationLockoutPeriod = TimeSpan.FromSeconds(3);
    
    /// <summary>
    /// Minimum time between state change events for the same device.
    /// Prevents UI thrashing from rapid updates.
    /// </summary>
    private static readonly TimeSpan MinEventInterval = TimeSpan.FromMilliseconds(250);
    
    private readonly ConcurrentDictionary<ushort, DateTime> _lastEventTime = new();

    public event EventHandler<DeviceStateChangedEventArgs>? DeviceStateChanged;
    public event EventHandler<DeviceState>? DeviceDiscovered;

    public DeviceStateManager(
        DispatcherQueue dispatcherQueue,
        IAirPodsDiscoveryService discoveryService,
        IDefaultAudioOutputMonitorService audioOutputMonitor)
    {
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _audioOutputMonitor = audioOutputMonitor ?? throw new ArgumentNullException(nameof(audioOutputMonitor));
        
        // Subscribe to discovery events
        _discoveryService.DeviceDiscovered += OnDiscoveryDeviceDiscovered;
        _discoveryService.DeviceUpdated += OnDiscoveryDeviceUpdated;
        
        // Subscribe to audio output changes
        _audioOutputMonitor.DefaultAudioOutputChanged += OnAudioOutputChanged;
    }
    
    private void OnDiscoveryDeviceDiscovered(object? sender, AirPodsDeviceInfo deviceInfo)
    {
        ReportAdvertisement(deviceInfo);
    }
    
    private void OnDiscoveryDeviceUpdated(object? sender, AirPodsDeviceInfo deviceInfo)
    {
        ReportAdvertisement(deviceInfo);
    }
    
    private async void OnAudioOutputChanged(object? sender, DefaultAudioOutputChangedEventArgs args)
    {
        // When audio output changes, check each known device to see if it became the default
        foreach (var device in _devices.Values)
        {
            if (!device.PairedBluetoothAddress.HasValue || device.PairedBluetoothAddress.Value == 0)
                continue;
                
            try
            {
                var isDefault = await Win32BluetoothConnector.IsDefaultAudioOutputAsync(device.PairedBluetoothAddress.Value);
                ReportAudioOutputChange(device.PairedBluetoothAddress.Value, isDefault);
            }
            catch (Exception ex)
            {
                LogDebug($"Error checking audio output for device: {ex.Message}");
            }
        }
    }

    public DeviceState? GetDevice(ushort productId)
    {
        return _devices.TryGetValue(productId, out var state) ? state : null;
    }

    public IReadOnlyList<DeviceState> GetAllDevices()
    {
        return _devices.Values.ToList();
    }

    public void ReportAdvertisement(AirPodsDeviceInfo deviceInfo)
    {
        if (_disposed) return;
        
        var isNewDevice = !_devices.ContainsKey(deviceInfo.ProductId);
        
        var state = _devices.GetOrAdd(deviceInfo.ProductId, _ => new DeviceState
        {
            ProductId = deviceInfo.ProductId
        });
        
        // Always update these non-contentious fields
        state.BleAddress = deviceInfo.Address;
        state.PairedDeviceId = deviceInfo.PairedDeviceId;
        state.PairedBluetoothAddress = deviceInfo.PairedBluetoothAddress;
        state.Model = deviceInfo.Model;
        state.DeviceName = deviceInfo.DeviceName;
        state.LeftBattery = deviceInfo.LeftBattery;
        state.RightBattery = deviceInfo.RightBattery;
        state.CaseBattery = deviceInfo.CaseBattery;
        state.IsLeftCharging = deviceInfo.IsLeftCharging;
        state.IsRightCharging = deviceInfo.IsRightCharging;
        state.IsCaseCharging = deviceInfo.IsCaseCharging;
        state.IsLeftInEar = deviceInfo.IsLeftInEar;
        state.IsRightInEar = deviceInfo.IsRightInEar;
        state.IsLidOpen = deviceInfo.IsLidOpen;
        state.SignalStrength = deviceInfo.SignalStrength;
        state.LastSeen = deviceInfo.LastSeen;
        
        // Only update connection state if not in lockout period
        if (!IsInLockoutPeriod(state))
        {
            var connectionChanged = state.IsConnected != deviceInfo.IsConnected;
            state.IsConnected = deviceInfo.IsConnected;
            
            if (connectionChanged)
            {
                LogDebug($"Connection state updated from advertisement: {deviceInfo.IsConnected} for {deviceInfo.Model}");
            }
        }
        else
        {
            LogDebug($"Ignoring connection state from advertisement (in lockout period): {deviceInfo.IsConnected} for {deviceInfo.Model}");
        }
        
        if (isNewDevice)
        {
            LogDebug($"New device discovered: {deviceInfo.Model} (ProductId=0x{deviceInfo.ProductId:X4})");
            RaiseDeviceDiscovered(state);
        }
        
        RaiseDeviceStateChanged(state, isNewDevice ? DeviceStateChangeReason.Discovered : DeviceStateChangeReason.AdvertisementUpdate);
    }

    public void ReportAudioOutputChange(ulong? bluetoothAddress, bool isDefaultAudioOutput)
    {
        if (_disposed) return;
        
        // Find device by Bluetooth address
        DeviceState? targetDevice = null;
        foreach (var device in _devices.Values)
        {
            if (device.PairedBluetoothAddress == bluetoothAddress)
            {
                targetDevice = device;
                break;
            }
        }
        
        if (targetDevice == null)
        {
            LogDebug($"Audio output change for unknown device: {bluetoothAddress:X12}");
            return;
        }
        
        // Audio output changes are high priority - they're authoritative
        // If device is the default audio output, it's definitely connected
        var wasDefaultAudio = targetDevice.IsDefaultAudioOutput;
        targetDevice.IsDefaultAudioOutput = isDefaultAudioOutput;
        
        if (isDefaultAudioOutput && !targetDevice.IsConnected)
        {
            // Audio routing implies connection
            targetDevice.IsConnected = true;
            LogDebug($"Device {targetDevice.Model} connected (inferred from audio output)");
        }
        
        if (wasDefaultAudio != isDefaultAudioOutput)
        {
            LogDebug($"Audio output changed for {targetDevice.Model}: {isDefaultAudioOutput}");
            RaiseDeviceStateChanged(targetDevice, DeviceStateChangeReason.AudioOutputChanged);
        }
    }

    public void BeginConnectionOperation(ushort productId)
    {
        if (_disposed) return;
        
        if (_devices.TryGetValue(productId, out var state))
        {
            state.IsOperationInProgress = true;
            LogDebug($"Connection operation started for {state.Model}");
        }
    }

    public void EndConnectionOperation(ushort productId, bool success, bool isConnected, bool isDefaultAudioOutput)
    {
        if (_disposed) return;
        
        if (_devices.TryGetValue(productId, out var state))
        {
            state.IsOperationInProgress = false;
            state.LastOperationCompletedAt = DateTime.UtcNow;
            
            if (success)
            {
                state.IsConnected = isConnected;
                state.IsDefaultAudioOutput = isDefaultAudioOutput;
                LogDebug($"Connection operation completed for {state.Model}: Connected={isConnected}, Audio={isDefaultAudioOutput}");
                RaiseDeviceStateChanged(state, isConnected ? DeviceStateChangeReason.UserConnected : DeviceStateChangeReason.UserDisconnected);
            }
            else
            {
                LogDebug($"Connection operation failed for {state.Model}");
                RaiseDeviceStateChanged(state, DeviceStateChangeReason.OperationFailed);
            }
        }
    }

    public void CleanupStaleDevices(TimeSpan staleThreshold)
    {
        if (_disposed) return;
        
        var now = DateTime.Now;
        var staleDevices = _devices.Values
            .Where(d => now - d.LastSeen > staleThreshold)
            .ToList();
        
        foreach (var device in staleDevices)
        {
            if (_devices.TryRemove(device.ProductId, out var removed))
            {
                LogDebug($"Removed stale device: {removed.Model}");
                RaiseDeviceStateChanged(removed, DeviceStateChangeReason.Stale);
            }
        }
    }

    private bool IsInLockoutPeriod(DeviceState state)
    {
        if (state.IsOperationInProgress)
            return true;
        
        if (state.LastOperationCompletedAt.HasValue)
        {
            var elapsed = DateTime.UtcNow - state.LastOperationCompletedAt.Value;
            return elapsed < OperationLockoutPeriod;
        }
        
        return false;
    }

    private void RaiseDeviceDiscovered(DeviceState state)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            DeviceDiscovered?.Invoke(this, state);
        });
    }

    private void RaiseDeviceStateChanged(DeviceState state, DeviceStateChangeReason reason)
    {
        // Debounce: Skip if we raised an event too recently for this device
        var now = DateTime.UtcNow;
        if (_lastEventTime.TryGetValue(state.ProductId, out var lastTime))
        {
            if (now - lastTime < MinEventInterval && 
                reason == DeviceStateChangeReason.AdvertisementUpdate)
            {
                // Only skip for advertisement updates - user actions and audio changes are important
                return;
            }
        }
        _lastEventTime[state.ProductId] = now;
        
        _dispatcherQueue.TryEnqueue(() =>
        {
            DeviceStateChanged?.Invoke(this, new DeviceStateChangedEventArgs
            {
                State = state,
                Reason = reason
            });
        });
    }

    [Conditional("DEBUG")]
    private static void LogDebug(string message) => Debug.WriteLine($"[DeviceStateManager] {message}");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        // Unsubscribe from events
        _discoveryService.DeviceDiscovered -= OnDiscoveryDeviceDiscovered;
        _discoveryService.DeviceUpdated -= OnDiscoveryDeviceUpdated;
        _audioOutputMonitor.DefaultAudioOutputChanged -= OnAudioOutputChanged;
        
        _devices.Clear();
        _lastEventTime.Clear();
    }
}
