// Apple proximity pairing message parser

using System.Runtime.InteropServices;

namespace DeviceCommunication.Apple
{
    /// <summary>
    /// Parses and interprets Apple AirPods proximity pairing advertisement messages.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This structure directly maps to the binary layout of Apple's proximity pairing
    /// BLE advertisement data. It provides methods to extract battery levels, charging
    /// status, case status, and in-ear detection information.
    /// </para>
    /// <para>
    /// Message format (27 bytes total):
    /// <list type="bullet">
    /// <item><description>Byte 0: Packet type (0x07)</description></item>
    /// <item><description>Byte 1: Remaining length (25)</description></item>
    /// <item><description>Byte 2: Reserved</description></item>
    /// <item><description>Bytes 3-4: Model ID as ushort (little-endian struct layout)</description></item>
    /// <item><description>Byte 5: Status flags</description></item>
    /// <item><description>Bytes 6-7: Battery status</description></item>
    /// <item><description>Byte 8: Lid status</description></item>
    /// <item><description>Byte 9: Device color</description></item>
    /// <item><description>Byte 10: Reserved</description></item>
    /// <item><description>Bytes 11-26: Reserved/encrypted data</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Note on byte order: The struct uses StructLayout with Pack=1, which means fields are
    /// laid out sequentially in memory. On little-endian systems (Windows), multi-byte values
    /// like the ushort modelId are stored with the least significant byte first. This means
    /// wire bytes [0x14, 0x20] naturally become the value 0x2014 without any byte swapping needed.
    /// </para>
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct ProximityPairingMessage
    {
        private byte packetType;
        private byte remainingLength;
        private byte unk1;
        private ushort modelId;
        private byte statusFlags;
        private fixed byte batteryStatus[2];
        private byte lidStatus;
        private byte color;
        private byte unk11;
        private fixed byte unk12[16];

        /// <summary>
        /// The expected size of a valid proximity pairing message in bytes.
        /// </summary>
        public const int MESSAGE_SIZE = 27;

        /// <summary>
        /// Validates whether the provided data represents a valid proximity pairing message.
        /// </summary>
        /// <param name="data">The raw advertisement data to validate.</param>
        /// <returns><c>true</c> if the data is a valid message; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        public static bool IsValid(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (data.Length != MESSAGE_SIZE) return false;

            const int expectedRemainingLength = MESSAGE_SIZE - 2;
            return data[0] == AppleConstants.PROXIMITY_PAIRING_PACKET_TYPE
                   && data[1] == expectedRemainingLength;
        }

        /// <summary>
        /// Creates a <see cref="ProximityPairingMessage"/> from raw byte data.
        /// </summary>
        /// <param name="data">The raw advertisement data.</param>
        /// <returns>
        /// A <see cref="ProximityPairingMessage"/> if the data is valid; otherwise, <c>null</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        public static ProximityPairingMessage? FromBytes(byte[] data)
        {
            if (!IsValid(data)) return null;

            fixed (byte* ptr = data)
            {
                return *(ProximityPairingMessage*)ptr;
            }
        }

        /// <summary>
        /// Searches for and extracts a proximity pairing message from Apple manufacturer data.
        /// </summary>
        /// <param name="manufacturerData">The raw manufacturer data from an Apple BLE advertisement.</param>
        /// <returns>
        /// A <see cref="ProximityPairingMessage"/> if found and valid; otherwise, <c>null</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="manufacturerData"/> is null.</exception>
        /// <remarks>
        /// This method searches through the manufacturer data to find the proximity pairing packet
        /// (identified by packet type 0x07) and extracts the 27-byte message. This is necessary because
        /// Apple's manufacturer data may contain multiple types of messages or have additional headers.
        /// </remarks>
        public static ProximityPairingMessage? FromManufacturerData(byte[] manufacturerData)
        {
            if (manufacturerData == null)
                throw new ArgumentNullException(nameof(manufacturerData));

            // Search for the proximity pairing packet type (0x07) in the manufacturer data
            for (int i = 0; i <= manufacturerData.Length - MESSAGE_SIZE; i++)
            {
                // Check if we found the start of a proximity pairing message
                if (manufacturerData[i] == AppleConstants.PROXIMITY_PAIRING_PACKET_TYPE &&
                    i + 1 < manufacturerData.Length &&
                    manufacturerData[i + 1] == MESSAGE_SIZE - 2) // Remaining length should be 25
                {
                    // Extract the 27-byte message
                    var messageBytes = new byte[MESSAGE_SIZE];
                    Array.Copy(manufacturerData, i, messageBytes, 0, MESSAGE_SIZE);

                    // Try to parse it
                    return FromBytes(messageBytes);
                }
            }

            return null;
        }

        /// <summary>
        /// Gets which AirPod is currently broadcasting this message.
        /// </summary>
        /// <returns>The side of the AirPod that is broadcasting.</returns>
        public readonly ProximitySide GetBroadcastSide()
        {
            return (statusFlags & 0x20) == 0 ? ProximitySide.Right : ProximitySide.Left;
        }

        /// <summary>
        /// Determines whether the left AirPod is broadcasting.
        /// </summary>
        /// <returns><c>true</c> if the left AirPod is broadcasting; otherwise, <c>false</c>.</returns>
        public readonly bool IsLeftBroadcasted() => GetBroadcastSide() == ProximitySide.Left;

        /// <summary>
        /// Determines whether the right AirPod is broadcasting.
        /// </summary>
        /// <returns><c>true</c> if the right AirPod is broadcasting; otherwise, <c>false</c>.</returns>
        public readonly bool IsRightBroadcasted() => GetBroadcastSide() == ProximitySide.Right;

        /// <summary>
        /// Gets the device model from the advertisement.
        /// </summary>
        /// <returns>The <see cref="AppleDeviceModel"/> that sent this advertisement.</returns>
        /// <remarks>
        /// <para>
        /// The model ID is stored as a 16-bit big-endian value in the wire protocol (bytes 3-4).
        /// For example, AirPods Pro 2 has model ID 0x2014, transmitted as bytes [0x14, 0x20] 
        /// (low byte first in memory due to little-endian struct layout).
        /// </para>
        /// <para>
        /// When the struct reads these bytes on a little-endian system (Windows), the StructLayout
        /// places byte[3]=0x14 at the low byte position and byte[4]=0x20 at the high byte position,
        /// which naturally forms the ushort value 0x2014 in memory.
        /// </para>
        /// </remarks>
        public readonly AppleDeviceModel GetModel()
        {
            return modelId switch
            {
                0x2002 => AppleDeviceModel.AirPods1,
                0x200F => AppleDeviceModel.AirPods2,
                0x2013 => AppleDeviceModel.AirPods3,
                0x200E => AppleDeviceModel.AirPodsPro,
                0x2014 => AppleDeviceModel.AirPodsPro2,
                0x2024 => AppleDeviceModel.AirPodsPro2UsbC,
                0x200A => AppleDeviceModel.AirPodsMax,
                0x2012 => AppleDeviceModel.BeatsFitPro,
                _ => AppleDeviceModel.Unknown
            };
        }

        /// <summary>
        /// Gets the battery level of the currently broadcasting AirPod.
        /// </summary>
        /// <returns>
        /// Battery level (0-10), or <c>null</c> if unavailable or invalid.
        /// Multiply by 10 to get percentage (0-100%).
        /// </returns>
        private readonly byte? GetCurrBattery()
        {
            var val = (byte)(batteryStatus[0] & 0x0F);
            return val <= 10 ? val : null;
        }

        /// <summary>
        /// Gets the battery level of the non-broadcasting AirPod.
        /// </summary>
        /// <returns>
        /// Battery level (0-10), or <c>null</c> if unavailable or invalid.
        /// Multiply by 10 to get percentage (0-100%).
        /// </returns>
        private readonly byte? GetAnotBattery()
        {
            var val = (byte)((batteryStatus[0] >> 4) & 0x0F);
            return val <= 10 ? val : null;
        }

        /// <summary>
        /// Gets the battery level of the left AirPod.
        /// </summary>
        /// <returns>
        /// Battery level (0-10), or <c>null</c> if unavailable.
        /// Multiply by 10 to get percentage (0-100%).
        /// </returns>
        public readonly byte? GetLeftBattery()
        {
            return IsLeftBroadcasted() ? GetCurrBattery() : GetAnotBattery();
        }

        /// <summary>
        /// Gets the battery level of the right AirPod.
        /// </summary>
        /// <returns>
        /// Battery level (0-10), or <c>null</c> if unavailable.
        /// Multiply by 10 to get percentage (0-100%).
        /// </returns>
        public readonly byte? GetRightBattery()
        {
            return IsRightBroadcasted() ? GetCurrBattery() : GetAnotBattery();
        }

        /// <summary>
        /// Gets the battery level of the charging case.
        /// </summary>
        /// <returns>
        /// Battery level (0-10), or <c>null</c> if unavailable.
        /// Multiply by 10 to get percentage (0-100%).
        /// </returns>
        public readonly byte? GetCaseBattery()
        {
            var val = (byte)(batteryStatus[1] & 0x0F);
            return val <= 10 ? val : null;
        }

        /// <summary>
        /// Determines whether the left AirPod is currently charging.
        /// </summary>
        /// <returns><c>true</c> if the left AirPod is charging; otherwise, <c>false</c>.</returns>
        public readonly bool IsLeftCharging()
        {
            return IsLeftBroadcasted()
                ? (batteryStatus[1] & 0x10) != 0
                : (batteryStatus[1] & 0x20) != 0;
        }

        /// <summary>
        /// Determines whether the right AirPod is currently charging.
        /// </summary>
        /// <returns><c>true</c> if the right AirPod is charging; otherwise, <c>false</c>.</returns>
        public readonly bool IsRightCharging()
        {
            return IsRightBroadcasted()
                ? (batteryStatus[1] & 0x10) != 0
                : (batteryStatus[1] & 0x20) != 0;
        }

        /// <summary>
        /// Determines whether the charging case is currently charging.
        /// </summary>
        /// <returns><c>true</c> if the case is charging; otherwise, <c>false</c>.</returns>
        public readonly bool IsCaseCharging() => (batteryStatus[1] & 0x40) != 0;

        /// <summary>
        /// Determines whether both AirPods are in the charging case.
        /// </summary>
        /// <returns><c>true</c> if both AirPods are in the case; otherwise, <c>false</c>.</returns>
        public readonly bool IsBothPodsInCase() => (statusFlags & 0x04) != 0;

        /// <summary>
        /// Determines whether the charging case lid is open.
        /// </summary>
        /// <returns><c>true</c> if the lid is open; otherwise, <c>false</c>.</returns>
        public readonly bool IsLidOpened() => (lidStatus & 0x08) == 0;

        /// <summary>
        /// Determines whether the left AirPod is currently in someone's ear.
        /// </summary>
        /// <returns><c>true</c> if the left AirPod is in ear; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// If the left AirPod is charging, this will always return <c>false</c>.
        /// </remarks>
        public readonly bool IsLeftInEar()
        {
            if (IsLeftCharging()) return false;
            return IsLeftBroadcasted()
                ? (statusFlags & 0x02) != 0
                : (statusFlags & 0x08) != 0;
        }

        /// <summary>
        /// Determines whether the right AirPod is currently in someone's ear.
        /// </summary>
        /// <returns><c>true</c> if the right AirPod is in ear; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// If the right AirPod is charging, this will always return <c>false</c>.
        /// </remarks>
        public readonly bool IsRightInEar()
        {
            if (IsRightCharging()) return false;
            return IsRightBroadcasted()
                ? (statusFlags & 0x02) != 0
                : (statusFlags & 0x08) != 0;
        }
    }
}
