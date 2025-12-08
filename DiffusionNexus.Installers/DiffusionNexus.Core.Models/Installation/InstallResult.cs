namespace DiffusionNexus.Core.Models.Installation;

/// <summary>
/// Result of an installation operation.
/// </summary>
public class InstallResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = [];
    public string InstallPath { get; set; } = string.Empty;
}
