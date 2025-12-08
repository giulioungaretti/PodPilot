using DeviceCommunication.Models;

namespace DeviceCommunication.Services;

/// <summary>
/// Unified service that combines Windows paired device state with BLE advertisement data.
/// Uses ProductID to match BLE data to paired devices.
/// </summary>
/// <remarks>
/// <para>
/// This service is the unified entry point for AirPods device state. It combines:
/// </para>
/// <list type="bullet">
/// <item><description>Paired device info from Windows API (source of truth for pairing/connection)</description></item>
/// <item><description>BLE advertisement data (enrichment for battery, sensors, etc.)</description></item>
/// <item><description>Audio output state (is device the default audio output)</description></item>
/// </list>
/// <para>
/// The service matches BLE data to paired devices using the Apple Product ID.
/// </para>
/// </remarks>
public interface IAirPodsStateService : IDisposable
{
    /// <summary>
    /// Raised when any device's state changes.
    /// </summary>
    event EventHandler<AirPodsStateChangedEventArgs>? StateChanged;
    
    /// <summary>
    /// Raised when a paired device appears via BLE but isn't connected.
    /// Used to bring UI forward for quick connection.
    /// </summary>
    event EventHandler<AirPodsState>? PairedDeviceNeedsAttention;
    
    /// <summary>
    /// Raised when AirPods are removed from case (for media pause feature).
    /// </summary>
    event EventHandler<AirPodsState>? AirPodsRemovedFromCase;
    
    /// <summary>
    /// Starts monitoring paired devices and BLE advertisements.
    /// </summary>
    Task StartAsync();
    
    /// <summary>
    /// Stops monitoring.
    /// </summary>
    void Stop();
    
    /// <summary>
    /// Gets all known devices (paired + unpaired seen via BLE).
    /// </summary>
    IReadOnlyList<AirPodsState> GetAllDevices();
    
    /// <summary>
    /// Gets only paired devices.
    /// </summary>
    IReadOnlyList<AirPodsState> GetPairedDevices();
    
    /// <summary>
    /// Gets a device by Product ID.
    /// </summary>
    AirPodsState? GetDevice(ushort productId);
    
    /// <summary>
    /// Reports that a user-initiated operation is starting.
    /// Prevents external state updates from overwriting user actions.
    /// </summary>
    void BeginOperation(ushort productId);
    
    /// <summary>
    /// Reports that a user-initiated operation completed.
    /// </summary>
    void EndOperation(ushort productId, bool success, bool isConnected, bool isAudioConnected);
}
