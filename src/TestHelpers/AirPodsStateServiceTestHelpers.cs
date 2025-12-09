using DeviceCommunication.Apple;
using DeviceCommunication.Models;
using DeviceCommunication.Services;
using NSubstitute;
using System;

namespace TestHelpers;

/// <summary>
/// Helper methods for testing AirPodsStateService.
/// Provides factory methods for creating test data and simulating device events.
/// </summary>
public static class AirPodsStateServiceTestHelpers
{
    // Test data constants
    public const ushort AirPodsPro2ProductId = 0x2014;
    public const ushort AirPods2ProductId = 0x200F;
    public const string TestDeviceName = "Test AirPods Pro";
    public const ulong TestBluetoothAddress = 0xAABBCCDDEEFF;
    public const ulong TestBleAddress = 0x112233445566;

    /// <summary>
    /// Creates a test paired device with default or custom values.
    /// </summary>
    public static PairedDeviceInfo CreatePairedDevice(
        ushort productId = AirPodsPro2ProductId,
        string? name = null,
        ulong? address = null,
        string? id = null,
        bool isConnected = false)
    {
        return new PairedDeviceInfo
        {
            Id = id ?? "device-id-123",
            ProductId = productId,
            Name = name ?? TestDeviceName,
            Address = address ?? TestBluetoothAddress,
            IsConnected = isConnected
        };
    }

    /// <summary>
    /// Creates test BLE enrichment data with default or custom values.
    /// </summary>
    public static BleEnrichmentData CreateBleData(
        ushort productId = AirPodsPro2ProductId,
        ulong? bleAddress = null,
        int leftBattery = 80,
        int rightBattery = 90,
        int caseBattery = 100,
        bool isLeftCharging = false,
        bool isRightCharging = false,
        bool isCaseCharging = false,
        bool isLeftInEar = false,
        bool isRightInEar = false,
        bool isLidOpen = true,
        bool isBothPodsInCase = true,
        int signalStrength = -50)
    {
        return new BleEnrichmentData
        {
            ProductId = productId,
            BleAddress = bleAddress ?? TestBleAddress,
            ModelName = AppleDeviceModelHelper.GetDisplayName(AppleDeviceModel.AirPodsPro2),
            LeftBattery = leftBattery,
            RightBattery = rightBattery,
            CaseBattery = caseBattery,
            IsLeftCharging = isLeftCharging,
            IsRightCharging = isRightCharging,
            IsCaseCharging = isCaseCharging,
            IsLeftInEar = isLeftInEar,
            IsRightInEar = isRightInEar,
            IsLidOpen = isLidOpen,
            IsBothPodsInCase = isBothPodsInCase,
            SignalStrength = (short)signalStrength,
            LastUpdate = DateTime.Now
        };
    }

    /// <summary>
    /// Simulates a paired device being added by raising the appropriate event.
    /// </summary>
    public static void RaisePairedDeviceAdded(
        IPairedDeviceWatcher pairedDeviceWatcher,
        PairedDeviceInfo device)
    {
        pairedDeviceWatcher.DeviceChanged += Raise.Event<EventHandler<PairedDeviceChangedEventArgs>>(
            pairedDeviceWatcher,
            new PairedDeviceChangedEventArgs
            {
                Device = device,
                ChangeType = PairedDeviceChangeType.Added
            });
    }

    /// <summary>
    /// Simulates a paired device being removed by raising the appropriate event.
    /// </summary>
    public static void RaisePairedDeviceRemoved(
        IPairedDeviceWatcher pairedDeviceWatcher,
        PairedDeviceInfo device)
    {
        pairedDeviceWatcher.DeviceChanged += Raise.Event<EventHandler<PairedDeviceChangedEventArgs>>(
            pairedDeviceWatcher,
            new PairedDeviceChangedEventArgs
            {
                Device = device,
                ChangeType = PairedDeviceChangeType.Removed
            });
    }

    /// <summary>
    /// Simulates a paired device being updated by raising the appropriate event.
    /// </summary>
    public static void RaisePairedDeviceUpdated(
        IPairedDeviceWatcher pairedDeviceWatcher,
        PairedDeviceInfo device)
    {
        pairedDeviceWatcher.DeviceChanged += Raise.Event<EventHandler<PairedDeviceChangedEventArgs>>(
            pairedDeviceWatcher,
            new PairedDeviceChangedEventArgs
            {
                Device = device,
                ChangeType = PairedDeviceChangeType.Updated
            });
    }

    /// <summary>
    /// Simulates BLE data being received by raising the appropriate event.
    /// </summary>
    public static void RaiseBleDataReceived(
        IBleDataProvider bleDataProvider,
        BleEnrichmentData data)
    {
        bleDataProvider.DataReceived += Raise.Event<EventHandler<BleEnrichmentData>>(
            bleDataProvider,
            data);
    }
}
