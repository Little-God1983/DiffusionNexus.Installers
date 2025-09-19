using System.Threading;
using System.Threading.Tasks;

namespace Installer.UI.Services;

public interface IUserInteractionService
{
    Task<string?> BrowseForFolderAsync(string? initialDirectory, CancellationToken cancellationToken);

    Task<string?> SaveLogFileAsync(string suggestedFileName, CancellationToken cancellationToken);

    Task SetClipboardTextAsync(string text, CancellationToken cancellationToken);
}
