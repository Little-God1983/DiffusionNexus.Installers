namespace Installer.Core.Manifests;

public sealed class ManifestDescriptor
{
    public ManifestDescriptor(string filePath, InstallManifest manifest)
    {
        FilePath = filePath;
        Manifest = manifest;
    }

    public string FilePath { get; }

    public InstallManifest Manifest { get; }

    public string DisplayName => Manifest.Title;

    public override string ToString() => DisplayName;
}
