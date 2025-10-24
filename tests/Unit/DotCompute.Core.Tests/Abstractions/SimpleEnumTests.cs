// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Enums;
using DotCompute.Abstractions.Types;
using FluentAssertions;
using Xunit;

namespace DotCompute.Core.Tests.Abstractions;

/// <summary>
/// Simple enum validation tests - extremely high confidence tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class SimpleEnumTests
{
    #region AcceleratorType Tests

    [Fact]
    public void AcceleratorType_CPU_IsDefined()
    {
        // Act & Assert
        Enum.IsDefined(typeof(AcceleratorType), AcceleratorType.CPU).Should().BeTrue();
    }

    [Fact]
    public void AcceleratorType_GPU_IsDefined()
    {
        // Act & Assert
        Enum.IsDefined(typeof(AcceleratorType), AcceleratorType.GPU).Should().BeTrue();
    }

    [Fact]
    public void AcceleratorType_CPU_NotEqualToGPU()
    {
        // Act & Assert
        AcceleratorType.CPU.Should().NotBe(AcceleratorType.GPU);
    }

    #endregion

    #region OptimizationLevel Tests

    [Theory]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.O1)]
    [InlineData(OptimizationLevel.O2)]
    [InlineData(OptimizationLevel.O3)]
    public void OptimizationLevel_AllValues_AreDefined(OptimizationLevel level)
    {
        // Act & Assert
        Enum.IsDefined(typeof(OptimizationLevel), level).Should().BeTrue();
    }

    [Fact]
    public void OptimizationLevel_None_IsZero()
    {
        // Act & Assert
        ((int)OptimizationLevel.None).Should().Be(0);
    }

    [Fact]
    public void OptimizationLevel_ValuesAreDistinct()
    {
        // Act & Assert
        OptimizationLevel.None.Should().NotBe(OptimizationLevel.O1);
        OptimizationLevel.O1.Should().NotBe(OptimizationLevel.O2);
        OptimizationLevel.O2.Should().NotBe(OptimizationLevel.O3);
    }

    #endregion

    #region PrecisionMode Tests

    [Theory]
    [InlineData(PrecisionMode.Single)]
    [InlineData(PrecisionMode.Double)]
    [InlineData(PrecisionMode.Half)]
    public void PrecisionMode_AllValues_AreDefined(PrecisionMode mode)
    {
        // Act & Assert
        Enum.IsDefined(typeof(PrecisionMode), mode).Should().BeTrue();
    }

    [Fact]
    public void PrecisionMode_ValuesAreDistinct()
    {
        // Act & Assert
        PrecisionMode.Single.Should().NotBe(PrecisionMode.Double);
        PrecisionMode.Double.Should().NotBe(PrecisionMode.Half);
        PrecisionMode.Half.Should().NotBe(PrecisionMode.Single);
    }

    #endregion
}
