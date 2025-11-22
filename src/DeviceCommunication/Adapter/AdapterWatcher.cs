// Bluetooth adapter state change monitoring

using Windows.Devices.Radios;

namespace DeviceCommunication.Adapter;

/// <summary>
/// Monitors and reports changes to the Bluetooth adapter's state.
/// </summary>
/// <remarks>
/// <para>
/// This class provides real-time notifications when the Bluetooth adapter is turned on or off.
/// It's useful for applications that need to respond to changes in Bluetooth availability.
/// </para>
/// <para>
/// This class implements <see cref="IDisposable"/> to properly clean up event subscriptions.
/// Always call <see cref="Dispose"/> or use a <c>using</c> statement to prevent memory leaks.
/// </para>
/// <para>
/// To use this class:
/// 1. Create an instance (preferably with a <c>using</c> statement)
/// 2. Subscribe to the <see cref="StateChanged"/> event
/// 3. Call <see cref="Start"/> to begin monitoring
/// 4. Call <see cref="Dispose"/> when done (automatic with <c>using</c>)
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Recommended: using statement for automatic disposal
/// using var watcher = new AdapterWatcher();
/// watcher.StateChanged += (sender, state) => 
///     Console.WriteLine($"Adapter is now {state}");
/// watcher.Start();
/// Console.ReadLine();
/// // Automatically disposed here
/// </code>
/// </example>
public class AdapterWatcher : IDisposable
{
    private Radio? _radio;
    private AdapterState _currentState;
    private bool _disposed;

    /// <summary>
    /// Occurs when the Bluetooth adapter state changes.
    /// </summary>
    public event EventHandler<AdapterState>? StateChanged;

    /// <summary>
    /// Gets the current state of the Bluetooth adapter.
    /// </summary>
    /// <value>The current <see cref="AdapterState"/>.</value>
    public AdapterState State => _radio != null ? GetStateFromRadio(_radio) : AdapterUtils.GetAdapterState();

    /// <summary>
    /// Starts monitoring the Bluetooth adapter for state changes.
    /// </summary>
    /// <remarks>
    /// If no Bluetooth adapter is available, this method returns without throwing an exception.
    /// Subsequent calls to <see cref="Start"/> while already started will have no effect.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_radio != null) return; // Already started

        var radio = AdapterUtils.GetBluetoothAdapterRadio();
        if (radio == null) return;

        _currentState = GetStateFromRadio(radio);
        _radio = radio;

        _radio.StateChanged += OnRadioStateChanged;
    }

    /// <summary>
    /// Stops monitoring the Bluetooth adapter and releases associated resources.
    /// </summary>
    /// <remarks>
    /// It's safe to call this method multiple times. Calling <see cref="Stop"/> when not started has no effect.
    /// </remarks>
    public void Stop()
    {
        if (_radio != null)
        {
            _radio.StateChanged -= OnRadioStateChanged;
            _radio = null;
        }
    }

    /// <summary>
    /// Event handler for radio state changes.
    /// </summary>
    private void OnRadioStateChanged(Radio sender, object args)
    {
        if (_disposed) return;

        var newState = GetStateFromRadio(sender);
        if (newState == _currentState) return;

        _currentState = newState;
        OnStateChanged(newState);
    }

    /// <summary>
    /// Raises the <see cref="StateChanged"/> event.
    /// </summary>
    /// <param name="state">The new adapter state.</param>
    /// <remarks>
    /// Exceptions from event handlers are logged to the console and do not prevent other handlers from executing.
    /// </remarks>
    protected virtual void OnStateChanged(AdapterState state)
    {
        if (_disposed) return;

        var handler = StateChanged;
        if (handler != null)
        {
            foreach (EventHandler<AdapterState> subscriber in handler.GetInvocationList())
            {
                try
                {
                    subscriber(this, state);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[AdapterWatcher] Exception in StateChanged handler: {ex}");
                }
            }
        }
    }

    /// <summary>
    /// Converts a Radio state to an AdapterState.
    /// </summary>
    private static AdapterState GetStateFromRadio(Radio radio)
    {
        return radio.State == RadioState.On ? AdapterState.On : AdapterState.Off;
    }

    #region IDisposable Implementation

    /// <summary>
    /// Releases all resources used by the <see cref="AdapterWatcher"/>.
    /// </summary>
    /// <remarks>
    /// This method unsubscribes from all events and releases the Radio instance.
    /// After calling this method, the object should not be used.
    /// </remarks>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="AdapterWatcher"/> 
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
            // Dispose managed resources
            Stop(); // Unsubscribe from Radio.StateChanged
            StateChanged = null; // Clear all event subscribers
        }

        // No unmanaged resources to release in this class

        _disposed = true;
    }

    #endregion
}
