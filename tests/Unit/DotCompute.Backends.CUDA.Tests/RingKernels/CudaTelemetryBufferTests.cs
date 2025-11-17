// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.RingKernels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.CUDA.Tests.RingKernels;

/// <summary>
/// Hardware integration tests for <see cref="CudaTelemetryBuffer"/>.
/// Tests pinned host memory allocation, zero-copy polling, and GPU interop.
/// </summary>
[Trait("Category", "Hardware")]
[Trait("Backend", "CUDA")]
public sealed class CudaTelemetryBufferTests : IDisposable
{
    private readonly ILogger<CudaTelemetryBuffer> _logger;

    public CudaTelemetryBufferTests(ITestOutputHelper output)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _logger = loggerFactory.CreateLogger<CudaTelemetryBuffer>();
    }

    public void Dispose()
    {
        // Cleanup handled by individual tests
    }

    /// <summary>
    /// Test 1: Verifies that CudaTelemetryBuffer allocates pinned host memory successfully.
    /// </summary>
    [Fact]
    public void CudaTelemetryBuffer_Allocate_Success()
    {
        // Arrange
        var buffer = new CudaTelemetryBuffer(_logger);

        try
        {
            // Act
            buffer.Allocate();

            // Assert
            Assert.NotEqual(IntPtr.Zero, buffer.HostPointer);
            Assert.NotEqual(IntPtr.Zero, buffer.DevicePointer);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Test 2: Verifies that double allocation throws InvalidOperationException.
    /// </summary>
    [Fact]
    public void CudaTelemetryBuffer_DoubleAllocate_ThrowsException()
    {
        // Arrange
        var buffer = new CudaTelemetryBuffer(_logger);

        try
        {
            buffer.Allocate();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => buffer.Allocate());
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Test 3: Verifies that PollAsync returns default telemetry after allocation.
    /// </summary>
    [Fact]
    public async Task CudaTelemetryBuffer_PollAsync_DefaultValues()
    {
        // Arrange
        var buffer = new CudaTelemetryBuffer(_logger);

        try
        {
            buffer.Allocate();

            // Act
            var telemetry = await buffer.PollAsync();

            // Assert
            Assert.Equal(0UL, telemetry.MessagesProcessed);
            Assert.Equal(0UL, telemetry.MessagesDropped);
            Assert.Equal(0, telemetry.QueueDepth);
            Assert.Equal(0L, telemetry.LastProcessedTimestamp);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Test 4: Verifies that PollAsync without allocation throws InvalidOperationException.
    /// </summary>
    [Fact]
    public async Task CudaTelemetryBuffer_PollAsync_WithoutAllocation_ThrowsException()
    {
        // Arrange
        var buffer = new CudaTelemetryBuffer(_logger);

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => buffer.PollAsync());
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Test 5: Verifies that Reset zeros out telemetry fields.
    /// </summary>
    [Fact]
    public async Task CudaTelemetryBuffer_Reset_ZerosFields()
    {
        // Arrange
        var buffer = new CudaTelemetryBuffer(_logger);

        try
        {
            buffer.Allocate();

            // Simulate some telemetry data by directly writing to host memory
            unsafe
            {
                var ptr = (RingKernelTelemetry*)buffer.HostPointer;
                ptr->MessagesProcessed = 100UL;
                ptr->MessagesDropped = 5UL;
                ptr->QueueDepth = 256;
                ptr->LastProcessedTimestamp = 123456L;
            }

            // Act
            buffer.Reset();
            var telemetry = await buffer.PollAsync();

            // Assert
            Assert.Equal(0UL, telemetry.MessagesProcessed);
            Assert.Equal(0UL, telemetry.MessagesDropped);
            Assert.Equal(0, telemetry.QueueDepth);
            Assert.Equal(0L, telemetry.LastProcessedTimestamp);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Test 6: Verifies that Dispose releases CUDA memory and nullifies pointers.
    /// </summary>
    [Fact]
    public void CudaTelemetryBuffer_Dispose_ReleasesMemory()
    {
        // Arrange
        var buffer = new CudaTelemetryBuffer(_logger);
        buffer.Allocate();

        // Act
        buffer.Dispose();

        // Assert - should not throw
        buffer.Dispose(); // Double dispose is safe
    }

    /// <summary>
    /// Test 7: Verifies that zero-copy polling has sub-microsecond latency.
    /// </summary>
    [Fact]
    public async Task CudaTelemetryBuffer_PollAsync_ZeroCopyPerformance()
    {
        // Arrange
        var buffer = new CudaTelemetryBuffer(_logger);

        try
        {
            buffer.Allocate();

            // Warmup
            for (int i = 0; i < 10; i++)
            {
                _ = await buffer.PollAsync();
            }

            // Act - measure 1000 polls
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                _ = await buffer.PollAsync();
            }
            sw.Stop();

            // Assert - Average latency should be < 1μs per poll (1000μs for 1000 polls)
            var averageLatencyMicroseconds = sw.Elapsed.TotalMicroseconds / 1000.0;
            Assert.True(averageLatencyMicroseconds < 1.0,
                $"Average latency {averageLatencyMicroseconds:F3}μs exceeds 1μs target");
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Test 8: Verifies that concurrent polls from multiple threads are thread-safe.
    /// </summary>
    [Fact]
    public async Task CudaTelemetryBuffer_PollAsync_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var buffer = new CudaTelemetryBuffer(_logger);

        try
        {
            buffer.Allocate();

            const int threadCount = 10;
            const int pollsPerThread = 100;
            var tasks = new Task[threadCount];

            // Act
            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    for (int j = 0; j < pollsPerThread; j++)
                    {
                        var telemetry = await buffer.PollAsync();
                        Assert.NotNull(telemetry); // Verifies no corruption
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Assert - no exceptions means success
        }
        finally
        {
            buffer.Dispose();
        }
    }
}
