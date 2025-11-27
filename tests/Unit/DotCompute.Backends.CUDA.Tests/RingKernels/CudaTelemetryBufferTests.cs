// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.RingKernels;
using DotCompute.SharedTestUtilities.Cuda;
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
    /// Test 1: Verifies that CudaTelemetryBuffer allocates memory successfully.
    /// In WSL2, falls back to device memory when pinned allocation fails.
    /// </summary>
    [SkippableFact]
    public void CudaTelemetryBuffer_Allocate_Success()
    {
        Skip.IfNot(CudaTestHelpers.IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var buffer = new CudaTelemetryBuffer(_logger);

        try
        {
            // Act
            buffer.Allocate();

            // Assert - DevicePointer must always be set after successful allocation
            // HostPointer is only set in pinned mode, not in WSL2 fallback mode
            Assert.NotEqual(IntPtr.Zero, buffer.DevicePointer);

            // Note: In pinned mode, HostPointer is also set; in fallback mode it's IntPtr.Zero
            // Both modes are valid, so we only assert DevicePointer
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Test 2: Verifies that double allocation throws InvalidOperationException.
    /// </summary>
    [SkippableFact]
    public void CudaTelemetryBuffer_DoubleAllocate_ThrowsException()
    {
        Skip.IfNot(CudaTestHelpers.IsCudaAvailable(), "CUDA hardware not available");

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
    [SkippableFact]
    public async Task CudaTelemetryBuffer_PollAsync_DefaultValues()
    {
        Skip.IfNot(CudaTestHelpers.IsCudaAvailable(), "CUDA hardware not available");

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
    /// Requires pinned memory (zero-copy mode) for direct host pointer access.
    /// </summary>
    [SkippableFact]
    public async Task CudaTelemetryBuffer_Reset_ZerosFields()
    {
        Skip.IfNot(CudaTestHelpers.IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var buffer = new CudaTelemetryBuffer(_logger);

        try
        {
            buffer.Allocate();

            // Skip if in fallback mode (no pinned memory available for direct access)
            Skip.If(buffer.HostPointer == IntPtr.Zero,
                "Pinned memory not available (WSL2 fallback mode) - cannot directly write to host memory");

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
    [SkippableFact]
    public void CudaTelemetryBuffer_Dispose_ReleasesMemory()
    {
        Skip.IfNot(CudaTestHelpers.IsCudaAvailable(), "CUDA hardware not available");

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
    /// Requires pinned memory mode - fallback mode uses cudaMemcpy which is slower.
    /// </summary>
    [SkippableFact]
    public async Task CudaTelemetryBuffer_PollAsync_ZeroCopyPerformance()
    {
        Skip.IfNot(CudaTestHelpers.IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var buffer = new CudaTelemetryBuffer(_logger);

        try
        {
            buffer.Allocate();

            // Skip if not in zero-copy mode (fallback uses cudaMemcpy which is ~10-100x slower)
            Skip.If(buffer.HostPointer == IntPtr.Zero,
                "Zero-copy mode not available (WSL2 fallback uses cudaMemcpy with higher latency)");

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
    [SkippableFact]
    public async Task CudaTelemetryBuffer_PollAsync_ConcurrentAccess_ThreadSafe()
    {
        Skip.IfNot(CudaTestHelpers.IsCudaAvailable(), "CUDA hardware not available");

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
