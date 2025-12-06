using System.Text;
using System.Text.Json;
using DeviceCommunication.Advertisement;
using DeviceCommunication.Apple;

namespace AdvertisementCapture;

/// <summary>
/// Utility to capture real BLE advertisements and save them for testing purposes.
/// </summary>
class Program
{
    private static readonly List<CapturedAdvertisement> _captures = new();
    private static readonly HashSet<ulong> _seenAddresses = new();
    private static string _outputPath = "captured_advertisements.json";
    private static bool _captureAll = false;
    private static bool _appleOnly = true;
    private static bool _screenOnly = false;
    private static int _captureLimit = 10;

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== BLE Advertisement Capture Tool ===\n");
        
        ParseArguments(args);
        ShowConfiguration();

        using var watcher = new AdvertisementWatcher();
        
        watcher.AdvertisementReceived += OnAdvertisementReceived;
        watcher.Stopped += (s, e) => Console.WriteLine("\n[!] Watcher stopped");

        Console.WriteLine($"\nStarting capture... (Press Ctrl+C to stop and save)\n");
        Console.WriteLine("Filters:");
        Console.WriteLine($"  - Apple devices only: {_appleOnly}");
        Console.WriteLine($"  - Capture limit: {_captureLimit} devices");
        Console.WriteLine($"  - Capture all packets: {_captureAll}\n");

        watcher.Start();

        // Wait for Ctrl+C
        var tcs = new TaskCompletionSource<bool>();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            tcs.SetResult(true);
        };

        await tcs.Task;

        watcher.Stop();

        if (_screenOnly)
        {
            Console.WriteLine($"\nCapture complete! Collected {_captures.Count} advertisements (screen-only mode; nothing saved to disk).");
        }
        else
        {
            SaveCaptures();
            Console.WriteLine($"\nCapture complete! Saved {_captures.Count} advertisements to '{_outputPath}'");
        }
    }

    private static void OnAdvertisementReceived(object? sender, AdvertisementReceivedData data)
    {
        // Check if we've reached the capture limit
        if (!_captureAll && _seenAddresses.Count >= _captureLimit)
            return;

        // Filter for Apple devices if requested
        bool isApple = data.ManufacturerData.ContainsKey(AppleConstants.VENDOR_ID);
        if (_appleOnly && !isApple)
            return;

        // Parse Apple data to check if it's AirPods
        bool isAirPods = false;
        string? modelName = null;
        string? broadcastSide = null;
        
        if (isApple)
        {
            var appleData = data.ManufacturerData[AppleConstants.VENDOR_ID];
            var message = ProximityPairingMessage.FromManufacturerData(appleData);
            if (message.HasValue)
            {
                var model = message.Value.GetModel();
                if (model != AppleDeviceModel.Unknown)
                {
                    isAirPods = true;
                    modelName = model.ToString();
                    broadcastSide = message.Value.GetBroadcastSide().ToString();
                }
            }
        }

        // For AirPods in --all mode, always capture (to see both pods)
        // For non-AirPods, skip duplicates
        if (!_captureAll && !isAirPods && _seenAddresses.Contains(data.Address))
            return;

        _seenAddresses.Add(data.Address);

        // Create capture record
        var capture = new CapturedAdvertisement
        {
            Timestamp = DateTime.Now,
            Address = data.Address,
            Rssi = data.Rssi,
            ManufacturerData = data.ManufacturerData.ToDictionary(
                kvp => kvp.Key,
                kvp => Convert.ToHexString(kvp.Value)
            )
        };

        // Try to parse Apple-specific data
        if (isApple)
        {
            var appleData = data.ManufacturerData[AppleConstants.VENDOR_ID];
            var message = ProximityPairingMessage.FromManufacturerData(appleData);
            
            if (message.HasValue)
            {
                var airPods = message.Value;
                var model = airPods.GetModel();
                
                capture.AppleInfo = new AppleDeviceInfo
                {
                    Model = model.ToString(),
                    BroadcastSide = airPods.GetBroadcastSide().ToString(),
                    LeftBattery = airPods.GetLeftBattery(),
                    RightBattery = airPods.GetRightBattery(),
                    CaseBattery = airPods.GetCaseBattery(),
                    IsLeftCharging = airPods.IsLeftCharging(),
                    IsRightCharging = airPods.IsRightCharging(),
                    IsCaseCharging = airPods.IsCaseCharging(),
                    IsLeftInEar = airPods.IsLeftInEar(),
                    IsRightInEar = airPods.IsRightInEar(),
                    IsLidOpen = airPods.IsLidOpened()
                };
            }
        }

        _captures.Add(capture);

        // Display capture with special highlighting for AirPods
        var marker = isAirPods ? "??" : "??";
        Console.WriteLine($"{marker} [{_captures.Count}] Captured: {data.Address:X12} (RSSI: {data.Rssi} dBm)");
        
        if (capture.AppleInfo != null)
        {
            Console.WriteLine($"    Model: {capture.AppleInfo.Model}");
            Console.WriteLine($"    Broadcast: {capture.AppleInfo.BroadcastSide} pod");
            
            if (capture.AppleInfo.LeftBattery.HasValue || capture.AppleInfo.RightBattery.HasValue)
            {
                Console.Write("    Battery: ");
                if (capture.AppleInfo.LeftBattery.HasValue)
                    Console.Write($"L:{capture.AppleInfo.LeftBattery * 10}% ");
                if (capture.AppleInfo.RightBattery.HasValue)
                    Console.Write($"R:{capture.AppleInfo.RightBattery * 10}% ");
                if (capture.AppleInfo.CaseBattery.HasValue)
                    Console.Write($"Case:{capture.AppleInfo.CaseBattery * 10}%");
                Console.WriteLine();
            }
        }
        
        Console.WriteLine();
    }

    private static void SaveCaptures()
    {
        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        var json = JsonSerializer.Serialize(_captures, options);
        File.WriteAllText(_outputPath, json);

        // Also save a C# code snippet for test data
        SaveTestDataSnippet();
    }

    private static void SaveTestDataSnippet()
    {
        var snippetPath = Path.ChangeExtension(_outputPath, ".cs");
        var sb = new StringBuilder();
        
        sb.AppendLine("// Generated test data from captured advertisements");
        sb.AppendLine("// Copy this into your test file");
        sb.AppendLine();
        sb.AppendLine("namespace TestData;");
        sb.AppendLine();
        sb.AppendLine("public static class CapturedAdvertisements");
        sb.AppendLine("{");
        
        for (int i = 0; i < _captures.Count; i++)
        {
            var capture = _captures[i];
            sb.AppendLine($"    // Capture {i + 1}: {capture.Address:X12}");
            
            if (capture.AppleInfo != null)
            {
                sb.AppendLine($"    // {capture.AppleInfo.Model} - {capture.AppleInfo.BroadcastSide} pod");
            }
            
            foreach (var (vendorId, hexData) in capture.ManufacturerData)
            {
                var bytes = Convert.FromHexString(hexData);
                var byteString = string.Join(", ", bytes.Select(b => $"0x{b:X2}"));
                
                sb.AppendLine($"    public static readonly byte[] Advertisement{i + 1}_Vendor{vendorId:X4} = new byte[]");
                sb.AppendLine("    {");
                sb.AppendLine($"        {byteString}");
                sb.AppendLine("    };");
                sb.AppendLine();
            }
        }
        
        sb.AppendLine("}");
        
        File.WriteAllText(snippetPath, sb.ToString());
        Console.WriteLine($"Also saved C# test data snippet to '{snippetPath}'");
    }

    private static void ParseArguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "-o":
                case "--output":
                    if (i + 1 < args.Length)
                        _outputPath = args[++i];
                    break;
                
                case "-a":
                case "--all":
                    _captureAll = true;
                    break;
                
                case "--any-device":
                    _appleOnly = false;
                    break;
                
                case "-n":
                case "--limit":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var limit))
                        _captureLimit = limit;
                    break;
                
                case "-l":
                case "--no-save":
                    _screenOnly = true;
                    break;
                
                case "-h":
                case "--help":
                    ShowHelp();
                    Environment.Exit(0);
                    break;
            }
        }
    }

    private static void ShowConfiguration()
    {
        Console.WriteLine("Configuration:");
        Console.WriteLine($"  Output file: {_outputPath}");
        Console.WriteLine($"  Capture all packets: {_captureAll}");
        Console.WriteLine($"  Apple devices only: {_appleOnly}");
        Console.WriteLine($"  Screen-only mode: {_screenOnly}");
        Console.WriteLine($"  Capture limit: {_captureLimit} devices");
    }

    private static void ShowHelp()
    {
        Console.WriteLine(@"
BLE Advertisement Capture Tool

Usage: AdvertisementCapture [options]

Options:
  -o, --output <file>    Output file path (default: captured_advertisements.json)
  -a, --all              Capture all packets (including duplicates)
  --any-device           Capture all BLE devices (not just Apple)
  -l, --no-save          Print captures to the console without saving files
  -n, --limit <number>   Number of unique devices to capture (default: 10)
  -h, --help             Show this help message

Examples:
  AdvertisementCapture                              # Capture 10 Apple devices
  AdvertisementCapture -o airpods.json -n 5         # Capture 5 devices to custom file
  AdvertisementCapture --any-device -a              # Capture all BLE advertisements
    AdvertisementCapture -l                          # Print captures without saving files
  AdvertisementCapture --limit 20                   # Capture 20 unique devices

Output:
  - JSON file with captured advertisement data
  - C# code snippet file for easy test data integration
");
    }
}

/// <summary>
/// Represents a captured BLE advertisement.
/// </summary>
public class CapturedAdvertisement
{
    public DateTime Timestamp { get; set; }
    public ulong Address { get; set; }
    public short Rssi { get; set; }
    public Dictionary<ushort, string> ManufacturerData { get; set; } = new();
    public AppleDeviceInfo? AppleInfo { get; set; }
}

/// <summary>
/// Apple-specific device information extracted from the advertisement.
/// </summary>
public class AppleDeviceInfo
{
    public string Model { get; set; } = string.Empty;
    public string BroadcastSide { get; set; } = string.Empty;
    public byte? LeftBattery { get; set; }
    public byte? RightBattery { get; set; }
    public byte? CaseBattery { get; set; }
    public bool IsLeftCharging { get; set; }
    public bool IsRightCharging { get; set; }
    public bool IsCaseCharging { get; set; }
    public bool IsLeftInEar { get; set; }
    public bool IsRightInEar { get; set; }
    public bool IsLidOpen { get; set; }
}
