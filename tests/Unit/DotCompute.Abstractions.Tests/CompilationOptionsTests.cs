using DotCompute.Abstractions;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit;

public sealed class CompilationOptionsTests
{
    [Fact]
    public void Default_Constructor_ShouldSetDefaultValues()
    {
        // Act
        var options = new CompilationOptions();

        // Assert
        options.OptimizationLevel.Should().Be(OptimizationLevel.Default);
        options.EnableDebugInfo.Should().BeFalse();
        options.AdditionalFlags.Should().BeNull();
        options.Defines.Should().BeNull();
        options.FastMath.Should().BeFalse();
        options.UnrollLoops.Should().BeFalse();
    }

    [Fact]
    public void InitProperties_ShouldSetCorrectly()
    {
        // Arrange
        var flags = new[] { "-flag1", "-flag2" };
        var defines = new Dictionary<string, string> { ["DEBUG"] = "1", ["VERSION"] = "1.0" };

        // Act
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Maximum,
            EnableDebugInfo = true,
            AdditionalFlags = flags,
            Defines = defines,
            FastMath = true,
            UnrollLoops = true
        };

        // Assert
        options.OptimizationLevel.Should().Be(OptimizationLevel.Maximum);
        options.EnableDebugInfo.Should().BeTrue();
        options.AdditionalFlags.Should().BeEquivalentTo(flags);
        options.Defines.Should().BeEquivalentTo(defines);
        options.FastMath.Should().BeTrue();
        options.UnrollLoops.Should().BeTrue();
    }

    [Theory]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.Default)]
    [InlineData(OptimizationLevel.Maximum)]
    public void OptimizationLevel_ShouldAcceptAllValues(OptimizationLevel level)
    {
        // Act
        var options = new CompilationOptions { OptimizationLevel = level };

        // Assert
        options.OptimizationLevel.Should().Be(level);
    }

    [Fact]
    public void Multiple_Instances_ShouldBeIndependent()
    {
        // Arrange
        var options1 = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.None,
            EnableDebugInfo = true
        };

        var options2 = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Maximum,
            FastMath = true
        };

        // Assert
        options1.OptimizationLevel.Should().Be(OptimizationLevel.None);
        options1.EnableDebugInfo.Should().BeTrue();
        options1.FastMath.Should().BeFalse();

        options2.OptimizationLevel.Should().Be(OptimizationLevel.Maximum);
        options2.EnableDebugInfo.Should().BeFalse();
        options2.FastMath.Should().BeTrue();
    }

    [Fact]
    public void Defines_CanBeNull()
    {
        // Act
        var options = new CompilationOptions { Defines = null };

        // Assert
        options.Defines.Should().BeNull();
    }

    [Fact]
    public void AdditionalFlags_CanBeEmpty()
    {
        // Act
        var options = new CompilationOptions { AdditionalFlags = [] };

        // Assert
        options.AdditionalFlags.Should().BeEmpty();
    }
}
