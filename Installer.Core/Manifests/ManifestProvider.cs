using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace AIKnowledge2Go.Installers.Core.Manifests;

public sealed class ManifestProvider : IManifestProvider
{
    private const string ManifestSchemaJson = """
    {
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "type": "object",
      "required": ["schemaVersion", "id", "title", "baseSoftware"],
      "properties": {
        "schemaVersion": { "type": "string" },
        "id": { "type": "string" },
        "title": { "type": "string" },
        "baseSoftware": {
          "type": "object",
          "required": ["name", "target"],
          "properties": {
            "name": { "type": "string" },
            "target": { "type": "string" }
          }
        }
      }
    }
    """;

    private readonly string _manifestDirectory;
    private readonly bool _enableWatcher;
    private readonly Action<string>? _log;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly JsonSchema _schema;
    private readonly object _gate = new();
    private List<ManifestDescriptor> _manifests = new();
    private FileSystemWatcher? _watcher;
    private bool _isDisposed;

    public ManifestProvider(string manifestDirectory, bool enableWatcher = true, Action<string>? log = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(manifestDirectory);

        _manifestDirectory = manifestDirectory;
        _enableWatcher = enableWatcher;
        _log = log;
        _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        _serializerOptions.Converters.Add(new PreferenceSelectorJsonConverter());
        _schema = JsonSchema.FromText(ManifestSchemaJson);
    }

    public event EventHandler<ManifestsChangedEventArgs>? ManifestsChanged;

    public IReadOnlyList<ManifestDescriptor> Manifests
    {
        get
        {
            lock (_gate)
            {
                return _manifests.ToImmutableArray();
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Directory.CreateDirectory(_manifestDirectory);
        await RefreshAsync(cancellationToken).ConfigureAwait(false);

        if (_enableWatcher)
        {
            _watcher = new FileSystemWatcher(_manifestDirectory, "*.json")
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true,
            };

            _watcher.Created += OnWatcherChanged;
            _watcher.Changed += OnWatcherChanged;
            _watcher.Renamed += OnWatcherChanged;
            _watcher.Deleted += OnWatcherChanged;
        }
    }

    public async Task<IReadOnlyList<ManifestDescriptor>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!Directory.Exists(_manifestDirectory))
        {
            return Array.Empty<ManifestDescriptor>();
        }

        var manifestFiles = Directory.EnumerateFiles(_manifestDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var manifests = new List<ManifestDescriptor>(manifestFiles.Length);

        foreach (var file in manifestFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                var node = JsonNode.Parse(json);
                if (node is null)
                {
                    continue;
                }

                ValidateSchema(file, node);
                var manifest = node.Deserialize<InstallManifest>(_serializerOptions);

                if (manifest is null)
                {
                    throw new ManifestValidationException($"Manifest '{file}' is empty after deserialisation.");
                }

                manifests.Add(new ManifestDescriptor(manifest.Title, manifest.Id, file, manifest));
            }
            catch (Exception ex) when (ex is ManifestValidationException or JsonException or IOException)
            {
                _log?.Invoke($"Failed to load manifest '{file}': {ex.Message}");
            }
        }

        lock (_gate)
        {
            _manifests = manifests;
        }

        ManifestsChanged?.Invoke(this, new ManifestsChangedEventArgs(Manifests));

        return Manifests;
    }

    private void ValidateSchema(string filePath, JsonNode node)
    {
        var evaluation = _schema.Evaluate(node, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
        });

        if (evaluation.IsValid)
        {
            return;
        }

        var errors = evaluation.Details
            .Where(d => !d.IsValid)
            .Select(d => d.ToString())
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .ToArray();

        if (errors.Length == 0)
        {
            errors = new[] { "Manifest does not satisfy the schema." };
        }

        throw new ManifestValidationException(
            $"Manifest '{Path.GetFileName(filePath)}' is invalid: {string.Join(", ", errors)}");
    }

    private void OnWatcherChanged(object sender, FileSystemEventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        _ = RefreshAsync();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (_watcher is not null)
        {
            _watcher.Created -= OnWatcherChanged;
            _watcher.Changed -= OnWatcherChanged;
            _watcher.Renamed -= OnWatcherChanged;
            _watcher.Deleted -= OnWatcherChanged;
            _watcher.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(ManifestProvider));
        }
    }
}
