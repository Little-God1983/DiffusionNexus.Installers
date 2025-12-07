using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DiffusionNexus.DataAccess;

/// <summary>
/// Service implementation for managing database file operations like export and import.
/// </summary>
public sealed class DatabaseManagementService : IDatabaseManagementService
{
    private readonly string _databasePath;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Event raised when the database has been imported and consumers should refresh their data.
    /// </summary>
    public event EventHandler? DatabaseImported;

    public DatabaseManagementService(string databasePath, IServiceProvider serviceProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _databasePath = databasePath;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public string GetDatabasePath() => _databasePath;

    /// <inheritdoc />
    public async Task ExportDatabaseAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        // Ensure destination directory exists
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Close all connections to ensure we get a consistent snapshot
        SqliteConnection.ClearAllPools();

        cancellationToken.ThrowIfCancellationRequested();

        // Copy the database file
        await Task.Run(() => File.Copy(_databasePath, destinationPath, overwrite: true), cancellationToken);
    }

    /// <inheritdoc />
    public async Task ImportDatabaseAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The specified database file was not found.", sourcePath);
        }

        // Validate that the source file is a valid SQLite database
        await ValidateDatabaseFileAsync(sourcePath, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        // Close all connections before replacing the file
        SqliteConnection.ClearAllPools();

        // Create a backup of the current database
        var backupPath = _databasePath + ".backup";
        if (File.Exists(_databasePath))
        {
            await Task.Run(() => File.Copy(_databasePath, backupPath, overwrite: true), cancellationToken);
        }

        try
        {
            // Replace the database file
            await Task.Run(() => File.Copy(sourcePath, _databasePath, overwrite: true), cancellationToken);

            // Raise event to notify consumers that the database has been replaced
            DatabaseImported?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Restore from backup on failure
            if (File.Exists(backupPath))
            {
                await Task.Run(() => File.Copy(backupPath, _databasePath, overwrite: true), CancellationToken.None);
            }
            throw;
        }
        finally
        {
            // Clean up backup file
            if (File.Exists(backupPath))
            {
                try
                {
                    File.Delete(backupPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    private static async Task ValidateDatabaseFileAsync(string sourcePath, CancellationToken cancellationToken)
    {
        // Read the first 16 bytes to check the SQLite header
        var buffer = new byte[16];
        await using var stream = File.OpenRead(sourcePath);
        var bytesRead = await stream.ReadAsync(buffer, cancellationToken);

        if (bytesRead < 16)
        {
            throw new InvalidOperationException("The specified file is not a valid SQLite database.");
        }

        // SQLite files start with "SQLite format 3\0"
        var header = System.Text.Encoding.ASCII.GetString(buffer, 0, 15);
        if (header != "SQLite format 3")
        {
            throw new InvalidOperationException("The specified file is not a valid SQLite database.");
        }
    }
}
