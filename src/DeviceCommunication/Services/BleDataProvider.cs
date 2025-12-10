using System.Collections.Concurrent;
using DeviceCommunication.Advertisement;
using DeviceCommunication.Apple;
using DeviceCommunication.Models;
using Microsoft.Extensions.Logging;

namespace DeviceCommunication.Services;

/// <summary>
/// Internal merged state for a single AirPods device (one ProductId).
/// Accumulates data from alternating left/right pod broadcasts.
/// </summary>
internal sealed class MergedDeviceState
{
    public ushort ProductId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    
    // Tracks if this state has received any broadcast data yet
    public bool HasReceivedData { get; set; }
    
    // Metadata (not semantically meaningful for change detection)
    public ulong LastSeenBleAddress { get; set; }
    public short LastSeenRssi { get; set; }
    public DateTime LastUpdate { get; set; }
    
    // Semantically meaningful fields
    public int? LeftBattery { get; set; }
    public int? RightBattery { get; set; }
    public int? CaseBattery { get; set; }
    public bool IsLeftCharging { get; set; }
    public bool IsRightCharging { get; set; }
    public bool IsCaseCharging { get; set; }
    public bool IsLeftInEar { get; set; }
    public bool IsRightInEar { get; set; }
    public bool IsLidOpen { get; set; }
    public bool IsBothPodsInCase { get; set; }
    
    /// <summary>
    /// Creates a snapshot for external consumption (events, GetLatestData).
    /// </summary>
    public BleEnrichmentData ToEnrichmentData()
    {
        return new BleEnrichmentData
        {
            ProductId = ProductId,
            BleAddress = LastSeenBleAddress,
            ModelName = ModelName,
            LeftBattery = LeftBattery,
            RightBattery = RightBattery,
            CaseBattery = CaseBattery,
            IsLeftCharging = IsLeftCharging,
            IsRightCharging = IsRightCharging,
            IsCaseCharging = IsCaseCharging,
            IsLeftInEar = IsLeftInEar,
            IsRightInEar = IsRightInEar,
            IsLidOpen = IsLidOpen,
            IsBothPodsInCase = IsBothPodsInCase,
            SignalStrength = LastSeenRssi,
            LastUpdate = LastUpdate
        };
    }
}

/// <summary>
/// Provides BLE advertisement data for AirPods devices.
/// Reuses existing advertisement parsing logic but separates concerns from device discovery.
/// </summary>
public sealed class BleDataProvider : IBleDataProvider
{
    private readonly ILogger<BleDataProvider> _logger;
    private readonly IAdvertisementWatcher _watcher;
    private readonly ConcurrentDictionary<ushort, MergedDeviceState> _deviceStates = new();
    private readonly TimeSpan _dataTimeout = TimeSpan.FromMinutes(5);
    private bool _disposed;

    public event EventHandler<BleEnrichmentData>? DataReceived;

    /// <summary>
    /// Creates a new BLE data provider.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="watcher">The BLE advertisement watcher.</param>
    public BleDataProvider(ILogger<BleDataProvider> logger, IAdvertisementWatcher watcher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        _watcher.AdvertisementReceived += OnAdvertisementReceived;
    }

    public void Start()
    {
        LogDebug("Starting BLE scanning...");
        _watcher.Start();
    }

    public void Stop()
    {
        LogDebug("Stopping BLE scanning...");
        _watcher.Stop();
    }

    public BleEnrichmentData? GetLatestData(ushort productId)
    {
        if (_deviceStates.TryGetValue(productId, out var state))
        {
            // Check if data is still fresh
            if (DateTime.Now - state.LastUpdate <= _dataTimeout)
            {
                return state.ToEnrichmentData();
            }
        }
        return null;
    }

    public IReadOnlyList<BleEnrichmentData> GetAllSeenDevices()
    {
        var now = DateTime.Now;
        return _deviceStates.Values
            .Where(s => now - s.LastUpdate <= _dataTimeout)
            .Select(s => s.ToEnrichmentData())
            .ToList();
    }

    private void OnAdvertisementReceived(object? sender, AdvertisementReceivedData data)
    {
        if (!data.ManufacturerData.TryGetValue(AppleConstants.VENDOR_ID, out var appleData))
            return;

        var message = ProximityPairingMessage.FromManufacturerData(appleData);
        if (!message.HasValue)
            return;

        var airPods = message.Value;
        var model = airPods.GetModel();

        if (model == AppleDeviceModel.Unknown)
            return;

        var productId = model.GetProductId();
        if (!productId.HasValue)
            return;

        var broadcastSide = airPods.GetBroadcastSide();

        // Get or create merged state for this device
        var state = _deviceStates.GetOrAdd(productId.Value, _ => new MergedDeviceState
        {
            ProductId = productId.Value,
            ModelName = model.GetDisplayName()
        });

        // Track if this is the first time we're seeing this device
        var isFirstUpdate = !state.HasReceivedData;

        // Take snapshot of previous state for change detection (only if not first update)
        var previousSnapshot = isFirstUpdate ? null : state.ToEnrichmentData();

        // Merge broadcast data into state
        MergeBroadcastIntoState(state, airPods, broadcastSide, data);

        // Mark that we've received data
        state.HasReceivedData = true;

        // Take snapshot of new state
        var newSnapshot = state.ToEnrichmentData();

        // Fire event on first update or if semantic state changed
        if (isFirstUpdate || !AreSemanticallySame(previousSnapshot, newSnapshot))
        {
            DataReceived?.Invoke(this, newSnapshot);
        }
    }

    /// <summary>
    /// Merges broadcast data from a ProximityPairingMessage into the device state.
    /// </summary>
    private void MergeBroadcastIntoState(
        MergedDeviceState state,
        ProximityPairingMessage message,
        ProximitySide broadcastSide,
        AdvertisementReceivedData rawData)
    {
        // Always update metadata
        state.LastSeenBleAddress = rawData.Address;
        state.LastSeenRssi = rawData.Rssi;
        state.LastUpdate = DateTime.Now;

        // Always update battery levels (message contains both pods' batteries)
        state.LeftBattery = message.GetLeftBattery() * 10;
        state.RightBattery = message.GetRightBattery() * 10;
        state.CaseBattery = message.GetCaseBattery() * 10;

        // Always update charging states (message contains both pods' charging)
        state.IsLeftCharging = message.IsLeftCharging();
        state.IsRightCharging = message.IsRightCharging();
        state.IsCaseCharging = message.IsCaseCharging();

        // Always update case state
        state.IsLidOpen = message.IsLidOpened();
        state.IsBothPodsInCase = message.IsBothPodsInCase();

        // Update in-ear detection - message contains BOTH pods' states
        // The ProximityPairingMessage methods read both pods' in-ear states from every broadcast
        // using different status flag bits depending on which pod is broadcasting
        state.IsLeftInEar = message.IsLeftInEar();
        state.IsRightInEar = message.IsRightInEar();

        _logger.LogDebug(
            "BLE update for {Model} from {BroadcastSide} pod: " +
            "LeftInEar={LeftInEar}, RightInEar={RightInEar}, " +
            "LeftBattery={LeftBattery}%, RightBattery={RightBattery}%",
            state.ModelName,
            broadcastSide,
            state.IsLeftInEar,
            state.IsRightInEar,
            state.LeftBattery,
            state.RightBattery);
    }

    /// <summary>
    /// Checks if two BleEnrichmentData snapshots are semantically the same.
    /// Excludes metadata fields like BleAddress, SignalStrength, and LastUpdate.
    /// </summary>
    private static bool AreSemanticallySame(BleEnrichmentData? a, BleEnrichmentData? b)
    {
        if (a == null || b == null) return false;
        
        return a.ProductId == b.ProductId &&
               a.ModelName == b.ModelName &&
               a.LeftBattery == b.LeftBattery &&
               a.RightBattery == b.RightBattery &&
               a.CaseBattery == b.CaseBattery &&
               a.IsLeftCharging == b.IsLeftCharging &&
               a.IsRightCharging == b.IsRightCharging &&
               a.IsCaseCharging == b.IsCaseCharging &&
               a.IsLeftInEar == b.IsLeftInEar &&
               a.IsRightInEar == b.IsRightInEar &&
               a.IsLidOpen == b.IsLidOpen &&
               a.IsBothPodsInCase == b.IsBothPodsInCase;
    }

    private void LogDebug(string message) => _logger.LogDebug("{Message}", message);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _watcher.AdvertisementReceived -= OnAdvertisementReceived;
        _watcher.Stop();
        _watcher.Dispose();
        _deviceStates.Clear();
    }
}
