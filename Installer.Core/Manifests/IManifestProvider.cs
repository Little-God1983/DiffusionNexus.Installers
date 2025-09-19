namespace AIKnowledge2Go.Installers.Core.Manifests;

public interface IManifestProvider : IDisposable
{
    event EventHandler<ManifestsChangedEventArgs>? ManifestsChanged;

    IReadOnlyList<ManifestDescriptor> Manifests { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManifestDescriptor>> RefreshAsync(CancellationToken cancellationToken = default);
}
