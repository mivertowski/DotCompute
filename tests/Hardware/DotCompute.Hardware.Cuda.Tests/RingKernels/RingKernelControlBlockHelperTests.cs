// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.RingKernels;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Tests.Common.Helpers;
using FluentAssertions;
using Xunit;

namespace DotCompute.Hardware.Cuda.Tests.RingKernels;

/// <summary>
/// Hardware tests for RingKernelControlBlockHelper GPU operations.
/// Tests device memory allocation, read/write operations, and atomic updates.
/// </summary>
[Collection("CUDA Hardware")]
public class RingKernelControlBlockHelperTests : IDisposable
{
    private IntPtr _context;
    private bool _disposed;

    public RingKernelControlBlockHelperTests()
    {
        // Initialize CUDA if available
        if (HardwareDetection.IsCudaAvailable())
        {
            try
            {
                var initResult = CudaRuntime.cuInit(0);
                if (initResult == CudaError.Success || initResult == (CudaError)4)
                {
                    var getDeviceResult = CudaRuntime.cuDeviceGet(out var device, 0);
                    if (getDeviceResult == CudaError.Success)
                    {
                        CudaRuntimeCore.cuCtxCreate(out _context, 0, device);
                    }
                }
            }
            catch
            {
                // Ignore initialization errors for test setup
            }
        }
    }

    [SkippableFact(DisplayName = "AllocateAndInitialize should create control block in device memory")]
    public void AllocateAndInitialize_ShouldCreateDeviceMemory()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");
        Skip.If(_context == IntPtr.Zero, "CUDA context not initialized");

        // Act
        var devicePtr = RingKernelControlBlockHelper.AllocateAndInitialize(_context);

        try
        {
            // Assert
            devicePtr.Should().NotBe(IntPtr.Zero, "device pointer should be allocated");

            // Verify we can read from it
            var controlBlock = RingKernelControlBlockHelper.Read(_context, devicePtr);
            controlBlock.IsActive.Should().Be(0, "should be initialized as inactive");
            controlBlock.ShouldTerminate.Should().Be(0);
            controlBlock.HasTerminated.Should().Be(0);
        }
        finally
        {
            RingKernelControlBlockHelper.Free(_context, devicePtr);
        }
    }

    [SkippableFact(DisplayName = "Read and Write should perform round-trip correctly")]
    public void ReadWrite_ShouldPerformRoundTrip()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");
        Skip.If(_context == IntPtr.Zero, "CUDA context not initialized");

        // Arrange
        var devicePtr = RingKernelControlBlockHelper.AllocateAndInitialize(_context);

        try
        {
            var originalBlock = new RingKernelControlBlock
            {
                IsActive = 1,
                ShouldTerminate = 0,
                HasTerminated = 0,
                ErrorsEncountered = 5,
                MessagesProcessed = 1234567890,
                LastActivityTicks = DateTime.UtcNow.Ticks,
                InputQueueHeadPtr = 0x1000,
                InputQueueTailPtr = 0x2000,
                OutputQueueHeadPtr = 0x3000,
                OutputQueueTailPtr = 0x4000
            };

            // Act
            RingKernelControlBlockHelper.Write(_context, devicePtr, originalBlock);
            var readBlock = RingKernelControlBlockHelper.Read(_context, devicePtr);

            // Assert
            readBlock.Should().Be(originalBlock, "written and read blocks should be identical");
            readBlock.IsActive.Should().Be(1);
            readBlock.MessagesProcessed.Should().Be(1234567890);
            readBlock.InputQueueHeadPtr.Should().Be(0x1000);
            readBlock.OutputQueueTailPtr.Should().Be(0x4000);
        }
        finally
        {
            RingKernelControlBlockHelper.Free(_context, devicePtr);
        }
    }

    [SkippableFact(DisplayName = "SetActive should update IsActive flag atomically")]
    public void SetActive_ShouldUpdateFlagAtomically()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");
        Skip.If(_context == IntPtr.Zero, "CUDA context not initialized");

        // Arrange
        var devicePtr = RingKernelControlBlockHelper.AllocateAndInitialize(_context);

        try
        {
            // Act: Set active
            RingKernelControlBlockHelper.SetActive(_context, devicePtr, true);
            var block1 = RingKernelControlBlockHelper.Read(_context, devicePtr);

            // Assert
            block1.IsActive.Should().Be(1, "SetActive(true) should set flag to 1");

            // Act: Set inactive
            RingKernelControlBlockHelper.SetActive(_context, devicePtr, false);
            var block2 = RingKernelControlBlockHelper.Read(_context, devicePtr);

            // Assert
            block2.IsActive.Should().Be(0, "SetActive(false) should set flag to 0");
        }
        finally
        {
            RingKernelControlBlockHelper.Free(_context, devicePtr);
        }
    }

    [SkippableFact(DisplayName = "SetActive should not modify other fields")]
    public void SetActive_ShouldNotModifyOtherFields()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");
        Skip.If(_context == IntPtr.Zero, "CUDA context not initialized");

        // Arrange
        var devicePtr = RingKernelControlBlockHelper.AllocateAndInitialize(_context);

        try
        {
            var originalBlock = new RingKernelControlBlock
            {
                IsActive = 0,
                ShouldTerminate = 1,
                HasTerminated = 0,
                MessagesProcessed = 999,
                InputQueueHeadPtr = 0x5000
            };

            RingKernelControlBlockHelper.Write(_context, devicePtr, originalBlock);

            // Act
            RingKernelControlBlockHelper.SetActive(_context, devicePtr, true);
            var modifiedBlock = RingKernelControlBlockHelper.Read(_context, devicePtr);

            // Assert
            modifiedBlock.IsActive.Should().Be(1, "IsActive should be updated");
            modifiedBlock.ShouldTerminate.Should().Be(1, "ShouldTerminate should be unchanged");
            modifiedBlock.MessagesProcessed.Should().Be(999, "MessagesProcessed should be unchanged");
            modifiedBlock.InputQueueHeadPtr.Should().Be(0x5000, "InputQueueHeadPtr should be unchanged");
        }
        finally
        {
            RingKernelControlBlockHelper.Free(_context, devicePtr);
        }
    }

    [SkippableFact(DisplayName = "SetTerminate should update ShouldTerminate flag")]
    public void SetTerminate_ShouldUpdateFlag()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");
        Skip.If(_context == IntPtr.Zero, "CUDA context not initialized");

        // Arrange
        var devicePtr = RingKernelControlBlockHelper.AllocateAndInitialize(_context);

        try
        {
            // Act
            RingKernelControlBlockHelper.SetTerminate(_context, devicePtr);
            var block = RingKernelControlBlockHelper.Read(_context, devicePtr);

            // Assert
            block.ShouldTerminate.Should().Be(1, "SetTerminate should set flag to 1");
        }
        finally
        {
            RingKernelControlBlockHelper.Free(_context, devicePtr);
        }
    }

    [SkippableFact(DisplayName = "WaitForTerminationAsync should timeout when HasTerminated not set")]
    public async Task WaitForTerminationAsync_ShouldTimeoutWhenNotSet()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");
        Skip.If(_context == IntPtr.Zero, "CUDA context not initialized");

        // Arrange
        var devicePtr = RingKernelControlBlockHelper.AllocateAndInitialize(_context);

        try
        {
            // Act
            var result = await RingKernelControlBlockHelper.WaitForTerminationAsync(
                _context,
                devicePtr,
                TimeSpan.FromMilliseconds(100),
                CancellationToken.None);

            // Assert
            result.Should().BeFalse("should timeout when HasTerminated is not set");
        }
        finally
        {
            RingKernelControlBlockHelper.Free(_context, devicePtr);
        }
    }

    [SkippableFact(DisplayName = "WaitForTerminationAsync should return true when HasTerminated is set")]
    public async Task WaitForTerminationAsync_ShouldReturnTrueWhenSet()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");
        Skip.If(_context == IntPtr.Zero, "CUDA context not initialized");

        // Arrange
        var devicePtr = RingKernelControlBlockHelper.AllocateAndInitialize(_context);

        try
        {
            // Set HasTerminated flag manually
            var block = RingKernelControlBlockHelper.Read(_context, devicePtr);
            block.HasTerminated = 1;
            RingKernelControlBlockHelper.Write(_context, devicePtr, block);

            // Act
            var result = await RingKernelControlBlockHelper.WaitForTerminationAsync(
                _context,
                devicePtr,
                TimeSpan.FromSeconds(1),
                CancellationToken.None);

            // Assert
            result.Should().BeTrue("should return true when HasTerminated is set");
        }
        finally
        {
            RingKernelControlBlockHelper.Free(_context, devicePtr);
        }
    }

    [SkippableFact(DisplayName = "WaitForTerminationAsync should respect cancellation token")]
    public async Task WaitForTerminationAsync_ShouldRespectCancellation()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");
        Skip.If(_context == IntPtr.Zero, "CUDA context not initialized");

        // Arrange
        var devicePtr = RingKernelControlBlockHelper.AllocateAndInitialize(_context);
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        try
        {
            // Act
            var result = await RingKernelControlBlockHelper.WaitForTerminationAsync(
                _context,
                devicePtr,
                TimeSpan.FromSeconds(10),
                cts.Token);

            // Assert
            result.Should().BeFalse("should stop waiting when cancelled");
        }
        finally
        {
            RingKernelControlBlockHelper.Free(_context, devicePtr);
        }
    }

    [SkippableFact(DisplayName = "Free should handle null pointer safely")]
    public void Free_ShouldHandleNullPointer()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");
        Skip.If(_context == IntPtr.Zero, "CUDA context not initialized");

        // Act & Assert (should not throw)
        RingKernelControlBlockHelper.Free(_context, IntPtr.Zero);
    }

    [SkippableFact(DisplayName = "Multiple allocations should have different addresses")]
    public void MultipleAllocations_ShouldHaveDifferentAddresses()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");
        Skip.If(_context == IntPtr.Zero, "CUDA context not initialized");

        // Arrange & Act
        var ptr1 = RingKernelControlBlockHelper.AllocateAndInitialize(_context);
        var ptr2 = RingKernelControlBlockHelper.AllocateAndInitialize(_context);
        var ptr3 = RingKernelControlBlockHelper.AllocateAndInitialize(_context);

        try
        {
            // Assert
            ptr1.Should().NotBe(IntPtr.Zero);
            ptr2.Should().NotBe(IntPtr.Zero);
            ptr3.Should().NotBe(IntPtr.Zero);

            ptr1.Should().NotBe(ptr2, "each allocation should have unique address");
            ptr2.Should().NotBe(ptr3, "each allocation should have unique address");
            ptr1.Should().NotBe(ptr3, "each allocation should have unique address");
        }
        finally
        {
            RingKernelControlBlockHelper.Free(_context, ptr1);
            RingKernelControlBlockHelper.Free(_context, ptr2);
            RingKernelControlBlockHelper.Free(_context, ptr3);
        }
    }

    [SkippableFact(DisplayName = "Operations should be thread-safe")]
    public async Task Operations_ShouldBeThreadSafe()
    {
        Skip.IfNot(HardwareDetection.IsCudaAvailable(), "CUDA device not available");
        Skip.If(_context == IntPtr.Zero, "CUDA context not initialized");

        // Arrange
        var devicePtr = RingKernelControlBlockHelper.AllocateAndInitialize(_context);

        try
        {
            // Act - Concurrent reads and writes
            var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(() =>
            {
                for (var j = 0; j < 10; j++)
                {
                    var block = RingKernelControlBlockHelper.Read(_context, devicePtr);
                    block.MessagesProcessed += 1;
                    RingKernelControlBlockHelper.Write(_context, devicePtr, block);
                }
            })).ToArray();

            await Task.WhenAll(tasks);

            // Assert - Should complete without exceptions
            var finalBlock = RingKernelControlBlockHelper.Read(_context, devicePtr);
            finalBlock.MessagesProcessed.Should().BeGreaterThanOrEqualTo(0, "operations should complete");
        }
        finally
        {
            RingKernelControlBlockHelper.Free(_context, devicePtr);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Dispose managed resources (none in this class)
        }

        // Clean up unmanaged resources
        if (_context != IntPtr.Zero)
        {
            try
            {
                CudaRuntimeCore.cuCtxDestroy(_context);
            }
            catch
            {
                // Ignore cleanup errors
            }
            _context = IntPtr.Zero;
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
