using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiffusionNexus.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DiffusionNexus.DataAccess
{
    public class ConfigurationRepository
    {
        private readonly DiffusionNexusContext _context;

        public ConfigurationRepository(DiffusionNexusContext context)
        {
            _context = context;
        }

        public async Task<List<InstallationConfiguration>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.InstallationConfigurations
                .Include(c => c.Repository)
                .Include(c => c.Python)
                .Include(c => c.Torch)
                .Include(c => c.Paths)
                .Include(c => c.Vram)
                .Include(c => c.GitRepositories)
                .Include(c => c.ModelDownloads)
                .ToListAsync(cancellationToken);
        }

        public async Task<InstallationConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.InstallationConfigurations
                .Include(c => c.Repository)
                .Include(c => c.Python)
                .Include(c => c.Torch)
                .Include(c => c.Paths)
                .Include(c => c.Vram)
                .Include(c => c.GitRepositories)
                .Include(c => c.ModelDownloads)
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

        public async Task<InstallationConfiguration> SaveAsync(
            InstallationConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            var existing = await GetByIdAsync(configuration.Id, cancellationToken);

            if (existing is not null)
            {
                // Detach the existing entity
                _context.Entry(existing).State = EntityState.Detached;
                
                // Update the configuration
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
            // Detach the original entity if it's being tracked
            var trackedEntity = _context.ChangeTracker.Entries<InstallationConfiguration>()
                .FirstOrDefault(e => e.Entity.Id == configuration.Id);
            
            if (trackedEntity is not null)
            {
                trackedEntity.State = EntityState.Detached;
            }

            // Create a new configuration with a new ID and deep copy all properties
            var newConfiguration = new InstallationConfiguration
            {
                Id = Guid.NewGuid(),
                Name = configuration.Name,
                Description = configuration.Description,
                
                Repository = new MainRepositorySettings
                {
                    Type = configuration.Repository.Type,
                    RepositoryUrl = configuration.Repository.RepositoryUrl,
                    Branch = configuration.Repository.Branch,
                    CommitHash = configuration.Repository.CommitHash
                },
                
                Python = new PythonEnvironmentSettings
                {
                    PythonVersion = configuration.Python.PythonVersion,
                    InterpreterPathOverride = configuration.Python.InterpreterPathOverride,
                    CreateVirtualEnvironment = configuration.Python.CreateVirtualEnvironment,
                    CreateVramSettings = configuration.Python.CreateVramSettings,
                    VirtualEnvironmentName = configuration.Python.VirtualEnvironmentName
                },
                
                Torch = new TorchSettings
                {
                    TorchVersion = configuration.Torch.TorchVersion,
                    CudaVersion = configuration.Torch.CudaVersion,
                    IndexUrl = configuration.Torch.IndexUrl
                },
                
                Paths = new PathSettings
                {
                    RootDirectory = configuration.Paths.RootDirectory,
                    DefaultModelDownloadDirectory = configuration.Paths.DefaultModelDownloadDirectory,
                    LogFileName = configuration.Paths.LogFileName
                },
                
                Vram = new VramSettings
                {
                    VramProfiles = configuration.Vram.VramProfiles
                },
                
                GitRepositories = configuration.GitRepositories.Select(r => new GitRepository
                {
                    Id = Guid.NewGuid(),
                    Name = r.Name,
                    Url = r.Url,
                    InstallRequirements = r.InstallRequirements,
                    Priority = r.Priority
                }).ToList(),
                
                ModelDownloads = configuration.ModelDownloads.Select(m => new ModelDownload
                {
                    Id = Guid.NewGuid(),
                    Name = m.Name,
                    Url = m.Url,
                    Destination = m.Destination,
                    VramProfile = m.VramProfile,
                    Enabled = m.Enabled
                }).ToList()
            };

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
    }
}
