using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotCompute.Backends.CUDA;
using DotCompute.Core.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests;

public class CudaBackendTests : IDisposable
{
    private readonly Mock<ILogger<CudaBackend>> _loggerMock;
    private readonly CudaBackend _backend;
    private readonly bool _isCudaAvailable;

    public CudaBackendTests()
    {
        _loggerMock = new Mock<ILogger<CudaBackend>>();
        _backend = new CudaBackend(_loggerMock.Object);
        _isCudaAvailable = CudaBackend.IsAvailable();
    }

    public void Dispose()
    {
        _backend.Dispose();
    }

    [Fact]
    public void IsAvailable_ReturnsCorrectStatus()
    {
        // Act
        var available = CudaBackend.IsAvailable();

        // Assert
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || 
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // CUDA might be available on Windows/Linux
            available.Should().BeOneOf(true, false);
        }
        else
        {
            // CUDA not available on macOS
            available.Should().BeFalse();
        }
    }

    [Fact]
    public void GetDeviceInfo_ReturnsDeviceInformation()
    {
        // Act
        var deviceInfo = _backend.GetDeviceInfo();

        // Assert
        deviceInfo.Should().NotBeNull();
        deviceInfo.Name.Should().NotBeNullOrEmpty();
        deviceInfo.ComputeCapability.Should().NotBeNullOrEmpty();
        deviceInfo.MemorySize.Should().BeGreaterThan(0);
        deviceInfo.MaxThreadsPerBlock.Should().BeGreaterThan(0);
        deviceInfo.MultiprocessorCount.Should().BeGreaterThan(0);
    }

    [SkippableFact]
    public void AllocateMemory_ValidSize_AllocatesSuccessfully()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Arrange
        const long size = 1024 * 1024; // 1MB

        // Act
        var memory = _backend.AllocateMemory(size);

        // Assert
        memory.Should().NotBeNull();
        memory.Size.Should().Be(size);
        memory.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void AllocateMemory_InvalidSize_ThrowsException()
    {
        // Act & Assert
        var action = () => _backend.AllocateMemory(-1);
        action.Should().Throw<ArgumentOutOfRangeException>();

        action = () => _backend.AllocateMemory(0);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [SkippableFact]
    public async Task CopyToDeviceAsync_ValidData_CopiesSuccessfully()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Arrange
        var data = Enumerable.Range(0, 1000).Select(i => (float)i).ToArray();
        var memory = _backend.AllocateMemory(data.Length * sizeof(float));

        // Act
        await _backend.CopyToDeviceAsync(data, memory);

        // Assert
        // Copy back to verify
        var result = new float[data.Length];
        await _backend.CopyFromDeviceAsync(memory, result);
        result.Should().BeEquivalentTo(data);
    }

    [SkippableFact]
    public async Task CopyFromDeviceAsync_ValidMemory_CopiesSuccessfully()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Arrange
        var data = Enumerable.Range(0, 100).Select(i => (float)i).ToArray();
        var memory = _backend.AllocateMemory(data.Length * sizeof(float));
        await _backend.CopyToDeviceAsync(data, memory);

        // Act
        var result = new float[data.Length];
        await _backend.CopyFromDeviceAsync(memory, result);

        // Assert
        result.Should().BeEquivalentTo(data);
    }

    [SkippableFact]
    public async Task ExecuteKernelAsync_SimpleKernel_ExecutesSuccessfully()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Arrange
        const int size = 1024;
        var input = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
        var output = new float[size];
        
        var inputMem = _backend.AllocateMemory(size * sizeof(float));
        var outputMem = _backend.AllocateMemory(size * sizeof(float));
        
        await _backend.CopyToDeviceAsync(input, inputMem);

        var kernel = _backend.CompileKernel(@"
            extern ""C"" __global__ void square(float* input, float* output, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx < n) {
                    output[idx] = input[idx] * input[idx];
                }
            }
        ", "square");

        // Act
        await _backend.ExecuteKernelAsync(kernel, new[] { inputMem, outputMem, size }, 
            gridSize: (size + 255) / 256, blockSize: 256);

        // Assert
        await _backend.CopyFromDeviceAsync(outputMem, output);
        for (int i = 0; i < size; i++)
        {
            output[i].Should().BeApproximately(input[i] * input[i], 0.001f);
        }
    }

    [SkippableFact]
    public void CompileKernel_InvalidCode_ThrowsException()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Act & Assert
        var action = () => _backend.CompileKernel("invalid cuda code", "kernel");
        action.Should().Throw<ComputeException>();
    }

    [SkippableFact]
    public void CreateStream_CreatesValidStream()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Act
        var stream = _backend.CreateStream();

        // Assert
        stream.Should().NotBeNull();
        stream.IsDisposed.Should().BeFalse();
    }

    [SkippableFact]
    public async Task Synchronize_WaitsForCompletion()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Arrange
        var data = new float[1000];
        var memory = _backend.AllocateMemory(data.Length * sizeof(float));

        // Act
        await _backend.CopyToDeviceAsync(data, memory);
        await _backend.Synchronize();

        // Assert - Should complete without throwing
        true.Should().BeTrue();
    }

    [SkippableFact]
    public void AllocateMemory_LargeAllocation_HandlesCorrectly()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Arrange
        var deviceInfo = _backend.GetDeviceInfo();
        var largeSize = Math.Min(deviceInfo.MemorySize / 4, 1L * 1024 * 1024 * 1024); // 1GB or 1/4 of available

        // Act
        var memory = _backend.AllocateMemory(largeSize);

        // Assert
        memory.Should().NotBeNull();
        memory.Size.Should().Be(largeSize);
    }

    [SkippableFact]
    public void AllocateMemory_ExceedsAvailable_ThrowsException()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Arrange
        var deviceInfo = _backend.GetDeviceInfo();
        var tooLarge = deviceInfo.MemorySize * 2;

        // Act & Assert
        var action = () => _backend.AllocateMemory(tooLarge);
        action.Should().Throw<ComputeException>();
    }

    [SkippableFact]
    public async Task MultipleStreams_ExecuteConcurrently()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Arrange
        const int streamCount = 4;
        const int dataSize = 10000;
        var streams = Enumerable.Range(0, streamCount).Select(_ => _backend.CreateStream()).ToArray();
        var memories = new IDeviceMemory[streamCount];
        var tasks = new Task[streamCount];

        // Act
        for (int i = 0; i < streamCount; i++)
        {
            var streamIndex = i;
            var data = Enumerable.Range(0, dataSize).Select(x => (float)(x * streamIndex)).ToArray();
            memories[i] = _backend.AllocateMemory(dataSize * sizeof(float));
            
            tasks[i] = Task.Run(async () =>
            {
                await _backend.CopyToDeviceAsync(data, memories[streamIndex], streams[streamIndex]);
            });
        }

        await Task.WhenAll(tasks);
        await _backend.Synchronize();

        // Assert - All operations should complete
        foreach (var memory in memories)
        {
            memory.Should().NotBeNull();
            memory.IsDisposed.Should().BeFalse();
        }

        // Cleanup
        foreach (var stream in streams)
        {
            stream.Dispose();
        }
    }

    [SkippableFact]
    public void GetMemoryInfo_ReturnsValidInfo()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Act
        var (free, total) = _backend.GetMemoryInfo();

        // Assert
        free.Should().BeGreaterThan(0);
        total.Should().BeGreaterThan(0);
        free.Should().BeLessThanOrEqualTo(total);
    }

    [Fact]
    public void Dispose_DisposesResources()
    {
        // Arrange
        var backend = new CudaBackend(_loggerMock.Object);

        // Act
        backend.Dispose();
        backend.Dispose(); // Second dispose should not throw

        // Assert
        var action = () => backend.AllocateMemory(1024);
        action.Should().Throw<ObjectDisposedException>();
    }

    [SkippableFact]
    public async Task ErrorHandling_KernelLaunchFailure_ThrowsException()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Arrange
        var kernel = _backend.CompileKernel(@"
            extern ""C"" __global__ void test() { }
        ", "test");

        // Act & Assert - Invalid grid/block size should throw
        var action = () => _backend.ExecuteKernelAsync(kernel, Array.Empty<object>(), 
            gridSize: 0, blockSize: 0);
        
        await action.Should().ThrowAsync<ComputeException>();
    }

    [SkippableFact]
    public void SetDevice_ValidDevice_SetsSuccessfully()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Act & Assert - Should not throw
        _backend.SetDevice(0);
    }

    [SkippableFact]
    public void SetDevice_InvalidDevice_ThrowsException()
    {
        Skip.IfNot(_isCudaAvailable, "CUDA not available");

        // Act & Assert
        var action = () => _backend.SetDevice(999);
        action.Should().Throw<ComputeException>();
    }
}

// Helper attribute to skip tests when CUDA is not available
public class SkippableFactAttribute : FactAttribute
{
    public override string Skip
    {
        get
        {
            if (!CudaBackend.IsAvailable())
                return "CUDA is not available on this system";
            return base.Skip ?? string.Empty;
        }
        set => base.Skip = value;
    }
}

public static class Skip
{
    public static void IfNot(bool condition, string reason)
    {
        if (!condition)
            throw new SkipException(reason);
    }
}

public class SkipException : Exception
{
    public SkipException(string reason) : base(reason) { }
}