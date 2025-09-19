namespace AIKnowledge2Go.Installers.Core.Settings;

public sealed record UserSettings
{
    public string? LastInstallRoot { get; init; }

    public string? LastManifestId { get; init; }

    public string? LastSelectedVramProfile { get; init; }
}
