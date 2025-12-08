# Current Plan

I seen in the GUI that a device is duplicated in the Paired and Discovered devices, we shoul be able to test this independently of the UI.
Write a test that shows this behaviour so we can explore the root cause.

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
