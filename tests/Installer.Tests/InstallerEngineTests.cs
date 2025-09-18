using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Installer.Core.Installation;
using Installer.Core.Logging;
using Installer.Core.Manifests;
using Xunit;

namespace Installer.Tests;

public sealed class InstallerEngineTests
{
    [Fact]
    public async Task InstallAsync_WritesLogAndCreatesTargetStructure()
    {
        var manifest = new InstallManifest
        {
            Id = "test",
            Title = "Test Manifest",
            BaseSoftware = new BaseSoftwareConfig
            {
                Name = "Demo",
                Target = "DemoApp"
            },
            Models =
            {
                new ModelConfig
                {
                    Name = "DemoModel",
                    Source = "mock",
                    Target = "DemoApp/models"
                }
            },
            Extensions =
            {
                new ExtensionConfig
                {
                    Name = "DemoExtension",
                    Repository = "https://example.com/demo.git",
                    Target = "DemoApp/extensions"
                }
            }
        };

        var descriptor = new ManifestDescriptor("test.json", manifest);
        var installRoot = Path.Combine(Path.GetTempPath(), "installer-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(installRoot);
        var logPath = Path.Combine(installRoot, "install.log");

        try
        {
            var request = new InstallRequest(descriptor, installRoot, null, null, logPath);
            var engine = new InstallerEngine();
            var logSink = new BufferingLogSink();

            var result = await engine.InstallAsync(request, null, logSink, CancellationToken.None);

            Assert.True(result.Success);
            Assert.False(result.Cancelled);
            Assert.NotNull(result.LogFilePath);
            Assert.True(File.Exists(result.LogFilePath));
            Assert.Contains(logSink.Messages, m => m.Message.Contains("Demo"));

            var expectedModelPath = Path.Combine(installRoot, "DemoApp", "models");
            Assert.True(Directory.Exists(expectedModelPath));
        }
        finally
        {
            Directory.Delete(installRoot, true);
        }
    }
}
