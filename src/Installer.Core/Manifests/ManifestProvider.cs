using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Installer.Core.Manifests;

public sealed class ManifestProvider : IManifestProvider
{
    private readonly string _manifestDirectory;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly Action<string>? _trace;
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public ManifestProvider(string manifestDirectory, Action<string>? trace = null)
    {
        if (string.IsNullOrWhiteSpace(manifestDirectory))
        {
            throw new ArgumentException("Manifest directory cannot be null or empty.", nameof(manifestDirectory));
        }

        _manifestDirectory = Path.GetFullPath(manifestDirectory);
        _trace = trace;

        Directory.CreateDirectory(_manifestDirectory);

        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        ConfigureWatcher();
    }

    public event EventHandler? ManifestsChanged;

    public async Task<IReadOnlyList<ManifestDescriptor>> GetManifestsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(_manifestDirectory))
        {
            return Array.Empty<ManifestDescriptor>();
        }

        var manifests = new List<ManifestDescriptor>();
        foreach (var file in Directory.EnumerateFiles(_manifestDirectory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var manifest = await JsonSerializer.DeserializeAsync<InstallManifest>(stream, _serializerOptions, cancellationToken)
                               .ConfigureAwait(false);

                if (manifest is null)
                {
                    continue;
                }

                NormalizeManifest(manifest, file);
                if (!IsManifestValid(manifest))
                {
                    _trace?.Invoke($"Manifest '{file}' is missing required fields and will be ignored.");
                    continue;
                }
                manifests.Add(new ManifestDescriptor(file, manifest));
            }
            catch (JsonException jsonEx)
            {
                _trace?.Invoke($"Failed to parse manifest '{file}': {jsonEx.Message}");
            }
            catch (IOException ioEx)
            {
                _trace?.Invoke($"Failed to read manifest '{file}': {ioEx.Message}");
            }
        }

        return manifests;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnWatcherChanged;
            _watcher.Changed -= OnWatcherChanged;
            _watcher.Deleted -= OnWatcherChanged;
            _watcher.Renamed -= OnWatcherRenamed;
            _watcher.Dispose();
        }
    }

    private void ConfigureWatcher()
    {
        var watcher = new FileSystemWatcher(_manifestDirectory, "*.json")
        {
            IncludeSubdirectories = false,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        watcher.Created += OnWatcherChanged;
        watcher.Changed += OnWatcherChanged;
        watcher.Deleted += OnWatcherChanged;
        watcher.Renamed += OnWatcherRenamed;

        _watcher = watcher;
    }

    private void OnWatcherChanged(object sender, FileSystemEventArgs e)
    {
        _trace?.Invoke($"Manifest directory change detected: {e.ChangeType} {e.Name}");
        ManifestsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnWatcherRenamed(object sender, RenamedEventArgs e) => OnWatcherChanged(sender, e);

    private static void NormalizeManifest(InstallManifest manifest, string file)
    {
        manifest.BaseSoftware ??= new BaseSoftwareConfig();
        manifest.Dependencies ??= new DependencyConfig();
        manifest.VramProfiles ??= new List<VramProfileConfig>();
        manifest.Models ??= new List<ModelConfig>();
        manifest.Extensions ??= new List<ExtensionConfig>();
        manifest.OptionalSteps ??= new List<OptionalStepConfig>();

        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            manifest.Id = Path.GetFileNameWithoutExtension(file);
        }

        if (string.IsNullOrWhiteSpace(manifest.Title))
        {
            manifest.Title = manifest.Id;
        }
    }

    private static bool IsManifestValid(InstallManifest manifest)
    {
        if (manifest.BaseSoftware is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.BaseSoftware.Target))
        {
            return false;
        }

        var hasSource = !string.IsNullOrWhiteSpace(manifest.BaseSoftware.RepositoryUrl)
                        || !string.IsNullOrWhiteSpace(manifest.BaseSoftware.Name);

        if (!hasSource)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(manifest.Id)
               && !string.IsNullOrWhiteSpace(manifest.Title)
               && !string.IsNullOrWhiteSpace(manifest.SchemaVersion);
    }
}
