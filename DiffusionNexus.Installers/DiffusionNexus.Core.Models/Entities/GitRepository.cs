using System.ComponentModel.DataAnnotations;

namespace DiffusionNexus.Core.Models.Entities;

/// <summary>
/// Additional Git repositories that should be cloned as part of an installation.
/// </summary>
public class GitRepository
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Remote repository URL.
    /// </summary>
    [Required]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Optional label displayed in the UI while editing the configuration.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Automatically assigned based on list order. Lower numbers execute first.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// When true the installer scans for requirements files and installs them
    /// after cloning.
    /// </summary>
    public bool InstallRequirements { get; set; } = true;
}
