using DiffusionNexus.Installers.ViewModels;
using FluentAssertions;
using Xunit;

namespace DiffusionNexus.Tests.ViewModels;

/// <summary>
/// Unit tests for VramProfileOption.
/// Tests selection behavior and callback mechanics.
/// </summary>
public class VramProfileOptionTests
{
    [Fact]
    public void WhenConstructedThenPropertiesAreSet()
    {
        // Arrange & Act
        var option = new VramProfileOption(16, true);

        // Assert
        option.Value.Should().Be(16);
        option.IsSelected.Should().BeTrue();
        option.DisplayName.Should().Be("16 GB");
    }

    [Fact]
    public void WhenConstructedWithDefaultsThenIsNotSelected()
    {
        // Arrange & Act
        var option = new VramProfileOption(8);

        // Assert
        option.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void WhenDisplayNameAccessedThenFormattedCorrectly()
    {
        // Arrange
        var option4 = new VramProfileOption(4);
        var option24 = new VramProfileOption(24);
        var option48 = new VramProfileOption(48);

        // Assert
        option4.DisplayName.Should().Be("4 GB");
        option24.DisplayName.Should().Be("24 GB");
        option48.DisplayName.Should().Be("48 GB");
    }

    [Fact]
    public void WhenSelectedThenCallbackIsInvoked()
    {
        // Arrange
        var callbackValue = 0;
        var option = new VramProfileOption(16, false, value => callbackValue = value);

        // Act
        option.IsSelected = true;

        // Assert
        callbackValue.Should().Be(16);
    }

    [Fact]
    public void WhenDeselectedThenReselectedAutomatically()
    {
        // Arrange
        var option = new VramProfileOption(8, true);

        // Act
        option.IsSelected = false;

        // Assert - Should remain selected (radio button behavior)
        option.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void WhenSetSelectedWithoutCallbackThenCallbackNotInvoked()
    {
        // Arrange
        var callbackInvoked = false;
        var option = new VramProfileOption(12, false, _ => callbackInvoked = true);

        // Act
        option.SetSelectedWithoutCallback(true);

        // Assert
        option.IsSelected.Should().BeTrue();
        callbackInvoked.Should().BeFalse();
    }

    [Fact]
    public void WhenSetSelectedWithoutCallbackToFalseThenOptionIsDeselected()
    {
        // Arrange
        var option = new VramProfileOption(16, true);

        // Act
        option.SetSelectedWithoutCallback(false);

        // Assert
        option.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void WhenMultipleOptionsExistThenOnlyOneCallbackTriggered()
    {
        // Arrange
        var callbackCounts = new Dictionary<int, int> { { 8, 0 }, { 16, 0 }, { 24, 0 } };
        var options = new[]
        {
            new VramProfileOption(8, true, v => callbackCounts[v]++),
            new VramProfileOption(16, false, v => callbackCounts[v]++),
            new VramProfileOption(24, false, v => callbackCounts[v]++)
        };

        // Act - Select the 16GB option
        options[1].IsSelected = true;

        // Assert
        callbackCounts[8].Should().Be(0);
        callbackCounts[16].Should().Be(1);
        callbackCounts[24].Should().Be(0);
    }
}
