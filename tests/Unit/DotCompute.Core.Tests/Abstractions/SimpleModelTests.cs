// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Configuration;
using DotCompute.Abstractions.Enums;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Memory;
using DotCompute.Abstractions.Models;
using DotCompute.Abstractions.Types;
using FluentAssertions;
using Xunit;
using MemoryOptions = DotCompute.Abstractions.Memory.MemoryOptions;

namespace DotCompute.Core.Tests.Abstractions;

/// <summary>
/// Simple tests for basic model classes and enums - high confidence passing tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class SimpleModelTests
{
    #region CompilationOptions Tests

    [Fact]
    public void CompilationOptions_DefaultConstructor_CreatesInstance()
    {
        // Act
        var options = new CompilationOptions();

        // Assert
        options.Should().NotBeNull();
    }

    [Theory]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.O1)]
    [InlineData(OptimizationLevel.O2)]
    [InlineData(OptimizationLevel.O3)]
    public void CompilationOptions_OptimizationLevel_CanBeSet(OptimizationLevel level)
    {
        // Act
        var options = new CompilationOptions { OptimizationLevel = level };

        // Assert
        options.OptimizationLevel.Should().Be(level);
    }

    [Fact]
    public void CompilationOptions_GenerateDebugInfo_DefaultsFalse()
    {
        // Act
        var options = new CompilationOptions();

        // Assert
        options.GenerateDebugInfo.Should().BeFalse();
    }

    [Fact]
    public void CompilationOptions_GenerateDebugInfo_CanBeSetTrue()
    {
        // Act
        var options = new CompilationOptions { GenerateDebugInfo = true };

        // Assert
        options.GenerateDebugInfo.Should().BeTrue();
    }

    #endregion

    #region MemoryOptions Tests

    [Fact]
    public void MemoryOptions_None_HasValue()
    {
        // Act
        var option = MemoryOptions.None;

        // Assert
        option.Should().Be(MemoryOptions.None);
    }

    [Fact]
    public void MemoryOptions_Pinned_HasDistinctValue()
    {
        // Act & Assert
        MemoryOptions.Pinned.Should().NotBe(MemoryOptions.None);
    }

    [Fact]
    public void MemoryOptions_Mapped_HasDistinctValue()
    {
        // Act & Assert
        MemoryOptions.Mapped.Should().NotBe(MemoryOptions.None);
        MemoryOptions.Mapped.Should().NotBe(MemoryOptions.Pinned);
    }

    [Fact]
    public void MemoryOptions_FlagsEnum_SupportsMultipleFlags()
    {
        // Act
        var combined = MemoryOptions.Pinned | MemoryOptions.Mapped;

        // Assert
        combined.Should().HaveFlag(MemoryOptions.Pinned);
        combined.Should().HaveFlag(MemoryOptions.Mapped);
    }

    #endregion

    #region DeviceCapabilities Tests

    [Fact]
    public void DeviceCapabilities_DefaultConstructor_CreatesInstance()
    {
        // Act
        var capabilities = new DeviceCapabilities();

        // Assert
        capabilities.Should().NotBeNull();
    }

    [Fact]
    public void DeviceCapabilities_MaxWorkGroupSize_CanBeSet()
    {
        // Act
        var capabilities = new DeviceCapabilities { MaxWorkGroupSize = 1024 };

        // Assert
        capabilities.MaxWorkGroupSize.Should().Be(1024);
    }

    [Fact]
    public void DeviceCapabilities_GlobalMemorySize_CanBeSet()
    {
        // Act
        var capabilities = new DeviceCapabilities { GlobalMemorySize = 8_000_000_000 };

        // Assert
        capabilities.GlobalMemorySize.Should().Be(8_000_000_000);
    }

    #endregion

    #region Enum Tests

    [Fact]
    public void AcceleratorType_HasCpuValue()
    {
        // Act & Assert
        Enum.IsDefined(typeof(AcceleratorType), AcceleratorType.CPU).Should().BeTrue();
    }

    [Fact]
    public void AcceleratorType_HasGpuValue()
    {
        // Act & Assert
        Enum.IsDefined(typeof(AcceleratorType), AcceleratorType.GPU).Should().BeTrue();
    }

    [Fact]
    public void OptimizationLevel_HasExpectedValues()
    {
        // Act & Assert
        Enum.IsDefined(typeof(OptimizationLevel), OptimizationLevel.None).Should().BeTrue();
        Enum.IsDefined(typeof(OptimizationLevel), OptimizationLevel.O1).Should().BeTrue();
        Enum.IsDefined(typeof(OptimizationLevel), OptimizationLevel.O2).Should().BeTrue();
        Enum.IsDefined(typeof(OptimizationLevel), OptimizationLevel.O3).Should().BeTrue();
    }

    [Theory]
    [InlineData(PrecisionMode.Single)]
    [InlineData(PrecisionMode.Double)]
    [InlineData(PrecisionMode.Half)]
    public void PrecisionMode_HasExpectedValue(PrecisionMode mode)
    {
        // Act & Assert
        Enum.IsDefined(typeof(PrecisionMode), mode).Should().BeTrue();
    }

    #endregion

    #region MemoryStatistics Tests

    [Fact]
    public void MemoryStatistics_DefaultInstance_HasZeroValues()
    {
        // Act
        var stats = new MemoryStatistics();

        // Assert
        stats.TotalAllocated.Should().Be(0);
        stats.CurrentUsage.Should().Be(0);
        stats.AllocationCount.Should().Be(0);
    }

    [Fact]
    public void MemoryStatistics_InitProperties_CanBeSet()
    {
        // Act
        var stats = new MemoryStatistics
        {
            TotalAllocated = 1000,
            CurrentUsage = 500,
            PeakUsage = 800,
            AllocationCount = 10
        };

        // Assert
        stats.TotalAllocated.Should().Be(1000);
        stats.CurrentUsage.Should().Be(500);
        stats.PeakUsage.Should().Be(800);
        stats.AllocationCount.Should().Be(10);
    }

    #endregion

    #region KernelDefinition Tests

    [Fact]
    public void KernelDefinition_WithNameAndSource_CreatesInstance()
    {
        // Act
        var definition = new KernelDefinition
        {
            Name = "TestKernel",
            Source = "kernel void test() { }"
        };

        // Assert
        definition.Name.Should().Be("TestKernel");
        definition.Source.Should().Be("kernel void test() { }");
    }

    [Fact]
    public void KernelDefinition_EntryPoint_CanBeSet()
    {
        // Act
        var definition = new KernelDefinition
        {
            Name = "Test",
            EntryPoint = "main_kernel"
        };

        // Assert
        definition.EntryPoint.Should().Be("main_kernel");
    }

    #endregion
}
