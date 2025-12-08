using System;
using System.Collections.Generic;
using DeviceCommunication.Models;

namespace GUI.Services;

/// <summary>
/// Represents the consolidated state of an AirPods device.
/// This is the single source of truth for device state across the application.
/// </summary>
public class DeviceState
{
    /// <summary>
    /// The Product ID that uniquely identifies the device model.
    /// </summary>
    public required ushort ProductId { get; init; }
    
    /// <summary>
    /// The Windows device ID of the paired device (used for connections).
    /// </summary>
    public string? PairedDeviceId { get; set; }
    
    /// <summary>
    /// The Bluetooth Classic address of the paired device (used for audio output checks).
    /// </summary>
    public ulong? PairedBluetoothAddress { get; set; }
    
    /// <summary>
    /// The rotating BLE advertisement address.
    /// </summary>
    public ulong BleAddress { get; set; }
    
    /// <summary>
    /// Human-readable model name (e.g., "AirPods Pro (2nd generation)").
    /// </summary>
    public string Model { get; set; } = string.Empty;
    
    /// <summary>
    /// Device name from Windows (e.g., "AirPods Pro").
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;
    
    // Battery state
    public int? LeftBattery { get; set; }
    public int? RightBattery { get; set; }
    public int? CaseBattery { get; set; }
    public bool IsLeftCharging { get; set; }
    public bool IsRightCharging { get; set; }
    public bool IsCaseCharging { get; set; }
    
    // Sensor state
    public bool IsLeftInEar { get; set; }
    public bool IsRightInEar { get; set; }
    public bool IsLidOpen { get; set; }
    
    // Connection state (managed by DeviceStateManager)
    public bool IsConnected { get; set; }
    public bool IsDefaultAudioOutput { get; set; }
    
    // Timestamps
    public DateTime LastSeen { get; set; }
    public int SignalStrength { get; set; }
    
    /// <summary>
    /// Whether a connection operation is in progress.
    /// When true, external state updates are ignored to prevent race conditions.
    /// </summary>
    public bool IsOperationInProgress { get; set; }
    
    /// <summary>
    /// When the last connection operation completed.
    /// Used for lockout period after operations.
    /// </summary>
    public DateTime? LastOperationCompletedAt { get; set; }
}

/// <summary>
/// Event args for device state changes.
/// </summary>
public class DeviceStateChangedEventArgs : EventArgs
{
    public required DeviceState State { get; init; }
    public required DeviceStateChangeReason Reason { get; init; }
}

/// <summary>
/// Reason for device state change.
/// </summary>
public enum DeviceStateChangeReason
{
    /// <summary>New device discovered via BLE.</summary>
    Discovered,
    
    /// <summary>Device state updated via BLE advertisement.</summary>
    AdvertisementUpdate,
    
    /// <summary>Audio output changed (device became/stopped being default audio).</summary>
    AudioOutputChanged,
    
    /// <summary>User initiated connection.</summary>
    UserConnected,
    
    /// <summary>User initiated disconnection.</summary>
    UserDisconnected,
    
    /// <summary>Connection operation failed.</summary>
    OperationFailed,
    
    /// <summary>Device went stale (not seen for a while).</summary>
    Stale
}

/// <summary>
/// Manages device state as the single source of truth for the application.
/// All components should report state changes here and subscribe to updates.
/// </summary>
public interface IDeviceStateManager : IDisposable
{
    /// <summary>
    /// Raised when any device's state changes.
    /// </summary>
    event EventHandler<DeviceStateChangedEventArgs>? DeviceStateChanged;
    
    /// <summary>
    /// Raised when a new device is discovered.
    /// </summary>
    event EventHandler<DeviceState>? DeviceDiscovered;
    
    /// <summary>
    /// Gets the current state of a device by Product ID.
    /// </summary>
    DeviceState? GetDevice(ushort productId);
    
    /// <summary>
    /// Gets all known devices.
    /// </summary>
    IReadOnlyList<DeviceState> GetAllDevices();
    
    /// <summary>
    /// Reports a device advertisement received from BLE scanning.
    /// Updates battery, sensor, and signal strength data.
    /// Connection state from advertisements is deprioritized during operations.
    /// </summary>
    void ReportAdvertisement(AirPodsDeviceInfo deviceInfo);
    
    /// <summary>
    /// Reports audio output change.
    /// </summary>
    void ReportAudioOutputChange(ulong? bluetoothAddress, bool isDefaultAudioOutput);
    
    /// <summary>
    /// Marks the start of a user-initiated connection operation.
    /// Locks out external state updates to prevent race conditions.
    /// </summary>
    void BeginConnectionOperation(ushort productId);
    
    /// <summary>
    /// Marks the end of a user-initiated connection operation.
    /// </summary>
    void EndConnectionOperation(ushort productId, bool success, bool isConnected, bool isDefaultAudioOutput);
    
    /// <summary>
    /// Removes stale devices that haven't been seen for a while.
    /// </summary>
    void CleanupStaleDevices(TimeSpan staleThreshold);
}
