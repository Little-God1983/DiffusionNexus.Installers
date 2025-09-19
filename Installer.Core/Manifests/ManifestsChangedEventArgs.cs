namespace AIKnowledge2Go.Installers.Core.Manifests;

public sealed class ManifestsChangedEventArgs : EventArgs
{
    public ManifestsChangedEventArgs(IReadOnlyList<ManifestDescriptor> manifests)
    {
        Manifests = manifests;
    }

    public IReadOnlyList<ManifestDescriptor> Manifests { get; }
}
