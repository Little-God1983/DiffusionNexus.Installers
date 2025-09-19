using System.Threading;
using System.Threading.Tasks;

namespace Installer.Core.Settings;

public interface IUserSettingsService
{
    Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default);
}
