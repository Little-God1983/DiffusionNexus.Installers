using DiffusionNexus.Core.Models.Installation;

namespace DiffusionNexus.Core.Models.Strategies;

/// <summary>
/// Interface for installation strategies.
/// </summary>
public interface IInstallStrategy
{
    string ApplicationName { get; }
    string Version { get; }
    Task<InstallResult> InstallAsync(InstallContext context, IProgress<InstallProgress> progress);
    Task<bool> ValidatePrerequisitesAsync();
    Task<bool> UninstallAsync(string installPath);
}
