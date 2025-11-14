// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.RingKernels;
using Xunit;

namespace DotCompute.Abstractions.Tests.RingKernels;

/// <summary>
/// Unit tests for <see cref="RingKernelTelemetry"/> struct.
/// Validates struct layout, atomic operations, and thread safety.
/// </summary>
public sealed class RingKernelTelemetryTests
{
    /// <summary>
    /// Test 1: Verifies that RingKernelTelemetry has the correct size (64 bytes for cache-line alignment).
    /// </summary>
    [Fact]
    public void RingKernelTelemetry_SizeIs64Bytes()
    {
        // Arrange & Act
        var size = Marshal.SizeOf<RingKernelTelemetry>();

        // Assert
        Assert.Equal(64, size);
    }

    /// <summary>
    /// Test 2: Verifies that RingKernelTelemetry is a sequential layout struct (required for P/Invoke).
    /// </summary>
    [Fact]
    public void RingKernelTelemetry_HasSequentialLayout()
    {
        // Arrange
        var type = typeof(RingKernelTelemetry);

        // Act
        var attribute = type.StructLayoutAttribute;

        // Assert
        Assert.NotNull(attribute);
        Assert.Equal(LayoutKind.Sequential, attribute!.Value);
    }

    /// <summary>
    /// Test 3: Verifies that a default-initialized RingKernelTelemetry has all fields set to zero.
    /// </summary>
    [Fact]
    public void RingKernelTelemetry_DefaultInitialization_AllFieldsZero()
    {
        // Arrange & Act
        var telemetry = new RingKernelTelemetry();

        // Assert
        Assert.Equal(0UL, telemetry.MessagesProcessed);
        Assert.Equal(0UL, telemetry.MessagesDropped);
        Assert.Equal(0, telemetry.QueueDepth);
        Assert.Equal(0L, telemetry.LastProcessedTimestamp);
    }

    /// <summary>
    /// Test 4: Verifies that MessagesProcessed can be updated using Interlocked.Add.
    /// </summary>
    [Fact]
    public void RingKernelTelemetry_MessagesProcessed_InterlockedAdd()
    {
        // Arrange
        var telemetry = new RingKernelTelemetry();

        // Act
        Interlocked.Add(ref telemetry.MessagesProcessed, 42UL);

        // Assert
        Assert.Equal(42UL, telemetry.MessagesProcessed);
    }

    /// <summary>
    /// Test 5: Verifies that MessagesDropped can be updated using Interlocked.Add.
    /// </summary>
    [Fact]
    public void RingKernelTelemetry_MessagesDropped_InterlockedAdd()
    {
        // Arrange
        var telemetry = new RingKernelTelemetry();

        // Act
        Interlocked.Add(ref telemetry.MessagesDropped, 7UL);

        // Assert
        Assert.Equal(7UL, telemetry.MessagesDropped);
    }

    /// <summary>
    /// Test 6: Verifies that QueueDepth can be updated using Interlocked.Exchange.
    /// </summary>
    [Fact]
    public void RingKernelTelemetry_QueueDepth_InterlockedExchange()
    {
        // Arrange
        var telemetry = new RingKernelTelemetry();

        // Act
        Interlocked.Exchange(ref telemetry.QueueDepth, 128);

        // Assert
        Assert.Equal(128, telemetry.QueueDepth);
    }

    /// <summary>
    /// Test 7: Verifies that LastProcessedTimestamp can be updated using Interlocked.Exchange.
    /// </summary>
    [Fact]
    public void RingKernelTelemetry_LastProcessedTimestamp_InterlockedExchange()
    {
        // Arrange
        var telemetry = new RingKernelTelemetry();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Act
        Interlocked.Exchange(ref telemetry.LastProcessedTimestamp, timestamp);

        // Assert
        Assert.Equal(timestamp, telemetry.LastProcessedTimestamp);
    }

    /// <summary>
    /// Test 8: Verifies thread safety by concurrently incrementing MessagesProcessed from multiple threads.
    /// </summary>
    [Fact]
    public async Task RingKernelTelemetry_MessagesProcessed_ThreadSafe()
    {
        // Arrange
        var telemetry = new RingKernelTelemetry();
        const int threadCount = 10;
        const int incrementsPerThread = 1000;
        var tasks = new Task[threadCount];

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < incrementsPerThread; j++)
                {
                    Interlocked.Add(ref telemetry.MessagesProcessed, 1UL);
                }
            });
        }
        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal((ulong)(threadCount * incrementsPerThread), telemetry.MessagesProcessed);
    }

    /// <summary>
    /// Test 9: Verifies thread safety by concurrently incrementing MessagesDropped from multiple threads.
    /// </summary>
    [Fact]
    public async Task RingKernelTelemetry_MessagesDropped_ThreadSafe()
    {
        // Arrange
        var telemetry = new RingKernelTelemetry();
        const int threadCount = 10;
        const int incrementsPerThread = 1000;
        var tasks = new Task[threadCount];

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < incrementsPerThread; j++)
                {
                    Interlocked.Add(ref telemetry.MessagesDropped, 1UL);
                }
            });
        }
        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal((ulong)(threadCount * incrementsPerThread), telemetry.MessagesDropped);
    }

    /// <summary>
    /// Test 10: Verifies thread safety by concurrently updating QueueDepth from multiple threads.
    /// </summary>
    [Fact]
    public async Task RingKernelTelemetry_QueueDepth_ThreadSafe()
    {
        // Arrange
        var telemetry = new RingKernelTelemetry();
        const int threadCount = 10;
        const int updatesPerThread = 100;
        var tasks = new Task[threadCount];

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < updatesPerThread; j++)
                {
                    Interlocked.Exchange(ref telemetry.QueueDepth, threadId * 10 + j);
                }
            });
        }
        await Task.WhenAll(tasks);

        // Assert
        // QueueDepth should be one of the values written by any thread (non-deterministic, but valid)
        Assert.InRange(telemetry.QueueDepth, 0, threadCount * 10 + updatesPerThread);
    }

    /// <summary>
    /// Test 11: Verifies that RingKernelTelemetry can be marshaled to unmanaged memory and back.
    /// </summary>
    [Fact]
    public void RingKernelTelemetry_MarshalToUnmanagedMemory()
    {
        // Arrange
        var original = new RingKernelTelemetry
        {
            MessagesProcessed = 1000UL,
            MessagesDropped = 5UL,
            QueueDepth = 256,
            LastProcessedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        // Act
        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<RingKernelTelemetry>());
        try
        {
            Marshal.StructureToPtr(original, ptr, false);
            var restored = Marshal.PtrToStructure<RingKernelTelemetry>(ptr);

            // Assert
            Assert.Equal(original.MessagesProcessed, restored.MessagesProcessed);
            Assert.Equal(original.MessagesDropped, restored.MessagesDropped);
            Assert.Equal(original.QueueDepth, restored.QueueDepth);
            Assert.Equal(original.LastProcessedTimestamp, restored.LastProcessedTimestamp);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    /// <summary>
    /// Test 12: Verifies that RingKernelTelemetry correctly handles maximum ulong values.
    /// </summary>
    [Fact]
    public void RingKernelTelemetry_MaxValues_NoOverflow()
    {
        // Arrange
        var telemetry = new RingKernelTelemetry
        {
            MessagesProcessed = ulong.MaxValue - 100,
            MessagesDropped = ulong.MaxValue - 50
        };

        // Act
        Interlocked.Add(ref telemetry.MessagesProcessed, 50UL);
        Interlocked.Add(ref telemetry.MessagesDropped, 25UL);

        // Assert
        Assert.Equal(ulong.MaxValue - 50, telemetry.MessagesProcessed);
        Assert.Equal(ulong.MaxValue - 25, telemetry.MessagesDropped);
    }

    /// <summary>
    /// Test 13: Verifies that multiple RingKernelTelemetry instances remain independent.
    /// </summary>
    [Fact]
    public void RingKernelTelemetry_MultipleInstances_Independent()
    {
        // Arrange
        var telemetry1 = new RingKernelTelemetry();
        var telemetry2 = new RingKernelTelemetry();

        // Act
        Interlocked.Add(ref telemetry1.MessagesProcessed, 100UL);
        Interlocked.Add(ref telemetry2.MessagesProcessed, 200UL);
        Interlocked.Exchange(ref telemetry1.QueueDepth, 10);
        Interlocked.Exchange(ref telemetry2.QueueDepth, 20);

        // Assert
        Assert.Equal(100UL, telemetry1.MessagesProcessed);
        Assert.Equal(200UL, telemetry2.MessagesProcessed);
        Assert.Equal(10, telemetry1.QueueDepth);
        Assert.Equal(20, telemetry2.QueueDepth);
    }

    /// <summary>
    /// Test 14: Verifies that RingKernelTelemetry struct can be copied by value correctly.
    /// </summary>
    [Fact]
    public void RingKernelTelemetry_ValueSemantics_Copy()
    {
        // Arrange
        var original = new RingKernelTelemetry
        {
            MessagesProcessed = 500UL,
            MessagesDropped = 10UL,
            QueueDepth = 64,
            LastProcessedTimestamp = 12345678L
        };

        // Act
        var copy = original; // Value type copy
        Interlocked.Add(ref copy.MessagesProcessed, 100UL); // Modify copy

        // Assert
        Assert.Equal(500UL, original.MessagesProcessed); // Original unchanged
        Assert.Equal(600UL, copy.MessagesProcessed); // Copy modified
    }

    /// <summary>
    /// Test 15: Verifies that RingKernelTelemetry maintains data integrity under high-frequency updates.
    /// </summary>
    [Fact]
    public async Task RingKernelTelemetry_HighFrequencyUpdates_DataIntegrity()
    {
        // Arrange
        var telemetry = new RingKernelTelemetry();
        const int threadCount = 20;
        const int updatesPerThread = 5000;
        var tasks = new Task[threadCount];

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < updatesPerThread; j++)
                {
                    // Simulate real telemetry updates
                    Interlocked.Add(ref telemetry.MessagesProcessed, 1UL);
                    if (j % 100 == 0)
                    {
                        Interlocked.Add(ref telemetry.MessagesDropped, 1UL);
                    }
                    Interlocked.Exchange(ref telemetry.QueueDepth, j % 512);
                    Interlocked.Exchange(ref telemetry.LastProcessedTimestamp, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                }
            });
        }
        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal((ulong)(threadCount * updatesPerThread), telemetry.MessagesProcessed);
        Assert.Equal((ulong)(threadCount * (updatesPerThread / 100)), telemetry.MessagesDropped);
        Assert.InRange(telemetry.QueueDepth, 0, 512);
        Assert.True(telemetry.LastProcessedTimestamp > 0);
    }
}
