# AirPods Device Deduplication Issue

## Problem Statement

The GUI application shows **two separate entries** for a single pair of AirPods, even though conceptually it should be one device.

## Root Cause Analysis

### Current Implementation
`AirPodsDiscoveryService` uses **Bluetooth address** as the deduplication key:

```csharp
bool isNew = !_discoveredDevices.ContainsKey(data.Address);
_discoveredDevices[data.Address] = deviceInfo;
```

### Real-World AirPods Behavior

**AirPods left and right pods broadcast with DIFFERENT Bluetooth addresses!**

- Left pod broadcasts from address `0xAABBCCDDEEFF`
- Right pod broadcasts from address `0x112233445566`
- Both contain the same model ID (e.g., 0x2014 for AirPods Pro 2)
- Both contain battery data for left, right, and case
- `GetBroadcastSide()` indicates which pod is transmitting

### Why This Happens

1. **Physical Reality**: Each AirPod has its own Bluetooth chip with a unique address
2. **Apple's Design**: Both pods can broadcast independently for redundancy
3. **Current Code**: Uses BLE address as identity ? treats them as separate devices

## Test Coverage

### ? Test 1: Same Address (Ideal Scenario)
```csharp
DeviceDiscoveredOnce_WhenBothPodsAdvertise_WithSameAddress_ShowsSingleDevice()
```
- Tests the IDEAL case where both pods use the same address
- **Passes** ?
- Not realistic for actual AirPods

### ?? Test 2: Different Addresses (Real-World Scenario) 
```csharp
DeviceDiscoveredTwice_WhenBothPodsAdvertise_WithDifferentAddresses_ShowsTwoDevices()
```
- Tests the REAL-WORLD case: different addresses
- **Documents the current behavior** ??
- Shows 2 devices (should be 1)

## Solutions

### Option 1: Device Fingerprinting (Recommended)
Create a composite key based on multiple factors:

```csharp
private string GetDeviceFingerprint(AdvertisementReceivedData data, ProximityPairingMessage airPods)
{
    var model = airPods.GetModel();
    var left = airPods.GetLeftBattery();
    var right = airPods.GetRightBattery();
    var caseB = airPods.GetCaseBattery();
    
    // Pods from the same AirPods will have:
    // - Same model
    // - Same battery levels (within tolerance)
    // - Similar RSSI (within range)
    
    return $"{model}_{left}_{right}_{caseB}";
}
```

**Pros:**
- Works with real AirPods behavior
- Deduplicates conceptually identical devices

**Cons:**
- Battery levels change over time
- Need tolerance for matching
- May still show duplicates briefly when battery updates

### Option 2: Track Address Pairs
Remember that addresses belong to the same device:

```csharp
private Dictionary<ulong, ulong> _addressPairs; // left -> right, right -> left

private bool IsSameDevice(ulong address1, ulong address2)
{
    return _addressPairs.TryGetValue(address1, out var paired) && paired == address2;
}
```

**Pros:**
- Accurate once learned
- Handles address changes

**Cons:**
- Complex state management
- Requires learning phase
- May show duplicates initially

### Option 3: Use Primary Address
Always use the "lowest" or "first seen" address as canonical:

```csharp
private Dictionary<string, ulong> _deviceToCanonicalAddress; // fingerprint -> address

private ulong GetCanonicalAddress(AdvertisementReceivedData data, ProximityPairingMessage airPods)
{
    var fingerprint = GetDeviceFingerprint(data, airPods);
    
    if (!_deviceToCanonicalAddress.TryGetValue(fingerprint, out var canonical))
    {
        _deviceToCanonicalAddress[fingerprint] = data.Address;
        return data.Address;
    }
    
    return canonical;
}
```

**Pros:**
- Clean single-address model
- Stable device identity

**Cons:**
- Arbitrary address selection
- Fingerprinting still needed

### Option 4: Show Both, Indicate Relationship (UI Solution)
Keep showing two entries but visually group them:

```
?? AirPods Pro (2nd gen) - Left Pod
   Battery: L: 80% R: 90% Case: 100%
   
?? AirPods Pro (2nd gen) - Right Pod  [Same Device]
   Battery: L: 80% R: 90% Case: 100%
```

**Pros:**
- No code changes needed in service
- Transparent about what's happening
- Educational for users

**Cons:**
- Cluttered UI
- Doesn't solve the conceptual problem
- Users expect one entry

## Recommendation

**Implement Option 3 (Primary Address with Fingerprinting)**

1. Create device fingerprint from model + battery state
2. Track canonical address for each fingerprint
3. Always use canonical address as dictionary key
4. Update device data from whichever pod broadcasts

This provides the best balance of:
- Accurate deduplication
- Stable device identity
- Reasonable complexity

## Data Collection Needed

To validate the hypothesis, we need to:

1. ? Capture real AirPods advertisements
2. ? Verify they use different addresses
3. ? Measure how often addresses change
4. ? Confirm battery levels match across pods
5. ? Test with multiple AirPods pairs nearby

Use the `AdvertisementCapture` tool with `--all` flag:

```bash
dotnet run --project AdvertisementCapture -- -o airpods_debug.json --all -n 20
```

Then analyze the output to confirm:
- Same model ID from different addresses
- Same battery values
- Different broadcast sides
- Timestamp patterns

## Next Steps

1. [ ] Run enhanced capture session with real AirPods
2. [ ] Analyze captured data for address patterns
3. [ ] Implement recommended solution (Option 3)
4. [ ] Update tests to verify new behavior
5. [ ] Test with multiple AirPods pairs
6. [ ] Document behavior in user-facing docs
