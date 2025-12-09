namespace PodPilot.Core.Services;

/// <summary>
/// Provides audio output detection and control functionality.
/// Abstraction to allow ViewModels to check and set audio state without OS dependencies.
/// </summary>
public interface IAudioOutputService
{
    /// <summary>
    /// Checks if a Bluetooth device is the current default audio output.
    /// </summary>
    /// <param name="bluetoothAddress">The Bluetooth MAC address.</param>
    /// <returns>True if the device is the default audio output; otherwise, false.</returns>
    Task<bool> IsDefaultAudioOutputAsync(ulong bluetoothAddress);

    /// <summary>
    /// Sets a Bluetooth device as the default audio output.
    /// </summary>
    /// <param name="bluetoothAddress">The Bluetooth MAC address.</param>
    /// <returns>True if the device was successfully set as default; otherwise, false.</returns>
    /// <remarks>
    /// This uses the undocumented IPolicyConfig COM interface, which works on Windows 7-11
    /// but is not officially supported by Microsoft and may break in future updates.
    /// </remarks>
    Task<bool> SetDefaultAudioOutputAsync(ulong bluetoothAddress);
}
