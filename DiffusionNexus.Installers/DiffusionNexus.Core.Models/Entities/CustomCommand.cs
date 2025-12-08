using DiffusionNexus.Core.Models.Enums;

namespace DiffusionNexus.Core.Models.Entities;

/// <summary>
/// Custom command to run during/after installation.
/// </summary>
public class CustomCommand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public ExecutionStage Stage { get; set; } = ExecutionStage.PostInstall;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 0;
    public bool ContinueOnError { get; set; } = false;
    public int TimeoutSeconds { get; set; } = 300;
}
