using DeviceCommunication.Adapter;
using DeviceCommunication.Advertisement;
using DeviceCommunication.Apple;
using DeviceCommunication.Diagnostics;
using DeviceCommunication.Services;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace CLI;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("device communication - c# examples\n");

        while (true)
        {
            Console.WriteLine("\nselect an example:");
            Console.WriteLine("1. monitor bluetooth adapter state");
            Console.WriteLine("2. scan for ble advertisements");
            Console.WriteLine("3. scan for airpods");
            Console.WriteLine("4. connect to bluetooth device");
            Console.WriteLine("5. complete airpods monitor (basic)");
            Console.WriteLine("7. bluetooth diagnostics (debug connection issues)");
            Console.WriteLine("9. list all bluetooth devices with model info [RECOMMENDED]");
            Console.WriteLine("0. exit");
            Console.Write("\nchoice: ");

            var choice = Console.ReadLine();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    await Example1_MonitorAdapterState();
                    break;
                case "2":
                    await Example2_ScanAdvertisements();
                    break;
                case "3":
                    await Example3_ScanForAirPods();
                    break;
                case "4":
                    await Example4_ConnectToDevice();
                    break;
                case "5":
                    await Example5_CompleteAirPodsMonitor();
                    break;
                case "7":
                    await Example7_BluetoothDiagnostics();
                    break;
                case "9":
                    await Example9_ListDevicesWithModels();
                    break;
                case "0":
                    return;
            }
        }
    }

    /// <summary>
    /// example 1: monitor bluetooth adapter state changes
    /// </summary>
    static async Task Example1_MonitorAdapterState()
    {
        Console.WriteLine("=== monitoring bluetooth adapter state ===\n");

        // check initial state
        var initialState = AdapterUtils.GetAdapterState();
        Console.WriteLine($"initial adapter state: {initialState}");
        Console.WriteLine($"adapter is on: {AdapterUtils.IsAdapterOn()}\n");

        // create watcher with automatic disposal
        using var watcher = new AdapterWatcher();

        // subscribe to state changes
        watcher.StateChanged += (sender, state) =>
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] adapter state changed to: {state}");
        };

        // start monitoring
        watcher.Start();
        Console.WriteLine("monitoring adapter state. turn bluetooth on/off to see changes.");
        Console.WriteLine("press enter to stop...\n");
        Console.ReadLine();

        // watcher.Dispose() called automatically
        Console.WriteLine("stopped monitoring.");
    }

    /// <summary>
    /// example 2: scan for all ble advertisements
    /// </summary>
    static async Task Example2_ScanAdvertisements()
    {
        Console.WriteLine("=== scanning ble advertisements ===\n");

        using var watcher = new AdvertisementWatcher();
        var devicesSeen = new HashSet<ulong>();

        watcher.AdvertisementReceived += (sender, data) =>
        {
            // only show each device once
            if (!devicesSeen.Add(data.Address))
                return;

            Console.WriteLine($"device found:");
            Console.WriteLine($"  address: {data.Address:X12}");
            Console.WriteLine($"  rssi: {data.Rssi} dbm");
            Console.WriteLine($"  timestamp: {data.Timestamp:HH:mm:ss.fff}");
            Console.WriteLine($"  manufacturer data: {data.ManufacturerData.Count} entries");

            foreach (var (companyId, bytes) in data.ManufacturerData)
            {
                Console.WriteLine($"    company id: {companyId} (0x{companyId:X4}), data: {BitConverter.ToString(bytes)}");
            }
            Console.WriteLine();
        };

        watcher.Stopped += (sender, e) =>
        {
            Console.WriteLine("watcher stopped.");
        };

        watcher.Start();
        Console.WriteLine($"status: {watcher.Status}");
        Console.WriteLine("scanning for ble devices. press enter to stop...\n");
        Console.ReadLine();

        // watcher.Dispose() called automatically
        Console.WriteLine($"\ntotal devices found: {devicesSeen.Count}");
    }

    /// <summary>
    /// example 3: scan specifically for airpods and apple devices
    /// </summary>
    static async Task Example3_ScanForAirPods()
    {
        Console.WriteLine("=== scanning for airpods ===\n");

        using var watcher = new AdvertisementWatcher();
        var airPodsFound = new HashSet<ulong>();

        watcher.AdvertisementReceived += (sender, data) =>
        {
            Console.WriteLine($"debug -- {data.Rssi}");
            // filter for apple manufacturer data
            if (!data.ManufacturerData.TryGetValue(AppleConstants.VENDOR_ID, out var appleData))
                return;

            // try to parse proximity pairing message
            var message = ProximityPairingMessage.FromManufacturerData(appleData);
            if (!message.HasValue) return;
            
            var airPods = message.Value;
            var model = airPods.GetModel();

            // filter out unknown models
            if (model == AppleDeviceModel.Unknown) return;

            // only show each device once
            if (!airPodsFound.Add(data.Address))
                return;

            Console.WriteLine($"🎧 {model} detected!");
            Console.WriteLine($"  address: {data.Address:X12}");
            Console.WriteLine($"  rssi: {data.Rssi} dbm");
            Console.WriteLine($"  broadcast side: {airPods.GetBroadcastSide()}");

            // battery info
            var left = airPods.GetLeftBattery();
            var right = airPods.GetRightBattery();
            var caseB = airPods.GetCaseBattery();

            if (left.HasValue || right.HasValue || caseB.HasValue)
            {
                Console.WriteLine("  battery:");
                if (left.HasValue)
                    Console.WriteLine($"    left: {left.Value * 10}%{(airPods.IsLeftCharging() ? " (charging)" : "")}");
                if (right.HasValue)
                    Console.WriteLine($"    right: {right.Value * 10}%{(airPods.IsRightCharging() ? " (charging)" : "")}");
                if (caseB.HasValue)
                    Console.WriteLine($"    case: {caseB.Value * 10}%{(airPods.IsCaseCharging() ? " (charging)" : "")}");
            }

            Console.WriteLine();
        };

        watcher.Start();
        Console.WriteLine("scanning for airpods. open your airpods case nearby.");
        Console.WriteLine("press enter to stop...\n");
        Console.ReadLine();

        // watcher.Dispose() called automatically
        Console.WriteLine($"\nairpods devices found: {airPodsFound.Count}");
    }

    /// <summary>
    /// example 4: connect to a specific bluetooth device
    /// </summary>
    static async Task Example4_ConnectToDevice()
    {
        Console.WriteLine("=== connect to bluetooth device ===\n");
        Console.Write("enter bluetooth address (e.g., 1a2b3c4d5e6f): ");
        var addressStr = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(addressStr))
        {
            Console.WriteLine("invalid address.");
            return;
        }

        try
        {
            // parse address
            var address = Convert.ToUInt64(addressStr.Replace(":", "").Replace("-", ""), 16);
            Console.WriteLine($"\nconnecting to device at address: {address:X12}...");

            // connect to device with automatic disposal
            using var device = await BluetoothDevice.FromBluetoothAddressAsync(address);

            if (device == null)
            {
                Console.WriteLine("error: device not found");
                return;
            }

            // check if paired
            var deviceInfo = await DeviceInformation.CreateFromIdAsync(device.DeviceId);
            if (!deviceInfo.Pairing.IsPaired)
            {
                Console.WriteLine("error: device is not paired");
                return;
            }

            // subscribe to events
            device.ConnectionStatusChanged += (sender, args) =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] connection state: {sender.ConnectionStatus}");
            };

            device.NameChanged += (sender, args) =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] device renamed to: {sender.Name}");
            };

            // display device info
            Console.WriteLine("\ndevice information:");
            Console.WriteLine($"  name: {device.Name}");
            Console.WriteLine($"  device id: {device.DeviceId}");
            Console.WriteLine($"  address: {device.BluetoothAddress:X12}");
            Console.WriteLine($"  connection state: {device.ConnectionStatus}");

            Console.WriteLine("\nmonitoring device. press enter to stop...");
            Console.ReadLine();
            
            // device.Dispose() called automatically
        }
        catch (Exception ex)
        {
            Console.WriteLine($"error: {ex.Message}");
        }
    }

    /// <summary>
    /// example 5: complete airpods monitoring application
    /// </summary>
    static async Task Example5_CompleteAirPodsMonitor()
    {
        Console.WriteLine("=== complete airpods monitor ===\n");

        using var watcher = new AdvertisementWatcher();
        var knownDevices = new Dictionary<ulong, DateTime>();

        watcher.AdvertisementReceived += (sender, data) =>
        {
            // filter for apple devices
            if (!data.ManufacturerData.TryGetValue(AppleConstants.VENDOR_ID, out var appleData))
                return;

            // parse proximity pairing message
            var message = ProximityPairingMessage.FromManufacturerData(appleData);
            if (!message.HasValue) return;

            var airPods = message.Value;
            var model = airPods.GetModel();

            if (model == AppleDeviceModel.Unknown) return;

            // throttle updates (only show update every 5 seconds per device)
            var now = DateTime.Now;
            if (knownDevices.TryGetValue(data.Address, out var lastSeen))
            {
                if ((now - lastSeen).TotalSeconds < 5)
                    return;
            }
            knownDevices[data.Address] = now;

            // clear and display
            Console.Clear();
            Console.WriteLine("=== airpods monitor ===");
            Console.WriteLine($"last update: {now:HH:mm:ss}\n");

            Console.WriteLine($"device: {model}");
            Console.WriteLine($"address: {data.Address:X12}");
            Console.WriteLine($"signal: {data.Rssi} dbm");
            Console.WriteLine($"broadcasting from: {airPods.GetBroadcastSide()} pod\n");

            // battery section
            Console.WriteLine("battery:");
            DisplayBatteryLine("left  ", airPods.GetLeftBattery(), airPods.IsLeftCharging());
            DisplayBatteryLine("right ", airPods.GetRightBattery(), airPods.IsRightCharging());
            DisplayBatteryLine("case  ", airPods.GetCaseBattery(), airPods.IsCaseCharging());
            Console.WriteLine();

            // status section
            Console.WriteLine("status:");
            Console.WriteLine($"  lid: {(airPods.IsLidOpened() ? "open ✓" : "closed")}");
            Console.WriteLine($"  left in ear: {(airPods.IsLeftInEar() ? "yes ✓" : "no")}");
            Console.WriteLine($"  right in ear: {(airPods.IsRightInEar() ? "yes ✓" : "no")}");
            Console.WriteLine($"  both in case: {(airPods.IsBothPodsInCase() ? "yes" : "no")}");
            Console.WriteLine();

            Console.WriteLine("press ctrl+c to stop...");
        };

        Console.WriteLine("starting airpods monitor...");
        Console.WriteLine("open your airpods case nearby.\n");

        watcher.Start();

        // wait for ctrl+c
        var tcs = new TaskCompletionSource<bool>();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            tcs.SetResult(true);
        };

        await tcs.Task;
        
        // watcher.Dispose() called automatically
        Console.WriteLine("\nmonitor stopped.");
    }

    static void DisplayBatteryLine(string label, byte? battery, bool charging)
    {
        if (!battery.HasValue)
        {
            Console.WriteLine($"  {label}: --");
            return;
        }

        var percent = battery.Value * 10;
        var bar = new string('█', percent / 10) + new string('░', 10 - percent / 10);
        var chargingIndicator = charging ? " ⚡ charging" : "";
        Console.WriteLine($"  {label}: [{bar}] {percent}%{chargingIndicator}");
    }

    /// <summary>
    /// example 7: bluetooth diagnostics to debug connection issues
    /// shows all paired devices and their actual connection status from windows
    /// </summary>
    static async Task Example7_BluetoothDiagnostics()
    {
        Console.WriteLine("=== bluetooth diagnostics ===\n");
        Console.WriteLine("generating diagnostic report...\n");

        // Generate and display the full diagnostic report
        var report = await BluetoothDiagnostics.GenerateDiagnosticReportAsync();
        Console.WriteLine(report);

        // Interactive mode to monitor connection changes
        Console.WriteLine("\n=== live connection monitoring ===");
        Console.WriteLine("monitoring paired devices for connection changes...");
        Console.WriteLine("press enter to stop.\n");

        var cts = new CancellationTokenSource();
        var monitorTask = Task.Run(async () =>
        {
            var lastConnectionStates = new Dictionary<string, bool>();

            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var devices = await BluetoothDiagnostics.GetPairedClassicDevicesAsync();
                    
                    foreach (var device in devices)
                    {
                        var key = device.Name;
                        var isConnected = device.IsConnected;

                        if (lastConnectionStates.TryGetValue(key, out var wasConnected))
                        {
                            if (wasConnected != isConnected)
                            {
                                var status = isConnected ? "CONNECTED" : "DISCONNECTED";
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {device.Name}: {status}");
                            }
                        }
                        else
                        {
                            // First time seeing this device
                            var status = isConnected ? "CONNECTED" : "disconnected";
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {device.Name}: initial state = {status}");
                        }

                        lastConnectionStates[key] = isConnected;
                    }

                    await Task.Delay(1000, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}] error: {ex.Message}");
                    await Task.Delay(2000, cts.Token);
                }
            }
        }, cts.Token);

        Console.ReadLine();
        await cts.CancelAsync();

        try
        {
            await monitorTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        Console.WriteLine("\ndiagnostics stopped.");
    }

    /// <summary>
    /// example 9: list all bluetooth devices and display their model information
    /// shows product id to apple device model mapping for paired devices
    /// </summary>
    static async Task Example9_ListDevicesWithModels()
    {
        Console.WriteLine("=== list bluetooth devices with model info ===\n");
        Console.WriteLine("enumerating all paired bluetooth devices...\n");

        var devices = await BluetoothDiagnostics.GetPairedClassicDevicesAsync();

        if (devices.Count == 0)
        {
            Console.WriteLine("no paired bluetooth devices found.");
            return;
        }

        Console.WriteLine($"found {devices.Count} paired device(s):\n");

        foreach (var deviceInfo in devices)
        {
            var connectionStatus = deviceInfo.IsConnected ? "CONNECTED" : "disconnected";
            Console.WriteLine($"📱 {deviceInfo.Name}");
            Console.WriteLine($"  address: {deviceInfo.BluetoothAddress:X12}");
            Console.WriteLine($"  status: {connectionStatus}");
            Console.WriteLine($"  device class: {deviceInfo.DeviceClass}");

            try
            {
                using var device = await DeviceCommunication.Device.Device.FromDeviceIdAsync(deviceInfo.Id);
                
                // Try to get vendor and product ID
                try
                {
                    var vendorId = await device.GetVendorIdAsync();
                    var productId = await device.GetProductIdAsync();
                    
                    Console.WriteLine($"  vendor id: 0x{vendorId:X4}");
                    Console.WriteLine($"  product id: 0x{productId:X4}");

                    // Check if it's an Apple device (Vendor ID 0x004C = 76)
                    if (vendorId == 0x004C || vendorId == 76)
                    {
                        var model = AppleDeviceModelHelper.GetModel(productId);
                        var modelName = GetAppleDeviceModelName(model);
                        Console.WriteLine($"  🍎 apple model: {modelName}");
                    }
                }
                catch
                {
                    Console.WriteLine($"  vendor/product id: not available");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  error querying device: {ex.Message}");
            }

            Console.WriteLine();
        }

        Console.WriteLine("listing complete.");
    }

    /// <summary>
    /// converts AppleDeviceModel enum to friendly display name
    /// </summary>
    static string GetAppleDeviceModelName(AppleDeviceModel model)
    {
        return model switch
        {
            AppleDeviceModel.AirPods1 => "AirPods (1st generation)",
            AppleDeviceModel.AirPods2 => "AirPods (2nd generation)",
            AppleDeviceModel.AirPods3 => "AirPods (3rd generation)",
            AppleDeviceModel.AirPodsPro => "AirPods Pro",
            AppleDeviceModel.AirPodsPro2 => "AirPods Pro (2nd generation)",
            AppleDeviceModel.AirPodsPro2UsbC => "AirPods Pro (2nd gen, USB-C)",
            AppleDeviceModel.AirPodsMax => "AirPods Max",
            AppleDeviceModel.BeatsFitPro => "Beats Fit Pro",
            AppleDeviceModel.Unknown => "Unknown Apple Device",
            _ => "Unknown"
        };
    }
}
