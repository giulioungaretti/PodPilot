using DeviceCommunication.Apple;
using Windows.Storage;

namespace GUI.Services;

/// <summary>
/// Service for managing application settings and saved devices.
/// </summary>
public class SettingsService
{
    private const string SAVED_DEVICE_PRODUCT_ID_KEY = "SavedAirPodsProductId";
    private const string SAVED_DEVICE_NAME_KEY = "SavedAirPodsDeviceName";
    private readonly ApplicationDataContainer _localSettings;

    public SettingsService()
    {
        _localSettings = ApplicationData.Current.LocalSettings;
    }

    /// <summary>
    /// Gets the saved AirPods device product ID.
    /// </summary>
    private ushort? GetSavedDeviceProductId()
    {
        if (_localSettings.Values.TryGetValue(SAVED_DEVICE_PRODUCT_ID_KEY, out var value))
        {
            if (value is ushort productId)
                return productId;
            // Handle conversion from stored int
            if (value is int intValue && intValue >= 0 && intValue <= ushort.MaxValue)
                return (ushort)intValue;
        }
        return null;
    }

    /// <summary>
    /// Gets the saved AirPods device name (for display when multiple devices of same model exist).
    /// </summary>
    public string? GetSavedDeviceName()
    {
        if (_localSettings.Values.TryGetValue(SAVED_DEVICE_NAME_KEY, out var value))
        {
            if (value is string name)
                return name;
        }
        return null;
    }

    /// <summary>
    /// Saves an AirPods device by Product ID and optional device name.
    /// </summary>
    private void SaveDevice(ushort productId, string? deviceName = null)
    {
        _localSettings.Values[SAVED_DEVICE_PRODUCT_ID_KEY] = productId;
        if (!string.IsNullOrEmpty(deviceName))
        {
            _localSettings.Values[SAVED_DEVICE_NAME_KEY] = deviceName;
        }
    }

    /// <summary>
    /// Saves an AirPods device by model enum.
    /// </summary>
    public void SaveDeviceByModel(AppleDeviceModel model, string? deviceName = null)
    {
        var productId = GetProductIdFromModel(model);
        if (productId.HasValue)
        {
            SaveDevice(productId.Value, deviceName);
        }
    }

    /// <summary>
    /// Clears the saved device.
    /// </summary>
    public void ClearSavedDevice()
    {
        _localSettings.Values.Remove(SAVED_DEVICE_PRODUCT_ID_KEY);
        _localSettings.Values.Remove(SAVED_DEVICE_NAME_KEY);
    }

    /// <summary>
    /// Checks if a device is saved.
    /// </summary>
    public bool HasSavedDevice() => GetSavedDeviceProductId().HasValue;

    /// <summary>
    /// Gets the saved device model.
    /// </summary>
    public AppleDeviceModel? GetSavedDeviceModel()
    {
        var productId = GetSavedDeviceProductId();
        if (!productId.HasValue)
            return null;

        var model = AppleDeviceModelHelper.GetModel(productId.Value);
        return model == AppleDeviceModel.Unknown ? null : model;
    }

    /// <summary>
    /// Converts AppleDeviceModel to Product ID.
    /// </summary>
    private static ushort? GetProductIdFromModel(AppleDeviceModel model)
    {
        return model switch
        {
            AppleDeviceModel.AirPods1 => 0x2002,
            AppleDeviceModel.AirPods2 => 0x200F,
            AppleDeviceModel.AirPods3 => 0x2013,
            AppleDeviceModel.AirPodsPro => 0x200E,
            AppleDeviceModel.AirPodsPro2 => 0x2014,
            AppleDeviceModel.AirPodsPro2UsbC => 0x2024,
            AppleDeviceModel.AirPodsMax => 0x200A,
            AppleDeviceModel.BeatsFitPro => 0x2012,
            _ => null
        };
    }
}
