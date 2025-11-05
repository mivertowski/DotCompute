// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using DotCompute.Backends.Metal.Native;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests
{
    /// <summary>
    /// Performance benchmark tests for Metal operations.
    /// Tests real-world performance scenarios and validates against expected performance metrics.
    /// </summary>
    [Trait("Category", "Hardware")]
    [Trait("Category", "Performance")]
    [Trait("Category", "Metal")]
    public class MetalPerformanceBenchmarkTests : MetalTestBase
    {
        private const string MemoryBandwidthShader = @"
#include <metal_stdlib>
using namespace metal;

kernel void memoryBandwidthTest(device float4* input [[ buffer(0) ]],
                               device float4* output [[ buffer(1) ]],
                               uint gid [[ thread_position_in_grid ]])
{
    float4 data = input[gid];
    // Simple computation to ensure memory access
    data.x = data.x * 1.1f + data.y;
    data.y = data.y * 1.1f + data.z;
    data.z = data.z * 1.1f + data.w;
    data.w = data.w * 1.1f + data.x;
    output[gid] = data;
}";

        private const string ComputeIntensiveShader = @"
#include <metal_stdlib>
using namespace metal;

kernel void computeIntensiveTest(device float* input [[ buffer(0) ]],
                                device float* output [[ buffer(1) ]],
                                uint gid [[ thread_position_in_grid ]])
{
    float value = input[gid];
    // Compute intensive operations
    for (int i = 0; i < 1000; i++) {
        value = sin(value) * cos(value) + sqrt(abs(value));
    }
    output[gid] = value;
}";

        private const string MatrixMultiplyOptimizedShader = @"
#include <metal_stdlib>
using namespace metal;

kernel void matrixMultiplyOptimized(device const float* A [[ buffer(0) ]],
                                   device const float* B [[ buffer(1) ]],
                                   device float* C [[ buffer(2) ]],
                                   constant uint& N [[ buffer(3) ]],
                                   uint2 gid [[ thread_position_in_grid ]])
{
    uint row = gid.y;
    uint col = gid.x;
    
    if (row >= N || col >= N) return;
    
    float sum = 0.0f;
    for (uint k = 0; k < N; k++) {
        sum += A[row * N + k] * B[k * N + col];
    }
    
    C[row * N + col] = sum;
}";

        private const string VectorAddShader = @"
#include <metal_stdlib>
using namespace metal;

kernel void vectorAdd(device const float* a [[ buffer(0) ]],
                     device const float* b [[ buffer(1) ]],
                     device float* c [[ buffer(2) ]],
                     uint gid [[ thread_position_in_grid ]])
{
    c[gid] = a[gid] + b[gid];
}";

        public MetalPerformanceBenchmarkTests(ITestOutputHelper output) : base(output) { }

        [Fact(Skip = "Test uses low-level MetalNative APIs not in current implementation")]
        public async Task Memory_Bandwidth_Benchmark_Should_Achieve_Expected_Performance()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");
            Skip.IfNot(IsAppleSilicon(), "Performance benchmarks optimized for Apple Silicon");

            await LogMetalDeviceCapabilitiesAsync();

            const int elementCount = 16 * 1024 * 1024; // 16M float4s = 256MB
            const int iterations = 20;
            const int warmupIterations = 5;

            var device = MetalNative.CreateSystemDefaultDevice();
            Skip.If(device == IntPtr.Zero, "Metal device creation failed");

            try
            {
                var commandQueue = MetalNative.CreateCommandQueue(device);
                Skip.If(commandQueue == IntPtr.Zero, "Command queue creation failed");

                try
                {
                    var library = MetalNative.CreateLibraryWithSource(device, MemoryBandwidthShader);
                    Skip.If(library == IntPtr.Zero, "Shader compilation failed");

                    try
                    {
                        var function = MetalNative.GetFunction(library, "memoryBandwidthTest");
                        Skip.If(function == IntPtr.Zero, "Function retrieval failed");

                        try
                        {
                            var error = IntPtr.Zero;
                            var pipelineState = MetalNative.CreateComputePipelineState(device, function, ref error);
                            Skip.If(pipelineState == IntPtr.Zero, "Pipeline state creation failed");

                            try
                            {
                                // Create buffers
                                var inputBufferSize = (nuint)(elementCount * 4 * sizeof(float)); // float4
                                var outputBufferSize = inputBufferSize;

                                var inputBuffer = MetalNative.CreateBuffer(device, inputBufferSize, 0);
                                var outputBuffer = MetalNative.CreateBuffer(device, outputBufferSize, 0);

                                Skip.If(inputBuffer == IntPtr.Zero || outputBuffer == IntPtr.Zero, "Buffer creation failed");

                                try
                                {
                                    // Initialize input data
                                    var inputPtr = MetalNative.GetBufferContents(inputBuffer);
                                    var inputData = MetalTestDataGenerator.CreateRandomData(elementCount * 4);
                                    unsafe
                                    {
                                        Marshal.Copy(inputData, 0, inputPtr, inputData.Length);
                                    }

                                    var measure = new MetalPerformanceMeasurement("Memory Bandwidth", Output);

                                    // Warmup
                                    for (var i = 0; i < warmupIterations; i++)
                                    {
                                        ExecuteKernel(commandQueue, pipelineState, inputBuffer, outputBuffer, elementCount);
                                    }

                                    var times = new double[iterations];

                                    for (var i = 0; i < iterations; i++)
                                    {
                                        measure.Start();
                                        ExecuteKernel(commandQueue, pipelineState, inputBuffer, outputBuffer, elementCount);
                                        measure.Stop();
                                        times[i] = measure.ElapsedTime.TotalSeconds;
                                    }

                                    var avgTime = times.Average();
                                    var minTime = times.Min();
                                    var maxTime = times.Max();

                                    // Calculate bandwidth (read + write)
                                    var bytesPerIteration = elementCount * 4 * sizeof(float) * 2; // read input + write output
                                    var avgBandwidthGBps = bytesPerIteration / (avgTime * 1024 * 1024 * 1024);
                                    var peakBandwidthGBps = bytesPerIteration / (minTime * 1024 * 1024 * 1024);

                                    // Apple Silicon unified memory should achieve high bandwidth
                                    var expectedBandwidth = GetExpectedMemoryBandwidth();
                                    var achievedRatio = avgBandwidthGBps / expectedBandwidth;

                                    Output.WriteLine($"Metal Memory Bandwidth Benchmark Results:");
                                    Output.WriteLine($"  Data Size: {bytesPerIteration / (1024 * 1024):F1} MB per iteration");
                                    Output.WriteLine($"  Iterations: {iterations} (after {warmupIterations} warmup)");
                                    Output.WriteLine($"  Average Time: {avgTime * 1000:F2} ms");
                                    Output.WriteLine($"  Min/Max Time: {minTime * 1000:F2} / {maxTime * 1000:F2} ms");
                                    Output.WriteLine($"  Average Bandwidth: {avgBandwidthGBps:F1} GB/s");
                                    Output.WriteLine($"  Peak Bandwidth: {peakBandwidthGBps:F1} GB/s");
                                    Output.WriteLine($"  Expected Bandwidth: {expectedBandwidth:F1} GB/s");
                                    Output.WriteLine($"  Efficiency: {achievedRatio:P1}");

                                    // Performance validations for Apple Silicon unified memory
                                    avgBandwidthGBps.Should().BeGreaterThan(100, "Apple Silicon should achieve high memory bandwidth");
                                    achievedRatio.Should().BeGreaterThan(0.5, "Should achieve at least 50% of theoretical bandwidth");
                                    peakBandwidthGBps.Should().BeGreaterThan(avgBandwidthGBps, "Peak should exceed average");
                                }
                                finally
                                {
                                    if (inputBuffer != IntPtr.Zero)
                                        MetalNative.ReleaseBuffer(inputBuffer);
                                    if (outputBuffer != IntPtr.Zero)
                                        MetalNative.ReleaseBuffer(outputBuffer);
                                }
                            }
                            finally
                            {
                                MetalNative.ReleaseComputePipelineState(pipelineState);
                            }
                        }
                        finally
                        {
                            MetalNative.ReleaseFunction(function);
                        }
                    }
                    finally
                    {
                        MetalNative.ReleaseLibrary(library);
                    }
                }
                finally
                {
                    MetalNative.ReleaseCommandQueue(commandQueue);
                }
            }
            finally
            {
                MetalNative.ReleaseDevice(device);
            }
        }

        [Fact(Skip = "Test uses low-level MetalNative APIs not in current implementation")]
        public void Compute_Performance_Benchmark_Should_Meet_Expectations()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");

            const int elementCount = 1024 * 1024; // 1M elements
            const int iterations = 10;
            const int operationsPerElement = 1000;

            var device = MetalNative.CreateSystemDefaultDevice();
            Skip.If(device == IntPtr.Zero, "Metal device creation failed");

            try
            {
                var commandQueue = MetalNative.CreateCommandQueue(device);
                var library = MetalNative.CreateLibraryWithSource(device, ComputeIntensiveShader);
                var function = MetalNative.GetFunction(library, "computeIntensiveTest");

                var error = IntPtr.Zero;
                var pipelineState = MetalNative.CreateComputePipelineState(device, function, ref error);

                Skip.If(pipelineState == IntPtr.Zero, "Pipeline state creation failed");

                try
                {
                    var inputData = MetalTestDataGenerator.CreateRandomData(elementCount, seed: 42, min: 0.1f, max: 10.0f);

                    var bufferSize = (nuint)(elementCount * sizeof(float));
                    var inputBuffer = MetalNative.CreateBuffer(device, bufferSize, 0);
                    var outputBuffer = MetalNative.CreateBuffer(device, bufferSize, 0);

                    // Initialize input data
                    var inputPtr = MetalNative.GetBufferContents(inputBuffer);
                    unsafe
                    {
                        Marshal.Copy(inputData, 0, inputPtr, inputData.Length);
                    }

                    // Warmup
                    ExecuteKernel(commandQueue, pipelineState, inputBuffer, outputBuffer, elementCount);

                    var times = new double[iterations];

                    for (var i = 0; i < iterations; i++)
                    {
                        var stopwatch = Stopwatch.StartNew();
                        ExecuteKernel(commandQueue, pipelineState, inputBuffer, outputBuffer, elementCount);
                        stopwatch.Stop();
                        times[i] = stopwatch.Elapsed.TotalSeconds;
                    }

                    var avgTime = times.Average();
                    var totalOperations = (long)elementCount * operationsPerElement * iterations;
                    var gflops = totalOperations / (avgTime * iterations * 1e9);

                    // Verify computation completed
                    var outputData = new float[elementCount];
                    var outputPtr = MetalNative.GetBufferContents(outputBuffer);
                    unsafe
                    {
                        Marshal.Copy(outputPtr, outputData, 0, outputData.Length);
                    }

                    var significantDifferences = 0;
                    for (var i = 0; i < Math.Min(1000, elementCount); i++)
                    {
                        if (Math.Abs(outputData[i] - inputData[i]) > 0.1f)
                        {
                            significantDifferences++;
                        }
                    }

                    Output.WriteLine($"Metal Compute Performance Benchmark:");
                    Output.WriteLine($"  Elements: {elementCount:N0}");
                    Output.WriteLine($"  Operations per Element: {operationsPerElement:N0}");
                    Output.WriteLine($"  Total Operations: {totalOperations:N0}");
                    Output.WriteLine($"  Average Time: {avgTime * 1000:F2} ms");
                    Output.WriteLine($"  Performance: {gflops:F2} GFLOPS");
                    Output.WriteLine($"  Verification: {significantDifferences}/1000 elements changed significantly");

                    gflops.Should().BeGreaterThan(10.0, "Apple Silicon should achieve reasonable compute performance");
                    significantDifferences.Should().BeGreaterThan(900, "Most elements should be modified by computation");

                    MetalNative.ReleaseBuffer(inputBuffer);
                    MetalNative.ReleaseBuffer(outputBuffer);
                }
                finally
                {
                    MetalNative.ReleaseComputePipelineState(pipelineState);
                    MetalNative.ReleaseFunction(function);
                    MetalNative.ReleaseLibrary(library);
                    MetalNative.ReleaseCommandQueue(commandQueue);
                }
            }
            finally
            {
                MetalNative.ReleaseDevice(device);
            }
        }

        [Fact(Skip = "Test uses low-level MetalNative APIs not in current implementation")]
        public void Matrix_Multiply_Performance_Should_Be_Optimized()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");

            var matrixSizes = new[] { 512, 1024 }; // Smaller sizes due to different optimization approach

            foreach (var matrixSize in matrixSizes)
            {
                BenchmarkMatrixMultiply(matrixSize);
            }
        }

        private void BenchmarkMatrixMultiply(int matrixSize)
        {
            const int iterations = 5;
            var elementCount = matrixSize * matrixSize;

            var device = MetalNative.CreateSystemDefaultDevice();
            var commandQueue = MetalNative.CreateCommandQueue(device);
            var library = MetalNative.CreateLibraryWithSource(device, MatrixMultiplyOptimizedShader);
            var function = MetalNative.GetFunction(library, "matrixMultiplyOptimized");

            var error = IntPtr.Zero;
            var pipelineState = MetalNative.CreateComputePipelineState(device, function, ref error);

            try
            {
                var hostA = MetalTestDataGenerator.CreateRandomData(elementCount, seed: 42);
                var hostB = MetalTestDataGenerator.CreateRandomData(elementCount, seed: 43);

                var bufferSize = (nuint)(elementCount * sizeof(float));
                var deviceA = MetalNative.CreateBuffer(device, bufferSize, 0);
                var deviceB = MetalNative.CreateBuffer(device, bufferSize, 0);
                var deviceC = MetalNative.CreateBuffer(device, bufferSize, 0);

                // Initialize input data
                unsafe
                {
                    var ptrA = MetalNative.GetBufferContents(deviceA);
                    var ptrB = MetalNative.GetBufferContents(deviceB);
                    Marshal.Copy(hostA, 0, ptrA, hostA.Length);
                    Marshal.Copy(hostB, 0, ptrB, hostB.Length);
                }

                // Warmup
                ExecuteMatrixMultiply(commandQueue, pipelineState, deviceA, deviceB, deviceC, matrixSize);

                var times = new double[iterations];

                for (var i = 0; i < iterations; i++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    ExecuteMatrixMultiply(commandQueue, pipelineState, deviceA, deviceB, deviceC, matrixSize);
                    stopwatch.Stop();
                    times[i] = stopwatch.Elapsed.TotalSeconds;
                }

                var avgTime = times.Average();
                var minTime = times.Min();

                // Calculate GFLOPS (2 * N^3 operations for matrix multiply)
                var totalOps = 2.0 * matrixSize * matrixSize * matrixSize;
                var avgGflops = totalOps / (avgTime * 1e9);
                var peakGflops = totalOps / (minTime * 1e9);

                // Verify results
                var hostResult = new float[elementCount];
                var resultPtr = MetalNative.GetBufferContents(deviceC);
                unsafe
                {
                    Marshal.Copy(resultPtr, hostResult, 0, hostResult.Length);
                }

                // Quick verification - compute first few elements on CPU
                for (var row = 0; row < Math.Min(3, matrixSize); row++)
                {
                    for (var col = 0; col < Math.Min(3, matrixSize); col++)
                    {
                        var expected = 0.0f;
                        for (var k = 0; k < matrixSize; k++)
                        {
                            expected += hostA[row * matrixSize + k] * hostB[k * matrixSize + col];
                        }

                        var actual = hostResult[row * matrixSize + col];
                        Math.Abs(actual - expected).Should().BeLessThan(0.01f,
                            $"Matrix multiply result incorrect at ({row},{col})");
                    }
                }

                Output.WriteLine($"Metal Matrix Multiply ({matrixSize}x{matrixSize}):");
                Output.WriteLine($"  Average Time: {avgTime * 1000:F2} ms");
                Output.WriteLine($"  Min Time: {minTime * 1000:F2} ms");
                Output.WriteLine($"  Average Performance: {avgGflops:F1} GFLOPS");
                Output.WriteLine($"  Peak Performance: {peakGflops:F1} GFLOPS");
                Output.WriteLine($"  Memory Usage: {elementCount * 3 * sizeof(float) / (1024 * 1024):F1} MB");

                // Performance expectations for Apple Silicon
                var expectedMinGflops = matrixSize switch
                {
                    512 => 50.0,   // Reasonable for smaller matrices
                    1024 => 100.0, // Good utilization expected
                    _ => 25.0
                };

                avgGflops.Should().BeGreaterThan(expectedMinGflops,
                    $"Matrix multiply should achieve reasonable performance for {matrixSize}x{matrixSize}");

                MetalNative.ReleaseBuffer(deviceA);
                MetalNative.ReleaseBuffer(deviceB);
                MetalNative.ReleaseBuffer(deviceC);
            }
            finally
            {
                MetalNative.ReleaseComputePipelineState(pipelineState);
                MetalNative.ReleaseFunction(function);
                MetalNative.ReleaseLibrary(library);
                MetalNative.ReleaseCommandQueue(commandQueue);
                MetalNative.ReleaseDevice(device);
            }
        }

        [Fact(Skip = "Test uses low-level MetalNative APIs")]
        public void Transfer_Performance_Should_Meet_Unified_Memory_Expectations()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");
            Skip.IfNot(IsAppleSilicon(), "Unified memory architecture tests require Apple Silicon");

            var transferSizes = new[] { 1, 4, 16, 64, 256 }; // MB
            const int iterations = 10;

            Output.WriteLine("Metal Unified Memory Performance Benchmark:");
            Output.WriteLine("Size (MB)\tCopy (GB/s)\tZero-Copy Latency (μs)");

            var device = MetalNative.CreateSystemDefaultDevice();

            try
            {
                foreach (var sizeMB in transferSizes)
                {
                    var sizeBytes = sizeMB * 1024 * 1024;
                    var elementCount = sizeBytes / sizeof(float);

                    var hostData = MetalTestDataGenerator.CreateRandomData(elementCount);
                    var bufferSize = (nuint)sizeBytes;
                    var deviceBuffer = MetalNative.CreateBuffer(device, bufferSize, 0);

                    try
                    {
                        // Memory copy performance (simulating traditional GPU transfer)
                        var copyTimes = new double[iterations];
                        for (var i = 0; i < iterations; i++)
                        {
                            var stopwatch = Stopwatch.StartNew();
                            var bufferPtr = MetalNative.GetBufferContents(deviceBuffer);
                            unsafe
                            {
                                Marshal.Copy(hostData, 0, bufferPtr, hostData.Length);
                            }
                            stopwatch.Stop();
                            copyTimes[i] = stopwatch.Elapsed.TotalSeconds;
                        }
                        var copyBandwidth = sizeBytes / (copyTimes.Average() * 1024 * 1024 * 1024);

                        // Zero-copy latency (accessing unified memory directly)
                        var accessTimes = new double[iterations];
                        for (var i = 0; i < iterations; i++)
                        {
                            var stopwatch = Stopwatch.StartNew();
                            var bufferPtr = MetalNative.GetBufferContents(deviceBuffer);
                            // Simulate simple access pattern
                            unsafe
                            {
                                var floatPtr = (float*)bufferPtr;
                                var sum = 0.0f;
                                for (var j = 0; j < Math.Min(1000, elementCount); j += 100)
                                {
                                    sum += floatPtr[j];
                                }
                            }
                            stopwatch.Stop();
                            accessTimes[i] = stopwatch.Elapsed.TotalMicroseconds;
                        }
                        var avgAccessLatency = accessTimes.Average();

                        Output.WriteLine($"{sizeMB}\t\t{copyBandwidth:F2}\t\t{avgAccessLatency:F2}");

                        // Unified memory should show excellent performance characteristics
                        if (sizeMB >= 16)
                        {
                            copyBandwidth.Should().BeGreaterThan(20.0,
                                $"Unified memory copy bandwidth should be high for {sizeMB}MB");
                        }

                        avgAccessLatency.Should().BeLessThan(100.0,
                            $"Zero-copy access latency should be low for {sizeMB}MB");
                    }
                    finally
                    {
                        MetalNative.ReleaseBuffer(deviceBuffer);
                    }
                }
            }
            finally
            {
                MetalNative.ReleaseDevice(device);
            }
        }

        private void ExecuteKernel(IntPtr commandQueue, IntPtr pipelineState, IntPtr inputBuffer, IntPtr outputBuffer, int elementCount)
        {
            throw new NotImplementedException("This method uses low-level MetalNative APIs that are not fully implemented");
        }

        private void ExecuteMatrixMultiply(IntPtr commandQueue, IntPtr pipelineState, IntPtr bufferA, IntPtr bufferB, IntPtr bufferC, int matrixSize)
        {
            throw new NotImplementedException("This method uses low-level MetalNative APIs that are not fully implemented");
        }

        private double GetExpectedMemoryBandwidth()
        {
            // Apple Silicon unified memory bandwidth estimates
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? 200.0 : 50.0;
        }
    }
}
