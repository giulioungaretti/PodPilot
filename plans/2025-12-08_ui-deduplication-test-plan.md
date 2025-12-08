# UI Deduplication Test Plan

## Problem
A device appears duplicated in both "Paired Devices" and "Nearby Devices" in the GUI. The service layer deduplicates correctly, but the UI logic populates both lists from overlapping sources, causing paired devices to show up twice.

## Root Cause
- `MainPageViewModel` adds devices from `GetPairedDevices()` to `PairedDevices`.
- It adds devices from `GetAllDevices()` to `DiscoveredDevices`, but `GetAllDevices()` includes paired devices.
- `OnDeviceDiscovered` also adds paired devices to both lists.
- Result: Paired devices appear in both sections.

## Current Test Coverage
- Service-level tests (`AirPodsStateServiceTests.cs`) confirm correct deduplication by `ProductId`.
- No test currently fails for the UI duplication bug, because service tests do not cover ViewModel logic.

## TDD Plan for UI Fix
1. **Add a testable abstraction for DispatcherQueue**
   - Create `IDispatcherWrapper` and a no-op test implementation.
2. **Unit test for MainPageViewModel**
   - Use fakes for `IDeviceStateManager` and `IBluetoothConnectionService`.
   - Simulate one paired device in both `GetPairedDevices()` and `GetAllDevices()`.
   - Assert that after `InitializeAsync()`, the paired device is only in `PairedDevices`, not in `DiscoveredDevices`.
   - This test will fail with current code (documents the bug).
3. **Fix the ViewModel logic**
   - Change population of `DiscoveredDevices` to use `GetAllDevices().Where(d => !d.IsPaired)`.
   - Update event handlers to avoid adding paired devices to `DiscoveredDevices`.
   - Re-run the test; it should now pass.

## Next Steps
- Implement the failing test for `MainPageViewModel`.
- Apply the UI fix and verify the test passes.
- Optionally, review other UI event handlers for similar issues.

---
Created: 2025-12-08
