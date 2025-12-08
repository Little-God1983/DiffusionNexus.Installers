using DiffusionNexus.Core.Models.Installation;
using DiffusionNexus.Core.Models.Strategies;
using Serilog;

namespace DiffusionNexus.Core.Services;

public class InstallerManager
{
    private readonly Dictionary<string, IInstallStrategy> _strategies;
    private IInstallStrategy? _currentStrategy;

    public InstallerManager()
    {
        _strategies = new Dictionary<string, IInstallStrategy>();

        // Register available strategies
        RegisterStrategy("ComfyUI", new ComfyUIInstallStrategy());
        RegisterStrategy("Automatic1111", new Automatic1111InstallStrategy());
    }

    public void RegisterStrategy(string name, IInstallStrategy strategy)
    {
        _strategies[name] = strategy;
    }

    public void SetStrategy(string applicationName)
    {
        if (_strategies.TryGetValue(applicationName, out var strategy))
        {
            _currentStrategy = strategy;
        }
        else
        {
            throw new ArgumentException($"No installation strategy found for {applicationName}");
        }
    }

    public async Task<InstallResult> InstallAsync(InstallContext context, IProgress<InstallProgress>? progress = null)
    {
        if (_currentStrategy is null)
        {
            throw new InvalidOperationException("No installation strategy selected");
        }

        Log.Information("Starting installation of {App}", _currentStrategy.ApplicationName);

        if (!await _currentStrategy.ValidatePrerequisitesAsync())
        {
            return new InstallResult
            {
                Success = false,
                Message = "Prerequisites not met",
                Errors = ["Please ensure Python and Git are installed"]
            };
        }

        return await _currentStrategy.InstallAsync(context, progress!);
    }

    public IEnumerable<string> GetAvailableApplications()
    {
        return _strategies.Keys;
    }
}
