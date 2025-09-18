using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Installer.Core.Manifests;

public sealed class InstallManifest
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "1.0";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("baseSoftware")]
    public BaseSoftwareConfig? BaseSoftware { get; set; }
        = new BaseSoftwareConfig();

    [JsonPropertyName("dependencies")]
    public DependencyConfig? Dependencies { get; set; }
        = new DependencyConfig();

    [JsonPropertyName("vramProfiles")]
    public List<VramProfileConfig> VramProfiles { get; set; }
        = new();

    [JsonPropertyName("models")]
    public List<ModelConfig> Models { get; set; } = new();

    [JsonPropertyName("extensions")]
    public List<ExtensionConfig> Extensions { get; set; } = new();

    [JsonPropertyName("optionalSteps")]
    public List<OptionalStepConfig> OptionalSteps { get; set; } = new();
}

public sealed class BaseSoftwareConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("repo")]
    public string? RepositoryUrl { get; set; }
        = string.Empty;

    [JsonPropertyName("ref")]
    public string? Ref { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;
}

public sealed class DependencyConfig
{
    [JsonPropertyName("python")]
    public string? Python { get; set; }
        = null;

    [JsonPropertyName("cuda")]
    public string? Cuda { get; set; }
        = null;

    [JsonPropertyName("pipRequirements")]
    public List<PipRequirementConfig> PipRequirements { get; set; }
        = new();
}

public sealed class PipRequirementConfig
{
    [JsonPropertyName("relativeTo")]
    public string? RelativeTo { get; set; }
        = null;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
}

public sealed class VramProfileConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("ggufPreference")]
    public List<string> GgufPreference { get; set; } = new();

    [JsonPropertyName("mixedBasicResolution")]
    public string? MixedBasicResolution { get; set; }
        = null;
}

public sealed class ModelConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("repo")]
    public string? Repository { get; set; }
        = null;

    [JsonPropertyName("match")]
    public string? Match { get; set; }
        = null;

    [JsonPropertyName("prefer")]
    public string? PreferExpression { get; set; }
        = null;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;
}

public sealed class ExtensionConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("repo")]
    public string Repository { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;
}

public sealed class OptionalStepConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("shell")]
    public string Shell { get; set; } = string.Empty;

    [JsonPropertyName("workingDir")]
    public string? WorkingDirectory { get; set; }
        = null;

    [JsonPropertyName("enabledByDefault")]
    public bool EnabledByDefault { get; set; } = true;
}
