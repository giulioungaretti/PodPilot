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
}
