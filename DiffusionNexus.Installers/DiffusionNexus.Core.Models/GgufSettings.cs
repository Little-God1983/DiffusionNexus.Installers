namespace DiffusionNexus.Core.Models
{
    /// <summary>
    /// GGUF model specific settings
    /// </summary>
    public class GgufSettings
    {
        public string Repository { get; set; } = string.Empty;
        public string Subfolder { get; set; } = string.Empty;
        public List<string> QuantizationPreferences { get; set; } = new();
        public bool AutoSelectByVram { get; set; } = true;
        public string FallbackQuantization { get; set; } = "Q4_K_M";
    }
}