using System.Collections.Concurrent;
using DeviceCommunication.Apple;
using DeviceCommunication.Models;
using Microsoft.Extensions.Logging;

namespace DeviceCommunication.Services;

/// <summary>
/// Unified service that combines Windows paired device state with BLE advertisement data.
/// Uses ProductID to match BLE data to paired devices.
/// </summary>
public sealed class AirPodsStateService : IAirPodsStateService
{
    private readonly ILogger<AirPodsStateService> _logger;
    private readonly IPairedDeviceWatcher _pairedDeviceWatcher;
    private readonly IBleDataProvider _bleDataProvider;
    private readonly IDefaultAudioOutputMonitorService _audioOutputMonitor;
    
    private readonly ConcurrentDictionary<ushort, AirPodsState> _stateByProductId = new();
    private readonly ConcurrentDictionary<ushort, OperationState> _operationStates = new();
    private readonly ConcurrentDictionary<ushort, bool> _previousBothInCase = new();
    
    private static readonly TimeSpan OperationLockoutPeriod = TimeSpan.FromSeconds(3);
    private bool _disposed;
    private bool _isRunning;

    public event EventHandler<AirPodsStateChangedEventArgs>? StateChanged;
    public event EventHandler<AirPodsState>? PairedDeviceNeedsAttention;
    public event EventHandler<AirPodsState>? AirPodsRemovedFromCase;

    public AirPodsStateService(
        ILogger<AirPodsStateService> logger,
        IPairedDeviceWatcher pairedDeviceWatcher,
        IBleDataProvider bleDataProvider,
        IDefaultAudioOutputMonitorService audioOutputMonitor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pairedDeviceWatcher = pairedDeviceWatcher ?? throw new ArgumentNullException(nameof(pairedDeviceWatcher));
        _bleDataProvider = bleDataProvider ?? throw new ArgumentNullException(nameof(bleDataProvider));
        _audioOutputMonitor = audioOutputMonitor ?? throw new ArgumentNullException(nameof(audioOutputMonitor));
    }

    public async Task StartAsync()
    {
        if (_isRunning) return;
        _isRunning = true;

        LogDebug("Starting AirPodsStateService...");

        // Subscribe to events
        _pairedDeviceWatcher.DeviceChanged += OnPairedDeviceChanged;
        _pairedDeviceWatcher.EnumerationCompleted += OnEnumerationCompleted;
        _bleDataProvider.DataReceived += OnBleDataReceived;
        _audioOutputMonitor.DefaultAudioOutputChanged += OnAudioOutputChanged;

        // Start watching paired devices first (source of truth)
        await _pairedDeviceWatcher.StartAsync();
        
        // Then start BLE scanning (enrichment)
        _bleDataProvider.Start();
        
        // Audio monitor should already be running

        LogDebug("AirPodsStateService started");
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;

        _pairedDeviceWatcher.DeviceChanged -= OnPairedDeviceChanged;
        _pairedDeviceWatcher.EnumerationCompleted -= OnEnumerationCompleted;
        _bleDataProvider.DataReceived -= OnBleDataReceived;
        _audioOutputMonitor.DefaultAudioOutputChanged -= OnAudioOutputChanged;

        _pairedDeviceWatcher.Stop();
        _bleDataProvider.Stop();

        LogDebug("AirPodsStateService stopped");
    }

    public IReadOnlyList<AirPodsState> GetAllDevices()
    {
        return _stateByProductId.Values.ToList();
    }

    public IReadOnlyList<AirPodsState> GetPairedDevices()
    {
        return _stateByProductId.Values.Where(s => s.IsPaired).ToList();
    }

    public AirPodsState? GetDevice(ushort productId)
    {
        return _stateByProductId.TryGetValue(productId, out var state) ? state : null;
    }

    public void BeginOperation(ushort productId)
    {
        _operationStates[productId] = new OperationState { IsInProgress = true };
        LogDebug($"Operation started for ProductId 0x{productId:X4}");
    }

    public void EndOperation(ushort productId, bool success, bool isConnected, bool isAudioConnected)
    {
        _operationStates[productId] = new OperationState 
        { 
            IsInProgress = false, 
            CompletedAt = DateTime.UtcNow 
        };

        if (success && _stateByProductId.TryGetValue(productId, out var currentState))
        {
            var updatedState = currentState with
            {
                IsConnected = isConnected,
                IsAudioConnected = isAudioConnected
            };
            _stateByProductId[productId] = updatedState;
            RaiseStateChanged(updatedState, AirPodsStateChangeReason.ConnectionChanged);
        }

        LogDebug($"Operation completed for ProductId 0x{productId:X4}: success={success}, connected={isConnected}");
    }

    #region Paired Device Events (Source of Truth)

    private void OnPairedDeviceChanged(object? sender, PairedDeviceChangedEventArgs args)
    {
        var device = args.Device;
        LogDebug($"Paired device changed: {device.Name} ({args.ChangeType})");

        switch (args.ChangeType)
        {
            case PairedDeviceChangeType.Added:
                HandlePairedDeviceAdded(device);
                break;
            case PairedDeviceChangeType.Updated:
                HandlePairedDeviceUpdated(device);
                break;
            case PairedDeviceChangeType.Removed:
                HandlePairedDeviceRemoved(device);
                break;
        }
    }

    private void OnEnumerationCompleted(object? sender, EventArgs e)
    {
        LogDebug("Initial paired device enumeration completed");
        
        // Raise events for all initially enumerated devices
        foreach (var device in _pairedDeviceWatcher.GetPairedDevices())
        {
            var state = CreateStateFromPairedDevice(device, null);
            _stateByProductId[device.ProductId] = state;
            RaiseStateChanged(state, AirPodsStateChangeReason.InitialEnumeration);
        }
    }

    private void HandlePairedDeviceAdded(PairedDeviceInfo device)
    {
        var bleData = _bleDataProvider.GetLatestData(device.ProductId);
        var state = CreateStateFromPairedDevice(device, bleData);
        
        _stateByProductId[device.ProductId] = state;
        RaiseStateChanged(state, AirPodsStateChangeReason.PairedDeviceAdded);
    }

    private void HandlePairedDeviceUpdated(PairedDeviceInfo device)
    {
        if (IsInLockoutPeriod(device.ProductId))
        {
            LogDebug($"Ignoring update for {device.Name} (in lockout period)");
            return;
        }

        if (_stateByProductId.TryGetValue(device.ProductId, out var currentState))
        {
            var updatedState = currentState with
            {
                IsConnected = device.IsConnected,
                PairedDeviceId = device.Id,
                BluetoothAddress = device.Address,
                Name = device.Name
            };
            
            _stateByProductId[device.ProductId] = updatedState;
            RaiseStateChanged(updatedState, AirPodsStateChangeReason.ConnectionChanged);
        }
    }

    private void HandlePairedDeviceRemoved(PairedDeviceInfo device)
    {
        if (_stateByProductId.TryRemove(device.ProductId, out var removedState))
        {
            RaiseStateChanged(removedState, AirPodsStateChangeReason.PairedDeviceRemoved);
        }
    }

    #endregion

    #region BLE Data Events (Enrichment)

    private void OnBleDataReceived(object? sender, BleEnrichmentData bleData)
    {
        var productId = bleData.ProductId;

        // Detect "removed from case" transition
        DetectRemovedFromCase(productId, bleData);

        if (_stateByProductId.TryGetValue(productId, out var currentState))
        {
            // Paired device - enrich with BLE data
            if (IsInLockoutPeriod(productId))
            {
                // Skip connection state updates during lockout
                return;
            }

            var enrichedState = EnrichStateWithBleData(currentState, bleData);
            _stateByProductId[productId] = enrichedState;
            RaiseStateChanged(enrichedState, AirPodsStateChangeReason.BleDataUpdated);

            // Check if paired but not connected - may need attention
            if (currentState.IsPaired && !currentState.IsConnected)
            {
                PairedDeviceNeedsAttention?.Invoke(this, enrichedState);
            }
        }
        else
        {
            // Unpaired device seen via BLE only
            var unpairedState = CreateStateFromBleData(bleData);
            _stateByProductId[productId] = unpairedState;
            RaiseStateChanged(unpairedState, AirPodsStateChangeReason.UnpairedDeviceSeen);
        }
    }

    private void DetectRemovedFromCase(ushort productId, BleEnrichmentData bleData)
    {
        var wasBothInCase = _previousBothInCase.GetValueOrDefault(productId, true);
        var isBothInCase = bleData.IsBothPodsInCase;

        _previousBothInCase[productId] = isBothInCase;

        // Detect transition: both in case ? not both in case
        if (wasBothInCase && !isBothInCase)
        {
            if (_stateByProductId.TryGetValue(productId, out var state))
            {
                LogDebug($"AirPods removed from case: {state.Name}");
                var updatedState = EnrichStateWithBleData(state, bleData);
                RaiseStateChanged(updatedState, AirPodsStateChangeReason.RemovedFromCase);
                AirPodsRemovedFromCase?.Invoke(this, updatedState);
            }
        }
    }

    #endregion

    #region Audio Output Events

    private async void OnAudioOutputChanged(object? sender, DefaultAudioOutputChangedEventArgs args)
    {
        // Check each known device to see if it became the default audio output
        foreach (var state in _stateByProductId.Values.Where(s => s.IsPaired && s.BluetoothAddress.HasValue))
        {
            try
            {
                var isDefault = await Win32BluetoothConnector.IsDefaultAudioOutputAsync(state.BluetoothAddress!.Value);
                
                if (state.IsAudioConnected != isDefault)
                {
                    var updatedState = state with 
                    { 
                        IsAudioConnected = isDefault,
                        IsConnected = isDefault || state.IsConnected // If audio connected, definitely connected
                    };
                    _stateByProductId[state.ProductId] = updatedState;
                    RaiseStateChanged(updatedState, AirPodsStateChangeReason.AudioOutputChanged);
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error checking audio output: {ex.Message}");
            }
        }
    }

    #endregion

    #region Helpers

    private static AirPodsState CreateStateFromPairedDevice(PairedDeviceInfo device, BleEnrichmentData? bleData)
    {
        var model = AppleDeviceModelHelper.GetModel(device.ProductId);
        
        return new AirPodsState
        {
            ProductId = device.ProductId,
            PairedDeviceId = device.Id,
            BluetoothAddress = device.Address,
            BleAddress = bleData?.BleAddress,
            Name = device.Name,
            ModelName = model.GetDisplayName(),
            IsPaired = true,
            IsConnected = device.IsConnected,
            IsAudioConnected = false, // Will be updated by audio output monitor
            LeftBattery = bleData?.LeftBattery,
            RightBattery = bleData?.RightBattery,
            CaseBattery = bleData?.CaseBattery,
            IsLeftCharging = bleData?.IsLeftCharging ?? false,
            IsRightCharging = bleData?.IsRightCharging ?? false,
            IsCaseCharging = bleData?.IsCaseCharging ?? false,
            IsLeftInEar = bleData?.IsLeftInEar ?? false,
            IsRightInEar = bleData?.IsRightInEar ?? false,
            IsLidOpen = bleData?.IsLidOpen ?? false,
            IsBothPodsInCase = bleData?.IsBothPodsInCase ?? true,
            SignalStrength = bleData?.SignalStrength ?? 0,
            LastBleUpdate = bleData?.LastUpdate,
            LastSeen = bleData?.LastUpdate ?? DateTime.Now
        };
    }

    private static AirPodsState CreateStateFromBleData(BleEnrichmentData bleData)
    {
        return new AirPodsState
        {
            ProductId = bleData.ProductId,
            PairedDeviceId = null,
            BluetoothAddress = null,
            BleAddress = bleData.BleAddress,
            Name = bleData.ModelName, // Use model name since not paired
            ModelName = bleData.ModelName,
            IsPaired = false,
            IsConnected = false,
            IsAudioConnected = false,
            LeftBattery = bleData.LeftBattery,
            RightBattery = bleData.RightBattery,
            CaseBattery = bleData.CaseBattery,
            IsLeftCharging = bleData.IsLeftCharging,
            IsRightCharging = bleData.IsRightCharging,
            IsCaseCharging = bleData.IsCaseCharging,
            IsLeftInEar = bleData.IsLeftInEar,
            IsRightInEar = bleData.IsRightInEar,
            IsLidOpen = bleData.IsLidOpen,
            IsBothPodsInCase = bleData.IsBothPodsInCase,
            SignalStrength = bleData.SignalStrength,
            LastBleUpdate = bleData.LastUpdate,
            LastSeen = bleData.LastUpdate
        };
    }

    private static AirPodsState EnrichStateWithBleData(AirPodsState state, BleEnrichmentData bleData)
    {
        return state with
        {
            BleAddress = bleData.BleAddress,
            LeftBattery = bleData.LeftBattery,
            RightBattery = bleData.RightBattery,
            CaseBattery = bleData.CaseBattery,
            IsLeftCharging = bleData.IsLeftCharging,
            IsRightCharging = bleData.IsRightCharging,
            IsCaseCharging = bleData.IsCaseCharging,
            IsLeftInEar = bleData.IsLeftInEar,
            IsRightInEar = bleData.IsRightInEar,
            IsLidOpen = bleData.IsLidOpen,
            IsBothPodsInCase = bleData.IsBothPodsInCase,
            SignalStrength = bleData.SignalStrength,
            LastBleUpdate = bleData.LastUpdate,
            LastSeen = bleData.LastUpdate
        };
    }

    private bool IsInLockoutPeriod(ushort productId)
    {
        if (!_operationStates.TryGetValue(productId, out var opState))
            return false;

        if (opState.IsInProgress)
            return true;

        if (opState.CompletedAt.HasValue)
        {
            var elapsed = DateTime.UtcNow - opState.CompletedAt.Value;
            return elapsed < OperationLockoutPeriod;
        }

        return false;
    }

    private void RaiseStateChanged(AirPodsState state, AirPodsStateChangeReason reason)
    {
        StateChanged?.Invoke(this, new AirPodsStateChangedEventArgs
        {
            State = state,
            Reason = reason
        });
    }

    private void LogDebug(string message) => _logger.LogDebug("{Message}", message);

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _stateByProductId.Clear();
        _operationStates.Clear();
        _previousBothInCase.Clear();
    }

    private sealed class OperationState
    {
        public bool IsInProgress { get; init; }
        public DateTime? CompletedAt { get; init; }
    }
}
