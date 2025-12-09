namespace DeviceCommunication.Services;

/// <summary>
/// Defines the strategy to use when connecting to Bluetooth audio devices.
/// </summary>
public enum ConnectionStrategy
{
    /// <summary>
    /// Simple strategy using only the AudioPlaybackConnection API (Windows 10 2004+).
    /// This is the cleanest approach and matches what Windows Settings uses.
    /// Best for modern devices and Windows versions.
    /// </summary>
    Simple,

    /// <summary>
    /// Full strategy using multiple fallback approaches including:
    /// - AudioPlaybackConnection (primary)
    /// - BluetoothSetServiceState Win32 API (fallback)
    /// - RFCOMM connection trick (for devices connected elsewhere)
    /// - WinRT property access (last resort)
    /// Best for maximum compatibility across devices and Windows versions.
    /// </summary>
    Full
}
