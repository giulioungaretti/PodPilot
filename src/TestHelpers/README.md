# TestHelpers Project

This project contains shared test helpers and utilities that can be used across multiple test projects in the solution.

## Purpose

The TestHelpers project provides reusable test infrastructure to avoid code duplication between:
- `DeviceCommunication.Tests`
- `GUI.Tests`

## Contents

### AirPodsStateServiceTestHelpers

Static helper class for testing `AirPodsStateService` with factory methods for creating test data and simulating device events.

#### Constants

```csharp
const ushort AirPodsPro2ProductId = 0x2014;
const ushort AirPods2ProductId = 0x200F;
const string TestDeviceName = "Test AirPods Pro";
const ulong TestBluetoothAddress = 0xAABBCCDDEEFF;
const ulong TestBleAddress = 0x112233445566;
```

#### Factory Methods

- **`CreatePairedDevice()`** - Creates a test `PairedDeviceInfo` with customizable properties
- **`CreateBleData()`** - Creates test `BleEnrichmentData` with customizable battery levels, charging states, etc.

#### Event Simulation Methods

- **`RaisePairedDeviceAdded()`** - Simulates a paired device being added
- **`RaisePairedDeviceRemoved()`** - Simulates a paired device being removed
- **`RaisePairedDeviceUpdated()`** - Simulates a paired device being updated
- **`RaiseBleDataReceived()`** - Simulates BLE data being received

## Usage Example

```csharp
using TestHelpers;
using DeviceCommunication.Services;
using NSubstitute;
using Xunit;

public class MyTest : IAsyncLifetime
{
    private readonly IPairedDeviceWatcher _pairedDeviceWatcher;
    private readonly IBleDataProvider _bleDataProvider;
    private readonly AirPodsStateService _sut;

    public MyTest()
    {
        _pairedDeviceWatcher = Substitute.For<IPairedDeviceWatcher>();
        _bleDataProvider = Substitute.For<IBleDataProvider>();
        // ... setup _sut
    }

    [Fact]
    public void MyTest()
    {
        // Create test data with defaults
        var device = AirPodsStateServiceTestHelpers.CreatePairedDevice();
        
        // Or customize
        var customDevice = AirPodsStateServiceTestHelpers.CreatePairedDevice(
            productId: AirPodsStateServiceTestHelpers.AirPods2ProductId,
            name: "My Custom AirPods",
            isConnected: true);

        // Simulate events
        AirPodsStateServiceTestHelpers.RaisePairedDeviceAdded(_pairedDeviceWatcher, device);
        
        var bleData = AirPodsStateServiceTestHelpers.CreateBleData(
            leftBattery: 75,
            rightBattery: 80,
            isLeftInEar: true);
            
        AirPodsStateServiceTestHelpers.RaiseBleDataReceived(_bleDataProvider, bleData);

        // Assert your expectations...
    }
}
```

## Adding to New Test Projects

To use TestHelpers in a new test project:

1. Add a project reference in your `.csproj`:
```xml
<ItemGroup>
  <ProjectReference Include="..\TestHelpers\TestHelpers.csproj" />
</ItemGroup>
```

2. Add the using statement in your test file:
```csharp
using TestHelpers;
```

3. Use the helper methods as shown in the usage example above.

## Dependencies

- **DeviceCommunication** - For model types and service interfaces
- **NSubstitute** - For event raising functionality

## Notes

- This project is **not** a test project itself (no `<IsTestProject>true</IsTestProject>`)
- It's a regular class library that test projects reference
- All helpers are static methods for ease of use
- Default values are provided for all parameters to minimize test code verbosity
