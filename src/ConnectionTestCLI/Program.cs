using DeviceCommunication.Advertisement;
using DeviceCommunication.Models;
using DeviceCommunication.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConnectionTestCLI;

/// <summary>
/// Simple CLI to test AirPods connection with out-of-case precondition.
/// Uses the unified AirPodsStateService architecture.
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

        // Create services using the modern architecture
        using var advertisementWatcher = new AdvertisementWatcher();
        using var bleDataProvider = new BleDataProvider(
            NullLogger<BleDataProvider>.Instance, 
            advertisementWatcher);
        using var pairedDeviceWatcher = new PairedDeviceWatcher(
            NullLogger<PairedDeviceWatcher>.Instance);
        using var audioMonitor = new DefaultAudioOutputMonitorService(
            NullLogger<DefaultAudioOutputMonitorService>.Instance);
        using var stateService = new AirPodsStateService(
            NullLogger<AirPodsStateService>.Instance,
            pairedDeviceWatcher, 
            bleDataProvider, 
            audioMonitor);
        var connector = new Win32BluetoothConnector();

        // Subscribe to state changes
        stateService.StateChanged += async (sender, args) =>
        {
            var state = args.State;
            
            // Only show updates for paired devices
            if (!state.IsPaired) return;
            
            var isNew = args.Reason == AirPodsStateChangeReason.PairedDeviceAdded 
                     || args.Reason == AirPodsStateChangeReason.InitialEnumeration;
            
            Console.WriteLine($"\n[{(isNew ? "NEW DEVICE" : "STATUS UPDATE")}]");
            DisplayDeviceStatus(state);
            await TryAutoConnectAsync(state, connector);
        };

        // Start the unified state service
        audioMonitor.Start();
        await stateService.StartAsync();

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
        stateService.Stop();
        audioMonitor.Stop();
    }

    static void DisplayDeviceStatus(AirPodsState state)
    {
        Console.WriteLine("\n----------------------------------------");
        Console.WriteLine($"[DEVICE] {state.ModelName}");
        Console.WriteLine($"  Device Name: {state.Name}");
        Console.WriteLine($"  Product ID: 0x{state.ProductId:X4}");
        Console.WriteLine($"  Paired Device ID: {state.PairedDeviceId ?? "Not found"}");
        Console.WriteLine($"  Signal: {state.SignalStrength} dBm");
        Console.WriteLine($"  Last Seen: {state.LastSeen:HH:mm:ss}");
        
        // Battery section
        Console.WriteLine("\n  Battery:");
        Console.WriteLine($"    Left:  {(state.LeftBattery.HasValue ? $"{state.LeftBattery}%" : "--")}{(state.IsLeftCharging ? " (charging)" : "")}");
        Console.WriteLine($"    Right: {(state.RightBattery.HasValue ? $"{state.RightBattery}%" : "--")}{(state.IsRightCharging ? " (charging)" : "")}");
        Console.WriteLine($"    Case:  {(state.CaseBattery.HasValue ? $"{state.CaseBattery}%" : "--")}{(state.IsCaseCharging ? " (charging)" : "")}");
        
        // Status section
        Console.WriteLine("\n  Status:");
        Console.WriteLine($"    Lid Open: {(state.IsLidOpen ? "Yes" : "No")}");
        Console.WriteLine($"    Left in Ear: {(state.IsLeftInEar ? "Yes" : "No")}");
        Console.WriteLine($"    Right in Ear: {(state.IsRightInEar ? "Yes" : "No")}");
        Console.WriteLine($"    Both Pods in Case: {(state.IsBothPodsInCase ? "YES (blocked)" : "No")}");
        Console.WriteLine($"    Connected: {(state.IsConnected ? "Yes" : "No")}");
        Console.WriteLine($"    Audio Active: {(state.IsAudioConnected ? "Yes" : "No")}");
        
        // Connection precondition check
        Console.WriteLine("\n  Connection Eligibility:");
        if (state.IsReadyForConnection)
        {
            Console.WriteLine("    [OK] READY - At least one pod is out of the case");
        }
        else
        {
            Console.WriteLine("    [BLOCKED] NOT READY - Both pods are in the case");
        }
        
        Console.WriteLine("----------------------------------------");
    }

    static async Task TryAutoConnectAsync(AirPodsState state, Win32BluetoothConnector connector)
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
            if (!state.IsReadyForConnection)
            {
                Console.WriteLine("\n[AUTO-CONNECT] Waiting... both pods are still in the case.");
                return;
            }

            if (!state.BluetoothAddress.HasValue)
            {
                Console.WriteLine("\n[AUTO-CONNECT] WARNING: No Bluetooth address found. Ensure AirPods are paired in Windows Settings.");
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
            Console.WriteLine($"  Device: {state.Name}");
            Console.WriteLine($"  Bluetooth Address: {state.BluetoothAddress:X12}");
            Console.WriteLine($"  Timestamp: {DateTime.Now:HH:mm:ss.fff}");
            Console.WriteLine("========================================\n");

            Console.WriteLine("[AUTO-CONNECT] Calling Win32BluetoothConnector.ConnectAudioDeviceAsync...\n");

            var success = await connector.ConnectAudioDeviceAsync(state.BluetoothAddress.Value);

            Console.WriteLine("\n========================================");
            Console.WriteLine("[AUTO-CONNECT] CONNECTION RESULT");
            Console.WriteLine("========================================");

            if (success)
            {
                Console.WriteLine("  [SUCCESS] CONNECTION SUCCESSFUL!");
                Console.WriteLine("  Audio should now route to AirPods.");
                lock (_lock) { _connectionAttempted = true; }
            }
            else
            {
                Console.WriteLine("  [FAILED] Connection failed.");
                Console.WriteLine("  Try again or connect manually via Windows Settings.");
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

