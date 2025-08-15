// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Tests.Shared;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Hardware.Integration;

/// <summary>
/// Integration tests for CUDA host-device memory transfer operations
/// </summary>
[Collection("CUDA Hardware Tests")]
public class CudaMemoryTransferTests : IDisposable
{
    private readonly ILogger<CudaMemoryTransferTests> _logger;
    private readonly ITestOutputHelper _output;
    private readonly List<CudaAccelerator> _accelerators = [];
    private readonly List<ISyncMemoryBuffer> _buffers = [];

    public CudaMemoryTransferTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<CudaMemoryTransferTests>();
    }

    [Theory]
    [Trait("Category", "Integration")]
    [Trait("Hardware", "CUDA")]
    [InlineData(1024, "1KB")]
    [InlineData(1024 * 1024, "1MB")]
    [InlineData(16 * 1024 * 1024, "16MB")]
    public unsafe void CudaMemoryTransfer_HostToDevice_ShouldPreserveDataIntegrity(long sizeInBytes, string description)
    {
        // Arrange
        if(!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;
        var deviceBuffer = memoryManager!.Allocate(sizeInBytes);
        _buffers.Add(deviceBuffer);

        var hostData = new byte[sizeInBytes];
        TestDataGenerator.FillRandomBytes(hostData);
        var retrievedData = new byte[sizeInBytes];

        // Act
        fixed(byte* hostPtr = hostData)
        fixed(byte* retrievedPtr = retrievedData)
        {
            memoryManager.CopyFromHost(hostPtr, deviceBuffer, sizeInBytes);
            memoryManager.CopyToHost(deviceBuffer, retrievedPtr, sizeInBytes);
        }

        // Assert
        retrievedData.Should().BeEquivalentTo(hostData, $"{description} data should be preserved in round-trip transfer");
    }

    [Theory]
    [Trait("Category", "Integration")]
    [Trait("Hardware", "CUDA")]
    [InlineData(typeof(float))]
    [InlineData(typeof(double))]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    public unsafe void CudaMemoryTransfer_TypedData_ShouldPreserveValues<T>(Type dataType) where T : unmanaged, IEquatable<T>
    {
        // Arrange
        if(!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;

        const int elementCount = 10000;
        var hostData = GenerateTypedTestData<T>(elementCount);
        var retrievedData = new T[elementCount];

        var sizeInBytes = elementCount * sizeof(T);
        var deviceBuffer = memoryManager!.Allocate(sizeInBytes);
        _buffers.Add(deviceBuffer);

        // Act
        fixed(T* hostPtr = hostData)
        fixed(T* retrievedPtr = retrievedData)
        {
            memoryManager.CopyFromHost(hostPtr, deviceBuffer, sizeInBytes);
            memoryManager.CopyToHost(deviceBuffer, retrievedPtr, sizeInBytes);
        }

        // Assert
        retrievedData.Should().BeEquivalentTo(hostData, $"{typeof(T).Name} data should be preserved accurately");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Hardware", "CUDA")]
    public unsafe void CudaMemoryTransfer_PartialTransfer_WithOffset_ShouldWorkCorrectly()
    {
        // Arrange
        if(!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;

        const int totalFloats = 1000;
        const int transferFloats = 500;
        const int offsetFloats = 250;

        var hostData = TestDataGenerator.GenerateFloatArray(totalFloats);
        var deviceBuffer = memoryManager!.Allocate(totalFloats * sizeof(float));
        var retrievedData = new float[transferFloats];
        _buffers.Add(deviceBuffer);

        // Act - Transfer full data to device
        fixed(float* hostPtr = hostData)
        {
            memoryManager.CopyFromHost(hostPtr, deviceBuffer, totalFloats * sizeof(float));
        }

        // Transfer partial data back with offset
        fixed(float* retrievedPtr = retrievedData)
        {
            memoryManager.CopyToHost(deviceBuffer, retrievedPtr, transferFloats * sizeof(float), offsetFloats * sizeof(float));
        }

        // Assert
        var expectedData = hostData.Skip(offsetFloats).Take(transferFloats).ToArray();
        retrievedData.Should().BeEquivalentTo(expectedData, "Partial transfer with offset should retrieve correct data segment");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Hardware", "CUDA")]
    public unsafe void CudaMemoryTransfer_MultipleBuffers_ShouldHandleConcurrentTransfers()
    {
        // Arrange
        if(!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;

        const int bufferCount = 5;
        const int elementsPerBuffer = 10000;
        const int bytesPerBuffer = elementsPerBuffer * sizeof(float);

        var hostDataArrays = Enumerable.Range(0, bufferCount)
            .Select(i => TestDataGenerator.GenerateFloatArray(elementsPerBuffer))
            .ToArray();

        var deviceBuffers = Enumerable.Range(0, bufferCount)
            .Select(i => memoryManager!.Allocate(bytesPerBuffer))
            .ToArray();
        _buffers.AddRange(deviceBuffers);

        var retrievedDataArrays = Enumerable.Range(0, bufferCount)
            .Select(i => new float[elementsPerBuffer])
            .ToArray();

        // Act - Upload all data
        for(int i = 0; i < bufferCount; i++)
        {
            fixed(float* hostPtr = hostDataArrays[i])
            {
                memoryManager!.CopyFromHost(hostPtr, deviceBuffers[i], bytesPerBuffer);
            }
        }

        // Download all data
        for(int i = 0; i < bufferCount; i++)
        {
            fixed(float* retrievedPtr = retrievedDataArrays[i])
            {
                memoryManager!.CopyToHost(deviceBuffers[i], retrievedPtr, bytesPerBuffer);
            }
        }

        // Assert
        for(int i = 0; i < bufferCount; i++)
        {
            retrievedDataArrays[i].Should().BeEquivalentTo(hostDataArrays[i], $"Buffer {i} should preserve data integrity");
        }
    }

    [Theory]
    [Trait("Category", "Performance")]
    [Trait("Hardware", "CUDA")]
    [InlineData(1024 * 1024)]        // 1MB
    [InlineData(64 * 1024 * 1024)]   // 64MB
    public unsafe void CudaMemoryTransfer_LargeData_ShouldMeetPerformanceTargets(long sizeInBytes)
    {
        // Arrange
        if(!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;
        var deviceBuffer = memoryManager!.Allocate(sizeInBytes);
        _buffers.Add(deviceBuffer);

        var hostData = new byte[sizeInBytes];
        TestDataGenerator.FillRandomBytes(hostData);

        // Warmup
        fixed(byte* hostPtr = hostData)
        {
            memoryManager.CopyFromHost(hostPtr, deviceBuffer, sizeInBytes);
            memoryManager.CopyToHost(deviceBuffer, hostPtr, sizeInBytes);
        }

        // Act - Measure upload performance
        var uploadStopwatch = Stopwatch.StartNew();
        fixed(byte* hostPtr = hostData)
        {
            memoryManager.CopyFromHost(hostPtr, deviceBuffer, sizeInBytes);
        }
        uploadStopwatch.Stop();

        // Act - Measure download performance  
        var downloadStopwatch = Stopwatch.StartNew();
        fixed(byte* hostPtr = hostData)
        {
            memoryManager.CopyToHost(deviceBuffer, hostPtr, sizeInBytes);
        }
        downloadStopwatch.Stop();

        // Assert
        var sizeMB = sizeInBytes /(1024.0 * 1024.0);
        var uploadBandwidth = sizeMB /(uploadStopwatch.ElapsedMilliseconds / 1000.0);
        var downloadBandwidth = sizeMB /(downloadStopwatch.ElapsedMilliseconds / 1000.0);

        uploadBandwidth.Should().BeGreaterThan(10.0, $"Upload bandwidth should be reasonable for {sizeMB:F1}MB");
        downloadBandwidth.Should().BeGreaterThan(10.0, $"Download bandwidth should be reasonable for {sizeMB:F1}MB");

        _output.WriteLine($"{sizeMB:F1}MB Upload: {uploadStopwatch.ElapsedMilliseconds}ms{uploadBandwidth:F1} MB/s)");
        _output.WriteLine($"{sizeMB:F1}MB Download: {downloadStopwatch.ElapsedMilliseconds}ms{downloadBandwidth:F1} MB/s)");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Hardware", "CUDA")]
    public unsafe void CudaMemoryTransfer_ZeroSizedTransfer_ShouldHandleGracefully()
    {
        // Arrange
        if(!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;
        var deviceBuffer = memoryManager!.Allocate(1024);
        _buffers.Add(deviceBuffer);

        var hostData = new byte[1024];

        // Act & Assert - Zero-sized transfers should not throw
        unsafe
        {
            fixed(byte* hostPtr = hostData)
            {
                // Execute operations directly to avoid lambda capture of fixed pointers
                var copyFromHostResult = false;
                var copyToHostResult = false;
                
                try
                {
                    memoryManager.CopyFromHost(hostPtr, deviceBuffer, 0);
                    copyFromHostResult = true;
                }
                catch
                {
                    copyFromHostResult = false;
                }
                
                try
                {
                    memoryManager.CopyToHost(deviceBuffer, hostPtr, 0);
                    copyToHostResult = true;
                }
                catch
                {
                    copyToHostResult = false;
                }
                
                copyFromHostResult.Should().BeTrue("CopyFromHost should not throw");
                copyToHostResult.Should().BeTrue("CopyToHost should not throw");
            }
        }
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    [Trait("Hardware", "CUDA")]
    public unsafe void CudaMemoryTransfer_BoundaryConditions_ShouldValidateCorrectly()
    {
        // Arrange
        if(!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;

        const int bufferSize = 1024;
        var deviceBuffer = memoryManager!.Allocate(bufferSize);
        _buffers.Add(deviceBuffer);

        var hostData = new byte[bufferSize];
        TestDataGenerator.FillRandomBytes(hostData);

        unsafe
        {
            fixed(byte* hostPtr = hostData)
            {
                // Act & Assert - Transfer exactly buffer size should work
                var exactSizeResult = false;
                try
                {
                    memoryManager!.CopyFromHost(hostPtr, deviceBuffer, bufferSize);
                    exactSizeResult = true;
                }
                catch
                {
                    exactSizeResult = false;
                }
                exactSizeResult.Should().BeTrue("Transfer of exact buffer size should succeed");

                // Transfer beyond buffer should throw
                var beyondBoundsResult = false;
                try
                {
                    memoryManager!.CopyFromHost(hostPtr, deviceBuffer, bufferSize + 1);
                    beyondBoundsResult = false; // Should not reach here
                }
                catch(ArgumentException)
                {
                    beyondBoundsResult = true; // Expected exception
                }
                catch
                {
                    beyondBoundsResult = false; // Wrong exception type
                }
                beyondBoundsResult.Should().BeTrue("Transfer beyond buffer bounds should be rejected");

                // Transfer with offset at boundary should throw
                var offsetBoundsResult = false;
                try
                {
                    memoryManager!.CopyFromHost(hostPtr, deviceBuffer, 1, bufferSize);
                    offsetBoundsResult = false; // Should not reach here
                }
                catch(ArgumentException)
                {
                    offsetBoundsResult = true; // Expected exception
                }
                catch
                {
                    offsetBoundsResult = false; // Wrong exception type
                }
                offsetBoundsResult.Should().BeTrue("Transfer with offset beyond bounds should be rejected");
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Hardware", "CUDA")]
    public unsafe void CudaMemoryTransfer_AlignmentRequirements_ShouldHandleUnalignedData()
    {
        // Arrange
        if(!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;

        // Test various data alignments
        var testSizes = new[] { 1, 3, 7, 15, 31, 63, 127, 255, 513, 1023 }; // Odd sizes to test alignment

        foreach (var size in testSizes)
        {
            var deviceBuffer = memoryManager!.Allocate(size);
            _buffers.Add(deviceBuffer);

            var hostData = new byte[size];
            TestDataGenerator.FillRandomBytes(hostData);
            var retrievedData = new byte[size];

            // Act
            fixed(byte* hostPtr = hostData)
            fixed(byte* retrievedPtr = retrievedData)
            {
                // Act & Assert - Execute transfers directly 
                memoryManager.CopyFromHost(hostPtr, deviceBuffer, size);
                memoryManager.CopyToHost(deviceBuffer, retrievedPtr, size);
                retrievedData.Should().BeEquivalentTo(hostData, $"Data integrity should be preserved for {size} bytes");
            }
        }
    }

    [Fact]
    [Trait("Category", "Stress")]
    [Trait("Hardware", "CUDA")]
    public unsafe void CudaMemoryTransfer_RepeatedTransfers_ShouldMaintainPerformance()
    {
        // Arrange
        if(!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;

        const int transferCount = 1000;
        const int dataSize = 4096; // 4KB transfers
        var deviceBuffer = memoryManager!.Allocate(dataSize);
        _buffers.Add(deviceBuffer);

        var hostData = new byte[dataSize];
        TestDataGenerator.FillRandomBytes(hostData);

        var transferTimes = new List<double>();

        // Act
        fixed(byte* hostPtr = hostData)
        {
            for(int i = 0; i < transferCount; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                memoryManager.CopyFromHost(hostPtr, deviceBuffer, dataSize);
                memoryManager.CopyToHost(deviceBuffer, hostPtr, dataSize);
                stopwatch.Stop();

                transferTimes.Add(stopwatch.ElapsedTicks * 1000000.0 / Stopwatch.Frequency);

                if(i % 100 == 0)
                {
                    _output.WriteLine($"Completed {i} transfers, current avg: {transferTimes.Skip(Math.Max(0, transferTimes.Count - 100)).Average():F2}μs");
                }
            }
        }

        // Assert
        var firstQuarter = transferTimes.Take(transferCount / 4).Average();
        var lastQuarter = transferTimes.Skip(3 * transferCount / 4).Average();
        var performanceDegradation = lastQuarter / firstQuarter;

        performanceDegradation .Should().BeLessThan(2.0, "Performance should not degrade significantly with repeated transfers");
        
        _output.WriteLine($"First quarter avg: {firstQuarter:F2}μs, Last quarter avg: {lastQuarter:F2}μs");
        _output.WriteLine($"Performance degradation: {performanceDegradation:F2}x");
    }

    [Theory]
    [Trait("Category", "Integration")]
    [Trait("Hardware", "CUDA")]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    public unsafe void CudaMemoryTransfer_ConcurrentThreads_ShouldBeThreadSafe(int threadCount)
    {
        // Arrange
        if(!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;

        const int elementsPerThread = 1000;
        const int bytesPerThread = elementsPerThread * sizeof(float);
        
        var buffers = Enumerable.Range(0, threadCount)
            .Select(i => memoryManager!.Allocate(bytesPerThread))
            .ToArray();
        _buffers.AddRange(buffers);

        var hostDataArrays = Enumerable.Range(0, threadCount)
            .Select(i => TestDataGenerator.GenerateFloatArray(elementsPerThread))
            .ToArray();

        var results = new ConcurrentBag<bool>();

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(threadIndex => Task.Run(() =>
        {
            try
            {
                var hostData = hostDataArrays[threadIndex];
                var deviceBuffer = buffers[threadIndex];
                var retrievedData = new float[elementsPerThread];

                fixed(float* hostPtr = hostData)
                fixed(float* retrievedPtr = retrievedData)
                {
                    // Multiple transfers per thread
                    for(int iteration = 0; iteration < 10; iteration++)
                    {
                        memoryManager?.CopyFromHost(hostPtr, deviceBuffer, bytesPerThread);
                        memoryManager?.CopyToHost(deviceBuffer, retrievedPtr, bytesPerThread);
                        
                        // Verify data integrity
                        var isDataCorrect = retrievedData.SequenceEqual(hostData);
                        if(!isDataCorrect)
                        {
                            results.Add(false);
                            return;
                        }
                    }
                }

                results.Add(true);
            }
            catch(Exception)
            {
                results.Add(false);
            }
        })).ToArray();

        Task.WaitAll(tasks);

        // Assert
        Assert.Equal(threadCount, results.Count());
        results.Should().AllSatisfy(result => result.Should().BeTrue("All concurrent transfers should succeed"));
    }

    [Fact]
    [Trait("Category", "EdgeCase")]
    [Trait("Hardware", "CUDA")]
    public unsafe void CudaMemoryTransfer_NullPointers_ShouldThrowAppropriateExceptions()
    {
        // Arrange
        if(!IsCudaAvailable()) return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;
        var deviceBuffer = memoryManager!.Allocate(1024);
        _buffers.Add(deviceBuffer);

        // Act & Assert
        Action uploadWithNullPtr =() => memoryManager.CopyFromHost(null, deviceBuffer, 1024);
        uploadWithNullPtr.Should().Throw<ArgumentNullException>("Upload with null source should throw");

        var hostData = new byte[1024];
        fixed(byte* hostPtr = hostData)
        {
            Action downloadWithNullPtr =() => memoryManager.CopyToHost(deviceBuffer, null, 1024);
            downloadWithNullPtr.Should().Throw<ArgumentNullException>("Download with null destination should throw");
        }
    }

    // Helper Methods
    private static T[] GenerateTypedTestData<T>(int count) where T : unmanaged, IEquatable<T>
    {
        if(typeof(T) == typeof(float))
            return(T[])(object)TestDataGenerator.GenerateFloatArray(count);
        if(typeof(T) == typeof(double))
            return(T[])(object)TestDataGenerator.GenerateDoubleArray(count);
        if(typeof(T) == typeof(int))
            return(T[])(object)TestDataGenerator.GenerateIntArray(count);
        if(typeof(T) == typeof(long))
            return (T[])(object)TestDataGenerator.GenerateArray(count, i => (long)Random.Shared.Next());
        
        throw new NotSupportedException($"Type {typeof(T)} is not supported for test data generation");
    }

    private CudaAccelerator CreateAccelerator()
    {
        var cudaLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<CudaAccelerator>();
        var accelerator = new CudaAccelerator(0, cudaLogger);
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
                if(!buffer.IsDisposed)
                {
                    var syncMemoryManager = _accelerators.FirstOrDefault()?.Memory as ISyncMemoryManager;
                    syncMemoryManager?.Free(buffer);
                }
            }
            catch(Exception ex)
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
            catch(Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing CUDA accelerator");
            }
        }
        _accelerators.Clear();
    }
}
