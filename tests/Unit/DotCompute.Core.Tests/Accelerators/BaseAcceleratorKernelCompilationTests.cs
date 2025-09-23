// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Tests.TestImplementations;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Core.Tests.Accelerators;

/// <summary>
/// Tests for BaseAccelerator kernel compilation functionality including
/// compilation, caching, validation, and error scenarios.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "BaseAccelerator")]
[Trait("TestType", "KernelCompilation")]
public sealed class BaseAcceleratorKernelCompilationTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IUnifiedMemoryManager> _mockMemory;
    private readonly TestAccelerator _accelerator;
    private bool _disposed;

    public BaseAcceleratorKernelCompilationTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockMemory = new Mock<IUnifiedMemoryManager>();

        var info = new AcceleratorInfo(
            AcceleratorType.CPU,
            "Test Accelerator",
            "1.0",
            1024 * 1024 * 1024,
            4,
            3000,
            new Version(1, 0),
            1024 * 1024,
            true
        );

        _accelerator = new TestAccelerator(info, _mockMemory.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CompileKernelAsync_ValidKernel_CompilesSuccessfully()
    {
        // Arrange
        var kernelDef = new KernelDefinition("testKernel", "kernel code", "testFunction");
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.O2 };

        // Act
        var result = await _accelerator.CompileKernelAsync(kernelDef, options);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("testKernel");
        result.IsReady.Should().BeTrue();
        _accelerator.CompileKernelAsyncCalled.Should().BeTrue();
    }

    [Fact]
    public async Task CompileKernelAsync_NullKernel_ThrowsArgumentNullException()
    {
        // Arrange
        KernelDefinition? kernelDef = null;
        var options = new CompilationOptions();

        // Act
        var act = async () => await _accelerator.CompileKernelAsync(kernelDef!, options);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("kernelDefinition");
    }

    [Fact]
    public async Task CompileKernelAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var kernelDef = new KernelDefinition("testKernel", "kernel code", "testFunction");
        CompilationOptions? options = null;

        // Act
        var act = async () => await _accelerator.CompileKernelAsync(kernelDef, options!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public async Task CompileKernelAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var kernelDef = new KernelDefinition("testKernel", "kernel code", "testFunction");
        var options = new CompilationOptions();
        _accelerator.Dispose();

        // Act
        var act = async () => await _accelerator.CompileKernelAsync(kernelDef, options);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Theory]
    [InlineData(OptimizationLevel.O0, false)]
    [InlineData(OptimizationLevel.O1, false)]
    [InlineData(OptimizationLevel.O2, true)]
    [InlineData(OptimizationLevel.O3, true)]
    public async Task CompileKernelAsync_WithDifferentOptimizationLevels_AppliesCorrectly(
        OptimizationLevel level, bool generateDebugInfo)
    {
        // Arrange
        var kernelDef = new KernelDefinition("testKernel", "kernel code", "testFunction");
        var options = new CompilationOptions
        {
            OptimizationLevel = level,
            GenerateDebugInfo = generateDebugInfo
        };

        // Act
        var result = await _accelerator.CompileKernelAsync(kernelDef, options);

        // Assert
        result.Should().NotBeNull();
        _accelerator.LastCompilationOptions.Should().NotBeNull();
        _accelerator.LastCompilationOptions!.OptimizationLevel.Should().Be(level);
        _accelerator.LastCompilationOptions.GenerateDebugInfo.Should().Be(generateDebugInfo);
    }

    [Fact]
    public async Task CompileKernelAsync_InvalidKernelCode_ThrowsCompilationException()
    {
        // Arrange
        var kernelDef = new KernelDefinition("invalidKernel", "INVALID_CODE", "testFunction");
        var options = new CompilationOptions();
        _accelerator.SimulateCompilationError = true;

        // Act
        var act = async () => await _accelerator.CompileKernelAsync(kernelDef, options);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Simulated compilation error*");
    }

    [Fact]
    public async Task CompileKernelAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var kernelDef = new KernelDefinition("testKernel", "kernel code", "testFunction");
        var options = new CompilationOptions();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _accelerator.CompileKernelAsync(kernelDef, options, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CompileKernelAsync_MultipleConcurrentCompilations_HandledCorrectly()
    {
        // Arrange
        var kernelDef1 = new KernelDefinition("kernel1", "code1", "func1");
        var kernelDef2 = new KernelDefinition("kernel2", "code2", "func2");
        var kernelDef3 = new KernelDefinition("kernel3", "code3", "func3");
        var options = new CompilationOptions();

        // Act
        var task1 = _accelerator.CompileKernelAsync(kernelDef1, options);
        var task2 = _accelerator.CompileKernelAsync(kernelDef2, options);
        var task3 = _accelerator.CompileKernelAsync(kernelDef3, options);

        var results = await Task.WhenAll(task1, task2, task3);

        // Assert
        results.Should().HaveCount(3);
        results.Should().OnlyContain(r => r.IsReady);
        results.Select(r => r.Name).Should().BeEquivalentTo(["kernel1", "kernel2", "kernel3"]);
        _accelerator.CompilationCount.Should().Be(3);
    }

    [Fact]
    public async Task CompileKernelAsync_TrackCompilationMetrics_LoggedCorrectly()
    {
        // Arrange
        var kernelDef = new KernelDefinition("metricsKernel", "kernel code", "testFunction");
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.O3 };

        // Act
        var result = await _accelerator.CompileKernelAsync(kernelDef, options);

        // Assert
        result.Should().NotBeNull();
        _accelerator.LastLoggedKernelName.Should().Be("metricsKernel");
        _accelerator.LastLoggedCompilationTime.Should().BePositive();
        _accelerator.LastLoggedByteCodeSize.Should().BePositive();
    }

    [Fact]
    public async Task CompileKernelAsync_CacheHit_ReturnsExistingKernel()
    {
        // Arrange
        var kernelDef = new KernelDefinition("cachedKernel", "kernel code", "testFunction");
        var options = new CompilationOptions();

        // Act - Compile twice with same definition
        var result1 = await _accelerator.CompileKernelAsync(kernelDef, options);
        var result2 = await _accelerator.CompileKernelAsync(kernelDef, options);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Name.Should().Be(result2.Name);
        _accelerator.CompilationCount.Should().Be(1); // Should only compile once due to caching
        _accelerator.CacheHits.Should().Be(1);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _accelerator?.Dispose();
        _disposed = true;
    }
}