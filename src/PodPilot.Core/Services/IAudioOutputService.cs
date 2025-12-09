namespace PodPilot.Core.Services;

/// <summary>
/// Provides audio output detection functionality.
/// Abstraction to allow ViewModels to check audio state without OS dependencies.
/// </summary>
public interface IAudioOutputService
{
    /// <summary>
    /// Checks if a Bluetooth device is the current default audio output.
    /// </summary>
    /// <param name="bluetoothAddress">The Bluetooth MAC address.</param>
    /// <returns>True if the device is the default audio output; otherwise, false.</returns>
    Task<bool> IsDefaultAudioOutputAsync(ulong bluetoothAddress);
}
