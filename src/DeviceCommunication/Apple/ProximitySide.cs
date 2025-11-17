// WinPods Device Communication - C# Implementation
// Proximity broadcast side enumeration
// File: Apple/ProximitySide.cs

namespace DeviceCommunication.Apple
{
    /// <summary>
    /// Indicates which AirPod is broadcasting the proximity pairing message.
    /// </summary>
    /// <remarks>
    /// AirPods alternate which earbud broadcasts status information to conserve battery.
    /// This enumeration indicates which side is currently transmitting.
    /// </remarks>
    public enum ProximitySide
    {
        /// <summary>
        /// The left AirPod is broadcasting.
        /// </summary>
        Left,

        /// <summary>
        /// The right AirPod is broadcasting.
        /// </summary>
        Right
    }
}
