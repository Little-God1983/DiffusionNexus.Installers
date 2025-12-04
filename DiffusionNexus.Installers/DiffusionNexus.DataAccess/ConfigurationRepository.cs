using DiffusionNexus.Core.Models;
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

        var existing = await GetByIdAsync(configuration.Id, cancellationToken);

        if (existing is not null)
        {
            _context.Entry(existing).State = EntityState.Detached;
            _context.InstallationConfigurations.Update(configuration);
        }
        else
        {
            await _context.InstallationConfigurations.AddAsync(configuration, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return configuration;
    }

    public async Task<InstallationConfiguration> SaveAsNewAsync(
        InstallationConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        DetachTrackedEntity(configuration.Id);

        var newConfiguration = CloneConfiguration(configuration);

        await _context.InstallationConfigurations.AddAsync(newConfiguration, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return newConfiguration;
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
