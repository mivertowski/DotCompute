using DotCompute.Abstractions;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit;

public class MemoryOptionsTests
{
    [Fact]
    public void None_ShouldHaveNoFlags()
    {
        // Act
        var options = MemoryOptions.None;

        // Assert
        options.Should().Be((MemoryOptions)0);
    }

    [Fact]
    public void MemoryOptions_ShouldBeFlags()
    {
        // Arrange
        var options = MemoryOptions.ReadOnly | MemoryOptions.HostVisible;

        // Assert
        options.HasFlag(MemoryOptions.ReadOnly).Should().BeTrue();
        options.HasFlag(MemoryOptions.HostVisible).Should().BeTrue();
        options.HasFlag(MemoryOptions.WriteOnly).Should().BeFalse();
    }

    [Fact]
    public void Individual_Flags_ShouldBeCorrect()
    {
        // Assert
        MemoryOptions.ReadOnly.Should().Be((MemoryOptions)1);
        MemoryOptions.WriteOnly.Should().Be((MemoryOptions)2);
        MemoryOptions.HostVisible.Should().Be((MemoryOptions)4);
        MemoryOptions.Cached.Should().Be((MemoryOptions)8);
        MemoryOptions.Atomic.Should().Be((MemoryOptions)16);
    }

    [Fact]
    public void Combined_Flags_ShouldWork()
    {
        // Arrange
        var options = MemoryOptions.ReadOnly | MemoryOptions.Cached;

        // Assert
        ((int)options).Should().Be(1 | 8);
        options.Should().HaveFlag(MemoryOptions.ReadOnly);
        options.Should().HaveFlag(MemoryOptions.Cached);
        options.Should().NotHaveFlag(MemoryOptions.WriteOnly);
    }

    [Theory]
    [InlineData(MemoryOptions.None, 0)]
    [InlineData(MemoryOptions.ReadOnly, 1)]
    [InlineData(MemoryOptions.WriteOnly, 2)]
    [InlineData(MemoryOptions.HostVisible, 4)]
    [InlineData(MemoryOptions.Cached, 8)]
    [InlineData(MemoryOptions.Atomic, 16)]
    public void Flag_Values_ShouldBeCorrect(MemoryOptions option, int expectedValue) => ((int)option).Should().Be(expectedValue);

    [Fact]
    public void Multiple_Flags_CanBeCombined()
    {
        // Arrange
        var options = MemoryOptions.ReadOnly | MemoryOptions.Cached | MemoryOptions.HostVisible;

        // Act & Assert
        options.HasFlag(MemoryOptions.ReadOnly).Should().BeTrue();
        options.HasFlag(MemoryOptions.Cached).Should().BeTrue();
        options.HasFlag(MemoryOptions.HostVisible).Should().BeTrue();
        options.HasFlag(MemoryOptions.WriteOnly).Should().BeFalse();
        options.HasFlag(MemoryOptions.Atomic).Should().BeFalse();
    }

    [Fact]
    public void ReadOnly_And_WriteOnly_CanBeCombined()
    {
        // This might seem contradictory but it's valid for flags
        // Arrange
        var options = MemoryOptions.ReadOnly | MemoryOptions.WriteOnly;

        // Act & Assert
        options.HasFlag(MemoryOptions.ReadOnly).Should().BeTrue();
        options.HasFlag(MemoryOptions.WriteOnly).Should().BeTrue();
    }
}
