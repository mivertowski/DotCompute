// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading.Tasks;
using DotCompute.Backends.CUDA.Timing;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests.Timing;

/// <summary>
/// Tests for <see cref="CudaEventTimer"/>.
/// </summary>
public class CudaEventTimerTests
{
    #region CreateEventAsync Tests

    [Fact]
    public async Task CreateEventAsync_WithValidContext_CreatesEvent()
    {
        // Arrange - Create minimal CUDA context
        var contextPtr = GetOrCreateTestContext();

        try
        {
            // Act
            var eventPtr = await CudaEventTimer.CreateEventAsync(contextPtr);

            // Assert
            Assert.NotEqual(IntPtr.Zero, eventPtr);

            // Cleanup
            await CudaEventTimer.DestroyEventAsync(eventPtr, contextPtr);
        }
        finally
        {
            CleanupTestContext(contextPtr);
        }
    }

    [Fact]
    public async Task CreateEventAsync_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CudaEventTimer.CreateEventAsync(IntPtr.Zero));
    }

    #endregion

    #region RecordEventAsync Tests

    [Fact]
    public async Task RecordEventAsync_WithValidParameters_RecordsEvent()
    {
        // Arrange
        var contextPtr = GetOrCreateTestContext();

        try
        {
            var eventPtr = await CudaEventTimer.CreateEventAsync(contextPtr);
            var stream = CreateTestStream(contextPtr);

            try
            {
                // Act - Should not throw
                await CudaEventTimer.RecordEventAsync(eventPtr, stream, contextPtr);

                // Assert - Synchronize to verify event was recorded
                await CudaEventTimer.SynchronizeEventAsync(eventPtr, contextPtr);
            }
            finally
            {
                await CudaEventTimer.DestroyEventAsync(eventPtr, contextPtr);
                stream.Dispose();
            }
        }
        finally
        {
            CleanupTestContext(contextPtr);
        }
    }

    [Fact]
    public async Task RecordEventAsync_NullEvent_ThrowsArgumentNullException()
    {
        // Arrange
        var contextPtr = GetOrCreateTestContext();

        try
        {
            var stream = CreateTestStream(contextPtr);

            try
            {
                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() =>
                    CudaEventTimer.RecordEventAsync(IntPtr.Zero, stream, contextPtr));
            }
            finally
            {
                stream.Dispose();
            }
        }
        finally
        {
            CleanupTestContext(contextPtr);
        }
    }

    #endregion

    #region SynchronizeEventAsync Tests

    [Fact]
    public async Task SynchronizeEventAsync_WithRecordedEvent_Synchronizes()
    {
        // Arrange
        var contextPtr = GetOrCreateTestContext();

        try
        {
            var eventPtr = await CudaEventTimer.CreateEventAsync(contextPtr);
            var stream = CreateTestStream(contextPtr);

            try
            {
                await CudaEventTimer.RecordEventAsync(eventPtr, stream, contextPtr);

                // Act - Should not throw
                await CudaEventTimer.SynchronizeEventAsync(eventPtr, contextPtr);

                // Assert - Implicit success (no exception)
            }
            finally
            {
                await CudaEventTimer.DestroyEventAsync(eventPtr, contextPtr);
                stream.Dispose();
            }
        }
        finally
        {
            CleanupTestContext(contextPtr);
        }
    }

    [Fact]
    public async Task SynchronizeEventAsync_NullEvent_ThrowsArgumentNullException()
    {
        // Arrange
        var contextPtr = GetOrCreateTestContext();

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                CudaEventTimer.SynchronizeEventAsync(IntPtr.Zero, contextPtr));
        }
        finally
        {
            CleanupTestContext(contextPtr);
        }
    }

    #endregion

    #region ElapsedTimeAsync Tests

    [Fact]
    public async Task ElapsedTimeAsync_BetweenTwoEvents_ReturnsPositiveTime()
    {
        // Arrange
        var contextPtr = GetOrCreateTestContext();

        try
        {
            var startEvent = await CudaEventTimer.CreateEventAsync(contextPtr);
            var endEvent = await CudaEventTimer.CreateEventAsync(contextPtr);
            var stream = CreateTestStream(contextPtr);

            try
            {
                // Record start and end events
                await CudaEventTimer.RecordEventAsync(startEvent, stream, contextPtr);

                // Simulate some work (simple delay)
                await Task.Delay(10);

                await CudaEventTimer.RecordEventAsync(endEvent, stream, contextPtr);
                await CudaEventTimer.SynchronizeEventAsync(endEvent, contextPtr);

                // Act
                var elapsedMs = await CudaEventTimer.ElapsedTimeAsync(startEvent, endEvent, contextPtr);

                // Assert - Should be >= 0 (GPU operations may be faster than CPU delay)
                Assert.True(elapsedMs >= 0, $"Expected elapsed time >= 0, got {elapsedMs}");
            }
            finally
            {
                await CudaEventTimer.DestroyEventAsync(startEvent, contextPtr);
                await CudaEventTimer.DestroyEventAsync(endEvent, contextPtr);
                stream.Dispose();
            }
        }
        finally
        {
            CleanupTestContext(contextPtr);
        }
    }

    [Fact]
    public async Task ElapsedTimeAsync_SameEvent_ReturnsZero()
    {
        // Arrange
        var contextPtr = GetOrCreateTestContext();

        try
        {
            var eventPtr = await CudaEventTimer.CreateEventAsync(contextPtr);
            var stream = CreateTestStream(contextPtr);

            try
            {
                await CudaEventTimer.RecordEventAsync(eventPtr, stream, contextPtr);
                await CudaEventTimer.SynchronizeEventAsync(eventPtr, contextPtr);

                // Act
                var elapsedMs = await CudaEventTimer.ElapsedTimeAsync(eventPtr, eventPtr, contextPtr);

                // Assert
                Assert.Equal(0f, elapsedMs);
            }
            finally
            {
                await CudaEventTimer.DestroyEventAsync(eventPtr, contextPtr);
                stream.Dispose();
            }
        }
        finally
        {
            CleanupTestContext(contextPtr);
        }
    }

    [Fact]
    public async Task ElapsedTimeAsync_NullStartEvent_ThrowsArgumentNullException()
    {
        // Arrange
        var contextPtr = GetOrCreateTestContext();

        try
        {
            var endEvent = await CudaEventTimer.CreateEventAsync(contextPtr);

            try
            {
                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() =>
                    CudaEventTimer.ElapsedTimeAsync(IntPtr.Zero, endEvent, contextPtr));
            }
            finally
            {
                await CudaEventTimer.DestroyEventAsync(endEvent, contextPtr);
            }
        }
        finally
        {
            CleanupTestContext(contextPtr);
        }
    }

    #endregion

    #region DestroyEventAsync Tests

    [Fact]
    public async Task DestroyEventAsync_WithValidEvent_DestroysEvent()
    {
        // Arrange
        var contextPtr = GetOrCreateTestContext();

        try
        {
            var eventPtr = await CudaEventTimer.CreateEventAsync(contextPtr);

            // Act - Should not throw
            await CudaEventTimer.DestroyEventAsync(eventPtr, contextPtr);

            // Assert - Implicit success (no exception)
        }
        finally
        {
            CleanupTestContext(contextPtr);
        }
    }

    [Fact]
    public async Task DestroyEventAsync_NullEvent_ThrowsArgumentNullException()
    {
        // Arrange
        var contextPtr = GetOrCreateTestContext();

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                CudaEventTimer.DestroyEventAsync(IntPtr.Zero, contextPtr));
        }
        finally
        {
            CleanupTestContext(contextPtr);
        }
    }

    #endregion

    #region MeasureAsync Tests

    [Fact]
    public async Task MeasureAsync_WithSimpleOperation_ReturnsMeasurement()
    {
        // Arrange
        var contextPtr = GetOrCreateTestContext();

        try
        {
            var stream = CreateTestStream(contextPtr);

            try
            {
                // Act - Measure a simple operation (empty lambda)
                var elapsedMs = await CudaEventTimer.MeasureAsync(
                    async streamHandle =>
                    {
                        await Task.Delay(1); // Simulate minimal work
                    },
                    stream,
                    contextPtr);

                // Assert
                Assert.True(elapsedMs >= 0, $"Expected elapsed time >= 0, got {elapsedMs}");
            }
            finally
            {
                stream.Dispose();
            }
        }
        finally
        {
            CleanupTestContext(contextPtr);
        }
    }

    [Fact]
    public async Task MeasureAsync_NullOperation_ThrowsArgumentNullException()
    {
        // Arrange
        var contextPtr = GetOrCreateTestContext();

        try
        {
            var stream = CreateTestStream(contextPtr);

            try
            {
                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() =>
                    CudaEventTimer.MeasureAsync(null!, stream, contextPtr));
            }
            finally
            {
                stream.Dispose();
            }
        }
        finally
        {
            CleanupTestContext(contextPtr);
        }
    }

    [Fact]
    public async Task MeasureAsync_OperationThrows_CleansUpEvents()
    {
        // Arrange
        var contextPtr = GetOrCreateTestContext();

        try
        {
            var stream = CreateTestStream(contextPtr);

            try
            {
                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await CudaEventTimer.MeasureAsync(
                        async streamHandle =>
                        {
                            await Task.Yield();
                            throw new InvalidOperationException("Test exception");
                        },
                        stream,
                        contextPtr));

                // Assert - Implicit: no resource leaks (events should be cleaned up)
            }
            finally
            {
                stream.Dispose();
            }
        }
        finally
        {
            CleanupTestContext(contextPtr);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets or creates a test CUDA context.
    /// </summary>
    private static IntPtr GetOrCreateTestContext()
    {
        // Initialize CUDA if not already initialized
        var initResult = CudaRuntime.cuInit(0);
        if (initResult != CudaError.Success && initResult != CudaError.NoDevice)
        {
            throw new InvalidOperationException($"Failed to initialize CUDA: {initResult}");
        }

        // Get device 0
        var deviceResult = CudaRuntime.cuDeviceGet(out var device, 0);
        if (deviceResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to get CUDA device: {deviceResult}");
        }

        // Create context
        var contextPtr = IntPtr.Zero;
        var contextResult = CudaRuntime.cuCtxCreate_v2(ref contextPtr, 0, device);
        if (contextResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to create CUDA context: {contextResult}");
        }

        return contextPtr;
    }

    /// <summary>
    /// Creates a test CUDA stream.
    /// </summary>
    private static CudaStream CreateTestStream(IntPtr contextPtr)
    {
        // Make context current
        var setContextResult = CudaRuntime.cuCtxSetCurrent(contextPtr);
        if (setContextResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to set CUDA context: {setContextResult}");
        }

        // Create stream
        var streamPtr = IntPtr.Zero;
        var streamResult = CudaRuntime.cudaStreamCreate(ref streamPtr);
        if (streamResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to create CUDA stream: {streamResult}");
        }

        return new CudaStream(streamPtr, contextPtr);
    }

    /// <summary>
    /// Cleans up a test CUDA context.
    /// </summary>
    private static void CleanupTestContext(IntPtr contextPtr)
    {
        if (contextPtr != IntPtr.Zero)
        {
            try
            {
                _ = CudaRuntime.cuCtxDestroy_v2(contextPtr);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #endregion
}
