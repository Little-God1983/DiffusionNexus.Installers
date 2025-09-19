# AIKnowledge2Go – Easy Installer

AIKnowledge2Go – Easy Installer is a cross-platform Avalonia application that reads JSON manifests and orchestrates scripted installs for diffusion model toolchains such as ComfyUI, Automatic1111, and Forge UI. The installer ships with a lightweight core engine that can be embedded into the broader DiffusionNexus ecosystem.

## Features

- JSON manifest loader with live file system watching (`manifests/*.json`).
- Extensible installation engine that stages base software, dependencies, models, extensions, and optional steps.
- Rich desktop UI built with Avalonia 11:
  - Manifest picker, install folder browser, VRAM profile selector.
  - Real-time progress, per-step status, and live log output.
  - Log export and clipboard copy utilities.
  - Telemetry opt-in persisted to user settings.
- Logs are streamed to the UI and persisted to disk in the install directory.

## Repository layout

```
AIKnowledge2Go.Installers.sln
├── manifests/                 # Example manifests consumed by the app
├── src/
│   ├── Installer.Core/        # Manifest models, installer engine, logging utilities
│   └── Installer.UI/          # Avalonia desktop UI
└── tests/
    └── Installer.Tests/       # xUnit test suite for the core services
```

## Prerequisites

- .NET 8 SDK
- A platform supported by Avalonia Desktop (Windows, macOS, or Linux)

## Building

```
dotnet restore
 dotnet build AIKnowledge2Go.Installers.sln
```

## Running the application

```
dotnet run --project src/Installer.UI/Installer.UI.csproj
```

The application scans the `manifests` directory at startup. Dropping new JSON manifests into this folder will make them available immediately in the manifest picker.

## Tests

```
dotnet test
```

## Publishing

Single-file self-contained builds can be produced with the standard `dotnet publish` workflow. For example:

```
dotnet publish src/Installer.UI/Installer.UI.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

The publish folder includes the bundled manifests so the installer is ready to run out of the box.

## Manifest schema summary

Key fields supported by the installer engine:

- `baseSoftware`: Primary repository/archive to prepare.
- `dependencies`: Python/CUDA metadata and additional requirement files.
- `vramProfiles`: VRAM-specific preferences surfaced in the UI.
- `models` and `extensions`: Downloads to queue during installation.
- `optionalSteps`: User-selectable shell commands executed after the base install.

Refer to the example manifests in the repository for reference structures.

## Telemetry

Telemetry is disabled by default. Users may opt in via the checkbox in the main window; the preference is stored in `%LOCALAPPDATA%/AIKnowledge2Go/EasyInstaller/settings.json` (or the platform equivalent).

## License

This project is provided as sample tooling for DiffusionNexus. Ensure you comply with the licenses of any third-party software or models referenced in the manifests.
