using System.Text.Json.Serialization;

namespace DiffusionNexus.Core.Models.Installation;

/// <summary>
/// Proxy configuration for downloads.
/// </summary>
public class ProxySettings
{
    public bool UseProxy { get; set; } = false;
    public string HttpProxy { get; set; } = string.Empty;
    public string HttpsProxy { get; set; } = string.Empty;
    public List<string> NoProxy { get; set; } = [];
    public string Username { get; set; } = string.Empty;

    [JsonIgnore]
    public string Password { get; set; } = string.Empty;
}
