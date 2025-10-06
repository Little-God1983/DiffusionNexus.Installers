namespace DiffusionNexus.Core.Models
{
    /// <summary>
    /// Pip package configuration
    /// </summary>
    public class PipPackage
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty; // Empty means latest
        public string IndexUrl { get; set; } = string.Empty; // Custom index
        public bool Enabled { get; set; } = true;
    }
}