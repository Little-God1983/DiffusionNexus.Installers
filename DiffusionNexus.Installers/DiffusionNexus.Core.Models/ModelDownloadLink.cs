using System;
using System.ComponentModel.DataAnnotations;

namespace DiffusionNexus.Core.Models
{
    /// <summary>
    /// Represents a single download link with an optional VRAM profile.
    /// </summary>
    public class ModelDownloadLink
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Optional VRAM profile for this download link. Null means no specific VRAM profile.
        /// </summary>
        public VramProfile? VramProfile { get; set; }

        /// <summary>
        /// Optional destination override for this specific download link.
        /// </summary>
        public string Destination { get; set; } = string.Empty;

        /// <summary>
        /// Allows authors to opt-out of individual download links without deleting them.
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}
