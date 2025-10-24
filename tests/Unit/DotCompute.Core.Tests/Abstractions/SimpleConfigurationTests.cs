// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Types;
using FluentAssertions;
using Xunit;

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
        options.Should().NotBeNull();
        options.OptimizationLevel.Should().Be(OptimizationLevel.None);
    }

    [Fact]
    public void CompilationOptions_GenerateDebugInfo_DefaultsToFalse()
    {
        // Act
        var options = new CompilationOptions();

        // Assert
        options.GenerateDebugInfo.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CompilationOptions_GenerateDebugInfo_CanBeSet(bool value)
    {
        // Act
        var options = new CompilationOptions { GenerateDebugInfo = value };

        // Assert
        options.GenerateDebugInfo.Should().Be(value);
    }

    [Fact]
    public void CompilationOptions_CanSetAllOptimizationLevels()
    {
        // Act & Assert
        var none = new CompilationOptions { OptimizationLevel = OptimizationLevel.None };
        none.OptimizationLevel.Should().Be(OptimizationLevel.None);

        var o1 = new CompilationOptions { OptimizationLevel = OptimizationLevel.O1 };
        o1.OptimizationLevel.Should().Be(OptimizationLevel.O1);

        var o2 = new CompilationOptions { OptimizationLevel = OptimizationLevel.O2 };
        o2.OptimizationLevel.Should().Be(OptimizationLevel.O2);

        var o3 = new CompilationOptions { OptimizationLevel = OptimizationLevel.O3 };
        o3.OptimizationLevel.Should().Be(OptimizationLevel.O3);
    }

    #endregion

    #region Simple Value Tests

    [Fact]
    public void OptimizationLevel_None_IsZero()
    {
        // Act & Assert
        ((int)OptimizationLevel.None).Should().Be(0);
    }

    [Fact]
    public void OptimizationLevel_ValuesAreSequential()
    {
        // Act & Assert
        ((int)OptimizationLevel.O1).Should().Be(1);
        ((int)OptimizationLevel.O2).Should().Be(2);
        ((int)OptimizationLevel.O3).Should().Be(3);
    }

    #endregion
}
