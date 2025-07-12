// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotCompute.Backends.Metal;
using DotCompute.Core.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Backends.Metal.Tests;

public class MetalBackendTests : IDisposable
{
    private readonly Mock<ILogger<MetalBackend>> _loggerMock;
    private readonly MetalBackend _backend;
    private readonly bool _isMetalAvailable;

    public MetalBackendTests()
    {
        _loggerMock = new Mock<ILogger<MetalBackend>>();
        _backend = new MetalBackend(_loggerMock.Object);
        _isMetalAvailable = MetalBackend.IsAvailable();
    }

    public void Dispose()
    {
        _backend?.Dispose();
    }

    [Fact]
    public void IsAvailable_ReturnsCorrectStatus()
    {
        // Act
        var available = MetalBackend.IsAvailable();

        // Assert
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Metal should be available on macOS
            available.Should().BeTrue();
        }
        else
        {
            // Metal not available on other platforms
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
        
        if (_isMetalAvailable)
        {
            deviceInfo.MemorySize.Should().BeGreaterThan(0);
            deviceInfo.MaxThreadgroupMemoryLength.Should().BeGreaterThan(0);
            deviceInfo.MaxThreadsPerGroup.Should().BeGreaterThan(0);
        }
    }

    [SkippableMetalFact]
    public void AllocateBuffer_ValidSize_AllocatesSuccessfully()
    {
        // Arrange
        const long size = 1024 * 1024; // 1MB

        // Act
        var buffer = _backend.AllocateBuffer(size);

        // Assert
        buffer.Should().NotBeNull();
        buffer.Length.Should().Be(size);
        buffer.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void AllocateBuffer_InvalidSize_ThrowsException()
    {
        // Act & Assert
        var action = () => _backend.AllocateBuffer(-1);
        action.Should().Throw<ArgumentOutOfRangeException>();

        action = () => _backend.AllocateBuffer(0);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [SkippableMetalFact]
    public async Task CopyToBufferAsync_ValidData_CopiesSuccessfully()
    {
        // Arrange
        var data = Enumerable.Range(0, 1000).Select(i => (float)i).ToArray();
        var buffer = _backend.AllocateBuffer(data.Length * sizeof(float));

        // Act
        await _backend.CopyToBufferAsync(data, buffer);

        // Assert
        // Copy back to verify
        var result = new float[data.Length];
        await _backend.CopyFromBufferAsync(buffer, result);
        result.Should().BeEquivalentTo(data);
    }

    [SkippableMetalFact]
    public async Task CopyFromBufferAsync_ValidBuffer_CopiesSuccessfully()
    {
        // Arrange
        var data = Enumerable.Range(0, 100).Select(i => (float)i).ToArray();
        var buffer = _backend.AllocateBuffer(data.Length * sizeof(float));
        await _backend.CopyToBufferAsync(data, buffer);

        // Act
        var result = new float[data.Length];
        await _backend.CopyFromBufferAsync(buffer, result);

        // Assert
        result.Should().BeEquivalentTo(data);
    }

    [SkippableMetalFact]
    public async Task ExecuteComputeShaderAsync_SimpleShader_ExecutesSuccessfully()
    {
        // Arrange
        const int size = 1024;
        var input = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
        var output = new float[size];
        
        var inputBuffer = _backend.AllocateBuffer(size * sizeof(float));
        var outputBuffer = _backend.AllocateBuffer(size * sizeof(float));
        
        await _backend.CopyToBufferAsync(input, inputBuffer);

        var shader = @"
            #include <metal_stdlib>
            using namespace metal;
            
            kernel void square(device const float* input [[ buffer(0) ]],
                              device float* output [[ buffer(1) ]],
                              uint id [[ thread_position_in_grid ]])
            {
                if (id >= 1024) return;
                output[id] = input[id] * input[id];
            }
        ";

        var function = _backend.CompileFunction(shader, "square");

        // Act
        await _backend.ExecuteComputeShaderAsync(function, new[] { inputBuffer, outputBuffer }, 
            threadsPerThreadgroup: 64, threadgroups: (size + 63) / 64);

        // Assert
        await _backend.CopyFromBufferAsync(outputBuffer, output);
        for (int i = 0; i < size; i++)
        {
            output[i].Should().BeApproximately(input[i] * input[i], 0.001f);
        }
    }

    [SkippableMetalFact]
    public void CompileFunction_InvalidShader_ThrowsException()
    {
        // Act & Assert
        var action = () => _backend.CompileFunction("invalid metal code", "kernel");
        action.Should().Throw<ComputeException>();
    }

    [SkippableMetalFact]
    public void CreateCommandQueue_CreatesValidQueue()
    {
        // Act
        var queue = _backend.CreateCommandQueue();

        // Assert
        queue.Should().NotBeNull();
        queue.IsDisposed.Should().BeFalse();
    }

    [SkippableMetalFact]
    public async Task CommitAndWait_WaitsForCompletion()
    {
        // Arrange
        var data = new float[1000];
        var buffer = _backend.AllocateBuffer(data.Length * sizeof(float));

        // Act
        await _backend.CopyToBufferAsync(data, buffer);
        await _backend.CommitAndWait();

        // Assert - Should complete without throwing
        true.Should().BeTrue();
    }

    [SkippableMetalFact]
    public void AllocateBuffer_LargeAllocation_HandlesCorrectly()
    {
        // Arrange
        var deviceInfo = _backend.GetDeviceInfo();
        var largeSize = Math.Min(deviceInfo.MemorySize / 4, 1L * 1024 * 1024 * 1024); // 1GB or 1/4 of available

        // Act
        var buffer = _backend.AllocateBuffer(largeSize);

        // Assert
        buffer.Should().NotBeNull();
        buffer.Length.Should().Be(largeSize);
    }

    [SkippableMetalFact]
    public void AllocateBuffer_ExceedsAvailable_ThrowsException()
    {
        // Arrange
        var deviceInfo = _backend.GetDeviceInfo();
        var tooLarge = deviceInfo.MemorySize * 2;

        // Act & Assert
        var action = () => _backend.AllocateBuffer(tooLarge);
        action.Should().Throw<ComputeException>();
    }

    [SkippableMetalFact]
    public async Task MultipleCommandQueues_ExecuteConcurrently()
    {
        // Arrange
        const int queueCount = 4;
        const int dataSize = 10000;
        var queues = Enumerable.Range(0, queueCount).Select(_ => _backend.CreateCommandQueue()).ToArray();
        var buffers = new IMetalBuffer[queueCount];
        var tasks = new Task[queueCount];

        // Act
        for (int i = 0; i < queueCount; i++)
        {
            var queueIndex = i;
            var data = Enumerable.Range(0, dataSize).Select(x => (float)(x * queueIndex)).ToArray();
            buffers[i] = _backend.AllocateBuffer(dataSize * sizeof(float));
            
            tasks[i] = Task.Run(async () =>
            {
                await _backend.CopyToBufferAsync(data, buffers[queueIndex], queues[queueIndex]);
            });
        }

        await Task.WhenAll(tasks);
        await _backend.CommitAndWait();

        // Assert - All operations should complete
        foreach (var buffer in buffers)
        {
            buffer.Should().NotBeNull();
            buffer.IsDisposed.Should().BeFalse();
        }

        // Cleanup
        foreach (var queue in queues)
        {
            queue.Dispose();
        }
    }

    [SkippableMetalFact]
    public void GetMemoryInfo_ReturnsValidInfo()
    {
        // Act
        var (used, total) = _backend.GetMemoryInfo();

        // Assert
        used.Should().BeGreaterOrEqualTo(0);
        total.Should().BeGreaterThan(0);
        used.Should().BeLessThanOrEqualTo(total);
    }

    [Fact]
    public void Dispose_DisposesResources()
    {
        // Arrange
        var backend = new MetalBackend(_loggerMock.Object);

        // Act
        backend.Dispose();
        backend.Dispose(); // Second dispose should not throw

        // Assert
        var action = () => backend.AllocateBuffer(1024);
        action.Should().Throw<ObjectDisposedException>();
    }

    [SkippableMetalFact]
    public async Task ErrorHandling_InvalidShaderExecution_ThrowsException()
    {
        // Arrange
        var shader = @"
            #include <metal_stdlib>
            using namespace metal;
            
            kernel void test() { }
        ";

        var function = _backend.CompileFunction(shader, "test");

        // Act & Assert - Invalid threadgroup configuration should throw
        var action = () => _backend.ExecuteComputeShaderAsync(function, Array.Empty<IMetalBuffer>(), 
            threadsPerThreadgroup: 0, threadgroups: 0);
        
        await action.Should().ThrowAsync<ComputeException>();
    }

    [SkippableMetalFact]
    public void SetThreadgroupMemoryLength_ValidSize_SetsSuccessfully()
    {
        // Arrange
        var shader = @"
            #include <metal_stdlib>
            using namespace metal;
            
            kernel void test(threadgroup float* shared_memory [[ threadgroup(0) ]]) { }
        ";

        var function = _backend.CompileFunction(shader, "test");

        // Act & Assert - Should not throw
        _backend.SetThreadgroupMemoryLength(function, 1024);
    }

    [SkippableMetalFact]
    public async Task TextureOperations_2DTexture_WorksCorrectly()
    {
        // Arrange
        const int width = 256;
        const int height = 256;
        var textureData = new float[width * height * 4]; // RGBA
        
        for (int i = 0; i < textureData.Length; i++)
        {
            textureData[i] = i / (float)textureData.Length;
        }

        // Act
        var texture = _backend.CreateTexture2D(width, height, MetalPixelFormat.RGBA32Float);
        await _backend.CopyToTextureAsync(textureData, texture);

        // Assert
        texture.Should().NotBeNull();
        texture.Width.Should().Be(width);
        texture.Height.Should().Be(height);
        texture.IsDisposed.Should().BeFalse();
    }

    [SkippableMetalFact]
    public void CreateLibrary_FromSource_CompilesSuccessfully()
    {
        // Arrange
        var source = @"
            #include <metal_stdlib>
            using namespace metal;
            
            kernel void add_arrays(device const float* a [[ buffer(0) ]],
                                   device const float* b [[ buffer(1) ]],
                                   device float* result [[ buffer(2) ]],
                                   uint id [[ thread_position_in_grid ]])
            {
                result[id] = a[id] + b[id];
            }
        ";

        // Act
        var library = _backend.CreateLibrary(source);

        // Assert
        library.Should().NotBeNull();
        library.IsDisposed.Should().BeFalse();
    }

    [SkippableMetalFact]
    public async Task ParallelExecution_MultipleShaders_ExecutesCorrectly()
    {
        // Arrange
        const int dataSize = 1000;
        var data1 = Enumerable.Range(0, dataSize).Select(i => (float)i).ToArray();
        var data2 = Enumerable.Range(0, dataSize).Select(i => (float)(i * 2)).ToArray();
        var result = new float[dataSize];

        var buffer1 = _backend.AllocateBuffer(dataSize * sizeof(float));
        var buffer2 = _backend.AllocateBuffer(dataSize * sizeof(float));
        var resultBuffer = _backend.AllocateBuffer(dataSize * sizeof(float));

        await _backend.CopyToBufferAsync(data1, buffer1);
        await _backend.CopyToBufferAsync(data2, buffer2);

        var addShader = @"
            #include <metal_stdlib>
            using namespace metal;
            
            kernel void add(device const float* a [[ buffer(0) ]],
                           device const float* b [[ buffer(1) ]],
                           device float* result [[ buffer(2) ]],
                           uint id [[ thread_position_in_grid ]])
            {
                if (id >= 1000) return;
                result[id] = a[id] + b[id];
            }
        ";

        var function = _backend.CompileFunction(addShader, "add");

        // Act
        await _backend.ExecuteComputeShaderAsync(function, new[] { buffer1, buffer2, resultBuffer },
            threadsPerThreadgroup: 64, threadgroups: (dataSize + 63) / 64);

        await _backend.CopyFromBufferAsync(resultBuffer, result);

        // Assert
        for (int i = 0; i < dataSize; i++)
        {
            result[i].Should().BeApproximately(data1[i] + data2[i], 0.001f);
        }
    }
}

// Helper attribute to skip tests when Metal is not available
public class SkippableMetalFactAttribute : FactAttribute
{
    public override string Skip
    {
        get
        {
            if (!MetalBackend.IsAvailable())
                return "Metal is not available on this system";
            return base.Skip ?? string.Empty;
        }
        set => base.Skip = value;
    }
}