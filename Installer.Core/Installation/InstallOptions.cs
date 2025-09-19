namespace AIKnowledge2Go.Installers.Core.Installation;

public sealed record InstallOptions
{
    public required string InstallRoot { get; init; }

    public string? SelectedVramProfileId { get; init; }

    public IReadOnlyCollection<string>? EnabledOptionalStepIds { get; init; }
}
