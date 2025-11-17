# Apple Continuity Protocol Research Documentation

## Overview

This document provides a comprehensive overview of Apple's Continuity Protocol as implemented in the WinPods Device Communication library. The protocol enables features like Handoff, Universal Clipboard, AirDrop, and proximity pairing for Apple devices via Bluetooth Low Energy (BLE) advertisements.

## Table of Contents

1. [Protocol Background](#protocol-background)
2. [BLE Advertisement Structure](#ble-advertisement-structure)
3. [Proximity Pairing Protocol](#proximity-pairing-protocol)
4. [Message Format Specification](#message-format-specification)
5. [Device Models](#device-models)
6. [Battery and Status Encoding](#battery-and-status-encoding)
7. [References](#references)

---

## Protocol Background

### What is Apple Continuity?

Apple's Continuity Protocol is a proprietary Bluetooth Low Energy (BLE) protocol that enables seamless experiences across Apple devices. It uses BLE advertisements to broadcast device status, battery information, and pairing data without requiring an active connection.

### Key Features

- **Proximity Pairing**: Automatic device discovery and pairing when devices are nearby
- **Battery Status Broadcasting**: Real-time battery levels for AirPods and charging case
- **In-Ear Detection**: Status of whether AirPods are currently being worn
- **Case Status**: Lid open/closed state and charging status
- **Power Efficiency**: Uses BLE advertisements (broadcast-only, no connection required)

### Why BLE Advertisements?

BLE advertisements are:
- **Connectionless**: Devices broadcast without establishing a connection
- **Low Power**: Minimal battery consumption
- **Discoverable**: Any nearby device can receive the broadcast
- **Fast**: Instant discovery without pairing handshake

---

## BLE Advertisement Structure

### Company Identifier

Apple devices use the Bluetooth SIG assigned company identifier in their manufacturer data:

```
Company ID: 0x004C (76 decimal)
```

This 16-bit value appears at the start of the manufacturer data section in BLE advertisements and identifies the data as coming from an Apple device.


### Advertisement Types

Apple uses multiple advertisement types within their manufacturer data, identified by a type byte:

| Type | Description |
|------|-------------|
| 0x01 | iBeacon |
| 0x02 | AirPlay Target |
| 0x03 | AirPlay Source |
| 0x05 | Magic Switch |
| 0x06 | Handoff |
| **0x07** | **Proximity Pairing** (AirPods) |
| 0x08 | Hey Siri |
| 0x09 | AirDrop |
| 0x0A | HomeKit |
| 0x0B | Proximity Action |
| 0x0C | Nearby Info |
| 0x0D | Find My |
| 0x0F | Nearby Action |
| 0x10 | Magic Switch |

Our implementation focuses on **Type 0x07 (Proximity Pairing)** used by AirPods and Beats devices.

---

## Proximity Pairing Protocol

### Purpose

The Proximity Pairing protocol (Type 0x07) broadcasts:
- Device model identification
- Battery levels (left, right, case)
- Charging status
- In-ear detection status
- Case lid state
- Device color information

### Advertising Behavior

**Broadcasting Side Alternation:**
AirPods alternate which earbud broadcasts the advertisement to conserve battery. The status flags indicate which side is currently transmitting:

```csharp
public enum ProximitySide
{
    Left,   // Left AirPod is broadcasting
    Right   // Right AirPod is broadcasting
}
```

**Broadcast Conditions:**
- Case lid is open with AirPods inside
- AirPods are removed from case
- AirPods are in pairing mode
- Periodic status updates when connected

---

## Message Format Specification

### Complete 27-Byte Structure

```
Offset  Size  Field                Description
------  ----  -------------------  --------------------------------------------------
0       1     Packet Type          0x07 (Proximity Pairing identifier)
1       1     Remaining Length     0x19 (25 bytes remaining)
2       1     Reserved             Reserved/unknown
3       2     Model ID             Device model identifier (big-endian on wire)
5       1     Status Flags         Broadcast side, in-ear detection, pod positions
6       2     Battery Status       Battery levels and charging flags
8       1     Lid Status           Case lid open/closed state
9       1     Device Color         Color identifier
10      1     Reserved             Reserved/unknown
11      16    Reserved/Encrypted   Reserved or encrypted data
```

**Total Message Size:** 27 bytes (0x1B)

### Wire Format vs. Memory Layout

**Important Note on Endianness:**
The Model ID field demonstrates a critical aspect of the protocol:

- **Wire Format (Network/Big-Endian):** Bytes are transmitted with the most significant byte first
  - Example: AirPods Pro 2 sends `[0x14, 0x20]` → represents model ID `0x2014`

- **Struct Layout (Little-Endian on Windows):** Due to `StructLayout(LayoutKind.Sequential, Pack = 1)`
  - Byte at offset 3 (0x14) becomes the low byte
  - Byte at offset 4 (0x20) becomes the high byte
  - Natural ushort value: `0x2014` (no byte swapping needed)

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct ProximityPairingMessage
{
    private byte packetType;        // Offset 0
    private byte remainingLength;   // Offset 1
    private byte unk1;              // Offset 2
    private ushort modelId;         // Offset 3-4 (little-endian struct layout)
    private byte statusFlags;       // Offset 5
    private fixed byte batteryStatus[2];  // Offset 6-7
    private byte lidStatus;         // Offset 8
    private byte color;             // Offset 9
    private byte unk11;             // Offset 10
    private fixed byte unk12[16];   // Offset 11-26
}
```

---

## Device Models

### Known Model IDs

The 2-byte Model ID field identifies the specific Apple device:

| Model ID | Device | Release Year |
|----------|--------|--------------|
| 0x2002 | AirPods (1st generation) | 2016 |
| 0x200F | AirPods (2nd generation) | 2019 |
| 0x2013 | AirPods (3rd generation) | 2021 |
| 0x200E | AirPods Pro (1st generation) | 2019 |
| 0x2014 | AirPods Pro (2nd gen, Lightning) | 2022 |
| 0x2024 | AirPods Pro (2nd gen, USB-C) | 2023 |
| 0x200A | AirPods Max | 2020 |
| 0x2012 | Beats Fit Pro | 2021 |

---

## Battery and Status Encoding

### Byte 5: Status Flags (statusFlags)

8-bit field encoding multiple status indicators:

```
Bit 7  Bit 6  Bit 5  Bit 4  Bit 3  Bit 2  Bit 1  Bit 0
  ?      ?     Side    ?     Other   Both    Curr    ?
                              InEar   InCase  InEar
```

**Bit Definitions:**

| Bit | Mask | Description |
|-----|------|-------------|
| 0 | 0x01 | Unknown/Reserved |
| 1 | 0x02 | Current (broadcasting) AirPod is in ear |
| 2 | 0x04 | Both AirPods are in case |
| 3 | 0x08 | Other (non-broadcasting) AirPod is in ear |
| 4 | 0x10 | Unknown/Reserved |
| 5 | 0x20 | Broadcasting side (0=Right, 1=Left) |
| 6-7 | 0xC0 | Unknown/Reserved |

**Broadcast Side Detection:**
```csharp
public readonly ProximitySide GetBroadcastSide()
{
    return (statusFlags & 0x20) == 0 ? ProximitySide.Right : ProximitySide.Left;
}
```

**In-Ear Detection:**
```csharp
public readonly bool IsLeftInEar()
{
    if (IsLeftCharging()) return false;
    return IsLeftBroadcasted()
        ? (statusFlags & 0x02) != 0  // Current pod in ear
        : (statusFlags & 0x08) != 0; // Other pod in ear
}
```

### Bytes 6-7: Battery Status (batteryStatus)

Two bytes encoding battery levels and charging status:

**Byte 6 (batteryStatus[0]) - Battery Levels:**

```
High Nibble (bits 4-7): Other AirPod battery
Low Nibble (bits 0-3):  Current AirPod battery
```

Battery values: 0-10 (multiply by 10 for percentage)
- 0 = 0%
- 1 = 10%
- 2 = 20%
- ...
- 10 = 100%
- 15 (0xF) = Unknown/Not available

**Byte 7 (batteryStatus[1]) - Case Battery & Charging:**

```
Bit 7  Bit 6  Bit 5  Bit 4  Bit 3  Bit 2  Bit 1  Bit 0
  ?     Case   Other  Curr    --- Case Battery ----
        Chrg   Chrg   Chrg
```

| Bits | Mask | Description |
|------|------|-------------|
| 0-3 | 0x0F | Case battery level (0-10) |
| 4 | 0x10 | Current (broadcasting) AirPod charging |
| 5 | 0x20 | Other AirPod charging |
| 6 | 0x40 | Case is charging |
| 7 | 0x80 | Unknown/Reserved |

**Battery Extraction:**
```csharp
// Current broadcasting AirPod
private readonly byte? GetCurrBattery()
{
    var val = (byte)(batteryStatus[0] & 0x0F);
    return val <= 10 ? val : null;
}

// Other (non-broadcasting) AirPod
private readonly byte? GetAnotBattery()
{
    var val = (byte)((batteryStatus[0] >> 4) & 0x0F);
    return val <= 10 ? val : null;
}

// Charging case
public readonly byte? GetCaseBattery()
{
    var val = (byte)(batteryStatus[1] & 0x0F);
    return val <= 10 ? val : null;
}
```

**Charging Status:**
```csharp
public readonly bool IsLeftCharging()
{
    return IsLeftBroadcasted()
        ? (batteryStatus[1] & 0x10) != 0  // Current pod charging
        : (batteryStatus[1] & 0x20) != 0; // Other pod charging
}

public readonly bool IsCaseCharging() => (batteryStatus[1] & 0x40) != 0;
```

### Byte 8: Lid Status (lidStatus)

```
Bit 7  Bit 6  Bit 5  Bit 4  Bit 3  Bit 2  Bit 1  Bit 0
  ?      ?      ?      ?     Open     ?      ?      ?
```

| Bit | Mask | Description |
|-----|------|-------------|
| 3 | 0x08 | Lid state (0=Open, 1=Closed) |
| Others | | Unknown/Reserved |

**Note:** Inverted logic - bit is CLEAR when lid is open:
```csharp
public readonly bool IsLidOpened() => (lidStatus & 0x08) == 0;
```

### Byte 9: Device Color (color)

Device color identifier (values not fully documented):
- Different values for white, black, etc.
- Specific encoding varies by device model

### Bytes 10-26: Reserved/Encrypted

The remaining 17 bytes are either:
- Reserved for future use
- Encrypted pairing data
- Device-specific information

These bytes are not decoded in the current implementation.

---

## References

### Official Bluetooth Specifications

1. **Bluetooth Core Specification v5.4**
   - https://www.bluetooth.com/specifications/specs/core-specification/
   - Defines BLE advertising protocol fundamentals
   - Volume 3, Part C: Generic Access Profile (GAP) - Advertisement packet structure
   - Volume 6, Part B: Link Layer Specification - Advertisement PDU format (max 31 bytes legacy)

2. **Bluetooth Assigned Numbers - Company Identifiers**
   - https://www.bluetooth.com/specifications/assigned-numbers/company-identifiers/
   - Apple Inc.: 0x004C (76 decimal)
   - Official registry for manufacturer identification in BLE advertisements


### Apple Protocol Documentation

3. **Core Bluetooth Framework Documentation**
   - https://developer.apple.com/documentation/corebluetooth
   - Official iOS/macOS Bluetooth LE API
   - CBCentralManager and advertisement scanning concepts
   - Note: Does not document proprietary Continuity Protocol details

### Community Reverse Engineering Research

4. **OpenPods - Android AirPods Implementation**
   - https://github.com/adolfintel/OpenPods
   - Most comprehensive reverse engineering of Apple Proximity Pairing Protocol (Type 0x07)
   - Documents 27-byte message format, battery encoding, and status flags
   - Primary source for model IDs and bit field specifications

5. **furiousMAC/continuity - Python Implementation**
   - https://github.com/furiousMAC/continuity
   - Documents multiple Apple Continuity protocol types (0x01-0x10)
   - Protocol analysis and packet capture examples
   - Cross-platform implementation reference




### Implementation Notes

- This implementation is based on observation and reverse engineering
- Protocol may change with iOS/firmware updates
- Some fields remain undocumented (encrypted/reserved sections)
- Testing performed with real AirPods Pro 2 device

---

## Appendix: Example Advertisement

### Real AirPods Pro 2 Advertisement (Hex Dump)

```
Manufacturer Data (Company ID: 0x004C):
07 19 01 14 20 55 AA 33 08 02 00 ...
│  │  │  │  │  │  │  │  │  │
│  │  │  │  │  │  │  │  │  └─ Reserved
│  │  │  │  │  │  │  │  └──── Device Color
│  │  │  │  │  │  │  └───────── Lid Status (0x08 bit clear = open)
│  │  │  │  │  │  └──────────── Case Battery + Charging (0x33 = case 3/10, pods charging)
│  │  │  │  │  └─────────────── Battery Levels (0xAA = 10/10 both pods)
│  │  │  │  └────────────────── Status Flags (0x55 = 0b01010101)
│  │  │  └───────────────────── Model ID High Byte (0x20)
│  │  └──────────────────────── Model ID Low Byte (0x14)
│  └─────────────────────────── Reserved
└────────────────────────────── Packet Type (0x07 = Proximity Pairing)
                                Length (0x19 = 25 bytes remaining)
```

### Decoded Values

- **Packet Type:** 0x07 (Proximity Pairing)
- **Model ID:** 0x2014 (AirPods Pro 2)
- **Broadcasting Side:** Left (bit 5 of status flags is 1)
- **Left Battery:** 10/10 (100%)
- **Right Battery:** 10/10 (100%)
- **Case Battery:** 3/10 (30%)
- **Left Charging:** Yes
- **Right Charging:** Yes
- **Case Charging:** No
- **Lid Open:** Yes
- **Left In Ear:** No
- **Right In Ear:** No

This example shows fully charged AirPods in an open case with low case battery.

---

## License

This documentation is provided for educational and interoperability purposes. Apple, AirPods, and related trademarks are property of Apple Inc.
