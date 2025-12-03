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

        public async Task<InstallationConfiguration> SaveAsync(
            InstallationConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            var existing = await GetByIdAsync(configuration.Id, cancellationToken);

            if (existing is not null)
            {
                _context.Entry(existing).CurrentValues.SetValues(configuration);
                existing.GitRepositories = configuration.GitRepositories;
                existing.ModelDownloads = configuration.ModelDownloads;
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
            configuration.Id = Guid.NewGuid();
            await _context.InstallationConfigurations.AddAsync(configuration, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return configuration;
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
