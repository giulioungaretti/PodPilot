// WinPods Device Communication - C# Implementation
// Core error handling types
// File: Core/BluetoothError.cs

namespace DeviceCommunication.Core
{
    /// <summary>
    /// Represents the types of errors that can occur during Bluetooth operations.
    /// </summary>
    public enum BluetoothError
    {
        /// <summary>
        /// The requested Bluetooth device could not be found.
        /// </summary>
        DeviceNotFound,

        /// <summary>
        /// A required device property could not be retrieved.
        /// </summary>
        PropertyNotFound,

        /// <summary>
        /// An error occurred in the Windows Bluetooth API.
        /// </summary>
        WindowsError
    }
}
