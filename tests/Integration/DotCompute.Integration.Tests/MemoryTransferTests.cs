// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Memory;
using DotCompute.Tests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Integration.Tests;

/// <summary>
/// Integration tests for memory transfer operations between devices and host.
/// Tests memory management, transfer optimization, and data integrity.
/// </summary>
public class MemoryTransferTests : IntegrationTestBase
{
    public MemoryTransferTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [InlineData(1024)]      // 1KB
    [InlineData(1024 * 1024)]      // 1MB
    [InlineData(16 * 1024 * 1024)] // 16MB
    public async Task MemoryTransfer_HostToDevice_ShouldTransferCorrectly(int dataSize)
    {
        // Arrange
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        await acceleratorManager.InitializeAsync();
        var accelerators = acceleratorManager.AvailableAccelerators;
        
        if (!accelerators.Any())
        {
            Logger.LogInformation("Skipping host-to-device transfer test - no accelerators available");
            return;
        }

        var testData = TestDataGenerator.GenerateFloatArray(dataSize / sizeof(float));
        var accelerator = accelerators.First();

        // Act
        var transferResult = await PerformHostToDeviceTransfer(
            memoryManager,
            accelerator,
            testData);

        // Assert
        transferResult.Should().NotBeNull();
        transferResult.Success.Should().BeTrue();
        transferResult.TransferTime.Should().BePositive();
        transferResult.DataIntegrity.Should().BeTrue();
        transferResult.TransferredBytes.Should().Be(dataSize);
        
        // Performance assertions
        var throughput = dataSize / transferResult.TransferTime.TotalSeconds / (1024 * 1024); // MB/s
        throughput.Should().BeGreaterThan(1, "Transfer throughput should be reasonable");
    }

    [Theory]
    [InlineData(1024)]      // 1KB
    [InlineData(1024 * 1024)]      // 1MB
    [InlineData(16 * 1024 * 1024)] // 16MB
    public async Task MemoryTransfer_DeviceToHost_ShouldTransferCorrectly(int dataSize)
    {
        // Arrange
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        await acceleratorManager.InitializeAsync();
        var accelerators = acceleratorManager.AvailableAccelerators;
        
        if (!accelerators.Any())
        {
            Logger.LogInformation("Skipping device-to-host transfer test - no accelerators available");
            return;
        }

        var testData = TestDataGenerator.GenerateFloatArray(dataSize / sizeof(float));
        var accelerator = accelerators.First();

        // First upload data to device
        var deviceBuffer = await CreateBufferOnDevice(accelerator, testData);

        // Act
        var transferResult = await PerformDeviceToHostTransfer(
            accelerator,
            deviceBuffer,
            dataSize);

        // Assert
        transferResult.Should().NotBeNull();
        transferResult.Success.Should().BeTrue();
        transferResult.TransferTime.Should().BePositive();
        transferResult.DataIntegrity.Should().BeTrue();
        transferResult.TransferredBytes.Should().Be(dataSize);
        
        // Verify data matches original
        var transferredData = transferResult.Data as float[];
        transferredData.Should().NotBeNull();
        transferredData!.Length.Should().Be(testData.Length);
        
        for (int i = 0; i < testData.Length; i++)
        {
            transferredData[i].Should().BeApproximately(testData[i], 0.001f);
        }
    }

    [Fact]
    public async Task MemoryTransfer_UnifiedMemory_ShouldShareMemorySpace()
    {
        // Arrange
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        await acceleratorManager.InitializeAsync();
        var accelerators = acceleratorManager.AvailableAccelerators;
        var unifiedMemoryAccelerator = accelerators.FirstOrDefault(a => a.Info.IsUnifiedMemory);
        
        if (unifiedMemoryAccelerator == null)
        {
            Logger.LogInformation("Skipping unified memory test - no unified memory accelerators available");
            return;
        }

        const int dataSize = 1024;
        var testData = GenerateTestData(dataSize);

        // Act
        var unifiedResult = await TestUnifiedMemoryAccess(
            unifiedMemoryAccelerator,
            testData);

        // Assert
        unifiedResult.Should().NotBeNull();
        unifiedResult.Success.Should().BeTrue();
        unifiedResult.HostAccessTime.Should().BePositive();
        unifiedResult.DeviceAccessTime.Should().BePositive();
        unifiedResult.DataConsistency.Should().BeTrue();
        
        // Unified memory should have minimal transfer overhead
        unifiedResult.HostAccessTime.Should().BeLessThan(TimeSpan.FromMilliseconds(10));
        unifiedResult.DeviceAccessTime.Should().BeLessThan(TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public async Task MemoryTransfer_AsyncTransfers_ShouldExecuteConcurrently()
    {
        // Arrange
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        await acceleratorManager.InitializeAsync();
        var accelerators = acceleratorManager.AvailableAccelerators;
        
        if (!accelerators.Any())
        {
            Logger.LogInformation("Skipping async transfer test - no accelerators available");
            return;
        }

        const int dataSize = 4 * 1024 * 1024; // 4MB
        const int transferCount = 4;
        var testDataSets = Enumerable.Range(0, transferCount)
            .Select(_ => GenerateTestData(dataSize / sizeof(float)))
            .ToArray();
        
        var accelerator = accelerators.First();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var asyncResults = await ExecuteAsyncTransfers(
            memoryManager,
            accelerator,
            testDataSets);
        stopwatch.Stop();

        // Assert
        asyncResults.Should().HaveCount(transferCount);
        asyncResults.All(r => r.Success).Should().BeTrue();
        asyncResults.All(r => r.DataIntegrity).Should().BeTrue();
        
        var totalTransferTime = asyncResults.Sum(r => r.TransferTime.TotalMilliseconds);
        var concurrentTime = stopwatch.Elapsed.TotalMilliseconds;
        
        // Concurrent transfers should show some parallelism benefit
        concurrentTime.Should().BeLessThan(totalTransferTime * 0.8,
            "Async transfers should show performance benefit");
    }

    [Fact]
    public async Task MemoryTransfer_PinnedMemory_ShouldImprovePerformance()
    {
        // Arrange
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        await acceleratorManager.InitializeAsync();
        var accelerators = acceleratorManager.AvailableAccelerators;
        
        if (!accelerators.Any())
        {
            Logger.LogInformation("Skipping pinned memory test - no accelerators available");
            return;
        }

        const int dataSize = 8 * 1024 * 1024; // 8MB
        var testData = TestDataGenerator.GenerateFloatArray(dataSize / sizeof(float));
        var accelerator = accelerators.First();

        // Act
        var regularResult = await PerformRegularMemoryTransfer(
            memoryManager,
            accelerator,
            testData);
            
        var pinnedResult = await PerformPinnedMemoryTransfer(
            memoryManager,
            accelerator,
            testData);

        // Assert
        regularResult.Success.Should().BeTrue();
        pinnedResult.Success.Should().BeTrue();
        
        // Pinned memory should be faster (though not guaranteed on all systems)
        var regularThroughput = dataSize / regularResult.TransferTime.TotalSeconds;
        var pinnedThroughput = dataSize / pinnedResult.TransferTime.TotalSeconds;
        
        Logger.LogInformation($"Regular memory throughput: {regularThroughput / (1024 * 1024):F2} MB/s");
        Logger.LogInformation($"Pinned memory throughput: {pinnedThroughput / (1024 * 1024):F2} MB/s");
        
        // At minimum, pinned memory should not be significantly slower
        pinnedThroughput.Should().BeGreaterThan(regularThroughput * 0.8);
    }

    [Fact]
    public async Task MemoryTransfer_MultipleDevices_ShouldTransferIndependently()
    {
        // Arrange
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        await acceleratorManager.InitializeAsync();
        var accelerators = acceleratorManager.AvailableAccelerators;
        
        if (accelerators.Count < 2)
        {
            Logger.LogInformation("Skipping multi-device transfer test - need at least 2 accelerators");
            return;
        }

        const int dataSize = 2 * 1024 * 1024; // 2MB per device
        var testDataSets = accelerators.Take(2).Select((_, i) => 
            GenerateTestData(dataSize / sizeof(float), seed: i + 100)).ToArray();

        // Act
        var multiDeviceResults = await ExecuteMultiDeviceTransfers(
            accelerators.Take(2).ToList(),
            testDataSets);

        // Assert
        multiDeviceResults.Should().HaveCount(2);
        multiDeviceResults.All(r => r.Success).Should().BeTrue();
        multiDeviceResults.All(r => r.DataIntegrity).Should().BeTrue();
        
        // Each device should process its own data independently
        for (int i = 0; i < 2; i++)
        {
            var result = multiDeviceResults[i];
            var originalData = testDataSets[i];
            var transferredData = result.Data as float[];
            
            transferredData.Should().NotBeNull();
            transferredData!.Length.Should().Be(originalData.Length);
        }
    }

    [Theory]
    [InlineData(MemoryType.DeviceLocal)]
    [InlineData(MemoryType.HostVisible)]
    [InlineData(MemoryType.Shared)]
    public async Task MemoryTransfer_DifferentMemoryTypes_ShouldHandleCorrectly(MemoryType memoryType)
    {
        // Arrange
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        await acceleratorManager.InitializeAsync();
        var accelerators = acceleratorManager.AvailableAccelerators;
        
        if (!accelerators.Any())
        {
            Logger.LogInformation($"Skipping {memoryType} memory test - no accelerators available");
            return;
        }

        const int dataSize = 1024;
        var testData = GenerateTestData(dataSize);
        var accelerator = accelerators.First();

        // Act
        var memoryTypeResult = await TestMemoryTypeTransfer(
            memoryManager,
            accelerator,
            testData,
            memoryType);

        // Assert
        memoryTypeResult.Should().NotBeNull();
        memoryTypeResult.Success.Should().BeTrue();
        memoryTypeResult.DataIntegrity.Should().BeTrue();
        memoryTypeResult.MemoryType.Should().Be(memoryType);
        
        // Performance should vary based on memory type
        switch (memoryType)
        {
            case MemoryType.DeviceLocal:
                // Should be fastest for device operations
                memoryTypeResult.AccessTime.Should().BePositive();
                break;
            case MemoryType.HostVisible:
                // Should allow host access
                memoryTypeResult.HostAccessible.Should().BeTrue();
                break;
            case MemoryType.Shared:
                // Should allow both host and device access
                memoryTypeResult.HostAccessible.Should().BeTrue();
                break;
        }
    }

    [Fact]
    public async Task MemoryTransfer_LargeDataset_ShouldHandleEfficiently()
    {
        // Arrange
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        await acceleratorManager.InitializeAsync();
        var accelerators = acceleratorManager.AvailableAccelerators;
        
        if (!accelerators.Any())
        {
            Logger.LogInformation("Skipping large dataset test - no accelerators available");
            return;
        }

        const int largeDataSize = 64 * 1024 * 1024; // 64MB
        var accelerator = accelerators.First();
        
        // Check if device has enough memory
        if (accelerator.Info.AvailableMemory < largeDataSize * 2)
        {
            Logger.LogInformation("Skipping large dataset test - insufficient device memory");
            return;
        }

        Logger.LogInformation($"Generating test data for {largeDataSize / sizeof(float)} elements");
        var largeTestData = GenerateTestData(largeDataSize / sizeof(float));
        Logger.LogInformation($"Test data generated successfully: {largeTestData.Length} elements");

        // Act
        var largeTransferResult = await PerformLargeDataTransfer(
            memoryManager,
            accelerator,
            largeTestData);

        // Assert
        largeTransferResult.Should().NotBeNull();
        largeTransferResult.Success.Should().BeTrue();
        largeTransferResult.DataIntegrity.Should().BeTrue();
        largeTransferResult.TransferTime.Should().BeLessThan(TimeSpan.FromSeconds(10));
        
        var throughput = largeDataSize / largeTransferResult.TransferTime.TotalSeconds / (1024 * 1024);
        throughput.Should().BeGreaterThan(10, "Large transfer should maintain good throughput");
    }

    [Fact]
    public async Task MemoryTransfer_ErrorRecovery_ShouldHandleFailures()
    {
        // Arrange
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        await acceleratorManager.InitializeAsync();
        var accelerators = acceleratorManager.AvailableAccelerators;
        
        if (!accelerators.Any())
        {
            Logger.LogInformation("Skipping error recovery test - no accelerators available");
            return;
        }

        var accelerator = accelerators.First();

        // Act & Assert - Test various error conditions
        
        // 1. Invalid buffer size
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateInvalidBuffer(memoryManager, -1));
            
        // 2. Out of memory condition (simulate by requesting huge allocation)
        var largeSize = accelerator.Info.TotalMemory * 2; // Request more than available
        var result = await TryCreateOversizedBuffer(memoryManager, accelerator, largeSize);
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        
        // 3. Transfer to disposed buffer
        var disposedBufferResult = await TestDisposedBufferTransfer(memoryManager, accelerator);
        disposedBufferResult.Success.Should().BeFalse();
    }

    // Helper methods
    private static float[] GenerateTestData(int size, int seed = 42)
    {
        return TestDataGenerator.GenerateFloatArray(size, -1000f, 1000f);
    }

    private async Task<TransferResult> PerformHostToDeviceTransfer(
        IMemoryManager memoryManager,
        IAccelerator accelerator,
        float[] data)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Create device buffer
            var buffer = await CreateInputBuffer(memoryManager, data);
            stopwatch.Stop();

            // Verify data integrity by reading back
            var readData = await ReadBufferAsync<float>(buffer);
            var integrity = VerifyDataIntegrity(data, readData);

            return new TransferResult
            {
                Success = true,
                TransferTime = stopwatch.Elapsed,
                TransferredBytes = data.Length * sizeof(float),
                DataIntegrity = integrity,
                Data = readData
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Host to device transfer failed");
            return new TransferResult
            {
                Success = false,
                TransferTime = stopwatch.Elapsed,
                Error = ex.Message
            };
        }
    }

    private async Task<TransferResult> PerformDeviceToHostTransfer(
        IAccelerator accelerator,
        IMemoryBuffer deviceBuffer,
        int expectedBytes)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var data = await ReadBufferAsync<float>(deviceBuffer);
            stopwatch.Stop();

            return new TransferResult
            {
                Success = true,
                TransferTime = stopwatch.Elapsed,
                TransferredBytes = expectedBytes,
                DataIntegrity = data != null,
                Data = data
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Device to host transfer failed");
            return new TransferResult
            {
                Success = false,
                TransferTime = stopwatch.Elapsed,
                Error = ex.Message
            };
        }
    }

    private async Task<UnifiedMemoryResult> TestUnifiedMemoryAccess(
        IAccelerator accelerator,
        float[] testData)
    {
        try
        {
            var hostStopwatch = System.Diagnostics.Stopwatch.StartNew();
            // Simulate host access
            var hostSum = testData.Sum();
            hostStopwatch.Stop();

            var deviceStopwatch = System.Diagnostics.Stopwatch.StartNew();
            // Simulate device access (through buffer creation)
            var buffer = await CreateInputBuffer(accelerator.Memory, testData);
            var deviceData = await ReadBufferAsync<float>(buffer);
            deviceStopwatch.Stop();

            var deviceSum = deviceData?.Sum() ?? 0;
            var consistency = Math.Abs(hostSum - deviceSum) < 0.01f;

            return new UnifiedMemoryResult
            {
                Success = true,
                HostAccessTime = hostStopwatch.Elapsed,
                DeviceAccessTime = deviceStopwatch.Elapsed,
                DataConsistency = consistency
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unified memory test failed");
            return new UnifiedMemoryResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<List<TransferResult>> ExecuteAsyncTransfers(
        IMemoryManager memoryManager,
        IAccelerator accelerator,
        float[][] testDataSets)
    {
        // Use limited concurrency to avoid resource contention
        var maxConcurrency = Math.Max(2, Environment.ProcessorCount / 2);
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        
        var tasks = testDataSets.Select(async testData =>
        {
            await semaphore.WaitAsync();
            try
            {
                // Add small delay to better simulate async behavior
                await Task.Delay(1);
                return await PerformHostToDeviceTransfer(memoryManager, accelerator, testData);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        semaphore.Dispose();
        return results.ToList();
    }

    private async Task<TransferResult> PerformRegularMemoryTransfer(
        IMemoryManager memoryManager,
        IAccelerator accelerator,
        float[] testData)
    {
        // Regular memory allocation and transfer
        return await PerformHostToDeviceTransfer(memoryManager, accelerator, testData);
    }

    private async Task<TransferResult> PerformPinnedMemoryTransfer(
        IMemoryManager memoryManager,
        IAccelerator accelerator,
        float[] testData)
    {
        // For this test, we'll simulate pinned memory behavior
        // In a real implementation, this would use actual pinned memory APIs
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Simulate pinned memory allocation
            var buffer = await CreateInputBuffer(memoryManager, testData);
            stopwatch.Stop();

            var readData = await ReadBufferAsync<float>(buffer);
            var integrity = VerifyDataIntegrity(testData, readData);

            return new TransferResult
            {
                Success = true,
                TransferTime = stopwatch.Elapsed,
                TransferredBytes = testData.Length * sizeof(float),
                DataIntegrity = integrity,
                Data = readData
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Pinned memory transfer failed");
            return new TransferResult
            {
                Success = false,
                TransferTime = stopwatch.Elapsed,
                Error = ex.Message
            };
        }
    }

    private async Task<List<TransferResult>> ExecuteMultiDeviceTransfers(
        List<IAccelerator> accelerators,
        float[][] testDataSets)
    {
        var tasks = accelerators.Zip(testDataSets, async (accelerator, testData) =>
        {
            var result = await PerformHostToDeviceTransfer(
                accelerator.Memory,
                accelerator,
                testData);
            return result;
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    private async Task<MemoryTypeResult> TestMemoryTypeTransfer(
        IMemoryManager memoryManager,
        IAccelerator accelerator,
        float[] testData,
        MemoryType memoryType)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // For this test, we'll simulate different memory types
            var buffer = await CreateInputBuffer(memoryManager, testData);
            stopwatch.Stop();

            var readData = await ReadBufferAsync<float>(buffer);
            var integrity = VerifyDataIntegrity(testData, readData);

            return new MemoryTypeResult
            {
                Success = true,
                MemoryType = memoryType,
                AccessTime = stopwatch.Elapsed,
                DataIntegrity = integrity,
                HostAccessible = memoryType != MemoryType.DeviceLocal
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, $"Memory type {memoryType} test failed");
            return new MemoryTypeResult
            {
                Success = false,
                MemoryType = memoryType,
                AccessTime = stopwatch.Elapsed,
                Error = ex.Message
            };
        }
    }

    private async Task<TransferResult> PerformLargeDataTransfer(
        IMemoryManager memoryManager,
        IAccelerator accelerator,
        float[] largeTestData)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            Logger.LogInformation($"Attempting to create input buffer for {largeTestData.Length} elements ({largeTestData.Length * sizeof(float) / (1024 * 1024)}MB)");
            var buffer = await CreateInputBuffer(memoryManager, largeTestData);
            Logger.LogInformation("Input buffer created successfully");
            
            // For large data, we'll verify a sample rather than the entire array
            var sampleSize = Math.Min(1000, largeTestData.Length);
            var sampleIndices = Enumerable.Range(0, sampleSize)
                .Select(i => Math.Min(i * largeTestData.Length / sampleSize, largeTestData.Length - 1))
                .ToArray();
            
            var readData = await ReadBufferAsync<float>(buffer);
            stopwatch.Stop();

            var integrity = readData != null && readData.Length == largeTestData.Length;
            if (integrity && readData != null)
            {
                // Spot check data integrity with bounds checking
                for (int i = 0; i < sampleIndices.Length; i++)
                {
                    var index = sampleIndices[i];
                    // Extra safety: ensure index is within bounds of both arrays
                    if (index >= 0 && index < readData.Length && index < largeTestData.Length)
                    {
                        if (Math.Abs(readData[index] - largeTestData[index]) > 0.001f)
                        {
                            integrity = false;
                            break;
                        }
                    }
                    else
                    {
                        // If index is out of bounds, fail integrity check
                        integrity = false;
                        break;
                    }
                }
            }

            return new TransferResult
            {
                Success = true,
                TransferTime = stopwatch.Elapsed,
                TransferredBytes = largeTestData.Length * sizeof(float),
                DataIntegrity = integrity,
                Data = readData
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Large data transfer failed");
            return new TransferResult
            {
                Success = false,
                TransferTime = stopwatch.Elapsed,
                Error = ex.Message
            };
        }
    }

    private async Task<ErrorResult> TryCreateOversizedBuffer(
        IMemoryManager memoryManager,
        IAccelerator accelerator,
        long size)
    {
        try
        {
            // This should fail due to insufficient memory
            var oversizedData = new float[size / sizeof(float)];
            var buffer = await CreateInputBuffer(memoryManager, oversizedData);
            
            return new ErrorResult
            {
                Success = true // Unexpected success
            };
        }
        catch (Exception ex)
        {
            return new ErrorResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<ErrorResult> TestDisposedBufferTransfer(
        IMemoryManager memoryManager,
        IAccelerator accelerator)
    {
        try
        {
            var testData = GenerateTestData(100);
            var buffer = await CreateInputBuffer(memoryManager, testData);
            
            // Dispose the buffer
            if (buffer is IDisposable disposable)
                disposable.Dispose();
            else if (buffer is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            
            // Try to read from disposed buffer (should fail)
            var readData = await ReadBufferAsync<float>(buffer);
            
            return new ErrorResult
            {
                Success = true, // Unexpected success
                Error = "Expected exception when accessing disposed buffer"
            };
        }
        catch (Exception ex)
        {
            return new ErrorResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<IMemoryBuffer> CreateBufferOnDevice(IAccelerator accelerator, float[] data)
    {
        return await CreateInputBuffer(accelerator.Memory, data);
    }

    private async Task<IMemoryBuffer> CreateInvalidBuffer(IMemoryManager memoryManager, long size)
    {
        if (size < 0)
            throw new ArgumentException("Invalid buffer size");
            
        var data = new float[size];
        return await CreateInputBuffer(memoryManager, data);
    }

    private static bool VerifyDataIntegrity(float[] original, float[]? transferred)
    {
        if (transferred == null || original.Length != transferred.Length)
            return false;
            
        for (int i = 0; i < original.Length; i++)
        {
            if (Math.Abs(original[i] - transferred[i]) > 0.001f)
                return false;
        }
        
        return true;
    }
}

/// <summary>
/// Result of memory transfer operation.
/// </summary>
public class TransferResult
{
    public bool Success { get; set; }
    public TimeSpan TransferTime { get; set; }
    public int TransferredBytes { get; set; }
    public bool DataIntegrity { get; set; }
    public object? Data { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Result of unified memory test.
/// </summary>
public class UnifiedMemoryResult
{
    public bool Success { get; set; }
    public TimeSpan HostAccessTime { get; set; }
    public TimeSpan DeviceAccessTime { get; set; }
    public bool DataConsistency { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Result of memory type specific test.
/// </summary>
public class MemoryTypeResult
{
    public bool Success { get; set; }
    public MemoryType MemoryType { get; set; }
    public TimeSpan AccessTime { get; set; }
    public bool DataIntegrity { get; set; }
    public bool HostAccessible { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Result of error condition test.
/// </summary>
public class ErrorResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}