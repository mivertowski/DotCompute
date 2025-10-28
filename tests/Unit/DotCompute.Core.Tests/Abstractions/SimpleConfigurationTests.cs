// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Types;

namespace DotCompute.Core.Tests.Abstractions;

/// <summary>
/// Simple configuration tests - ultra-high confidence tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class SimpleConfigurationTests
{
    #region CompilationOptions Basic Tests

    [Fact]
    public void CompilationOptions_DefaultConstructor_SetsDefaults()
    {
        // Act
        var options = new CompilationOptions();

        // Assert
        _ = options.Should().NotBeNull();
        _ = options.OptimizationLevel.Should().Be(OptimizationLevel.Default);
    }

    [Fact]
    public void CompilationOptions_GenerateDebugInfo_DefaultsToFalse()
    {
        // Act
        var options = new CompilationOptions();

        // Assert
        _ = options.GenerateDebugInfo.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CompilationOptions_GenerateDebugInfo_CanBeSet(bool value)
    {
        // Act
        var options = new CompilationOptions { GenerateDebugInfo = value };

        // Assert
        _ = options.GenerateDebugInfo.Should().Be(value);
    }

    [Fact]
    public void CompilationOptions_CanSetAllOptimizationLevels()
    {
        // Act & Assert
        var none = new CompilationOptions { OptimizationLevel = OptimizationLevel.None };
        _ = none.OptimizationLevel.Should().Be(OptimizationLevel.None);

        var o1 = new CompilationOptions { OptimizationLevel = OptimizationLevel.O1 };
        _ = o1.OptimizationLevel.Should().Be(OptimizationLevel.O1);

        var o2 = new CompilationOptions { OptimizationLevel = OptimizationLevel.O2 };
        _ = o2.OptimizationLevel.Should().Be(OptimizationLevel.O2);

        var o3 = new CompilationOptions { OptimizationLevel = OptimizationLevel.O3 };
        _ = o3.OptimizationLevel.Should().Be(OptimizationLevel.O3);
    }

    #endregion

    #region Simple Value Tests

    [Fact]
    public void OptimizationLevel_None_IsZero() =>
        // Act & Assert
        ((int)OptimizationLevel.None).Should().Be(0);

    [Fact]
    public void OptimizationLevel_ValuesAreSequential()
    {
        // Act & Assert
        _ = ((int)OptimizationLevel.O1).Should().Be(1);
        _ = ((int)OptimizationLevel.O2).Should().Be(2);
        _ = ((int)OptimizationLevel.O3).Should().Be(3);
    }

    #endregion
}
