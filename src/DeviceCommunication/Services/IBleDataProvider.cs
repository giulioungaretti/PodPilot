using DeviceCommunication.Models;

namespace DeviceCommunication.Services;

/// <summary>
/// Provides BLE advertisement data for AirPods devices.
/// This service only parses and provides enrichment data - it does not track pairing or connection state.
/// </summary>
/// <remarks>
/// <para>
/// The BLE data provider scans for Apple proximity pairing messages and extracts battery,
/// charging, in-ear detection, and signal strength data. This data is matched to paired
/// devices using the Product ID.
/// </para>
/// <para>
/// This service does NOT determine if a device is paired or connected - that's the job
/// of <see cref="IPairedDeviceWatcher"/>. This service only provides enrichment data
/// that can be combined with paired device info.
/// </para>
/// </remarks>
public interface IBleDataProvider : IDisposable
{
    /// <summary>
    /// Raised when BLE data is received for a device.
    /// </summary>
    event EventHandler<BleEnrichmentData>? DataReceived;
    
    /// <summary>
    /// Starts scanning for BLE advertisements.
    /// </summary>
    void Start();
    
    /// <summary>
    /// Stops scanning for BLE advertisements.
    /// </summary>
    void Stop();
    
    /// <summary>
    /// Gets the latest BLE data for a device by Product ID.
    /// Returns null if no data has been received for this device.
    /// </summary>
    BleEnrichmentData? GetLatestData(ushort productId);
    
    /// <summary>
    /// Gets all devices that have been seen via BLE.
    /// Includes both paired and unpaired devices.
    /// </summary>
    IReadOnlyList<BleEnrichmentData> GetAllSeenDevices();
}
