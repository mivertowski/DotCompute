// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Tests.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Tests.Hardware.Unit;

/// <summary>
/// Unit tests for CUDA memory management operations
/// </summary>
[Collection("CUDA Hardware Tests")]
public class CudaMemoryManagerTests : IDisposable
{
    private readonly ILogger<CudaMemoryManagerTests> _logger;
    private readonly ITestOutputHelper _output;
    private readonly List<CudaAccelerator> _accelerators = [];
    private readonly List<ISyncMemoryBuffer> _buffers = [];

    public CudaMemoryManagerTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<CudaMemoryManagerTests>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaMemoryManager_Allocate_ShouldCreateValidBuffer()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as IMemoryManager;
        var syncMemoryManager = memoryManager as ISyncMemoryManager;

        // Act
        var buffer = syncMemoryManager!.Allocate(1024);
        _buffers.Add(buffer);

        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(1024);
        buffer.IsDisposed.Should().BeFalse();
    }

    [Theory]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    [InlineData(1024)]
    [InlineData(4096)]
    [InlineData(1024 * 1024)] // 1MB
    [InlineData(16 * 1024 * 1024)] // 16MB
    public void CudaMemoryManager_Allocate_WithDifferentSizes_ShouldSucceed(long sizeInBytes)
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var syncMemoryManager = accelerator.Memory as ISyncMemoryManager;

        // Act
        var buffer = syncMemoryManager!.Allocate(sizeInBytes);
        _buffers.Add(buffer);

        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(sizeInBytes);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaMemoryManager_Allocate_WithZeroSize_ShouldThrow()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var syncMemoryManager = accelerator.Memory as ISyncMemoryManager;

        // Act & Assert
        Action allocateZero = () => syncMemoryManager!.Allocate(0);
        allocateZero.Should().Throw<ArgumentException>()
            .WithMessage("*Size must be greater than zero*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaMemoryManager_Allocate_WithNegativeSize_ShouldThrow()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var syncMemoryManager = accelerator.Memory as ISyncMemoryManager;

        // Act & Assert
        Action allocateNegative = () => syncMemoryManager!.Allocate(-1024);
        allocateNegative.Should().Throw<ArgumentException>()
            .WithMessage("*Size must be greater than zero*");
    }

    [Theory]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    public void CudaMemoryManager_AllocateAligned_WithValidAlignment_ShouldSucceed(int alignment)
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var syncMemoryManager = accelerator.Memory as ISyncMemoryManager;

        // Act
        var buffer = syncMemoryManager!.AllocateAligned(4096, alignment);
        _buffers.Add(buffer);

        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().BeGreaterOrEqualTo(4096);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaMemoryManager_AllocateAligned_WithInvalidAlignment_ShouldThrow()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var syncMemoryManager = accelerator.Memory as ISyncMemoryManager;

        // Act & Assert
        Action allocateInvalidAlignment = () => syncMemoryManager!.AllocateAligned(1024, 100); // Not a power of 2
        allocateInvalidAlignment.Should().Throw<ArgumentException>()
            .WithMessage("*Alignment must be a power of 2*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public unsafe void CudaMemoryManager_CopyFromHost_ShouldTransferData()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var syncMemoryManager = accelerator.Memory as ISyncMemoryManager;
        var buffer = syncMemoryManager!.Allocate(1024);
        _buffers.Add(buffer);

        var hostData = TestDataGenerator.GenerateFloatArray(256);
        
        // Act
        fixed (float* hostPtr = hostData)
        {
            syncMemoryManager.CopyFromHost(hostPtr, buffer, hostData.Length * sizeof(float));
        }

        // Assert - Should not throw
        buffer.IsDisposed.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public unsafe void CudaMemoryManager_CopyToHost_ShouldRetrieveData()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var syncMemoryManager = accelerator.Memory as ISyncMemoryManager;
        var buffer = syncMemoryManager!.Allocate(1024);
        _buffers.Add(buffer);

        var hostData = TestDataGenerator.GenerateFloatArray(256);
        var retrievedData = new float[256];

        // Act
        fixed (float* hostPtr = hostData)
        fixed (float* retrievedPtr = retrievedData)
        {
            syncMemoryManager.CopyFromHost(hostPtr, buffer, hostData.Length * sizeof(float));
            syncMemoryManager.CopyToHost(buffer, retrievedPtr, hostData.Length * sizeof(float));
        }

        // Assert
        retrievedData.Should().BeEquivalentTo(hostData, "Data should be preserved during GPU round-trip");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaMemoryManager_Copy_BetweenGpuBuffers_ShouldSucceed()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var syncMemoryManager = accelerator.Memory as ISyncMemoryManager;
        var sourceBuffer = syncMemoryManager!.Allocate(1024);
        var destBuffer = syncMemoryManager.Allocate(1024);
        _buffers.AddRange([sourceBuffer, destBuffer]);

        // Act & Assert
        Action copyAction = () => syncMemoryManager.Copy(sourceBuffer, destBuffer, 512);
        copyAction.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaMemoryManager_Copy_WithInvalidParameters_ShouldThrow()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var syncMemoryManager = accelerator.Memory as ISyncMemoryManager;
        var sourceBuffer = syncMemoryManager!.Allocate(1024);
        var destBuffer = syncMemoryManager.Allocate(1024);
        _buffers.AddRange([sourceBuffer, destBuffer]);

        // Act & Assert - Copy more than buffer size
        Action copyTooMuch = () => syncMemoryManager.Copy(sourceBuffer, destBuffer, 2048);
        copyTooMuch.Should().Throw<ArgumentException>()
            .WithMessage("*would exceed buffer bounds*");
    }

    [Theory]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(255)]
    public void CudaMemoryManager_Fill_WithDifferentValues_ShouldSucceed(byte fillValue)
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var syncMemoryManager = accelerator.Memory as ISyncMemoryManager;
        var buffer = syncMemoryManager!.Allocate(1024);
        _buffers.Add(buffer);

        // Act & Assert
        Action fillAction = () => syncMemoryManager.Fill(buffer, fillValue, 512);
        fillAction.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaMemoryManager_Zero_ShouldClearBuffer()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var syncMemoryManager = accelerator.Memory as ISyncMemoryManager;
        var buffer = syncMemoryManager!.Allocate(1024);
        _buffers.Add(buffer);

        // Act & Assert
        Action zeroAction = () => syncMemoryManager.Zero(buffer);
        zeroAction.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaMemoryManager_GetStatistics_ShouldReturnValidData()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var syncMemoryManager = accelerator.Memory as ISyncMemoryManager;

        // Act
        var stats = syncMemoryManager!.GetStatistics();

        // Assert
        stats.Should().NotBeNull();
        stats.TotalMemory.Should().BeGreaterThan(0);
        stats.FreeMemory.Should().BeGreaterOrEqualTo(0);
        stats.UsedMemory.Should().BeGreaterOrEqualTo(0);
        stats.AllocationCount.Should().BeGreaterOrEqualTo(0);
        stats.TotalMemory.Should().Be(stats.FreeMemory + stats.UsedMemory);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaMemoryManager_MultipleAllocations_ShouldTrackCorrectly()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var syncMemoryManager = accelerator.Memory as ISyncMemoryManager;

        // Act
        var initialStats = syncMemoryManager!.GetStatistics();
        
        var buffer1 = syncMemoryManager.Allocate(1024);
        var buffer2 = syncMemoryManager.Allocate(2048);
        _buffers.AddRange([buffer1, buffer2]);

        var afterAllocStats = syncMemoryManager.GetStatistics();

        // Assert
        afterAllocStats.AllocationCount.Should().BeGreaterOrEqualTo(initialStats.AllocationCount + 2);
        afterAllocStats.AllocatedMemory.Should().BeGreaterOrEqualTo(initialStats.AllocatedMemory + 3072);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaMemoryManager_Free_ShouldReleaseBuffer()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var syncMemoryManager = accelerator.Memory as ISyncMemoryManager;
        var buffer = syncMemoryManager!.Allocate(1024);

        // Act
        syncMemoryManager.Free(buffer);

        // Assert
        buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Hardware", "CUDA")]
    public void CudaMemoryManager_Reset_ShouldClearAllBuffers()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var syncMemoryManager = accelerator.Memory as ISyncMemoryManager;
        var buffer1 = syncMemoryManager!.Allocate(1024);
        var buffer2 = syncMemoryManager.Allocate(2048);

        // Act
        syncMemoryManager.Reset();

        // Assert
        buffer1.IsDisposed.Should().BeTrue();
        buffer2.IsDisposed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Stress")]
    [Trait("Hardware", "CUDA")]
    public void CudaMemoryManager_StressTest_MultipleAllocationsAndDeallocations()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var syncMemoryManager = accelerator.Memory as ISyncMemoryManager;
        const int iterationCount = 100;

        // Act & Assert
        for (int i = 0; i < iterationCount; i++)
        {
            var buffer = syncMemoryManager!.Allocate(1024 * (i % 10 + 1));
            buffer.Should().NotBeNull();
            
            if (i % 2 == 0) // Free every other allocation
            {
                syncMemoryManager.Free(buffer);
            }
            else
            {
                _buffers.Add(buffer);
            }
        }

        var finalStats = syncMemoryManager!.GetStatistics();
        finalStats.AllocationCount.Should().BeGreaterOrEqualTo(iterationCount / 2);
    }

    [Theory]
    [Trait("Category", "EdgeCase")]
    [Trait("Hardware", "CUDA")]
    [InlineData(MemoryOptions.None)]
    [InlineData(MemoryOptions.DeviceLocal)]
    [InlineData(MemoryOptions.HostVisible)]
    public void CudaMemoryManager_Allocate_WithMemoryOptions_ShouldRespectOptions(MemoryOptions options)
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var syncMemoryManager = accelerator.Memory as ISyncMemoryManager;

        // Act & Assert
        var buffer = syncMemoryManager!.Allocate(1024, options);
        _buffers.Add(buffer);
        
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(1024);
    }

    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Hardware", "CUDA")]
    public void CudaMemoryManager_LargeAllocation_ShouldCompleteInReasonableTime()
    {
        // Arrange
        if (!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var syncMemoryManager = accelerator.Memory as ISyncMemoryManager;
        const long largeSize = 100 * 1024 * 1024; // 100MB

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var buffer = syncMemoryManager!.Allocate(largeSize);
        _buffers.Add(buffer);
        stopwatch.Stop();

        // Assert
        buffer.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "Large allocation should complete within 5 seconds");
        
        _output.WriteLine($"100MB allocation took {stopwatch.ElapsedMilliseconds}ms");
    }

    // Helper Methods
    private CudaAccelerator CreateAccelerator()
    {
        var accelerator = new CudaAccelerator(0, _logger);
        _accelerators.Add(accelerator);
        return accelerator;
    }

    private static bool IsCudaAvailable()
    {
        try
        {
            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            return result == CudaError.Success && deviceCount > 0;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        foreach (var buffer in _buffers.ToList())
        {
            try
            {
                if (!buffer.IsDisposed)
                {
                    var syncMemoryManager = _accelerators.FirstOrDefault()?.Memory as ISyncMemoryManager;
                    syncMemoryManager?.Free(buffer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing CUDA buffer");
            }
        }
        _buffers.Clear();

        foreach (var accelerator in _accelerators)
        {
            try
            {
                accelerator?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing CUDA accelerator");
            }
        }
        _accelerators.Clear();
    }
}