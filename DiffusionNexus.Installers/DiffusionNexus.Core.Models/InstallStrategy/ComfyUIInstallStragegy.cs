using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiffusionNexus.Core.Models.InstallStrategy
{
    // ComfyUI Installation Strategy
    public class ComfyUIInstallStrategy : BaseInstallStrategy
    {
        public override string ApplicationName => "ComfyUI";
        public override string Version => "latest";

        private const string REPO_URL = "https://github.com/comfyanonymous/ComfyUI.git";

        public ComfyUIInstallStrategy(ILogger<BaseInstallStrategy> logger) : base(logger) { }

        public override async Task<InstallResult> InstallAsync(InstallContext context, IProgress<InstallProgress> progress)
        {
            var result = new InstallResult();

            try
            {
                // Step 1: Clone repository
                ReportProgress(progress, 10, "Cloning ComfyUI repository...");
                var cloneSuccess = await RunCommandAsync("git",
                    $"clone {REPO_URL} \"{context.InstallPath}\"");

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
                _logger.LogError(ex, "Failed to install ComfyUI");
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        private async Task SetupDefaultModels(string installPath)
        {
            // Create necessary directories
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

            await Task.CompletedTask;
        }
    }

    // Automatic1111 Installation Strategy
    public class Automatic1111InstallStrategy : BaseInstallStrategy
    {
        public override string ApplicationName => "Automatic1111";
        public override string Version => "latest";

        private const string REPO_URL = "https://github.com/AUTOMATIC1111/stable-diffusion-webui.git";

        public Automatic1111InstallStrategy(ILogger<BaseInstallStrategy> logger) : base(logger) { }

        public override async Task<InstallResult> InstallAsync(InstallContext context, IProgress<InstallProgress> progress)
        {
            var result = new InstallResult();

            try
            {
                // Step 1: Clone repository
                ReportProgress(progress, 10, "Cloning Automatic1111 repository...");
                var cloneSuccess = await RunCommandAsync("git",
                    $"clone {REPO_URL} \"{context.InstallPath}\"");

                if (!cloneSuccess)
                {
                    result.Errors.Add("Failed to clone repository");
                    return result;
                }

                // Step 2: Run webui-user.bat setup (Windows) or webui.sh (Linux/Mac)
                ReportProgress(progress, 40, "Running initial setup...");

                if (OperatingSystem.IsWindows())
                {
                    // Create webui-user.bat with custom settings
                    await CreateWebuiUserBat(context);

                    // Run the setup
                    await RunCommandAsync("cmd.exe",
                        "/c webui-user.bat --skip-torch-cuda-test --exit",
                        context.InstallPath);
                }
                else
                {
                    await RunCommandAsync("bash",
                        "webui.sh --skip-torch-cuda-test --exit",
                        context.InstallPath);
                }

                ReportProgress(progress, 100, "Installation complete!");

                result.Success = true;
                result.InstallPath = context.InstallPath;
                result.Message = "Automatic1111 installed successfully";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to install Automatic1111");
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        private async Task CreateWebuiUserBat(InstallContext context)
        {
                        var content = $@"@echo off

            set PYTHON={context.PythonPath ?? "python"}
            set GIT=git
            set VENV_DIR=venv
            set COMMANDLINE_ARGS=--autolaunch

            ";

            if (context.UseCuda)
            {
                content += $"set CUDA_VERSION={context.CudaVersion}\n";
                content += "set COMMANDLINE_ARGS=%COMMANDLINE_ARGS% --opt-split-attention\n";
            }

            content += "\ncall webui.bat";

            var batPath = Path.Combine(context.InstallPath, "webui-user.bat");
            await File.WriteAllTextAsync(batPath, content);
        }
    }
}
