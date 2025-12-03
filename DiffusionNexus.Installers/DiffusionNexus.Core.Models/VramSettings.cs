namespace DiffusionNexus.Core.Models
{
    /// <summary>
    /// VRAM configuration settings for installation.
    /// </summary>
    public class VramSettings
    {
        /// <summary>
        /// Comma-separated VRAM profile values (e.g., "8,16,24,24+").
        /// Default values when activated: "8,16,24,24+"
        /// </summary>
        public string VramProfiles { get; set; } = string.Empty;
    }
}