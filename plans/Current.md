# Plan
## Bugs and issues
- [ ] the minimal airpod window looks ugly and doesn't update like the main window
- [ ] the logic to bring up the minimal window seems flakey
- [ ] If I connect to the airpods with the connect audio button, sometimes Device.IsActive seems to be false
- [ ] The logs seems still verbose and yet not easy to see what's happening

## Improvements
- [ ] Rethink the entire data model, and model each pod independently with its own state (connected, battery, etc)  
      After all we only use the "both" state for audio routing/"is default audio sink"/connecting/and pairing status, everything else can be per-pod. And a pod can have its own UI component and we can show/hide them independently. 
      I am not so sure about the case, and in general the fact that each pod broadcasts information (often stale) about the other pod.

## New Features
- [ ] Implement a feature to allow users to customize the appearance of the minimal airpod window.



## Optional Next Steps

From the completed refactoring plans\Previous.md, these optional enhancements remain:

### Audio Policy Configuration
- [ ] Create `IAudioPolicyProvider` interface with `AutoPausePolicy` enum
- [ ] Implement configurable ear detection behavior

### DI in CLI Projects
- [ ] Update MinimalCLI to use DI instead of manual `new`
- [ ] Create test doubles for `IBleDataProvider`, `IPairedDeviceWatcher`

### Testing
- [ ] Unit tests for `AirPodsStateService` correlation logic
- [ ] Integration tests with fake BLE/paired event sequences

### Audio Route Fallback
- [ ] Add `PreviousAudioOutputId` to `AirPodsState` for reverting audio when disconnecting
