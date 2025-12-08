namespace DiffusionNexus.Core.Models.Installation;

/// <summary>
/// Common installation context.
/// </summary>
public class InstallContext
{
    public string InstallPath { get; set; } = string.Empty;
    public string? PythonPath { get; set; }
    public bool CreateVirtualEnvironment { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    public bool UseCuda { get; set; }
    public string CudaVersion { get; set; } = string.Empty;
}
