// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Backends.Metal.Native;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using System.Runtime.InteropServices;

namespace DotCompute.Hardware.Metal.Tests
{
    /// <summary>
    /// Concurrency and multi-threading tests for Metal backend.
    /// Tests parallel kernel execution, command queue management, and thread safety.
    /// </summary>
    [Trait("Category", "Hardware")]
    [Trait("Category", "Concurrency")]
    [Trait("Category", "Metal")]
    public class MetalConcurrencyTests : MetalTestBase
    {
        private const string ConcurrentKernelShader = @"
#include <metal_stdlib>
using namespace metal;

kernel void concurrentAdd(device const float* a [[ buffer(0) ]],
                         device const float* b [[ buffer(1) ]],
                         device float* result [[ buffer(2) ]],
                         constant uint& offset [[ buffer(3) ]],
                         uint gid [[ thread_position_in_grid ]])
{
    uint idx = gid + offset;
    result[idx] = a[idx] + b[idx] + offset * 0.1f; // Add offset for uniqueness
}";

        private const string WorkloadShader = @"
#include <metal_stdlib>
using namespace metal;

kernel void workloadTest(device const float* input [[ buffer(0) ]],
                        device float* output [[ buffer(1) ]],
                        constant uint& workloadSize [[ buffer(2) ]],
                        uint gid [[ thread_position_in_grid ]])
{
    float value = input[gid];
    
    // Variable workload based on workloadSize
    for (uint i = 0; i < workloadSize; i++) {
        value = sin(value) * cos(value) + sqrt(abs(value));
    }
    
    output[gid] = value;
}";

        private const string AtomicOperationsShader = @"
#include <metal_stdlib>
using namespace metal;

kernel void atomicCounter(device atomic_uint* counter [[ buffer(0) ]],
                         device uint* results [[ buffer(1) ]],
                         uint gid [[ thread_position_in_grid ]])
{
    // Each thread increments counter and stores the old value
    uint oldValue = atomic_fetch_add_explicit(counter, 1, memory_order_relaxed);
    results[gid] = oldValue;
}";

        public MetalConcurrencyTests(ITestOutputHelper output) : base(output) { }

        [Fact(Skip = "Test uses low-level MetalNative APIs not in current implementation")]
        public void Multiple_Command_Queues_Should_Execute_Concurrently()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");

            const int numQueues = 4;
            const int elementsPerQueue = 256 * 1024; // 256K elements per queue
            const int iterations = 10;

            var device = MetalNative.CreateSystemDefaultDevice();
            Skip.If(device == IntPtr.Zero, "Metal device creation failed");

            try
            {
                var library = MetalNative.CreateLibraryWithSource(device, ConcurrentKernelShader);
                var function = MetalNative.GetFunction(library, "concurrentAdd");
                var error = IntPtr.Zero;
                var pipelineState = MetalNative.CreateComputePipelineState(device, function, ref error);
                Skip.If(pipelineState == IntPtr.Zero, "Pipeline state creation failed");

                try
                {
                    // Create multiple command queues
                    var commandQueues = new IntPtr[numQueues];
                    for (var i = 0; i < numQueues; i++)
                    {
                        commandQueues[i] = MetalNative.CreateCommandQueue(device);
                        Skip.If(commandQueues[i] == IntPtr.Zero, $"Command queue {i} creation failed");
                    }

                    try
                    {
                        var sequentialTimes = new double[iterations];
                        var concurrentTimes = new double[iterations];

                        for (var iter = 0; iter < iterations; iter++)
                        {
                            // Test sequential execution
                            var sequentialMeasure = new MetalPerformanceMeasurement("Sequential", Output);
                            sequentialMeasure.Start();

                            for (var queue = 0; queue < numQueues; queue++)
                            {
                                ExecuteQueueWork(commandQueues[queue], pipelineState, device, 
                                               elementsPerQueue, (uint)queue);
                            }

                            sequentialMeasure.Stop();
                            sequentialTimes[iter] = sequentialMeasure.ElapsedTime.TotalSeconds;

                            // Test concurrent execution
                            var concurrentMeasure = new MetalPerformanceMeasurement("Concurrent", Output);
                            concurrentMeasure.Start();

                            var tasks = new Task[numQueues];
                            for (var queue = 0; queue < numQueues; queue++)
                            {
                                var queueIndex = queue; // Capture for closure
                                tasks[queue] = Task.Run(() =>
                                {
                                    ExecuteQueueWork(commandQueues[queueIndex], pipelineState, device,
                                                   elementsPerQueue, (uint)queueIndex);
                                });
                            }

                            Task.WaitAll(tasks);
                            concurrentMeasure.Stop();
                            concurrentTimes[iter] = concurrentMeasure.ElapsedTime.TotalSeconds;
                        }

                        var avgSequential = sequentialTimes.Average();
                        var avgConcurrent = concurrentTimes.Average();
                        var speedup = avgSequential / avgConcurrent;

                        Output.WriteLine($"Multi-Queue Concurrency Results:");
                        Output.WriteLine($"  Number of Queues: {numQueues}");
                        Output.WriteLine($"  Elements per Queue: {elementsPerQueue:N0}");
                        Output.WriteLine($"  Iterations: {iterations}");
                        Output.WriteLine($"  Sequential Time: {avgSequential * 1000:F2} ms");
                        Output.WriteLine($"  Concurrent Time: {avgConcurrent * 1000:F2} ms");
                        Output.WriteLine($"  Speedup: {speedup:F2}x");

                        // Metal should show some concurrency benefit
                        speedup.Should().BeGreaterThan(1.2, "Multiple command queues should provide concurrency benefit");
                        avgConcurrent.Should().BeLessThan(avgSequential, "Concurrent execution should be faster");
                    }
                    finally
                    {
                        // Cleanup command queues
                        for (var i = 0; i < numQueues; i++)
                        {
                            if (commandQueues[i] != IntPtr.Zero)
                            {
                                MetalNative.ReleaseCommandQueue(commandQueues[i]);
                            }
                        }
                    }
                }
                finally
                {
                    MetalNative.ReleaseComputePipelineState(pipelineState);
                    MetalNative.ReleaseFunction(function);
                    MetalNative.ReleaseLibrary(library);
                }
            }
            finally
            {
                MetalNative.ReleaseDevice(device);
            }
        }

        [SkippableFact]
        public void Thread_Safety_Should_Be_Maintained_Under_Stress()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");

            const int numThreads = 8;
            const int operationsPerThread = 100;
            const int elementCount = 1024;

            var device = MetalNative.CreateSystemDefaultDevice();
            Skip.If(device == IntPtr.Zero, "Metal device creation failed");

            try
            {
                var library = MetalNative.CreateLibraryWithSource(device, ConcurrentKernelShader);
                var function = MetalNative.GetFunction(library, "concurrentAdd");
                var errorPtr = IntPtr.Zero;
                var pipelineState = MetalNative.CreateComputePipelineState(device, function, ref errorPtr);

                var errors = new ConcurrentBag<Exception>();
                var completedOperations = new int[numThreads];

                var measure = new MetalPerformanceMeasurement("Thread Safety Stress", Output);
                measure.Start();

                // Launch multiple threads performing Metal operations
                var tasks = Enumerable.Range(0, numThreads).Select(threadId => Task.Run(async () =>
                {
                    try
                    {
                        var commandQueue = MetalNative.CreateCommandQueue(device);
                        
                        try
                        {
                            for (var op = 0; op < operationsPerThread; op++)
                            {
                                // Each thread performs independent Metal operations
                                ExecuteQueueWork(commandQueue, pipelineState, device, elementCount, (uint)(threadId * 1000 + op));
                                completedOperations[threadId]++;

                                // Small delay to increase chance of race conditions
                                if (op % 10 == 0)
                                {
                                    Task.Delay(1).Wait();
                                }
                            }
                        }
                        finally
                        {
                            MetalNative.ReleaseCommandQueue(commandQueue);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                })).ToArray();

                Task.WaitAll(tasks);
                measure.Stop();

                var totalOperations = completedOperations.Sum();
                var expectedOperations = numThreads * operationsPerThread;

                Output.WriteLine($"Thread Safety Stress Test Results:");
                Output.WriteLine($"  Threads: {numThreads}");
                Output.WriteLine($"  Operations per Thread: {operationsPerThread}");
                Output.WriteLine($"  Expected Total Operations: {expectedOperations}");
                Output.WriteLine($"  Completed Operations: {totalOperations}");
                Output.WriteLine($"  Errors: {errors.Count}");
                Output.WriteLine($"  Total Time: {measure.ElapsedTime.TotalSeconds:F2} seconds");
                Output.WriteLine($"  Operations per Second: {totalOperations / measure.ElapsedTime.TotalSeconds:F0}");

                // Validate thread safety
                errors.Should().BeEmpty("No errors should occur during concurrent operations");
                totalOperations.Should().Be(expectedOperations, "All operations should complete successfully");

                // Report per-thread completion
                for (var i = 0; i < numThreads; i++)
                {
                    completedOperations[i].Should().Be(operationsPerThread,
                        $"Thread {i} should complete all operations");
                }

                MetalNative.ReleaseComputePipelineState(pipelineState);
                MetalNative.ReleaseFunction(function);
                MetalNative.ReleaseLibrary(library);
            }
            finally
            {
                MetalNative.ReleaseDevice(device);
            }
        }

        [SkippableFact]
        public void Variable_Workload_Distribution_Should_Balance_Efficiently()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");
            Skip.IfNot(IsAppleSilicon(), "Workload balancing optimized for Apple Silicon unified architecture");

            var workloadSizes = new uint[] { 10, 50, 100, 500, 1000 }; // Different compute intensities
            const int elementsPerWorkload = 10240; // 10K elements each

            var device = MetalNative.CreateSystemDefaultDevice();
            var commandQueue = MetalNative.CreateCommandQueue(device);
            var library = MetalNative.CreateLibraryWithSource(device, WorkloadShader);
            var function = MetalNative.GetFunction(library, "workloadTest");
            var errorPtr = IntPtr.Zero;
            var pipelineState = MetalNative.CreateComputePipelineState(device, function, ref errorPtr);

            try
            {
                var results = new Dictionary<uint, double>();
                
                foreach (var workloadSize in workloadSizes)
                {
                    var measure = new MetalPerformanceMeasurement($"Workload {workloadSize}", Output);

                    // Create test data
                    var inputData = MetalTestDataGenerator.CreateRandomData(elementsPerWorkload, min: 0.1f, max: 1.0f);
                    var bufferSize = (nuint)(elementsPerWorkload * sizeof(float));
                    
                    var inputBuffer = MetalNative.CreateBuffer(device, bufferSize, 0);
                    var outputBuffer = MetalNative.CreateBuffer(device, bufferSize, 0);

                    try
                    {
                        // Initialize input data
                        unsafe
                        {
                            var inputPtr = MetalNative.GetBufferContents(inputBuffer);
                            Marshal.Copy(inputData, 0, inputPtr, inputData.Length);
                        }

                        measure.Start();

                        var commandBuffer = MetalNative.CreateCommandBuffer(commandQueue);
                        var encoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);
                        
                        MetalNative.SetComputePipelineState(encoder, pipelineState);
                        MetalNative.SetBuffer(encoder, inputBuffer, 0, 0);
                        MetalNative.SetBuffer(encoder, outputBuffer, 0, 1);
                        unsafe
                        {
                            MetalNative.SetBytes(encoder, &workloadSize, sizeof(uint), 2);
                        }

                        var threadsPerGroup = 256u;
                        var threadgroupsPerGrid = ((uint)elementsPerWorkload + threadsPerGroup - 1) / threadsPerGroup;
                        
                        MetalNative.DispatchThreadgroups(encoder, threadgroupsPerGrid, 1, 1,
                                                       threadsPerGroup, 1, 1);
                        MetalNative.EndEncoding(encoder);
                        MetalNative.Commit(commandBuffer);
                        MetalNative.WaitUntilCompleted(commandBuffer);

                        measure.Stop();

                        results[workloadSize] = measure.ElapsedTime.TotalMilliseconds;

                        // Verify computation was performed
                        var outputData = new float[elementsPerWorkload];
                        unsafe
                        {
                            var outputPtr = MetalNative.GetBufferContents(outputBuffer);
                            Marshal.Copy(outputPtr, outputData, 0, outputData.Length);
                        }

                        // Results should be different from input (computation occurred)
                        var differences = 0;
                        for (var i = 0; i < Math.Min(100, elementsPerWorkload); i++)
                        {
                            if (Math.Abs(outputData[i] - inputData[i]) > 0.001f)
                            {
                                differences++;
                            }
                        }

                        differences.Should().BeGreaterThan(80, 
                            $"Most elements should be modified by workload {workloadSize}");

                        MetalNative.ReleaseCommandBuffer(commandBuffer);
                        MetalNative.ReleaseComputeCommandEncoder(encoder);
                    }
                    finally
                    {
                        MetalNative.ReleaseBuffer(inputBuffer);
                        MetalNative.ReleaseBuffer(outputBuffer);
                    }
                }

                // Analyze workload scaling
                Output.WriteLine($"Variable Workload Distribution Results:");
                Output.WriteLine($"Workload Size\tTime (ms)\tThroughput (GFLOPS)");

                var minTime = results.Values.Min();
                foreach (var kvp in results.OrderBy(x => x.Key))
                {
                    var workloadSize = kvp.Key;
                    var timeMs = kvp.Value;
                    var totalOps = elementsPerWorkload * workloadSize;
                    var gflops = totalOps / (timeMs / 1000.0 * 1e9);
                    
                    Output.WriteLine($"{workloadSize}\t\t{timeMs:F2}\t\t{gflops:F2}");
                    
                    // Larger workloads should take proportionally more time
                    if (workloadSize > workloadSizes[0])
                    {
                        timeMs.Should().BeGreaterThan(minTime, 
                            $"Workload {workloadSize} should take more time than minimum workload");
                    }
                }

                // Test concurrent execution of different workloads
                TestConcurrentVariableWorkloads(device, pipelineState, workloadSizes);
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

        [SkippableFact]
        public void Atomic_Operations_Should_Maintain_Consistency()
        {
            Skip.IfNot(IsMetalAvailable(), "Metal not available");

            const int numThreads = 1024;
            const int numIterations = 5;

            var device = MetalNative.CreateSystemDefaultDevice();
            var commandQueue = MetalNative.CreateCommandQueue(device);
            var library = MetalNative.CreateLibraryWithSource(device, AtomicOperationsShader);
            var function = MetalNative.GetFunction(library, "atomicCounter");
            var errorPtr = IntPtr.Zero;
            var pipelineState = MetalNative.CreateComputePipelineState(device, function, ref errorPtr);

            try
            {
                var counterBufferSize = (nuint)sizeof(uint);
                var resultsBufferSize = (nuint)(numThreads * sizeof(uint));

                for (var iter = 0; iter < numIterations; iter++)
                {
                    var counterBuffer = MetalNative.CreateBuffer(device, counterBufferSize, 0);
                    var resultsBuffer = MetalNative.CreateBuffer(device, resultsBufferSize, 0);

                    try
                    {
                        // Initialize counter to 0
                        unsafe
                        {
                            var counterPtr = MetalNative.GetBufferContents(counterBuffer);
                            var counterIntPtr = (uint*)counterPtr;
                            *counterIntPtr = 0;
                        }

                        var measure = new MetalPerformanceMeasurement($"Atomic Test {iter + 1}", Output);
                        measure.Start();

                        var commandBuffer = MetalNative.CreateCommandBuffer(commandQueue);
                        var encoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);
                        
                        MetalNative.SetComputePipelineState(encoder, pipelineState);
                        MetalNative.SetBuffer(encoder, counterBuffer, 0, 0);
                        MetalNative.SetBuffer(encoder, resultsBuffer, 0, 1);

                        var threadsPerGroup = 256u;
                        var threadgroupsPerGrid = ((uint)numThreads + threadsPerGroup - 1) / threadsPerGroup;
                        
                        MetalNative.DispatchThreadgroups(encoder, threadgroupsPerGrid, 1, 1,
                                                       threadsPerGroup, 1, 1);
                        MetalNative.EndEncoding(encoder);
                        MetalNative.Commit(commandBuffer);
                        MetalNative.WaitUntilCompleted(commandBuffer);

                        measure.Stop();

                        // Check final counter value
                        uint finalCounterValue;
                        unsafe
                        {
                            var counterPtr = MetalNative.GetBufferContents(counterBuffer);
                            var counterIntPtr = (uint*)counterPtr;
                            finalCounterValue = *counterIntPtr;
                        }

                        // Check individual thread results
                        var results = new uint[numThreads];
                        unsafe
                        {
                            var resultsPtr = MetalNative.GetBufferContents(resultsBuffer);
                            Marshal.Copy(resultsPtr, results.Select(x => (int)x).ToArray(), 0, results.Length);
                        }

                        // Validate atomicity
                        finalCounterValue.Should().Be((uint)numThreads, 
                            $"Final counter should equal number of threads in iteration {iter + 1}");

                        // Each thread should have gotten a unique value from 0 to numThreads-1
                        var uniqueValues = results.Distinct().Count();
                        uniqueValues.Should().Be(numThreads, 
                            $"All atomic increments should return unique values in iteration {iter + 1}");

                        // Values should range from 0 to numThreads-1
                        results.Min().Should().Be(0u, "Minimum atomic value should be 0");
                        results.Max().Should().Be((uint)(numThreads - 1), "Maximum atomic value should be numThreads-1");

                        Output.WriteLine($"  Iteration {iter + 1}: Counter={finalCounterValue}, " +
                                       $"Unique Values={uniqueValues}, Time={measure.ElapsedTime.TotalMilliseconds:F2}ms");

                        MetalNative.ReleaseCommandBuffer(commandBuffer);
                        MetalNative.ReleaseComputeCommandEncoder(encoder);
                    }
                    finally
                    {
                        MetalNative.ReleaseBuffer(counterBuffer);
                        MetalNative.ReleaseBuffer(resultsBuffer);
                    }
                }

                Output.WriteLine($"Atomic Operations Consistency Test:");
                Output.WriteLine($"  All {numIterations} iterations passed atomicity checks");
                Output.WriteLine($"  Thread count: {numThreads}");
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

        private void ExecuteQueueWork(IntPtr commandQueue, IntPtr pipelineState, IntPtr device, int elementCount, uint offset)
        {
            var bufferSize = (nuint)(elementCount * sizeof(float));
            
            var bufferA = MetalNative.CreateBuffer(device, bufferSize, 0);
            var bufferB = MetalNative.CreateBuffer(device, bufferSize, 0);
            var bufferResult = MetalNative.CreateBuffer(device, bufferSize, 0);

            try
            {
                // Initialize with test data
                var dataA = MetalTestDataGenerator.CreateLinearSequence(elementCount, offset);
                var dataB = MetalTestDataGenerator.CreateLinearSequence(elementCount, offset * 2);

                unsafe
                {
                    var ptrA = MetalNative.GetBufferContents(bufferA);
                    var ptrB = MetalNative.GetBufferContents(bufferB);
                    Marshal.Copy(dataA, 0, ptrA, dataA.Length);
                    Marshal.Copy(dataB, 0, ptrB, dataB.Length);
                }

                var commandBuffer = MetalNative.CreateCommandBuffer(commandQueue);
                var encoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);
                
                MetalNative.SetComputePipelineState(encoder, pipelineState);
                MetalNative.SetBuffer(encoder, bufferA, 0, 0);
                MetalNative.SetBuffer(encoder, bufferB, 0, 1);
                MetalNative.SetBuffer(encoder, bufferResult, 0, 2);
                unsafe
                {
                    MetalNative.SetBytes(encoder, &offset, sizeof(uint), 3);
                }

                var threadsPerGroup = 256u;
                var threadgroupsPerGrid = ((uint)elementCount + threadsPerGroup - 1) / threadsPerGroup;
                
                MetalNative.DispatchThreadgroups(encoder, threadgroupsPerGrid, 1, 1,
                                               threadsPerGroup, 1, 1);
                MetalNative.EndEncoding(encoder);
                MetalNative.Commit(commandBuffer);
                MetalNative.WaitUntilCompleted(commandBuffer);

                MetalNative.ReleaseCommandBuffer(commandBuffer);
                MetalNative.ReleaseComputeCommandEncoder(encoder);
            }
            finally
            {
                MetalNative.ReleaseBuffer(bufferA);
                MetalNative.ReleaseBuffer(bufferB);
                MetalNative.ReleaseBuffer(bufferResult);
            }
        }

        private void TestConcurrentVariableWorkloads(IntPtr device, IntPtr pipelineState, uint[] workloadSizes)
        {
            const int elementsPerWorkload = 2048; // Smaller for concurrent test
            var commandQueues = new IntPtr[workloadSizes.Length];
            
            try
            {
                // Create separate command queues for each workload
                for (var i = 0; i < workloadSizes.Length; i++)
                {
                    commandQueues[i] = MetalNative.CreateCommandQueue(device);
                }

                var measure = new MetalPerformanceMeasurement("Concurrent Variable Workloads", Output);
                measure.Start();

                // Execute all workloads concurrently
                var tasks = workloadSizes.Select((workloadSize, index) => Task.Run(() =>
                {
                    var bufferSize = (nuint)(elementsPerWorkload * sizeof(float));
                    var inputBuffer = MetalNative.CreateBuffer(device, bufferSize, 0);
                    var outputBuffer = MetalNative.CreateBuffer(device, bufferSize, 0);

                    try
                    {
                        var inputData = MetalTestDataGenerator.CreateRandomData(elementsPerWorkload);
                        unsafe
                        {
                            var inputPtr = MetalNative.GetBufferContents(inputBuffer);
                            Marshal.Copy(inputData, 0, inputPtr, inputData.Length);
                        }

                        var commandBuffer = MetalNative.CreateCommandBuffer(commandQueues[index]);
                        var encoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);
                        
                        MetalNative.SetComputePipelineState(encoder, pipelineState);
                        MetalNative.SetBuffer(encoder, inputBuffer, 0, 0);
                        MetalNative.SetBuffer(encoder, outputBuffer, 0, 1);
                        unsafe
                        {
                            MetalNative.SetBytes(encoder, &workloadSize, sizeof(uint), 2);
                        }

                        var threadsPerGroup = 256u;
                        var threadgroupsPerGrid = ((uint)elementsPerWorkload + threadsPerGroup - 1) / threadsPerGroup;
                        
                        MetalNative.DispatchThreadgroups(encoder, threadgroupsPerGrid, 1, 1,
                                                       threadsPerGroup, 1, 1);
                        MetalNative.EndEncoding(encoder);
                        MetalNative.Commit(commandBuffer);
                        MetalNative.WaitUntilCompleted(commandBuffer);

                        MetalNative.ReleaseCommandBuffer(commandBuffer);
                        MetalNative.ReleaseComputeCommandEncoder(encoder);
                    }
                    finally
                    {
                        MetalNative.ReleaseBuffer(inputBuffer);
                        MetalNative.ReleaseBuffer(outputBuffer);
                    }
                })).ToArray();

                Task.WaitAll(tasks);
                measure.Stop();

                Output.WriteLine($"Concurrent Variable Workload Execution:");
                Output.WriteLine($"  Workloads: [{string.Join(", ", workloadSizes)}]");
                Output.WriteLine($"  Total Time: {measure.ElapsedTime.TotalMilliseconds:F2} ms");
                Output.WriteLine($"  Average per Workload: {measure.ElapsedTime.TotalMilliseconds / workloadSizes.Length:F2} ms");
            }
            finally
            {
                // Cleanup command queues
                for (var i = 0; i < commandQueues.Length; i++)
                {
                    if (commandQueues[i] != IntPtr.Zero)
                    {
                        MetalNative.ReleaseCommandQueue(commandQueues[i]);
                    }
                }
            }
        }
    }
}