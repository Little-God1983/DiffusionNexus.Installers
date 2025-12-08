using DiffusionNexus.Core.Models.Configuration;

namespace DiffusionNexus.Core.Models.Validation;

/// <summary>
/// Provides validation helpers for <see cref="InstallationConfiguration"/>.
/// </summary>
public static class ConfigurationValidator
{
    private static readonly HashSet<string> SupportedPythonVersions =
        new(StringComparer.OrdinalIgnoreCase) { "3.8", "3.9", "3.10", "3.11", "3.12", "3.13" };

    public static ValidationResult Validate(this InstallationConfiguration config)
    {
        var result = new ValidationResult();

        if (config.Repository is null)
        {
            result.Errors.Add("Repository selection is required.");
            return result;
        }

        if (string.IsNullOrWhiteSpace(config.Repository.RepositoryUrl))
        {
            result.Errors.Add("Repository URL cannot be empty.");
        }

        if (config.Paths is null || string.IsNullOrWhiteSpace(config.Paths.RootDirectory))
        {
            result.Errors.Add("Installation root directory is required.");
        }

        if (config.Python is null)
        {
            result.Errors.Add("Python settings are missing.");
        }
        else
        {
            if (!SupportedPythonVersions.Contains(config.Python.PythonVersion))
            {
                result.Errors.Add(
                    $"Python version '{config.Python.PythonVersion}' is not supported. Choose between 3.8 and 3.13.");
            }

            if (!config.Python.CreateVirtualEnvironment &&
                string.IsNullOrWhiteSpace(config.Python.InterpreterPathOverride))
            {
                result.Errors.Add(
                    "When 'Create venv' is disabled you must provide an interpreter path override.");
            }

            if (config.Python.CreateVirtualEnvironment &&
                string.IsNullOrWhiteSpace(config.Python.VirtualEnvironmentName))
            {
                result.Errors.Add("Virtual environment name cannot be empty when creation is enabled.");
            }

            if (!string.IsNullOrWhiteSpace(config.Python.InterpreterPathOverride) &&
                !File.Exists(config.Python.InterpreterPathOverride))
            {
                result.Warnings.Add("Interpreter path override does not exist on disk.");
            }
        }

        if (config.GitRepositories is null)
        {
            result.Errors.Add("Git repository list is missing.");
        }
        else
        {
            if (config.GitRepositories.Any(repo => string.IsNullOrWhiteSpace(repo.Url)))
            {
                result.Errors.Add("All Git repositories must include a URL.");
            }

            var duplicateUrls = config.GitRepositories
                .Where(repo => !string.IsNullOrWhiteSpace(repo.Url))
                .GroupBy(repo => repo.Url.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (duplicateUrls.Count > 0)
            {
                result.Warnings.Add(
                    $"Duplicate Git repository URLs detected: {string.Join(", ", duplicateUrls)}.");
            }
        }

        if (config.ModelDownloads is null)
        {
            result.Errors.Add("Model download list is missing.");
        }
        else
        {
            foreach (var model in config.ModelDownloads)
            {
                var hasLegacyUrl = !string.IsNullOrWhiteSpace(model.Url);
                var hasDownloadLinks = model.DownloadLinks?.Count > 0;
                var hasValidDownloadLink = model.DownloadLinks?.Any(link => !string.IsNullOrWhiteSpace(link.Url)) == true;

                if (!hasLegacyUrl && !hasDownloadLinks)
                {
                    result.Errors.Add($"Model '{model.Name}' must have at least one download link.");
                }
                else if (hasDownloadLinks && !hasValidDownloadLink)
                {
                    result.Errors.Add($"Model '{model.Name}' has download links but none have a valid URL.");
                }
            }
        }

        if (config.Torch is null)
        {
            result.Errors.Add("Torch settings are missing.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(config.Torch.CudaVersion))
            {
                result.Errors.Add("CUDA version cannot be empty.");
            }

            if (!string.IsNullOrWhiteSpace(config.Torch.TorchVersion) &&
                !Version.TryParse(config.Torch.TorchVersion, out _))
            {
                result.Warnings.Add(
                    $"Torch version '{config.Torch.TorchVersion}' is not a valid semantic version.");
            }
        }

        return result;
    }
}
