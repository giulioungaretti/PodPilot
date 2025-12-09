namespace PodPilot.Core.Services;

/// <summary>
/// Provides system launcher functionality for opening settings and URIs.
/// Abstraction to allow ViewModels to launch system UI without OS dependencies.
/// </summary>
public interface ISystemLauncherService
{
    /// <summary>
    /// Opens the Windows Bluetooth settings page.
    /// </summary>
    /// <returns>True if the settings page was launched successfully; otherwise, false.</returns>
    Task<bool> OpenBluetoothSettingsAsync();
}
