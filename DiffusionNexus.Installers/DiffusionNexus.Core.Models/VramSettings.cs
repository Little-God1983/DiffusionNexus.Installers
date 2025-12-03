namespace DiffusionNexus.Core.Models
{
    /// <summary>
    /// VRAM configuration settings for installation.
    /// </summary>
    public class VramSettings
    {
        /// <summary>
        /// Comma-separated VRAM profile mappings (e.g., "8GB:folder1,12GB:folder2").
        /// </summary>
        public string VramProfiles { get; set; } = string.Empty;
    }
}