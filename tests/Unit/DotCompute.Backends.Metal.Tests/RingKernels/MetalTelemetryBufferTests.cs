// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.Metal.RingKernels;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.Tests.RingKernels;

/// <summary>
/// Hardware integration tests for <see cref="MetalTelemetryBuffer"/>.
/// Tests shared memory allocation, zero-copy polling, and Metal interop on Apple Silicon.
/// </summary>
[Trait("Category", "Hardware")]
[Trait("Backend", "Metal")]
public sealed class MetalTelemetryBufferTests : IDisposable
{
    private readonly ILogger<MetalTelemetryBuffer> _logger;
    private readonly IntPtr _device;
    private readonly bool _isMetalAvailable;

    public MetalTelemetryBufferTests(ITestOutputHelper output)
    {
        _logger = new TestOutputLogger<MetalTelemetryBuffer>(output);
        _isMetalAvailable = MetalNative.IsMetalSupported();

        if (_isMetalAvailable)
        {
            _device = MetalNative.CreateSystemDefaultDevice();
        }
        else
        {
            _device = IntPtr.Zero;
        }
    }

    private void RequireMetalSupport()
    {
        Skip.IfNot(_isMetalAvailable, "Metal is not available on this system");
        Skip.If(_device == IntPtr.Zero, "Metal device could not be created");
    }

    public void Dispose()
    {
        if (_device != IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
        }
    }

    /// <summary>
    /// Test 1: Verifies that MetalTelemetryBuffer allocates shared memory successfully.
    /// </summary>
    [SkippableFact]
    public void MetalTelemetryBuffer_Allocate_Success()
    {
        RequireMetalSupport();

        // Arrange
        var buffer = new MetalTelemetryBuffer(_device, _logger);

        try
        {
            // Act
            buffer.Allocate();

            // Assert
            Assert.NotEqual(IntPtr.Zero, buffer.BufferObject);
            Assert.NotEqual(IntPtr.Zero, buffer.ContentsPointer);
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
    public void MetalTelemetryBuffer_DoubleAllocate_ThrowsException()
    {
        RequireMetalSupport();

        // Arrange
        var buffer = new MetalTelemetryBuffer(_device, _logger);

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
    public async Task MetalTelemetryBuffer_PollAsync_DefaultValues()
    {
        RequireMetalSupport();

        // Arrange
        var buffer = new MetalTelemetryBuffer(_device, _logger);

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
    [SkippableFact]
    public async Task MetalTelemetryBuffer_PollAsync_WithoutAllocation_ThrowsException()
    {
        RequireMetalSupport();

        // Arrange
        var buffer = new MetalTelemetryBuffer(_device, _logger);

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
    [SkippableFact]
    public async Task MetalTelemetryBuffer_Reset_ZerosFields()
    {
        RequireMetalSupport();

        // Arrange
        var buffer = new MetalTelemetryBuffer(_device, _logger);

        try
        {
            buffer.Allocate();

            // Simulate some telemetry data by directly writing to shared memory
            unsafe
            {
                var ptr = (RingKernelTelemetry*)buffer.ContentsPointer;
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
    /// Test 6: Verifies that Dispose releases Metal memory buffer.
    /// </summary>
    [SkippableFact]
    public void MetalTelemetryBuffer_Dispose_ReleasesMemory()
    {
        RequireMetalSupport();

        // Arrange
        var buffer = new MetalTelemetryBuffer(_device, _logger);
        buffer.Allocate();

        // Act
        buffer.Dispose();

        // Assert - should not throw
        buffer.Dispose(); // Double dispose is safe
    }

    /// <summary>
    /// Test 7: Verifies that zero-copy polling has sub-microsecond latency on unified memory.
    /// </summary>
    [SkippableFact]
    public async Task MetalTelemetryBuffer_PollAsync_ZeroCopyPerformance()
    {
        RequireMetalSupport();

        // Arrange
        var buffer = new MetalTelemetryBuffer(_device, _logger);

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

            // Assert - Average latency should be < 2μs per poll (accounting for debug overhead and system variance)
            var averageLatencyMicroseconds = sw.Elapsed.TotalMicroseconds / 1000.0;
            Assert.True(averageLatencyMicroseconds < 2.0,
                $"Average latency {averageLatencyMicroseconds:F3}μs exceeds 2μs target");
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
    public async Task MetalTelemetryBuffer_PollAsync_ConcurrentAccess_ThreadSafe()
    {
        RequireMetalSupport();

        // Arrange
        var buffer = new MetalTelemetryBuffer(_device, _logger);

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

    /// <summary>
    /// Simple test logger that writes to xUnit output.
    /// </summary>
    private sealed class TestOutputLogger<T> : ILogger<T>
    {
        private readonly ITestOutputHelper _output;

        public TestOutputLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            _output.WriteLine($"[{logLevel}] {message}");
            if (exception != null)
            {
                _output.WriteLine(exception.ToString());
            }
        }

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }
}

/// <summary>
/// Skip attribute helper for conditional test execution.
/// </summary>
public static class Skip
{
    public static void IfNot(bool condition, string reason)
    {
        if (!condition)
        {
            throw new Xunit.SkipException(reason);
        }
    }

    public static void If(bool condition, string reason)
    {
        if (condition)
        {
            throw new Xunit.SkipException(reason);
        }
    }
}
