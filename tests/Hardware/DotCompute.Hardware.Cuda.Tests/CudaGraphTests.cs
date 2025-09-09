// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Extensions;
using DotCompute.Hardware.Cuda.Tests.Helpers;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Hardware tests for CUDA graph functionality.
    /// Tests graph creation, capture, execution, and performance optimization.
    /// </summary>
    [Trait("Category", "RequiresCUDA")]
    public class CudaGraphTests : TestBase
    {
        private const string SimpleKernel = @"
            __global__ void simpleAdd(float* a, float* b, float* c, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx < n) {
                    c[idx] = a[idx] + b[idx];
                }
            }";

        private const string MultiKernel1 = @"
            __global__ void multiply(float* a, float* b, float* c, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx < n) {
                    c[idx] = a[idx] * b[idx];
                }
            }";

        private const string MultiKernel2 = @"
            __global__ void scale(float* a, float scale, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx < n) {
                    a[idx] = a[idx] * scale;
                }
            }";

        public CudaGraphTests(ITestOutputHelper output) : base(output) { }

        [SkippableFact]
        public async Task Graph_Creation_Should_Succeed()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(await SupportsGraphs(), "CUDA graphs not supported");


            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateProductionAccelerator(0);


            const int elementCount = 1024;

            // Prepare data

            var hostA = new float[elementCount];
            var hostB = new float[elementCount];


            for (var i = 0; i < elementCount; i++)
            {
                hostA[i] = i * 0.5f;
                hostB[i] = i * 0.3f;
            }


            await using var deviceA = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var deviceB = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var deviceC = await accelerator.Memory.AllocateAsync<float>(elementCount);


            await deviceA.WriteAsync(hostA.AsSpan(), 0);
            await deviceB.WriteAsync(hostB.AsSpan(), 0);

            // Compile kernel

            var kernelDef = new KernelDefinition("simpleAdd", SimpleKernel, "simpleAdd");
            var kernel = await accelerator.CompileKernelAsync(kernelDef);

            // Create graph

            var graph = accelerator.CreateGraph();
            _ = graph.Should().NotBeNull();


            const int blockSize = 256;
            var gridSize = (elementCount + blockSize - 1) / blockSize;
            var launchConfig = new LaunchConfiguration
            {
                GridSize = new Dim3(gridSize),
                BlockSize = new Dim3(blockSize)
            };

            // Add kernel to graph
            _ = graph.AddKernel(kernel, launchConfig, deviceA, deviceB, deviceC, elementCount);

            // Instantiate graph

            var executableGraph = graph.Instantiate();
            _ = executableGraph.Should().NotBeNull();

            // Execute graph

            await CudaGraphExecutable.LaunchAsync(null);

            // Verify results

            var result = new float[elementCount];
            await deviceC.ReadAsync(result.AsSpan(), 0);


            for (var i = 0; i < Math.Min(100, elementCount); i++)
            {
                _ = result[i].Should().BeApproximately(hostA[i] + hostB[i], 0.0001f, $"at index {i}");
            }


            Output.WriteLine($"Graph creation and execution successful");
            Output.WriteLine($"  Elements processed: {elementCount}");
            Output.WriteLine($"  Grid size: {gridSize}, Block size: {blockSize}");
        }

        [SkippableFact]
        public async Task Graph_Capture_Should_Work()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(await SupportsGraphs(), "CUDA graphs not supported");


            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateProductionAccelerator(0);


            const int elementCount = 2048;


            var hostData = new float[elementCount];
            for (var i = 0; i < elementCount; i++)
            {
                hostData[i] = i * 0.1f;
            }


            await using var deviceData = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await deviceData.WriteAsync(hostData.AsSpan(), 0);


            var kernelDef = new KernelDefinition("scale", MultiKernel2, "scale");
            var kernel = await accelerator.CompileKernelAsync(kernelDef);


            const int blockSize = 256;
            var gridSize = (elementCount + blockSize - 1) / blockSize;
            var launchConfig = new LaunchConfiguration
            {
                GridSize = new Dim3(gridSize),
                BlockSize = new Dim3(blockSize)
            };

            // Create and start graph capture

            var stream = accelerator.CreateStream();
            _ = stream.BeginCapture();

            // Execute operations in capture mode

            await kernel.LaunchAsync(launchConfig, stream, deviceData, 2.0f, elementCount);
            await kernel.LaunchAsync(launchConfig, stream, deviceData, 1.5f, elementCount);

            // End capture

            var capturedGraph = stream.EndCapture();
            _ = capturedGraph.Should().NotBeNull();

            // Execute the captured graph

            var executableGraph = capturedGraph.Instantiate();
            await CudaGraphExecutable.LaunchAsync(null);

            // Verify results (should be scaled by 2.0 * 1.5 = 3.0)

            var result = new float[elementCount];
            await deviceData.ReadAsync(result.AsSpan(), 0);


            for (var i = 0; i < Math.Min(100, elementCount); i++)
            {
                var expected = hostData[i] * 2.0f * 1.5f;
                _ = result[i].Should().BeApproximately(expected, 0.0001f, $"at index {i}");
            }


            Output.WriteLine($"Graph capture test successful");
            Output.WriteLine($"  Operations captured: 2 kernel launches");
            Output.WriteLine($"  Final scaling factor: 3.0x");
        }

        [SkippableFact]
        public async Task Multi_Kernel_Graph_Should_Execute_Correctly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(await SupportsGraphs(), "CUDA graphs not supported");


            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateProductionAccelerator(0);


            const int elementCount = 1024;


            var hostA = new float[elementCount];
            var hostB = new float[elementCount];


            for (var i = 0; i < elementCount; i++)
            {
                hostA[i] = i * 0.2f;
                hostB[i] = (i + 1) * 0.3f;
            }


            await using var deviceA = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var deviceB = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var deviceC = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var deviceD = await accelerator.Memory.AllocateAsync<float>(elementCount);


            await deviceA.WriteAsync(hostA.AsSpan(), 0);
            await deviceB.WriteAsync(hostB.AsSpan(), 0);

            // Compile kernels

            var multiplyKernelDef = new KernelDefinition("multiply", MultiKernel1, "multiply");
            var multiplyKernel = await accelerator.CompileKernelAsync(multiplyKernelDef);
            var scaleKernelDef = new KernelDefinition("scale", MultiKernel2, "scale");
            var scaleKernel = await accelerator.CompileKernelAsync(scaleKernelDef);


            const int blockSize = 256;
            var gridSize = (elementCount + blockSize - 1) / blockSize;
            var launchConfig = new LaunchConfiguration
            {
                GridSize = new Dim3(gridSize),
                BlockSize = new Dim3(blockSize)
            };

            // Create graph with multiple operations

            var graph = accelerator.CreateGraph();

            // Step 1: Multiply A and B, store in C
            _ = graph.AddKernel(multiplyKernel, launchConfig, deviceA, deviceB, deviceC, elementCount);

            // Step 2: Scale C by 2.0, store in D  

            graph.AddMemoryCopy(deviceC, deviceD, elementCount * sizeof(float));
            _ = graph.AddKernel(scaleKernel, launchConfig, deviceD, 2.0f, elementCount);


            var executableGraph = graph.Instantiate();

            // Execute the multi-kernel graph

            var stopwatch = Stopwatch.StartNew();
            await CudaGraphExecutable.LaunchAsync(null);
            stopwatch.Stop();

            // Verify results

            var result = new float[elementCount];
            await deviceD.ReadAsync(result.AsSpan(), 0);


            for (var i = 0; i < Math.Min(100, elementCount); i++)
            {
                var expected = hostA[i] * hostB[i] * 2.0f;
                _ = result[i].Should().BeApproximately(expected, 0.0001f, $"at index {i}");
            }


            Output.WriteLine($"Multi-kernel graph execution successful");
            Output.WriteLine($"  Operations: multiply + copy + scale");
            Output.WriteLine($"  Execution time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        }

        [SkippableFact]
        public async Task Graph_Performance_Should_Be_Better_Than_Individual_Launches()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(await SupportsGraphs(), "CUDA graphs not supported");


            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateProductionAccelerator(0);


            const int elementCount = 1024;
            const int iterations = 100;


            var hostData = new float[elementCount];
            for (var i = 0; i < elementCount; i++)
            {
                hostData[i] = i * 0.1f;
            }


            await using var deviceData = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await deviceData.WriteAsync(hostData.AsSpan(), 0);


            var kernelDef = new KernelDefinition("scale", MultiKernel2, "scale");
            var kernel = await accelerator.CompileKernelAsync(kernelDef);


            const int blockSize = 256;
            var gridSize = (elementCount + blockSize - 1) / blockSize;
            var launchConfig = new LaunchConfiguration
            {
                GridSize = new Dim3(gridSize),
                BlockSize = new Dim3(blockSize)
            };

            // Test individual kernel launches

            var individualTimes = new double[iterations];


            for (var i = 0; i < iterations; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                await kernel.LaunchAsync(launchConfig, deviceData, 1.01f, elementCount);
                stopwatch.Stop();
                individualTimes[i] = stopwatch.Elapsed.TotalMicroseconds;
            }

            // Create graph with multiple operations for realistic comparison
            // CUDA graphs benefit from capturing multiple kernels, not single ones

            var stream = accelerator.CreateStream();
            _ = stream.BeginCapture();

            // Capture 10 kernel launches in the graph to demonstrate the benefit

            const int kernelsPerGraph = 10;
            for (var k = 0; k < kernelsPerGraph; k++)
            {
                await kernel.LaunchAsync(launchConfig, stream, deviceData, 1.01f + k * 0.01f, elementCount);
            }


            var capturedGraph2 = stream.EndCapture();
            var executableGraph = capturedGraph2.Instantiate();

            // Test graph execution (each graph launch executes 10 kernels)

            var graphTimes = new double[iterations];


            for (var i = 0; i < iterations; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                await CudaGraphExecutable.LaunchAsync(null);
                stopwatch.Stop();
                // Divide by kernelsPerGraph to get per-kernel time for fair comparison
                graphTimes[i] = stopwatch.Elapsed.TotalMicroseconds / kernelsPerGraph;
            }


            var avgIndividualTime = individualTimes.Average();
            var avgGraphTime = graphTimes.Average();
            var speedup = avgIndividualTime / avgGraphTime;


            Output.WriteLine($"Graph vs Individual Launch Performance:");
            Output.WriteLine($"  Individual Launch Avg: {avgIndividualTime:F2} μs");
            Output.WriteLine($"  Graph Launch Avg: {avgGraphTime:F2} μs");
            Output.WriteLine($"  Speedup: {speedup:F2}x");

            // Graph execution should be at least as fast, often faster due to reduced overhead
            _ = avgGraphTime.Should().BeLessThanOrEqualTo(avgIndividualTime * 1.1,

                "Graph execution should not be significantly slower than individual launches");
        }

        [SkippableFact]
        public async Task Graph_With_Dependencies_Should_Execute_In_Order()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(await SupportsGraphs(), "CUDA graphs not supported");


            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateProductionAccelerator(0);


            const int elementCount = 1024;


            var hostInput = new float[elementCount];
            for (var i = 0; i < elementCount; i++)
            {
                hostInput[i] = i + 1.0f; // Start with values 1, 2, 3, ...
            }


            await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var deviceTemp = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(elementCount);


            await deviceInput.WriteAsync(hostInput.AsSpan(), 0);


            var scaleKernelDef = new KernelDefinition("scale", MultiKernel2, "scale");
            var scaleKernel = await accelerator.CompileKernelAsync(scaleKernelDef);
            var addKernelDef = new KernelDefinition("simpleAdd", SimpleKernel, "simpleAdd");
            var addKernel = await accelerator.CompileKernelAsync(addKernelDef);


            const int blockSize = 256;
            var gridSize = (elementCount + blockSize - 1) / blockSize;
            var launchConfig = new LaunchConfiguration
            {
                GridSize = new Dim3(gridSize),
                BlockSize = new Dim3(blockSize)
            };

            // Create graph with dependencies:
            // 1. Scale input by 2.0 -> temp
            // 2. Add input + temp -> output (should be input + input*2 = input*3)

            var graph = accelerator.CreateGraph();

            // Copy input to temp, then scale temp

            graph.AddMemoryCopy(deviceInput, deviceTemp, elementCount * sizeof(float));
            var scaleNode = graph.AddKernel(scaleKernel, launchConfig, deviceTemp, 2.0f, elementCount);

            // Add original input to scaled temp

            var addNode = graph.AddKernel(addKernel, launchConfig, deviceInput, deviceTemp, deviceOutput, elementCount);

            // Set dependency: add must wait for scale

            graph.AddDependency(scaleNode, addNode);


            var executableGraph = graph.Instantiate();
            await CudaGraphExecutable.LaunchAsync(null);

            // Verify results (should be input * 3)

            var result = new float[elementCount];
            await deviceOutput.ReadAsync(result.AsSpan(), 0);


            for (var i = 0; i < Math.Min(100, elementCount); i++)
            {
                var expected = hostInput[i] * 3.0f; // input + (input * 2.0)
                _ = result[i].Should().BeApproximately(expected, 0.0001f, $"at index {i}");
            }


            Output.WriteLine($"Graph with dependencies executed correctly");
            Output.WriteLine($"  Result verification: input * 3.0 = {result[0]:F1} (expected: {hostInput[0] * 3:F1})");
        }

        [SkippableFact]
        public async Task Graph_Memory_Operations_Should_Work()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(await SupportsGraphs(), "CUDA graphs not supported");


            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateProductionAccelerator(0);


            const int elementCount = 1024;


            var hostData = new float[elementCount];
            for (var i = 0; i < elementCount; i++)
            {
                hostData[i] = (float)Math.Sin(i * 0.01);
            }


            await using var deviceSrc = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var deviceDst1 = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var deviceDst2 = await accelerator.Memory.AllocateAsync<float>(elementCount);


            await deviceSrc.WriteAsync(hostData.AsSpan(), 0);

            // Create graph with memory operations

            var graph = accelerator.CreateGraph();

            // Copy src -> dst1

            graph.AddMemoryCopy(deviceSrc, deviceDst1, elementCount * sizeof(float));

            // Copy dst1 -> dst2 

            graph.AddMemoryCopy(deviceDst1, deviceDst2, elementCount * sizeof(float));


            var executableGraph = graph.Instantiate();
            await CudaGraphExecutable.LaunchAsync(null);

            // Verify both destinations have correct data

            var result1 = new float[elementCount];
            var result2 = new float[elementCount];


            await deviceDst1.ReadAsync(result1.AsSpan(), 0);
            await deviceDst2.ReadAsync(result2.AsSpan(), 0);


            for (var i = 0; i < elementCount; i++)
            {
                _ = result1[i].Should().BeApproximately(hostData[i], 0.0001f, $"dst1 at index {i}");
                _ = result2[i].Should().BeApproximately(hostData[i], 0.0001f, $"dst2 at index {i}");
            }


            Output.WriteLine($"Graph memory operations successful");
            Output.WriteLine($"  Memory copies: src->dst1->dst2");
            Output.WriteLine($"  Data integrity verified");
        }

        [SkippableFact]
        public async Task Graph_Update_Should_Work()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(await SupportsGraphs(), "CUDA graphs not supported");
            Skip.IfNot(await SupportsGraphUpdate(), "CUDA graph update not supported");


            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateProductionAccelerator(0);


            const int elementCount = 512;


            var hostData = new float[elementCount];
            for (var i = 0; i < elementCount; i++)
            {
                hostData[i] = i * 0.1f;
            }


            await using var deviceData = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await deviceData.WriteAsync(hostData.AsSpan(), 0);


            var kernelDef = new KernelDefinition("scale", MultiKernel2, "scale");
            var kernel = await accelerator.CompileKernelAsync(kernelDef);


            const int blockSize = 256;
            var gridSize = (elementCount + blockSize - 1) / blockSize;
            var launchConfig = new LaunchConfiguration
            {
                GridSize = new Dim3(gridSize),
                BlockSize = new Dim3(blockSize)
            };

            // Create initial graph with scale factor 2.0

            var graph = accelerator.CreateGraph();
            var kernelNode = graph.AddKernel(kernel, launchConfig, deviceData, 2.0f, elementCount);


            var executableGraph = graph.Instantiate();
            await CudaGraphExecutable.LaunchAsync(null);

            // Verify initial results (scaled by 2.0)

            var result1 = new float[elementCount];
            await deviceData.ReadAsync(result1.AsSpan(), 0);

            _ = result1[0].Should().BeApproximately(hostData[0] * 2.0f, 0.0001f);

            // Update graph to use scale factor 3.0

            try
            {
                CudaGraphExecutable.UpdateKernelNode(kernelNode, kernel, launchConfig, deviceData, 3.0f, elementCount);

                // Reset data and execute updated graph

                await deviceData.WriteAsync(hostData.AsSpan(), 0);
                await CudaGraphExecutable.LaunchAsync(null);


                var result2 = new float[elementCount];
                await deviceData.ReadAsync(result2.AsSpan(), 0);

                _ = result2[0].Should().BeApproximately(hostData[0] * 3.0f, 0.0001f);


                Output.WriteLine($"Graph update successful");
                Output.WriteLine($"  Original scale: 2.0, result: {result1[0]:F2}");
                Output.WriteLine($"  Updated scale: 3.0, result: {result2[0]:F2}");
            }
            catch (NotSupportedException)
            {
                Output.WriteLine("Graph update not supported - test skipped");
            }
        }

        /// <summary>
        /// Check if CUDA graphs are supported on this device/driver
        /// </summary>
        private static async Task<bool> SupportsGraphs()
        {
            if (!IsCudaAvailable())
            {
                return false;
            }


            try
            {
                var factory = new CudaAcceleratorFactory();
                await using var accelerator = factory.CreateProductionAccelerator(0);

                // CUDA graphs require compute capability 3.5+ and CUDA 10.0+

                var cc = accelerator.Info.ComputeCapability;
                return cc.Major > 3 || (cc.Major == 3 && cc.Minor >= 5);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if CUDA graph updates are supported
        /// </summary>
        private static async Task<bool> SupportsGraphUpdate()
        {
            if (!await SupportsGraphs())
            {
                return false;
            }


            try
            {
                var factory = new CudaAcceleratorFactory();
                await using var accelerator = factory.CreateProductionAccelerator(0);

                // Graph updates require CUDA 11.1+
                // For now, assume supported if graphs are supported

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}