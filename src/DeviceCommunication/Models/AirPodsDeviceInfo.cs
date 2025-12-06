namespace DeviceCommunication.Models;

/// <summary>
/// Represents information about a discovered or saved AirPods device.
/// </summary>
public class AirPodsDeviceInfo
{
    public ulong Address { get; set; }
    public string Model { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public int? LeftBattery { get; set; }
    public int? RightBattery { get; set; }
    public int? CaseBattery { get; set; }
    public bool IsLeftCharging { get; set; }
    public bool IsRightCharging { get; set; }
    public bool IsCaseCharging { get; set; }
    public bool IsLeftInEar { get; set; }
    public bool IsRightInEar { get; set; }
    public bool IsLidOpen { get; set; }
    public bool IsConnected { get; set; }
    public int SignalStrength { get; set; }
    public DateTime LastSeen { get; set; }
    public bool IsSaved { get; set; }
}
