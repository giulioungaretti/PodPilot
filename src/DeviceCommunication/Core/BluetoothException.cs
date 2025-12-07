// WinPods Device Communication - C# Implementation
// Core exception handling
// File: Core/BluetoothException.cs

using System;

namespace DeviceCommunication.Core
{
    /// <summary>
    /// Exception thrown when a Bluetooth operation fails.
    /// </summary>
    public class BluetoothException : Exception
    {
        /// <summary>
        /// Gets the specific Bluetooth error that occurred.
        /// </summary>
        public BluetoothError Error { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BluetoothException"/> class.
        /// </summary>
        /// <param name="error">The Bluetooth error that occurred.</param>
        public BluetoothException(BluetoothError error)
            : base(GetErrorMessage(error))
        {
            Error = error;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BluetoothException"/> class with an inner exception.
        /// </summary>
        /// <param name="error">The Bluetooth error that occurred.</param>
        /// <param name="innerException">The exception that caused this exception.</param>
        public BluetoothException(BluetoothError error, Exception innerException)
            : base(GetErrorMessage(error), innerException)
        {
            Error = error;
        }

        /// <summary>
        /// Gets a human-readable error message for the specified error type.
        /// </summary>
        /// <param name="error">The Bluetooth error.</param>
        /// <returns>A descriptive error message.</returns>
        private static string GetErrorMessage(BluetoothError error) => error switch
        {
            BluetoothError.DeviceNotFound => "Bluetooth device was not found",
            BluetoothError.DeviceNotPaired => "Bluetooth device is not paired with this system",
            BluetoothError.PropertyNotFound => "Device property could not be retrieved",
            BluetoothError.WindowsError => "Windows API error occurred",
            _ => "Unknown Bluetooth error"
        };
    }
}
