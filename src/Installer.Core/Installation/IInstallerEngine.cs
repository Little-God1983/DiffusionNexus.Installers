using System.Threading;
using System.Threading.Tasks;
using Installer.Core.Logging;

namespace Installer.Core.Installation;

public interface IInstallerEngine
{
    Task<InstallResult> InstallAsync(
        InstallRequest request,
        IProgress<InstallProgress>? progress,
        ILogSink logSink,
        CancellationToken cancellationToken);
}
