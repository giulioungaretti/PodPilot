# next tasks


## Performance Recommendations for PodPilot

This part contains performance and simplification recommendations identified during code analysis.

## ?? High Priority

### 1. ? Cache Paired Device Lookup (COMPLETED)

**Location**: `DeviceCommunication/Services/PairedDeviceLookupService.cs`

**Solution Implemented**:
- Created `IPairedDeviceLookupService` interface with `FindByProductIdAsync` and `InvalidateCache` methods
- Created `PairedDeviceLookupService` with 30-second cache expiration
- Thread-safe cache access using `SemaphoreSlim`
- Null results are cached to avoid repeated lookups for unpaired devices
- Updated `SimpleAirPodsDiscoveryService` to use the new service via DI

**Impact**: Major performance improvement - reduces device enumeration from 10+/second to once per 30 seconds per Product ID.

---

### 2. Fix Timer-Based Polling with Fire-and-Forget Async

**Location**: `MainPageViewModel.OnCleanupTimerTick`

**Problem**:
- Runs every 1 second
- Calls `RefreshDefaultAudioOutputStatusAsync` which queries Windows audio APIs
- Fire-and-forget pattern (`_ = device.RefreshDefaultAudioOutputStatusAsync()`) can cause race conditions

**Solution**:

```csharp
private readonly TimeSpan _cleanupInterval = TimeSpan.FromSeconds(5); // Increase from 1s to 5s
private bool _isRefreshing;

private async void OnCleanupTimerTick(DispatcherQueueTimer sender, object args)
{
    if (_isRefreshing) return; // Prevent overlapping executions
    _isRefreshing = true;
    
    try
    {
        var now = DateTime.Now;
        var devicesToRemove = new List<AirPodsDeviceViewModel>();

        foreach (var device in DiscoveredDevices)
        {
            var timeSinceLastSeen = (now - device.LastSeen).TotalSeconds;
            
            if (timeSinceLastSeen >= 10)
            {
                devicesToRemove.Add(device);
            }
            else
            {
                device.RefreshIsActive();
                await device.RefreshDefaultAudioOutputStatusAsync(); // Await instead of fire-and-forget
            }
        }

        foreach (var device in devicesToRemove)
        {
            DiscoveredDevices.Remove(device);
        }
        
        HasDiscoveredDevices = DiscoveredDevices.Count > 0;
    }
    finally
    {
        _isRefreshing = false;
    }
}
```

**Alternative**: Use event-based audio output change detection instead of polling:

```csharp
// In App.xaml.cs or a dedicated service
Windows.Media.Devices.MediaDevice.DefaultAudioRenderDeviceChanged += OnDefaultAudioDeviceChanged;
```

---

## ?? Medium Priority

### 3. Add Conditional Debug Logging (PARTIAL ?)

**Location**: `Win32BluetoothConnector` and `SimpleAirPodsDiscoveryService`

**Status**: Implemented in `SimpleAirPodsDiscoveryService` and `PairedDeviceLookupService`. Still needed in `Win32BluetoothConnector`.

**Problem**: 100+ `Debug.WriteLine` calls execute even in release builds, causing string allocations.

**Solution**: Use `ConditionalAttribute`:

```csharp
using System.Diagnostics;

// Add to each class with extensive logging
[Conditional("DEBUG")]
private static void LogDebug(string message) => Debug.WriteLine(message);

// Replace Debug.WriteLine($"...") with:
LogDebug($"...");
```

**Or better**: Use `Microsoft.Extensions.Logging.ILogger` for structured logging with configurable levels.

---

### 4. Simplify ViewModel Property Cascades

**Location**: `AirPodsDeviceViewModel`

**Problem**: Setting `IsConnected` triggers 10+ `PropertyChanged` notifications:

```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(ShowConnectButton))]
[NotifyPropertyChangedFor(nameof(CanConnectButton))]
[NotifyPropertyChangedFor(nameof(ShowPairingWarning))]
// ... 7 more attributes
private bool _isConnected;
```

**Solution**: Consider a state object pattern:

```csharp
public record ConnectionState(bool IsConnected, bool IsConnecting, string? PairedDeviceId)
{
    public bool CanConnect => !IsConnected && !IsConnecting && !string.IsNullOrEmpty(PairedDeviceId);
    public bool ShowConnectButton => !IsConnected && !IsConnecting;
    public bool ShowPairingWarning => string.IsNullOrEmpty(PairedDeviceId) && !IsConnected;
    public string ConnectionButtonText => IsConnected ? "Disconnect" : "Connect";
    // All computed properties in one place
}

// In ViewModel:
[ObservableProperty]
private ConnectionState _connectionState = new(false, false, null);
```

This reduces multiple PropertyChanged events to a single state update.

---

## ?? Low Priority

### 5. Service Coordinator Pattern (PARTIAL ?)

**Status**: Partially addressed by extracting `IPairedDeviceLookupService` as a shared singleton. Full orchestrator pattern not yet implemented.

**Current Architecture**:
```
App.xaml.cs creates:
??? SimpleAirPodsDiscoveryService (shared)
??? BackgroundDeviceMonitoringService (uses shared discovery)
??? EarDetectionService (also uses shared discovery)
??? BluetoothConnectionService
```

**Problem**: Multiple services subscribe to the same discovery service events independently, with no coordination.

**Solution**: Consider a central orchestrator:

```csharp
public class AirPodsOrchestrator : IDisposable
{
    private readonly IAirPodsDiscoveryService _discovery;
    private readonly Dictionary<ushort, AirPodsDeviceState> _devices = new();
    
    public event Action<AirPodsDeviceState>? DeviceStateChanged;
    
    // Coordinates all device state in one place
    // Other services subscribe to this instead of raw discovery events
}
```

---

## ? Completed

- [x] **#1**: Extracted `IPairedDeviceLookupService` with 30-second caching - eliminates repeated device enumeration on every BLE advertisement
- [x] **#3 (partial)**: Added conditional debug logging to `SimpleAirPodsDiscoveryService` and `PairedDeviceLookupService`
- [x] **#5 (partial)**: Improved service encapsulation - `PairedDeviceLookupService` is now a shared singleton that other services can consume via DI
- [x] Removed redundant discovery services (`AirPodsDiscoveryService`, `GroupedAirPodsDiscoveryService`)
- [x] Extracted `GetModelDisplayName` and `GetProductId` to `AppleDeviceModelHelper` extension methods

### New Architecture (after #1 and #5 partial)

```
App.xaml.cs DI Container:
??? IPairedDeviceLookupService (singleton, cached device lookup)
??? IAirPodsDiscoveryService (singleton, uses IPairedDeviceLookupService)
??? BackgroundDeviceMonitoringService (uses IAirPodsDiscoveryService)
??? EarDetectionService (uses IAirPodsDiscoveryService)
??? BluetoothConnectionService
```

**Key Improvements**:
- `PairedDeviceLookupService` caches lookups for 30 seconds, reducing device enumeration from 10+/second to ~once per 30 seconds
- Thread-safe cache access via `SemaphoreSlim`
- Null results are also cached to avoid repeated lookups for unpaired devices
- `InvalidateCache()` method available for when users pair new devices

---

## Implementation Priority

| Priority | Task | Impact | Effort | Status |
|----------|------|--------|--------|--------|
| ?? 1 | Cache paired device lookup | Major perf | Low | ? Done |
| ?? 2 | Fix timer interval + async | Medium perf | Low | ? Pending |
| ?? 3 | Conditional debug logging | Minor perf | Low | ? Partial |
| ?? 4 | ViewModel state simplification | Code clarity | Medium | ? Pending |
| ?? 5 | Service coordinator pattern | Architecture | High | ? Partial |

