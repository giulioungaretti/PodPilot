using Microsoft.VisualStudio.TestTools.UnitTesting;
using GUI.Services;

namespace GUI.Tests.Services;

[TestClass]
public class SettingsServiceTests
{
    private SettingsService? _settingsService;

    [TestInitialize]
    public void Setup()
    {
        _settingsService = new SettingsService();
        // Clear any existing saved device
        _settingsService.ClearSavedDevice();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _settingsService?.ClearSavedDevice();
    }

    [TestMethod]
    public void SaveDeviceAddress_SavesAddress()
    {
        // Arrange
        var address = 0x123456789ABCUL;

        // Act
        _settingsService!.SaveDeviceAddress(address);
        var savedAddress = _settingsService.GetSavedDeviceAddress();

        // Assert
        Assert.IsNotNull(savedAddress);
        Assert.AreEqual(address, savedAddress.Value);
    }

    [TestMethod]
    public void GetSavedDeviceAddress_ReturnsNullWhenNoDeviceSaved()
    {
        // Arrange
        _settingsService!.ClearSavedDevice();

        // Act
        var savedAddress = _settingsService.GetSavedDeviceAddress();

        // Assert
        Assert.IsNull(savedAddress);
    }

    [TestMethod]
    public void ClearSavedDevice_RemovesSavedAddress()
    {
        // Arrange
        var address = 0x123456789ABCUL;
        _settingsService!.SaveDeviceAddress(address);

        // Act
        _settingsService.ClearSavedDevice();
        var savedAddress = _settingsService.GetSavedDeviceAddress();

        // Assert
        Assert.IsNull(savedAddress);
    }

    [TestMethod]
    public void HasSavedDevice_ReturnsTrueWhenDeviceSaved()
    {
        // Arrange
        var address = 0x123456789ABCUL;
        _settingsService!.SaveDeviceAddress(address);

        // Act
        var hasSavedDevice = _settingsService.HasSavedDevice();

        // Assert
        Assert.IsTrue(hasSavedDevice);
    }

    [TestMethod]
    public void HasSavedDevice_ReturnsFalseWhenNoDeviceSaved()
    {
        // Arrange
        _settingsService!.ClearSavedDevice();

        // Act
        var hasSavedDevice = _settingsService.HasSavedDevice();

        // Assert
        Assert.IsFalse(hasSavedDevice);
    }

    [TestMethod]
    public void SaveDeviceAddress_OverwritesPreviousAddress()
    {
        // Arrange
        var firstAddress = 0x111111111111UL;
        var secondAddress = 0x222222222222UL;

        // Act
        _settingsService!.SaveDeviceAddress(firstAddress);
        _settingsService.SaveDeviceAddress(secondAddress);
        var savedAddress = _settingsService.GetSavedDeviceAddress();

        // Assert
        Assert.IsNotNull(savedAddress);
        Assert.AreEqual(secondAddress, savedAddress.Value);
    }
}
