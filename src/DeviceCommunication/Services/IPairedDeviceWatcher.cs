using DeviceCommunication.Models;

namespace DeviceCommunication.Services;

/// <summary>
/// Watches for changes to paired Apple/AirPods Bluetooth devices via Windows APIs.
/// This is the source of truth for which devices are paired and their connection status.
/// </summary>
/// <remarks>
/// <para>
/// This service monitors Windows for paired Bluetooth device changes using the DeviceWatcher API.
/// It filters for Apple devices (by Product ID) and raises events when devices are added,
/// updated (e.g., connection state changes), or removed.
/// </para>
/// <para>
/// Unlike BLE advertisement scanning which sees all nearby devices, this only tracks
/// devices that are explicitly paired with Windows.
/// </para>
/// </remarks>
public interface IPairedDeviceWatcher : IDisposable
{
    /// <summary>
    /// Raised when a paired device is added, updated, or removed.
    /// </summary>
    event EventHandler<PairedDeviceChangedEventArgs>? DeviceChanged;
    
    /// <summary>
    /// Raised when the initial enumeration of paired devices is complete.
    /// </summary>
    event EventHandler? EnumerationCompleted;
    
    /// <summary>
    /// Starts watching for paired device changes.
    /// </summary>
    Task StartAsync();
    
    /// <summary>
    /// Stops watching for paired device changes.
    /// </summary>
    void Stop();
    
    /// <summary>
    /// Gets all currently known paired Apple devices.
    /// </summary>
    IReadOnlyList<PairedDeviceInfo> GetPairedDevices();
    
    /// <summary>
    /// Gets a paired device by its Product ID.
    /// </summary>
    /// <param name="productId">The Apple Product ID.</param>
    /// <returns>The paired device info if found; otherwise, null.</returns>
    PairedDeviceInfo? GetByProductId(ushort productId);
}
