// Real AirPods Pro 2 advertisement data captured from BLE scanner
// This can be used as realistic test data

namespace TestData;

public static class RealAirPodsAdvertisements
{
    /// <summary>
    /// Real AirPods Pro 2 proximity pairing message captured in the wild.
    /// Model ID: 0x2014 (AirPods Pro 2nd generation)
    /// Source: CLI Example 5 - Complete AirPods Monitor
    /// </summary>
    public static readonly byte[] AirPodsPro2_RealCapture = new byte[]
    {
        0x07, 0x19, 0x01, 0x14, 0x20, 0x55, 0xAA, 0xB8, 0x11,
        0x00, 0x04, 0x1B, 0xE9, 0xD4, 0x3B, 0xA1, 0x34, 0xD2,
        0x3B, 0x34, 0x24, 0xF0, 0x3D, 0x56, 0xB5, 0xA6, 0x3A
    };

    /// <summary>
    /// Captured Apple device advertisements (iPhones, etc.) - not AirPods.
    /// These are from real BLE scans but don't contain proximity pairing messages.
    /// </summary>
    public static class OtherAppleDevices
    {
        // Capture 1: 43E8E743D90E
        public static readonly byte[] iPhone_Nearby_1 = new byte[]
        {
            0x10, 0x07, 0x38, 0x1F, 0xBF, 0x8D, 0xE3, 0xC9, 0x18
        };

        // Capture 2: 525E20DF0EB2
        public static readonly byte[] iPhone_Nearby_2 = new byte[]
        {
            0x10, 0x07, 0x33, 0x1F, 0x04, 0x14, 0xF1, 0x48, 0x38
        };

        // Capture 3: 6BB1B1739822
        public static readonly byte[] AppleDevice_3 = new byte[]
        {
            0x10, 0x06, 0x24, 0x1D, 0x98, 0x9D, 0xA3, 0x58
        };

        // Capture 4: D564BAF53AEE
        public static readonly byte[] AppleDevice_4 = new byte[]
        {
            0x12, 0x02, 0x00, 0x00
        };

        // Capture 5: D39DF3CD53C0
        public static readonly byte[] AppleDevice_5 = new byte[]
        {
            0x12, 0x02, 0x00, 0x03
        };
    }
}
