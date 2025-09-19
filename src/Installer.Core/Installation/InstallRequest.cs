using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Installer.Core.Manifests;

namespace Installer.Core.Installation;

public sealed class InstallRequest
{
    public InstallRequest(
        ManifestDescriptor descriptor,
        string installRoot,
        string? selectedVramProfileId = null,
        IEnumerable<string>? enabledOptionalStepIds = null,
        string? logFilePath = null)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        InstallRoot = string.IsNullOrWhiteSpace(installRoot)
            ? throw new ArgumentException("Install root must be provided.", nameof(installRoot))
            : installRoot;
        SelectedVramProfileId = selectedVramProfileId;
        EnabledOptionalStepIds = enabledOptionalStepIds is null
            ? ImmutableHashSet<string>.Empty
            : enabledOptionalStepIds.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
        LogFilePath = logFilePath;
    }

    public ManifestDescriptor Descriptor { get; }

    public InstallManifest Manifest => Descriptor.Manifest;

    public string InstallRoot { get; }

    public string? SelectedVramProfileId { get; }

    public IImmutableSet<string> EnabledOptionalStepIds { get; }

    public string? LogFilePath { get; }
}
