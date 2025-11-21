// WinPods Device Communication - C# Implementation
// Device connection state enumeration
// File: Device/DeviceConnectionState.cs

namespace DeviceCommunication.Device
{
    /// <summary>
    /// Represents the connection state of a Bluetooth device.
    /// </summary>
    public enum DeviceConnectionState
    {
        /// <summary>
        /// The device is connected and available for communication.
        /// </summary>
        Connected,

        /// <summary>
        /// The device is disconnected or out of range.
        /// </summary>
        Disconnected
    }
}
