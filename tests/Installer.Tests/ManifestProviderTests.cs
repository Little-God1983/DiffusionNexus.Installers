using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Installer.Core.Manifests;
using Xunit;

namespace Installer.Tests;

public sealed class ManifestProviderTests
{
    [Fact]
    public async Task GetManifestsAsync_ReadsAllValidManifests()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "installer-tests-manifests", Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var manifest = new InstallManifest
            {
                SchemaVersion = "1.0",
                Id = "sample",
                Title = "Sample Manifest",
                BaseSoftware = new BaseSoftwareConfig
                {
                    Name = "Example",
                    Target = "Example"
                }
            };

            await using (var stream = File.Create(Path.Combine(tempDirectory, "sample.json")))
            {
                await JsonSerializer.SerializeAsync(stream, manifest);
            }

            await using (var stream = File.Create(Path.Combine(tempDirectory, "invalid.json")))
            {
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync("{\"invalid\": true}");
            }

            using var provider = new ManifestProvider(tempDirectory);
            var results = await provider.GetManifestsAsync();

            Assert.Single(results);
            Assert.Equal("Sample Manifest", results[0].Manifest.Title);
            Assert.Equal("sample", results[0].Manifest.Id);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }
}
