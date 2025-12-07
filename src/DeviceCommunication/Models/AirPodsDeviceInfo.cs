namespace DeviceCommunication.Models;

/// <summary>
/// Represents information about a discovered or saved AirPods device.
/// </summary>
public record AirPodsDeviceInfo
{
    /// <summary>
    /// The BLE advertisement address (may rotate for privacy).
    /// </summary>
    public ulong Address { get; init; }
    
    /// <summary>
    /// The Product ID that uniquely identifies the device model (stable, doesn't rotate).
    /// Used for matching to paired Windows devices.
    /// </summary>
    public ushort ProductId { get; init; }
    
    /// <summary>
    /// The Windows device ID of the paired device (if found).
    /// Used for connection operations.
    /// </summary>
    public string? PairedDeviceId { get; init; }
    
    public string Model { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
    public int? LeftBattery { get; init; }
    public int? RightBattery { get; init; }
    public int? CaseBattery { get; init; }
    public bool IsLeftCharging { get; init; }
    public bool IsRightCharging { get; init; }
    public bool IsCaseCharging { get; init; }
    public bool IsLeftInEar { get; init; }
    public bool IsRightInEar { get; init; }
    public bool IsLidOpen { get; init; }
    public bool IsConnected { get; init; }
    public int SignalStrength { get; init; }
    public DateTime LastSeen { get; init; }
}
