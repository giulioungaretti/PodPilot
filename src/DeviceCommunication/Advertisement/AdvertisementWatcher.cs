// BLE advertisement scanner

using Windows.Devices.Bluetooth.Advertisement;

namespace DeviceCommunication.Advertisement
{
    /// <summary>
    /// Scans for and reports Bluetooth Low Energy (BLE) advertisements.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class wraps the Windows BLE advertisement watcher and provides a simplified,
    /// event-driven interface for discovering nearby BLE devices.
    /// </para>
    /// <para>
    /// This class implements <see cref="IDisposable"/> to properly clean up event subscriptions.
    /// Always call <see cref="Dispose"/> or use a <c>using</c> statement to prevent memory leaks.
    /// </para>
    /// <para>
    /// Usage pattern:
    /// 1. Create an instance (preferably with a <c>using</c> statement)
    /// 2. Optionally set a filter using <see cref="SetFilter"/>
    /// 3. Subscribe to <see cref="AdvertisementReceived"/> and/or <see cref="Stopped"/> events
    /// 4. Call <see cref="Start"/> to begin scanning
    /// 5. Call <see cref="Dispose"/> when done (automatic with <c>using</c>)
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Recommended: using statement for automatic disposal
    /// using var watcher = new AdvertisementWatcher();
    /// watcher.AdvertisementReceived += (sender, data) =>
    ///     Console.WriteLine($"Found device: {data.Address:X12} (RSSI: {data.Rssi} dBm)");
    /// watcher.Start();
    /// Console.ReadLine();
    /// // Automatically disposed here
    /// </code>
    /// </example>
    public class AdvertisementWatcher : IDisposable
    {
        private readonly BluetoothLEAdvertisementWatcher _watcher;
        private bool _disposed;

        /// <summary>
        /// Occurs when an advertisement is received.
        /// </summary>
        public event EventHandler<AdvertisementReceivedData>? AdvertisementReceived;

        /// <summary>
        /// Occurs when the watcher stops scanning.
        /// </summary>
        public event EventHandler? Stopped;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdvertisementWatcher"/> class.
        /// </summary>
        public AdvertisementWatcher()
        {
            _watcher = new BluetoothLEAdvertisementWatcher();
            _watcher.Received += OnAdvertisementReceived;
            _watcher.Stopped += OnWatcherStopped;
        }

        /// <summary>
        /// Starts scanning for BLE advertisements.
        /// </summary>
        /// <remarks>
        /// Calling this method when already started has no effect.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
        public void Start()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _watcher.Start();
        }

        /// <summary>
        /// Stops scanning for BLE advertisements.
        /// </summary>
        /// <remarks>
        /// Calling this method when already stopped has no effect.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
        public void Stop()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _watcher.Stop();
        }

        /// <summary>
        /// Gets the current status of the watcher.
        /// </summary>
        /// <value>
        /// <see cref="AdvertisementWatcherStatus.Started"/> if actively scanning,
        /// otherwise <see cref="AdvertisementWatcherStatus.Stopped"/>.
        /// </value>
        public AdvertisementWatcherStatus Status =>
            _watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started
                ? AdvertisementWatcherStatus.Started
                : AdvertisementWatcherStatus.Stopped;

        /// <summary>
        /// Sets a filter to limit which advertisements are reported.
        /// </summary>
        /// <param name="filter">The filter to apply to incoming advertisements.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="filter"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
        /// <remarks>
        /// Filters can be used to reduce CPU usage and callback frequency by only
        /// reporting advertisements that match specific criteria (e.g., specific service UUIDs).
        /// </remarks>
        public void SetFilter(BluetoothLEAdvertisementFilter filter)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            _watcher.AdvertisementFilter = filter;
        }

        /// <summary>
        /// Event handler for received advertisements.
        /// </summary>
        private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            if (_disposed) return;
            
            try
            {
                var data = AdvertisementReceivedData.FromEventArgs(args);
                OnAdvertisementReceived(data);
            }
            catch
            {
                // Ignore parsing errors to prevent crashes from malformed advertisements
            }
        }

        /// <summary>
        /// Event handler for watcher stopped events.
        /// </summary>
        private void OnWatcherStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
        {
            if (_disposed) return;
            OnStopped();
        }

        /// <summary>
        /// Raises the <see cref="AdvertisementReceived"/> event.
        /// </summary>
        /// <param name="data">The advertisement data.</param>
        /// <remarks>
        /// Exceptions from event handlers are logged to the console and do not prevent other handlers from executing.
        /// </remarks>
        protected virtual void OnAdvertisementReceived(AdvertisementReceivedData data)
        {
            if (_disposed) return;
            
            var handler = AdvertisementReceived;
            if (handler != null)
            {
                foreach (EventHandler<AdvertisementReceivedData> subscriber in handler.GetInvocationList())
                {
                    try
                    {
                        subscriber(this, data);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[AdvertisementWatcher] Exception in AdvertisementReceived handler: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// Raises the <see cref="Stopped"/> event.
        /// </summary>
        /// <remarks>
        /// Exceptions from event handlers are logged to the console and do not prevent other handlers from executing.
        /// </remarks>
        protected virtual void OnStopped()
        {
            if (_disposed) return;
            
            var handler = Stopped;
            if (handler != null)
            {
                foreach (EventHandler subscriber in handler.GetInvocationList())
                {
                    try
                    {
                        subscriber(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[AdvertisementWatcher] Exception in Stopped handler: {ex}");
                    }
                }
            }
        }

        #region IDisposable Implementation

        /// <summary>
        /// Releases all resources used by the <see cref="AdvertisementWatcher"/>.
        /// </summary>
        /// <remarks>
        /// This method unsubscribes from all events and stops the watcher.
        /// After calling this method, the object should not be used.
        /// </remarks>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="AdvertisementWatcher"/> 
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
                // Stop the watcher
                try
                {
                    _watcher.Stop();
                }
                catch
                {
                    // Ignore exceptions during disposal
                }

                // Unsubscribe from Windows events
                _watcher.Received -= OnAdvertisementReceived;
                _watcher.Stopped -= OnWatcherStopped;

                // Clear our event subscribers
                AdvertisementReceived = null;
                Stopped = null;
            }

            // No unmanaged resources to release in this class

            _disposed = true;
        }

        #endregion
    }
}
