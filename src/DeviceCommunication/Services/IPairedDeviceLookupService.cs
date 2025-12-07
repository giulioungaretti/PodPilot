using DeviceCommunication.Models;

namespace DeviceCommunication.Services;

/// <summary>
/// Provides cached lookup of paired Bluetooth devices by Product ID.
/// </summary>
/// <remarks>
/// <para>
/// This service centralizes paired device enumeration and provides caching to avoid
/// expensive device enumeration on every BLE advertisement (10+ times/second).
/// </para>
/// <para>
/// Cache entries expire after a configurable duration (default 30 seconds) to balance
/// performance with freshness. Call <see cref="InvalidateCache"/> when the user pairs
/// a new device to ensure immediate visibility.
/// </para>
/// </remarks>
public interface IPairedDeviceLookupService
{
    /// <summary>
    /// Finds a paired Bluetooth device with the specified Product ID.
    /// Results are cached to avoid repeated expensive device enumeration.
    /// </summary>
    /// <param name="targetProductId">The Apple Product ID to search for.</param>
    /// <returns>
    /// The paired device information if found; otherwise, <c>null</c>.
    /// Null results are also cached to avoid repeated lookups for unpaired devices.
    /// </returns>
    Task<PairedDeviceInfo?> FindByProductIdAsync(ushort targetProductId);

    /// <summary>
    /// Invalidates the entire cache.
    /// Call this when the user pairs a new device to ensure immediate visibility.
    /// </summary>
    void InvalidateCache();

    /// <summary>
    /// Invalidates a specific Product ID from the cache.
    /// Useful when a specific device's state may have changed.
    /// </summary>
    /// <param name="productId">The Product ID to invalidate.</param>
    void InvalidateCache(ushort productId);
}
