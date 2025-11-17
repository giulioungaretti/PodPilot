// Apple Continuity Protocol constants

namespace DeviceCommunication.Apple
{
    /// <summary>
    /// Constants used in Apple's Continuity Protocol.
    /// </summary>
    /// <remarks>
    /// Apple uses a proprietary protocol for device pairing and status updates
    /// via BLE advertisements. These constants are used to identify and parse
    /// Apple-specific advertisement data.
    /// </remarks>
    public static class AppleConstants
    {
        /// <summary>
        /// Apple's Bluetooth SIG assigned company identifier.
        /// </summary>
        /// <remarks>
        /// This value appears in the manufacturer data section of BLE advertisements
        /// from Apple devices. Value: 0x004C (76 decimal).
        /// </remarks>
        public const ushort VENDOR_ID = 76;

        /// <summary>
        /// Packet type identifier for proximity pairing advertisements.
        /// </summary>
        /// <remarks>
        /// This byte appears as the first byte in Apple's manufacturer data
        /// for devices broadcasting proximity pairing information (e.g., AirPods
        /// with an open case). Value: 0x07.
        /// </remarks>
        public const byte PROXIMITY_PAIRING_PACKET_TYPE = 0x07;
    }
}
