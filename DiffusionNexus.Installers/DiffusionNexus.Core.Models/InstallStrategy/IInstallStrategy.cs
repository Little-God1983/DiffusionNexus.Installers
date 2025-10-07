using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiffusionNexus.Core.Models.InstallStrategy
{
    public interface IInstallStrategy
    {
        string ApplicationName { get; }
        string Version { get; }
        Task<InstallResult> InstallAsync(InstallContext context, IProgress<InstallProgress> progress);
        Task<bool> ValidatePrerequisitesAsync();
        Task<bool> UninstallAsync(string installPath);
    }
}
