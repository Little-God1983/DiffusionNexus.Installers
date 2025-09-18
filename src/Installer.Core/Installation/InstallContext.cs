using System;
using System.Collections.Generic;
using System.IO;
using Installer.Core.Logging;
using Installer.Core.Manifests;

namespace Installer.Core.Installation;

public sealed class InstallContext
{
    private readonly Dictionary<string, string> _pathAliases;

    public InstallContext(InstallRequest request, ILogSink logSink)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Log = logSink ?? throw new ArgumentNullException(nameof(logSink));
        RootDirectory = Path.GetFullPath(request.InstallRoot);
        Directory.CreateDirectory(RootDirectory);

        _pathAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["<installRoot>"] = RootDirectory,
            ["installRoot"] = RootDirectory,
        };

        if (!string.IsNullOrWhiteSpace(request.Manifest.BaseSoftware?.Target))
        {
            var baseTarget = CombineWithRoot(request.Manifest.BaseSoftware!.Target);
            _pathAliases["baseSoftware.target"] = baseTarget;
        }
    }

    public InstallRequest Request { get; }

    public string RootDirectory { get; }

    public ILogSink Log { get; }

    public string CacheDirectory
    {
        get
        {
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var path = Path.Combine(basePath, "AIKnowledge2Go", "cache");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public VramProfileConfig? SelectedVramProfile
    {
        get
        {
            var manifest = Request.Manifest;
            if (manifest.VramProfiles.Count == 0)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(Request.SelectedVramProfileId))
            {
                return manifest.VramProfiles[0];
            }

            return manifest.VramProfiles
                .Find(p => string.Equals(p.Id, Request.SelectedVramProfileId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public string CombineWithRoot(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return RootDirectory;
        }

        var combined = Path.GetFullPath(Path.Combine(RootDirectory, relativePath));
        return combined;
    }

    public string ResolvePath(string? relativeTo, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return RootDirectory;
        }

        if (!string.IsNullOrWhiteSpace(relativeTo) && _pathAliases.TryGetValue(relativeTo!, out var aliasTarget))
        {
            return Path.GetFullPath(Path.Combine(aliasTarget, path));
        }

        return CombineWithRoot(path);
    }
}
