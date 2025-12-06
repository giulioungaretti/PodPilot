using DeviceCommunication.Models;

namespace DeviceCommunication.Services;

/// <summary>
/// Represents a change in device state (discovered, updated, or removed).
/// </summary>
public record DeviceStateChange
{
    /// <summary>
    /// The type of state change that occurred.
    /// </summary>
    public required DeviceChangeType ChangeType { get; init; }

    /// <summary>
    /// The device information associated with this change.
    /// </summary>
    public required AirPodsDeviceInfo Device { get; init; }

    /// <summary>
    /// Unique identifier for this logical device (used for grouping broadcasts from same physical device).
    /// </summary>
    public required Guid DeviceId { get; init; }
}

/// <summary>
/// Type of device state change.
/// </summary>
public enum DeviceChangeType
{
    /// <summary>
    /// A new device was discovered.
    /// </summary>
    Added,

    /// <summary>
    /// An existing device's information was updated.
    /// </summary>
    Updated,

    /// <summary>
    /// A device was removed (timed out or went out of range).
    /// </summary>
    Removed
}
