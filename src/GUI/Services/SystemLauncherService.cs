using System;
using System.Threading.Tasks;
using PodPilot.Core.Services;

namespace GUI.Services;

/// <summary>
/// Implementation of <see cref="ISystemLauncherService"/> that uses Windows.System.Launcher.
/// </summary>
public sealed class SystemLauncherService : ISystemLauncherService
{
    /// <inheritdoc />
    public async Task<bool> OpenBluetoothSettingsAsync()
    {
        try
        {
            var uri = new Uri("ms-settings:bluetooth");
            return await global::Windows.System.Launcher.LaunchUriAsync(uri);
        }
        catch
        {
            return false;
        }
    }
}
