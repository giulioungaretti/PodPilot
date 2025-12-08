using System.Collections.Concurrent;
using DeviceCommunication.Advertisement;
using DeviceCommunication.Apple;
using DeviceCommunication.Models;
using Microsoft.Extensions.Logging;

namespace DeviceCommunication.Services;

/// <summary>
/// Provides BLE advertisement data for AirPods devices.
/// Reuses existing advertisement parsing logic but separates concerns from device discovery.
/// </summary>
public sealed class BleDataProvider : IBleDataProvider
{
    private readonly ILogger<BleDataProvider> _logger;
    private readonly IAdvertisementWatcher _watcher;
    private readonly ConcurrentDictionary<ushort, BleEnrichmentData> _dataByProductId = new();
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
        if (_dataByProductId.TryGetValue(productId, out var data))
        {
            // Check if data is still fresh
            if (DateTime.Now - data.LastUpdate <= _dataTimeout)
            {
                return data;
            }
        }
        return null;
    }

    public IReadOnlyList<BleEnrichmentData> GetAllSeenDevices()
    {
        var now = DateTime.Now;
        return _dataByProductId.Values
            .Where(d => now - d.LastUpdate <= _dataTimeout)
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

        var enrichmentData = new BleEnrichmentData
        {
            ProductId = productId.Value,
            BleAddress = data.Address,
            ModelName = model.GetDisplayName(),
            LeftBattery = airPods.GetLeftBattery() * 10,
            RightBattery = airPods.GetRightBattery() * 10,
            CaseBattery = airPods.GetCaseBattery() * 10,
            IsLeftCharging = airPods.IsLeftCharging(),
            IsRightCharging = airPods.IsRightCharging(),
            IsCaseCharging = airPods.IsCaseCharging(),
            IsLeftInEar = airPods.IsLeftInEar(),
            IsRightInEar = airPods.IsRightInEar(),
            IsLidOpen = airPods.IsLidOpened(),
            IsBothPodsInCase = airPods.IsBothPodsInCase(),
            SignalStrength = data.Rssi,
            LastUpdate = DateTime.Now
        };

        var isNew = !_dataByProductId.ContainsKey(productId.Value);
        _dataByProductId[productId.Value] = enrichmentData;

        if (isNew)
        {
            LogDebug($"New device seen: {model.GetDisplayName()} (ProductId=0x{productId.Value:X4})");
        }

        DataReceived?.Invoke(this, enrichmentData);
    }

    private void LogDebug(string message) => _logger.LogDebug("{Message}", message);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _watcher.AdvertisementReceived -= OnAdvertisementReceived;
        _watcher.Stop();
        _watcher.Dispose();
        _dataByProductId.Clear();
    }
}
