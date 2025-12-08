namespace DeviceCommunication.Models;

/// <summary>
/// Enrichment data from BLE advertisements for AirPods devices.
/// This is not the source of truth for pairing/connection - just enrichment data.
/// </summary>
public sealed record BleEnrichmentData
{
    /// <summary>
    /// The Product ID from the BLE advertisement.
    /// Used to match to paired devices.
    /// </summary>
    public required ushort ProductId { get; init; }
    
    /// <summary>
    /// The rotating BLE advertisement address.
    /// </summary>
    public required ulong BleAddress { get; init; }
    
    /// <summary>
    /// The model display name (e.g., "AirPods Pro (2nd generation)").
    /// </summary>
    public required string ModelName { get; init; }
    
    // Battery levels (0-100, null if unknown)
    public int? LeftBattery { get; init; }
    public int? RightBattery { get; init; }
    public int? CaseBattery { get; init; }
    
    // Charging state
    public bool IsLeftCharging { get; init; }
    public bool IsRightCharging { get; init; }
    public bool IsCaseCharging { get; init; }
    
    // Sensor state
    public bool IsLeftInEar { get; init; }
    public bool IsRightInEar { get; init; }
    public bool IsLidOpen { get; init; }
    public bool IsBothPodsInCase { get; init; }
    
    // Signal
    public short SignalStrength { get; init; }
    public DateTime LastUpdate { get; init; }
}
