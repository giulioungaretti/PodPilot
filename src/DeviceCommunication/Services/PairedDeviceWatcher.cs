using System.Collections.Concurrent;
using DeviceCommunication.Apple;
using DeviceCommunication.Models;
using Microsoft.Extensions.Logging;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace DeviceCommunication.Services;

/// <summary>
/// Watches for changes to paired Apple/AirPods Bluetooth devices via Windows DeviceWatcher API.
/// This is the source of truth for which devices are paired and their connection status.
/// </summary>
public sealed class PairedDeviceWatcher : IPairedDeviceWatcher
{
    private readonly ILogger<PairedDeviceWatcher> _logger;
    private readonly ConcurrentDictionary<ushort, PairedDeviceInfo> _devicesByProductId = new();
    private readonly ConcurrentDictionary<string, ushort> _productIdByDeviceId = new();
    private DeviceWatcher? _watcher;
    private bool _disposed;
    private bool _enumerationComplete;

    /// <summary>
    /// Additional properties to request from Windows for device connection state.
    /// </summary>
    private static readonly string[] RequestedProperties =
    [
        "System.Devices.Aep.IsConnected",
        "System.Devices.Aep.Bluetooth.Le.IsConnectable"
    ];

    public event EventHandler<PairedDeviceChangedEventArgs>? DeviceChanged;
    public event EventHandler? EnumerationCompleted;

    public PairedDeviceWatcher(ILogger<PairedDeviceWatcher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync()
    {
        if (_watcher != null) return;

        LogDebug("Starting paired device watcher...");

        // First, enumerate existing paired devices
        await EnumerateExistingDevicesAsync();

        // Then start watching for changes
        StartDeviceWatcher();
    }

    public void Stop()
    {
        if (_watcher == null) return;

        LogDebug("Stopping paired device watcher...");
        
        _watcher.Added -= OnDeviceAdded;
        _watcher.Updated -= OnDeviceUpdated;
        _watcher.Removed -= OnDeviceRemoved;
        _watcher.EnumerationCompleted -= OnEnumerationCompleted;
        _watcher.Stop();
        _watcher = null;
    }

    public IReadOnlyList<PairedDeviceInfo> GetPairedDevices()
    {
        return _devicesByProductId.Values.ToList();
    }

    public PairedDeviceInfo? GetByProductId(ushort productId)
    {
        return _devicesByProductId.TryGetValue(productId, out var device) ? device : null;
    }

    private async Task EnumerateExistingDevicesAsync()
    {
        try
        {
            var selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
            var devices = await DeviceInformation.FindAllAsync(selector, RequestedProperties);

            LogDebug($"Found {devices.Count} paired Bluetooth devices");

            foreach (var deviceInfo in devices)
            {
                await ProcessDeviceAsync(deviceInfo, PairedDeviceChangeType.Added, isInitialEnumeration: true);
            }

            _enumerationComplete = true;
            LogDebug($"Initial enumeration complete. Found {_devicesByProductId.Count} Apple devices.");
        }
        catch (Exception ex)
        {
            LogDebug($"Error enumerating paired devices: {ex.Message}");
        }
    }

    private void StartDeviceWatcher()
    {
        var selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
        
        _watcher = DeviceInformation.CreateWatcher(selector, RequestedProperties);
        _watcher.Added += OnDeviceAdded;
        _watcher.Updated += OnDeviceUpdated;
        _watcher.Removed += OnDeviceRemoved;
        _watcher.EnumerationCompleted += OnEnumerationCompleted;
        
        _watcher.Start();
        LogDebug("DeviceWatcher started");
    }

    private async void OnDeviceAdded(DeviceWatcher sender, DeviceInformation deviceInfo)
    {
        LogDebug($"Device added: {deviceInfo.Name} ({deviceInfo.Id})");
        await ProcessDeviceAsync(deviceInfo, PairedDeviceChangeType.Added);
    }

    private async void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        // Find the ProductId for this device
        if (!_productIdByDeviceId.TryGetValue(update.Id, out var productId))
        {
            // We don't know this device - might not be an Apple device
            return;
        }

        // Check if connection state changed
        var isConnected = update.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var connectedObj)
            && connectedObj is true;

        if (_devicesByProductId.TryGetValue(productId, out var existingDevice))
        {
            var updated = existingDevice with { IsConnected = isConnected };
            _devicesByProductId[productId] = updated;

            LogDebug($"Device updated: {updated.Name} - Connected={isConnected}");
            
            RaiseDeviceChanged(updated, PairedDeviceChangeType.Updated);
        }
    }

    private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (_productIdByDeviceId.TryRemove(update.Id, out var productId))
        {
            if (_devicesByProductId.TryRemove(productId, out var device))
            {
                LogDebug($"Device removed: {device.Name}");
                RaiseDeviceChanged(device, PairedDeviceChangeType.Removed);
            }
        }
    }

    private void OnEnumerationCompleted(DeviceWatcher sender, object args)
    {
        if (!_enumerationComplete)
        {
            _enumerationComplete = true;
            LogDebug("DeviceWatcher enumeration completed");
            EnumerationCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task ProcessDeviceAsync(DeviceInformation deviceInfo, PairedDeviceChangeType changeType, bool isInitialEnumeration = false)
    {
        try
        {
            // Try to get the Product ID to identify Apple devices
            using var device = await Device.Device.FromDeviceIdAsync(deviceInfo.Id);
            
            ushort productId;
            try
            {
                productId = await device.GetProductIdAsync();
            }
            catch
            {
                // Not an Apple device or doesn't have Product ID
                return;
            }

            // Check if it's a known Apple device model
            var model = AppleDeviceModelHelper.GetModel(productId);
            if (model == AppleDeviceModel.Unknown)
            {
                return;
            }

            var isConnected = deviceInfo.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var connectedObj)
                && connectedObj is true;

            var pairedDevice = new PairedDeviceInfo
            {
                Id = deviceInfo.Id,
                ProductId = productId,
                Name = device.GetName(),
                Address = device.GetAddress(),
                IsConnected = isConnected
            };

            _devicesByProductId[productId] = pairedDevice;
            _productIdByDeviceId[deviceInfo.Id] = productId;

            LogDebug($"Apple device: {pairedDevice.Name} (ProductId=0x{productId:X4}, Connected={isConnected})");

            // Don't raise events during initial enumeration - wait for EnumerationCompleted
            if (!isInitialEnumeration)
            {
                RaiseDeviceChanged(pairedDevice, changeType);
            }
        }
        catch (Exception ex)
        {
            LogDebug($"Error processing device {deviceInfo.Id}: {ex.Message}");
        }
    }

    private void RaiseDeviceChanged(PairedDeviceInfo device, PairedDeviceChangeType changeType)
    {
        DeviceChanged?.Invoke(this, new PairedDeviceChangedEventArgs
        {
            Device = device,
            ChangeType = changeType
        });
    }

    private void LogDebug(string message) => _logger.LogDebug("{Message}", message);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _devicesByProductId.Clear();
        _productIdByDeviceId.Clear();
    }
}
