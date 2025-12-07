using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;

namespace DeviceCommunication.Services;

public static class BluetoothDeviceHelper
{
    // Audio/Video major class
    private const byte MajorClassAudioVideo = 0x04;

    // Minor classes for audio devices within Audio/Video major class
    private const byte MinorClassHeadset = 0x04;
    private const byte MinorClassHandsFree = 0x08;
    private const byte MinorClassMicrophone = 0x10;
    private const byte MinorClassLoudspeaker = 0x14;
    private const byte MinorClassHeadphones = 0x18;
    private const byte MinorClassPortableAudio = 0x1C;
    private const byte MinorClassCarAudio = 0x20;
    private const byte MinorClassHiFiAudio = 0x28;

    // Well-known audio-related RFCOMM service UUIDs
    private static readonly Guid A2dpSinkUuid = new("0000110b-0000-1000-8000-00805f9b34fb");
    private static readonly Guid A2dpSourceUuid = new("0000110a-0000-1000-8000-00805f9b34fb");
    private static readonly Guid HandsFreeUuid = new("0000111e-0000-1000-8000-00805f9b34fb");
    private static readonly Guid HeadsetUuid = new("00001108-0000-1000-8000-00805f9b34fb");
    private static readonly Guid AvrcpTargetUuid = new("0000110c-0000-1000-8000-00805f9b34fb");
    private static readonly Guid AvrcpControllerUuid = new("0000110e-0000-1000-8000-00805f9b34fb");

    public static bool IsAudioDevice(BluetoothDevice device)
    {
        return IsAudioDeviceByClass(device) || HasAudioServices(device.RfcommServices);
    }

    public static bool IsHeadphones(BluetoothDevice device)
    {
        var cod = device.ClassOfDevice;
        byte majorClass = (byte)((cod.RawValue >> 8) & 0x1F);
        byte minorClass = (byte)((cod.RawValue >> 2) & 0x3F);

        if (majorClass == MajorClassAudioVideo)
        {
            return minorClass == MinorClassHeadphones ||
                   minorClass == MinorClassHeadset ||
                   minorClass == MinorClassPortableAudio;
        }

        return false;
    }

    public static bool IsAudioDeviceByClass(BluetoothDevice device)
    {
        var cod = device.ClassOfDevice;
        byte majorClass = (byte)((cod.RawValue >> 8) & 0x1F);

        return majorClass == MajorClassAudioVideo;
    }

    public static bool HasAudioServices(IReadOnlyList<RfcommDeviceService> services)
    {
        if (services == null)
            return false;

        foreach (var service in services)
        {
            var uuid = service.ServiceId.Uuid;
            if (uuid == A2dpSinkUuid || uuid == A2dpSourceUuid ||
                uuid == HandsFreeUuid || uuid == HeadsetUuid ||
                uuid == AvrcpTargetUuid || uuid == AvrcpControllerUuid)
            {
                return true;
            }
        }

        return false;
    }

    public static async Task<AudioDeviceInfo> GetAudioDeviceInfoAsync(BluetoothDevice device)
    {
        var info = new AudioDeviceInfo
        {
            Name = device.Name,
            BluetoothAddress = device.BluetoothAddress,
            IsConnected = device.ConnectionStatus == BluetoothConnectionStatus.Connected,
            DeviceId = device.DeviceId
        };

        // Parse Class of Device
        var cod = device.ClassOfDevice;
        info.MajorClass = (byte)((cod.RawValue >> 8) & 0x1F);
        info.MinorClass = (byte)((cod.RawValue >> 2) & 0x3F);
        info.MajorClassName = GetMajorClassName(info.MajorClass);
        info.MinorClassName = GetMinorClassName(info.MajorClass, info.MinorClass);
        info.IsAudioDevice = info.MajorClass == MajorClassAudioVideo;
        info.IsHeadphones = IsHeadphones(device);

        // Get RFCOMM services
        try
        {
            var servicesResult = await device.GetRfcommServicesAsync();
            if (servicesResult.Error == BluetoothError.Success)
            {
                info.AudioServices = [];
                foreach (var service in servicesResult.Services)
                {
                    var serviceName = GetKnownServiceName(service.ServiceId.Uuid);
                    info.AudioServices.Add((service.ServiceId.Uuid, serviceName));
                }
            }
        }
        catch
        {
            // Services may not be available
        }

        return info;
    }

    public static void PrintDeviceDebugInfo(BluetoothDevice device)
    {
        Debug.WriteLine("=== Bluetooth Device Debug Info ===");
        Debug.WriteLine($"Name: {device.Name}");
        Debug.WriteLine($"Device ID: {device.DeviceId}");
        Debug.WriteLine($"Bluetooth Address: {device.BluetoothAddress:X12}");
        Debug.WriteLine($"Connection Status: {device.ConnectionStatus}");

        // Class of Device
        var cod = device.ClassOfDevice;
        byte majorClass = (byte)((cod.RawValue >> 8) & 0x1F);
        byte minorClass = (byte)((cod.RawValue >> 2) & 0x3F);
        Debug.WriteLine($"Class of Device Raw: 0x{cod.RawValue:X6}");
        Debug.WriteLine($"Major Class: {GetMajorClassName(majorClass)} (0x{majorClass:X2})");
        Debug.WriteLine($"Minor Class: {GetMinorClassName(majorClass, minorClass)} (0x{minorClass:X2})");
        Debug.WriteLine($"Is Audio Device: {majorClass == MajorClassAudioVideo}");
        Debug.WriteLine($"Is Headphones: {IsHeadphones(device)}");

        // DeviceInformation properties
        var deviceInfo = device.DeviceInformation;
        if (deviceInfo != null)
        {
            Debug.WriteLine("--- DeviceInformation Properties ---");
            Debug.WriteLine($"  Display Name: {deviceInfo.Name}");
            Debug.WriteLine($"  Is Enabled: {deviceInfo.IsEnabled}");
            Debug.WriteLine($"  Is Default: {deviceInfo.IsDefault}");
            Debug.WriteLine($"  Kind: {deviceInfo.Kind}");

            if (deviceInfo.Pairing != null)
            {
                Debug.WriteLine($"  Is Paired: {deviceInfo.Pairing.IsPaired}");
                Debug.WriteLine($"  Can Pair: {deviceInfo.Pairing.CanPair}");
                Debug.WriteLine($"  Protection Level: {deviceInfo.Pairing.ProtectionLevel}");
            }

            if (deviceInfo.Properties != null)
            {
                Debug.WriteLine("  --- Extended Properties ---");
                foreach (var prop in deviceInfo.Properties)
                {
                    Debug.WriteLine($"    {prop.Key}: {prop.Value}");
                }
            }
        }

        // RFCOMM Services (cached)
        var services = device.RfcommServices;
        if (services != null && services.Count > 0)
        {
            Debug.WriteLine("--- RFCOMM Services (Cached) ---");
            foreach (var service in services)
            {
                var serviceName = GetKnownServiceName(service.ServiceId.Uuid);
                Debug.WriteLine($"  {serviceName}: {service.ServiceId.Uuid}");
            }
        }

        Debug.WriteLine("===================================");
    }

    private static string GetMajorClassName(byte majorClass)
    {
        return majorClass switch
        {
            0x00 => "Miscellaneous",
            0x01 => "Computer",
            0x02 => "Phone",
            0x03 => "LAN/Network Access",
            0x04 => "Audio/Video",
            0x05 => "Peripheral",
            0x06 => "Imaging",
            0x07 => "Wearable",
            0x08 => "Toy",
            0x09 => "Health",
            0x1F => "Uncategorized",
            _ => $"Unknown (0x{majorClass:X2})"
        };
    }

    private static string GetMinorClassName(byte majorClass, byte minorClass)
    {
        if (majorClass == MajorClassAudioVideo)
        {
            return minorClass switch
            {
                0x00 => "Uncategorized",
                0x01 => "Wearable Headset",
                0x02 => "Hands-free Device",
                0x04 => "Headset",
                0x08 => "Hands-Free",
                0x10 => "Microphone",
                0x14 => "Loudspeaker",
                0x18 => "Headphones",
                0x1C => "Portable Audio",
                0x20 => "Car Audio",
                0x24 => "Set-top Box",
                0x28 => "HiFi Audio",
                0x2C => "VCR",
                0x30 => "Video Camera",
                0x34 => "Camcorder",
                0x38 => "Video Monitor",
                0x3C => "Video Display and Loudspeaker",
                _ => $"Unknown Audio (0x{minorClass:X2})"
            };
        }

        return $"0x{minorClass:X2}";
    }

    private static string GetKnownServiceName(Guid uuid)
    {
        if (uuid == A2dpSinkUuid) return "A2DP Sink (Audio Receiver)";
        if (uuid == A2dpSourceUuid) return "A2DP Source (Audio Sender)";
        if (uuid == HandsFreeUuid) return "Hands-Free";
        if (uuid == HeadsetUuid) return "Headset";
        if (uuid == AvrcpTargetUuid) return "AVRCP Target";
        if (uuid == AvrcpControllerUuid) return "AVRCP Controller";
        return "Unknown Service";
    }
}

public class AudioDeviceInfo
{
    public string Name { get; set; } = string.Empty;
    public ulong BluetoothAddress { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public byte MajorClass { get; set; }
    public byte MinorClass { get; set; }
    public string MajorClassName { get; set; } = string.Empty;
    public string MinorClassName { get; set; } = string.Empty;
    public bool IsAudioDevice { get; set; }
    public bool IsHeadphones { get; set; }
    public List<(Guid Uuid, string Name)> AudioServices { get; set; } = [];

    public override string ToString()
    {
        return $"{Name} [{MinorClassName}] - {(IsConnected ? "Connected" : "Disconnected")}";
    }
}