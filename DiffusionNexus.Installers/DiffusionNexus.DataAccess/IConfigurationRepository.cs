// File: DiffusionNexus.DataAccess/Repositories/IConfigurationRepository.cs
using DiffusionNexus.Core.Models;

namespace DiffusionNexus.DataAccess;

/// <summary>
/// Repository interface for InstallationConfiguration entities.
/// Read operations are safe for both Creator and Installer apps.
/// Write operations should primarily be used by the Creator app.
/// </summary>
public interface IConfigurationRepository
{
    #region Read Operations (Safe for Installer)

    /// <summary>
    /// Gets all configurations from the database.
    /// </summary>
    Task<IReadOnlyList<InstallationConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a configuration by its unique identifier.
    /// </summary>
    Task<InstallationConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a configuration with the specified ID exists.
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a configuration with the specified name exists.
    /// </summary>
    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    #endregion

    #region Write Operations (Creator App Only)

    /// <summary>
    /// Saves a configuration to the database (insert or update).
    /// </summary>
    Task<InstallationConfiguration> SaveAsync(InstallationConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a configuration as a new entity with a new ID.
    /// </summary>
    Task<InstallationConfiguration> SaveAsNewAsync(InstallationConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a configuration by its unique identifier.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    #endregion
}
