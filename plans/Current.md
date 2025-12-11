# Plan
## Bugs and issues
- [ ] the minimal airpod window looks ugly and doesn't update like the main window
- [ ] the logic to bring up the minimal window seems flakey
- [ ] If I connect to the airpods with the connect audio button, sometimes Device.IsActive seems to be false
- [ ] The logs seems still verbose and yet not easy to see what's happening

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
