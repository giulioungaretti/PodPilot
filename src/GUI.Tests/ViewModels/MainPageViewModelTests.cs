using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.UI.Dispatching;
using GUI.Models;
using GUI.Services;
using GUI.ViewModels;
using Moq;

namespace GUI.Tests.ViewModels;

/// <summary>
/// Integration tests for MainPageViewModel.
/// NOTE: These are integration tests, not true unit tests, because services lack interfaces.
/// TODO: Refactor services to use interfaces (ISettingsService, IAirPodsDiscoveryService) for proper unit testing.
/// </summary>
[TestClass]
public class MainPageViewModelTests
{
    [TestCleanup]
    public void Cleanup()
    {
        // Clean up any persisted settings to avoid test pollution
        var settingsService = new SettingsService();
        settingsService.ClearSavedDevice();
    }

    [TestMethod]
    public void Initialize_WithSavedDevice_SetsHasSavedDeviceTrue()
    {
        // Arrange
        var settingsService = new SettingsService();
        var discoveryService = new AirPodsDiscoveryService();
        var dispatcherQueue = CreateMockDispatcherQueue();
        var viewModel = new MainPageViewModel(discoveryService, settingsService, dispatcherQueue);

        settingsService.SaveDeviceAddress(0x123456789ABC);

        // Act
        viewModel.Initialize();

        // Assert
        Assert.IsTrue(viewModel.HasSavedDevice);

        // Cleanup
        viewModel.Dispose();
        discoveryService.Dispose();
    }

    [TestMethod]
    public void Initialize_WithoutSavedDevice_SetsHasSavedDeviceFalse()
    {
        // Arrange
        var settingsService = new SettingsService();
        var discoveryService = new AirPodsDiscoveryService();
        var dispatcherQueue = CreateMockDispatcherQueue();
        var viewModel = new MainPageViewModel(discoveryService, settingsService, dispatcherQueue);

        settingsService.ClearSavedDevice();

        // Act
        viewModel.Initialize();

        // Assert
        Assert.IsFalse(viewModel.HasSavedDevice);

        // Cleanup
        viewModel.Dispose();
        discoveryService.Dispose();
    }

    [TestMethod]
    public void SaveDevice_SetsDeviceAsSaved()
    {
        // Arrange
        var settingsService = new SettingsService();
        var discoveryService = new AirPodsDiscoveryService();
        var dispatcherQueue = CreateMockDispatcherQueue();
        var viewModel = new MainPageViewModel(discoveryService, settingsService, dispatcherQueue);

        var device = CreateMockDevice(0x123456789ABC);

        // Act
        viewModel.SaveDeviceCommand.Execute(device);

        // Assert
        Assert.IsTrue(device.IsSaved);
        Assert.IsTrue(viewModel.HasSavedDevice);
        Assert.AreEqual(device, viewModel.SavedDevice);

        // Cleanup
        viewModel.Dispose();
        discoveryService.Dispose();
    }

    [TestMethod]
    public void ForgetDevice_ClearsSavedDevice()
    {
        // Arrange
        var settingsService = new SettingsService();
        var discoveryService = new AirPodsDiscoveryService();
        var dispatcherQueue = CreateMockDispatcherQueue();
        var viewModel = new MainPageViewModel(discoveryService, settingsService, dispatcherQueue);

        var device = CreateMockDevice(0x123456789ABC);
        viewModel.SaveDeviceCommand.Execute(device);

        // Act
        viewModel.ForgetDeviceCommand.Execute(null);

        // Assert
        Assert.IsFalse(viewModel.HasSavedDevice);
        Assert.IsNull(viewModel.SavedDevice);

        // Cleanup
        viewModel.Dispose();
        discoveryService.Dispose();
    }

    [TestMethod]
    public void StartScanning_SetsIsScanningTrue()
    {
        // Arrange
        var settingsService = new SettingsService();
        var discoveryService = new AirPodsDiscoveryService();
        var dispatcherQueue = CreateMockDispatcherQueue();
        var viewModel = new MainPageViewModel(discoveryService, settingsService, dispatcherQueue);

        // Act
        viewModel.StartScanningCommand.Execute(null);

        // Assert
        Assert.IsTrue(viewModel.IsScanning);

        // Cleanup
        viewModel.Dispose();
        discoveryService.Dispose();
    }

    [TestMethod]
    public void StopScanning_SetsIsScanningFalse()
    {
        // Arrange
        var settingsService = new SettingsService();
        var discoveryService = new AirPodsDiscoveryService();
        var dispatcherQueue = CreateMockDispatcherQueue();
        var viewModel = new MainPageViewModel(discoveryService, settingsService, dispatcherQueue);

        viewModel.StartScanningCommand.Execute(null);

        // Act
        viewModel.StopScanningCommand.Execute(null);

        // Assert
        Assert.IsFalse(viewModel.IsScanning);

        // Cleanup
        viewModel.Dispose();
        discoveryService.Dispose();
    }

    private AirPodsDeviceInfo CreateMockDevice(ulong address)
    {
        return new AirPodsDeviceInfo
        {
            Address = address,
            Model = "AirPods Pro (2nd generation)",
            DeviceName = "Test AirPods",
            LeftBattery = 85,
            RightBattery = 80,
            CaseBattery = 90,
            IsLeftCharging = false,
            IsRightCharging = false,
            IsCaseCharging = true,
            IsLeftInEar = false,
            IsRightInEar = false,
            IsLidOpen = true,
            IsConnected = true,
            SignalStrength = -45,
            LastSeen = DateTime.Now,
            IsSaved = false
        };
    }

    private DispatcherQueue CreateMockDispatcherQueue()
    {
        var mockQueue = new Mock<DispatcherQueue>();
        mockQueue.Setup(q => q.TryEnqueue(It.IsAny<DispatcherQueueHandler>()))
                 .Returns((DispatcherQueueHandler callback) =>
                 {
                     callback.Invoke();
                     return true;
                 });
        return mockQueue.Object;
    }
}
