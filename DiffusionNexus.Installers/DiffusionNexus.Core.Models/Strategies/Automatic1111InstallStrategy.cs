using DiffusionNexus.Core.Models.Installation;
using Serilog;

namespace DiffusionNexus.Core.Models.Strategies;

/// <summary>
/// Automatic1111 Installation Strategy.
/// </summary>
public class Automatic1111InstallStrategy : BaseInstallStrategy
{
    public override string ApplicationName => "Automatic1111";
    public override string Version => "latest";

    private const string RepoUrl = "https://github.com/AUTOMATIC1111/stable-diffusion-webui.git";

    public override async Task<InstallResult> InstallAsync(InstallContext context, IProgress<InstallProgress> progress)
    {
        var result = new InstallResult();

        try
        {
            // Step 1: Clone repository
            ReportProgress(progress, 10, "Cloning Automatic1111 repository...");
            var cloneSuccess = await RunCommandAsync("git",
                $"clone {RepoUrl} \"{context.InstallPath}\"");

            if (!cloneSuccess)
            {
                result.Errors.Add("Failed to clone repository");
                return result;
            }

            // Step 2: Run webui-user.bat setup (Windows) or webui.sh (Linux/Mac)
            ReportProgress(progress, 40, "Running initial setup...");

            if (OperatingSystem.IsWindows())
            {
                await CreateWebuiUserBat(context);
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
            Log.Error(ex, "Failed to install Automatic1111");
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private static async Task CreateWebuiUserBat(InstallContext context)
    {
        var content = $"""
            @echo off

            set PYTHON={context.PythonPath ?? "python"}
            set GIT=git
            set VENV_DIR=venv
            set COMMANDLINE_ARGS=--autolaunch

            """;

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
