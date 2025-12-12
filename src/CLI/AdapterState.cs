// Bluetooth adapter state enumeration
// File: Services/AdapterState.cs

namespace CLI.Services;

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
