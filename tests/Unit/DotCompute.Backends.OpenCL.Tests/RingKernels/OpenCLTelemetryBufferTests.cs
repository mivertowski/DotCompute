// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.OpenCL.RingKernels;
using DotCompute.Backends.OpenCL.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.OpenCL.Tests.RingKernels;

/// <summary>
/// Hardware integration tests for <see cref="OpenCLTelemetryBuffer"/>.
/// Tests mapped pinned memory allocation, zero-copy polling, and OpenCL interop.
/// </summary>
[Trait("Category", "Hardware")]
[Trait("Backend", "OpenCL")]
public sealed class OpenCLTelemetryBufferTests : IDisposable
{
    private readonly ILogger<OpenCLTelemetryBuffer> _logger;
    private readonly IntPtr _context;
    private readonly IntPtr _commandQueue;

    public OpenCLTelemetryBufferTests(ITestOutputHelper output)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddXUnit(output);
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _logger = loggerFactory.CreateLogger<OpenCLTelemetryBuffer>();

        // Note: These would be initialized from an actual OpenCL device in a real test
        // For now, using IntPtr.Zero placeholders - actual tests require OpenCL hardware
        _context = IntPtr.Zero;
        _commandQueue = IntPtr.Zero;
    }

    public void Dispose()
    {
        // Cleanup handled by individual tests
    }

    /// <summary>
    /// Test 1: Verifies that OpenCLTelemetryBuffer allocates mapped memory successfully.
    /// </summary>
    [Fact(Skip = "Requires OpenCL hardware initialization")]
    public void OpenCLTelemetryBuffer_Allocate_Success()
    {
        // Arrange
        var buffer = new OpenCLTelemetryBuffer(_context, _commandQueue, _logger);

        try
        {
            // Act
            buffer.Allocate();

            // Assert
            Assert.NotEqual(IntPtr.Zero, buffer.BufferObject);
            Assert.NotEqual(IntPtr.Zero, buffer.MappedPointer);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Test 2: Verifies that double allocation throws InvalidOperationException.
    /// </summary>
    [Fact(Skip = "Requires OpenCL hardware initialization")]
    public void OpenCLTelemetryBuffer_DoubleAllocate_ThrowsException()
    {
        // Arrange
        var buffer = new OpenCLTelemetryBuffer(_context, _commandQueue, _logger);

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
    [Fact(Skip = "Requires OpenCL hardware initialization")]
    public async Task OpenCLTelemetryBuffer_PollAsync_DefaultValues()
    {
        // Arrange
        var buffer = new OpenCLTelemetryBuffer(_context, _commandQueue, _logger);

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
    [Fact(Skip = "Requires OpenCL hardware initialization")]
    public async Task OpenCLTelemetryBuffer_PollAsync_WithoutAllocation_ThrowsException()
    {
        // Arrange
        var buffer = new OpenCLTelemetryBuffer(_context, _commandQueue, _logger);

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
    [Fact(Skip = "Requires OpenCL hardware initialization")]
    public async Task OpenCLTelemetryBuffer_Reset_ZerosFields()
    {
        // Arrange
        var buffer = new OpenCLTelemetryBuffer(_context, _commandQueue, _logger);

        try
        {
            buffer.Allocate();

            // Simulate some telemetry data by directly writing to mapped memory
            unsafe
            {
                var ptr = (RingKernelTelemetry*)buffer.MappedPointer;
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
    /// Test 6: Verifies that Dispose releases OpenCL memory and unmaps buffer.
    /// </summary>
    [Fact(Skip = "Requires OpenCL hardware initialization")]
    public void OpenCLTelemetryBuffer_Dispose_ReleasesMemory()
    {
        // Arrange
        var buffer = new OpenCLTelemetryBuffer(_context, _commandQueue, _logger);
        buffer.Allocate();

        // Act
        buffer.Dispose();

        // Assert - should not throw
        buffer.Dispose(); // Double dispose is safe
    }

    /// <summary>
    /// Test 7: Verifies that zero-copy polling has sub-microsecond latency.
    /// </summary>
    [Fact(Skip = "Requires OpenCL hardware initialization")]
    public async Task OpenCLTelemetryBuffer_PollAsync_ZeroCopyPerformance()
    {
        // Arrange
        var buffer = new OpenCLTelemetryBuffer(_context, _commandQueue, _logger);

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

            // Assert - Average latency should be < 1μs per poll
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
    [Fact(Skip = "Requires OpenCL hardware initialization")]
    public async Task OpenCLTelemetryBuffer_PollAsync_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var buffer = new OpenCLTelemetryBuffer(_context, _commandQueue, _logger);

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
                        Assert.NotNull(telemetry);
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
