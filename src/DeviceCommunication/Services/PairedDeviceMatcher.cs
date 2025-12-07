namespace DeviceCommunication.Services;

/// <summary>
/// [LEGACY ARCHITECTURE] Result of matching an advertised device to a paired device.
/// </summary>
/// <remarks>
/// <para><strong>?? This class is part of the legacy architecture and is maintained only for educational/comparison purposes.</strong></para>
/// <para>The legacy architecture used multi-tier matching (name-based, single audio device, connected device)
/// to resolve rotating BLE addresses to paired devices. This was complex and error-prone.</para>
/// <para><strong>New Approach:</strong> Use <see cref="SimpleAirPodsDiscoveryService"/> which directly matches
/// devices by Product ID (stable identifier) using <c>Device.GetProductIdAsync()</c>. No heuristics needed.</para>
/// </remarks>
/// <param name="PairedName">The name of the paired device.</param>
/// <param name="PairedAddress">The Bluetooth address of the paired device.</param>
/// <param name="IsConnected">Whether the paired device is currently connected.</param>
/// <param name="Score">Match confidence score (higher is better).</param>
/// <param name="MatchTier">The tier that produced this match (for diagnostics).</param>
public record DeviceMatch(string PairedName, ulong PairedAddress, bool IsConnected, int Score, MatchTier MatchTier = MatchTier.NameBased);

/// <summary>
/// Indicates which matching strategy produced a result.
/// </summary>
public enum MatchTier
{
    /// <summary>Name-based matching found a good match.</summary>
    NameBased,
    /// <summary>Only one AudioVideo device is paired, assumed to be the target.</summary>
    SingleAudioDevice,
    /// <summary>A connected AudioVideo device was selected as the best guess.</summary>
    ConnectedAudioDevice
}

/// <summary>
/// [LEGACY ARCHITECTURE] Matches advertised AirPods devices to paired Bluetooth devices using a multi-tier strategy.
/// </summary>
/// <remarks>
/// <para><strong>?? LEGACY - Use Product ID-based approach instead</strong></para>
/// <para>This class demonstrates the old complex matching architecture that is no longer recommended.
/// Kept for educational purposes and backward compatibility with CLI Example 8.</para>
/// <para><strong>Why this approach was replaced:</strong></para>
/// <list type="bullet">
/// <item>Complex multi-tier heuristics required (name matching, device class fallbacks)</item>
/// <item>BLE address rotation made tracking difficult</item>
/// <item>User-renamed devices broke name-based matching</item>
/// <item>Required connection state monitoring</item>
/// </list>
/// <para><strong>New Product ID approach:</strong></para>
/// <list type="bullet">
/// <item>Product ID is stable and doesn't rotate</item>
/// <item>Direct Windows API: <c>Device.GetProductIdAsync()</c> returns Product ID from paired device</item>
/// <item>BLE advertisement contains same Product ID</item>
/// <item>Simple equality check replaces all heuristics</item>
/// </list>
/// <para>See <see cref="SimpleAirPodsDiscoveryService"/> for the recommended approach.</para>
/// <para><strong>Original matching tiers (legacy):</strong></para>
/// <list type="number">
/// <item>Tier 1: Name-based matching - best for users who keep default device names</item>
/// <item>Tier 2: Single AudioVideo device - if only one headphone is paired, it's probably the target</item>
/// <item>Tier 3: Connected AudioVideo device - if one AudioVideo device is connected, prefer it</item>
/// </list>
/// </remarks>
[Obsolete("This class uses legacy multi-tier matching heuristics. Use Product ID-based identification via SimpleAirPodsDiscoveryService instead. Kept for educational purposes and CLI Example 8.")]
public static class PairedDeviceMatcher
{
    // Score thresholds for name-based matching
    private const int ExactContainsScore = 100;
    private const int CoreModelScore = 80;
    private const int AirPodsKeywordScore = 50;
    private const int MinimumNameMatchScore = 50;

    // Scores for fallback tiers (lower than name-based to indicate less confidence)
    private const int SingleAudioDeviceScore = 40;
    private const int ConnectedAudioDeviceScore = 30;

    /// <summary>
    /// Finds the best matching paired device for an advertised model name using multi-tier matching.
    /// </summary>
    /// <param name="advertisedModelName">The model name from the BLE advertisement (e.g., "AirPods Pro (2nd generation)").</param>
    /// <param name="pairedDevices">List of paired Bluetooth devices.</param>
    /// <returns>The best match if found, null if no reasonable match exists.</returns>
    public static DeviceMatch? FindBestMatch(
        string advertisedModelName,
        IReadOnlyList<PairedBluetoothDevice> pairedDevices)
    {
        if (string.IsNullOrWhiteSpace(advertisedModelName) || pairedDevices.Count == 0)
            return null;

        // Tier 1: Name-based matching (highest confidence)
        var nameMatch = FindByNameSimilarity(advertisedModelName, pairedDevices);
        if (nameMatch is not null)
            return nameMatch;

        // Tier 2 & 3: Device class-based fallbacks
        return FindByDeviceClass(pairedDevices);
    }

    /// <summary>
    /// Tier 1: Finds a match based on name similarity.
    /// </summary>
    private static DeviceMatch? FindByNameSimilarity(
        string advertisedModelName,
        IReadOnlyList<PairedBluetoothDevice> pairedDevices)
    {
        DeviceMatch? bestMatch = null;

        foreach (var paired in pairedDevices)
        {
            var score = CalculateNameMatchScore(advertisedModelName, paired.Name);

            if (score >= MinimumNameMatchScore && (bestMatch is null || score > bestMatch.Score))
            {
                bestMatch = new DeviceMatch(paired.Name, paired.Address, paired.IsConnected, score, MatchTier.NameBased);
            }
        }

        return bestMatch;
    }

    /// <summary>
    /// Tier 2 &amp; 3: Finds a match based on device class when name matching fails.
    /// </summary>
    private static DeviceMatch? FindByDeviceClass(IReadOnlyList<PairedBluetoothDevice> pairedDevices)
    {
        // Filter to AudioVideo devices only (headphones, speakers, etc.)
        var audioDevices = pairedDevices
            .Where(IsAudioDevice)
            .ToList();

        if (audioDevices.Count == 0)
            return null;

        // Tier 2: If only one AudioVideo device is paired, it's probably the AirPods
        if (audioDevices.Count == 1)
        {
            var device = audioDevices[0];
            return new DeviceMatch(
                device.Name,
                device.Address,
                device.IsConnected,
                SingleAudioDeviceScore,
                MatchTier.SingleAudioDevice);
        }

        // Tier 3: If one AudioVideo device is currently connected, prefer it
        var connectedAudioDevices = audioDevices.Where(d => d.IsConnected).ToList();
        if (connectedAudioDevices.Count == 1)
        {
            var device = connectedAudioDevices[0];
            return new DeviceMatch(
                device.Name,
                device.Address,
                device.IsConnected,
                ConnectedAudioDeviceScore,
                MatchTier.ConnectedAudioDevice);
        }

        // Multiple audio devices, none connected or multiple connected - can't determine
        return null;
    }

    /// <summary>
    /// Determines if a device is an audio device based on its device class.
    /// </summary>
    private static bool IsAudioDevice(PairedBluetoothDevice device)
    {
        return device.DeviceClass.Contains("Audio", StringComparison.OrdinalIgnoreCase) ||
               device.DeviceClass.Equals("Wearable", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Calculates a similarity score between an advertised model name and a paired device name.
    /// </summary>
    private static int CalculateNameMatchScore(string advertisedName, string pairedName)
    {
        // Exact contains (either direction) - highest score
        // e.g., "AirPods Pro (2nd generation)" contains in "John's AirPods Pro (2nd generation)"
        if (pairedName.Contains(advertisedName, StringComparison.OrdinalIgnoreCase) ||
            advertisedName.Contains(pairedName, StringComparison.OrdinalIgnoreCase))
        {
            return ExactContainsScore;
        }

        // Core model match - extract and compare core model names
        var advertisedCore = ExtractCoreModelName(advertisedName);
        var pairedCore = ExtractCoreModelName(pairedName);

        if (!string.IsNullOrEmpty(advertisedCore) && !string.IsNullOrEmpty(pairedCore))
        {
            if (pairedCore.Contains(advertisedCore, StringComparison.OrdinalIgnoreCase) ||
                advertisedCore.Contains(pairedCore, StringComparison.OrdinalIgnoreCase))
            {
                return CoreModelScore;
            }
        }

        // Both contain "AirPods" - weak match but better than nothing
        if (advertisedName.Contains("AirPods", StringComparison.OrdinalIgnoreCase) &&
            pairedName.Contains("AirPods", StringComparison.OrdinalIgnoreCase))
        {
            return AirPodsKeywordScore;
        }

        // Both contain "Beats" - for Beats products
        if (advertisedName.Contains("Beats", StringComparison.OrdinalIgnoreCase) &&
            pairedName.Contains("Beats", StringComparison.OrdinalIgnoreCase))
        {
            return AirPodsKeywordScore;
        }

        return 0;
    }

    /// <summary>
    /// Extracts the core model name, removing generation info and user prefixes.
    /// "AirPods Pro (2nd generation)" becomes "AirPods Pro"
    /// "John's AirPods Pro" becomes "AirPods Pro"
    /// </summary>
    private static string ExtractCoreModelName(string name)
    {
        // Remove common patterns
        var result = name;

        // Remove parenthetical suffixes like "(2nd generation)", "(USB-C)"
        var parenIndex = result.IndexOf('(');
        if (parenIndex > 0)
        {
            result = result[..parenIndex].Trim();
        }

        // Try to find AirPods or Beats as anchor
        var airPodsIndex = result.IndexOf("AirPods", StringComparison.OrdinalIgnoreCase);
        if (airPodsIndex >= 0)
        {
            result = result[airPodsIndex..];
        }
        else
        {
            var beatsIndex = result.IndexOf("Beats", StringComparison.OrdinalIgnoreCase);
            if (beatsIndex >= 0)
            {
                result = result[beatsIndex..];
            }
        }

        return result.Trim();
    }
}
