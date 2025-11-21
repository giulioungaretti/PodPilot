// Bluetooth device management

using DeviceCommunication.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using BluetoothError = DeviceCommunication.Core.BluetoothError;

namespace DeviceCommunication.Device;

/// <summary>
/// Represents a Bluetooth device and provides access to its properties and state.
/// </summary>
/// <remarks>
/// <para>
/// This class wraps a Windows <see cref="BluetoothDevice"/> and provides a simplified
/// interface for monitoring device connection state, retrieving device properties,
/// and subscribing to device events.
/// </para>
/// <para>
/// This class implements <see cref="IDisposable"/> to properly clean up event subscriptions
/// and dispose the underlying <see cref="BluetoothDevice"/>.
/// Always call <see cref="Dispose"/> or use a <c>using</c> statement to prevent memory leaks.
/// </para>
/// <para>
/// Create instances using the static factory methods:
/// <list type="bullet">
/// <item><description><see cref="FromBluetoothAddressAsync"/> - Create from MAC address</description></item>
/// <item><description><see cref="FromDeviceIdAsync"/> - Create from Windows device ID</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Recommended: using statement for automatic disposal
/// using var device = await Device.FromBluetoothAddressAsync(0x1A2B3C4D5E6F);
/// device.ConnectionStateChanged += (sender, state) =>
///     Console.WriteLine($"Connection: {state}");
/// Console.WriteLine($"Device: {device.GetName()}");
/// // Automatically disposed here
/// </code>
/// </example>
public class Device : IDisposable
{
    private const string PROPERTY_BLUETOOTH_VENDOR_ID = "System.DeviceInterface.Bluetooth.VendorId";
    private const string PROPERTY_BLUETOOTH_PRODUCT_ID = "System.DeviceInterface.Bluetooth.ProductId";
    private const string PROPERTY_AEP_CONTAINER_ID = "System.Devices.Aep.ContainerId";

    private readonly BluetoothDevice _device;
    private bool _disposed;

    /// <summary>
    /// Occurs when the device's connection state changes.
    /// </summary>
    public event EventHandler<DeviceConnectionState>? ConnectionStateChanged;

    /// <summary>
    /// Occurs when the device's name changes.
    /// </summary>
    public event EventHandler<string>? NameChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="Device"/> class.
    /// </summary>
    /// <param name="device">The underlying Windows Bluetooth device.</param>
    private Device(BluetoothDevice device)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        Initialize();
    }

    /// <summary>
    /// Creates a <see cref="Device"/> instance from a Bluetooth MAC address.
    /// </summary>
    /// <param name="address">The 48-bit Bluetooth MAC address.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the device.</returns>
    /// <exception cref="BluetoothException">
    /// Thrown when the device cannot be found or accessed.
    /// </exception>
    /// <example>
    /// <code>
    /// ulong address = 0x1A2B3C4D5E6F;
    /// var device = await Device.FromBluetoothAddressAsync(address);
    /// Console.WriteLine($"Device: {device.GetName()}");
    /// </code>
    /// </example>
    public static async Task<Device> FromBluetoothAddressAsync(ulong address)
    {
        var device = await BluetoothDevice.FromBluetoothAddressAsync(address);
        if (device == null)
            throw new BluetoothException(BluetoothError.DeviceNotFound);
        return new Device(device);
    }

    /// <summary>
    /// Creates a <see cref="Device"/> instance from a Windows device ID.
    /// </summary>
    /// <param name="deviceId">The Windows device identifier string.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the device.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="deviceId"/> is null.</exception>
    /// <exception cref="BluetoothException">
    /// Thrown when the device cannot be found or accessed.
    /// </exception>
    public static async Task<Device> FromDeviceIdAsync(string deviceId)
    {
        if (deviceId == null)
            throw new ArgumentNullException(nameof(deviceId));

        var device = await BluetoothDevice.FromIdAsync(deviceId);
        if (device == null)
            throw new BluetoothException(BluetoothError.DeviceNotFound);
        return new Device(device);
    }

    /// <summary>
    /// Initializes event handlers for the device.
    /// </summary>
    private void Initialize()
    {
        _device.ConnectionStatusChanged += OnConnectionStatusChanged;
        _device.NameChanged += OnNameChanged;
    }

    /// <summary>
    /// Event handler for connection status changes.
    /// </summary>
    private void OnConnectionStatusChanged(BluetoothDevice sender, object args)
    {
        if (_disposed) return;
        
        var state = sender.ConnectionStatus == BluetoothConnectionStatus.Connected
            ? DeviceConnectionState.Connected
            : DeviceConnectionState.Disconnected;
        OnConnectionStateChanged(state);
    }

    /// <summary>
    /// Event handler for device name changes.
    /// </summary>
    private void OnNameChanged(BluetoothDevice sender, object args)
    {
        if (_disposed) return;
        OnNameChanged(sender.Name);
    }

    /// <summary>
    /// Raises the <see cref="ConnectionStateChanged"/> event.
    /// </summary>
    /// <param name="state">The new connection state.</param>
    /// <remarks>
    /// Exceptions from event handlers are logged to the console and do not prevent other handlers from executing.
    /// </remarks>
    protected virtual void OnConnectionStateChanged(DeviceConnectionState state)
    {
        if (_disposed) return;
        
        var handler = ConnectionStateChanged;
        if (handler != null)
        {
            foreach (EventHandler<DeviceConnectionState> subscriber in handler.GetInvocationList())
            {
                try
                {
                    subscriber(this, state);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Device] Exception in ConnectionStateChanged handler: {ex}");
                }
            }
        }
    }

    /// <summary>
    /// Raises the <see cref="NameChanged"/> event.
    /// </summary>
    /// <param name="name">The new device name.</param>
    /// <remarks>
    /// Exceptions from event handlers are logged to the console and do not prevent other handlers from executing.
    /// </remarks>
    protected virtual void OnNameChanged(string name)
    {
        if (_disposed) return;
        
        var handler = NameChanged;
        if (handler != null)
        {
            foreach (EventHandler<string> subscriber in handler.GetInvocationList())
            {
                try
                {
                    subscriber(this, name);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Device] Exception in NameChanged handler: {ex}");
                }
            }
        }
    }

    /// <summary>
    /// Gets the Windows device identifier for this device.
    /// </summary>
    /// <returns>The device ID string.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
    public string GetDeviceId()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _device.DeviceId;
    }

    /// <summary>
    /// Gets the friendly name of the device.
    /// </summary>
    /// <returns>The device name, or "Unknown" if not available.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
    public string GetName()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _device.DeviceInformation?.Name ?? "Unknown";
    }

    /// <summary>
    /// Gets the Bluetooth MAC address of the device.
    /// </summary>
    /// <returns>The 48-bit MAC address as a <see cref="ulong"/>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
    public ulong GetAddress()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _device.BluetoothAddress;
    }

    /// <summary>
    /// Gets detailed device information including system properties.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation. 
    /// The task result contains the <see cref="DeviceInformation"/>.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
    public async Task<DeviceInformation> GetInfoAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        var properties = new[]
        {
            PROPERTY_BLUETOOTH_VENDOR_ID,
            PROPERTY_BLUETOOTH_PRODUCT_ID,
            PROPERTY_AEP_CONTAINER_ID
        };
        return await DeviceInformation.CreateFromIdAsync(_device.DeviceId, properties);
    }

    /// <summary>
    /// Gets the vendor ID (VID) of the device.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation. 
    /// The task result contains the vendor ID.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
    /// <exception cref="BluetoothException">
    /// Thrown when the vendor ID property cannot be retrieved.
    /// </exception>
    public async Task<ushort> GetVendorIdAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        var info = await GetInfoAsync();
        if (info.Properties.TryGetValue(PROPERTY_BLUETOOTH_VENDOR_ID, out var value))
            return (ushort)value;
        throw new BluetoothException(BluetoothError.PropertyNotFound);
    }

    /// <summary>
    /// Gets the product ID (PID) of the device.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation. 
    /// The task result contains the product ID.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
    /// <exception cref="BluetoothException">
    /// Thrown when the product ID property cannot be retrieved.
    /// </exception>
    public async Task<ushort> GetProductIdAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        var info = await GetInfoAsync();
        if (info.Properties.TryGetValue(PROPERTY_BLUETOOTH_PRODUCT_ID, out var value))
            return (ushort)value;
        throw new BluetoothException(BluetoothError.PropertyNotFound);
    }

    /// <summary>
    /// Gets the current connection state of the device.
    /// </summary>
    /// <returns>The current <see cref="DeviceConnectionState"/>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
    public DeviceConnectionState GetConnectionState()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        return _device.ConnectionStatus == BluetoothConnectionStatus.Connected
            ? DeviceConnectionState.Connected
            : DeviceConnectionState.Disconnected;
    }

    /// <summary>
    /// Determines whether the device is currently connected.
    /// </summary>
    /// <returns><c>true</c> if the device is connected; otherwise, <c>false</c>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
    public bool IsConnected()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return GetConnectionState() == DeviceConnectionState.Connected;
    }

    #region IDisposable Implementation

    /// <summary>
    /// Releases all resources used by the <see cref="Device"/>.
    /// </summary>
    /// <remarks>
    /// This method unsubscribes from all events and disposes the underlying <see cref="BluetoothDevice"/>.
    /// After calling this method, the object should not be used.
    /// </remarks>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="Device"/> 
    /// and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources; 
    /// <c>false</c> to release only unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Unsubscribe from Windows events
            if (_device != null)
            {
                _device.ConnectionStatusChanged -= OnConnectionStatusChanged;
                _device.NameChanged -= OnNameChanged;
                _device.Dispose(); // Dispose the underlying Windows device
            }

            // Clear our event subscribers
            ConnectionStateChanged = null;
            NameChanged = null;
        }

        // No unmanaged resources to release in this class

        _disposed = true;
    }

    #endregion
}
