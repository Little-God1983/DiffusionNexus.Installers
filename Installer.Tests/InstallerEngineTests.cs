using System.Collections.Generic;
using System.IO;
using AIKnowledge2Go.Installers.Core.Installation;
using AIKnowledge2Go.Installers.Core.Logging;
using AIKnowledge2Go.Installers.Core.Manifests;
using Xunit;

namespace Installer.Tests;

public class InstallerEngineTests
{
    [Fact]
    public async Task InstallerEngine_CreatesExpectedArtifacts()
    {
        using var temp = new TemporaryDirectory();
        var manifest = new InstallManifest
        {
            SchemaVersion = "1.0",
            Id = "test",
            Title = "Test Install",
            BaseSoftware = new BaseSoftware
            {
                Name = "ComfyUI",
                Target = "ComfyUI"
            },
            Dependencies = new DependencySection
            {
                Python = "3.12",
                Cuda = "12.8",
                PipRequirements = new[]
                {
                    new PipRequirement { Path = "requirements.txt" }
                }
            },
            Models = new[]
            {
                new ModelEntry
                {
                    Name = "ModelA",
                    Target = "ComfyUI/models",
                    Source = "huggingface",
                    Repository = "example/repo"
                }
            },
            Extensions = new[]
            {
                new ExtensionEntry
                {
                    Name = "ExtA",
                    Repository = "https://example.com/ext",
                    Target = "ComfyUI/extensions"
                }
            },
            OptionalSteps = new[]
            {
                new OptionalStep
                {
                    Id = "launch",
                    Description = "Launch",
                    Shell = "python main.py",
                    EnabledByDefault = true
                }
            }
        };

        var engine = new InstallerEngine();
        var memoryLog = new InMemoryLogSink();
        var options = new InstallOptions
        {
            InstallRoot = temp.Path,
            SelectedVramProfileId = null,
            EnabledOptionalStepIds = new[] { "launch" }
        };

        var progressUpdates = new List<InstallProgress>();
        var progress = new Progress<InstallProgress>(p => progressUpdates.Add(p));

        var result = await engine.InstallAsync(manifest, options, progress, memoryLog);

        Assert.True(result.Success);
        Assert.Contains(progressUpdates, p => p.IsCompleted);

        var baseInfoFile = Path.Combine(temp.Path, "ComfyUI", "INSTALLER_INFO.txt");
        Assert.True(File.Exists(baseInfoFile));

        var dependenciesPlan = Path.Combine(temp.Path, "dependencies-plan.txt");
        Assert.True(File.Exists(dependenciesPlan));

        var modelMetadata = Path.Combine(temp.Path, "ComfyUI", "models", "ModelA.model.txt");
        Assert.True(File.Exists(modelMetadata));

        var extensionMetadata = Path.Combine(temp.Path, "ComfyUI", "extensions", "ExtA.extension.txt");
        Assert.True(File.Exists(extensionMetadata));

        var optionalLog = Path.Combine(temp.Path, "optional-steps.log");
        Assert.True(File.Exists(optionalLog));
    }
}
