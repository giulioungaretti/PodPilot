using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.UI.Dispatching;
using DeviceCommunication.Advertisement;
using DeviceCommunication.Models;
using DeviceCommunication.Services;
using GUI.Services;
using GUI.ViewModels;
using Moq;

namespace GUI.Tests.ViewModels;

[TestClass]
public class MainPageViewModelTests
{
    [TestCleanup]
    public void Cleanup()
    {
        var settingsService = new SettingsService();
        settingsService.ClearSavedDevice();
    }

    [TestMethod]
    public void Initialize_WithSavedDevice_SetsHasSavedDeviceTrue()
    {
        var settingsService = new SettingsService();
        var mockWatcher = new Mock<IAdvertisementWatcher>();
        var discoveryService = new AirPodsDiscoveryService(mockWatcher.Object);
        var dispatcherQueue = CreateMockDispatcherQueue();
        var viewModel = new MainPageViewModel(discoveryService, settingsService, dispatcherQueue);

        settingsService.SaveDeviceAddress(0x123456789ABC);

        viewModel.Initialize();

        Assert.IsTrue(viewModel.HasSavedDevice);

        viewModel.Dispose();
        discoveryService.Dispose();
    }

    [TestMethod]
    public void Initialize_WithoutSavedDevice_SetsHasSavedDeviceFalse()
    {
        var settingsService = new SettingsService();
        var mockWatcher = new Mock<IAdvertisementWatcher>();
        var discoveryService = new AirPodsDiscoveryService(mockWatcher.Object);
        var dispatcherQueue = CreateMockDispatcherQueue();
        var viewModel = new MainPageViewModel(discoveryService, settingsService, dispatcherQueue);

        settingsService.ClearSavedDevice();

        viewModel.Initialize();

        Assert.IsFalse(viewModel.HasSavedDevice);

        viewModel.Dispose();
        discoveryService.Dispose();
    }

    [TestMethod]
    public void SaveDevice_SetsDeviceAsSaved()
    {
        var settingsService = new SettingsService();
        var mockWatcher = new Mock<IAdvertisementWatcher>();
        var discoveryService = new AirPodsDiscoveryService(mockWatcher.Object);
        var dispatcherQueue = CreateMockDispatcherQueue();
        var viewModel = new MainPageViewModel(discoveryService, settingsService, dispatcherQueue);

        var device = CreateMockDevice(0x123456789ABC);

        viewModel.SaveDeviceCommand.Execute(device);

        Assert.IsTrue(device.IsSaved);
        Assert.IsTrue(viewModel.HasSavedDevice);
        Assert.AreEqual(device, viewModel.SavedDevice);

        viewModel.Dispose();
        discoveryService.Dispose();
    }

    [TestMethod]
    public void ForgetDevice_ClearsSavedDevice()
    {
        var settingsService = new SettingsService();
        var mockWatcher = new Mock<IAdvertisementWatcher>();
        var discoveryService = new AirPodsDiscoveryService(mockWatcher.Object);
        var dispatcherQueue = CreateMockDispatcherQueue();
        var viewModel = new MainPageViewModel(discoveryService, settingsService, dispatcherQueue);

        var device = CreateMockDevice(0x123456789ABC);
        viewModel.SaveDeviceCommand.Execute(device);

        viewModel.ForgetDeviceCommand.Execute(null);

        Assert.IsFalse(viewModel.HasSavedDevice);
        Assert.IsNull(viewModel.SavedDevice);

        viewModel.Dispose();
        discoveryService.Dispose();
    }

    [TestMethod]
    public void StartScanning_SetsIsScanningTrue()
    {
        var settingsService = new SettingsService();
        var mockWatcher = new Mock<IAdvertisementWatcher>();
        var discoveryService = new AirPodsDiscoveryService(mockWatcher.Object);
        var dispatcherQueue = CreateMockDispatcherQueue();
        var viewModel = new MainPageViewModel(discoveryService, settingsService, dispatcherQueue);

        viewModel.StartScanningCommand.Execute(null);

        Assert.IsTrue(viewModel.IsScanning);

        viewModel.Dispose();
        discoveryService.Dispose();
    }

    [TestMethod]
    public void StopScanning_SetsIsScanningFalse()
    {
        var settingsService = new SettingsService();
        var mockWatcher = new Mock<IAdvertisementWatcher>();
        var discoveryService = new AirPodsDiscoveryService(mockWatcher.Object);
        var dispatcherQueue = CreateMockDispatcherQueue();
        var viewModel = new MainPageViewModel(discoveryService, settingsService, dispatcherQueue);

        viewModel.StartScanningCommand.Execute(null);

        viewModel.StopScanningCommand.Execute(null);

        Assert.IsFalse(viewModel.IsScanning);

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
