using DiffusionNexus.Core.Models.Configuration;
using DiffusionNexus.Installers.ViewModels;
using FluentAssertions;
using Xunit;

namespace DiffusionNexus.Tests.ViewModels;


/// <summary>
/// Unit tests for ConfigurationListItemViewModel.
/// Tests display formatting and property access.
/// </summary>
public class ConfigurationListItemViewModelTests
{
    [Fact]
    public void WhenConstructedThenPropertiesMatchConfiguration()
    {
        // Arrange
        var config = new InstallationConfiguration
        {
            Id = Guid.NewGuid(),
            Name = "My Configuration",
            Description = "Test description"
        };

        // Act
        var viewModel = new ConfigurationListItemViewModel(config, () => { });

        // Assert
        viewModel.Id.Should().Be(config.Id);
        viewModel.Name.Should().Be("My Configuration");
        viewModel.Description.Should().Be("Test description");
        viewModel.Configuration.Should().BeSameAs(config);
    }

    [Fact]
    public void WhenDescriptionIsEmptyThenDisplayShowsOnlyName()
    {
        // Arrange
        var config = new InstallationConfiguration
        {
            Name = "Simple Config",
            Description = string.Empty
        };

        // Act
        var viewModel = new ConfigurationListItemViewModel(config, () => { });

        // Assert
        viewModel.Display.Should().Be("Simple Config");
    }

    [Fact]
    public void WhenDescriptionIsWhitespaceThenDisplayShowsOnlyName()
    {
        // Arrange
        var config = new InstallationConfiguration
        {
            Name = "Simple Config",
            Description = "   "
        };

        // Act
        var viewModel = new ConfigurationListItemViewModel(config, () => { });

        // Assert
        viewModel.Display.Should().Be("Simple Config");
    }

    [Fact]
    public void WhenDescriptionExistsThenDisplayShowsNameAndDescription()
    {
        // Arrange
        var config = new InstallationConfiguration
        {
            Name = "Full Config",
            Description = "With description"
        };

        // Act
        var viewModel = new ConfigurationListItemViewModel(config, () => { });

        // Assert
        viewModel.Display.Should().Be("Full Config - With description");
    }

    [Fact]
    public void WhenOnChangedCallbackProvidedThenItIsAccessible()
    {
        // Arrange
        var callbackInvoked = false;
        var config = new InstallationConfiguration();

        // Act
        var viewModel = new ConfigurationListItemViewModel(config, () => callbackInvoked = true);
        viewModel.OnChanged();

        // Assert
        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    public void WhenIdAccessedThenReturnsConfigurationId()
    {
        // Arrange
        var expectedId = Guid.NewGuid();
        var config = new InstallationConfiguration { Id = expectedId };

        // Act
        var viewModel = new ConfigurationListItemViewModel(config, () => { });

        // Assert
        viewModel.Id.Should().Be(expectedId);
    }
}
