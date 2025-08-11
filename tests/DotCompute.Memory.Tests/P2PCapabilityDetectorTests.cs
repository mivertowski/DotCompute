// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for P2P capability detection including hardware-specific scenarios.
/// </summary>
public sealed class P2PCapabilityDetectorTests : IDisposable
{
    private readonly P2PCapabilityDetector _detector;

    public P2PCapabilityDetectorTests()
    {
        _detector = new P2PCapabilityDetector(NullLogger<P2PCapabilityDetector>.Instance);
    }

    [Fact]
    public async Task DetectP2PCapability_CudaDevices_ReturnsNVLinkCapability()
    {
        // Arrange
        var device1 = CreateMockAccelerator("cuda-device-0", "CUDA", "RTX 4090");
        var device2 = CreateMockAccelerator("cuda-device-1", "CUDA", "RTX 4090");

        // Act
        var capability = await _detector.DetectP2PCapabilityAsync(device1, device2);

        // Assert
        Assert.True(capability.IsSupported);
        Assert.Equal(P2PConnectionType.NVLink, capability.ConnectionType);
        Assert.True(capability.EstimatedBandwidthGBps > 0);
        Assert.Null(capability.LimitationReason);
        Assert.Equal(device1.Info.Id, capability.Device1Id);
        Assert.Equal(device2.Info.Id, capability.Device2Id);
    }

    [Fact]
    public async Task DetectP2PCapability_RocmDevices_ReturnsXGMICapability()
    {
        // Arrange
        var device1 = CreateMockAccelerator("rocm-device-0", "ROCm", "MI210");
        var device2 = CreateMockAccelerator("rocm-device-2", "ROCm", "MI210");

        // Act
        var capability = await _detector.DetectP2PCapabilityAsync(device1, device2);

        // Assert
        Assert.True(capability.IsSupported);
        Assert.Equal(P2PConnectionType.XGMI, capability.ConnectionType);
        Assert.Equal(400.0, capability.EstimatedBandwidthGBps);
        Assert.Null(capability.LimitationReason);
    }

    [Fact]
    public async Task DetectP2PCapability_CpuDevices_ReturnsSharedMemoryCapability()
    {
        // Arrange
        var device1 = CreateMockAccelerator("cpu-device-0", "CPU", "Intel CPU");
        var device2 = CreateMockAccelerator("cpu-device-1", "CPU", "Intel CPU");

        // Act
        var capability = await _detector.DetectP2PCapabilityAsync(device1, device2);

        // Assert
        Assert.True(capability.IsSupported);
        Assert.Equal(P2PConnectionType.SharedMemory, capability.ConnectionType);
        Assert.Equal(100.0, capability.EstimatedBandwidthGBps);
    }

    [Fact]
    public async Task DetectP2PCapability_DifferentDeviceTypes_ReturnsNotSupported()
    {
        // Arrange
        var cudaDevice = CreateMockAccelerator("cuda-device-0", "CUDA", "RTX 4090");
        var rocmDevice = CreateMockAccelerator("rocm-device-0", "ROCm", "MI210");

        // Act
        var capability = await _detector.DetectP2PCapabilityAsync(cudaDevice, rocmDevice);

        // Assert
        Assert.False(capability.IsSupported);
        Assert.Equal(P2PConnectionType.NotSupported, capability.ConnectionType);
        Assert.NotNull(capability.LimitationReason);
        Assert.Contains("Different device types", capability.LimitationReason);
    }

    [Fact]
    public async Task DetectP2PCapability_SameDevice_ReturnsNotSupported()
    {
        // Arrange
        var device = CreateMockAccelerator("cuda-device-0", "CUDA", "RTX 4090");

        // Act
        var capability = await _detector.DetectP2PCapabilityAsync(device, device);

        // Assert
        Assert.False(capability.IsSupported);
        Assert.Equal(P2PConnectionType.NotSupported, capability.ConnectionType);
        Assert.Contains("Same device", capability.LimitationReason);
    }

    [Fact]
    public async Task DetectP2PCapability_OpenCLDevices_ReturnsNotSupported()
    {
        // Arrange
        var device1 = CreateMockAccelerator("opencl-device-0", "OpenCL", "OpenCL GPU");
        var device2 = CreateMockAccelerator("opencl-device-1", "OpenCL", "OpenCL GPU");

        // Act
        var capability = await _detector.DetectP2PCapabilityAsync(device1, device2);

        // Assert
        Assert.False(capability.IsSupported);
        Assert.Equal(P2PConnectionType.NotSupported, capability.ConnectionType);
        Assert.Contains("OpenCL does not support", capability.LimitationReason);
    }

    [Fact]
    public async Task DetectP2PCapability_CacheResults_ReturnsCachedCapability()
    {
        // Arrange
        var device1 = CreateMockAccelerator("cuda-device-0", "CUDA", "RTX 4090");
        var device2 = CreateMockAccelerator("cuda-device-1", "CUDA", "RTX 4090");

        // Act
        var capability1 = await _detector.DetectP2PCapabilityAsync(device1, device2);
        var capability2 = await _detector.DetectP2PCapabilityAsync(device1, device2);

        // Assert
        Assert.Equal(capability1.Device1Id, capability2.Device1Id);
        Assert.Equal(capability1.Device2Id, capability2.Device2Id);
        Assert.Equal(capability1.IsSupported, capability2.IsSupported);
        Assert.Equal(capability1.ConnectionType, capability2.ConnectionType);
    }

    [Fact]
    public async Task GetDeviceCapabilities_CudaDevice_ReturnsCorrectCapabilities()
    {
        // Arrange
        var device = CreateMockAccelerator("cuda-device-0", "CUDA", "RTX 4090");

        // Act
        var capabilities = await _detector.GetDeviceCapabilitiesAsync(device);

        // Assert
        Assert.Equal(device.Info.Id, capabilities.DeviceId);
        Assert.Equal(device.Info.Name, capabilities.DeviceName);
        Assert.Equal("CUDA", capabilities.DeviceType);
        Assert.True(capabilities.SupportsP2P);
        Assert.Equal(8, capabilities.MaxP2PConnections);
        Assert.Equal(600.0, capabilities.P2PBandwidthGBps);
    }

    [Fact]
    public async Task GetDeviceCapabilities_CacheResults_ReturnsCachedCapabilities()
    {
        // Arrange
        var device = CreateMockAccelerator("cuda-device-0", "CUDA", "RTX 4090");

        // Act
        var capabilities1 = await _detector.GetDeviceCapabilitiesAsync(device);
        var capabilities2 = await _detector.GetDeviceCapabilitiesAsync(device);

        // Assert
        Assert.Equal(capabilities1.DeviceId, capabilities2.DeviceId);
        Assert.Equal(capabilities1.P2PBandwidthGBps, capabilities2.P2PBandwidthGBps);
    }

    [Fact]
    public async Task EnableP2PAccess_SupportedDevices_ReturnsSuccess()
    {
        // Arrange
        var device1 = CreateMockAccelerator("cuda-device-0", "CUDA", "RTX 4090");
        var device2 = CreateMockAccelerator("cuda-device-1", "CUDA", "RTX 4090");

        // Act
        var result = await _detector.EnableP2PAccessAsync(device1, device2);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Capability);
        Assert.True(result.Capability.IsSupported);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task EnableP2PAccess_UnsupportedDevices_ReturnsFailure()
    {
        // Arrange
        var device1 = CreateMockAccelerator("opencl-device-0", "OpenCL", "OpenCL GPU");
        var device2 = CreateMockAccelerator("opencl-device-1", "OpenCL", "OpenCL GPU");

        // Act
        var result = await _detector.EnableP2PAccessAsync(device1, device2);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Capability);
        Assert.False(result.Capability.IsSupported);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task DisableP2PAccess_SupportedDevices_ReturnsSuccess()
    {
        // Arrange
        var device1 = CreateMockAccelerator("cuda-device-0", "CUDA", "RTX 4090");
        var device2 = CreateMockAccelerator("cuda-device-1", "CUDA", "RTX 4090");
        
        // Enable first
        await _detector.EnableP2PAccessAsync(device1, device2);

        // Act
        var result = await _detector.DisableP2PAccessAsync(device1, device2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetOptimalTransferStrategy_P2PSupportedLargeData_ReturnsDirectP2P()
    {
        // Arrange
        var device1 = CreateMockAccelerator("cuda-device-0", "CUDA", "RTX 4090");
        var device2 = CreateMockAccelerator("cuda-device-1", "CUDA", "RTX 4090");
        var dataSizeBytes = 64 * 1024 * 1024; // 64MB

        // Act
        var strategy = await _detector.GetOptimalTransferStrategyAsync(device1, device2, dataSizeBytes);

        // Assert
        Assert.Equal(TransferType.DirectP2P, strategy.Type);
        Assert.True(strategy.EstimatedBandwidthGBps > 0);
        Assert.False(strategy.RequiresIntermediate);
    }

    [Fact]
    public async Task GetOptimalTransferStrategy_P2PNotSupportedSmallData_ReturnsHostMediated()
    {
        // Arrange
        var device1 = CreateMockAccelerator("opencl-device-0", "OpenCL", "OpenCL GPU");
        var device2 = CreateMockAccelerator("opencl-device-1", "OpenCL", "OpenCL GPU");
        var dataSizeBytes = 1024; // 1KB

        // Act
        var strategy = await _detector.GetOptimalTransferStrategyAsync(device1, device2, dataSizeBytes);

        // Assert
        Assert.Equal(TransferType.HostMediated, strategy.Type);
        Assert.True(strategy.RequiresIntermediate);
    }

    [Fact]
    public async Task GetOptimalTransferStrategy_P2PSupportedSmallData_ReturnsHostMediated()
    {
        // Arrange - even with P2P support, small data should use host-mediated
        var device1 = CreateMockAccelerator("cuda-device-0", "CUDA", "RTX 4090");
        var device2 = CreateMockAccelerator("cuda-device-1", "CUDA", "RTX 4090");
        var dataSizeBytes = 512; // 512 bytes - below P2P threshold

        // Act
        var strategy = await _detector.GetOptimalTransferStrategyAsync(device1, device2, dataSizeBytes);

        // Assert
        Assert.Equal(TransferType.HostMediated, strategy.Type);
        Assert.True(strategy.RequiresIntermediate);
    }

    [Theory]
    [InlineData(1024, 4096)]           // Small data
    [InlineData(1024 * 1024, 32768)]  // Medium data
    [InlineData(64 * 1024 * 1024, 1024 * 1024)] // Large data
    public async Task GetOptimalTransferStrategy_VariousDataSizes_ReturnsAppropriateChunkSize(
        int dataSizeBytes, int expectedMaxChunkSize)
    {
        // Arrange
        var device1 = CreateMockAccelerator("cuda-device-0", "CUDA", "RTX 4090");
        var device2 = CreateMockAccelerator("cuda-device-1", "CUDA", "RTX 4090");

        // Act
        var strategy = await _detector.GetOptimalTransferStrategyAsync(device1, device2, dataSizeBytes);

        // Assert
        Assert.True(strategy.ChunkSize <= expectedMaxChunkSize);
        Assert.True(strategy.ChunkSize >= 4096); // Minimum chunk size
        Assert.True(strategy.ChunkSize <= dataSizeBytes); // Chunk size doesn't exceed data size
    }

    [Fact]
    public async Task DetectP2PCapability_ConcurrentAccess_HandlesRaceConditions()
    {
        // Arrange
        var device1 = CreateMockAccelerator("cuda-device-0", "CUDA", "RTX 4090");
        var device2 = CreateMockAccelerator("cuda-device-1", "CUDA", "RTX 4090");

        // Act - concurrent detection calls
        var tasks = new Task<P2PConnectionCapability>[10];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = _detector.DetectP2PCapabilityAsync(device1, device2).AsTask();
        }

        var results = await Task.WhenAll(tasks);

        // Assert - all results should be identical (cached)
        var firstResult = results[0];
        foreach (var result in results)
        {
            Assert.Equal(firstResult.IsSupported, result.IsSupported);
            Assert.Equal(firstResult.ConnectionType, result.ConnectionType);
            Assert.Equal(firstResult.Device1Id, result.Device1Id);
            Assert.Equal(firstResult.Device2Id, result.Device2Id);
        }
    }

    [Fact]
    public async Task DetectP2PCapability_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var device1 = CreateMockAccelerator("cuda-device-0", "CUDA", "RTX 4090");
        var device2 = CreateMockAccelerator("cuda-device-1", "CUDA", "RTX 4090");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => 
            _detector.DetectP2PCapabilityAsync(device1, device2, cts.Token).AsTask());
    }

    private static IAccelerator CreateMockAccelerator(string id, string deviceType, string name)
    {
        return new MockAccelerator
        {
            Info = new AcceleratorInfo
            {
                Id = id,
                Name = name,
                DeviceType = deviceType,
                ComputeUnits = 128,
                MaxWorkGroupSize = 1024,
                LocalMemorySize = 64 * 1024,
                GlobalMemorySize = 24L * 1024 * 1024 * 1024 // 24GB
            }
        };
    }

    public void Dispose()
    {
        _detector?.DisposeAsync().AsTask().Wait();
    }

    /// <summary>
    /// Mock accelerator for testing P2P capability detection.
    /// </summary>
    private sealed class MockAccelerator : IAccelerator
    {
        public required AcceleratorInfo Info { get; init; }
        public IMemoryManager Memory { get; } = new MockMemoryManager();
        public bool IsDisposed => false;

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

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Dispose() { }
    }

    /// <summary>
    /// Mock memory manager for testing.
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
    /// Mock memory buffer for testing.
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
        public bool IsDisposed => false;

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

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Mock compiled kernel for testing.
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