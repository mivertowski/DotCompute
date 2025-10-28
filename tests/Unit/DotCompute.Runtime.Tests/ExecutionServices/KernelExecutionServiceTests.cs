// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// COMMENTED OUT: Multiple API mismatches in KernelExecutionService and IAccelerator
// TODO: Uncomment when the following APIs are implemented:
// - IAccelerator.Name property
// - IAccelerator.ExecuteAsync(ICompiledKernel) method
// - KernelExecutionService constructor needs IUnifiedKernelCompiler instead of IKernelCompiler
/*
using DotCompute.Abstractions;
using DotCompute.Runtime.Services;
using DotCompute.Runtime.Services.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.ExecutionServices;

/// <summary>
/// Tests for KernelExecutionService
/// </summary>
public sealed class KernelExecutionServiceTests : IDisposable
{
    private readonly IKernelCompiler _mockCompiler;
    private readonly IAccelerator _mockAccelerator;
    private KernelExecutionService? _service;

    public KernelExecutionServiceTests()
    {
        _mockCompiler = Substitute.For<IKernelCompiler>();
        _mockAccelerator = Substitute.For<IAccelerator>();
        _mockAccelerator.Name.Returns("TestAccelerator");
    }

    public void Dispose()
    {
        _service?.Dispose();
    }

    [Fact]
    public void Constructor_WithNullCompiler_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new KernelExecutionService(null!, _mockAccelerator);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("compiler");
    }

    [Fact]
    public void Constructor_WithNullAccelerator_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new KernelExecutionService(_mockCompiler, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("accelerator");
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        _service = new KernelExecutionService(_mockCompiler, _mockAccelerator);

        // Assert
        _service.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteKernelAsync_WithValidKernel_Succeeds()
    {
        // Arrange
        _service = new KernelExecutionService(_mockCompiler, _mockAccelerator);
        var kernel = Substitute.For<IKernel>();
        var compiledKernel = Substitute.For<ICompiledKernel>();
        _mockCompiler.CompileAsync(kernel).Returns(compiledKernel);

        // Act
        await _service.ExecuteKernelAsync(kernel);

        // Assert
        await _mockAccelerator.Received(1).ExecuteAsync(compiledKernel);
    }

    [Fact]
    public async Task ExecuteKernelAsync_WithNullKernel_ThrowsArgumentNullException()
    {
        // Arrange
        _service = new KernelExecutionService(_mockCompiler, _mockAccelerator);

        // Act
        var action = async () => await _service.ExecuteKernelAsync(null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteKernelAsync_WithCompilationFailure_ThrowsException()
    {
        // Arrange
        _service = new KernelExecutionService(_mockCompiler, _mockAccelerator);
        var kernel = Substitute.For<IKernel>();
        _mockCompiler.CompileAsync(kernel).Throws(new InvalidOperationException("Compilation failed"));

        // Act
        var action = async () => await _service.ExecuteKernelAsync(kernel);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteKernelAsync_WithCancellation_ThrowsTaskCanceledException()
    {
        // Arrange
        _service = new KernelExecutionService(_mockCompiler, _mockAccelerator);
        var kernel = Substitute.For<IKernel>();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var action = async () => await _service.ExecuteKernelAsync(kernel, cts.Token);

        // Assert
        await action.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task CompileAndCacheKernelAsync_CachesCompiledKernel()
    {
        // Arrange
        _service = new KernelExecutionService(_mockCompiler, _mockAccelerator);
        var kernel = Substitute.For<IKernel>();
        var compiledKernel = Substitute.For<ICompiledKernel>();
        _mockCompiler.CompileAsync(kernel).Returns(compiledKernel);

        // Act
        await _service.CompileAndCacheKernelAsync(kernel);
        await _service.ExecuteKernelAsync(kernel);

        // Assert
        await _mockCompiler.Received(1).CompileAsync(kernel);
    }

    [Fact]
    public async Task GetKernelStatistics_ReturnsStatistics()
    {
        // Arrange
        _service = new KernelExecutionService(_mockCompiler, _mockAccelerator);
        var kernel = Substitute.For<IKernel>();
        var compiledKernel = Substitute.For<ICompiledKernel>();
        _mockCompiler.CompileAsync(kernel).Returns(compiledKernel);
        await _service.ExecuteKernelAsync(kernel);

        // Act
        var stats = _service.GetKernelStatistics(kernel);

        // Assert
        stats.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteKernelBatchAsync_ExecutesMultipleKernels()
    {
        // Arrange
        _service = new KernelExecutionService(_mockCompiler, _mockAccelerator);
        var kernels = new[]
        {
            Substitute.For<IKernel>(),
            Substitute.For<IKernel>(),
            Substitute.For<IKernel>()
        };
        var compiledKernel = Substitute.For<ICompiledKernel>();
        _mockCompiler.CompileAsync(Arg.Any<IKernel>()).Returns(compiledKernel);

        // Act
        await _service.ExecuteKernelBatchAsync(kernels);

        // Assert
        await _mockAccelerator.Received(3).ExecuteAsync(Arg.Any<ICompiledKernel>());
    }

    [Fact]
    public async Task WarmUpKernelAsync_PreparesKernelExecution()
    {
        // Arrange
        _service = new KernelExecutionService(_mockCompiler, _mockAccelerator);
        var kernel = Substitute.For<IKernel>();
        var compiledKernel = Substitute.For<ICompiledKernel>();
        _mockCompiler.CompileAsync(kernel).Returns(compiledKernel);

        // Act
        await _service.WarmUpKernelAsync(kernel);

        // Assert
        await _mockCompiler.Received(1).CompileAsync(kernel);
    }

    [Fact]
    public void ClearKernelCache_ClearsCache()
    {
        // Arrange
        _service = new KernelExecutionService(_mockCompiler, _mockAccelerator);

        // Act
        _service.ClearKernelCache();

        // Assert - no exception thrown
    }

    [Fact]
    public void GetCacheStatistics_ReturnsStatistics()
    {
        // Arrange
        _service = new KernelExecutionService(_mockCompiler, _mockAccelerator);

        // Act
        var stats = _service.GetCacheStatistics();

        // Assert
        stats.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_ReleasesResources()
    {
        // Arrange
        _service = new KernelExecutionService(_mockCompiler, _mockAccelerator);

        // Act
        _service.Dispose();

        // Assert - should not throw
        _service.Dispose();
    }

    [Fact]
    public async Task ExecuteKernelAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _service = new KernelExecutionService(_mockCompiler, _mockAccelerator);
        var kernel = Substitute.For<IKernel>();
        _service.Dispose();

        // Act
        var action = async () => await _service.ExecuteKernelAsync(kernel);

        // Assert
        await action.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task ExecuteKernelAsync_WithTimeout_CompletesWithinTimeout()
    {
        // Arrange
        _service = new KernelExecutionService(_mockCompiler, _mockAccelerator);
        var kernel = Substitute.For<IKernel>();
        var compiledKernel = Substitute.For<ICompiledKernel>();
        _mockCompiler.CompileAsync(kernel).Returns(compiledKernel);
        var timeout = TimeSpan.FromSeconds(5);

        // Act
        var task = _service.ExecuteKernelAsync(kernel);
        var completed = await Task.WhenAny(task, Task.Delay(timeout)) == task;

        // Assert
        completed.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteKernelAsync_WithMetrics_RecordsMetrics()
    {
        // Arrange
        _service = new KernelExecutionService(_mockCompiler, _mockAccelerator);
        var kernel = Substitute.For<IKernel>();
        var compiledKernel = Substitute.For<ICompiledKernel>();
        _mockCompiler.CompileAsync(kernel).Returns(compiledKernel);

        // Act
        await _service.ExecuteKernelAsync(kernel);

        // Assert
        var stats = _service.GetKernelStatistics(kernel);
        stats.ExecutionCount.Should().BeGreaterThan(0);
    }
}
*/
