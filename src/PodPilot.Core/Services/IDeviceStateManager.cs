using PodPilot.Core.Models;

namespace PodPilot.Core.Services;

/// <summary>
/// Event args for device state changes in the UI layer.
/// </summary>
public class DeviceStateChangedEventArgs : EventArgs
{
    public required AirPodsState State { get; init; }
    public required AirPodsStateChangeReason Reason { get; init; }
}

/// <summary>
/// UI-layer device state manager that wraps <see cref="IAirPodsStateService"/> 
/// and marshals events to the UI thread.
/// </summary>
public interface IDeviceStateManager : IDisposable
{
    /// <summary>
    /// Raised when any device's state changes (on UI thread).
    /// </summary>
    event EventHandler<DeviceStateChangedEventArgs>? DeviceStateChanged;
    
    /// <summary>
    /// Raised when a new device is discovered (on UI thread).
    /// </summary>
    event EventHandler<AirPodsState>? DeviceDiscovered;
    
    /// <summary>
    /// Raised when a paired device appears via BLE but isn't connected (on UI thread).
    /// Used to bring UI forward for quick connection.
    /// </summary>
    event EventHandler<AirPodsState>? PairedDeviceNeedsAttention;
    
    /// <summary>
    /// Gets the current state of a device by Product ID.
    /// </summary>
    AirPodsState? GetDevice(ushort productId);
    
    /// <summary>
    /// Gets all known devices.
    /// </summary>
    IReadOnlyList<AirPodsState> GetAllDevices();
    
    /// <summary>
    /// Gets only paired devices.
    /// </summary>
    IReadOnlyList<AirPodsState> GetPairedDevices();
    
    /// <summary>
    /// Starts monitoring (starts underlying services).
    /// </summary>
    Task StartAsync();
    
    /// <summary>
    /// Stops monitoring.
    /// </summary>
    void Stop();
    
    /// <summary>
    /// Marks the start of a user-initiated connection operation.
    /// </summary>
    void BeginConnectionOperation(ushort productId);
    
    /// <summary>
    /// Marks the end of a user-initiated connection operation.
    /// </summary>
    void EndConnectionOperation(ushort productId, bool success, bool isConnected, bool isAudioConnected);
}
