using DeviceCommunication.Adapter;
using DeviceCommunication.Advertisement;
using DeviceCommunication.Apple;
using DeviceCommunication.Core;
using DeviceCommunication.Device;
using DeviceCommunication.Services;

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
            Console.WriteLine("5. complete airpods monitor (legacy)");
            Console.WriteLine("6. reactive airpods monitor (new architecture)");
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
                case "6":
                    await Example6_ReactiveAirPodsMonitor();
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
            using var device = await Device.FromBluetoothAddressAsync(address);

            // subscribe to events
            device.ConnectionStateChanged += (sender, state) =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] connection state: {state}");
            };

            device.NameChanged += (sender, name) =>
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] device renamed to: {name}");
            };

            // display device info
            Console.WriteLine("\ndevice information:");
            Console.WriteLine($"  name: {device.GetName()}");
            Console.WriteLine($"  device id: {device.GetDeviceId()}");
            Console.WriteLine($"  address: {device.GetAddress():X12}");
            Console.WriteLine($"  connection state: {device.GetConnectionState()}");
            Console.WriteLine($"  is connected: {device.IsConnected()}");

            try
            {
                var vendorId = await device.GetVendorIdAsync();
                var productId = await device.GetProductIdAsync();
                Console.WriteLine($"  vendor id: {vendorId} (0x{vendorId:X4})");
                Console.WriteLine($"  product id: {productId} (0x{productId:X4})");
            }
            catch (BluetoothException ex)
            {
                Console.WriteLine($"  could not retrieve vendor/product id: {ex.Message}");
            }

            Console.WriteLine("\nmonitoring device. press enter to stop...");
            Console.ReadLine();
            
            // device.Dispose() called automatically
        }
        catch (BluetoothException ex)
        {
            Console.WriteLine($"error: {ex.Message} ({ex.Error})");
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
    /// example 6: reactive airpods monitor using new layered architecture
    /// demonstrates proper separation of concerns with IObservable pattern
    /// </summary>
    static async Task Example6_ReactiveAirPodsMonitor()
    {
        Console.WriteLine("=== reactive airpods monitor (new architecture) ===\n");
        Console.WriteLine("this example demonstrates the new layered architecture:");
        Console.WriteLine("  layer 1: IAdvertisementStream - raw BLE advertisements");
        Console.WriteLine("  layer 2: AirPodsDeviceAggregator - grouping & deduplication");
        Console.WriteLine("  layer 3: IObservable subscription - reactive updates\n");

        using var advertisementStream = new AdvertisementStream();
        using var aggregator = new AirPodsDeviceAggregator(advertisementStream);

        var knownDevices = new Dictionary<Guid, DateTime>();

        // Subscribe to device state changes
        using var subscription = aggregator.DeviceChanges.Subscribe(
            onNext: change =>
            {
                var now = DateTime.Now;

                // Throttle updates (only show update every 5 seconds per device)
                if (change.ChangeType == DeviceChangeType.Updated)
                {
                    if (knownDevices.TryGetValue(change.DeviceId, out var lastSeen))
                    {
                        if ((now - lastSeen).TotalSeconds < 5)
                            return;
                    }
                }

                knownDevices[change.DeviceId] = now;

                // Display device state change
                Console.Clear();
                Console.WriteLine("=== reactive airpods monitor ===");
                Console.WriteLine($"last update: {now:HH:mm:ss}");
                Console.WriteLine($"change type: {change.ChangeType}\n");

                if (change.ChangeType == DeviceChangeType.Removed)
                {
                    Console.WriteLine($"device removed: {change.Device.Model}");
                    Console.WriteLine($"device id: {change.DeviceId}");
                }
                else
                {
                    var device = change.Device;
                    Console.WriteLine($"device: {device.Model}");
                    Console.WriteLine($"device id: {change.DeviceId}");
                    Console.WriteLine($"address: {device.Address:X12}");
                    Console.WriteLine($"signal: {device.SignalStrength} dbm\n");

                    // battery section
                    Console.WriteLine("battery:");
                    if (device.LeftBattery.HasValue)
                    {
                        var leftBatt = (byte)(device.LeftBattery.Value / 10);
                        DisplayBatteryLine("left  ", leftBatt, device.IsLeftCharging);
                    }
                    if (device.RightBattery.HasValue)
                    {
                        var rightBatt = (byte)(device.RightBattery.Value / 10);
                        DisplayBatteryLine("right ", rightBatt, device.IsRightCharging);
                    }
                    if (device.CaseBattery.HasValue)
                    {
                        var caseBatt = (byte)(device.CaseBattery.Value / 10);
                        DisplayBatteryLine("case  ", caseBatt, device.IsCaseCharging);
                    }
                    Console.WriteLine();

                    // status section
                    Console.WriteLine("status:");
                    Console.WriteLine($"  lid: {(device.IsLidOpen ? "open ✓" : "closed")}");
                    Console.WriteLine($"  left in ear: {(device.IsLeftInEar ? "yes ✓" : "no")}");
                    Console.WriteLine($"  right in ear: {(device.IsRightInEar ? "yes ✓" : "no")}");
                    Console.WriteLine();
                }

                Console.WriteLine($"total devices tracked: {knownDevices.Count}");
                Console.WriteLine("\npress ctrl+c to stop...");
            },
            onError: ex => Console.Error.WriteLine($"error in device stream: {ex}"),
            onCompleted: () => Console.WriteLine("device stream completed")
        );

        Console.WriteLine("starting reactive monitor...");
        Console.WriteLine("open your airpods case nearby.\n");

        aggregator.Start();

        // wait for ctrl+c
        var tcs = new TaskCompletionSource<bool>();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            tcs.SetResult(true);
        };

        await tcs.Task;

        Console.WriteLine("\nmonitor stopped.");
    }
}
