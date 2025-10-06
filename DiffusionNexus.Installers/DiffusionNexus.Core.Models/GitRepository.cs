using System.ComponentModel.DataAnnotations;

namespace DiffusionNexus.Core.Models
{
    /// <summary>
    /// Git repository for custom nodes/extensions
    /// </summary>
    public class GitRepository
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Url { get; set; } = string.Empty;

        public string Branch { get; set; } = string.Empty;
        public string CommitHash { get; set; } = string.Empty;
        public bool UseLatestRelease { get; set; } = true;
        public string LocalPath { get; set; } = string.Empty; // Relative to custom_nodes
        public bool Enabled { get; set; } = true;
        public bool InstallRequirements { get; set; } = true;
        public string RequirementsFile { get; set; } = "requirements.txt";
        public List<string> Tags { get; set; } = new();
        public int Priority { get; set; } = 0; // Installation order
    }
}