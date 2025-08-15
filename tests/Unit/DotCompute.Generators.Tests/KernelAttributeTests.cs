// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Kernel;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit;

public class KernelAttributeTests
{
    [Fact]
    public void KernelAttribute_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var attribute = new KernelAttribute();

        // Assert
        attribute.Backends.Should().Be(KernelBackends.CPU);
        attribute.VectorSize.Should().Be(8); // Default vector size should be 8(256-bit)
        attribute.IsParallel.Should().BeTrue(); // Default should enable parallel execution
        attribute.GridDimensions.Should().BeNull; // Grid dimensions should be null by default
        attribute.BlockDimensions.Should().BeNull; // Block dimensions should be null by default
        attribute.Optimizations.Should().Be(OptimizationHints.None);
        attribute.MemoryPattern.Should().Be(MemoryAccessPattern.Sequential);
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
        attribute.Backends.HaveFlag(KernelBackends.CUDA);
        attribute.Backends.HaveFlag(KernelBackends.Metal);
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
        attribute.Optimizations.HaveFlag(OptimizationHints.Vectorize);
        attribute.Optimizations.HaveFlag(OptimizationHints.FastMath);
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
        switch(backend)
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
                backend.HaveFlag(KernelBackends.CPU);
                backend.HaveFlag(KernelBackends.CUDA);
                backend.HaveFlag(KernelBackends.Metal);
                backend.HaveFlag(KernelBackends.OpenCL);
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
        allHints.HaveFlag(OptimizationHints.AggressiveInlining);
        allHints.HaveFlag(OptimizationHints.LoopUnrolling);
        allHints.HaveFlag(OptimizationHints.Vectorize);
        allHints.HaveFlag(OptimizationHints.Prefetch);
        allHints.HaveFlag(OptimizationHints.FastMath);
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
        attributeUsage.NotBeNull("KernelAttribute should have AttributeUsage");
        attributeUsage!.ValidOn.HaveFlag(AttributeTargets.Method);
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
        attribute.Backends.HaveFlag(KernelBackends.CPU);
        attribute.Backends.HaveFlag(KernelBackends.CUDA);
        attribute.Backends.HaveFlag(KernelBackends.Metal);
        attribute.Backends.Should().NotHaveFlag(KernelBackends.OpenCL);
        
        var backendValue =(int)attribute.Backends;
        Assert.Equal(1 + 2 + 4, backendValue); // CPU + CUDA + Metal
    }

    [Fact]
    public void KernelAttribute_CombinedOptimizations_ShouldWorkCorrectly()
    {
        // Arrange
        var attribute = new KernelAttribute();

        // Act
        attribute.Optimizations = OptimizationHints.Vectorize | OptimizationHints.LoopUnrolling | OptimizationHints.FastMath;

        // Assert
        attribute.Optimizations.HaveFlag(OptimizationHints.Vectorize);
        attribute.Optimizations.HaveFlag(OptimizationHints.LoopUnrolling);
        attribute.Optimizations.HaveFlag(OptimizationHints.FastMath);
        attribute.Optimizations.Should().NotHaveFlag(OptimizationHints.AggressiveInlining);
        attribute.Optimizations.Should().NotHaveFlag(OptimizationHints.Prefetch);
        
        var optimizationValue =(int)attribute.Optimizations;
        Assert.Equal(4 + 2 + 16, optimizationValue); // Vectorize + LoopUnrolling + FastMath
    }
}
