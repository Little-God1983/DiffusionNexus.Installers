using DiffusionNexus.Core.Models.Enums;

namespace DiffusionNexus.Core.Models.Installation;

/// <summary>
/// General installation options.
/// </summary>
public class InstallationOptions
{
    public bool CreateStartupScript { get; set; } = true;
    public string StartupScriptName { get; set; } = "run_nvidia.bat";
    public List<string> StartupArguments { get; set; } = ["--windows-standalone-build"];

    public bool PauseOnError { get; set; } = true;
    public bool AutoStartAfterInstall { get; set; } = false;
    public string ListenAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8188;

    public bool EnableLogging { get; set; } = true;
    public LogLevel LogLevel { get; set; } = LogLevel.Info;

    public bool VerifyDownloads { get; set; } = true;
    public bool CleanupOnFailure { get; set; } = false;
    public bool BackupExisting { get; set; } = true;

    public ProxySettings? Proxy { get; set; }
}
