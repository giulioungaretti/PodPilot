using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DeviceCommunication.Advertisement;

/// <summary>
/// Observable stream wrapper around <see cref="AdvertisementWatcher"/>.
/// Converts event-based API to reactive observable pattern.
/// </summary>
public class AdvertisementStream : IAdvertisementStream
{
    private readonly IAdvertisementWatcher _watcher;
    private readonly Subject<AdvertisementReceivedData> _advertisementSubject;
    private bool _disposed;

    public IObservable<AdvertisementReceivedData> Advertisements { get; }

    public AdvertisementWatcherStatus Status => _watcher.Status;

    public AdvertisementStream() : this(new AdvertisementWatcher())
    {
    }

    public AdvertisementStream(IAdvertisementWatcher watcher)
    {
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        _advertisementSubject = new Subject<AdvertisementReceivedData>();
        Advertisements = _advertisementSubject.AsObservable();

        _watcher.AdvertisementReceived += OnAdvertisementReceived;
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

    private void OnAdvertisementReceived(object? sender, AdvertisementReceivedData data)
    {
        if (_disposed) return;
        
        try
        {
            _advertisementSubject.OnNext(data);
        }
        catch (Exception ex)
        {
            _advertisementSubject.OnError(ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _watcher.AdvertisementReceived -= OnAdvertisementReceived;
        
        _advertisementSubject.OnCompleted();
        _advertisementSubject.Dispose();
        
        _watcher.Dispose();
    }
}
