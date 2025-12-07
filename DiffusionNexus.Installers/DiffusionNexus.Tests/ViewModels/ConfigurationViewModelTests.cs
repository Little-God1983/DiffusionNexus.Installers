using DiffusionNexus.Core.Models;
using DiffusionNexus.Core.Services;
using DiffusionNexus.DataAccess;
using DiffusionNexus.Installers.ViewModels;
using FluentAssertions;
using Moq;
using Xunit;

namespace DiffusionNexus.Tests.ViewModels;

/// <summary>
/// Unit tests for ConfigurationViewModel.
/// Tests configuration management, repository operations, and model operations.
/// </summary>
public class ConfigurationViewModelTests
{
    private readonly Mock<IConfigurationRepository> _configurationRepositoryMock;
    private readonly Mock<IDatabaseManagementService> _databaseManagementServiceMock;
    private readonly InstallationEngine _installationEngine;

    public ConfigurationViewModelTests()
    {
        _configurationRepositoryMock = new Mock<IConfigurationRepository>();
        _databaseManagementServiceMock = new Mock<IDatabaseManagementService>();
        _installationEngine = new InstallationEngine();

        // Setup default behavior for repository
        _configurationRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstallationConfiguration>());
    }

    private ConfigurationViewModel CreateViewModel()
    {
        return new ConfigurationViewModel(
            _configurationRepositoryMock.Object,
            _databaseManagementServiceMock.Object,
            _installationEngine);
    }

    #region NewConfiguration Tests

    [Fact]
    public void WhenNewConfigurationCalledThenDefaultValuesAreSet()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.NewConfigurationCommand.Execute(null);

        // Assert
        viewModel.SelectedRepositoryType.Should().Be(RepositoryType.ComfyUI);
        viewModel.RepositoryUrl.Should().Be("https://github.com/comfyanonymous/ComfyUI");
        viewModel.SelectedPythonVersion.Should().Be("3.12");
        viewModel.CreateVirtualEnvironment.Should().BeTrue();
        viewModel.VirtualEnvironmentName.Should().Be("venv");
        viewModel.InstallSageAttention.Should().BeTrue();
        viewModel.InstallTriton.Should().BeTrue();
    }

    [Fact]
    public void WhenNewConfigurationCalledThenCollectionsAreCleared()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act
        viewModel.NewConfigurationCommand.Execute(null);

        // Assert
        viewModel.GitRepositories.Should().BeEmpty();
        viewModel.ModelDownloads.Should().BeEmpty();
        viewModel.Logs.Should().BeEmpty();
        viewModel.PreviewPlan.Should().BeEmpty();
        viewModel.ValidationSummary.Should().BeEmpty();
    }

    [Fact]
    public void WhenNewConfigurationCalledThenSelectedSavedConfigurationIsNull()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.NewConfigurationCommand.Execute(null);

        // Assert
        viewModel.SelectedSavedConfiguration.Should().BeNull();
    }

    #endregion

    #region Property Change Tests

    [Fact]
    public void WhenRepositoryTypeChangedToComfyUIThenDefaultsAreApplied()
    {
        // Arrange
        var viewModel = CreateViewModel();
        // Start with a non-ComfyUI type
        viewModel.SelectedRepositoryType = RepositoryType.A1111;
        viewModel.SelectedPythonVersion = "3.10";
        viewModel.InstallSageAttention = false;
        viewModel.InstallTriton = false;

        // Act - Change to ComfyUI
        viewModel.SelectedRepositoryType = RepositoryType.ComfyUI;

        // Assert
        viewModel.SelectedPythonVersion.Should().Be("3.12");
        viewModel.InstallSageAttention.Should().BeTrue();
        viewModel.InstallTriton.Should().BeTrue();
    }

    [Fact]
    public void WhenRepositoryUrlChangedThenPropertyIsUpdated()
    {
        // Arrange
        var viewModel = CreateViewModel();
        const string newUrl = "https://github.com/test/repo";

        // Act
        viewModel.RepositoryUrl = newUrl;

        // Assert
        viewModel.RepositoryUrl.Should().Be(newUrl);
    }

    [Fact]
    public void WhenPythonVersionChangedThenPropertyIsUpdated()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.SelectedPythonVersion = "3.11";

        // Assert
        viewModel.SelectedPythonVersion.Should().Be("3.11");
    }

    [Fact]
    public void WhenCreateVirtualEnvironmentChangedThenPropertyIsUpdated()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.CreateVirtualEnvironment = false;

        // Assert
        viewModel.CreateVirtualEnvironment.Should().BeFalse();
    }

    [Fact]
    public void WhenVirtualEnvironmentNameChangedThenPropertyIsUpdated()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.VirtualEnvironmentName = "custom_venv";

        // Assert
        viewModel.VirtualEnvironmentName.Should().Be("custom_venv");
    }

    [Fact]
    public void WhenInstallSageAttentionEnabledThenTritonIsAlsoEnabled()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.InstallTriton = false;
        viewModel.InstallSageAttention = false;

        // Act
        viewModel.InstallSageAttention = true;

        // Assert
        viewModel.InstallSageAttention.Should().BeTrue();
        viewModel.InstallTriton.Should().BeTrue();
    }

    [Fact]
    public void WhenInterpreterPathOverrideChangedThenPropertyIsUpdated()
    {
        // Arrange
        var viewModel = CreateViewModel();
        const string path = @"C:\Python312\python.exe";

        // Act
        viewModel.InterpreterPathOverride = path;

        // Assert
        viewModel.InterpreterPathOverride.Should().Be(path);
    }

    [Fact]
    public void WhenRootDirectoryChangedThenPropertyIsUpdated()
    {
        // Arrange
        var viewModel = CreateViewModel();
        const string path = @"C:\MyProjects\ComfyUI";

        // Act
        viewModel.RootDirectory = path;

        // Assert
        viewModel.RootDirectory.Should().Be(path);
    }

    [Fact]
    public void WhenDefaultModelDirectoryChangedThenPropertyIsUpdated()
    {
        // Arrange
        var viewModel = CreateViewModel();
        const string path = @"D:\Models";

        // Act
        viewModel.DefaultModelDirectory = path;

        // Assert
        viewModel.DefaultModelDirectory.Should().Be(path);
    }

    [Fact]
    public void WhenLogFileNameChangedThenPropertyIsUpdated()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.LogFileName = "custom.log";

        // Assert
        viewModel.LogFileName.Should().Be("custom.log");
    }

    [Fact]
    public void WhenCudaVersionChangedThenPropertyIsUpdated()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.CudaVersion = "11.8";

        // Assert
        viewModel.CudaVersion.Should().Be("11.8");
    }

    [Fact]
    public void WhenTorchVersionChangedThenPropertyIsUpdated()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.TorchVersion = "2.3.1";

        // Assert
        viewModel.TorchVersion.Should().Be("2.3.1");
    }

    [Fact]
    public void WhenTorchIndexUrlChangedThenPropertyIsUpdated()
    {
        // Arrange
        var viewModel = CreateViewModel();
        const string url = "https://download.pytorch.org/whl/cu121";

        // Act
        viewModel.TorchIndexUrl = url;

        // Assert
        viewModel.TorchIndexUrl.Should().Be(url);
    }

    #endregion

    #region VRAM Settings Tests

    [Fact]
    public void WhenCreateVramSettingsEnabledThenDefaultProfilesAreSet()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.VramProfiles = string.Empty;

        // Act
        viewModel.CreateVramSettings = true;

        // Assert
        viewModel.CreateVramSettings.Should().BeTrue();
        viewModel.VramProfiles.Should().Be("8,16,24,24+");
    }

    [Fact]
    public void WhenVramProfilesChangedThenPropertyIsUpdated()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.CreateVramSettings = true;

        // Act
        viewModel.VramProfiles = "4,8,12";

        // Assert
        viewModel.VramProfiles.Should().Be("4,8,12");
    }

    [Fact]
    public void WhenViewModelCreatedThenVramProfileOptionsAreInitialized()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.AvailableVramProfileOptions.Should().NotBeEmpty();
        viewModel.AvailableVramProfileOptions.Count.Should().Be(VramProfileConstants.DefaultProfiles.Length);
    }

    #endregion

    #region Repository Management Tests

    [Fact]
    public void WhenMoveRepositoryUpCalledOnFirstItemThenNoExceptionThrown()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var repo1 = new GitRepository { Name = "Repo1", Url = "https://github.com/test/repo1", Priority = 1 };
        
        var repoVm1 = new GitRepositoryItemViewModel(repo1, () => { });
        viewModel.GitRepositories.Add(repoVm1);

        // Act - Should not throw even when at start of list
        var action = () => viewModel.MoveRepositoryUpWithParameter(repoVm1);

        // Assert
        action.Should().NotThrow();
        viewModel.GitRepositories[0].Name.Should().Be("Repo1");
    }

    [Fact]
    public void WhenMoveRepositoryDownCalledOnLastItemThenNoExceptionThrown()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var repo1 = new GitRepository { Name = "Repo1", Url = "https://github.com/test/repo1", Priority = 1 };
        
        var repoVm1 = new GitRepositoryItemViewModel(repo1, () => { });
        viewModel.GitRepositories.Add(repoVm1);

        // Act - Should not throw even when at end of list
        var action = () => viewModel.MoveRepositoryDownWithParameter(repoVm1);

        // Assert
        action.Should().NotThrow();
        viewModel.GitRepositories[0].Name.Should().Be("Repo1");
    }

    [Fact]
    public void WhenDeleteRepositoryCalledThenRepositoryIsRemoved()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var repo1 = new GitRepository { Name = "Repo1", Url = "https://github.com/test/repo1", Priority = 1 };
        var repoVm = new GitRepositoryItemViewModel(repo1, () => { });
        viewModel.GitRepositories.Add(repoVm);

        // Act
        viewModel.DeleteRepositoryCommand.Execute(repoVm);

        // Assert
        viewModel.GitRepositories.Should().BeEmpty();
    }

    [Fact]
    public void WhenDeleteRepositoryCalledThenPrioritiesAreUpdated()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var repo1 = new GitRepository { Name = "Repo1", Url = "https://github.com/test/repo1", Priority = 1 };
        var repo2 = new GitRepository { Name = "Repo2", Url = "https://github.com/test/repo2", Priority = 2 };
        var repo3 = new GitRepository { Name = "Repo3", Url = "https://github.com/test/repo3", Priority = 3 };
        
        var repoVm1 = new GitRepositoryItemViewModel(repo1, () => { });
        var repoVm2 = new GitRepositoryItemViewModel(repo2, () => { });
        var repoVm3 = new GitRepositoryItemViewModel(repo3, () => { });
        
        viewModel.GitRepositories.Add(repoVm1);
        viewModel.GitRepositories.Add(repoVm2);
        viewModel.GitRepositories.Add(repoVm3);

        // Act
        viewModel.DeleteRepositoryCommand.Execute(repoVm2);

        // Assert
        viewModel.GitRepositories.Should().HaveCount(2);
        viewModel.GitRepositories[0].Priority.Should().Be(1);
        viewModel.GitRepositories[1].Priority.Should().Be(2);
    }

    [Fact]
    public void WhenMoveRepositoryUpCalledWithNullThenNoException()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        var action = () => viewModel.MoveRepositoryUpWithParameter(null!);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void WhenMoveRepositoryDownCalledWithNullThenNoException()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        var action = () => viewModel.MoveRepositoryDownWithParameter(null!);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void WhenGitRepositoriesAddedThenCountIncreases()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var repo = new GitRepository { Name = "TestRepo", Url = "https://github.com/test/repo" };
        var repoVm = new GitRepositoryItemViewModel(repo, () => { });

        // Act
        viewModel.GitRepositories.Add(repoVm);

        // Assert
        viewModel.GitRepositories.Should().HaveCount(1);
        viewModel.GitRepositories[0].Name.Should().Be("TestRepo");
    }

    [Fact]
    public void WhenRepositoryRemovedByIndexThenCountDecreases()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var repo1 = new GitRepository { Name = "Repo1", Url = "https://github.com/test/repo1" };
        var repo2 = new GitRepository { Name = "Repo2", Url = "https://github.com/test/repo2" };
        
        viewModel.GitRepositories.Add(new GitRepositoryItemViewModel(repo1, () => { }));
        viewModel.GitRepositories.Add(new GitRepositoryItemViewModel(repo2, () => { }));

        // Act
        viewModel.GitRepositories.RemoveAt(0);

        // Assert
        viewModel.GitRepositories.Should().HaveCount(1);
        viewModel.GitRepositories[0].Name.Should().Be("Repo2");
    }

    #endregion

    #region Model Management Tests

    [Fact]
    public void WhenDeleteModelCalledThenModelIsRemoved()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var model = new ModelDownload { Name = "TestModel", Enabled = true };
        var modelVm = new ModelDownloadItemViewModel(model, () => { });
        viewModel.ModelDownloads.Add(modelVm);

        // Act
        viewModel.DeleteModelCommand.Execute(modelVm);

        // Assert
        viewModel.ModelDownloads.Should().BeEmpty();
    }

    [Fact]
    public void WhenDeleteModelCalledWithNullThenNoException()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        var action = () => viewModel.DeleteModelCommand.Execute(null);

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region Static Options Tests

    [Fact]
    public void WhenViewModelCreatedThenRepositoryTypesAreAvailable()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.RepositoryTypes.Should().Contain(RepositoryType.ComfyUI);
        viewModel.RepositoryTypes.Should().Contain(RepositoryType.A1111);
        viewModel.RepositoryTypes.Should().Contain(RepositoryType.Forge);
    }

    [Fact]
    public void WhenViewModelCreatedThenPythonVersionsAreAvailable()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.PythonVersions.Should().Contain("3.10");
        viewModel.PythonVersions.Should().Contain("3.11");
        viewModel.PythonVersions.Should().Contain("3.12");
    }

    [Fact]
    public void WhenViewModelCreatedThenCudaVersionsAreAvailable()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.SuggestedCudaVersions.Should().Contain("12.8");
        viewModel.SuggestedCudaVersions.Should().Contain("12.4");
        viewModel.SuggestedCudaVersions.Should().Contain("11.8");
    }

    [Fact]
    public void WhenViewModelCreatedThenTorchVersionsAreAvailable()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.SuggestedTorchVersions.Should().Contain("2.4.0");
        viewModel.SuggestedTorchVersions.Should().Contain("2.3.1");
        viewModel.SuggestedTorchVersions.Should().Contain(string.Empty);
    }

    #endregion

    #region Busy State Tests

    [Fact]
    public void WhenViewModelCreatedThenIsBusyIsFalse()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.IsBusy.Should().BeFalse();
    }

    #endregion

    #region Collections Initialization Tests

    [Fact]
    public void WhenViewModelCreatedThenCollectionsAreInitialized()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.GitRepositories.Should().NotBeNull();
        viewModel.ModelDownloads.Should().NotBeNull();
        viewModel.Logs.Should().NotBeNull();
        viewModel.SavedConfigurations.Should().NotBeNull();
        viewModel.AvailableVramProfileOptions.Should().NotBeNull();
    }

    #endregion
}
