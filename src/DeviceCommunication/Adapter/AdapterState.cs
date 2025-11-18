// Bluetooth adapter state enumeration
// File: Adapter/AdapterState.cs

namespace DeviceCommunication.Adapter;

/// <summary>
/// Represents the operational state of a Bluetooth adapter.
/// </summary>
public enum AdapterState
{
    /// <summary>
    /// The Bluetooth adapter is powered on and operational.
    /// </summary>
    On,

    /// <summary>
    /// The Bluetooth adapter is powered off or disabled.
    /// </summary>
    Off
}
