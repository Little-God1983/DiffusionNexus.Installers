using DiffusionNexus.Core.Models.Entities;
using DiffusionNexus.Core.Models.Enums;
using DiffusionNexus.Installers.ViewModels;
using FluentAssertions;
using Xunit;

namespace DiffusionNexus.Tests.ViewModels;

/// <summary>
/// Unit tests for ModelDownloadItemViewModel.
/// Tests property bindings and model synchronization.
/// </summary>
public class ModelDownloadItemViewModelTests
{
    [Fact]
    public void WhenConstructedThenPropertiesMatchModel()
    {
        // Arrange
        var model = new ModelDownload
        {
            Name = "TestModel",
            Url = "https://example.com/model.safetensors",
            Destination = @"C:\Models\checkpoints",
            VramProfile = VramProfile.VRAM_16GB,
            Enabled = true
        };

        // Act
        var viewModel = new ModelDownloadItemViewModel(model, () => { });

        // Assert
        viewModel.Name.Should().Be("TestModel");
        viewModel.Url.Should().Be("https://example.com/model.safetensors");
        viewModel.Destination.Should().Be(@"C:\Models\checkpoints");
        viewModel.VramProfile.Should().Be(VramProfile.VRAM_16GB);
        viewModel.Enabled.Should().BeTrue();
        viewModel.Model.Should().BeSameAs(model);
    }

    [Fact]
    public void WhenNameChangedThenModelIsUpdated()
    {
        // Arrange
        var model = new ModelDownload { Name = "Original" };
        var viewModel = new ModelDownloadItemViewModel(model, () => { });

        // Act
        viewModel.Name = "Updated";

        // Assert
        model.Name.Should().Be("Updated");
    }

    [Fact]
    public void WhenUrlChangedThenModelIsUpdated()
    {
        // Arrange
        var model = new ModelDownload { Url = "https://original.com" };
        var viewModel = new ModelDownloadItemViewModel(model, () => { });

        // Act
        viewModel.Url = "https://updated.com";

        // Assert
        model.Url.Should().Be("https://updated.com");
    }

    [Fact]
    public void WhenDestinationChangedThenModelIsUpdated()
    {
        // Arrange
        var model = new ModelDownload { Destination = @"C:\Original" };
        var viewModel = new ModelDownloadItemViewModel(model, () => { });

        // Act
        viewModel.Destination = @"D:\Updated";

        // Assert
        model.Destination.Should().Be(@"D:\Updated");
    }

    [Fact]
    public void WhenVramProfileChangedThenModelIsUpdated()
    {
        // Arrange
        var model = new ModelDownload { VramProfile = VramProfile.VRAM_8GB };
        var viewModel = new ModelDownloadItemViewModel(model, () => { });

        // Act
        viewModel.VramProfile = VramProfile.VRAM_24GB;

        // Assert
        model.VramProfile.Should().Be(VramProfile.VRAM_24GB);
    }

    [Fact]
    public void WhenEnabledChangedThenModelIsUpdated()
    {
        // Arrange
        var model = new ModelDownload { Enabled = true };
        var viewModel = new ModelDownloadItemViewModel(model, () => { });

        // Act
        viewModel.Enabled = false;

        // Assert
        model.Enabled.Should().BeFalse();
    }

    [Fact]
    public void WhenPropertyChangedThenCallbackIsInvoked()
    {
        // Arrange
        var callbackInvoked = false;
        var model = new ModelDownload();
        var viewModel = new ModelDownloadItemViewModel(model, () => callbackInvoked = true);

        // Act
        viewModel.Name = "NewName";

        // Assert
        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    public void WhenRefreshDownloadLinksCountCalledThenCountIsUpdated()
    {
        // Arrange
        var model = new ModelDownload
        {
            DownloadLinks = new List<ModelDownloadLink>
            {
                new ModelDownloadLink { Url = "https://link1.com" },
                new ModelDownloadLink { Url = "https://link2.com" }
            }
        };
        var viewModel = new ModelDownloadItemViewModel(model, () => { });

        // Assert initial count
        viewModel.DownloadLinksCount.Should().Be(2);

        // Add a new link to the model
        model.DownloadLinks.Add(new ModelDownloadLink { Url = "https://link3.com" });

        // Act
        viewModel.RefreshDownloadLinksCount();

        // Assert updated count
        viewModel.DownloadLinksCount.Should().Be(3);
    }

    [Fact]
    public void WhenConstructedWithDownloadLinksThenCountIsSet()
    {
        // Arrange
        var model = new ModelDownload
        {
            DownloadLinks = new List<ModelDownloadLink>
            {
                new ModelDownloadLink(),
                new ModelDownloadLink(),
                new ModelDownloadLink(),
                new ModelDownloadLink(),
                new ModelDownloadLink()
            }
        };

        // Act
        var viewModel = new ModelDownloadItemViewModel(model, () => { });

        // Assert
        viewModel.DownloadLinksCount.Should().Be(5);
    }
}
