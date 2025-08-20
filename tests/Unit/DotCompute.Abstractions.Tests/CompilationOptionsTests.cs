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
        _ = options.AdditionalFlags.Should().NotBeNull().And.BeEmpty();
        _ = options.Defines.Should().NotBeNull().And.BeEmpty();
        _ = options.EnableFastMath.Should().BeTrue();
        _ = options.UnrollLoops.Should().BeFalse();
        _ = options.PreferredBlockSize.Should().Be(new Dim3(256, 1, 1));
        _ = options.SharedMemorySize.Should().Be(0);
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
            AdditionalFlags = new List<string>(flags),
            Defines = defines,
            EnableFastMath = true,
            UnrollLoops = true,
            PreferredBlockSize = new Dim3(512, 1, 1),
            SharedMemorySize = 2048
        };

        // Assert
        _ = options.OptimizationLevel.Should().Be(OptimizationLevel.Maximum);
        _ = options.EnableDebugInfo.Should().BeTrue();
        _ = options.AdditionalFlags.Should().BeEquivalentTo(flags);
        _ = options.Defines.Should().BeEquivalentTo(defines);
        _ = options.EnableFastMath.Should().BeTrue();
        _ = options.UnrollLoops.Should().BeTrue();
        _ = options.PreferredBlockSize.Should().Be(new Dim3(512, 1, 1));
        _ = options.SharedMemorySize.Should().Be(2048);
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
            EnableFastMath = true
        };

        // Assert
        _ = options1.OptimizationLevel.Should().Be(OptimizationLevel.None);
        _ = options1.EnableDebugInfo.Should().BeTrue();
        _ = options1.EnableFastMath.Should().BeTrue(); // Default value

        _ = options2.OptimizationLevel.Should().Be(OptimizationLevel.Maximum);
        _ = options2.EnableDebugInfo.Should().BeFalse();
        _ = options2.EnableFastMath.Should().BeTrue();
    }

    [Fact]
    public void Defines_CanBeSetToCustomDictionary()
    {
        // Act
        var customDefines = new Dictionary<string, string> { ["TEST"] = "1" };
        var options = new CompilationOptions { Defines = customDefines };

        // Assert
        _ = options.Defines.Should().BeEquivalentTo(customDefines);
    }

    [Fact]
    public void AdditionalFlags_CanBeEmpty()
    {
        // Act
        var options = new CompilationOptions { AdditionalFlags = [] };

        // Assert
        _ = options.AdditionalFlags.Should().BeEmpty();
    }

    [Fact]
    public void Clone_ShouldCopyAllProperties()
    {
        // Arrange
        var original = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Aggressive,
            EnableDebugInfo = true,
            UnrollLoops = true,
            PreferredBlockSize = new Dim3(512, 2, 1),
            SharedMemorySize = 4096,
            EnableFastMath = false
        };

        // Act
        var cloned = original.Clone();

        // Assert
        _ = cloned.OptimizationLevel.Should().Be(original.OptimizationLevel);
        _ = cloned.EnableDebugInfo.Should().Be(original.EnableDebugInfo);
        _ = cloned.UnrollLoops.Should().Be(original.UnrollLoops);
        _ = cloned.PreferredBlockSize.Should().Be(original.PreferredBlockSize);
        _ = cloned.SharedMemorySize.Should().Be(original.SharedMemorySize);
        _ = cloned.EnableFastMath.Should().Be(original.EnableFastMath);
        
        // Ensure it's a different instance
        _ = ReferenceEquals(original, cloned).Should().BeFalse();
    }
}
