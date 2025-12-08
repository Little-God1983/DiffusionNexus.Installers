using DiffusionNexus.Core.Models.Configuration;
using DiffusionNexus.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DiffusionNexus.DataAccess;

/// <summary>
/// Repository implementation for InstallationConfiguration entities.
/// </summary>
public sealed class ConfigurationRepository : IConfigurationRepository
{
    private readonly DiffusionNexusContext _context;

    public ConfigurationRepository(DiffusionNexusContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    #region Read Operations

    public async Task<IReadOnlyList<InstallationConfiguration>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await BuildFullQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<InstallationConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await BuildFullQuery()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.InstallationConfigurations
            .AnyAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.InstallationConfigurations.Where(c => c.Name == name);
        
        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }
        
        return await query.AnyAsync(cancellationToken);
    }

    #endregion

    #region Write Operations

    public async Task<InstallationConfiguration> SaveAsync(
        InstallationConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Check if configuration exists without tracking
        var exists = await _context.InstallationConfigurations
            .AsNoTracking()
            .AnyAsync(c => c.Id == configuration.Id, cancellationToken);

        if (exists)
        {
            // Clear all tracked entities to avoid conflicts
            _context.ChangeTracker.Clear();

            // Delete existing child collections that will be replaced
            await DeleteChildCollectionsAsync(configuration.Id, cancellationToken);

            // Clear tracker again after delete operations
            _context.ChangeTracker.Clear();

            // Store child collections temporarily
            var gitRepositories = configuration.GitRepositories.ToList();
            var modelDownloads = configuration.ModelDownloads.ToList();

            // Clear the collections on the configuration
            configuration.GitRepositories.Clear();
            configuration.ModelDownloads.Clear();

            // Attach the configuration entity
            _context.InstallationConfigurations.Attach(configuration);
            
            // Mark the main entity as modified
            _context.Entry(configuration).State = EntityState.Modified;
            
            // Mark all owned entities as modified so their changes are persisted
            _context.Entry(configuration).Reference(c => c.Repository).TargetEntry!.State = EntityState.Modified;
            _context.Entry(configuration).Reference(c => c.Python).TargetEntry!.State = EntityState.Modified;
            _context.Entry(configuration).Reference(c => c.Torch).TargetEntry!.State = EntityState.Modified;
            _context.Entry(configuration).Reference(c => c.Paths).TargetEntry!.State = EntityState.Modified;
            _context.Entry(configuration).Reference(c => c.Vram).TargetEntry!.State = EntityState.Modified;

            // Save the main configuration first
            await _context.SaveChangesAsync(cancellationToken);

            // Now add child entities with new IDs
            foreach (var repo in gitRepositories)
            {
                repo.Id = Guid.NewGuid();
                configuration.GitRepositories.Add(repo);
                _context.Set<GitRepository>().Add(repo);
            }

            foreach (var model in modelDownloads)
            {
                model.Id = Guid.NewGuid();

                foreach (var link in model.DownloadLinks)
                {
                    link.Id = Guid.NewGuid();
                }

                configuration.ModelDownloads.Add(model);
                _context.Set<ModelDownload>().Add(model);
            }

            // Save the children
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // New configuration - ensure all entities have IDs
            if (configuration.Id == Guid.Empty)
            {
                configuration.Id = Guid.NewGuid();
            }

            foreach (var repo in configuration.GitRepositories)
            {
                if (repo.Id == Guid.Empty)
                {
                    repo.Id = Guid.NewGuid();
                }
            }

            foreach (var model in configuration.ModelDownloads)
            {
                if (model.Id == Guid.Empty)
                {
                    model.Id = Guid.NewGuid();
                }

                foreach (var link in model.DownloadLinks)
                {
                    if (link.Id == Guid.Empty)
                    {
                        link.Id = Guid.NewGuid();
                    }
                }
            }

            await _context.InstallationConfigurations.AddAsync(configuration, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return configuration;
    }

    /// <summary>
    /// Deletes all child collections for a configuration to prepare for replacement.
    /// </summary>
    private async Task DeleteChildCollectionsAsync(Guid configurationId, CancellationToken cancellationToken)
    {
        // Use raw SQL or ExecuteDelete for cleaner deletion without tracking issues
        // Delete ModelDownloadLinks first (they reference ModelDownloads)
        var modelDownloadIds = await _context.Set<ModelDownload>()
            .AsNoTracking()
            .Where(m => EF.Property<Guid>(m, "InstallationConfigurationId") == configurationId)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (modelDownloadIds.Count > 0)
        {
            await _context.Set<ModelDownloadLink>()
                .Where(l => modelDownloadIds.Contains(EF.Property<Guid>(l, "ModelDownloadId")))
                .ExecuteDeleteAsync(cancellationToken);
        }

        // Delete ModelDownloads
        await _context.Set<ModelDownload>()
            .Where(m => EF.Property<Guid>(m, "InstallationConfigurationId") == configurationId)
            .ExecuteDeleteAsync(cancellationToken);

        // Delete GitRepositories
        await _context.Set<GitRepository>()
            .Where(r => EF.Property<Guid>(r, "InstallationConfigurationId") == configurationId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<InstallationConfiguration> SaveAsNewAsync(
        InstallationConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        DetachTrackedEntity(configuration.Id);

        var newConfiguration = CloneConfiguration(configuration);

        // Ensure the new configuration has a unique name
        newConfiguration.Name = await GenerateUniqueNameAsync(configuration.Name, cancellationToken);

        await _context.InstallationConfigurations.AddAsync(newConfiguration, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return newConfiguration;
    }

    /// <summary>
    /// Generates a unique configuration name by appending a suffix if needed.
    /// </summary>
    private async Task<string> GenerateUniqueNameAsync(string baseName, CancellationToken cancellationToken)
    {
        // Check if the base name is already unique
        if (!await NameExistsAsync(baseName, null, cancellationToken))
        {
            return baseName;
        }

        // Try appending " (Copy)", " (Copy 2)", etc.
        var copyName = $"{baseName} (Copy)";
        if (!await NameExistsAsync(copyName, null, cancellationToken))
        {
            return copyName;
        }

        // Find the next available number
        var counter = 2;
        while (counter < 100) // Safety limit
        {
            var numberedName = $"{baseName} (Copy {counter})";
            if (!await NameExistsAsync(numberedName, null, cancellationToken))
            {
                return numberedName;
            }
            counter++;
        }

        // Fallback: append timestamp
        return $"{baseName} ({DateTime.Now:yyyyMMdd-HHmmss})";
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var configuration = await GetByIdAsync(id, cancellationToken);
        if (configuration is not null)
        {
            _context.InstallationConfigurations.Remove(configuration);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    #endregion

    #region Private Helpers

    private IQueryable<InstallationConfiguration> BuildFullQuery()
    {
        return _context.InstallationConfigurations
            .Include(c => c.GitRepositories)
            .Include(c => c.ModelDownloads)
                .ThenInclude(m => m.DownloadLinks);
    }

    private void DetachTrackedEntity(Guid configurationId)
    {
        var trackedEntity = _context.ChangeTracker
            .Entries<InstallationConfiguration>()
            .FirstOrDefault(e => e.Entity.Id == configurationId);

        if (trackedEntity is not null)
        {
            trackedEntity.State = EntityState.Detached;
        }
    }

    private static InstallationConfiguration CloneConfiguration(InstallationConfiguration source)
    {
        return new InstallationConfiguration
        {
            Id = Guid.NewGuid(),
            Name = source.Name,
            Description = source.Description,

            Repository = new MainRepositorySettings
            {
                Type = source.Repository.Type,
                RepositoryUrl = source.Repository.RepositoryUrl,
                Branch = source.Repository.Branch,
                CommitHash = source.Repository.CommitHash
            },

            Python = new PythonEnvironmentSettings
            {
                PythonVersion = source.Python.PythonVersion,
                InterpreterPathOverride = source.Python.InterpreterPathOverride,
                CreateVirtualEnvironment = source.Python.CreateVirtualEnvironment,
                CreateVramSettings = source.Python.CreateVramSettings,
                VirtualEnvironmentName = source.Python.VirtualEnvironmentName,
                InstallTriton = source.Python.InstallTriton,
                InstallSageAttention = source.Python.InstallSageAttention
            },

            Torch = new TorchSettings
            {
                TorchVersion = source.Torch.TorchVersion,
                CudaVersion = source.Torch.CudaVersion,
                IndexUrl = source.Torch.IndexUrl
            },

            Paths = new PathSettings
            {
                RootDirectory = source.Paths.RootDirectory,
                DefaultModelDownloadDirectory = source.Paths.DefaultModelDownloadDirectory,
                LogFileName = source.Paths.LogFileName
            },

            Vram = new VramSettings
            {
                VramProfiles = source.Vram.VramProfiles
            },

            GitRepositories = source.GitRepositories.Select(r => new GitRepository
            {
                Id = Guid.NewGuid(),
                Name = r.Name,
                Url = r.Url,
                InstallRequirements = r.InstallRequirements,
                Priority = r.Priority
            }).ToList(),

            ModelDownloads = source.ModelDownloads.Select(m => new ModelDownload
            {
                Id = Guid.NewGuid(),
                Name = m.Name,
                Url = m.Url,
                Destination = m.Destination,
                VramProfile = m.VramProfile,
                Enabled = m.Enabled,
                DownloadLinks = m.DownloadLinks.Select(l => new ModelDownloadLink
                {
                    Id = Guid.NewGuid(),
                    Url = l.Url,
                    VramProfile = l.VramProfile,
                    Destination = l.Destination,
                    Enabled = l.Enabled
                }).ToList()
            }).ToList()
        };
    }

    #endregion
}
