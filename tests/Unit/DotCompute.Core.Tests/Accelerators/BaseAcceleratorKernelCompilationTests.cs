// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Tests.TestImplementations;
using Microsoft.Extensions.Logging;
using Moq;

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
    /// <summary>
    /// Initializes a new instance of the BaseAcceleratorKernelCompilationTests class.
    /// </summary>

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
    /// <summary>
    /// Gets compile kernel async_ valid kernel_ compiles successfully.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    public async Task CompileKernelAsync_ValidKernel_CompilesSuccessfully()
    {
        // Arrange
        var kernelDef = new KernelDefinition("testKernel", "kernel code", "testFunction");
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.O2 };

        // Act
        var result = await _accelerator.CompileKernelAsync(kernelDef, options);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Name.Should().Be("testKernel");
        _ = result.Id.Should().NotBeEmpty();
        _ = _accelerator.CompileKernelAsyncCalled.Should().BeTrue();
    }
    /// <summary>
    /// Gets compile kernel async_ null kernel_ throws argument null exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    public async Task CompileKernelAsync_NullKernel_ThrowsArgumentNullException()
    {
        // Arrange
        KernelDefinition? kernelDef = null;
        var options = new CompilationOptions();

        // Act
        var act = async () => await _accelerator.CompileKernelAsync(kernelDef!, options);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("kernelDefinition");
    }
    /// <summary>
    /// Gets compile kernel async_ null options_ throws argument null exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    public async Task CompileKernelAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var kernelDef = new KernelDefinition("testKernel", "kernel code", "testFunction");
        CompilationOptions? options = null;

        // Act
        var act = async () => await _accelerator.CompileKernelAsync(kernelDef, options!);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("options");
    }
    /// <summary>
    /// Gets compile kernel async_ when disposed_ throws object disposed exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    public async Task CompileKernelAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var kernelDef = new KernelDefinition("testKernel", "kernel code", "testFunction");
        var options = new CompilationOptions();
        await _accelerator.DisposeAsync();

        // Act
        var act = async () => await _accelerator.CompileKernelAsync(kernelDef, options);

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }
    /// <summary>
    /// Gets compile kernel async_ with different optimization levels_ applies correctly.
    /// </summary>
    /// <param name="level">The level.</param>
    /// <param name="generateDebugInfo">The generate debug info.</param>
    /// <returns>The result of the operation.</returns>

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
        _ = result.Should().NotBeNull();
        _ = _accelerator.LastCompilationOptions.Should().NotBeNull();
        _ = _accelerator.LastCompilationOptions!.OptimizationLevel.Should().Be(level);
        _ = _accelerator.LastCompilationOptions.GenerateDebugInfo.Should().Be(generateDebugInfo);
    }
    /// <summary>
    /// Gets compile kernel async_ invalid kernel code_ throws compilation exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Simulated compilation error*");
    }
    /// <summary>
    /// Gets compile kernel async_ with cancellation_ throws operation canceled exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    public async Task CompileKernelAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var kernelDef = new KernelDefinition("testKernel", "kernel code", "testFunction");
        var options = new CompilationOptions();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = async () => await _accelerator.CompileKernelAsync(kernelDef, options, cts.Token);

        // Assert
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }
    /// <summary>
    /// Gets compile kernel async_ multiple concurrent compilations_ handled correctly.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    public async Task CompileKernelAsync_MultipleConcurrentCompilations_HandledCorrectly()
    {
        // Arrange
        var kernelDef1 = new KernelDefinition("kernel1", "code1", "func1");
        var kernelDef2 = new KernelDefinition("kernel2", "code2", "func2");
        var kernelDef3 = new KernelDefinition("kernel3", "code3", "func3");
        var options = new CompilationOptions();

        // Act
        var task1 = _accelerator.CompileKernelAsync(kernelDef1, options).AsTask();
        var task2 = _accelerator.CompileKernelAsync(kernelDef2, options).AsTask();
        var task3 = _accelerator.CompileKernelAsync(kernelDef3, options).AsTask();

        var results = await Task.WhenAll(task1, task2, task3);

        // Assert
        _ = results.Should().HaveCount(3);
        _ = results.Should().OnlyContain(r => !string.IsNullOrEmpty(r.Name));
        _ = results.Select(r => r.Name).Should().BeEquivalentTo(["kernel1", "kernel2", "kernel3"]);
        _ = _accelerator.CompilationCount.Should().Be(3);
    }
    /// <summary>
    /// Gets compile kernel async_ track compilation metrics_ logged correctly.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    public async Task CompileKernelAsync_TrackCompilationMetrics_LoggedCorrectly()
    {
        // Arrange
        var kernelDef = new KernelDefinition("metricsKernel", "kernel code", "testFunction");
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.O3 };

        // Act
        var result = await _accelerator.CompileKernelAsync(kernelDef, options);

        // Assert
        _ = result.Should().NotBeNull();
        _ = _accelerator.LastLoggedKernelName.Should().Be("metricsKernel");
        _ = _accelerator.LastLoggedCompilationTime.Should().BePositive();
        _ = _accelerator.LastLoggedByteCodeSize.Should().BePositive();
    }
    /// <summary>
    /// Gets compile kernel async_ cache hit_ returns existing kernel.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
        _ = result1.Should().NotBeNull();
        _ = result2.Should().NotBeNull();
        _ = result1.Name.Should().Be(result2.Name);
        _ = _accelerator.CompilationCount.Should().Be(1); // Should only compile once due to caching
        _ = _accelerator.CacheHits.Should().Be(1);
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (_disposed)
            return;

        _accelerator?.Dispose();
        _disposed = true;
    }
}