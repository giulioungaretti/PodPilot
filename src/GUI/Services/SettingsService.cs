using System.Text.Json;
using Windows.Storage;

namespace GUI.Services;

/// <summary>
/// Service for managing application settings and saved devices.
/// </summary>
public class SettingsService
{
    private const string SAVED_DEVICE_KEY = "SavedAirPodsAddress";
    private readonly ApplicationDataContainer _localSettings;

    public SettingsService()
    {
        _localSettings = ApplicationData.Current.LocalSettings;
    }

    /// <summary>
    /// Gets the saved AirPods device address.
    /// </summary>
    public ulong? GetSavedDeviceAddress()
    {
        if (_localSettings.Values.TryGetValue(SAVED_DEVICE_KEY, out var value))
        {
            if (value is ulong address)
                return address;
        }
        return null;
    }

    /// <summary>
    /// Saves an AirPods device address.
    /// </summary>
    public void SaveDeviceAddress(ulong address)
    {
        _localSettings.Values[SAVED_DEVICE_KEY] = address;
    }

    /// <summary>
    /// Clears the saved device.
    /// </summary>
    public void ClearSavedDevice()
    {
        _localSettings.Values.Remove(SAVED_DEVICE_KEY);
    }

    /// <summary>
    /// Checks if a device is saved.
    /// </summary>
    public bool HasSavedDevice() => GetSavedDeviceAddress().HasValue;
}
