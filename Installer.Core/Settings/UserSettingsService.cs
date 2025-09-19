using System.IO;
using System.Text.Json;

namespace AIKnowledge2Go.Installers.Core.Settings;

public sealed class UserSettingsService : IUserSettingsService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public UserSettingsService(string? filePath = null)
    {
        _filePath = filePath ?? GetDefaultSettingsFile();
    }

    public async Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new UserSettings();
            }

            await using var stream = File.OpenRead(_filePath);
            var settings = await JsonSerializer.DeserializeAsync<UserSettings>(stream, _options, cancellationToken).ConfigureAwait(false);
            return settings ?? new UserSettings();
        }
        catch (JsonException)
        {
            return new UserSettings();
        }
    }

    public async Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Open(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, settings, _options, cancellationToken).ConfigureAwait(false);
    }

    private static string GetDefaultSettingsFile()
    {
        var basePath = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var directory = Path.Combine(basePath, "AIKnowledge2Go", "EasyInstaller");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "settings.json");
    }
}
