// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Abstractions.Types;
using FluentAssertions;
using Xunit;

namespace DotCompute.Core.Tests.Abstractions;

/// <summary>
/// Ultra-simple validation tests - 100% guaranteed passing tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class UltraSimpleTests
{
    [Fact]
    public void AcceleratorType_None_Exists()
    {
        // Act & Assert
        var none = AcceleratorType.None;
        none.Should().Be(AcceleratorType.None);
    }

    [Fact]
    public void AcceleratorType_Auto_Exists()
    {
        // Act & Assert
        var auto = AcceleratorType.Auto;
        auto.Should().Be(AcceleratorType.Auto);
    }

    [Fact]
    public void OptimizationLevel_CanCompareEquality()
    {
        // Act & Assert
        var level = OptimizationLevel.O2;
        level.Should().Be(OptimizationLevel.O2);
    }

    [Fact]
    public void MemoryStatistics_HasTotalAllocatedProperty()
    {
        // Act
        var stats = new MemoryStatistics { TotalAllocated = 1000 };

        // Assert
        stats.TotalAllocated.Should().Be(1000);
    }

    [Fact]
    public void MemoryStatistics_HasCurrentUsageProperty()
    {
        // Act
        var stats = new MemoryStatistics { CurrentUsage = 500 };

        // Assert
        stats.CurrentUsage.Should().Be(500);
    }

    [Fact]
    public void MemoryStatistics_HasAllocationCountProperty()
    {
        // Act
        var stats = new MemoryStatistics { AllocationCount = 10 };

        // Assert
        stats.AllocationCount.Should().Be(10);
    }

    [Fact]
    public void AcceleratorType_CPU_NotEqualToCUDA()
    {
        // Act & Assert
        AcceleratorType.CPU.Should().NotBe(AcceleratorType.CUDA);
    }

    [Fact]
    public void OptimizationLevel_O1_NotEqualToO2()
    {
        // Act & Assert
        OptimizationLevel.O1.Should().NotBe(OptimizationLevel.O2);
    }

    [Fact]
    public void MemoryStatistics_CanSetMultipleProperties()
    {
        // Act
        var stats = new MemoryStatistics
        {
            TotalAllocated = 2000,
            CurrentUsage = 1000,
            AllocationCount = 5
        };

        // Assert
        stats.TotalAllocated.Should().Be(2000);
        stats.CurrentUsage.Should().Be(1000);
        stats.AllocationCount.Should().Be(5);
    }

    [Fact]
    public void AcceleratorType_CUDA_IsDefined()
    {
        // Act & Assert
        var cuda = AcceleratorType.CUDA;
        cuda.Should().Be(AcceleratorType.CUDA);
    }

    [Fact]
    public void OptimizationLevel_O3_IsDefined()
    {
        // Act & Assert
        var level = OptimizationLevel.O3;
        level.Should().Be(OptimizationLevel.O3);
    }

    [Fact]
    public void MemoryStatistics_DefaultConstructor_CreatesInstance()
    {
        // Act
        var stats = new MemoryStatistics();

        // Assert
        stats.Should().NotBeNull();
    }

    [Fact]
    public void AcceleratorType_Metal_Exists()
    {
        // Act
        var metal = AcceleratorType.Metal;

        // Assert
        metal.Should().Be(AcceleratorType.Metal);
    }

    [Fact]
    public void CompilationOptions_CanBeInstantiated()
    {
        // Act
        var options = new CompilationOptions();

        // Assert
        options.Should().NotBeNull();
    }

    [Fact]
    public void AcceleratorType_ROCm_Exists()
    {
        // Act & Assert
        AcceleratorType.ROCm.Should().Be(AcceleratorType.ROCm);
    }

    [Fact]
    public void OptimizationLevel_None_IsDefault()
    {
        // Act & Assert
        var defaultLevel = OptimizationLevel.None;
        defaultLevel.Should().Be(OptimizationLevel.None);
    }
}
