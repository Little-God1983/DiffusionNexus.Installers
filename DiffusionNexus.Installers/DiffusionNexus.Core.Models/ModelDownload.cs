using System;
using System.ComponentModel.DataAnnotations;

namespace DiffusionNexus.Core.Models
{
    /// <summary>
    /// Model download configuration authored by the UI.
    /// </summary>
    public class ModelDownload
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Optional override for where the file should be placed. When empty the
        /// engine uses <see cref="InstallationConfiguration.Paths"/> to determine
        /// the destination.
        /// </summary>
        public string Destination { get; set; } = string.Empty;

        /// <summary>
        /// VRAM profile influences the default folder resolution. Custom allows the
        /// user to bypass the automatic mapping logic.
        /// </summary>
        public VramProfile VramProfile { get; set; } = VramProfile.VRAM_16GB;

        /// <summary>
        /// Allows authors to opt-out of model downloads without deleting them.
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}
