using System.Threading;
using System.Threading.Tasks;

namespace AIKnowledge2Go.Installers.UI.Services;

public interface IDialogService
{
    Task<string?> BrowseForInstallRootAsync(CancellationToken cancellationToken = default);

    Task<string?> ExportLogAsync(string suggestedFileName, CancellationToken cancellationToken = default);

    Task ShowMessageAsync(string title, string message, CancellationToken cancellationToken = default);
}
