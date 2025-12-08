using System.Collections.Concurrent;
using System.Diagnostics;
using DeviceCommunication.Models;
using Windows.Devices.Bluetooth;

namespace DeviceCommunication.Services;

/// <summary>
/// Provides cached lookup of paired Bluetooth devices by Product ID.
/// </summary>
/// <remarks>
/// <para>
/// This service significantly improves performance by caching paired device lookups.
/// Without caching, every BLE advertisement (10+ times/second) would trigger a full
/// enumeration of all paired Bluetooth devices via DeviceInformation.FindAllAsync.
/// </para>
/// <para>
/// The cache uses a 30-second expiration by default. Even null results (device not found)
/// are cached to avoid repeated expensive lookups for devices that aren't paired.
/// </para>
/// <para>
/// Thread-safety is achieved using <see cref="ConcurrentDictionary{TKey,TValue}"/> for lock-free
/// reads and <see cref="Lazy{T}"/> to prevent duplicate lookups for the same ProductId.
/// </para>
/// </remarks>
public sealed class PairedDeviceLookupService : IPairedDeviceLookupService
{
    private readonly ConcurrentDictionary<ushort, Lazy<Task<CacheEntry>>> _cache = new();
    private readonly TimeSpan _cacheExpiration;

    /// <summary>
    /// Creates a new instance with the default 30-second cache expiration.
    /// </summary>
    public PairedDeviceLookupService() : this(TimeSpan.FromSeconds(30))
    {
    }

    /// <summary>
    /// Creates a new instance with a custom cache expiration.
    /// </summary>
    /// <param name="cacheExpiration">How long cache entries remain valid.</param>
    public PairedDeviceLookupService(TimeSpan cacheExpiration)
    {
        _cacheExpiration = cacheExpiration;
    }

    /// <inheritdoc />
    public async Task<PairedDeviceInfo?> FindByProductIdAsync(ushort targetProductId)
    {
        // Try to get existing entry
        if (_cache.TryGetValue(targetProductId, out var existingLazy))
        {
            var existingEntry = await existingLazy.Value.ConfigureAwait(false);
            if (DateTime.UtcNow - existingEntry.Timestamp < _cacheExpiration)
            {
                //LogDebug($"Cache HIT for ProductId 0x{targetProductId:X4}");
                return existingEntry.DeviceInfo;
            }

            // Entry expired - remove it so we can create a fresh one
            _cache.TryRemove(targetProductId, out _);
        }

        LogDebug($"Cache MISS for ProductId 0x{targetProductId:X4}, querying paired devices...");

        // GetOrAdd with Lazy ensures only one lookup runs even if called concurrently
        var lazy = _cache.GetOrAdd(
            targetProductId,
            _ => new Lazy<Task<CacheEntry>>(() => CreateCacheEntryAsync(targetProductId)));

        var entry = await lazy.Value.ConfigureAwait(false);
        return entry.DeviceInfo;
    }

    private static async Task<CacheEntry> CreateCacheEntryAsync(ushort targetProductId)
    {
        var result = await LookupPairedDeviceAsync(targetProductId).ConfigureAwait(false);
        LogDebug($"Cached result for ProductId 0x{targetProductId:X4}: {result?.Name ?? "null"}");
        return new CacheEntry(result, DateTime.UtcNow);
    }

    /// <inheritdoc />
    public void InvalidateCache()
    {
        var count = _cache.Count;
        _cache.Clear();
        LogDebug($"Cache invalidated ({count} entries cleared)");
    }

    /// <inheritdoc />
    public void InvalidateCache(ushort productId)
    {
        if (_cache.TryRemove(productId, out _))
        {
            LogDebug($"Cache entry invalidated for ProductId 0x{productId:X4}");
        }
    }

    private static async Task<PairedDeviceInfo?> LookupPairedDeviceAsync(ushort targetProductId)
    {
        try
        {
            // Get all paired Bluetooth devices
            var selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
            var deviceInfos = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(selector)
                .AsTask().ConfigureAwait(false);

            LogDebug($"Searching {deviceInfos.Count} paired devices for ProductId 0x{targetProductId:X4}...");

            foreach (var deviceInfo in deviceInfos)
            {
                try
                {
                    using var device = await Device.Device.FromDeviceIdAsync(deviceInfo.Id).ConfigureAwait(false);

                    // Try to get Product ID
                    ushort productId;
                    try
                    {
                        productId = await device.GetProductIdAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // Device doesn't have Product ID property
                        continue;
                    }

                    if (productId == targetProductId)
                    {
                        LogDebug($"MATCH FOUND: {device.GetName()} (ProductId=0x{productId:X4})");
                        return new PairedDeviceInfo
                        {
                            Id = deviceInfo.Id,
                            Name = device.GetName(),
                            Address = device.GetAddress(),
                            IsConnected = device.IsConnected()
                        };
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"Error querying device {deviceInfo.Id}: {ex.Message}");
                }
            }

            LogDebug($"No paired device found with ProductId 0x{targetProductId:X4}");
        }
        catch (Exception ex)
        {
            LogDebug($"Error enumerating paired devices: {ex.Message}");
        }

        return null;
    }

    [Conditional("DEBUG")]
    private static void LogDebug(string message) => Debug.WriteLine($"[PairedDeviceLookupService] {message}");

    private sealed record CacheEntry(PairedDeviceInfo? DeviceInfo, DateTime Timestamp);
}
