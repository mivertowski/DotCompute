// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Backends.Metal.Registration;
using DotCompute.Backends.Metal.Utilities;
using DotCompute.Tests.Utilities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal
{

/// <summary>
/// Comprehensive backend tests for Metal accelerators focusing on device capability,
/// shader compilation, command buffers, and GPU performance validation.
/// </summary>
public class MetalAcceleratorTests : IClassFixture<MetalTestFixture>
{
    private readonly MetalTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public MetalAcceleratorTests(MetalTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [SkippableFact]
    public void CanCreateMetalAccelerator()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        var accelerator = _fixture.ServiceProvider.GetRequiredService<IAccelerator>();
        
        accelerator.Should().NotBeNull();
        accelerator.Info.Should().NotBeNull();
        accelerator.Info.DeviceType.Should().ContainAny("GPU", "IntegratedGPU", "DiscreteGPU", "ExternalGPU");
        accelerator.Info.Vendor.Should().Be("Apple");
    }

    [SkippableFact]
    public void CanAllocateMemory()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        var accelerator = _fixture.ServiceProvider.GetRequiredService<IAccelerator>();
        
        const int size = 1024 * sizeof(float);
        var memory = accelerator.Memory.Allocate(size);

        memory.Should().NotBeNull();
        memory.SizeInBytes.Should().Be(size);
        memory.Handle.Should().NotShould().Should().Be(IntPtr.Zero);

        accelerator.Memory.Free(memory);
    }

    [SkippableFact]
    public async Task CanCompileKernel()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        var accelerator = _fixture.ServiceProvider.GetRequiredService<IAccelerator>();
        
        var kernelDefinition = new KernelDefinition
        {
            Name = "testKernel",
            Code = "VectorAdd",
            Parameters = new[]
            {
                new KernelParameter { Name = "a", DataType = DataType.Float32, MemoryKind = MemoryKind.Global },
                new KernelParameter { Name = "b", DataType = DataType.Float32, MemoryKind = MemoryKind.Global },
                new KernelParameter { Name = "result", DataType = DataType.Float32, MemoryKind = MemoryKind.Global },
                new KernelParameter { Name = "size", DataType = DataType.UInt32, MemoryKind = MemoryKind.Constant }
            }
        };

        var kernel = await accelerator.CompileKernelAsync(kernelDefinition);

        kernel.Should().NotBeNull();
        kernel.Name.Should().Be("testKernel");
        kernel.Definition.Should().Be(kernelDefinition);
    }

    [SkippableFact]
    public async Task CanExecuteVectorAddKernel()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        var accelerator = _fixture.ServiceProvider.GetRequiredService<IAccelerator>();
        const int size = 1024;
        
        // Prepare test data
        var a = new float[size];
        var b = new float[size];
        var expected = new float[size];
        
        for (int i = 0; i < size; i++)
        {
            a[i] = i;
            b[i] = i * 2;
            expected[i] = a[i] + b[i];
        }

        // Allocate GPU memory
        var bufferA = accelerator.Memory.Allocate(size * sizeof(float));
        var bufferB = accelerator.Memory.Allocate(size * sizeof(float));
        var bufferResult = accelerator.Memory.Allocate(size * sizeof(float));

        try
        {
            // Copy data to GPU
            accelerator.Memory.CopyToDevice(bufferA, MemoryMarshal.AsBytes(a.AsSpan();
            accelerator.Memory.CopyToDevice(bufferB, MemoryMarshal.AsBytes(b.AsSpan();

            // Compile kernel
            var kernel = await accelerator.CompileKernelAsync(Kernels.MetalOptimizedKernels.CreateVectorAddKernel());

            // Execute kernel
            await kernel.ExecuteAsync(new KernelExecutionContext
            {
                GlobalWorkSize = new WorkSize(size, 1, 1),
                Arguments = new[]
                {
                    new KernelArgument("a", bufferA),
                    new KernelArgument("b", bufferB),
                    new KernelArgument("result", bufferResult),
                    new KernelArgument("size", (uint)size)
                }
            });

            // Synchronize
            await accelerator.SynchronizeAsync();

            // Copy result back
            var result = new float[size];
            accelerator.Memory.CopyFromDevice(MemoryMarshal.AsBytes(result.AsSpan()), bufferResult);

            // Verify
            result.Should().BeEquivalentTo(expected, options => options.WithStrictOrdering());
        }
        finally
        {
            accelerator.Memory.Free(bufferA);
            accelerator.Memory.Free(bufferB);
            accelerator.Memory.Free(bufferResult);
        }
    }

    [SkippableFact]
    public void CanGetDeviceInformation()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        var devices = MetalUtilities.GetAllDevices();
        
        devices.Should().NotBeEmpty();
        
        foreach (var device in devices)
        {
            device.Name.Should().NotBeNullOrWhiteSpace();
            device.MaxThreadgroupSize.Should().BeGreaterThan(0);
            device.MaxBufferLength.Should().BeGreaterThan(0);
            
            // Log device info
            _fixture.Logger.LogInformation("Metal Device: {Device}", device.ToString());
            _output.WriteLine($"Metal Device: {device.Name}");
            _output.WriteLine($"  Max Threadgroup Size: {device.MaxThreadgroupSize}");
            _output.WriteLine($"  Max Buffer Length: {device.MaxBufferLength:N0}");
        }
    }

    [SkippableFact]
    public async Task MetalAccelerator_DeviceCapability_DetectsGPUFeatures()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        var accelerator = _fixture.ServiceProvider.GetRequiredService<IAccelerator>();
        var devices = MetalUtilities.GetAllDevices();
        
        devices.Should().NotBeEmpty();
        
        foreach (var device in devices)
        {
            _output.WriteLine($"Metal GPU: {device.Name}");
            _output.WriteLine($"  Device Type: {device.Type}");
            _output.WriteLine($"  Memory: {device.RecommendedMaxWorkingSetSize:N0} bytes");
            _output.WriteLine($"  Max Threadgroup Memory: {device.MaxThreadgroupMemoryLength:N0} bytes");
            _output.WriteLine($"  Max Threads Per Threadgroup: {device.MaxThreadsPerThreadgroup}");
            _output.WriteLine($"  Programming Sample Rate: {device.ProgrammableSamplePositionsSupported}");
            
            device.Name.Should().NotBeNullOrWhiteSpace();
            device.MaxThreadgroupSize.Should().BeGreaterThan(0);
            device.MaxBufferLength.Should().BeGreaterThan(0);
            device.RecommendedMaxWorkingSetSize.Should().BeGreaterThan(0);
        }
    }

    [SkippableFact]
    public async Task MetalAccelerator_ShaderCompilation_CompilesToMSL()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        var accelerator = _fixture.ServiceProvider.GetRequiredService<IAccelerator>();
        var kernelSource = TestKernels.MetalMatrixMultiply;

        var stopwatch = Stopwatch.StartNew();
        var compiledKernel = await accelerator.CompileKernelAsync(kernelSource);
        stopwatch.Stop();

        compiledKernel.Should().NotBeNull();
        compiledKernel.Name.Should().Be(kernelSource.Name);
        
        _output.WriteLine($"Metal shader compilation took {stopwatch.ElapsedMicroseconds} μs");
        _output.WriteLine($"Compiled kernel: {compiledKernel.Name}");
    }

    [SkippableFact]
    public async Task MetalAccelerator_CommandBuffer_ExecutesKernels()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        var accelerator = _fixture.ServiceProvider.GetRequiredService<IAccelerator>();
        const int size = 1024;
        
        var testData = TestDataGenerator.GenerateFloatArray(size);
        var multiplier = 2.0f;
        var expected = testData.Select(x => x * multiplier).ToArray();

        var inputBuffer = accelerator.Memory.Allocate(size * sizeof(float));
        var outputBuffer = accelerator.Memory.Allocate(size * sizeof(float));

        try
        {
            // Copy data to GPU
            accelerator.Memory.CopyToDevice(inputBuffer, MemoryMarshal.AsBytes(testData.AsSpan();

            // Compile and execute kernel
            var kernel = await accelerator.CompileKernelAsync(TestKernels.MetalVectorScale);
            var args = new KernelArguments(inputBuffer, outputBuffer, multiplier, size);

            var stopwatch = Stopwatch.StartNew();
            await kernel.ExecuteAsync(args);
            await accelerator.SynchronizeAsync();
            stopwatch.Stop();

            // Copy result back
            var result = new float[size];
            accelerator.Memory.CopyFromDevice(MemoryMarshal.AsBytes(result.AsSpan()), outputBuffer);

            result.Should().BeEquivalentTo(expected, options => options.WithStrictOrdering());
            
            _output.WriteLine($"Metal command buffer execution ({size} elements): {stopwatch.ElapsedMicroseconds} μs");
        }
        finally
        {
            accelerator.Memory.Free(inputBuffer);
            accelerator.Memory.Free(outputBuffer);
        }
    }

    [SkippableFact]
    public async Task MetalAccelerator_MultipleCommandBuffers_ExecutesConcurrently()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        var accelerator = _fixture.ServiceProvider.GetRequiredService<IAccelerator>();
        const int size = 512;
        
        var data1 = TestDataGenerator.GenerateFloatArray(size);
        var data2 = TestDataGenerator.GenerateFloatArray(size);
        
        var buffer1 = accelerator.Memory.Allocate(size * sizeof(float));
        var buffer2 = accelerator.Memory.Allocate(size * sizeof(float));
        var result1 = accelerator.Memory.Allocate(size * sizeof(float));
        var result2 = accelerator.Memory.Allocate(size * sizeof(float));

        try
        {
            accelerator.Memory.CopyToDevice(buffer1, MemoryMarshal.AsBytes(data1.AsSpan();
            accelerator.Memory.CopyToDevice(buffer2, MemoryMarshal.AsBytes(data2.AsSpan();

            var kernel1 = await accelerator.CompileKernelAsync(TestKernels.MetalVectorAdd);
            var kernel2 = await accelerator.CompileKernelAsync(TestKernels.MetalVectorMultiply);

            var stopwatch = Stopwatch.StartNew();
            
            // Execute kernels concurrently
            var task1 = kernel1.ExecuteAsync(new KernelArguments(buffer1, buffer2, result1, size));
            var task2 = kernel2.ExecuteAsync(new KernelArguments(buffer1, buffer2, result2, size));
            
            await Task.WhenAll(task1, task2);
            await accelerator.SynchronizeAsync();
            
            stopwatch.Stop();

            _output.WriteLine($"Concurrent Metal kernel execution took {stopwatch.ElapsedMicroseconds} μs");
            
            // Verify results
            var resultArray1 = new float[size];
            var resultArray2 = new float[size];
            
            accelerator.Memory.CopyFromDevice(MemoryMarshal.AsBytes(resultArray1.AsSpan()), result1);
            accelerator.Memory.CopyFromDevice(MemoryMarshal.AsBytes(resultArray2.AsSpan()), result2);

            for (int i = 0; i < size; i++)
            {
                resultArray1[i].Should().BeApproximately(data1[i] + data2[i], 1e-5f);
                resultArray2[i].Should().BeApproximately(data1[i] * data2[i], 1e-5f);
            }
        }
        finally
        {
            accelerator.Memory.Free(buffer1);
            accelerator.Memory.Free(buffer2);
            accelerator.Memory.Free(result1);
            accelerator.Memory.Free(result2);
        }
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(4096)]
    [InlineData(16384)]
    [InlineData(65536)]
    public async Task MetalAccelerator_VectorAddition_PerformanceBaseline(int size)
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        var accelerator = _fixture.ServiceProvider.GetRequiredService<IAccelerator>();
        var stopwatch = Stopwatch.StartNew();
        
        var result = await ExecuteMetalVectorAddition(accelerator, size);
        
        stopwatch.Stop();
        
        result.Should().NotBeNull();
        result.Length.Should().Be(size);
        
        // Metal should be fast on Apple GPUs
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000);
        
        _output.WriteLine($"Metal vector addition ({size} elements): {stopwatch.ElapsedMicroseconds} μs");
    }

    [SkippableFact]
    public async Task MetalAccelerator_MemoryBandwidth_MeasuresTransferRate()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Metal is only available on macOS");
        Skip.IfNot(MetalUtilities.IsMetalAvailable(), "Metal is not available on this system");

        var accelerator = _fixture.ServiceProvider.GetRequiredService<IAccelerator>();
        const int size = 1024 * 1024; // 1M floats = 4MB
        
        var testData = TestDataGenerator.GenerateFloatArray(size);
        var buffer = accelerator.Memory.Allocate(size * sizeof(float));

        try
        {
            // Measure upload bandwidth
            var uploadStopwatch = Stopwatch.StartNew();
            accelerator.Memory.CopyToDevice(buffer, MemoryMarshal.AsBytes(testData.AsSpan();
            uploadStopwatch.Stop();

            // Measure download bandwidth
            var result = new float[size];
            var downloadStopwatch = Stopwatch.StartNew();
            accelerator.Memory.CopyFromDevice(MemoryMarshal.AsBytes(result.AsSpan()), buffer);
            downloadStopwatch.Stop();

            var dataSize = size * sizeof(float);
            var uploadBandwidth = (dataSize / (double)uploadStopwatch.ElapsedTicks) * TimeSpan.TicksPerSecond / (1024 * 1024 * 1024);
            var downloadBandwidth = (dataSize / (double)downloadStopwatch.ElapsedTicks) * TimeSpan.TicksPerSecond / (1024 * 1024 * 1024);

            _output.WriteLine($"Metal memory bandwidth:");
            _output.WriteLine($"  Upload: {uploadBandwidth:F2} GB/s");
            _output.WriteLine($"  Download: {downloadBandwidth:F2} GB/s");
            
            // Apple GPUs should have reasonable bandwidth
            uploadBandwidth.Should().BeGreaterThan(0.1, "Upload bandwidth should be at least 100 MB/s");
            downloadBandwidth.Should().BeGreaterThan(0.1, "Download bandwidth should be at least 100 MB/s");
        }
        finally
        {
            accelerator.Memory.Free(buffer);
        }
    }

    private static async Task<float[]> ExecuteMetalVectorAddition(IAccelerator accelerator, int size)
    {
        var a = TestDataGenerator.GenerateFloatArray(size);
        var b = TestDataGenerator.GenerateFloatArray(size);
        
        var bufferA = accelerator.Memory.Allocate(size * sizeof(float));
        var bufferB = accelerator.Memory.Allocate(size * sizeof(float));
        var bufferResult = accelerator.Memory.Allocate(size * sizeof(float));

        try
        {
            accelerator.Memory.CopyToDevice(bufferA, MemoryMarshal.AsBytes(a.AsSpan();
            accelerator.Memory.CopyToDevice(bufferB, MemoryMarshal.AsBytes(b.AsSpan();

            var kernel = await accelerator.CompileKernelAsync(TestKernels.MetalVectorAdd);
            await kernel.ExecuteAsync(new KernelArguments(bufferA, bufferB, bufferResult, size));
            await accelerator.SynchronizeAsync();

            var result = new float[size];
            accelerator.Memory.CopyFromDevice(MemoryMarshal.AsBytes(result.AsSpan()), bufferResult);
            
            return result;
        }
        finally
        {
            accelerator.Memory.Free(bufferA);
            accelerator.Memory.Free(bufferB);
            accelerator.Memory.Free(bufferResult);
        }
    }
}

/// <summary>
/// Test kernels for Metal backend validation.
/// </summary>
public static class TestKernels
{
    public static readonly KernelDefinition MetalVectorAdd = new()
    {
        Name = "MetalVectorAdd",
        Code = System.Text.Encoding.UTF8.GetBytes(@"
#include <metal_stdlib>
using namespace metal;

kernel void MetalVectorAdd(device const float* a [[buffer(0)]],
                          device const float* b [[buffer(1)]],
                          device float* result [[buffer(2)]],
                          constant uint& size [[buffer(3)]],
                          uint index [[thread_position_in_grid]])
{
    if (index < size)
    {
        result[index] = a[index] + b[index];
    }
}"),
        EntryPoint = "MetalVectorAdd"
    };

    public static readonly KernelDefinition MetalVectorMultiply = new()
    {
        Name = "MetalVectorMultiply",
        Code = System.Text.Encoding.UTF8.GetBytes(@"
#include <metal_stdlib>
using namespace metal;

kernel void MetalVectorMultiply(device const float* a [[buffer(0)]],
                               device const float* b [[buffer(1)]],
                               device float* result [[buffer(2)]],
                               constant uint& size [[buffer(3)]],
                               uint index [[thread_position_in_grid]])
{
    if (index < size)
    {
        result[index] = a[index] * b[index];
    }
}"),
        EntryPoint = "MetalVectorMultiply"
    };

    public static readonly KernelDefinition MetalVectorScale = new()
    {
        Name = "MetalVectorScale",
        Code = System.Text.Encoding.UTF8.GetBytes(@"
#include <metal_stdlib>
using namespace metal;

kernel void MetalVectorScale(device const float* input [[buffer(0)]],
                            device float* output [[buffer(1)]],
                            constant float& scale [[buffer(2)]],
                            constant uint& size [[buffer(3)]],
                            uint index [[thread_position_in_grid]])
{
    if (index < size)
    {
        output[index] = input[index] * scale;
    }
}"),
        EntryPoint = "MetalVectorScale"
    };

    public static readonly KernelDefinition MetalMatrixMultiply = new()
    {
        Name = "MetalMatrixMultiply",
        Code = System.Text.Encoding.UTF8.GetBytes(@"
#include <metal_stdlib>
using namespace metal;

kernel void MetalMatrixMultiply(device const float* A [[buffer(0)]],
                               device const float* B [[buffer(1)]],
                               device float* C [[buffer(2)]],
                               constant uint& M [[buffer(3)]],
                               constant uint& N [[buffer(4)]],
                               constant uint& K [[buffer(5)]],
                               uint2 index [[thread_position_in_grid]])
{
    uint row = index.y;
    uint col = index.x;
    
    if (row < M && col < N)
    {
        float sum = 0.0f;
        for (uint i = 0; i < K; i++)
        {
            sum += A[row * K + i] * B[i * N + col];
        }
        C[row * N + col] = sum;
    }
}"),
        EntryPoint = "MetalMatrixMultiply"
    };
}

public class MetalTestFixture : IDisposable
{
    public ServiceProvider ServiceProvider { get; }
    public ILogger<MetalAcceleratorTests> Logger { get; }

    public MetalTestFixture()
    {
        var services = new ServiceCollection();
        
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && MetalUtilities.IsMetalAvailable())
        {
            services.AddMetalBackend(options =>
            {
                options.MaxMemoryAllocation = 1024 * 1024 * 1024; // 1GB for tests
                options.EnableMetalPerformanceShaders = true;
            });
        }

        ServiceProvider = services.BuildServiceProvider();
        Logger = ServiceProvider.GetRequiredService<ILogger<MetalAcceleratorTests>>();
    }

    public void Dispose()
    {
        ServiceProvider?.Dispose();
    }
}

// Helper attribute to skip tests when conditions aren't met
public static class Skip
{
    public static void IfNot(bool condition, string reason)
    {
        if (!condition)
        {
            throw new SkipException(reason);
        }
    }
}

public class SkipException : Exception
{
    public SkipException(string reason) : base(reason) { }
}}
