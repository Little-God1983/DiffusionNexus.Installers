namespace Installer.Core.Settings;

public sealed class UserSettings
{
    public string? LastInstallDirectory { get; set; }
        = null;

    public bool TelemetryOptIn { get; set; }
        = false;

    public static UserSettings Default => new();
}
