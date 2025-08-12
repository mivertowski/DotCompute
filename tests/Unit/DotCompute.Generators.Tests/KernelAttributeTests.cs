// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Kernel;
using FluentAssertions;
using Xunit;

namespace DotCompute.Tests.Unit;

public class KernelAttributeTests
{
    [Fact]
    public void KernelAttribute_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var attribute = new KernelAttribute();

        // Assert
        attribute.Backends.Should().Be(KernelBackends.CPU, "Default backend should be CPU");
        attribute.VectorSize.Should().Be(8, "Default vector size should be 8 (256-bit)");
        attribute.IsParallel.Should().BeTrue("Default should enable parallel execution");
        attribute.GridDimensions.Should().BeNull("Grid dimensions should be null by default");
        attribute.BlockDimensions.Should().BeNull("Block dimensions should be null by default");
        attribute.Optimizations.Should().Be(OptimizationHints.None, "Default should be no optimizations");
        attribute.MemoryPattern.Should().Be(MemoryAccessPattern.Sequential, "Default should be sequential access");
    }

    [Fact]
    public void KernelAttribute_SetBackends_ShouldStoreValue()
    {
        // Arrange
        var attribute = new KernelAttribute();

        // Act
        attribute.Backends = KernelBackends.CUDA | KernelBackends.Metal;

        // Assert
        attribute.Backends.Should().Be(KernelBackends.CUDA | KernelBackends.Metal);
        attribute.Backends.Should().HaveFlag(KernelBackends.CUDA);
        attribute.Backends.Should().HaveFlag(KernelBackends.Metal);
        attribute.Backends.Should().NotHaveFlag(KernelBackends.CPU);
    }

    [Fact]
    public void KernelAttribute_SetVectorSize_ShouldStoreValue()
    {
        // Arrange
        var attribute = new KernelAttribute();

        // Act
        attribute.VectorSize = 16;

        // Assert
        attribute.VectorSize.Should().Be(16);
    }

    [Fact]
    public void KernelAttribute_SetIsParallel_ShouldStoreValue()
    {
        // Arrange
        var attribute = new KernelAttribute();

        // Act
        attribute.IsParallel = false;

        // Assert
        attribute.IsParallel.Should().BeFalse();
    }

    [Fact]
    public void KernelAttribute_SetGridDimensions_ShouldStoreValue()
    {
        // Arrange
        var attribute = new KernelAttribute();
        var gridDims = new[] { 256, 256, 1 };

        // Act
        attribute.GridDimensions = gridDims;

        // Assert
        attribute.GridDimensions.Should().BeEquivalentTo(gridDims);
    }

    [Fact]
    public void KernelAttribute_SetBlockDimensions_ShouldStoreValue()
    {
        // Arrange
        var attribute = new KernelAttribute();
        var blockDims = new[] { 16, 16, 1 };

        // Act
        attribute.BlockDimensions = blockDims;

        // Assert
        attribute.BlockDimensions.Should().BeEquivalentTo(blockDims);
    }

    [Fact]
    public void KernelAttribute_SetOptimizations_ShouldStoreValue()
    {
        // Arrange
        var attribute = new KernelAttribute();

        // Act
        attribute.Optimizations = OptimizationHints.Vectorize | OptimizationHints.FastMath;

        // Assert
        attribute.Optimizations.Should().HaveFlag(OptimizationHints.Vectorize);
        attribute.Optimizations.Should().HaveFlag(OptimizationHints.FastMath);
        attribute.Optimizations.Should().NotHaveFlag(OptimizationHints.LoopUnrolling);
    }

    [Fact]
    public void KernelAttribute_SetMemoryPattern_ShouldStoreValue()
    {
        // Arrange
        var attribute = new KernelAttribute();

        // Act
        attribute.MemoryPattern = MemoryAccessPattern.Random;

        // Assert
        attribute.MemoryPattern.Should().Be(MemoryAccessPattern.Random);
    }

    [Theory]
    [InlineData(KernelBackends.CPU)]
    [InlineData(KernelBackends.CUDA)]
    [InlineData(KernelBackends.Metal)]
    [InlineData(KernelBackends.OpenCL)]
    [InlineData(KernelBackends.All)]
    public void KernelBackends_EnumValues_ShouldHaveCorrectFlags(KernelBackends backend)
    {
        // Act & Assert
        switch (backend)
        {
            case KernelBackends.CPU:
                ((int)backend).Should().Be(1);
                break;
            case KernelBackends.CUDA:
                ((int)backend).Should().Be(2);
                break;
            case KernelBackends.Metal:
                ((int)backend).Should().Be(4);
                break;
            case KernelBackends.OpenCL:
                ((int)backend).Should().Be(8);
                break;
            case KernelBackends.All:
                backend.Should().HaveFlag(KernelBackends.CPU);
                backend.Should().HaveFlag(KernelBackends.CUDA);
                backend.Should().HaveFlag(KernelBackends.Metal);
                backend.Should().HaveFlag(KernelBackends.OpenCL);
                break;
        }
    }

    [Theory]
    [InlineData(OptimizationHints.None, 0)]
    [InlineData(OptimizationHints.AggressiveInlining, 1)]
    [InlineData(OptimizationHints.LoopUnrolling, 2)]
    [InlineData(OptimizationHints.Vectorize, 4)]
    [InlineData(OptimizationHints.Prefetch, 8)]
    [InlineData(OptimizationHints.FastMath, 16)]
    public void OptimizationHints_EnumValues_ShouldHaveCorrectFlags(OptimizationHints hint, int expectedValue)
    {
        // Act & Assert
        ((int)hint).Should().Be(expectedValue);
    }

    [Fact]
    public void OptimizationHints_All_ShouldIncludeAllFlags()
    {
        // Arrange & Act
        var allHints = OptimizationHints.All;

        // Assert
        allHints.Should().HaveFlag(OptimizationHints.AggressiveInlining);
        allHints.Should().HaveFlag(OptimizationHints.LoopUnrolling);
        allHints.Should().HaveFlag(OptimizationHints.Vectorize);
        allHints.Should().HaveFlag(OptimizationHints.Prefetch);
        allHints.Should().HaveFlag(OptimizationHints.FastMath);
    }

    [Theory]
    [InlineData(MemoryAccessPattern.Sequential)]
    [InlineData(MemoryAccessPattern.Strided)]
    [InlineData(MemoryAccessPattern.Random)]
    [InlineData(MemoryAccessPattern.Coalesced)]
    [InlineData(MemoryAccessPattern.Tiled)]
    public void MemoryAccessPattern_EnumValues_ShouldBeDefined(MemoryAccessPattern pattern)
    {
        // Act & Assert
        Enum.IsDefined(typeof(MemoryAccessPattern), pattern).Should().BeTrue();
    }

    [Fact]
    public void KernelAttribute_CanBeAppliedToMethod()
    {
        // This test verifies the attribute can be applied to methods
        // by checking if the AttributeUsage allows it
        
        // Arrange
        var attributeType = typeof(KernelAttribute);
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
                                        .Cast<AttributeUsageAttribute>()
                                        .FirstOrDefault();

        // Assert
        attributeUsage.Should().NotBeNull("KernelAttribute should have AttributeUsage");
        attributeUsage!.ValidOn.Should().HaveFlag(AttributeTargets.Method);
        attributeUsage.AllowMultiple.Should().BeFalse("KernelAttribute should not allow multiple instances");
    }

    [Fact]
    public void KernelAttribute_CombinedBackends_ShouldWorkCorrectly()
    {
        // Arrange
        var attribute = new KernelAttribute();

        // Act
        attribute.Backends = KernelBackends.CPU | KernelBackends.CUDA | KernelBackends.Metal;

        // Assert
        attribute.Backends.Should().HaveFlag(KernelBackends.CPU);
        attribute.Backends.Should().HaveFlag(KernelBackends.CUDA);
        attribute.Backends.Should().HaveFlag(KernelBackends.Metal);
        attribute.Backends.Should().NotHaveFlag(KernelBackends.OpenCL);
        
        var backendValue = (int)attribute.Backends;
        backendValue.Should().Be(1 + 2 + 4); // CPU + CUDA + Metal
    }

    [Fact]
    public void KernelAttribute_CombinedOptimizations_ShouldWorkCorrectly()
    {
        // Arrange
        var attribute = new KernelAttribute();

        // Act
        attribute.Optimizations = OptimizationHints.Vectorize | OptimizationHints.LoopUnrolling | OptimizationHints.FastMath;

        // Assert
        attribute.Optimizations.Should().HaveFlag(OptimizationHints.Vectorize);
        attribute.Optimizations.Should().HaveFlag(OptimizationHints.LoopUnrolling);
        attribute.Optimizations.Should().HaveFlag(OptimizationHints.FastMath);
        attribute.Optimizations.Should().NotHaveFlag(OptimizationHints.AggressiveInlining);
        attribute.Optimizations.Should().NotHaveFlag(OptimizationHints.Prefetch);
        
        var optimizationValue = (int)attribute.Optimizations;
        optimizationValue.Should().Be(4 + 2 + 16); // Vectorize + LoopUnrolling + FastMath
    }
}