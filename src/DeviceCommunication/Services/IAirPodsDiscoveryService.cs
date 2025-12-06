using System;
using DeviceCommunication.Models;

namespace DeviceCommunication.Services;

/// <summary>
/// Provides functionality to discover AirPods devices via Bluetooth advertisements.
/// Raises events when devices are discovered or updated, and allows scanning control.
/// </summary>
/// <remarks>
/// This interface is deprecated. Use <see cref="AirPodsDeviceAggregator"/> with <see cref="Advertisement.IAdvertisementStream"/>
/// for a cleaner reactive architecture with proper separation of concerns.
/// This interface will be maintained for backward compatibility with CLI examples.
/// </remarks>
[Obsolete("Use AirPodsDeviceAggregator with IAdvertisementStream for better architecture. This interface is maintained for backward compatibility.")]
public interface IAirPodsDiscoveryService : IDisposable
{
    /// <summary>
    /// Occurs when a new AirPods device is discovered.
    /// </summary>
    event EventHandler<AirPodsDeviceInfo>? DeviceDiscovered;

    /// <summary>
    /// Occurs when an existing AirPods device's information is updated.
    /// </summary>
    event EventHandler<AirPodsDeviceInfo>? DeviceUpdated;

    /// <summary>
    /// Occurs when an AirPods device is removed due to timeout (no longer advertising).
    /// </summary>
    event EventHandler<AirPodsDeviceInfo>? DeviceRemoved;

    /// <summary>
    /// Starts scanning for AirPods devices via Bluetooth advertisements.
    /// </summary>
    void StartScanning();

    /// <summary>
    /// Stops scanning for AirPods devices.
    /// </summary>
    void StopScanning();

    /// <summary>
    /// Gets a read-only list of all currently discovered AirPods devices.
    /// </summary>
    /// <returns>A read-only list of <see cref="AirPodsDeviceInfo"/> objects representing discovered devices.</returns>
    IReadOnlyList<AirPodsDeviceInfo> GetDiscoveredDevices();
}

