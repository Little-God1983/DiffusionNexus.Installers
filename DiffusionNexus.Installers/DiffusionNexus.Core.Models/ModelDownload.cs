using System.ComponentModel.DataAnnotations;

namespace DiffusionNexus.Core.Models
{
    /// <summary>
    /// Model download configuration
    /// </summary>
    public class ModelDownload
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Url { get; set; } = string.Empty;

        public ModelSource Source { get; set; } = ModelSource.HuggingFace;
        public ModelType Type { get; set; } = ModelType.Checkpoint;
        public string Destination { get; set; } = string.Empty; // Relative path
        public string FileName { get; set; } = string.Empty; // Override filename
        public long ExpectedSize { get; set; } = 0; // In bytes
        public string Sha256Hash { get; set; } = string.Empty; // For verification
        public bool Enabled { get; set; } = true;
        public int Priority { get; set; } = 0;

        // For GGUF models
        public GgufSettings GgufSettings { get; set; } = null;

        // Download options
        public int MaxRetries { get; set; } = 5;
        public int RetryDelaySeconds { get; set; } = 2;
        public bool VerifyChecksum { get; set; } = false;
        public List<string> Tags { get; set; } = new();
    }
}