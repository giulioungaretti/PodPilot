// Device Communication - C# Implementation
// Bluetooth adapter utility functions
// File: AdapterUtils.cs

using Windows.Devices.Bluetooth;
using Windows.Devices.Radios;

namespace CLI.Services
{
    /// <summary>
    /// Provides utility methods for interacting with the system's Bluetooth adapter.
    /// </summary>
    public static class AdapterUtils
    {
        /// <summary>
        /// Gets the default Bluetooth adapter's radio interface.
        /// </summary>
        /// <returns>
        /// The <see cref="Radio"/> interface for the Bluetooth adapter, 
        /// or <c>null</c> if no Bluetooth adapter is available.
        /// </returns>
        /// <remarks>
        /// This method synchronously blocks while retrieving the adapter.
        /// Consider using async/await patterns in production code if this becomes a bottleneck.
        /// </remarks>
        public static Radio? GetBluetoothAdapterRadio()
        {
            try
            {
                var adapter = BluetoothAdapter.GetDefaultAsync().AsTask().Result;
                if (adapter == null) return null;
                return adapter.GetRadioAsync().AsTask().Result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Determines whether the Bluetooth adapter is currently powered on.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the Bluetooth adapter is on; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsAdapterOn()
        {
            var radio = GetBluetoothAdapterRadio();
            return radio?.State == RadioState.On;
        }

        /// <summary>
        /// Gets the current state of the Bluetooth adapter.
        /// </summary>
        /// <returns>The current <see cref="AdapterState"/> of the Bluetooth adapter.</returns>
        public static AdapterState GetAdapterState()
        {
            return IsAdapterOn() ? AdapterState.On : AdapterState.Off;
        }
    }
}
