using DeviceCommunication.Apple;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace DeviceCommunication.Services;

/// <summary>
/// [LEGACY ARCHITECTURE] Monitors Bluetooth device connection status changes.
/// </summary>
/// <remarks>
/// <para><strong>?? This class is part of the legacy architecture and is maintained only for educational/comparison purposes.</strong></para>
/// <para>For new code, consider using <see cref="SimpleAirPodsDiscoveryService"/> which uses Product ID-based identification
/// instead of complex connection monitoring and address tracking.</para>
/// <para>Legacy approach: Track connection states ? Match by address rotation ? Complex heuristics</para>
/// <para>New approach: Direct Product ID lookup ? Stable device identification ? Simple and reliable</para>
/// </remarks>
/// <summary>
/// Represents a paired Bluetooth device with its current state.
/// </summary>
/// <param name="Name">The device name (may be user-customized).</param>
/// <param name="Address">The Bluetooth address.</param>
/// <param name="IsConnected">Whether the device is currently connected.</param>
/// <param name="DeviceClass">The major device class (e.g., "AudioVideo", "Phone", "Computer").</param>
public record PairedBluetoothDevice(string Name, ulong Address, bool IsConnected, string DeviceClass);

/// <summary>
/// Represents a connection state change event for a Bluetooth device.
/// </summary>
public record BluetoothConnectionChange
{
    public required string DeviceName { get; init; }
    public required ulong BluetoothAddress { get; init; }
    public required bool IsConnected { get; init; }
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// Monitors Bluetooth device connection status changes using Windows APIs.
/// Provides an observable stream of connection state changes and access to all paired devices.
/// </summary>
public sealed class BluetoothConnectionMonitor : IDisposable
{
    private readonly Subject<BluetoothConnectionChange> _connectionChanges;
    private readonly ConcurrentDictionary<string, TrackedDevice> _trackedDevices = new();
    private readonly DeviceWatcher _deviceWatcher;
    private bool _disposed;
    private bool _started;

    /// <summary>
    /// Observable stream of connection state changes.
    /// </summary>
    public IObservable<BluetoothConnectionChange> ConnectionChanges { get; }

    public BluetoothConnectionMonitor()
    {
        _connectionChanges = new Subject<BluetoothConnectionChange>();
        ConnectionChanges = _connectionChanges.AsObservable();

        // Create a device watcher for paired Bluetooth devices
        string[] requestedProperties =
        [
            "System.Devices.Aep.IsConnected",
            "System.Devices.Aep.DeviceAddress",
            "System.Devices.Aep.IsPresent"
        ];

        var selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
        _deviceWatcher = DeviceInformation.CreateWatcher(
            selector,
            requestedProperties,
            DeviceInformationKind.AssociationEndpoint);

        _deviceWatcher.Added += OnDeviceAdded;
        _deviceWatcher.Updated += OnDeviceUpdated;
        _deviceWatcher.Removed += OnDeviceRemoved;
        _deviceWatcher.Stopped += OnWatcherStopped;
    }

    /// <summary>
    /// Starts monitoring Bluetooth device connections.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) return;

        _started = true;
        _deviceWatcher.Start();
    }

    /// <summary>
    /// Stops monitoring Bluetooth device connections.
    /// </summary>
    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_started) return;

        _started = false;
        if (_deviceWatcher.Status == DeviceWatcherStatus.Started ||
            _deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted)
        {
            _deviceWatcher.Stop();
        }
    }

    /// <summary>
    /// Gets the current connection status for a specific Bluetooth address.
    /// </summary>
    public bool IsConnected(ulong bluetoothAddress)
    {
        return _trackedDevices.Values
            .Any(d => d.BluetoothAddress == bluetoothAddress && d.IsConnected);
    }

    /// <summary>
    /// Gets the current connection status for a device by name (case-insensitive contains).
    /// </summary>
    public bool IsConnectedByName(string namePattern)
    {
        return _trackedDevices.Values
            .Any(d => d.Name.Contains(namePattern, StringComparison.OrdinalIgnoreCase) && d.IsConnected);
    }

    /// <summary>
    /// Gets all paired Bluetooth devices (connected or not).
    /// </summary>
    public IReadOnlyList<PairedBluetoothDevice> GetAllPairedDevices()
    {
        return _trackedDevices.Values
            .Select(d => new PairedBluetoothDevice(d.Name, d.BluetoothAddress, d.IsConnected, d.DeviceClass))
            .ToList();
    }

    /// <summary>
    /// Gets all currently connected devices.
    /// </summary>
    public IReadOnlyList<PairedBluetoothDevice> GetConnectedDevices()
    {
        return _trackedDevices.Values
            .Where(d => d.IsConnected)
            .Select(d => new PairedBluetoothDevice(d.Name, d.BluetoothAddress, d.IsConnected, d.DeviceClass))
            .ToList();
    }

    private async void OnDeviceAdded(DeviceWatcher sender, DeviceInformation deviceInfo)
    {
        if (_disposed) return;

        try
        {
            var device = await BluetoothDevice.FromIdAsync(deviceInfo.Id);
            if (device is null) return;

            var isConnected = GetIsConnected(deviceInfo);
            var deviceClass = GetMajorDeviceClass(device);

            _trackedDevices[deviceInfo.Id] = new TrackedDevice
            {
                Id = deviceInfo.Id,
                Name = device.Name,
                BluetoothAddress = device.BluetoothAddress,
                DeviceClass = deviceClass,
                IsConnected = isConnected,
                Model = AppleDeviceModel.Unknown
            };

            // Emit initial state
            _connectionChanges.OnNext(new BluetoothConnectionChange
            {
                DeviceName = device.Name,
                BluetoothAddress = device.BluetoothAddress,
                IsConnected = isConnected,
                Timestamp = DateTime.Now
            });
        }
        catch
        {
            // Ignore devices we can't access
        }
    }

    private void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (_disposed) return;

        if (!_trackedDevices.TryGetValue(update.Id, out var trackedDevice))
            return;

        var wasConnected = trackedDevice.IsConnected;
        var isNowConnected = GetIsConnected(update);

        if (wasConnected == isNowConnected)
            return; // No change

        trackedDevice.IsConnected = isNowConnected;

        _connectionChanges.OnNext(new BluetoothConnectionChange
        {
            DeviceName = trackedDevice.Name,
            BluetoothAddress = trackedDevice.BluetoothAddress,
            IsConnected = isNowConnected,
            Timestamp = DateTime.Now
        });
    }

    private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (_disposed) return;

        if (_trackedDevices.TryRemove(update.Id, out var trackedDevice) && trackedDevice.IsConnected)
        {
            // Emit disconnection event
            _connectionChanges.OnNext(new BluetoothConnectionChange
            {
                DeviceName = trackedDevice.Name,
                BluetoothAddress = trackedDevice.BluetoothAddress,
                IsConnected = false,
                Timestamp = DateTime.Now
            });
        }
    }

    private void OnWatcherStopped(DeviceWatcher sender, object? args)
    {
        // DeviceWatcher stopped
    }

    private static bool GetIsConnected(DeviceInformation deviceInfo)
    {
        if (deviceInfo.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var value)
            && value is bool isConnected)
        {
            return isConnected;
        }
        return false;
    }

    private static bool GetIsConnected(DeviceInformationUpdate update)
    {
        if (update.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var value)
            && value is bool isConnected)
        {
            return isConnected;
        }
        return false;
    }

    /// <summary>
    /// Gets the major device class from a BluetoothDevice.
    /// </summary>
    private static string GetMajorDeviceClass(BluetoothDevice device)
    {
        try
        {
            return device.ClassOfDevice.MajorClass.ToString();
        }
        catch
        {
            return "Unknown";
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();

        _deviceWatcher.Added -= OnDeviceAdded;
        _deviceWatcher.Updated -= OnDeviceUpdated;
        _deviceWatcher.Removed -= OnDeviceRemoved;
        _deviceWatcher.Stopped -= OnWatcherStopped;

        _connectionChanges.OnCompleted();
        _connectionChanges.Dispose();

        _trackedDevices.Clear();
    }

    private class TrackedDevice
    {
        public required string Id { get; init; }
        public required AppleDeviceModel Model { get; init; }
        public required string Name { get; init; }
        public required ulong BluetoothAddress { get; init; }
        public required string DeviceClass { get; init; }
        public bool IsConnected { get; set; }
    }
}
