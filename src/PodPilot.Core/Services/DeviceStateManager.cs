using Microsoft.Extensions.Logging;
using PodPilot.Core.Models;

namespace PodPilot.Core.Services;

/// <summary>
/// UI-layer device state manager that wraps <see cref="IAirPodsStateService"/> 
/// and marshals events to the UI thread via <see cref="IDispatcherService"/>.
/// </summary>
public sealed class DeviceStateManager : IDeviceStateManager
{
    private readonly ILogger<DeviceStateManager> _logger;
    private readonly IAirPodsStateService _stateService;
    private readonly IDispatcherService _dispatcher;
    private readonly object _eventLock = new();
    private bool _disposed;

    /// <summary>
    /// Minimum time between state change events for the same device.
    /// Prevents UI thrashing from rapid BLE updates.
    /// </summary>
    private static readonly TimeSpan MinEventInterval = TimeSpan.FromMilliseconds(250);

    private readonly Dictionary<ushort, DateTime> _lastEventTime = new();

    public event EventHandler<DeviceStateChangedEventArgs>? DeviceStateChanged;
    public event EventHandler<AirPodsState>? DeviceDiscovered;
    public event EventHandler<AirPodsState>? PairedDeviceNeedsAttention;

    public DeviceStateManager(
        ILogger<DeviceStateManager> logger,
        IDispatcherService dispatcher,
        IAirPodsStateService stateService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));

        // Subscribe to state service events
        _stateService.StateChanged += OnStateChanged;
        _stateService.PairedDeviceNeedsAttention += OnPairedDeviceNeedsAttention;
    }

    public async Task StartAsync()
    {
        await _stateService.StartAsync();
    }

    public void Stop()
    {
        _stateService.Stop();
    }

    public AirPodsState? GetDevice(ushort productId)
    {
        return _stateService.GetDevice(productId);
    }

    public IReadOnlyList<AirPodsState> GetAllDevices()
    {
        return _stateService.GetAllDevices();
    }

    public IReadOnlyList<AirPodsState> GetPairedDevices()
    {
        return _stateService.GetPairedDevices();
    }

    public void BeginConnectionOperation(ushort productId)
    {
        _stateService.BeginOperation(productId);
    }

    public void EndConnectionOperation(ushort productId, bool success, bool isConnected, bool isAudioConnected)
    {
        _stateService.EndOperation(productId, success, isConnected, isAudioConnected);
    }

    private void OnStateChanged(object? sender, AirPodsStateChangedEventArgs args)
    {
        var state = args.State;
        var reason = args.Reason;

        // Check if this is a new device discovery
        var isDiscovery = reason == AirPodsStateChangeReason.InitialEnumeration
            || reason == AirPodsStateChangeReason.PairedDeviceAdded
            || reason == AirPodsStateChangeReason.UnpairedDeviceSeen;

        // Debounce BLE updates only
        if (reason == AirPodsStateChangeReason.BleDataUpdated)
        {
            var now = DateTime.UtcNow;
            lock (_eventLock)
            {
                if (_lastEventTime.TryGetValue(state.ProductId, out var lastTime))
                {
                    if (now - lastTime < MinEventInterval)
                    {
                        return; // Skip this update
                    }
                }
                _lastEventTime[state.ProductId] = now;
            }
        }

        _dispatcher.TryEnqueue(() =>
        {
            if (isDiscovery)
            {
                DeviceDiscovered?.Invoke(this, state);
            }

            DeviceStateChanged?.Invoke(this, new DeviceStateChangedEventArgs
            {
                State = state,
                Reason = reason
            });
        });
    }

    private void OnPairedDeviceNeedsAttention(object? sender, AirPodsState state)
    {
        _dispatcher.TryEnqueue(() =>
        {
            PairedDeviceNeedsAttention?.Invoke(this, state);
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _stateService.StateChanged -= OnStateChanged;
        _stateService.PairedDeviceNeedsAttention -= OnPairedDeviceNeedsAttention;

        _lastEventTime.Clear();
    }
}
