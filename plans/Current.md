# Current Plan

No active plan. Previous work archived to [2025-12-08_architecture-refactoring-complete.md](2025-12-08_architecture-refactoring-complete.md).

## Optional Next Steps

From the completed refactoring, these optional enhancements remain:

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
