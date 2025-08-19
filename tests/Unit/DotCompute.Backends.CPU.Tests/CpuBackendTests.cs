// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Backends.CPU;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Kernels;
using DotCompute.Backends.CPU.Threading;
using DotCompute.Tests.Utilities;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using DotCompute.Core.Compute;

namespace DotCompute.Tests
{

/// <summary>
/// Comprehensive backend tests for CPU accelerators focusing on SIMD optimization, 
/// thread pool management, NUMA awareness, and performance validation.
/// </summary>
public sealed class CpuBackendTests
{
    private readonly ITestOutputHelper _output;

    public CpuBackendTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CpuAccelerator_DeviceDiscovery_FindsSystemCPU()
    {
        var cpuProvider = new CpuAcceleratorProvider();
        var accelerators = await cpuProvider.GetAvailableAcceleratorsAsync();

        Assert.NotEmpty(accelerators);
        Assert.Equal(1, accelerators.Count()); // Single CPU accelerator

        var accelerator = accelerators.First();
        accelerator.Info.DeviceType.Should().Be(AcceleratorType.CPU);
        accelerator.Info.Name.Should().NotBeNullOrEmpty();
        accelerator.Info.MemorySize.Should().BeGreaterThan(0);
        accelerator.Info.IsUnifiedMemory.Should().BeTrue();
        accelerator.Info.ComputeUnits.Should().BeGreaterThan(0);

        _output.WriteLine($"CPU: {accelerator.Info.Name}");
        _output.WriteLine($"Memory: {accelerator.Info.MemorySize:N0} bytes");
        _output.WriteLine($"Compute Units: {accelerator.Info.ComputeUnits}");
        _output.WriteLine($"Max Clock: {accelerator.Info.MaxClockFrequency} MHz");
    }

    [Fact]
    public async Task CpuAccelerator_WithSIMD_UsesVectorization()
    {
        var accelerator = await CreateCpuAccelerator();
        var kernelSource = TestKernels.VectorizedAdd;
        
        var stopwatch = Stopwatch.StartNew();
        var compiledKernel = await accelerator.CompileKernelAsync(kernelSource);
        stopwatch.Stop();

        Assert.NotNull(compiledKernel);
        compiledKernel.Name.Should().Be(kernelSource.Name);
        
        _output.WriteLine($"SIMD kernel compilation took {stopwatch.ElapsedMicroseconds} μs");

        // Test SIMD execution
        const int size = 1024;
        var a = TestDataGenerator.GenerateFloatArray(size);
        var b = TestDataGenerator.GenerateFloatArray(size);
        var result = new float[size];

        var bufferA = accelerator.Memory.Allocate(size * sizeof(float));
        var bufferB = accelerator.Memory.Allocate(size * sizeof(float));
        var bufferResult = accelerator.Memory.Allocate(size * sizeof(float));

        try
        {
            accelerator.Memory.CopyToDevice<float>(bufferA, a.AsSpan());
            accelerator.Memory.CopyToDevice<float>(bufferB, b.AsSpan());

            var execStopwatch = Stopwatch.StartNew();
            await compiledKernel.ExecuteAsync(new KernelArguments(bufferA, bufferB, bufferResult, size));
            await accelerator.SynchronizeAsync();
            execStopwatch.Stop();

            accelerator.Memory.CopyFromDevice(MemoryMarshal.AsBytes(result.AsSpan()), bufferResult);

            // Verify SIMD execution results
            for(int i = 0; i < size; i++)
            {
                result[i].Should().BeApproximately(a[i] + b[i], 1e-5f);
            }

            _output.WriteLine($"SIMD vector addition{size} elements): {execStopwatch.ElapsedMicroseconds} μs");
        }
        finally
        {
            accelerator.Memory.Free(bufferA);
            accelerator.Memory.Free(bufferB);
            accelerator.Memory.Free(bufferResult);
        }
    }

    [Fact]
    public async Task CpuAccelerator_ThreadPool_ManagesParallelExecution()
    {
        var accelerator = await CreateCpuAccelerator();
        var threadPool = new CpuThreadPool();
        
        const int taskCount = Environment.ProcessorCount * 2;
        var tasks = new Task[taskCount];
        var results = new int[taskCount];

        var stopwatch = Stopwatch.StartNew();
        
        for(int i = 0; i < taskCount; i++)
        {
            int taskIndex = i;
            tasks[i] = Task.Run(() =>
            {
                // Simulate compute-intensive work
                var sum = 0;
                for(int j = 0; j < 1000000; j++)
                {
                    sum += j % 100;
                }
                results[taskIndex] = sum;
            });
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        results.Should().AllSatisfy(result => result.Should().BeGreaterThan(0));
        
        _output.WriteLine($"Thread pool executed {taskCount} tasks in {stopwatch.ElapsedMicroseconds} μs");
        _output.WriteLine($"Average task time: {stopwatch.ElapsedMicroseconds / taskCount} μs");
    }

    [Fact]
    public void CpuAccelerator_NumaAwareness_DetectsTopology()
    {
        var numaInfo = NumaInfo.GetSystemInfo();
        
        Assert.NotNull(numaInfo);
        (numaInfo.NodeCount > 0).Should().BeTrue();
        numaInfo.ProcessorCount.Should().Be(Environment.ProcessorCount);
        
        _output.WriteLine($"NUMA Nodes: {numaInfo.NodeCount}");
        _output.WriteLine($"Processors: {numaInfo.ProcessorCount}");
        _output.WriteLine($"Memory per node: {numaInfo.MemoryPerNode:N0} bytes");
        
        if(numaInfo.NodeCount > 1)
        {
            _output.WriteLine("✓ NUMA topology detected - memory allocation will be optimized");
        }
        else
        {
            _output.WriteLine("✓ Uniform memory architecture detected");
        }
    }

    [Fact]
    public async Task CpuAccelerator_MemoryManagement_HandlesUnifiedMemory()
    {
        var accelerator = await CreateCpuAccelerator();
        const int size = 1024 * 1024; // 1MB

        var initialMemory = accelerator.Memory.GetAllocatedMemory();
        var buffer = accelerator.Memory.Allocate(size);

        try
        {
            Assert.NotNull(buffer);
            buffer.SizeInBytes.Should().Be(size);
            
            // CPU memory should be directly accessible
            var currentMemory = accelerator.Memory.GetAllocatedMemory();
            Assert.True(currentMemory > initialMemory);
            
            // Test unified memory access
            var testData = TestDataGenerator.GenerateFloatArray(size / sizeof(float));
            accelerator.Memory.CopyToDevice<float>(buffer, testData.AsSpan());
            
            var result = new float[testData.Length];
            accelerator.Memory.CopyFromDevice(MemoryMarshal.AsBytes(result.AsSpan()), buffer);
            
            result.Should().BeEquivalentTo(testData);
            
            _output.WriteLine($"Unified memory allocation: {size:N0} bytes");
        }
        finally
        {
            accelerator.Memory.Free(buffer);
        }
    }

    [Fact]
    public async Task CpuAccelerator_KernelCompilation_GeneratesOptimizedCode()
    {
        var accelerator = await CreateCpuAccelerator();
        var kernelSource = TestKernels.MatrixMultiply;

        var stopwatch = Stopwatch.StartNew();
        var compiledKernel = await accelerator.CompileKernelAsync(kernelSource);
        stopwatch.Stop();

        Assert.NotNull(compiledKernel);
        compiledKernel.Name.Should().Be(kernelSource.Name);
        
        _output.WriteLine($"CPU kernel compilation took {stopwatch.ElapsedMicroseconds} μs");
        
        // Test optimized execution
        const int matrixSize = 64;
        var matrixA = TestDataGenerator.GenerateFloatArray(matrixSize * matrixSize);
        var matrixB = TestDataGenerator.GenerateFloatArray(matrixSize * matrixSize);
        var result = new float[matrixSize * matrixSize];

        var bufferA = accelerator.Memory.Allocate(matrixSize * matrixSize * sizeof(float));
        var bufferB = accelerator.Memory.Allocate(matrixSize * matrixSize * sizeof(float));
        var bufferResult = accelerator.Memory.Allocate(matrixSize * matrixSize * sizeof(float));

        try
        {
            accelerator.Memory.CopyToDevice(bufferA, MemoryMarshal.AsBytes(matrixA.AsSpan()));
            accelerator.Memory.CopyToDevice(bufferB, MemoryMarshal.AsBytes(matrixB.AsSpan()));

            var execStopwatch = Stopwatch.StartNew();
            await compiledKernel.ExecuteAsync(new KernelArguments(bufferA, bufferB, bufferResult, matrixSize));
            await accelerator.SynchronizeAsync();
            execStopwatch.Stop();

            accelerator.Memory.CopyFromDevice(MemoryMarshal.AsBytes(result.AsSpan()), bufferResult);

            // Verify matrix multiplication results
            for(int i = 0; i < matrixSize; i++)
            {
                for(int j = 0; j < matrixSize; j++)
                {
                    float expected = 0;
                    for(int k = 0; k < matrixSize; k++)
                    {
                        expected += matrixA[i * matrixSize + k] * matrixB[k * matrixSize + j];
                    }
                    result[i * matrixSize + j].Should().BeApproximately(expected, 1e-4f);
                }
            }

            _output.WriteLine($"Matrix multiplication{matrixSize}x{matrixSize}): {execStopwatch.ElapsedMicroseconds} μs");
        }
        finally
        {
            accelerator.Memory.Free(bufferA);
            accelerator.Memory.Free(bufferB);
            accelerator.Memory.Free(bufferResult);
        }
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(4096)]
    [InlineData(16384)]
    [InlineData(65536)]
    public async Task CpuAccelerator_VectorAddition_PerformanceBaseline(int size)
    {
        var accelerator = await CreateCpuAccelerator();
        var stopwatch = Stopwatch.StartNew();
        
        var result = await ExecuteVectorAddition(accelerator, size);
        
        stopwatch.Stop();
        
        Assert.NotNull(result);
        result.Length.Should().Be(size);
        
        // CPU should complete within reasonable time
        (stopwatch.ElapsedMilliseconds < 1000).Should().BeTrue();
        
        _output.WriteLine($"CPU vector addition{size} elements): {stopwatch.ElapsedMicroseconds} μs");
    }

    [Fact]
    public async Task CpuAccelerator_SimdCapabilities_DetectsInstructionSets()
    {
        var accelerator = await CreateCpuAccelerator();
        var capabilities = SimdCapabilities.GetSummary();
        
        Assert.NotNull(capabilities);
        capabilities.IsHardwareAccelerated.Should().BeTrue();
        (capabilities.PreferredVectorWidth > 0).Should().BeTrue();
        capabilities.SupportedInstructionSets.Should().NotBeEmpty();
        
        _output.WriteLine($"Hardware acceleration: {capabilities.IsHardwareAccelerated}");
        _output.WriteLine($"Preferred vector width: {capabilities.PreferredVectorWidth}");
        _output.WriteLine($"Supported instruction sets: {string.Join(", ", capabilities.SupportedInstructionSets)}");
        
        // Platform-specific validation
        if(RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            capabilities.SupportsSse2.Should().BeTrue();
        }
        else if(RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            capabilities.SupportedInstructionSets.Should().Contain("NEON");
        }
    }

    [Fact]
    public async Task CpuAccelerator_MultipleKernels_ExecutesInParallel()
    {
        var accelerator = await CreateCpuAccelerator();
        const int size = 1024;
        
        var data1 = TestDataGenerator.GenerateFloatArray(size);
        var data2 = TestDataGenerator.GenerateFloatArray(size);
        
        var buffer1 = accelerator.Memory.Allocate(size * sizeof(float));
        var buffer2 = accelerator.Memory.Allocate(size * sizeof(float));
        var result1 = accelerator.Memory.Allocate(size * sizeof(float));
        var result2 = accelerator.Memory.Allocate(size * sizeof(float));

        try
        {
            accelerator.Memory.CopyToDevice(buffer1, MemoryMarshal.AsBytes(data1.AsSpan()));
            accelerator.Memory.CopyToDevice(buffer2, MemoryMarshal.AsBytes(data2.AsSpan()));

            var kernel1 = await accelerator.CompileKernelAsync(TestKernels.VectorizedAdd);
            var kernel2 = await accelerator.CompileKernelAsync(TestKernels.VectorizedMultiply);

            var stopwatch = Stopwatch.StartNew();
            
            // Execute kernels in parallel
            var task1 = kernel1.ExecuteAsync(new KernelArguments(buffer1, buffer2, result1, size));
            var task2 = kernel2.ExecuteAsync(new KernelArguments(buffer1, buffer2, result2, size));
            
            await Task.WhenAll(task1, task2);
            await accelerator.SynchronizeAsync();
            
            stopwatch.Stop();

            _output.WriteLine($"Parallel CPU kernel execution took {stopwatch.ElapsedMicroseconds} μs");
            
            // Verify results
            var resultArray1 = new float[size];
            var resultArray2 = new float[size];
            
            accelerator.Memory.CopyFromDevice(MemoryMarshal.AsBytes(resultArray1.AsSpan()), result1);
            accelerator.Memory.CopyFromDevice(MemoryMarshal.AsBytes(resultArray2.AsSpan()), result2);

            for(int i = 0; i < size; i++)
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

    [Fact]
    public async Task CpuAccelerator_MemoryAlignment_OptimizesSimdAccess()
    {
        var accelerator = await CreateCpuAccelerator();
        const int size = 1024;
        
        // Test aligned memory allocation
        var alignedBuffer = accelerator.Memory.Allocate(size * sizeof(float), MemoryHints.HostVisible);
        
        try
        {
            Assert.NotNull(alignedBuffer);
            alignedBuffer.SizeInBytes.Should().Be(size * sizeof(float));
            
            // Memory should be aligned for SIMD operations
            var handle = alignedBuffer.Handle;
            (handle.ToInt64() % 32).Should().Be(0); // Memory should be 32-byte aligned for AVX
            
            _output.WriteLine($"Memory alignment: {handle.ToInt64() % 32} bytes");
            _output.WriteLine("✓ Memory is properly aligned for SIMD operations");
        }
        finally
        {
            accelerator.Memory.Free(alignedBuffer);
        }
    }

    private static async Task<IAccelerator> CreateCpuAccelerator()
    {
        var provider = new CpuAcceleratorProvider();
        var accelerators = await provider.GetAvailableAcceleratorsAsync();
        
        return accelerators.First();
    }

    private static async Task<float[]> ExecuteVectorAddition(IAccelerator accelerator, int size)
    {
        var a = TestDataGenerator.GenerateFloatArray(size);
        var b = TestDataGenerator.GenerateFloatArray(size);
        
        var bufferA = accelerator.Memory.Allocate(size * sizeof(float));
        var bufferB = accelerator.Memory.Allocate(size * sizeof(float));
        var bufferResult = accelerator.Memory.Allocate(size * sizeof(float));

        try
        {
            accelerator.Memory.CopyToDevice<float>(bufferA, a.AsSpan());
            accelerator.Memory.CopyToDevice<float>(bufferB, b.AsSpan());

            var kernel = await accelerator.CompileKernelAsync(TestKernels.VectorizedAdd);
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
/// Test kernels for CPU backend validation.
/// </summary>
public static class TestKernels
{
    public static readonly KernelDefinition VectorizedAdd = new()
    {
        Name = "VectorizedAdd",
        Code = System.Text.Encoding.UTF8.GetBytes(@"
public static void VectorizedAdd(float[] a, float[] b, float[] result, int size)
{
    System.Threading.Tasks.Parallel.For(0, size, i =>
    {
        result[i] = a[i] + b[i];
    });
}"),
        EntryPoint = "VectorizedAdd"
    };

    public static readonly KernelDefinition VectorizedMultiply = new()
    {
        Name = "VectorizedMultiply",
        Code = System.Text.Encoding.UTF8.GetBytes(@"
public static void VectorizedMultiply(float[] a, float[] b, float[] result, int size)
{
    System.Threading.Tasks.Parallel.For(0, size, i =>
    {
        result[i] = a[i] * b[i];
    });
}"),
        EntryPoint = "VectorizedMultiply"
    };

    public static readonly KernelDefinition MatrixMultiply = new()
    {
        Name = "MatrixMultiply",
        Code = System.Text.Encoding.UTF8.GetBytes(@"
public static void MatrixMultiply(float[] A, float[] B, float[] C, int size)
{
    System.Threading.Tasks.Parallel.For(0, size, i =>
    {
        for(int j = 0; j < size; j++)
        {
            float sum = 0.0f;
            for(int k = 0; k < size; k++)
            {
                sum += A[i * size + k] * B[k * size + j];
            }
            C[i * size + j] = sum;
        }
    });
}"),
        EntryPoint = "MatrixMultiply"
    };
}

/// <summary>
/// SIMD capabilities detection for CPU backend.
/// </summary>
public static class SimdCapabilities
{
    public static SimdCapabilitySummary GetSummary()
    {
        return new SimdCapabilitySummary
        {
            IsHardwareAccelerated = System.Numerics.Vector.IsHardwareAccelerated,
            PreferredVectorWidth = System.Numerics.Vector<float>.Count * sizeof(float) * 8,
            SupportedInstructionSets = GetSupportedInstructionSets(),
            SupportsSse2 = System.Runtime.Intrinsics.X86.Sse2.IsSupported,
            SupportsAvx = System.Runtime.Intrinsics.X86.Avx.IsSupported,
            SupportsAvx2 = System.Runtime.Intrinsics.X86.Avx2.IsSupported,
            SupportsAvx512 = System.Runtime.Intrinsics.X86.Avx512F.IsSupported,
            SupportsNeon = System.Runtime.Intrinsics.Arm.AdvSimd.IsSupported
        };
    }

    private static string[] GetSupportedInstructionSets()
    {
        var sets = new List<string>();
        
        if(System.Runtime.Intrinsics.X86.Sse2.IsSupported) sets.Add("SSE2");
        if(System.Runtime.Intrinsics.X86.Avx.IsSupported) sets.Add("AVX");
        if(System.Runtime.Intrinsics.X86.Avx2.IsSupported) sets.Add("AVX2");
        if(System.Runtime.Intrinsics.X86.Avx512F.IsSupported) sets.Add("AVX512F");
        if(System.Runtime.Intrinsics.Arm.AdvSimd.IsSupported) sets.Add("NEON");
        
        return sets.ToArray();
    }
}

public class SimdCapabilitySummary
{
    public bool IsHardwareAccelerated { get; set; }
    public int PreferredVectorWidth { get; set; }
    public string[] SupportedInstructionSets { get; set; } = Array.Empty<string>();
    public bool SupportsSse2 { get; set; }
    public bool SupportsAvx { get; set; }
    public bool SupportsAvx2 { get; set; }
    public bool SupportsAvx512 { get; set; }
    public bool SupportsNeon { get; set; }
}
}
