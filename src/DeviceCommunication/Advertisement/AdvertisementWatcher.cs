using Windows.Devices.Bluetooth.Advertisement;

namespace DeviceCommunication.Advertisement
{
    /// <summary>
    /// Windows implementation of <see cref="IAdvertisementWatcher"/>.
    /// Wraps the Windows BLE advertisement watcher.
    /// </summary>
    public class AdvertisementWatcher : IAdvertisementWatcher
    {
        private readonly BluetoothLEAdvertisementWatcher _watcher;
        private bool _disposed;

        public event EventHandler<AdvertisementReceivedData>? AdvertisementReceived;
        public event EventHandler? Stopped;

        public AdvertisementWatcher()
        {
            _watcher = new BluetoothLEAdvertisementWatcher();
            _watcher.Received += OnAdvertisementReceived;
            _watcher.Stopped += OnWatcherStopped;
        }

        public void Start()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _watcher.Start();
        }

        public void Stop()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _watcher.Stop();
        }

        public AdvertisementWatcherStatus Status =>
            _watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started
                ? AdvertisementWatcherStatus.Started
                : AdvertisementWatcherStatus.Stopped;

        public void SetFilter(BluetoothLEAdvertisementFilter filter)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(filter);
            _watcher.AdvertisementFilter = filter;
        }

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

        private void OnWatcherStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
        {
            if (_disposed) return;
            OnStopped();
        }

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

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

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

                _watcher.Received -= OnAdvertisementReceived;
                _watcher.Stopped -= OnWatcherStopped;
                AdvertisementReceived = null;
                Stopped = null;
            }

            _disposed = true;
        }
    }
}
