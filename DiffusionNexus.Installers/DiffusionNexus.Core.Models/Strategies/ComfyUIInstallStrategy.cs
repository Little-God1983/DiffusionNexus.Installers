using DiffusionNexus.Core.Models.Installation;
using Serilog;

namespace DiffusionNexus.Core.Models.Strategies;

/// <summary>
/// ComfyUI Installation Strategy.
/// </summary>
public class ComfyUIInstallStrategy : BaseInstallStrategy
{
    public override string ApplicationName => "ComfyUI";
    public override string Version => "latest";

    private const string RepoUrl = "https://github.com/comfyanonymous/ComfyUI.git";

    public override async Task<InstallResult> InstallAsync(InstallContext context, IProgress<InstallProgress> progress)
    {
        var result = new InstallResult();

        try
        {
            // Step 1: Clone repository
            ReportProgress(progress, 10, "Cloning ComfyUI repository...");
            var cloneSuccess = await RunCommandAsync("git",
                $"clone {RepoUrl} \"{context.InstallPath}\"");

            if (!cloneSuccess)
            {
                result.Errors.Add("Failed to clone repository");
                return result;
            }

            // Step 2: Create virtual environment if needed
            if (context.CreateVirtualEnvironment)
            {
                ReportProgress(progress, 30, "Creating virtual environment...");
                await RunCommandAsync(context.PythonPath ?? "python",
                    "-m venv venv",
                    context.InstallPath);
            }

            // Step 3: Install requirements
            ReportProgress(progress, 50, "Installing Python dependencies...");
            var pipPath = context.CreateVirtualEnvironment
                ? Path.Combine(context.InstallPath, "venv", "Scripts", "pip.exe")
                : "pip";

            // Install PyTorch with CUDA if needed
            if (context.UseCuda)
            {
                ReportProgress(progress, 60, "Installing PyTorch with CUDA support...");
                await RunCommandAsync(pipPath,
                    $"install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu{context.CudaVersion}",
                    context.InstallPath);
            }

            // Install other requirements
            ReportProgress(progress, 80, "Installing remaining dependencies...");
            await RunCommandAsync(pipPath,
                "install -r requirements.txt",
                context.InstallPath);

            // Step 4: Download models if needed
            ReportProgress(progress, 90, "Setting up default models...");
            await SetupDefaultModels(context.InstallPath);

            ReportProgress(progress, 100, "Installation complete!");

            result.Success = true;
            result.InstallPath = context.InstallPath;
            result.Message = "ComfyUI installed successfully";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to install ComfyUI");
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private static Task SetupDefaultModels(string installPath)
    {
        var modelPaths = new[]
        {
            Path.Combine(installPath, "models", "checkpoints"),
            Path.Combine(installPath, "models", "vae"),
            Path.Combine(installPath, "models", "loras"),
            Path.Combine(installPath, "output"),
            Path.Combine(installPath, "input")
        };

        foreach (var path in modelPaths)
        {
            Directory.CreateDirectory(path);
        }

        return Task.CompletedTask;
    }
}
