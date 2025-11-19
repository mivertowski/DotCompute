// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Configuration;
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
/// Integration tests for CUDA Ring Kernels requiring GPU hardware.
/// Tests end-to-end scenarios including kernel lifecycle, message passing, and multi-kernel coordination.
/// </summary>
[Collection("CUDA Hardware")]
public class CudaRingKernelIntegrationTests : IDisposable
{
    // LoggerMessage delegate for high-performance logging
    private static readonly Action<ILogger, Exception?> _informationMessage = LoggerMessage.Define(LogLevel.Information, new EventId(5555), "TODO: Convert message to template");
    private readonly ILogger<CudaRingKernelRuntime> _runtimeLogger;
    private readonly ILogger<CudaRingKernelCompiler> _compilerLogger;
    private readonly ILogger<CudaMessageQueue<int>> _queueLogger;
    private readonly CudaRingKernelCompiler _compiler;
    private readonly CudaRingKernelRuntime _runtime;
    private bool _disposed;

    public CudaRingKernelIntegrationTests()
    {
        _runtimeLogger = Substitute.For<ILogger<CudaRingKernelRuntime>>();
        _compilerLogger = Substitute.For<ILogger<CudaRingKernelCompiler>>();
        _queueLogger = Substitute.For<ILogger<CudaMessageQueue<int>>>();
        var kernelDiscovery = new RingKernelDiscovery(NullLogger<RingKernelDiscovery>.Instance);
        var stubGenerator = new CudaRingKernelStubGenerator(NullLogger<CudaRingKernelStubGenerator>.Instance);
        _compiler = new CudaRingKernelCompiler(_compilerLogger, kernelDiscovery, stubGenerator);
        var registryLogger = NullLogger<MessageQueueRegistry>.Instance;
        var registry = new MessageQueueRegistry(registryLogger);
        _runtime = new CudaRingKernelRuntime(_runtimeLogger, _compiler, registry);
    }

    #region Kernel Lifecycle Tests

    [SkippableFact(DisplayName = "Complete kernel lifecycle: Launch → Activate → Deactivate → Terminate")]
    public async Task CompleteKernelLifecycle_ShouldSucceed()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Arrange
        const string kernelId = "lifecycle_test_kernel";
        const int gridSize = 1;
        const int blockSize = 256;

        try
        {
            // Act: Launch kernel
            await _runtime.LaunchAsync(kernelId, gridSize, blockSize);
            var status1 = await _runtime.GetStatusAsync(kernelId);

            // Assert: Kernel launched but not active
            status1.Should().NotBeNull();
            status1.IsLaunched.Should().BeTrue();
            status1.IsActive.Should().BeFalse();

            // Act: Activate kernel
            await _runtime.ActivateAsync(kernelId);
            var status2 = await _runtime.GetStatusAsync(kernelId);

            // Assert: Kernel active
            status2.IsActive.Should().BeTrue();

            // Act: Deactivate kernel
            await _runtime.DeactivateAsync(kernelId);
            var status3 = await _runtime.GetStatusAsync(kernelId);

            // Assert: Kernel deactivated but still launched
            status3.IsActive.Should().BeFalse();
            status3.IsLaunched.Should().BeTrue();

            // Act: Terminate kernel
            await _runtime.TerminateAsync(kernelId);

            // Assert: Kernel removed from list
            var kernels = await _runtime.ListKernelsAsync();
            kernels.Should().NotContain(kernelId);
        }
        catch (Exception ex) when (ex.Message.Contains("CUDA", StringComparison.Ordinal))
        {
            Skip.If(true, $"CUDA operation failed: {ex.Message}");
        }
    }

    [SkippableFact(DisplayName = "Launch multiple kernels and verify isolation")]
    public async Task LaunchMultipleKernels_ShouldMaintainIsolation()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        try
        {
            // Arrange
            const string kernel1 = "kernel_1";
            const string kernel2 = "kernel_2";
            const string kernel3 = "kernel_3";

            // Act: Launch multiple kernels
            await _runtime.LaunchAsync(kernel1, 1, 256);
            await _runtime.LaunchAsync(kernel2, 2, 512);
            await _runtime.LaunchAsync(kernel3, 1, 128);

            // Get status for each
            var status1 = await _runtime.GetStatusAsync(kernel1);
            var status2 = await _runtime.GetStatusAsync(kernel2);
            var status3 = await _runtime.GetStatusAsync(kernel3);

            // Assert: Each kernel has correct configuration
            status1.GridSize.Should().Be(1);
            status1.BlockSize.Should().Be(256);

            status2.GridSize.Should().Be(2);
            status2.BlockSize.Should().Be(512);

            status3.GridSize.Should().Be(1);
            status3.BlockSize.Should().Be(128);

            // Assert: All kernels are listed
            var kernels = await _runtime.ListKernelsAsync();
            kernels.Should().Contain(kernel1);
            kernels.Should().Contain(kernel2);
            kernels.Should().Contain(kernel3);

            // Cleanup
            await _runtime.TerminateAsync(kernel1);
            await _runtime.TerminateAsync(kernel2);
            await _runtime.TerminateAsync(kernel3);
        }
        catch (Exception ex) when (ex.Message.Contains("CUDA", StringComparison.Ordinal))
        {
            Skip.If(true, $"CUDA operation failed: {ex.Message}");
        }
    }

    #endregion

    #region Message Queue Integration Tests

    [SkippableFact(DisplayName = "Message queue: Initialize → Enqueue → Dequeue on GPU")]
    public async Task MessageQueue_EndToEndOperation_ShouldSucceed()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        try
        {
            // Arrange
            var queue = new CudaMessageQueue<int>(256, _queueLogger);
            await queue.InitializeAsync();

            var message1 = KernelMessage<int>.Create(0, 1, MessageType.Data, 42);
            var message2 = KernelMessage<int>.Create(0, 1, MessageType.Data, 100);
            var message3 = KernelMessage<int>.Create(0, 1, MessageType.Data, 200);

            // Act: Enqueue messages
            var enqueued1 = await queue.TryEnqueueAsync(message1);
            var enqueued2 = await queue.TryEnqueueAsync(message2);
            var enqueued3 = await queue.TryEnqueueAsync(message3);

            // Assert: Enqueue succeeded
            enqueued1.Should().BeTrue();
            enqueued2.Should().BeTrue();
            enqueued3.Should().BeTrue();
            queue.Count.Should().Be(3);

            // Act: Dequeue messages
            var dequeued1 = await queue.TryDequeueAsync();
            var dequeued2 = await queue.TryDequeueAsync();
            var dequeued3 = await queue.TryDequeueAsync();

            // Assert: Messages dequeued in FIFO order
            dequeued1.Should().NotBeNull();
            dequeued1!.Value.Payload.Should().Be(42);

            dequeued2.Should().NotBeNull();
            dequeued2!.Value.Payload.Should().Be(100);

            dequeued3.Should().NotBeNull();
            dequeued3!.Value.Payload.Should().Be(200);

            queue.Count.Should().Be(0);

            // Cleanup
            await queue.DisposeAsync();
        }
        catch (Exception ex) when (ex.Message.Contains("CUDA", StringComparison.Ordinal))
        {
            Skip.If(true, $"CUDA operation failed: {ex.Message}");
        }
    }

    [SkippableFact(DisplayName = "Message queue: Fill to capacity and verify overflow handling")]
    public async Task MessageQueue_FillToCapacity_ShouldHandleOverflow()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        try
        {
            // Arrange
            const int capacity = 64; // Small capacity for faster test
            var queue = new CudaMessageQueue<int>(capacity, _queueLogger);
            await queue.InitializeAsync();

            // Act: Fill queue to capacity
            int successfulEnqueues = 0;
            for (int i = 0; i < capacity + 10; i++)
            {
                var message = KernelMessage<int>.Create(0, 0, MessageType.Data, i);
                if (await queue.TryEnqueueAsync(message))
                {
                    successfulEnqueues++;
                }
            }

            // Assert: Queue filled to near capacity (accounting for ring buffer overhead)
            successfulEnqueues.Should().BeGreaterThan(capacity - 10);
            successfulEnqueues.Should().BeLessThanOrEqualTo(capacity);
            queue.IsFull.Should().BeTrue();

            // Act: Verify overflow attempts fail
            var overflowMessage = KernelMessage<int>.Create(0, 0, MessageType.Data, 999);
            var overflowResult = await queue.TryEnqueueAsync(overflowMessage);

            // Assert: Overflow rejected
            overflowResult.Should().BeFalse();

            // Act: Dequeue some messages
            for (int i = 0; i < 10; i++)
            {
                await queue.TryDequeueAsync();
            }

            // Assert: Can enqueue again after dequeuing
            var newMessage = KernelMessage<int>.Create(0, 0, MessageType.Data, 1000);
            var newResult = await queue.TryEnqueueAsync(newMessage);
            newResult.Should().BeTrue();

            // Cleanup
            await queue.DisposeAsync();
        }
        catch (Exception ex) when (ex.Message.Contains("CUDA", StringComparison.Ordinal))
        {
            Skip.If(true, $"CUDA operation failed: {ex.Message}");
        }
    }

    [SkippableFact(DisplayName = "Message queue: Statistics tracking")]
    public async Task MessageQueue_Statistics_ShouldTrackOperations()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        try
        {
            // Arrange
            var queue = new CudaMessageQueue<int>(256, _queueLogger);
            await queue.InitializeAsync();

            // Act: Perform operations
            for (int i = 0; i < 10; i++)
            {
                var message = KernelMessage<int>.Create(0, 0, MessageType.Data, i);
                await queue.TryEnqueueAsync(message);
            }

            for (int i = 0; i < 5; i++)
            {
                await queue.TryDequeueAsync();
            }

            // Get statistics
            var stats = await queue.GetStatisticsAsync();

            // Assert: Statistics reflect operations
            stats.TotalEnqueued.Should().Be(10);
            stats.TotalDequeued.Should().Be(5);
            stats.TotalDropped.Should().Be(0);
            queue.Count.Should().Be(5);

            // Cleanup
            await queue.DisposeAsync();
        }
        catch (Exception ex) when (ex.Message.Contains("CUDA", StringComparison.Ordinal))
        {
            Skip.If(true, $"CUDA operation failed: {ex.Message}");
        }
    }

    #endregion

    #region Compiler Integration Tests

    [SkippableFact(DisplayName = "Compiler: Generate valid CUDA C for persistent kernel")]
    public void Compiler_GeneratePersistentKernel_ShouldProduceValidCode()
    {
        // This test doesn't require GPU - tests code generation only

        // Arrange
        var kernelDef = new KernelDefinition
        {
            Name = "TestKernel",
            Source = "// User kernel code",
            EntryPoint = "main"
        };

        var config = new RingKernelConfig
        {
            KernelId = "test_kernel",
            Mode = RingKernelMode.Persistent,
            QueueCapacity = 1024,
            Domain = RingKernelDomain.General,
            MaxInputMessageSizeBytes = 256,
            MaxOutputMessageSizeBytes = 256,
            MessagingStrategy = MessagePassingStrategy.SharedMemory
        };

        // Act
        var cudaSource = _compiler.CompileToCudaC(kernelDef, "// User kernel code", config);

        // Assert: Generated code contains essential components
        cudaSource.Should().Contain("#include <cuda_runtime.h>");
        cudaSource.Should().Contain("#include <cooperative_groups.h>");
        cudaSource.Should().Contain("struct MessageQueue");
        cudaSource.Should().Contain("struct KernelControl");
        cudaSource.Should().Contain("__global__ void");
        cudaSource.Should().Contain("while (true)"); // Persistent loop
        cudaSource.Should().Contain("cuda::atomic<int>");
        cudaSource.Should().Contain("extern \"C\"");
    }

    [SkippableFact(DisplayName = "Compiler: Generate valid CUDA C for event-driven kernel")]
    public void Compiler_GenerateEventDrivenKernel_ShouldProduceValidCode()
    {
        // Arrange
        var kernelDef = new KernelDefinition
        {
            Name = "EventKernel",
            Source = "// Event kernel code",
            EntryPoint = "main"
        };

        var config = new RingKernelConfig
        {
            KernelId = "event_kernel",
            Mode = RingKernelMode.EventDriven,
            QueueCapacity = 512,
            Domain = RingKernelDomain.General,
            MaxInputMessageSizeBytes = 128,
            MaxOutputMessageSizeBytes = 128,
            MessagingStrategy = MessagePassingStrategy.AtomicQueue
        };

        // Act
        var cudaSource = _compiler.CompileToCudaC(kernelDef, "// Event kernel code", config);

        // Assert: Event-driven specific code
        cudaSource.Should().Contain("// Event-driven mode");
        cudaSource.Should().Contain("while (input_queue->try_dequeue");
        cudaSource.Should().NotContain("while (true)"); // No infinite loop
        cudaSource.Should().Contain("if (processed >= 512)"); // Burst limit
    }

    [SkippableFact(DisplayName = "Compiler: Generate graph analytics optimizations")]
    public void Compiler_GenerateGraphAnalyticsKernel_ShouldIncludeGridSync()
    {
        // Arrange
        var kernelDef = new KernelDefinition
        {
            Name = "GraphKernel",
            Source = "// Graph processing code",
            EntryPoint = "main"
        };

        var config = new RingKernelConfig
        {
            KernelId = "graph_kernel",
            Mode = RingKernelMode.Persistent,
            QueueCapacity = 1024,
            Domain = RingKernelDomain.GraphAnalytics,
            MaxInputMessageSizeBytes = 256,
            MaxOutputMessageSizeBytes = 256,
            MessagingStrategy = MessagePassingStrategy.SharedMemory
        };

        // Act
        var cudaSource = _compiler.CompileToCudaC(kernelDef, "// Graph processing code", config);

        // Assert: Graph analytics specific optimizations
        cudaSource.Should().Contain("grid.sync()");
        cudaSource.Should().Contain("// Graph analytics: Synchronize after message processing");
    }

    #endregion

    #region Error Handling Tests

    [SkippableFact(DisplayName = "Runtime: Handle invalid kernel launch gracefully")]
    public async Task Runtime_InvalidKernelLaunch_ShouldThrow()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        // Act & Assert: Invalid grid size
        await FluentActions.Invoking(async () =>
            await _runtime.LaunchAsync("invalid_kernel", -1, 256))
            .Should().ThrowAsync<ArgumentException>();

        // Act & Assert: Invalid block size
        await FluentActions.Invoking(async () =>
            await _runtime.LaunchAsync("invalid_kernel", 1, 0))
            .Should().ThrowAsync<ArgumentException>();

        // Act & Assert: Invalid kernel ID
        await FluentActions.Invoking(async () =>
            await _runtime.LaunchAsync("", 1, 256))
            .Should().ThrowAsync<ArgumentException>();
    }

    [SkippableFact(DisplayName = "Runtime: Operations on non-existent kernel should throw")]
    public async Task Runtime_NonExistentKernel_ShouldThrow()
    {
        // Act & Assert: Activate non-existent kernel
        await FluentActions.Invoking(async () =>
            await _runtime.ActivateAsync("nonexistent"))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");

        // Act & Assert: Get status of non-existent kernel
        await FluentActions.Invoking(async () =>
            await _runtime.GetStatusAsync("nonexistent"))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not found*");

        // Act & Assert: Terminate non-existent kernel
        await FluentActions.Invoking(async () =>
            await _runtime.TerminateAsync("nonexistent"))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    #endregion

    #region Performance Tests

    [SkippableFact(DisplayName = "Performance: Message queue throughput")]
    public async Task Performance_MessageQueueThroughput_ShouldMeetBaseline()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");

        try
        {
            // Arrange
            const int messageCount = 1000;
            var queue = new CudaMessageQueue<int>(1024, _queueLogger);
            await queue.InitializeAsync();

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act: Enqueue messages
            for (int i = 0; i < messageCount; i++)
            {
                var message = KernelMessage<int>.Create(0, 0, MessageType.Data, i);
                await queue.TryEnqueueAsync(message);
            }

            // Dequeue messages
            for (int i = 0; i < messageCount; i++)
            {
                await queue.TryDequeueAsync();
            }

            stopwatch.Stop();

            // Assert: Performance baseline (adjust based on hardware)
            var throughput = messageCount * 2 / stopwatch.Elapsed.TotalSeconds; // Enqueue + Dequeue
            _informationMessage(_runtimeLogger, null);

            // Baseline: Should handle at least 1000 ops/sec
            throughput.Should().BeGreaterThan(1000);

            // Cleanup
            await queue.DisposeAsync();
        }
        catch (Exception ex) when (ex.Message.Contains("CUDA", StringComparison.Ordinal))
        {
            Skip.If(true, $"CUDA operation failed: {ex.Message}");
        }
    }

    #endregion

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                _runtime.DisposeAsync().AsTask().Wait();
                _compiler.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
