// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using FluentAssertions;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests;

/// <summary>
/// Comparison tests between Metal and CUDA performance patterns.
/// Validates that Metal backend performs comparably to CUDA patterns where applicable.
/// </summary>
[Trait("Category", "RequiresMetal")]
[Trait("Category", "Comparison")]
[Trait("Category", "Performance")]
public class MetalComparisonTests : MetalTestBase
{
    public MetalComparisonTests(ITestOutputHelper output) : base(output) { }

    [SkippableFact]
    public async Task Vector_Operations_Should_Match_CUDA_Patterns()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        // Test vector operations that are common in CUDA benchmarks
        const int elementCount = 4 * 1024 * 1024; // 4M elements
        const int iterations = 20;

        var hostA = MetalTestUtilities.TestDataGenerator.CreateRandomData(elementCount, seed: 1);
        var hostB = MetalTestUtilities.TestDataGenerator.CreateRandomData(elementCount, seed: 2);
        var hostResult = new float[elementCount];

        await using var deviceA = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceB = await accelerator.Memory.AllocateAsync<float>(elementCount);
        await using var deviceResult = await accelerator.Memory.AllocateAsync<float>(elementCount);

        await deviceA.CopyFromAsync(hostA.AsMemory());
        await deviceB.CopyFromAsync(hostB.AsMemory());

        var testOperations = new Dictionary<string, (string code, string entry)>
        {
            ["VectorAdd"] = (CreateVectorAddKernel(), "vector_add"),
            ["VectorMul"] = (CreateVectorMulKernel(), "vector_mul"),
            ["VectorSaxpy"] = (CreateVectorSaxpyKernel(), "vector_saxpy"),
            ["VectorDot"] = (CreateVectorReductionKernel(), "vector_reduction")
        };

        var results = new Dictionary<string, OperationResult>();

        foreach (var operation in testOperations)
        {
            var kernelDef = new KernelDefinition
            {
                Name = operation.Key,
                Code = operation.Value.code,
                EntryPoint = operation.Value.entry
            };

            var kernel = await accelerator.CompileKernelAsync(kernelDef);

            var threadgroups = (elementCount + 256 - 1) / 256;
            var gridSize = (threadgroups, 1, 1);
            var threadSize = (256, 1, 1);

            // Warm up
            if (operation.Key == "VectorDot")
            {
                await using var tempResult = await accelerator.Memory.AllocateAsync<float>(1);
                var warmupArgs = new KernelArguments { deviceA, deviceB, tempResult, (uint)elementCount };
                await kernel.LaunchAsync(gridSize, threadSize, warmupArgs);
            }
            else
            {
                var warmupArgs = CreateKernelArguments(operation.Key, deviceA, deviceB, deviceResult, elementCount);
                await kernel.LaunchAsync(gridSize, threadSize, warmupArgs);
            }
            await accelerator.SynchronizeAsync();

            // Measure performance
            var times = new double[iterations];

            for (var i = 0; i < iterations; i++)
            {
                var stopwatch = Stopwatch.StartNew();

                if (operation.Key == "VectorDot")
                {
                    await using var tempResult = await accelerator.Memory.AllocateAsync<float>(1);
                    var args = new KernelArguments { deviceA, deviceB, tempResult, (uint)elementCount };
                    await kernel.LaunchAsync(gridSize, threadSize, args);
                }
                else
                {
                    var args = CreateKernelArguments(operation.Key, deviceA, deviceB, deviceResult, elementCount);
                    await kernel.LaunchAsync(gridSize, threadSize, args);
                }

                await accelerator.SynchronizeAsync();
                stopwatch.Stop();
                times[i] = stopwatch.Elapsed.TotalSeconds;
            }

            var avgTime = times.Average();
            var minTime = times.Min();

            // Calculate bandwidth (bytes transferred per second)
            var bytesTransferred = operation.Key switch
            {
                "VectorAdd" or "VectorMul" => elementCount * sizeof(float) * 3L, // 2 reads + 1 write
                "VectorSaxpy" => elementCount * sizeof(float) * 3L, // 2 reads + 1 write
                "VectorDot" => elementCount * sizeof(float) * 2L, // 2 reads
                _ => elementCount * sizeof(float) * 3L
            };

            var avgBandwidth = bytesTransferred / (avgTime * 1024 * 1024 * 1024);
            var peakBandwidth = bytesTransferred / (minTime * 1024 * 1024 * 1024);

            results[operation.Key] = new OperationResult
            {
                AverageTime = avgTime * 1000, // Convert to ms
                MinTime = minTime * 1000,
                AverageBandwidth = avgBandwidth,
                PeakBandwidth = peakBandwidth
            };

            Output.WriteLine($"{operation.Key}:");
            Output.WriteLine($"  Average time: {avgTime * 1000:F3} ms");
            Output.WriteLine($"  Min time: {minTime * 1000:F3} ms");
            Output.WriteLine($"  Average bandwidth: {avgBandwidth:F2} GB/s");
            Output.WriteLine($"  Peak bandwidth: {peakBandwidth:F2} GB/s");
        }

        // Compare against expected CUDA-like performance patterns
        ValidateVectorOperationPerformance(results);
    }

    [SkippableFact]
    public async Task Matrix_Operations_Should_Scale_Like_CUDA()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        var matrixSizes = new[] { 256, 512, 1024 };
        var results = new Dictionary<int, MatrixResult>();

        foreach (var matrixSize in matrixSizes)
        {
            var elementCount = matrixSize * matrixSize;
            var requiredMemory = (long)elementCount * sizeof(float) * 3 * 2; // Safety margin

            if (accelerator.Info.TotalMemory < requiredMemory)
            {
                Output.WriteLine($"Skipping {matrixSize}x{matrixSize} due to insufficient memory");
                continue;
            }

            var hostA = MetalTestUtilities.TestDataGenerator.CreateRandomData(elementCount, seed: 1);
            var hostB = MetalTestUtilities.TestDataGenerator.CreateRandomData(elementCount, seed: 2);

            await using var deviceA = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var deviceB = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var deviceC = await accelerator.Memory.AllocateAsync<float>(elementCount);

            await deviceA.CopyFromAsync(hostA.AsMemory());
            await deviceB.CopyFromAsync(hostB.AsMemory());

            // Test both naive and optimized matrix multiply (like CUDA patterns)
            var naiveResult = await BenchmarkMatrixMultiply(accelerator, deviceA, deviceB, deviceC,
                matrixSize, "naive", CreateNaiveMatrixMultiply());

            var optimizedResult = await BenchmarkMatrixMultiply(accelerator, deviceA, deviceB, deviceC,
                matrixSize, "optimized", CreateOptimizedMatrixMultiply());

            results[matrixSize] = new MatrixResult
            {
                Size = matrixSize,
                NaiveGFLOPS = naiveResult.GFLOPS,
                OptimizedGFLOPS = optimizedResult.GFLOPS,
                SpeedupFactor = optimizedResult.GFLOPS / naiveResult.GFLOPS,
                NaiveTime = naiveResult.Time,
                OptimizedTime = optimizedResult.Time
            };

            Output.WriteLine($"Matrix {matrixSize}x{matrixSize}:");
            Output.WriteLine($"  Naive: {naiveResult.GFLOPS:F2} GFLOPS ({naiveResult.Time:F2} ms)");
            Output.WriteLine($"  Optimized: {optimizedResult.GFLOPS:F2} GFLOPS ({optimizedResult.Time:F2} ms)");
            Output.WriteLine($"  Speedup: {optimizedResult.GFLOPS / naiveResult.GFLOPS:F2}x");
        }

        // Validate scaling patterns match CUDA expectations
        ValidateMatrixScalingPatterns(results);
    }

    [SkippableFact]
    public async Task Memory_Access_Patterns_Should_Follow_GPU_Optimization_Rules()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        const int dataSize = 1024 * 1024; // 1M elements
        const int iterations = 10;

        await using var deviceData = await accelerator.Memory.AllocateAsync<float>(dataSize);

        var memoryPatterns = new Dictionary<string, string>
        {
            ["Coalesced"] = CreateCoalescedAccessKernel(),
            ["Strided"] = CreateStridedAccessKernel(),
            ["Random"] = CreateRandomAccessKernel(),
            ["Sequential"] = CreateSequentialAccessKernel()
        };

        var results = new Dictionary<string, double>();

        foreach (var pattern in memoryPatterns)
        {
            var kernelDef = new KernelDefinition
            {
                Name = $"memory_{pattern.Key.ToLowerInvariant()}",
                Code = pattern.Value,
                EntryPoint = $"memory_{pattern.Key.ToLowerInvariant()}"
            };

            var kernel = await accelerator.CompileKernelAsync(kernelDef);

            var threadgroups = (dataSize + 256 - 1) / 256;
            var gridSize = (threadgroups, 1, 1);
            var threadSize = (256, 1, 1);

            // Warm up
            var warmupArgs = new KernelArguments { deviceData, (uint)dataSize };
            await kernel.LaunchAsync(gridSize, threadSize, warmupArgs);
            await accelerator.SynchronizeAsync();

            // Measure
            var times = new double[iterations];
            for (var i = 0; i < iterations; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                var args = new KernelArguments { deviceData, (uint)dataSize };
                await kernel.LaunchAsync(gridSize, threadSize, args);
                await accelerator.SynchronizeAsync();
                stopwatch.Stop();
                times[i] = stopwatch.Elapsed.TotalSeconds;
            }

            var avgTime = times.Average();
            var bandwidth = (dataSize * sizeof(float) * 2) / (avgTime * 1024 * 1024 * 1024); // Read + write
            results[pattern.Key] = bandwidth;

            Output.WriteLine($"{pattern.Key} Access: {bandwidth:F2} GB/s ({avgTime * 1000:F3} ms)");
        }

        // Validate memory access pattern performance follows GPU optimization rules
        ValidateMemoryAccessPatterns(results);
    }

    [SkippableFact]
    public async Task Concurrent_Execution_Should_Show_GPU_Like_Parallelism()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        const int elementCount = 1024 * 1024;
        const int numStreams = 4;
        const int iterations = 5;

        // Create multiple workloads like CUDA stream processing
        var buffers = new List<(IUnifiedMemoryBuffer<float> input, IUnifiedMemoryBuffer<float> output)>();
        var hostData = new float[numStreams][];

        for (var i = 0; i < numStreams; i++)
        {
            var input = await accelerator.Memory.AllocateAsync<float>(elementCount);
            var output = await accelerator.Memory.AllocateAsync<float>(elementCount);
            buffers.Add((input, output));

            hostData[i] = MetalTestUtilities.TestDataGenerator.CreateRandomData(elementCount, seed: i + 1);
            await input.CopyFromAsync(hostData[i].AsMemory());
        }

        var kernel = await accelerator.CompileKernelAsync(MetalTestUtilities.KernelTemplates.VectorScale);
        var threadgroups = (elementCount + 256 - 1) / 256;
        var gridSize = (threadgroups, 1, 1);
        var threadSize = (256, 1, 1);

        try
        {
            // Sequential execution (like single CUDA stream)
            var sequentialTimes = new double[iterations];
            for (var iter = 0; iter < iterations; iter++)
            {
                var stopwatch = Stopwatch.StartNew();
                for (var i = 0; i < numStreams; i++)
                {
                    var args = new KernelArguments { buffers[i].input, buffers[i].output, 2.0f, (uint)elementCount };
                    await kernel.LaunchAsync(gridSize, threadSize, args);
                }
                await accelerator.SynchronizeAsync();
                stopwatch.Stop();
                sequentialTimes[iter] = stopwatch.Elapsed.TotalSeconds;
            }

            // Concurrent execution (like multiple CUDA streams)
            var concurrentTimes = new double[iterations];
            for (var iter = 0; iter < iterations; iter++)
            {
                var stopwatch = Stopwatch.StartNew();
                var tasks = new Task[numStreams];

                for (var i = 0; i < numStreams; i++)
                {
                    var streamIndex = i;
                    tasks[i] = Task.Run(async () =>
                    {
                        var args = new KernelArguments { buffers[streamIndex].input, buffers[streamIndex].output, 2.0f, (uint)elementCount };
                        await kernel.LaunchAsync(gridSize, threadSize, args);
                    });
                }

                await Task.WhenAll(tasks);
                await accelerator.SynchronizeAsync();
                stopwatch.Stop();
                concurrentTimes[iter] = stopwatch.Elapsed.TotalSeconds;
            }

            var avgSequential = sequentialTimes.Average();
            var avgConcurrent = concurrentTimes.Average();
            var parallelEfficiency = avgSequential / avgConcurrent;

            Output.WriteLine($"Concurrent Execution Analysis:");
            Output.WriteLine($"  Sequential execution: {avgSequential * 1000:F2} ms");
            Output.WriteLine($"  Concurrent execution: {avgConcurrent * 1000:F2} ms");
            Output.WriteLine($"  Parallel efficiency: {parallelEfficiency:F2}x");
            Output.WriteLine($"  Theoretical maximum: {numStreams}x");

            // Metal may not show as much parallelism as CUDA due to command buffer serialization
            // but should still show some improvement
            parallelEfficiency.Should().BeGreaterThan(1.0, "Concurrent execution should provide some benefit");

            // On Apple Silicon with unified memory, we might see better efficiency
            if (IsAppleSilicon())
            {
                parallelEfficiency.Should().BeGreaterThan(1.5, "Apple Silicon should show good parallel efficiency");
            }

            // Verify correctness
            for (var i = 0; i < numStreams; i++)
            {
                var result = new float[elementCount];
                await buffers[i].output.CopyToAsync(result.AsMemory());

                for (var j = 0; j < Math.Min(1000, elementCount); j++)
                {
                    var expected = hostData[i][j] * 2.0f;
                    Math.Abs(result[j] - expected).Should().BeLessThan(0.001f, $"Stream {i} result should be correct");
                }
            }
        }
        finally
        {
            // Cleanup
            foreach (var (input, output) in buffers)
            {
                await input.DisposeAsync();
                await output.DisposeAsync();
            }
        }
    }

    [SkippableFact]
    public async Task Occupancy_Patterns_Should_Match_GPU_Behavior()
    {
        Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

        await using var accelerator = Factory.CreateProductionAccelerator();
        accelerator.Should().NotBeNull();

        const int dataSize = 1024 * 1024;

        // Test different threadgroup sizes (like CUDA block sizes)
        var threadgroupSizes = new[] { 32, 64, 128, 256, 512, 1024 };
        var results = new Dictionary<int, double>();

        await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(dataSize);
        await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(dataSize);

        var testData = MetalTestUtilities.TestDataGenerator.CreateRandomData(dataSize);
        await deviceInput.CopyFromAsync(testData.AsMemory());

        var kernel = await accelerator.CompileKernelAsync(MetalTestUtilities.KernelTemplates.VectorScale);

        foreach (var threadgroupSize in threadgroupSizes)
        {
            try
            {
                var threadgroups = (dataSize + threadgroupSize - 1) / threadgroupSize;
                var gridSize = (threadgroups, 1, 1);
                var threadSize = (threadgroupSize, 1, 1);

                const int warmupIterations = 3;
                const int testIterations = 10;

                // Warm up
                for (var i = 0; i < warmupIterations; i++)
                {
                    var warmupArgs = new KernelArguments { deviceInput, deviceOutput, 1.5f, (uint)dataSize };
                    await kernel.LaunchAsync(gridSize, threadSize, warmupArgs);
                }
                await accelerator.SynchronizeAsync();

                // Measure
                var times = new double[testIterations];
                for (var i = 0; i < testIterations; i++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var args = new KernelArguments { deviceInput, deviceOutput, 1.5f, (uint)dataSize };
                    await kernel.LaunchAsync(gridSize, threadSize, args);
                    await accelerator.SynchronizeAsync();
                    stopwatch.Stop();
                    times[i] = stopwatch.Elapsed.TotalSeconds;
                }

                var avgTime = times.Average();
                var bandwidth = (dataSize * sizeof(float) * 2) / (avgTime * 1024 * 1024 * 1024);
                results[threadgroupSize] = bandwidth;

                Output.WriteLine($"Threadgroup size {threadgroupSize}: {bandwidth:F2} GB/s ({avgTime * 1000:F3} ms)");
            }
            catch (Exception ex)
            {
                Output.WriteLine($"Threadgroup size {threadgroupSize}: Failed - {ex.Message}");
                results[threadgroupSize] = 0.0;
            }
        }

        // Analyze occupancy patterns
        var bestThreadgroupSize = results.OrderByDescending(r => r.Value).First();
        var worstThreadgroupSize = results.Where(r => r.Value > 0).OrderBy(r => r.Value).First();
        var performanceRange = bestThreadgroupSize.Value / worstThreadgroupSize.Value;

        Output.WriteLine($"\nOccupancy Analysis:");
        Output.WriteLine($"  Best threadgroup size: {bestThreadgroupSize.Key} ({bestThreadgroupSize.Value:F2} GB/s)");
        Output.WriteLine($"  Worst threadgroup size: {worstThreadgroupSize.Key} ({worstThreadgroupSize.Value:F2} GB/s)");
        Output.WriteLine($"  Performance range: {performanceRange:F2}x");

        // Validate GPU-like occupancy behavior
        bestThreadgroupSize.Value.Should().BeGreaterThan(0, "Should achieve measurable performance");
        performanceRange.Should().BeGreaterThan(1.2, "Different threadgroup sizes should show performance variation");

        // Typical GPU behavior: very small or very large block sizes perform poorly
        if (results.ContainsKey(32) && results.ContainsKey(256))
        {
            results[256].Should().BeGreaterThan(results[32], "Larger threadgroup sizes typically perform better for memory-bound kernels");
        }
    }

    private async Task<(double GFLOPS, double Time)> BenchmarkMatrixMultiply(
        IAccelerator accelerator,
        IUnifiedMemoryBuffer<float> deviceA,
        IUnifiedMemoryBuffer<float> deviceB,
        IUnifiedMemoryBuffer<float> deviceC,
        int matrixSize,
        string variant,
        string kernelCode)
    {
        var kernelDef = new KernelDefinition
        {
            Name = $"matrix_multiply_{variant}",
            Code = kernelCode,
            EntryPoint = $"matrix_multiply_{variant}"
        };

        var kernel = await accelerator.CompileKernelAsync(kernelDef);

        var threadgroupSize = variant == "optimized" ? 16 : 32;
        var threadgroups = ((matrixSize + threadgroupSize - 1) / threadgroupSize,
                           (matrixSize + threadgroupSize - 1) / threadgroupSize, 1);
        var threadSize = (threadgroupSize, threadgroupSize, 1);

        // Warm up
        var warmupArgs = new KernelArguments { deviceA, deviceB, deviceC, (uint)matrixSize };
        await kernel.LaunchAsync(threadgroups, threadSize, warmupArgs);
        await accelerator.SynchronizeAsync();

        // Measure
        const int iterations = 3;
        var times = new double[iterations];

        for (var i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var args = new KernelArguments { deviceA, deviceB, deviceC, (uint)matrixSize };
            await kernel.LaunchAsync(threadgroups, threadSize, args);
            await accelerator.SynchronizeAsync();
            stopwatch.Stop();
            times[i] = stopwatch.Elapsed.TotalSeconds;
        }

        var avgTime = times.Average();
        var totalOps = 2.0 * matrixSize * matrixSize * matrixSize;
        var gflops = totalOps / (avgTime * 1e9);

        return (gflops, avgTime * 1000);
    }

    private static KernelArguments CreateKernelArguments(string operation,
        IUnifiedMemoryBuffer<float> deviceA, IUnifiedMemoryBuffer<float> deviceB,
        IUnifiedMemoryBuffer<float> deviceResult, int elementCount)
    {
        return operation switch
        {
            "VectorSaxpy" => new KernelArguments { deviceA, deviceB, deviceResult, 2.5f, (uint)elementCount },
            _ => new KernelArguments { deviceA, deviceB, deviceResult, (uint)elementCount }
        };
    }

    private void ValidateVectorOperationPerformance(Dictionary<string, OperationResult> results)
    {
        // Vector operations should achieve reasonable bandwidth
        foreach (var result in results)
        {
            result.Value.AverageBandwidth.Should().BeGreaterThan(50.0,
                $"{result.Key} should achieve reasonable memory bandwidth");
        }

        // Performance patterns should match GPU expectations
        if (results.ContainsKey("VectorAdd") && results.ContainsKey("VectorMul"))
        {
            // Addition and multiplication should have similar performance (both memory-bound)
            var addBandwidth = results["VectorAdd"].AverageBandwidth;
            var mulBandwidth = results["VectorMul"].AverageBandwidth;
            var ratio = Math.Max(addBandwidth, mulBandwidth) / Math.Min(addBandwidth, mulBandwidth);

            ratio.Should().BeLessThan(2.0, "Vector add and multiply should have similar performance");
        }
    }

    private void ValidateMatrixScalingPatterns(Dictionary<int, MatrixResult> results)
    {
        if (results.Count < 2)
            return;

        var sortedResults = results.OrderBy(r => r.Key).ToArray();

        // Performance should increase with matrix size (better GPU utilization)
        for (var i = 1; i < sortedResults.Length; i++)
        {
            var smaller = sortedResults[i - 1].Value;
            var larger = sortedResults[i].Value;

            larger.OptimizedGFLOPS.Should().BeGreaterThan(smaller.OptimizedGFLOPS * 0.8,
                "Larger matrices should achieve better performance due to better GPU utilization");
        }

        // Optimized version should outperform naive version
        foreach (var result in results.Values)
        {
            result.SpeedupFactor.Should().BeGreaterThan(1.2,
                "Optimized matrix multiply should be faster than naive version");
        }
    }

    private void ValidateMemoryAccessPatterns(Dictionary<string, double> results)
    {
        // Coalesced access should be fastest
        if (results.ContainsKey("Coalesced") && results.ContainsKey("Random"))
        {
            results["Coalesced"].Should().BeGreaterThan(results["Random"],
                "Coalesced memory access should be faster than random access");
        }

        // Strided access should be slower than sequential
        if (results.ContainsKey("Sequential") && results.ContainsKey("Strided"))
        {
            results["Sequential"].Should().BeGreaterThan(results["Strided"] * 0.8,
                "Sequential access should be faster than or similar to strided access");
        }

        // All patterns should achieve some reasonable bandwidth
        foreach (var result in results)
        {
            result.Value.Should().BeGreaterThan(10.0,
                $"{result.Key} access pattern should achieve reasonable bandwidth");
        }
    }

    // Kernel creation methods
    private static string CreateVectorAddKernel() => @"
        #include <metal_stdlib>
        using namespace metal;
        
        kernel void vector_add(
            device float* a [[buffer(0)]],
            device float* b [[buffer(1)]],
            device float* c [[buffer(2)]],
            constant uint& n [[buffer(3)]],
            uint id [[thread_position_in_grid]]
        ) {
            if (id >= n) return;
            c[id] = a[id] + b[id];
        }";

    private static string CreateVectorMulKernel() => @"
        #include <metal_stdlib>
        using namespace metal;
        
        kernel void vector_mul(
            device float* a [[buffer(0)]],
            device float* b [[buffer(1)]],
            device float* c [[buffer(2)]],
            constant uint& n [[buffer(3)]],
            uint id [[thread_position_in_grid]]
        ) {
            if (id >= n) return;
            c[id] = a[id] * b[id];
        }";

    private static string CreateVectorSaxpyKernel() => @"
        #include <metal_stdlib>
        using namespace metal;
        
        kernel void vector_saxpy(
            device float* a [[buffer(0)]],
            device float* b [[buffer(1)]],
            device float* c [[buffer(2)]],
            constant float& alpha [[buffer(3)]],
            constant uint& n [[buffer(4)]],
            uint id [[thread_position_in_grid]]
        ) {
            if (id >= n) return;
            c[id] = alpha * a[id] + b[id];
        }";

    private static string CreateVectorReductionKernel() => @"
        #include <metal_stdlib>
        using namespace metal;
        
        kernel void vector_reduction(
            device float* a [[buffer(0)]],
            device float* b [[buffer(1)]],
            device atomic<float>* result [[buffer(2)]],
            constant uint& n [[buffer(3)]],
            uint id [[thread_position_in_grid]]
        ) {
            if (id >= n) return;
            atomic_fetch_add_explicit(result, a[id] * b[id], memory_order_relaxed);
        }";

    private static string CreateNaiveMatrixMultiply() => @"
        #include <metal_stdlib>
        using namespace metal;
        
        kernel void matrix_multiply_naive(
            device float* A [[buffer(0)]],
            device float* B [[buffer(1)]],
            device float* C [[buffer(2)]],
            constant uint& N [[buffer(3)]],
            uint2 gid [[thread_position_in_grid]]
        ) {
            const uint row = gid.y;
            const uint col = gid.x;
            
            if (row >= N || col >= N) return;
            
            float sum = 0.0f;
            for (uint k = 0; k < N; k++) {
                sum += A[row * N + k] * B[k * N + col];
            }
            C[row * N + col] = sum;
        }";

    private static string CreateOptimizedMatrixMultiply() => @"
        #include <metal_stdlib>
        using namespace metal;
        
        kernel void matrix_multiply_optimized(
            device float* A [[buffer(0)]],
            device float* B [[buffer(1)]],
            device float* C [[buffer(2)]],
            constant uint& N [[buffer(3)]],
            uint2 gid [[thread_position_in_grid]],
            threadgroup float* shared_A [[threadgroup(0)]],
            threadgroup float* shared_B [[threadgroup(1)]]
        ) {
            const uint row = gid.y;
            const uint col = gid.x;
            const uint local_row = thread_position_in_threadgroup().y;
            const uint local_col = thread_position_in_threadgroup().x;
            const uint tile_size = 16;
            
            if (row >= N || col >= N) return;
            
            float sum = 0.0f;
            
            for (uint tile = 0; tile < (N + tile_size - 1) / tile_size; tile++) {
                if (row < N && tile * tile_size + local_col < N) {
                    shared_A[local_row * tile_size + local_col] = A[row * N + tile * tile_size + local_col];
                } else {
                    shared_A[local_row * tile_size + local_col] = 0.0f;
                }
                
                if (col < N && tile * tile_size + local_row < N) {
                    shared_B[local_row * tile_size + local_col] = B[(tile * tile_size + local_row) * N + col];
                } else {
                    shared_B[local_row * tile_size + local_col] = 0.0f;
                }
                
                threadgroup_barrier(mem_flags::mem_threadgroup);
                
                for (uint k = 0; k < tile_size; k++) {
                    sum += shared_A[local_row * tile_size + k] * shared_B[k * tile_size + local_col];
                }
                
                threadgroup_barrier(mem_flags::mem_threadgroup);
            }
            
            C[row * N + col] = sum;
        }";

    private static string CreateCoalescedAccessKernel() => @"
        #include <metal_stdlib>
        using namespace metal;
        
        kernel void memory_coalesced(
            device float* data [[buffer(0)]],
            constant uint& n [[buffer(1)]],
            uint id [[thread_position_in_grid]]
        ) {
            if (id >= n) return;
            data[id] = data[id] * 1.01f + 0.5f;
        }";

    private static string CreateStridedAccessKernel() => @"
        #include <metal_stdlib>
        using namespace metal;
        
        kernel void memory_strided(
            device float* data [[buffer(0)]],
            constant uint& n [[buffer(1)]],
            uint id [[thread_position_in_grid]]
        ) {
            uint stride = 16;
            uint index = (id * stride) % n;
            if (index >= n) return;
            data[index] = data[index] * 1.01f + 0.5f;
        }";

    private static string CreateRandomAccessKernel() => @"
        #include <metal_stdlib>
        using namespace metal;
        
        kernel void memory_random(
            device float* data [[buffer(0)]],
            constant uint& n [[buffer(1)]],
            uint id [[thread_position_in_grid]]
        ) {
            if (id >= n) return;
            uint random_index = (id * 1103515245u + 12345u) % n;
            data[random_index] = data[random_index] * 1.01f + 0.5f;
        }";

    private static string CreateSequentialAccessKernel() => @"
        #include <metal_stdlib>
        using namespace metal;
        
        kernel void memory_sequential(
            device float* data [[buffer(0)]],
            constant uint& n [[buffer(1)]],
            uint id [[thread_position_in_grid]]
        ) {
            if (id >= n - 1) return;
            data[id + 1] = data[id] * 1.01f + 0.5f;
        }";

    private record OperationResult
    {
        public double AverageTime { get; init; }
        public double MinTime { get; init; }
        public double AverageBandwidth { get; init; }
        public double PeakBandwidth { get; init; }
    }

    private record MatrixResult
    {
        public int Size { get; init; }
        public double NaiveGFLOPS { get; init; }
        public double OptimizedGFLOPS { get; init; }
        public double SpeedupFactor { get; init; }
        public double NaiveTime { get; init; }
        public double OptimizedTime { get; init; }
    }
}
