namespace DiffusionNexus.Installers.ViewModels;

/// <summary>
/// Shared constants for VRAM profile values used across the application.
/// </summary>
public static class VramProfileConstants
{
    /// <summary>
    /// Default VRAM profile sizes in GB.
    /// </summary>
    public static readonly int[] DefaultProfiles = [4, 6, 8, 12, 16, 24, 32, 48, 64];

    /// <summary>
    /// Default selected VRAM profile in GB.
    /// </summary>
    public const int DefaultSelectedProfile = 8;
}
