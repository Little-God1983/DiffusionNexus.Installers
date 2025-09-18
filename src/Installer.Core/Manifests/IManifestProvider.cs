using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Installer.Core.Manifests;

public interface IManifestProvider : IDisposable
{
    event EventHandler? ManifestsChanged;

    Task<IReadOnlyList<ManifestDescriptor>> GetManifestsAsync(
        CancellationToken cancellationToken = default);
}
