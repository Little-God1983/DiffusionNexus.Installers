using DiffusionNexus.Core.Models;
using DiffusionNexus.Installers.ViewModels;
using FluentAssertions;
using Xunit;

namespace DiffusionNexus.Tests.ViewModels;

/// <summary>
/// Unit tests for GitRepositoryItemViewModel.
/// Tests property bindings and model synchronization.
/// </summary>
public class GitRepositoryItemViewModelTests
{
    [Fact]
    public void WhenConstructedThenPropertiesMatchModel()
    {
        // Arrange
        var model = new GitRepository
        {
            Name = "TestRepo",
            Url = "https://github.com/test/repo",
            InstallRequirements = true,
            Priority = 5
        };

        // Act
        var viewModel = new GitRepositoryItemViewModel(model, () => { });

        // Assert
        viewModel.Name.Should().Be("TestRepo");
        viewModel.Url.Should().Be("https://github.com/test/repo");
        viewModel.InstallRequirements.Should().BeTrue();
        viewModel.Priority.Should().Be(5);
        viewModel.Model.Should().BeSameAs(model);
    }

    [Fact]
    public void WhenNameChangedThenModelIsUpdated()
    {
        // Arrange
        var model = new GitRepository { Name = "Original" };
        var viewModel = new GitRepositoryItemViewModel(model, () => { });

        // Act
        viewModel.Name = "Updated";

        // Assert
        model.Name.Should().Be("Updated");
    }

    [Fact]
    public void WhenUrlChangedThenModelIsUpdated()
    {
        // Arrange
        var model = new GitRepository { Url = "https://original.com" };
        var viewModel = new GitRepositoryItemViewModel(model, () => { });

        // Act
        viewModel.Url = "https://updated.com";

        // Assert
        model.Url.Should().Be("https://updated.com");
    }

    [Fact]
    public void WhenInstallRequirementsChangedThenModelIsUpdated()
    {
        // Arrange
        var model = new GitRepository { InstallRequirements = false };
        var viewModel = new GitRepositoryItemViewModel(model, () => { });

        // Act
        viewModel.InstallRequirements = true;

        // Assert
        model.InstallRequirements.Should().BeTrue();
    }

    [Fact]
    public void WhenPriorityChangedThenModelIsUpdated()
    {
        // Arrange
        var model = new GitRepository { Priority = 1 };
        var viewModel = new GitRepositoryItemViewModel(model, () => { });

        // Act
        viewModel.Priority = 10;

        // Assert
        model.Priority.Should().Be(10);
    }

    [Fact]
    public void WhenPropertyChangedThenCallbackIsInvoked()
    {
        // Arrange
        var callbackInvoked = false;
        var model = new GitRepository();
        var viewModel = new GitRepositoryItemViewModel(model, () => callbackInvoked = true);

        // Act
        viewModel.Name = "NewName";

        // Assert
        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    public void WhenMultiplePropertiesChangedThenCallbackIsInvokedForEach()
    {
        // Arrange
        var callbackCount = 0;
        var model = new GitRepository
        {
            Name = "Initial",
            Url = "https://initial.com",
            InstallRequirements = false
        };
        var viewModel = new GitRepositoryItemViewModel(model, () => callbackCount++);
        
        // Reset callback count after construction (since construction sets properties)
        callbackCount = 0;

        // Act
        viewModel.Name = "Name1";
        viewModel.Url = "https://url1.com";
        viewModel.InstallRequirements = true;

        // Assert
        callbackCount.Should().Be(3);
    }
}
