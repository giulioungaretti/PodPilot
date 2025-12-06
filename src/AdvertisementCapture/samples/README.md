# Captured Advertisement Samples

This directory contains real BLE advertisement data captured using the Advertisement Capture tool.

## Files

### `captured_apple_devices.json`
Real BLE advertisements from Apple devices captured on 2025-12-06.

**Contents:**
- 5 Apple device advertisements (non-AirPods)
- Likely iPhones and other Apple devices nearby
- RSSI values ranging from -66 to -84 dBm
- Various Apple advertisement types (0x10, 0x12 packet types)

**Note:** None of these contained AirPods proximity pairing messages (0x07 packet type).

### `captured_apple_devices.cs`
C# code snippet generated from the captured data, ready for use in tests.

### `RealCapturedData.cs`
Curated collection of real advertisement data:

- **`AirPodsPro2_RealCapture`**: Real AirPods Pro 2 proximity pairing message from CLI example
- **`OtherAppleDevices`**: Collection of non-AirPods Apple device advertisements

## Usage in Tests

```csharp
// Use the real AirPods Pro 2 data
var realAirPodsData = new byte[]
{
    0x07, 0x19, 0x01, 0x14, 0x20, 0x55, 0xAA, 0xB8, 0x11,
    0x00, 0x04, 0x1B, 0xE9, 0xD4, 0x3B, 0xA1, 0x34, 0xD2,
    0x3B, 0x34, 0x24, 0xF0, 0x3D, 0x56, 0xB5, 0xA6, 0x3A
};

var data = new AdvertisementReceivedData
{
    Address = 0xABCDEF123456UL,
    Rssi = -45,
    ManufacturerData = new Dictionary<ushort, byte[]>
    {
        [AppleConstants.VENDOR_ID] = realAirPodsData
    }
};
```

## Capturing Your Own Data

To capture AirPods advertisements:

1. **Open your AirPods case** near your PC
2. **Run the capture tool:**
   ```bash
   dotnet run --project AdvertisementCapture -- -o airpods.json -n 3
   ```
3. **Wait for captures** (typically 2-3 seconds)
4. **Press Ctrl+C** to save

The tool will generate both JSON and C# files with your captured data.

## Advertisement Types Found

### Proximity Pairing (0x07)
AirPods and Beats products broadcast these when:
- Case is opened
- Pods are being worn
- Battery status is available

Format: 27 bytes containing model, battery, charging state, in-ear detection, etc.

### Other Apple Advertisements (0x10, 0x12, etc.)
Various Apple devices broadcast different message types:
- Continuity messages
- Handoff data
- Nearby device info
- iCloud pairing status

These are not AirPods and are correctly filtered out by `AirPodsDiscoveryService`.

## Test Integration

The test file `GUI.Tests/Services/AirPodsDiscoveryServiceTests.cs` includes:

- `RealCapturedData_AirPodsPro2_ParsesCorrectly()` - Uses real captured AirPods data
- Synthetic test data generators for controlled testing scenarios
- Edge cases with real non-AirPods Apple device data

This combination ensures tests cover both realistic scenarios and controlled test cases.
