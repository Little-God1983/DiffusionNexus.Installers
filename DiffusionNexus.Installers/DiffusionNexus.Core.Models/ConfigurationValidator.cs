namespace DiffusionNexus.Core.Models
{
    /// <summary>
    /// Extension class for configuration validation
    /// </summary>
    public static class ConfigurationValidator
    {
        public static ValidationResult Validate(this InstallationConfiguration config)
        {
            var result = new ValidationResult();

            // Validate paths
            if (string.IsNullOrWhiteSpace(config.Paths?.RootDirectory))
            {
                result.Errors.Add("Root directory is required");
            }

            // Validate Python version
            if (config.PythonEnvironment?.PythonVersion < 3.8)
            {
                result.Errors.Add("Python version must be 3.8 or higher");
            }

            // Validate main repository
            if (string.IsNullOrWhiteSpace(config.MainRepository?.RepositoryUrl))
            {
                result.Errors.Add("Main repository URL is required");
            }

            // Add more validation as needed

            return result;
        }
    }
}