namespace DiffusionNexus.Core.Models.Installation;

/// <summary>
/// Progress information for installation operations.
/// </summary>
public class InstallProgress
{
    public int PercentComplete { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public string? Details { get; set; }
}
