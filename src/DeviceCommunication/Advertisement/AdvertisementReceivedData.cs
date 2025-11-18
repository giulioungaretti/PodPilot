// Parsed BLE advertisement data

using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace DeviceCommunication.Advertisement
{
    /// <summary>
    /// Contains parsed data from a received BLE advertisement.
    /// </summary>
    public class AdvertisementReceivedData
    {
        /// <summary>
        /// Gets the received signal strength indicator (RSSI) in dBm.
        /// </summary>
        /// <value>
        /// The RSSI value, typically ranging from -100 (weak) to 0 (strong) dBm.
        /// </value>
        public short Rssi { get; init; }

        /// <summary>
        /// Gets the timestamp when the advertisement was received.
        /// </summary>
        public DateTimeOffset Timestamp { get; init; }

        /// <summary>
        /// Gets the Bluetooth address of the advertising device.
        /// </summary>
        /// <value>
        /// A 48-bit MAC address as a <see cref="ulong"/>.
        /// </value>
        public ulong Address { get; init; }

        /// <summary>
        /// Gets the manufacturer-specific data included in the advertisement.
        /// </summary>
        /// <value>
        /// A dictionary mapping company IDs to their raw data bytes.
        /// Company IDs are assigned by the Bluetooth SIG.
        /// </value>
        public Dictionary<ushort, byte[]> ManufacturerData { get; init; } = new();

        /// <summary>
        /// Creates an <see cref="AdvertisementReceivedData"/> instance from Windows BLE event arguments.
        /// </summary>
        /// <param name="args">The event arguments from a BLE advertisement received event.</param>
        /// <returns>A new <see cref="AdvertisementReceivedData"/> with parsed advertisement data.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="args"/> is null.</exception>
        public static AdvertisementReceivedData FromEventArgs(BluetoothLEAdvertisementReceivedEventArgs args)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));

            var data = new AdvertisementReceivedData
            {
                Rssi = args.RawSignalStrengthInDBm,
                Timestamp = args.Timestamp,
                Address = args.BluetoothAddress,
                ManufacturerData = new Dictionary<ushort, byte[]>()
            };

            foreach (var mfgData in args.Advertisement.ManufacturerData)
            {
                var companyId = mfgData.CompanyId;
                var buffer = mfgData.Data;
                var bytes = new byte[buffer.Length];
                using var reader = DataReader.FromBuffer(buffer);
                reader.ReadBytes(bytes);
                data.ManufacturerData[companyId] = bytes;
            }

            return data;
        }
    }
}
