using DeviceCommunication.Models;
using DeviceCommunication.Services;

namespace ConnectionTestCLI;

/// <summary>
/// Simple CLI to test AirPods connection with out-of-case precondition.
/// Automatically connects when at least one pod is out of the case.
/// </summary>
class Program
{
    private static bool _connectionAttempted = false;
    private static bool _isConnecting = false;
    private static readonly object _lock = new();

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== AirPods Auto-Connect Test CLI ===");
        Console.WriteLine("This CLI automatically connects when pods are out of the case.\n");
        Console.WriteLine("Scanning for AirPods...\n");

        using var discoveryService = new SimpleAirPodsDiscoveryService();
        using var connectionService = new BluetoothConnectionService();

        // Subscribe to discovery events
        discoveryService.DeviceDiscovered += async (sender, device) =>
        {
            Console.WriteLine("\n[NEW DEVICE DISCOVERED]");
            DisplayDeviceStatus(device);
            await TryAutoConnectAsync(device, connectionService);
        };

        // Subscribe to update events
        discoveryService.DeviceUpdated += async (sender, device) =>
        {
            Console.WriteLine("\n[STATUS UPDATE]");
            DisplayDeviceStatus(device);
            await TryAutoConnectAsync(device, connectionService);
        };

        // Start discovery
        discoveryService.StartScanning();

        Console.WriteLine("Waiting for AirPods... Take them out of the case to auto-connect.");
        Console.WriteLine("Press 'r' to reset (allow reconnect), or 'q' to quit.\n");

        var running = true;
        while (running)
        {
            var key = Console.ReadKey(intercept: true);
            
            switch (key.KeyChar)
            {
                case 'r':
                case 'R':
                    lock (_lock)
                    {
                        _connectionAttempted = false;
                        Console.WriteLine("\n[RESET] Connection state reset. Will auto-connect when pods are ready.");
                    }
                    break;
                    
                case 'q':
                case 'Q':
                    running = false;
                    break;
            }
        }

        Console.WriteLine("\nExiting...");
    }

    static void DisplayDeviceStatus(AirPodsDeviceInfo device)
    {
        Console.WriteLine("\n----------------------------------------");
        Console.WriteLine($"[DEVICE] {device.Model}");
        Console.WriteLine($"  Device Name: {device.DeviceName}");
        Console.WriteLine($"  Product ID: 0x{device.ProductId:X4}");
        Console.WriteLine($"  Paired Device ID: {device.PairedDeviceId ?? "Not found"}");
        Console.WriteLine($"  Signal: {device.SignalStrength} dBm");
        Console.WriteLine($"  Last Seen: {device.LastSeen:HH:mm:ss}");
        
        // Battery section
        Console.WriteLine("\n  Battery:");
        Console.WriteLine($"    Left:  {(device.LeftBattery.HasValue ? $"{device.LeftBattery}%" : "--")}{(device.IsLeftCharging ? " (charging)" : "")}");
        Console.WriteLine($"    Right: {(device.RightBattery.HasValue ? $"{device.RightBattery}%" : "--")}{(device.IsRightCharging ? " (charging)" : "")}");
        Console.WriteLine($"    Case:  {(device.CaseBattery.HasValue ? $"{device.CaseBattery}%" : "--")}{(device.IsCaseCharging ? " (charging)" : "")}");
        
        // Status section
        Console.WriteLine("\n  Status:");
        Console.WriteLine($"    Lid Open: {(device.IsLidOpen ? "Yes" : "No")}");
        Console.WriteLine($"    Left in Ear: {(device.IsLeftInEar ? "Yes" : "No")}");
        Console.WriteLine($"    Right in Ear: {(device.IsRightInEar ? "Yes" : "No")}");
        Console.WriteLine($"    Both Pods in Case: {(device.IsBothPodsInCase ? "YES (blocked)" : "No")}");
        Console.WriteLine($"    Connected: {(device.IsConnected ? "Yes" : "No")}");
        
        // Connection precondition check
        Console.WriteLine("\n  Connection Eligibility:");
        if (device.IsReadyForConnection)
        {
            Console.WriteLine("    [OK] READY - At least one pod is out of the case");
        }
        else
        {
            Console.WriteLine("    [BLOCKED] NOT READY - Both pods are in the case");
        }
        
        Console.WriteLine("----------------------------------------");
    }

    static async Task TryAutoConnectAsync(AirPodsDeviceInfo device, BluetoothConnectionService connectionService)
    {
        // Check if we should attempt connection
        lock (_lock)
        {
            if (_connectionAttempted || _isConnecting)
            {
                if (_connectionAttempted)
                    Console.WriteLine("\n[AUTO-CONNECT] Already connected this session. Press 'r' to reset.");
                return;
            }

            // Check precondition: at least one pod must be out of the case
            if (!device.IsReadyForConnection)
            {
                Console.WriteLine("\n[AUTO-CONNECT] Waiting... both pods are still in the case.");
                return;
            }

            if (string.IsNullOrEmpty(device.PairedDeviceId))
            {
                Console.WriteLine("\n[AUTO-CONNECT] WARNING: No paired device ID found. Ensure AirPods are paired in Windows Settings.");
                return;
            }

            _isConnecting = true;
        }

        try
        {
            Console.WriteLine("\n========================================");
            Console.WriteLine("[AUTO-CONNECT] INITIATING CONNECTION");
            Console.WriteLine("========================================");
            Console.WriteLine("  [OK] Precondition passed: At least one pod is out of the case");
            Console.WriteLine($"  Device: {device.DeviceName}");
            Console.WriteLine($"  Device ID: {device.PairedDeviceId}");
            Console.WriteLine($"  Timestamp: {DateTime.Now:HH:mm:ss.fff}");
            Console.WriteLine("========================================\n");

            Console.WriteLine("[AUTO-CONNECT] Calling BluetoothConnectionService.ConnectByDeviceIdAsync...\n");

            var result = await connectionService.ConnectByDeviceIdAsync(device.PairedDeviceId);

            Console.WriteLine("\n========================================");
            Console.WriteLine("[AUTO-CONNECT] CONNECTION RESULT");
            Console.WriteLine("========================================");

            switch (result)
            {
                case ConnectionResult.Connected:
                    Console.WriteLine("  [SUCCESS] CONNECTION SUCCESSFUL!");
                    Console.WriteLine("  Audio should now route to AirPods.");
                    lock (_lock) { _connectionAttempted = true; }
                    break;
                case ConnectionResult.NeedsPairing:
                    Console.WriteLine("  [FAILED] Device needs to be paired first.");
                    Console.WriteLine("  Open Windows Settings > Bluetooth & devices to pair.");
                    break;
                case ConnectionResult.DeviceNotFound:
                    Console.WriteLine("  [FAILED] Device not found.");
                    Console.WriteLine("  Ensure AirPods are nearby and case lid is open.");
                    break;
                case ConnectionResult.Failed:
                    Console.WriteLine("  [FAILED] Connection failed.");
                    Console.WriteLine("  Try again or connect manually via Windows Settings.");
                    break;
            }

            Console.WriteLine("========================================\n");
            Console.WriteLine("Press 'r' to reset and reconnect, or 'q' to quit.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[AUTO-CONNECT] ERROR: Connection error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            lock (_lock) { _isConnecting = false; }
        }
    }
}

