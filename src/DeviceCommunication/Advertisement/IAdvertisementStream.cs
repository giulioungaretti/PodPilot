using System;

namespace DeviceCommunication.Advertisement;

/// <summary>
/// Represents a stream of BLE advertisements.
/// This is the lowest layer - it emits raw advertisement data with no grouping or state management.
/// </summary>
public interface IAdvertisementStream : IDisposable
{
    /// <summary>
    /// Observable stream of BLE advertisements as they are received.
    /// </summary>
    IObservable<AdvertisementReceivedData> Advertisements { get; }

    /// <summary>
    /// Starts receiving advertisements.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops receiving advertisements.
    /// </summary>
    void Stop();

    /// <summary>
    /// Gets the current status of the advertisement watcher.
    /// </summary>
    AdvertisementWatcherStatus Status { get; }
}
