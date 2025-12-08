namespace DiffusionNexus.Core.Models.Enums;

/// <summary>
/// Download targets may use VRAM profiles to select destination folders.
/// </summary>
public enum VramProfile
{
    VRAM_4GB,
    VRAM_6GB,
    VRAM_8GB,
    VRAM_12GB,
    VRAM_16GB,
    VRAM_24GB,
    VRAM_32GB,
    VRAM_48GB,
    VRAM_64GB,
    Custom
}
