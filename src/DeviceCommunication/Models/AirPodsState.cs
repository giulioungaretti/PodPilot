namespace DeviceCommunication.Models;

/// <summary>
/// Unified state for an AirPods device combining paired device info and BLE enrichment data.
/// </summary>
public sealed record AirPodsState
{
    /// <summary>
    /// The Apple Product ID that uniquely identifies the device model.
    /// </summary>
    public required ushort ProductId { get; init; }
    
    /// <summary>
    /// The Windows device ID (for paired devices, used for connections).
    /// Null for unpaired devices seen only via BLE.
    /// </summary>
    public string? PairedDeviceId { get; init; }
    
    /// <summary>
    /// The Bluetooth Classic address (for paired devices).
    /// Used for audio output matching.
    /// </summary>
    public ulong? BluetoothAddress { get; init; }
    
    /// <summary>
    /// The rotating BLE advertisement address (from latest BLE data).
    /// </summary>
    public ulong? BleAddress { get; init; }
    
    /// <summary>
    /// Device name - from paired device if available, otherwise from BLE model name.
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Model display name (e.g., "AirPods Pro (2nd generation)").
    /// </summary>
    public required string ModelName { get; init; }
    
    // Pairing and connection state (from Windows API - source of truth)
    
    /// <summary>
    /// Whether the device is paired with Windows.
    /// </summary>
    public bool IsPaired { get; init; }
    
    /// <summary>
    /// Whether the device is connected (from Windows API).
    /// </summary>
    public bool IsConnected { get; init; }
    
    /// <summary>
    /// Whether the device is currently the default audio output.
    /// </summary>
    public bool IsAudioConnected { get; init; }
    
    // Battery state (from BLE - enrichment data)
    
    public int? LeftBattery { get; init; }
    public int? RightBattery { get; init; }
    public int? CaseBattery { get; init; }
    public bool IsLeftCharging { get; init; }
    public bool IsRightCharging { get; init; }
    public bool IsCaseCharging { get; init; }
    
    // Sensor state (from BLE - enrichment data)
    
    public bool IsLeftInEar { get; init; }
    public bool IsRightInEar { get; init; }
    public bool IsLidOpen { get; init; }
    public bool IsBothPodsInCase { get; init; }
    
    // Signal and timing
    
    public short SignalStrength { get; init; }
    public DateTime? LastBleUpdate { get; init; }
    public DateTime LastSeen { get; init; }
    
    /// <summary>
    /// Whether at least one AirPod is in ear.
    /// </summary>
    public bool IsInEar => IsLeftInEar || IsRightInEar;
    
    /// <summary>
    /// Whether the device is ready for connection (at least one pod out of case).
    /// </summary>
    public bool IsReadyForConnection => !IsBothPodsInCase;
}

/// <summary>
/// Reason for state change notification.
/// </summary>
public enum AirPodsStateChangeReason
{
    /// <summary>Initial enumeration of paired devices.</summary>
    InitialEnumeration,
    
    /// <summary>A new paired device was added.</summary>
    PairedDeviceAdded,
    
    /// <summary>A paired device was removed (unpaired).</summary>
    PairedDeviceRemoved,
    
    /// <summary>Device connection state changed (connected/disconnected).</summary>
    ConnectionChanged,
    
    /// <summary>Audio output state changed.</summary>
    AudioOutputChanged,
    
    /// <summary>BLE data updated (battery, sensors, etc.).</summary>
    BleDataUpdated,
    
    /// <summary>Unpaired device seen via BLE.</summary>
    UnpairedDeviceSeen,
    
    /// <summary>Device removed from case (for auto-pause feature).</summary>
    RemovedFromCase
}

/// <summary>
/// Event args for state changes.
/// </summary>
public sealed record AirPodsStateChangedEventArgs
{
    public required AirPodsState State { get; init; }
    public required AirPodsStateChangeReason Reason { get; init; }
}
