namespace DiffusionNexus.DataAccess;

/// <summary>
/// Service for managing database file operations like export and import.
/// </summary>
public interface IDatabaseManagementService
{
    /// <summary>
    /// Gets the full path to the current database file.
    /// </summary>
    string GetDatabasePath();

    /// <summary>
    /// Exports the database by copying it to the specified destination path.
    /// </summary>
    /// <param name="destinationPath">The destination file path for the exported database.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExportDatabaseAsync(string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a database from the specified source path, replacing the current database.
    /// </summary>
    /// <param name="sourcePath">The source file path of the database to import.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ImportDatabaseAsync(string sourcePath, CancellationToken cancellationToken = default);
}
