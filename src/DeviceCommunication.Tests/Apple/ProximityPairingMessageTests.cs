// WinPods Device Communication - Unit Tests
// Tests for ProximityPairingMessage
// File: Apple/ProximityPairingMessageTests.cs

using DeviceCommunication.Apple;
using FluentAssertions;
using Xunit;

namespace DeviceCommunication.Tests.Apple;

/// <summary>
/// Unit tests for the <see cref="ProximityPairingMessage"/> struct.
/// </summary>
public class ProximityPairingMessageTests
{
    // Valid AirPods Pro 2 message with sample data
    // Note: Model ID bytes are in wire format [0x14, 0x20] which reads as ushort 0x2014
    private static readonly byte[] ValidAirPodsPro2Message = new byte[]
    {
        0x07, // Packet type
        0x19, // Remaining length (25)
        0x01, // Unknown
        0x14, 0x20, // Model ID bytes in wire format (little-endian struct reads as 0x2014)
        0x55, // Status flags
        0x56, 0x60, // Battery status
        0x00, // Lid status (open)
        0x00, // Color
        0x00, // Unknown
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Unknown/encrypted
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00  // Unknown/encrypted
    };

    [Fact]
    public void IsValid_WithNullData_ThrowsArgumentNullException()
    {
        Action act = () => ProximityPairingMessage.IsValid(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsValid_WithIncorrectLength_ReturnsFalse()
    {
        var data = new byte[26]; // Too short

        var result = ProximityPairingMessage.IsValid(data);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithIncorrectPacketType_ReturnsFalse()
    {
        var data = new byte[27];
        data[0] = 0x08; // Wrong packet type
        data[1] = 0x19;

        var result = ProximityPairingMessage.IsValid(data);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithIncorrectRemainingLength_ReturnsFalse()
    {
        var data = new byte[27];
        data[0] = 0x07;
        data[1] = 0x18; // Wrong remaining length

        var result = ProximityPairingMessage.IsValid(data);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithValidMessage_ReturnsTrue()
    {
        var result = ProximityPairingMessage.IsValid(ValidAirPodsPro2Message);

        result.Should().BeTrue();
    }

    [Fact]
    public void FromBytes_WithNullData_ThrowsArgumentNullException()
    {
        Action act = () => ProximityPairingMessage.FromBytes(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromBytes_WithInvalidMessage_ReturnsNull()
    {
        var data = new byte[27];

        var result = ProximityPairingMessage.FromBytes(data);

        result.Should().BeNull();
    }

    [Fact]
    public void FromBytes_WithValidMessage_ReturnsMessage()
    {
        var result = ProximityPairingMessage.FromBytes(ValidAirPodsPro2Message);

        result.Should().NotBeNull();
    }

    [Fact]
    public void GetModel_WithAirPodsPro2_ReturnsCorrectModel()
    {
        var message = ProximityPairingMessage.FromManufacturerData(ValidAirPodsPro2Message);

        var model = message!.Value.GetModel();

        model.Should().Be(AppleDeviceModel.AirPodsPro2);
    }

    // Note: Bytes are in wire format order as they appear in the BLE advertisement.
    // The struct reads them and due to little-endian architecture, byte order is reversed.
    // For model ID 0x2014: wire bytes [0x14, 0x20] -> struct reads as ushort 0x2014
    [Theory]
    [InlineData(0x02, 0x20, AppleDeviceModel.AirPods1)]       // Wire: [0x02, 0x20] -> Struct: 0x2002
    [InlineData(0x0F, 0x20, AppleDeviceModel.AirPods2)]       // Wire: [0x0F, 0x20] -> Struct: 0x200F
    [InlineData(0x13, 0x20, AppleDeviceModel.AirPods3)]       // Wire: [0x13, 0x20] -> Struct: 0x2013
    [InlineData(0x0E, 0x20, AppleDeviceModel.AirPodsPro)]     // Wire: [0x0E, 0x20] -> Struct: 0x200E
    [InlineData(0x14, 0x20, AppleDeviceModel.AirPodsPro2)]    // Wire: [0x14, 0x20] -> Struct: 0x2014
    [InlineData(0x24, 0x20, AppleDeviceModel.AirPodsPro2UsbC)] // Wire: [0x24, 0x20] -> Struct: 0x2024
    [InlineData(0x0A, 0x20, AppleDeviceModel.AirPodsMax)]     // Wire: [0x0A, 0x20] -> Struct: 0x200A
    [InlineData(0x12, 0x20, AppleDeviceModel.BeatsFitPro)]    // Wire: [0x12, 0x20] -> Struct: 0x2012
    public void GetModel_WithDifferentModels_ReturnsCorrectModel(byte byte1, byte byte2, AppleDeviceModel expectedModel)
    {
        var data = (byte[])ValidAirPodsPro2Message.Clone();
        data[3] = byte1;
        data[4] = byte2;

        var message = ProximityPairingMessage.FromBytes(data);

        var model = message!.Value.GetModel();

        model.Should().Be(expectedModel);
    }

    [Fact]
    public void GetModel_WithUnknownModelId_ReturnsUnknown()
    {
        var data = (byte[])ValidAirPodsPro2Message.Clone();
        data[3] = 0xFF;
        data[4] = 0xFF;

        var message = ProximityPairingMessage.FromBytes(data);

        var model = message!.Value.GetModel();

        model.Should().Be(AppleDeviceModel.Unknown);
    }

    [Theory]
    [InlineData(0x20, ProximitySide.Left)]
    [InlineData(0x00, ProximitySide.Right)]
    public void GetBroadcastSide_WithDifferentSides_ReturnsCorrectSide(byte statusByte, ProximitySide expectedSide)
    {
        var data = (byte[])ValidAirPodsPro2Message.Clone();
        data[5] = statusByte;

        var message = ProximityPairingMessage.FromBytes(data);

        var side = message!.Value.GetBroadcastSide();
        var isLeft = message.Value.IsLeftBroadcasted();
        var isRight = message.Value.IsRightBroadcasted();

        side.Should().Be(expectedSide);
        if (expectedSide == ProximitySide.Left)
        {
            isLeft.Should().BeTrue();
            isRight.Should().BeFalse();
        }
        else
        {
            isLeft.Should().BeFalse();
            isRight.Should().BeTrue();
        }
    }

    [Theory]
    [InlineData(0x00, (byte)0)]
    [InlineData(0x05, (byte)5)]
    [InlineData(0x0A, (byte)10)]
    [InlineData(0x0F, null)] // Invalid value > 10
    public void GetLeftBattery_WithDifferentValues_ReturnsCorrectBattery(byte batteryByte, byte? expectedBattery)
    {
        var data = (byte[])ValidAirPodsPro2Message.Clone();
        data[5] = 0x20; // Left broadcasting
        data[6] = batteryByte; // Current battery (left when left broadcasting)

        var message = ProximityPairingMessage.FromBytes(data);

        var battery = message!.Value.GetLeftBattery();

        battery.Should().Be(expectedBattery);
    }

    [Fact]
    public void GetCaseBattery_WithValidValue_ReturnsBattery()
    {
        var data = (byte[])ValidAirPodsPro2Message.Clone();
        data[7] = 0x08; // Case battery = 8

        var message = ProximityPairingMessage.FromBytes(data);

        var battery = message!.Value.GetCaseBattery();

        battery.Should().Be(8);
    }

    [Fact]
    public void GetCaseBattery_WithInvalidValue_ReturnsNull()
    {
        var data = (byte[])ValidAirPodsPro2Message.Clone();
        data[7] = 0x0F; // Invalid value > 10

        var message = ProximityPairingMessage.FromBytes(data);

        var battery = message!.Value.GetCaseBattery();

        battery.Should().BeNull();
    }

    [Theory]
    [InlineData(0x20, 0x10, true, false, false)]  // Left charging
    [InlineData(0x00, 0x10, false, true, false)]  // Right charging
    [InlineData(0x20, 0x40, false, false, true)]  // Case charging
    [InlineData(0x20, 0x50, true, false, true)]   // Left and case charging
    public void ChargingStatus_WithDifferentStates_ReturnsCorrectStatus(
        byte statusByte, byte batteryByte, bool expectedLeftCharging, bool expectedRightCharging, bool expectedCaseCharging)
    {
        var data = (byte[])ValidAirPodsPro2Message.Clone();
        data[5] = statusByte;
        data[7] = batteryByte;

        var message = ProximityPairingMessage.FromBytes(data);

        message!.Value.IsLeftCharging().Should().Be(expectedLeftCharging);
        message.Value.IsRightCharging().Should().Be(expectedRightCharging);
        message.Value.IsCaseCharging().Should().Be(expectedCaseCharging);
    }

    [Fact]
    public void IsBothPodsInCase_WhenBothInCase_ReturnsTrue()
    {
        var data = (byte[])ValidAirPodsPro2Message.Clone();
        data[5] = 0x04; // Both in case bit set

        var message = ProximityPairingMessage.FromBytes(data);

        var result = message!.Value.IsBothPodsInCase();

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(0x00, true)]   // Lid open
    [InlineData(0x08, false)]  // Lid closed
    public void IsLidOpened_WithDifferentStates_ReturnsCorrectStatus(byte lidByte, bool expectedOpen)
    {
        var data = (byte[])ValidAirPodsPro2Message.Clone();
        data[8] = lidByte;

        var message = ProximityPairingMessage.FromBytes(data);

        var result = message!.Value.IsLidOpened();

        result.Should().Be(expectedOpen);
    }

    [Fact]
    public void IsLeftInEar_WhenLeftInEar_ReturnsTrue()
    {
        var data = (byte[])ValidAirPodsPro2Message.Clone();
        data[5] = 0x22; // Left broadcasting, in-ear bit set
        data[7] = 0x00; // Not charging

        var message = ProximityPairingMessage.FromBytes(data);

        var result = message!.Value.IsLeftInEar();

        result.Should().BeTrue();
    }

    [Fact]
    public void IsLeftInEar_WhenLeftCharging_ReturnsFalse()
    {
        var data = (byte[])ValidAirPodsPro2Message.Clone();
        data[5] = 0x22; // Left broadcasting, in-ear bit set
        data[7] = 0x10; // Left charging

        var message = ProximityPairingMessage.FromBytes(data);

        var result = message!.Value.IsLeftInEar();

        result.Should().BeFalse();
    }

    [Fact]
    public void IsRightInEar_WhenRightInEar_ReturnsTrue()
    {
        var data = (byte[])ValidAirPodsPro2Message.Clone();
        data[5] = 0x02; // Right broadcasting, in-ear bit set
        data[7] = 0x00; // Not charging

        var message = ProximityPairingMessage.FromBytes(data);

        var result = message!.Value.IsRightInEar();

        result.Should().BeTrue();
    }

    [Fact]
    public void GetModel_WithRealCapturedAirPodsPro2Data_ReturnsAirPodsPro2()
    {
        byte[] realData = new byte[] 
        { 
            0x07, 0x19, 0x01, 0x14, 0x20, 0x55, 0xAA, 0xB8, 0x11, 
            0x00, 0x04, 0x1B, 0xE9, 0xD4, 0x3B, 0xA1, 0x34, 0xD2, 
            0x3B, 0x34, 0x24, 0xF0, 0x3D, 0x56, 0xB5, 0xA6, 0x3A 
        };
        
        var message = ProximityPairingMessage.FromBytes(realData);
        var model = message!.Value.GetModel();
        
        message.Should().NotBeNull();
        model.Should().Be(AppleDeviceModel.AirPodsPro2);
    }

    [Theory]
    [InlineData(0)]  // Message at start
    [InlineData(3)]  // Message at offset
    public void FromManufacturerData_WithValidMessage_ExtractsMessage(int offset)
    {
        byte[] manufacturerData = new byte[offset + 27];
        for (int i = 0; i < offset; i++)
        {
            manufacturerData[i] = 0xFF;
        }
        Array.Copy(ValidAirPodsPro2Message, 0, manufacturerData, offset, 27);
        
        var message = ProximityPairingMessage.FromManufacturerData(manufacturerData);
        
        message.Should().NotBeNull();
        message!.Value.GetModel().Should().Be(AppleDeviceModel.AirPodsPro2);
    }

    [Fact]
    public void FromManufacturerData_WithTruncatedData_ReturnsNull()
    {
        byte[] truncatedData = new byte[] 
        { 
            0x10, 0x07, 0x26, 0x1F, 0x3A, 0x61, 0xF9, 0xB9, 0x68 
        };
        
        var message = ProximityPairingMessage.FromManufacturerData(truncatedData);
        
        message.Should().BeNull();
    }

    [Fact]
    public void FromManufacturerData_WithNoValidMessage_ReturnsNull()
    {
        byte[] invalidData = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
        
        var message = ProximityPairingMessage.FromManufacturerData(invalidData);
        
        message.Should().BeNull();
    }

    [Fact]
    public void FromManufacturerData_WithNullData_ThrowsArgumentNullException()
    {
        Action act = () => ProximityPairingMessage.FromManufacturerData(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
