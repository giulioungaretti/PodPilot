# Advertisement Capture Tool

A utility for capturing real Bluetooth Low Energy (BLE) advertisements and saving them for testing purposes.

## Features

- ?? **Real-time BLE scanning** - Captures live advertisements from nearby devices
- ?? **Apple device filtering** - Focus on AirPods and other Apple devices
- ?? **JSON export** - Save captured data in structured JSON format
- ?? **Test data generation** - Auto-generates C# code snippets for unit tests
- ?? **Detailed parsing** - Decodes AirPods battery, charging, and ear detection data

## Usage

### Basic Capture
Capture 10 Apple devices (default):
```bash
dotnet run --project AdvertisementCapture
```

### Custom Output File
```bash
dotnet run --project AdvertisementCapture -- -o my_captures.json
```

### Capture More Devices
```bash
dotnet run --project AdvertisementCapture -- --limit 20
```

### Capture All BLE Devices
```bash
dotnet run --project AdvertisementCapture -- --any-device
```

### Capture All Packets (Including Duplicates)
```bash
dotnet run --project AdvertisementCapture -- --all
```

## Command-Line Options

| Option | Description | Default |
|--------|-------------|---------|
| `-o, --output <file>` | Output file path | `captured_advertisements.json` |
| `-n, --limit <number>` | Number of unique devices to capture | `10` |
| `-a, --all` | Capture all packets (including duplicates) | `false` |
| `--any-device` | Capture all BLE devices (not just Apple) | Apple only |
| `-h, --help` | Show help message | - |

## Output Files

The tool generates two files:

### 1. JSON Data File
Contains structured advertisement data:
```json
[
  {
    "timestamp": "2024-01-15T10:30:00",
    "address": 123456789012,
    "rssi": -45,
    "manufacturerData": {
      "76": "07191401142055AAB811000..."
    },
    "appleInfo": {
      "model": "AirPodsPro2",
      "broadcastSide": "Left",
      "leftBattery": 8,
      "rightBattery": 9,
      "caseBattery": 10,
      ...
    }
  }
]
```

### 2. C# Code Snippet File
Ready-to-use test data:
```csharp
// Generated test data from captured advertisements
namespace TestData;

public static class CapturedAdvertisements
{
    // Capture 1: 1A2B3C4D5E6F
    // AirPodsPro2 - Left pod
    public static readonly byte[] Advertisement1_Vendor004C = new byte[]
    {
        0x07, 0x19, 0x01, 0x14, 0x20, 0x55, 0xAA, 0xB8, ...
    };
}
```

## Example Workflow

1. **Capture real AirPods data:**
   ```bash
   dotnet run --project AdvertisementCapture -- -o airpods_test_data.json -n 5
   ```

2. **Open your AirPods case near your PC**

3. **Wait for captures** (Press Ctrl+C when done)

4. **Use generated test data** in your unit tests:
   ```csharp
   // Copy from airpods_test_data.cs into your test file
   var testData = CapturedAdvertisements.Advertisement1_Vendor004C;
   ```

## Tips

- ?? **Keep devices close** - Better signal strength (-50 dBm or higher)
- ?? **Open AirPods case** - Triggers continuous advertisements
- ?? **Wait a few seconds** - Advertisements may not be immediate
- ?? **Both pods broadcast** - You'll see advertisements from left and right pods

## Use Cases

- ? Creating realistic test data for unit tests
- ? Debugging BLE advertisement parsing
- ? Analyzing AirPods broadcast patterns
- ? Testing device deduplication logic
- ? Documenting real-world advertisement formats

## Requirements

- Windows 10/11 with Bluetooth LE support
- .NET 8 or higher
- Physical Bluetooth adapter

## Example Output

```
=== BLE Advertisement Capture Tool ===

Configuration:
  Output file: captured_advertisements.json
  Capture all packets: False
  Apple devices only: True
  Capture limit: 10 devices

Starting capture... (Press Ctrl+C to stop and save)

[1] Captured: 1A2B3C4D5E6F (RSSI: -45 dBm)
    Model: AirPodsPro2
    Broadcast: Left pod
    Battery: L:80% R:90% Case:100%

[2] Captured: 1A2B3C4D5E6F (RSSI: -46 dBm)
    Model: AirPodsPro2
    Broadcast: Right pod
    Battery: L:80% R:90% Case:100%

^C
Capture complete! Saved 2 advertisements to 'captured_advertisements.json'
Also saved C# test data snippet to 'captured_advertisements.cs'
```
