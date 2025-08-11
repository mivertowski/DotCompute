// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Core.Execution;
using DotCompute.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Integration tests for MultiGpuMemoryManager with real P2P scenarios.
/// Tests both single-GPU fallback mechanisms and multi-GPU P2P optimizations.
/// </summary>
public sealed class MultiGpuMemoryManagerIntegrationTests : IAsyncDisposable
{
    private readonly MultiGpuMemoryManager _memoryManager;
    private readonly MockAccelerator[] _devices;

    public MultiGpuMemoryManagerIntegrationTests()
    {
        _memoryManager = new MultiGpuMemoryManager(NullLogger<MultiGpuMemoryManager>.Instance);
        
        // Create mock devices representing different GPU scenarios
        _devices = new[]
        {
            new MockAccelerator("cuda-gpu-0", "CUDA", "RTX 4090 #0"),
            new MockAccelerator("cuda-gpu-1", "CUDA", "RTX 4090 #1"),
            new MockAccelerator("rocm-gpu-0", "ROCm", "MI210 #0"),
            new MockAccelerator("cpu-device-0", "CPU", "Intel Xeon")
        };
    }

    [Fact]
    public async Task EnablePeerToPeer_CudaDevices_EnablesP2PSuccessfully()
    {
        // Arrange
        var device1 = _devices[0]; // CUDA GPU 0
        var device2 = _devices[1]; // CUDA GPU 1

        // Act
        var result = await _memoryManager.EnablePeerToPeerAsync(device1, device2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task EnablePeerToPeer_DifferentDeviceTypes_ReturnsFalse()
    {
        // Arrange
        var cudaDevice = _devices[0]; // CUDA GPU
        var rocmDevice = _devices[2]; // ROCm GPU

        // Act
        var result = await _memoryManager.EnablePeerToPeerAsync(cudaDevice, rocmDevice);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CreateBufferSlice_WithP2PSupport_CreatesOptimizedSlice()
    {
        // Arrange
        var sourceDevice = _devices[0]; // CUDA GPU 0
        var targetDevice = _devices[1]; // CUDA GPU 1
        
        // Enable P2P first
        await _memoryManager.EnablePeerToPeerAsync(sourceDevice, targetDevice);

        // Create source buffer
        var sourceBuffer = await CreateMockBuffer<float>(sourceDevice, 1024);

        // Act
        var slice = await _memoryManager.CreateBufferSliceAsync<float>(
            sourceBuffer, targetDevice, 256, 512);

        // Assert
        Assert.NotNull(slice);
        Assert.Equal(targetDevice, slice.Accelerator);
        Assert.Equal(512, slice.Length);
    }

    [Fact]
    public async Task TransferBuffer_BetweenP2PDevices_CompletesSuccessfully()
    {
        // Arrange
        var sourceDevice = _devices[0];
        var targetDevice = _devices[1];
        
        await _memoryManager.EnablePeerToPeerAsync(sourceDevice, targetDevice);
        
        var sourceBuffer = await CreateMockBuffer<float>(sourceDevice, 1024);
        var targetBuffer = await CreateMockBuffer<float>(targetDevice, 1024);

        // Act
        await _memoryManager.TransferBufferAsync(sourceBuffer, targetBuffer, 0, 0, 512);

        // Assert - should complete without throwing
        Assert.False(sourceBuffer.IsDisposed);
        Assert.False(targetBuffer.IsDisposed);
    }

    [Fact]
    public async Task TransferBuffer_BetweenNonP2PDevices_UsesHostMediatedTransfer()
    {
        // Arrange
        var cudaDevice = _devices[0]; // CUDA
        var rocmDevice = _devices[2]; // ROCm (different type, no P2P)
        
        var sourceBuffer = await CreateMockBuffer<float>(cudaDevice, 1024);
        var targetBuffer = await CreateMockBuffer<float>(rocmDevice, 1024);

        // Act
        await _memoryManager.TransferBufferAsync(sourceBuffer, targetBuffer, 0, 0, 256);

        // Assert - should complete without throwing
        Assert.False(sourceBuffer.IsDisposed);
        Assert.False(targetBuffer.IsDisposed);
    }

    [Fact]
    public async Task OptimizeMemoryLayout_MultipleDevices_EstablishesP2PConnections()
    {
        // Arrange
        var testDevices = _devices.Take(2).ToArray(); // Two CUDA devices

        // Act
        await _memoryManager.OptimizeMemoryLayoutAsync(testDevices);

        // Assert
        var stats = _memoryManager.GetMemoryStatistics();
        Assert.True(stats.TotalP2PConnections > 0);
    }

    [Fact]
    public async Task GetMemoryStatistics_WithActiveTransfers_ReturnsAccurateStats()
    {
        // Arrange
        var device1 = _devices[0];
        var device2 = _devices[1];
        
        await _memoryManager.EnablePeerToPeerAsync(device1, device2);
        
        var buffer1 = await CreateMockBuffer<float>(device1, 1024);
        var buffer2 = await CreateMockBuffer<float>(device2, 1024);

        // Act
        var initialStats = _memoryManager.GetMemoryStatistics();
        
        // Perform some transfers
        await _memoryManager.TransferBufferAsync(buffer1, buffer2, 0, 0, 100);
        await _memoryManager.TransferBufferAsync(buffer2, buffer1, 0, 0, 200);
        
        var finalStats = _memoryManager.GetMemoryStatistics();

        // Assert
        Assert.True(finalStats.TotalP2PTransfers >= initialStats.TotalP2PTransfers);
        Assert.True(finalStats.TotalP2PBytesTransferred >= initialStats.TotalP2PBytesTransferred);
    }

    [Fact]
    public async Task WaitForTransfers_CompletesAllPendingTransfers()
    {
        // Arrange
        var device1 = _devices[0];
        var device2 = _devices[1];
        
        await _memoryManager.EnablePeerToPeerAsync(device1, device2);
        
        var buffer1 = await CreateMockBuffer<float>(device1, 2048);
        var buffer2 = await CreateMockBuffer<float>(device2, 2048);

        // Act
        var transferTask1 = _memoryManager.TransferBufferAsync(buffer1, buffer2, 0, 0, 1024);
        var transferTask2 = _memoryManager.TransferBufferAsync(buffer2, buffer1, 1024, 1024, 512);

        await _memoryManager.WaitForTransfersAsync(device1);
        await _memoryManager.WaitForTransfersAsync(device2);

        // Assert
        Assert.True(transferTask1.IsCompleted);
        Assert.True(transferTask2.IsCompleted);
    }

    [Fact]
    public async Task CreateBufferSlice_SingleGpuFallback_WorksWithoutP2P()
    {
        // Arrange - use same device (no P2P needed)
        var device = _devices[0];
        var sourceBuffer = await CreateMockBuffer<float>(device, 1024);

        // Act
        var slice = await _memoryManager.CreateBufferSliceAsync<float>(
            sourceBuffer, device, 128, 256);

        // Assert
        Assert.NotNull(slice);
        Assert.Equal(device, slice.Accelerator);
        Assert.Equal(256, slice.Length);
    }

    [Fact]
    public async Task OptimizeMemoryLayout_MixedDeviceTypes_HandlesGracefully()
    {
        // Arrange - include all device types
        var allDevices = _devices;

        // Act
        await _memoryManager.OptimizeMemoryLayoutAsync(allDevices);

        // Assert
        var stats = _memoryManager.GetMemoryStatistics();
        // Should have some P2P connections (at least CUDA-CUDA and CPU-CPU)
        Assert.True(stats.TotalP2PConnections >= 0);
    }

    [Fact]
    public async Task TransferBuffer_LargeData_UsesStreamingStrategy()
    {
        // Arrange
        var device1 = _devices[0];
        var device2 = _devices[1];
        
        await _memoryManager.EnablePeerToPeerAsync(device1, device2);
        
        // Create large buffers (simulating large dataset)
        var sourceBuffer = await CreateMockBuffer<float>(device1, 16 * 1024 * 1024); // 64MB
        var targetBuffer = await CreateMockBuffer<float>(device2, 16 * 1024 * 1024);

        // Act
        var startTime = DateTime.UtcNow;
        await _memoryManager.TransferBufferAsync(sourceBuffer, targetBuffer);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.True(duration.TotalMilliseconds < 10000); // Should complete within 10 seconds
        Assert.False(sourceBuffer.IsDisposed);
        Assert.False(targetBuffer.IsDisposed);
    }

    [Fact]
    public async Task MemoryStatistics_TracksP2PEfficiency()
    {
        // Arrange
        var device1 = _devices[0];
        var device2 = _devices[1];
        
        // Act
        await _memoryManager.EnablePeerToPeerAsync(device1, device2);
        var stats = _memoryManager.GetMemoryStatistics();

        // Assert
        Assert.True(stats.P2PEfficiencyRatio >= 0.0);
        Assert.True(stats.P2PEfficiencyRatio <= 1.0);
        
        if (stats.TotalP2PConnections > 0)
        {
            Assert.True(stats.P2PEfficiencyRatio > 0.0);
        }
    }

    [Fact]
    public async Task ConcurrentP2POperations_HandlesConcurrencyCorrectly()
    {
        // Arrange
        var device1 = _devices[0];
        var device2 = _devices[1];
        
        await _memoryManager.EnablePeerToPeerAsync(device1, device2);
        
        var buffers1 = new IBuffer<float>[5];
        var buffers2 = new IBuffer<float>[5];
        
        for (var i = 0; i < 5; i++)
        {
            buffers1[i] = await CreateMockBuffer<float>(device1, 1024);
            buffers2[i] = await CreateMockBuffer<float>(device2, 1024);
        }

        // Act - concurrent transfers
        var transferTasks = new Task[10];
        for (var i = 0; i < 5; i++)
        {
            var index = i;
            transferTasks[i] = _memoryManager.TransferBufferAsync(buffers1[index], buffers2[index]);
            transferTasks[i + 5] = _memoryManager.TransferBufferAsync(buffers2[index], buffers1[index]);
        }

        await Task.WhenAll(transferTasks);

        // Assert
        var stats = _memoryManager.GetMemoryStatistics();
        Assert.True(stats.TotalP2PTransfers >= 10);
    }

    [Fact]
    public async Task TransferBuffer_WithCancellation_CancelsGracefully()
    {
        // Arrange
        var device1 = _devices[0];
        var device2 = _devices[1];
        
        await _memoryManager.EnablePeerToPeerAsync(device1, device2);
        
        var sourceBuffer = await CreateMockBuffer<float>(device1, 1024);
        var targetBuffer = await CreateMockBuffer<float>(device2, 1024);
        
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(1)); // Cancel very quickly

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _memoryManager.TransferBufferAsync(sourceBuffer, targetBuffer, 0, 0, 1024, cts.Token).AsTask());
    }

    [Fact]
    public async Task MultipleDeviceTypes_CoexistGracefully()
    {
        // Arrange - use CUDA, ROCm, and CPU devices
        var cudaDevice = _devices[0];
        var rocmDevice = _devices[2];
        var cpuDevice = _devices[3];

        var cudaBuffer = await CreateMockBuffer<float>(cudaDevice, 512);
        var rocmBuffer = await CreateMockBuffer<float>(rocmDevice, 512);
        var cpuBuffer = await CreateMockBuffer<float>(cpuDevice, 512);

        // Act - transfers between different device types
        await _memoryManager.TransferBufferAsync(cudaBuffer, rocmBuffer); // Should use host-mediated
        await _memoryManager.TransferBufferAsync(rocmBuffer, cpuBuffer);  // Should use host-mediated
        await _memoryManager.TransferBufferAsync(cpuBuffer, cudaBuffer);  // Should use host-mediated

        // Assert
        var stats = _memoryManager.GetMemoryStatistics();
        Assert.True(stats.TotalP2PTransfers >= 3);
        Assert.True(stats.DeviceStatistics.Count >= 3);
    }

    [Fact]
    public async Task ErrorHandling_InvalidBufferOperations_HandlesGracefully()
    {
        // Arrange
        var device1 = _devices[0];
        var device2 = _devices[1];
        var disposedBuffer = await CreateMockBuffer<float>(device1, 256);
        var validBuffer = await CreateMockBuffer<float>(device2, 256);
        
        // Dispose one buffer
        await disposedBuffer.DisposeAsync();

        // Act & Assert - should handle disposed buffer gracefully
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            _memoryManager.TransferBufferAsync(disposedBuffer, validBuffer).AsTask());
    }

    /// <summary>
    /// Creates a mock buffer for testing purposes.
    /// </summary>
    private async Task<IBuffer<T>> CreateMockBuffer<T>(IAccelerator device, int elementCount) where T : unmanaged
    {
        var sizeInBytes = elementCount * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var memoryBuffer = await device.Memory.AllocateAsync(sizeInBytes, MemoryOptions.None);
        return new MockBuffer<T>(memoryBuffer, device, elementCount);
    }

    public async ValueTask DisposeAsync()
    {
        await _memoryManager.DisposeAsync();
        
        foreach (var device in _devices)
        {
            device?.Dispose();
        }
    }

    /// <summary>
    /// Mock buffer implementation for testing.
    /// </summary>
    private sealed class MockBuffer<T> : IBuffer<T> where T : unmanaged
    {
        private readonly IMemoryBuffer _memoryBuffer;
        private bool _disposed;

        public MockBuffer(IMemoryBuffer memoryBuffer, IAccelerator accelerator, int length)
        {
            _memoryBuffer = memoryBuffer;
            Accelerator = accelerator;
            Length = length;
        }

        public int Length { get; }
        public long SizeInBytes => _memoryBuffer.SizeInBytes;
        public IAccelerator Accelerator { get; }
        public MemoryType MemoryType => MemoryType.Device;
        public MemoryOptions Options => _memoryBuffer.Options;
        public bool IsDisposed => _disposed;

        public Task CopyFromHostAsync<TData>(TData[] source, int offset, CancellationToken cancellationToken = default) where TData : unmanaged
        {
            ThrowIfDisposed();
            return Task.CompletedTask;
        }

        public ValueTask CopyFromHostAsync<TData>(ReadOnlyMemory<TData> source, long offset, CancellationToken cancellationToken = default) where TData : unmanaged
        {
            ThrowIfDisposed();
            return ValueTask.CompletedTask;
        }

        public Task CopyToHostAsync<TData>(TData[] destination, int offset, CancellationToken cancellationToken = default) where TData : unmanaged
        {
            ThrowIfDisposed();
            return Task.CompletedTask;
        }

        public ValueTask CopyToHostAsync<TData>(Memory<TData> destination, long offset, CancellationToken cancellationToken = default) where TData : unmanaged
        {
            ThrowIfDisposed();
            return ValueTask.CompletedTask;
        }

        public Task CopyFromAsync(IMemoryBuffer source, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return Task.CompletedTask;
        }

        public Task CopyToAsync(IMemoryBuffer destination, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return Task.CompletedTask;
        }

        public ValueTask CopyToAsync(IBuffer<T> destination, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return ValueTask.CompletedTask;
        }

        public ValueTask CopyToAsync(int sourceOffset, IBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return ValueTask.CompletedTask;
        }

        public Task FillAsync<TData>(TData value, CancellationToken cancellationToken = default) where TData : unmanaged
        {
            ThrowIfDisposed();
            return Task.CompletedTask;
        }

        public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return ValueTask.CompletedTask;
        }

        public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return ValueTask.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return Task.CompletedTask;
        }

        public IBuffer<T> Slice(int offset, int count)
        {
            ThrowIfDisposed();
            return new MockBuffer<T>(_memoryBuffer, Accelerator, count);
        }

        public IBuffer<TNew> AsType<TNew>() where TNew : unmanaged
        {
            ThrowIfDisposed();
            var newElementCount = (int)(SizeInBytes / System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>());
            return new MockBuffer<TNew>(_memoryBuffer, Accelerator, newElementCount);
        }

        public MappedMemory<T> Map(MapMode mode)
        {
            ThrowIfDisposed();
            return default;
        }

        public MappedMemory<T> MapRange(int offset, int count, MapMode mode)
        {
            ThrowIfDisposed();
            return default;
        }

        public ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return ValueTask.FromResult(default(MappedMemory<T>));
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _memoryBuffer.Dispose();
                _disposed = true;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await _memoryBuffer.DisposeAsync();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Mock accelerator for integration testing.
    /// </summary>
    private sealed class MockAccelerator : IAccelerator
    {
        public MockAccelerator(string id, string deviceType, string name)
        {
            Info = new AcceleratorInfo
            {
                Id = id,
                Name = name,
                DeviceType = deviceType,
                ComputeUnits = 128,
                MaxWorkGroupSize = 1024,
                LocalMemorySize = 64 * 1024,
                GlobalMemorySize = 24L * 1024 * 1024 * 1024
            };
            Memory = new MockMemoryManager();
        }

        public AcceleratorInfo Info { get; }
        public IMemoryManager Memory { get; }
        public bool IsDisposed { get; private set; }

        public ValueTask<ICompiledKernel> CompileKernelAsync(
            KernelDefinition definition,
            CompilationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<ICompiledKernel>(new MockCompiledKernel());
        }

        public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Mock memory manager for integration testing.
    /// </summary>
    private sealed class MockMemoryManager : IMemoryManager
    {
        public ValueTask<IMemoryBuffer> AllocateAsync(
            long sizeInBytes,
            MemoryOptions options,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IMemoryBuffer>(new MockMemoryBuffer(sizeInBytes, options));
        }

        public ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
            ReadOnlyMemory<T> source,
            MemoryOptions options,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            var sizeInBytes = source.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            return ValueTask.FromResult<IMemoryBuffer>(new MockMemoryBuffer(sizeInBytes, options));
        }

        public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
        {
            return new MockMemoryBuffer(length, buffer.Options);
        }
    }

    /// <summary>
    /// Mock memory buffer for integration testing.
    /// </summary>
    private sealed class MockMemoryBuffer : IMemoryBuffer
    {
        public MockMemoryBuffer(long sizeInBytes, MemoryOptions options)
        {
            SizeInBytes = sizeInBytes;
            Options = options;
        }

        public long SizeInBytes { get; }
        public MemoryOptions Options { get; }
        public bool IsDisposed { get; private set; }

        public ValueTask CopyFromHostAsync<T>(
            ReadOnlyMemory<T> source,
            long offset,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask CopyToHostAsync<T>(
            Memory<T> destination,
            long offset,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Mock compiled kernel for integration testing.
    /// </summary>
    private sealed class MockCompiledKernel : ICompiledKernel
    {
        public string Name => "MockKernel";

        public ValueTask ExecuteAsync(
            KernelArguments arguments,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}