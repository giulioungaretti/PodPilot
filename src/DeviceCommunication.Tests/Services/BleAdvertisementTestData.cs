using DeviceCommunication.Advertisement;
using DeviceCommunication.Apple;

namespace DeviceCommunication.Tests.Services;

/// <summary>
/// Pre-built advertisement scenarios for testing BleDataProvider.
/// Each constant is a complete, ready-to-use AdvertisementReceivedData object representing
/// a specific device state (e.g., "AirPods Pro 2 with both pods in case").
/// Tests use these scenarios directly without constructing or validating binary payloads.
/// </summary>
internal static class BleAdvertisementTestData
{
    /// <summary>
    /// Create a left broadcast showing both pods in ear
    /// Status byte 0x2A = 0x20 (left broadcasting) | 0x02 (left in ear) | 0x08 (right in ear)
    /// Battery[1] byte 0x00 = no charging (bits 0x20 and 0x10 are clear)
    /// </summary>
    public static readonly AdvertisementReceivedData LeftBroadcastBothInEar =
       new()
       {
           Address = 0x111111111111,
           Rssi = -50,
           Timestamp = DateTimeOffset.Now,
           ManufacturerData = new Dictionary<ushort, byte[]>
           {
                {
                    AppleConstants.VENDOR_ID,
                    new byte[]
                    {
                        0x07, 0x19, 0x01, 0x14, 0x20, 0x2A, 0x89, 0x00, 0x00,
                        // ^ status 0x2A = left broadcasting + both in ear
                        // batteryStatus[1] = 0x00 (no charging flags set)
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00
                    }
                }
           }
       };

    // Create a right broadcast also showing both pods in ear
    // Status byte 0x0A = 0x00 (right broadcasting) | 0x02 (right in ear) | 0x08 (left in ear)
    // Battery[1] byte 0x00 = no charging (bits 0x20 and 0x10 are clear)
    public static readonly AdvertisementReceivedData RightBroadcastBothInEar =
        new()
        {
            Address = 0x222222222222,
            Rssi = -50,
            Timestamp = DateTimeOffset.Now,
            ManufacturerData = new Dictionary<ushort, byte[]>
            {
                {
                    AppleConstants.VENDOR_ID,
                    new byte[]
                    {
                        0x07, 0x19, 0x01, 0x14, 0x20, 0x0A, 0x89, 0x00, 0x00,
                        // ^ status 0x0A = right broadcasting + both in ear
                        // batteryStatus[1] = 0x00 (no charging flags set)
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00
                    }
                }
            }
        };


    /// <summary>
    /// AirPods Pro 2 (0x2014) with both pods in case, lid open.
    /// </summary>
    public static readonly AdvertisementReceivedData AirPodsPro2_BothInCase =
        new()
        {
            Address = 0x112233445566,
            Rssi = -50,
            Timestamp = DateTimeOffset.Now,
            ManufacturerData = new Dictionary<ushort, byte[]>
            {
                {
                    AppleConstants.VENDOR_ID,
                    new byte[]
                    {
                        0x07, 0x19, 0x01, 0x14, 0x20, 0x55, 0xAA, 0xB8, 0x11,
                        0x00, 0x04, 0x1B, 0xE9, 0xD4, 0x3B, 0xA1, 0x34, 0xD2,
                        0x3B, 0x34, 0x24, 0xF0, 0x3D, 0x56, 0xB5, 0xA6, 0x3A
                    }
                }
            }
        };

    /// <summary>
    /// AirPods Pro 2 with left pod broadcasting, both pods in case.
    /// </summary>
    public static readonly AdvertisementReceivedData AirPodsPro2_LeftBroadcasting_BothInCase =
        new()
        {
            Address = 0x112233445566,
            Rssi = -50,
            Timestamp = DateTimeOffset.Now,
            ManufacturerData = new Dictionary<ushort, byte[]>
            {
                {
                    AppleConstants.VENDOR_ID,
                    new byte[]
                    {
                        0x07, 0x19, 0x01, 0x14, 0x20, 0x24, 0x89, 0x60,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00
                    }
                }
            }
        };

    /// <summary>
    /// AirPods Pro 2 with right pod broadcasting, both pods in case.
    /// </summary>
    public static readonly AdvertisementReceivedData AirPodsPro2_RightBroadcasting_BothInCase =
        new()
        {
            Address = 0x112233445566,
            Rssi = -50,
            Timestamp = DateTimeOffset.Now,
            ManufacturerData = new Dictionary<ushort, byte[]>
            {
                {
                    AppleConstants.VENDOR_ID,
                    new byte[]
                    {
                        0x07, 0x19, 0x01, 0x14, 0x20, 0x04, 0x89, 0x60,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00
                    }
                }
            }
        };

    /// <summary>
    /// AirPods Pro 2 with left pod broadcasting, left in ear, right in case.
    /// </summary>
    public static readonly AdvertisementReceivedData AirPodsPro2_LeftBroadcasting_LeftInEar =
        new()
        {
            Address = 0x112233445566,
            Rssi = -50,
            Timestamp = DateTimeOffset.Now,
            ManufacturerData = new Dictionary<ushort, byte[]>
            {
                {
                    AppleConstants.VENDOR_ID,
                    new byte[]
                    {
                        0x07, 0x19, 0x01, 0x14, 0x20, 0x26, 0x89, 0x60,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00
                    }
                }
            }
        };

    /// <summary>
    /// AirPods Pro 2 with right pod broadcasting, right in ear, left in case.
    /// </summary>
    public static readonly AdvertisementReceivedData AirPodsPro2_RightBroadcasting_RightInEar =
        new()
        {
            Address = 0x112233445566,
            Rssi = -50,
            Timestamp = DateTimeOffset.Now,
            ManufacturerData = new Dictionary<ushort, byte[]>
            {
                {
                    AppleConstants.VENDOR_ID,
                    new byte[]
                    {
                        0x07, 0x19, 0x01, 0x14, 0x20, 0x02, 0x89, 0x60,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00
                    }
                }
            }
        };

    /// <summary>
    /// AirPods Pro 2 with left pod broadcasting, both pods in ear.
    ///  (0x60 in the battery field). meaning it's charging.
    /// </summary>
    public static readonly AdvertisementReceivedData AirPodsPro2_LeftBroadcasting_BothInEar =
        new()
        {
            Address = 0x112233445566,
            Rssi = -50,
            Timestamp = DateTimeOffset.Now,
            ManufacturerData = new Dictionary<ushort, byte[]>
            {
                {
                    AppleConstants.VENDOR_ID,
                    new byte[]
                    {
                        0x07, 0x19, 0x01, 0x14, 0x20, 0x2E, 0x89, 0x60,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00
                    }
                }
            }
        };

    /// <summary>
    /// AirPods Pro 2 with left pod charging, both pods in case.
    /// </summary>
    public static readonly AdvertisementReceivedData AirPodsPro2_LeftCharging =
        new()
        {
            Address = 0x112233445566,
            Rssi = -50,
            Timestamp = DateTimeOffset.Now,
            ManufacturerData = new Dictionary<ushort, byte[]>
            {
                {
                    AppleConstants.VENDOR_ID,
                    new byte[]
                    {
                        0x07, 0x19, 0x01, 0x14, 0x20, 0x24, 0x89, 0x70,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00
                    }
                }
            }
        };

    /// <summary>
    /// AirPods Pro 2 with case charging, both pods in case.
    /// </summary>
    public static readonly AdvertisementReceivedData AirPodsPro2_CaseCharging =
        new()
        {
            Address = 0x112233445566,
            Rssi = -50,
            Timestamp = DateTimeOffset.Now,
            ManufacturerData = new Dictionary<ushort, byte[]>
            {
                {
                    AppleConstants.VENDOR_ID,
                    new byte[]
                    {
                        0x07, 0x19, 0x01, 0x14, 0x20, 0x24, 0x89, 0x70,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00
                    }
                }
            }
        };

    /// <summary>
    /// AirPods Pro 2 with lid closed, both pods in case.
    /// </summary>
    public static readonly AdvertisementReceivedData AirPodsPro2_LidClosed =
        new()
        {
            Address = 0x112233445566,
            Rssi = -50,
            Timestamp = DateTimeOffset.Now,
            ManufacturerData = new Dictionary<ushort, byte[]>
            {
                {
                    AppleConstants.VENDOR_ID,
                    new byte[]
                    {
                        0x07, 0x19, 0x01, 0x14, 0x20, 0x24, 0x89, 0x60,
                        0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00
                    }
                }
            }
        };

    /// <summary>
    /// AirPods 3 (0x2013) with both pods in case, lid open.
    /// </summary>
    public static readonly AdvertisementReceivedData AirPods3_BothInCase =
        new()
        {
            Address = 0x112233445566,
            Rssi = -50,
            Timestamp = DateTimeOffset.Now,
            ManufacturerData = new Dictionary<ushort, byte[]>
            {
                {
                    AppleConstants.VENDOR_ID,
                    new byte[]
                    {
                        0x07, 0x19, 0x01, 0x13, 0x20, 0x24, 0x89, 0x60,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00
                    }
                }
            }
        };
}

/// <summary>
/// Builder for creating modified copies of AdvertisementReceivedData.
/// Allows overriding Address, RSSI, and Timestamp on a base advertisement scenario.
/// </summary>
internal class AdvertisementBuilder
{
    private readonly AdvertisementReceivedData _baseAdvertisement;
    private ulong? _address;
    private short? _rssi;
    private DateTimeOffset? _timestamp;

    /// <summary>
    /// Creates a builder with the given advertisement as the base.
    /// </summary>
    /// <param name="baseAdvertisement">The base advertisement (typically from BleAdvertisementTestData)</param>
    public AdvertisementBuilder(AdvertisementReceivedData baseAdvertisement)
    {
        if (baseAdvertisement == null)
            throw new ArgumentNullException(nameof(baseAdvertisement));

        _baseAdvertisement = baseAdvertisement;
    }

    /// <summary>
    /// Sets the BLE address (rotating MAC address).
    /// </summary>
    public AdvertisementBuilder WithAddress(ulong address)
    {
        _address = address;
        return this;
    }

    /// <summary>
    /// Sets the signal strength (RSSI in dBm).
    /// </summary>
    public AdvertisementBuilder WithRssi(short rssi)
    {
        _rssi = rssi;
        return this;
    }

    /// <summary>
    /// Sets the timestamp when the advertisement was received.
    /// </summary>
    public AdvertisementBuilder WithTimestamp(DateTimeOffset timestamp)
    {
        _timestamp = timestamp;
        return this;
    }

    /// <summary>
    /// Builds the AdvertisementReceivedData with overrides applied.
    /// </summary>
    public AdvertisementReceivedData Build()
    {
        return new AdvertisementReceivedData
        {
            Address = _address ?? _baseAdvertisement.Address,
            Rssi = _rssi ?? _baseAdvertisement.Rssi,
            Timestamp = _timestamp ?? _baseAdvertisement.Timestamp,
            ManufacturerData = new Dictionary<ushort, byte[]>(_baseAdvertisement.ManufacturerData)
        };
    }
}
