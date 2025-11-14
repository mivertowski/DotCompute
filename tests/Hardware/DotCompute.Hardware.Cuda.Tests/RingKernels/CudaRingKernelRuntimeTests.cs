// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.RingKernels;
using DotCompute.Core.Messaging;
using DotCompute.Tests.Common.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DotCompute.Hardware.Cuda.Tests.RingKernels;

/// <summary>
/// Comprehensive tests for CudaRingKernelRuntime lifecycle management.
/// Tests kernel launch, activation, deactivation, termination, and metrics collection.
/// </summary>
[Collection("CUDA Hardware")]
public class CudaRingKernelRuntimeTests : IAsyncDisposable
{
    private readonly ILogger<CudaRingKernelRuntime> _runtimeLogger;
    private readonly ILogger<CudaRingKernelCompiler> _compilerLogger;
    private readonly CudaRingKernelCompiler _compiler;
    private readonly CudaRingKernelRuntime _runtime;

    public CudaRingKernelRuntimeTests()
    {
        _runtimeLogger = Substitute.For<ILogger<CudaRingKernelRuntime>>();
        _compilerLogger = Substitute.For<ILogger<CudaRingKernelCompiler>>();
        _compiler = new CudaRingKernelCompiler(_compilerLogger);
        var registryLogger = NullLogger<MessageQueueRegistry>.Instance;
        var registry = new MessageQueueRegistry(registryLogger);
        _runtime = new CudaRingKernelRuntime(_runtimeLogger, _compiler, registry);
    }

    #region Launch Tests

    [SkippableFact(DisplayName = "LaunchAsync should create kernel with valid parameters")]
    public async Task LaunchAsync_ShouldCreateKernel()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        const string kernelId = "test_launch_kernel";
        const int gridSize = 1;
        const int blockSize = 256;

        try
        {
            // Act
            await _runtime.LaunchAsync(kernelId, gridSize, blockSize);

            // Assert
            var kernels = await _runtime.ListKernelsAsync();
            kernels.Should().Contain(kernelId);

            var status = await _runtime.GetStatusAsync(kernelId);
            status.Should().NotBeNull();
            status.IsLaunched.Should().BeTrue();
            status.IsActive.Should().BeFalse("kernel should start inactive");
            status.GridSize.Should().Be(gridSize);
            status.BlockSize.Should().Be(blockSize);
        }
        finally
        {
            await CleanupKernelAsync(kernelId);
        }
    }

    [SkippableFact(DisplayName = "LaunchAsync should reject null or empty kernel ID")]
    public async Task LaunchAsync_ShouldRejectInvalidKernelId()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Act & Assert - null throws ArgumentNullException
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _runtime.LaunchAsync(null!, 1, 256));

        // Empty and whitespace throw ArgumentException
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _runtime.LaunchAsync("", 1, 256));

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _runtime.LaunchAsync("   ", 1, 256));
    }

    [SkippableFact(DisplayName = "LaunchAsync should reject invalid grid/block sizes")]
    public async Task LaunchAsync_ShouldRejectInvalidSizes()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _runtime.LaunchAsync("kernel", 0, 256));

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _runtime.LaunchAsync("kernel", -1, 256));

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _runtime.LaunchAsync("kernel", 1, 0));

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _runtime.LaunchAsync("kernel", 1, -1));
    }

    [SkippableFact(DisplayName = "LaunchAsync should support multiple concurrent kernels")]
    public async Task LaunchAsync_ShouldSupportMultipleKernels()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        var kernelIds = new[] { "kernel_1", "kernel_2", "kernel_3" };

        try
        {
            // Act
            foreach (var kernelId in kernelIds)
            {
                await _runtime.LaunchAsync(kernelId, 1, 128);
            }

            // Assert
            var allKernels = await _runtime.ListKernelsAsync();
            allKernels.Should().Contain(kernelIds);

            foreach (var kernelId in kernelIds)
            {
                var status = await _runtime.GetStatusAsync(kernelId);
                status.IsLaunched.Should().BeTrue();
            }
        }
        finally
        {
            foreach (var kernelId in kernelIds)
            {
                await CleanupKernelAsync(kernelId);
            }
        }
    }

    #endregion

    #region Activation Tests

    [SkippableFact(DisplayName = "ActivateAsync should activate launched kernel")]
    public async Task ActivateAsync_ShouldActivateKernel()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        const string kernelId = "test_activate_kernel";
        await _runtime.LaunchAsync(kernelId, 1, 256);

        try
        {
            // Act
            await _runtime.ActivateAsync(kernelId);

            // Assert
            var status = await _runtime.GetStatusAsync(kernelId);
            status.IsActive.Should().BeTrue();
        }
        finally
        {
            await CleanupKernelAsync(kernelId);
        }
    }

    [SkippableFact(DisplayName = "ActivateAsync should reject not-launched kernel")]
    public async Task ActivateAsync_ShouldRejectNotLaunchedKernel()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _runtime.ActivateAsync("nonexistent_kernel"));
    }

    [SkippableFact(DisplayName = "ActivateAsync should reject already-active kernel")]
    public async Task ActivateAsync_ShouldRejectAlreadyActiveKernel()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        const string kernelId = "test_reactivate_kernel";
        await _runtime.LaunchAsync(kernelId, 1, 256);
        await _runtime.ActivateAsync(kernelId);

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _runtime.ActivateAsync(kernelId));
        }
        finally
        {
            await CleanupKernelAsync(kernelId);
        }
    }

    [SkippableFact(DisplayName = "DeactivateAsync should deactivate active kernel")]
    public async Task DeactivateAsync_ShouldDeactivateKernel()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        const string kernelId = "test_deactivate_kernel";
        await _runtime.LaunchAsync(kernelId, 1, 256);
        await _runtime.ActivateAsync(kernelId);

        try
        {
            // Act
            await _runtime.DeactivateAsync(kernelId);

            // Assert
            var status = await _runtime.GetStatusAsync(kernelId);
            status.IsActive.Should().BeFalse();
            status.IsLaunched.Should().BeTrue("kernel should still be launched");
        }
        finally
        {
            await CleanupKernelAsync(kernelId);
        }
    }

    [SkippableFact(DisplayName = "DeactivateAsync should reject not-active kernel")]
    public async Task DeactivateAsync_ShouldRejectNotActiveKernel()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        const string kernelId = "test_deactivate_inactive_kernel";
        await _runtime.LaunchAsync(kernelId, 1, 256);

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _runtime.DeactivateAsync(kernelId));
        }
        finally
        {
            await CleanupKernelAsync(kernelId);
        }
    }

    #endregion

    #region Termination Tests

    [SkippableFact(DisplayName = "TerminateAsync should cleanup all resources")]
    public async Task TerminateAsync_ShouldCleanupResources()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        const string kernelId = "test_terminate_kernel";
        await _runtime.LaunchAsync(kernelId, 1, 256);

        // Act
        await _runtime.TerminateAsync(kernelId);

        // Assert
        var kernels = await _runtime.ListKernelsAsync();
        kernels.Should().NotContain(kernelId, "terminated kernel should be removed");
    }

    [SkippableFact(DisplayName = "TerminateAsync should work on active kernel")]
    public async Task TerminateAsync_ShouldTerminateActiveKernel()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        const string kernelId = "test_terminate_active_kernel";
        await _runtime.LaunchAsync(kernelId, 1, 256);
        await _runtime.ActivateAsync(kernelId);

        // Act
        await _runtime.TerminateAsync(kernelId);

        // Assert
        var kernels = await _runtime.ListKernelsAsync();
        kernels.Should().NotContain(kernelId);
    }

    [SkippableFact(DisplayName = "TerminateAsync should reject nonexistent kernel")]
    public async Task TerminateAsync_ShouldRejectNonexistentKernel()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _runtime.TerminateAsync("nonexistent_kernel"));
    }

    #endregion

    #region Status and Metrics Tests

    [SkippableFact(DisplayName = "GetStatusAsync should return accurate kernel status")]
    public async Task GetStatusAsync_ShouldReturnAccurateStatus()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        const string kernelId = "test_status_kernel";
        const int gridSize = 2;
        const int blockSize = 512;

        await _runtime.LaunchAsync(kernelId, gridSize, blockSize);

        try
        {
            // Act
            var status = await _runtime.GetStatusAsync(kernelId);

            // Assert
            status.Should().NotBeNull();
            status.KernelId.Should().Be(kernelId);
            status.IsLaunched.Should().BeTrue();
            status.IsActive.Should().BeFalse();
            status.IsTerminating.Should().BeFalse();
            status.GridSize.Should().Be(gridSize);
            status.BlockSize.Should().Be(blockSize);
            status.Uptime.Should().BeGreaterThan(TimeSpan.Zero);
        }
        finally
        {
            await CleanupKernelAsync(kernelId);
        }
    }

    [SkippableFact(DisplayName = "GetStatusAsync should reflect activation state")]
    public async Task GetStatusAsync_ShouldReflectActivationState()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        const string kernelId = "test_status_activation_kernel";
        await _runtime.LaunchAsync(kernelId, 1, 256);

        try
        {
            // Before activation
            var status1 = await _runtime.GetStatusAsync(kernelId);
            status1.IsActive.Should().BeFalse();

            // After activation
            await _runtime.ActivateAsync(kernelId);
            var status2 = await _runtime.GetStatusAsync(kernelId);
            status2.IsActive.Should().BeTrue();

            // After deactivation
            await _runtime.DeactivateAsync(kernelId);
            var status3 = await _runtime.GetStatusAsync(kernelId);
            status3.IsActive.Should().BeFalse();
        }
        finally
        {
            await CleanupKernelAsync(kernelId);
        }
    }

    [SkippableFact(DisplayName = "GetMetricsAsync should return valid metrics")]
    public async Task GetMetricsAsync_ShouldReturnValidMetrics()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        const string kernelId = "test_metrics_kernel";
        await _runtime.LaunchAsync(kernelId, 1, 256);

        try
        {
            // Act
            var metrics = await _runtime.GetMetricsAsync(kernelId);

            // Assert
            metrics.Should().NotBeNull();
            metrics.LaunchCount.Should().BeGreaterThanOrEqualTo(1);
            metrics.ThroughputMsgsPerSec.Should().BeGreaterThanOrEqualTo(0);
            metrics.InputQueueUtilization.Should().BeInRange(0, 100);
            metrics.OutputQueueUtilization.Should().BeInRange(0, 100);
        }
        finally
        {
            await CleanupKernelAsync(kernelId);
        }
    }

    [SkippableFact(DisplayName = "GetMetricsAsync should reject nonexistent kernel")]
    public async Task GetMetricsAsync_ShouldRejectNonexistentKernel()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _runtime.GetMetricsAsync("nonexistent_kernel"));
    }

    #endregion

    #region Message Queue Tests

    [SkippableFact(DisplayName = "CreateMessageQueueAsync should create queue with power-of-2 capacity")]
    public async Task CreateMessageQueueAsync_ShouldCreateQueue()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Act
        var queue = await _runtime.CreateMessageQueueAsync<int>(256);

        // Assert
        queue.Should().NotBeNull();
        queue.Should().BeAssignableTo<IMessageQueue<int>>();
    }

    [SkippableFact(DisplayName = "CreateMessageQueueAsync should reject non-power-of-2 capacity")]
    public async Task CreateMessageQueueAsync_ShouldRejectInvalidCapacity()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _runtime.CreateMessageQueueAsync<int>(100));

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _runtime.CreateMessageQueueAsync<int>(0));

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _runtime.CreateMessageQueueAsync<int>(-1));
    }

    #endregion

    #region List Kernels Tests

    [SkippableFact(DisplayName = "ListKernelsAsync should return empty list initially")]
    public async Task ListKernelsAsync_ShouldReturnEmptyListInitially()
    {
        // Act
        var kernels = await _runtime.ListKernelsAsync();

        // Assert
        kernels.Should().NotBeNull();
        kernels.Should().BeEmpty();
    }

    [SkippableFact(DisplayName = "ListKernelsAsync should include all launched kernels")]
    public async Task ListKernelsAsync_ShouldIncludeAllLaunchedKernels()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        var kernelIds = new[] { "list_kernel_1", "list_kernel_2", "list_kernel_3" };

        try
        {
            foreach (var kernelId in kernelIds)
            {
                await _runtime.LaunchAsync(kernelId, 1, 256);
            }

            // Act
            var kernels = await _runtime.ListKernelsAsync();

            // Assert
            kernels.Should().Contain(kernelIds);
            kernels.Count.Should().BeGreaterThanOrEqualTo(kernelIds.Length);
        }
        finally
        {
            foreach (var kernelId in kernelIds)
            {
                await CleanupKernelAsync(kernelId);
            }
        }
    }

    [SkippableFact(DisplayName = "ListKernelsAsync should not include terminated kernels")]
    public async Task ListKernelsAsync_ShouldNotIncludeTerminatedKernels()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        const string kernelId = "list_terminated_kernel";
        await _runtime.LaunchAsync(kernelId, 1, 256);
        await _runtime.TerminateAsync(kernelId);

        // Act
        var kernels = await _runtime.ListKernelsAsync();

        // Assert
        kernels.Should().NotContain(kernelId);
    }

    #endregion

    #region Disposal Tests

    [SkippableFact(DisplayName = "DisposeAsync should terminate all active kernels")]
    public async Task DisposeAsync_ShouldTerminateAllKernels()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        var registryLogger = NullLogger<MessageQueueRegistry>.Instance;
        var registry = new MessageQueueRegistry(registryLogger);
        var runtime = new CudaRingKernelRuntime(_runtimeLogger, _compiler, registry);
        await runtime.LaunchAsync("dispose_kernel_1", 1, 256);
        await runtime.LaunchAsync("dispose_kernel_2", 1, 256);

        // Act
        await runtime.DisposeAsync();

        // Assert - Should not throw on re-dispose
        await runtime.DisposeAsync();
    }

    #endregion

    #region Helper Methods

    private async Task CleanupKernelAsync(string kernelId)
    {
        try
        {
            var kernels = await _runtime.ListKernelsAsync();
            if (kernels.Contains(kernelId))
            {
                await _runtime.TerminateAsync(kernelId);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _runtime.DisposeAsync();
        _compiler?.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}
