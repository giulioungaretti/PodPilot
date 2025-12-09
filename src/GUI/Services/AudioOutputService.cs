using System.Threading.Tasks;
using DeviceCommunication.Services;
using PodPilot.Core.Services;

namespace GUI.Services;

/// <summary>
/// Implementation of <see cref="IAudioOutputService"/> that uses Win32BluetoothConnector.
/// </summary>
public sealed class AudioOutputService : IAudioOutputService
{
    /// <inheritdoc />
    public Task<bool> IsDefaultAudioOutputAsync(ulong bluetoothAddress)
    {
        return Win32BluetoothConnector.IsDefaultAudioOutputAsync(bluetoothAddress);
    }
}
