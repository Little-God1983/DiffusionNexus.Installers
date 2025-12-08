using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using DiffusionNexus.Core.Models.Configuration;

namespace DiffusionNexus.Core.Services
{
    public enum ConfigurationFormat
    {
        Json,
        Xml
    }

    /// <summary>
    /// Handles persistence of <see cref="InstallationConfiguration"/> instances.
    /// </summary>
    public class ConfigurationService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        public async Task<InstallationConfiguration> LoadAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            var format = GetFormatFromExtension(filePath);

            await using var stream = File.OpenRead(filePath);
            cancellationToken.ThrowIfCancellationRequested();

            return format switch
            {
                ConfigurationFormat.Json => await JsonSerializer
                    .DeserializeAsync<InstallationConfiguration>(stream, JsonOptions, cancellationToken)
                    ?? throw new InvalidOperationException("Failed to deserialize configuration from JSON."),
                ConfigurationFormat.Xml => (InstallationConfiguration)(new XmlSerializer(typeof(InstallationConfiguration))
                    .Deserialize(stream) ?? throw new InvalidOperationException("Failed to deserialize configuration from XML.")),
                _ => throw new NotSupportedException($"Unsupported configuration format '{format}'.")
            };
        }

        public async Task SaveAsync(
            InstallationConfiguration configuration,
            string filePath,
            ConfigurationFormat? format = null,
            CancellationToken cancellationToken = default)
        {
            var resolvedFormat = format ?? GetFormatFromExtension(filePath);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? ".");

            await using var stream = File.Create(filePath);
            cancellationToken.ThrowIfCancellationRequested();

            switch (resolvedFormat)
            {
                case ConfigurationFormat.Json:
                    await JsonSerializer.SerializeAsync(stream, configuration, JsonOptions, cancellationToken);
                    break;
                case ConfigurationFormat.Xml:
                    var serializer = new XmlSerializer(typeof(InstallationConfiguration));
                    serializer.Serialize(stream, configuration);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported configuration format '{resolvedFormat}'.");
            }
        }

        public static ConfigurationFormat GetFormatFromExtension(string filePath)
        {
            var extension = Path.GetExtension(filePath);

            return extension.ToLowerInvariant() switch
            {
                ".json" => ConfigurationFormat.Json,
                ".xml" => ConfigurationFormat.Xml,
                _ => throw new NotSupportedException(
                    "Unsupported file extension. Use .json or .xml for configuration files.")
            };
        }
    }
}
