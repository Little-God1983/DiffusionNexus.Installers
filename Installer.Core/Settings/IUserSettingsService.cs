namespace AIKnowledge2Go.Installers.Core.Settings;

public interface IUserSettingsService
{
    Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default);
}
