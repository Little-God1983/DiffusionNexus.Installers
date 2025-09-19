using System.Text.Json.Serialization;

namespace AIKnowledge2Go.Installers.Core.Manifests;

public sealed record InstallManifest
{
    [JsonPropertyName("schemaVersion")]
    public required string SchemaVersion { get; init; }

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("baseSoftware")]
    public required BaseSoftware BaseSoftware { get; init; }

    [JsonPropertyName("dependencies")]
    public DependencySection? Dependencies { get; init; }

    [JsonPropertyName("vramProfiles")]
    public IReadOnlyList<VramProfile>? VramProfiles { get; init; }

    [JsonPropertyName("models")]
    public IReadOnlyList<ModelEntry>? Models { get; init; }

    [JsonPropertyName("extensions")]
    public IReadOnlyList<ExtensionEntry>? Extensions { get; init; }

    [JsonPropertyName("optionalSteps")]
    public IReadOnlyList<OptionalStep>? OptionalSteps { get; init; }
}

public sealed record BaseSoftware
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("repo")]
    public string? Repository { get; init; }

    [JsonPropertyName("archive")]
    public string? Archive { get; init; }

    [JsonPropertyName("ref")]
    public string? Reference { get; init; }

    [JsonPropertyName("target")]
    public required string Target { get; init; }
}

public sealed record DependencySection
{
    [JsonPropertyName("python")]
    public string? Python { get; init; }

    [JsonPropertyName("cuda")]
    public string? Cuda { get; init; }

    [JsonPropertyName("pipRequirements")]
    public IReadOnlyList<PipRequirement>? PipRequirements { get; init; }
}

public sealed record PipRequirement
{
    [JsonPropertyName("relativeTo")]
    public string? RelativeTo { get; init; }

    [JsonPropertyName("path")]
    public required string Path { get; init; }
}

public sealed record VramProfile
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("ggufPreference")]
    public IReadOnlyList<string>? GgufPreference { get; init; }

    [JsonPropertyName("mixedBasicResolution")]
    public string? MixedBasicResolution { get; init; }
}

public sealed record ModelEntry
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("repo")]
    public string? Repository { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("match")]
    public string? Match { get; init; }

    [JsonPropertyName("prefer")]
    [JsonConverter(typeof(PreferenceSelectorJsonConverter))]
    public PreferenceSelector? Prefer { get; init; }

    [JsonPropertyName("target")]
    public required string Target { get; init; }
}

public sealed record ExtensionEntry
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("repo")]
    public required string Repository { get; init; }

    [JsonPropertyName("target")]
    public required string Target { get; init; }
}

public sealed record OptionalStep
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("shell")]
    public required string Shell { get; init; }

    [JsonPropertyName("workingDir")]
    public string? WorkingDirectory { get; init; }

    [JsonPropertyName("enabledByDefault")]
    public bool EnabledByDefault { get; init; } = true;
}
