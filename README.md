# AIKnowledge2Go – Easy Installer

AIKnowledge2Go – Easy Installer is a cross-platform Avalonia UI application that loads JSON manifests describing end-to-end diffusion workflow installs. It lets you pick an installation directory, choose a manifest, and watch progress with live logging. The codebase is organised into reusable core components that can later be embedded into the DiffusionNexus host.

## Solution layout

- `Installer.Core` – domain models, manifest loader, logging utilities, and the mock installer engine.
- `Installer.UI` – Avalonia desktop application that provides the MVVM UI and user interactions.
- `Installer.Tests` – xUnit test project with unit coverage for the manifest loader and installer engine.
- `manifests/` – example manifests for ComfyUI, ForgeUI, and Automatic1111 installs.

## Prerequisites

- .NET 8 SDK

## Build & run

Restore dependencies and run the application:

```bash
dotnet restore
cd Installer.UI
dotnet run
```

The UI automatically discovers manifests placed in the `manifests/` folder (also copied to the output directory on build). Select an install directory, choose a manifest, optionally pick a VRAM profile, then click **Install** to execute the scripted install. Logs stream live in the UI and are written to disk.

To create a self-contained single-file build (example for Windows x64):

```bash
dotnet publish Installer.UI/Installer.UI.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  /p:PublishSingleFile=true
```

## Testing

Run the unit tests from the solution root:

```bash
dotnet test
```

## Manifest authoring

Manifests follow the schema described in `Installer.Core/Manifests/InstallManifest.cs`. Drop additional `.json` files into the `manifests/` directory and they will appear in the UI without restarting.
