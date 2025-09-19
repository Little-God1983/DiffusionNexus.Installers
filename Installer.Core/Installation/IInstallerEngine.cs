using AIKnowledge2Go.Installers.Core.Logging;
using AIKnowledge2Go.Installers.Core.Manifests;

namespace AIKnowledge2Go.Installers.Core.Installation;

public interface IInstallerEngine
{
    Task<InstallResult> InstallAsync(
        InstallManifest manifest,
        InstallOptions options,
        IProgress<InstallProgress>? progress,
        ILogSink logSink,
        CancellationToken cancellationToken = default);
}
