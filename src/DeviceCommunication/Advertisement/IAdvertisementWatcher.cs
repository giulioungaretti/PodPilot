using Windows.Devices.Bluetooth.Advertisement;

namespace DeviceCommunication.Advertisement
{
    /// <summary>
    /// Scans for and reports Bluetooth Low Energy (BLE) advertisements.
    /// </summary>
    /// <remarks>
    /// <para>Usage pattern:</para>
    /// <para>1. Create instance (use <c>using</c> statement)</para>
    /// <para>2. Subscribe to <see cref="AdvertisementReceived"/> and/or <see cref="Stopped"/> events</para>
    /// <para>3. Optionally call <see cref="SetFilter"/> to limit advertisements</para>
    /// <para>4. Call <see cref="Start"/> to begin scanning</para>
    /// <para>5. Call <see cref="Dispose"/> when done (automatic with <c>using</c>)</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// using var watcher = new AdvertisementWatcher();
    /// watcher.AdvertisementReceived += (s, data) => Console.WriteLine($"{data.Address:X12}");
    /// watcher.Start();
    /// </code>
    /// </example>
    public interface IAdvertisementWatcher : IDisposable
    {
        /// <summary>Occurs when an advertisement is received.</summary>
        event EventHandler<AdvertisementReceivedData>? AdvertisementReceived;

        /// <summary>Occurs when the watcher stops scanning.</summary>
        event EventHandler? Stopped;

        /// <summary>Gets the current scanning status.</summary>
        AdvertisementWatcherStatus Status { get; }

        /// <summary>Starts scanning for BLE advertisements.</summary>
        void Start();

        /// <summary>Stops scanning for BLE advertisements.</summary>
        void Stop();

        /// <summary>Sets a filter to limit which advertisements are reported.</summary>
        void SetFilter(BluetoothLEAdvertisementFilter filter);
    }
}
