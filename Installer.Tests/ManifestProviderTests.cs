using AIKnowledge2Go.Installers.Core.Manifests;
using Xunit;

namespace Installer.Tests;

public class ManifestProviderTests
{
    [Fact]
    public async Task ManifestProvider_LoadsValidManifest()
    {
        using var temp = new TemporaryDirectory();
        var manifestContent = """
        {
          "schemaVersion": "1.0",
          "id": "sample",
          "title": "Sample Manifest",
          "baseSoftware": {
            "name": "ComfyUI",
            "target": "ComfyUI"
          }
        }
        """;

        await File.WriteAllTextAsync(Path.Combine(temp.Path, "sample.json"), manifestContent);

        using var provider = new ManifestProvider(temp.Path, enableWatcher: false);
        await provider.InitializeAsync();

        Assert.Single(provider.Manifests);
        var descriptor = provider.Manifests[0];
        Assert.Equal("Sample Manifest", descriptor.Title);
        Assert.Equal("sample", descriptor.Manifest.Id);
    }

    [Fact]
    public async Task ManifestProvider_IgnoresInvalidManifest()
    {
        using var temp = new TemporaryDirectory();
        var invalidContent = """
        {
          "schemaVersion": "1.0",
          "id": "broken",
          "title": "Broken"
        }
        """;

        await File.WriteAllTextAsync(Path.Combine(temp.Path, "broken.json"), invalidContent);

        using var provider = new ManifestProvider(temp.Path, enableWatcher: false);
        await provider.InitializeAsync();

        Assert.Empty(provider.Manifests);
    }
}
