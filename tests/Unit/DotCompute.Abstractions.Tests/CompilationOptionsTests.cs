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
        _ = options.OptimizationLevel.Should().Be(OptimizationLevel.Default);
        _ = options.EnableDebugInfo.Should().BeFalse();
        _ = options.AdditionalFlags.Should().BeNull();
        _ = options.Defines.Should().BeNull();
        _ = options.FastMath.Should().BeFalse();
        _ = options.UnrollLoops.Should().BeFalse();
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
        _ = options.OptimizationLevel.Should().Be(OptimizationLevel.Maximum);
        _ = options.EnableDebugInfo.Should().BeTrue();
        _ = options.AdditionalFlags.Should().BeEquivalentTo(flags);
        _ = options.Defines.Should().BeEquivalentTo(defines);
        _ = options.FastMath.Should().BeTrue();
        _ = options.UnrollLoops.Should().BeTrue();
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
        _ = options.OptimizationLevel.Should().Be(level);
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
        _ = options1.OptimizationLevel.Should().Be(OptimizationLevel.None);
        _ = options1.EnableDebugInfo.Should().BeTrue();
        _ = options1.FastMath.Should().BeFalse();

        _ = options2.OptimizationLevel.Should().Be(OptimizationLevel.Maximum);
        _ = options2.EnableDebugInfo.Should().BeFalse();
        _ = options2.FastMath.Should().BeTrue();
    }

    [Fact]
    public void Defines_CanBeNull()
    {
        // Act
        var options = new CompilationOptions { Defines = null };

        // Assert
        _ = options.Defines.Should().BeNull();
    }

    [Fact]
    public void AdditionalFlags_CanBeEmpty()
    {
        // Act
        var options = new CompilationOptions { AdditionalFlags = [] };

        // Assert
        _ = options.AdditionalFlags.Should().BeEmpty();
    }
}
