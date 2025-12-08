using System.Collections.Frozen;
using DiffusionNexus.Core.Models.Enums;

namespace DiffusionNexus.Core.Models.Helpers;

/// <summary>
/// Centralized helper for VRAM profile conversions and constants.
/// This is the single source of truth for all VRAM profile values.
/// To add a new VRAM profile, simply add it to the enum and the GbToProfile dictionary.
/// </summary>
public static class VramProfileHelper
{
    /// <summary>
    /// Bidirectional mapping between VRAM sizes (in GB) and their enum values.
    /// This is the ONLY place you need to update when adding new profiles.
    /// </summary>
    private static readonly FrozenDictionary<int, VramProfile> GbToProfile = new Dictionary<int, VramProfile>
    {
        [4] = VramProfile.VRAM_4GB,
        [6] = VramProfile.VRAM_6GB,
        [8] = VramProfile.VRAM_8GB,
        [12] = VramProfile.VRAM_12GB,
        [16] = VramProfile.VRAM_16GB,
        [24] = VramProfile.VRAM_24GB,
        [32] = VramProfile.VRAM_32GB,
        [48] = VramProfile.VRAM_48GB,
        [64] = VramProfile.VRAM_64GB,
    }.ToFrozenDictionary();

    /// <summary>
    /// Reverse lookup: enum to GB value.
    /// </summary>
    private static readonly FrozenDictionary<VramProfile, int> ProfileToGb =
        GbToProfile.ToFrozenDictionary(kvp => kvp.Value, kvp => kvp.Key);

    /// <summary>
    /// All supported VRAM profile sizes in GB, sorted ascending.
    /// Derived automatically from the mapping table.
    /// </summary>
    public static readonly int[] SupportedProfiles = [.. GbToProfile.Keys.Order()];

    /// <summary>
    /// Default selected VRAM profile in GB.
    /// </summary>
    public const int DefaultSelectedProfile = 8;

    /// <summary>
    /// Converts a VRAM size in GB to the corresponding enum value.
    /// </summary>
    public static VramProfile? FromGigabytes(int sizeInGb) =>
        GbToProfile.TryGetValue(sizeInGb, out var profile) ? profile : null;

    /// <summary>
    /// Converts a VRAM profile enum to its size in GB.
    /// </summary>
    public static int? ToGigabytes(VramProfile profile) =>
        ProfileToGb.TryGetValue(profile, out var gb) ? gb : null;

    /// <summary>
    /// Parses a display string (e.g., "16GB", "16+GB", "16") to a VramProfile enum.
    /// </summary>
    public static VramProfile? FromDisplayString(string? displayValue)
    {
        if (string.IsNullOrWhiteSpace(displayValue) || displayValue == "None")
            return null;

        var numericString = displayValue
            .Replace("+GB", "")
            .Replace("GB", "")
            .Replace("+", "")
            .Trim();

        return int.TryParse(numericString, out var sizeInGb) ? FromGigabytes(sizeInGb) : null;
    }

    /// <summary>
    /// Converts a VramProfile enum to a display string (e.g., "16GB").
    /// </summary>
    public static string ToDisplayString(VramProfile profile) =>
        ToGigabytes(profile) is { } gb ? $"{gb}GB" : "Custom";
}
