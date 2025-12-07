// WinPods Device Communication - C# Implementation
// Apple device model enumeration
// File: Apple/AppleDeviceModel.cs

namespace DeviceCommunication.Apple
{
    /// <summary>
    /// Represents known Apple audio device models.
    /// </summary>
    /// <remarks>
    /// These model identifiers are extracted from Apple's Continuity Protocol
    /// proximity pairing advertisements.
    /// </remarks>
    public enum AppleDeviceModel
    {
        /// <summary>
        /// First generation AirPods (2016).
        /// </summary>
        AirPods1,

        /// <summary>
        /// Second generation AirPods (2019).
        /// </summary>
        AirPods2,

        /// <summary>
        /// Third generation AirPods (2021).
        /// </summary>
        AirPods3,

        /// <summary>
        /// First generation AirPods Pro (2019).
        /// </summary>
        AirPodsPro,

        /// <summary>
        /// Second generation AirPods Pro with Lightning case (2022).
        /// </summary>
        AirPodsPro2,

        /// <summary>
        /// Second generation AirPods Pro with USB-C case (2023).
        /// </summary>
        AirPodsPro2UsbC,

        /// <summary>
        /// AirPods Max over-ear headphones (2020).
        /// </summary>
        AirPodsMax,

        /// <summary>
        /// Beats Fit Pro earbuds (2021).
        /// </summary>
        BeatsFitPro,

        /// <summary>
        /// Unknown or unrecognized device model.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Provides helper methods for AppleDeviceModel.
    /// </summary>
    public static class AppleDeviceModelHelper
    {
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
        public static AppleDeviceModel GetModel(ushort modelId)
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
        /// Gets a user-friendly display name for the device model.
        /// </summary>
        /// <param name="model">The device model.</param>
        /// <returns>A human-readable name for the device model.</returns>
        public static string GetDisplayName(this AppleDeviceModel model) => model switch
        {
            AppleDeviceModel.AirPods1 => "AirPods (1st generation)",
            AppleDeviceModel.AirPods2 => "AirPods (2nd generation)",
            AppleDeviceModel.AirPods3 => "AirPods (3rd generation)",
            AppleDeviceModel.AirPodsPro => "AirPods Pro",
            AppleDeviceModel.AirPodsPro2 => "AirPods Pro (2nd generation)",
            AppleDeviceModel.AirPodsPro2UsbC => "AirPods Pro (2nd gen, USB-C)",
            AppleDeviceModel.AirPodsMax => "AirPods Max",
            AppleDeviceModel.BeatsFitPro => "Beats Fit Pro",
            _ => "Unknown AirPods"
        };

        /// <summary>
        /// Gets the Product ID for the device model, used for matching to Windows paired devices.
        /// </summary>
        /// <param name="model">The device model.</param>
        /// <returns>The Product ID, or null if the model is unknown.</returns>
        public static ushort? GetProductId(this AppleDeviceModel model) => model switch
        {
            AppleDeviceModel.AirPods1 => 0x2002,
            AppleDeviceModel.AirPods2 => 0x200F,
            AppleDeviceModel.AirPods3 => 0x2013,
            AppleDeviceModel.AirPodsPro => 0x200E,
            AppleDeviceModel.AirPodsPro2 => 0x2014,
            AppleDeviceModel.AirPodsPro2UsbC => 0x2024,
            AppleDeviceModel.AirPodsMax => 0x200A,
            AppleDeviceModel.BeatsFitPro => 0x2012,
            _ => null
        };
    }
}
