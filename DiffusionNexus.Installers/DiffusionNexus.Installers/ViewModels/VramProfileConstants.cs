using DiffusionNexus.Core.Models.Enums;
using DiffusionNexus.Core.Models.Helpers;

namespace DiffusionNexus.Installers.ViewModels;

/// <summary>
/// Shared constants for VRAM profile values used across the Installers project.
/// Delegates to <see cref="VramProfileHelper"/> as the single source of truth.
/// </summary>
public static class VramProfileConstants
{
    /// <summary>
    /// Default VRAM profile sizes in GB.
    /// </summary>
    public static int[] DefaultProfiles => VramProfileHelper.SupportedProfiles;

    /// <summary>
    /// Default selected VRAM profile in GB.
    /// </summary>
    public static int DefaultSelectedProfile => VramProfileHelper.DefaultSelectedProfile;
}
