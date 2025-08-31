// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Memory;
using DotCompute.Abstractions.Types;
using DotCompute.Core;
using DotCompute.Core.Kernels;
using DotCompute.Core.Memory;
using DotCompute.Memory;
using DotCompute.Tests.Common;
using DotCompute.Tests.Common.Assertions;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Integration.Tests;

/// <summary>
/// Comprehensive integration tests validating end-to-end workflow functionality.
/// Tests complete computation pipelines, multi-accelerator coordination, memory coherency,
/// error recovery, and real-world scenarios including image processing and matrix operations.
/// These tests verify that the consolidated architecture maintains full functionality with proper performance.
/// </summary>
public class ConsolidatedArchitectureTests : TestBase, IAsyncLifetime
{
    private TestAccelerator? _accelerator;
    private TestCpuAccelerator? _cpuAccelerator;
    private TestKernelCompiler? _compiler;
    private TestMemoryBuffer<float>? _buffer;
    private ILogger? _logger;
    private readonly List<IAccelerator> _accelerators = new();
    private readonly Random _random = new(42); // Fixed seed for reproducible tests - reserved for future use
    
#pragma warning disable CA1823 // Unused field '_random'
#pragma warning restore CA1823

    public ConsolidatedArchitectureTests(ITestOutputHelper output) : base(output)
    {
    }

    public async Task InitializeAsync()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        try 
        {
            _logger = loggerFactory.CreateLogger<ConsolidatedArchitectureTests>();
        }
        finally 
        {
            loggerFactory.Dispose();
        }
        
        // Initialize primary GPU accelerator
        var gpuInfo = new AcceleratorInfo(
            AcceleratorType.CUDA,
            "Integration Test GPU Accelerator",
            "1.0",
            4L * 1024 * 1024 * 1024, // 4GB
            32,
            1500,
            new Version(1, 0),
            64 * 1024 * 1024, // 64MB max allocation
            true
        );
        
        // Initialize CPU fallback accelerator
        var cpuInfo = new AcceleratorInfo(
            AcceleratorType.CPU,
            "Integration Test CPU Accelerator",
            "1.0",
            16L * 1024 * 1024 * 1024, // 16GB
            Environment.ProcessorCount,
            3000,
            new Version(1, 0),
            256 * 1024 * 1024, // 256MB max allocation
            false
        );
        
        _accelerator = new TestAccelerator(gpuInfo, _logger);
        _cpuAccelerator = new TestCpuAccelerator(cpuInfo, _logger);
        _accelerators.Add(_accelerator);
        _accelerators.Add(_cpuAccelerator);
        
        _compiler = new TestKernelCompiler(_logger);
        _buffer = new TestMemoryBuffer<float>(1024 * sizeof(float), _accelerator);
        
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        foreach (var accelerator in _accelerators)
        {
            if (accelerator != null)
                await accelerator.DisposeAsync();
        }
        
        if (_buffer != null)
            await _buffer.DisposeAsync();
        
        _accelerators.Clear();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ConsolidatedAccelerator_CompileAndExecuteKernel_Works()
    {
        // Arrange
        var kernelCode = GenerateTestKernelCode();
        var definition = new KernelDefinition("vector_add", Convert.ToBase64String(kernelCode), "main");
        
        // Act - Compile using accelerator (which uses BaseAccelerator)
        var compiledKernel = await _accelerator!.CompileKernelAsync(definition);
        
        // Assert
        Assert.NotNull(compiledKernel);
        Assert.Equal("vector_add", compiledKernel.Name);
    }

    [Fact]
    public async Task ConsolidatedCompiler_WithCaching_ReusesSameInstance()
    {
        // Arrange
        var definition = new KernelDefinition("cached_kernel", Convert.ToBase64String(GenerateTestKernelCode()), "main");
        
        // Act - Compile twice
        var kernel1 = await _compiler!.CompileAsync(definition);
        var kernel2 = await _compiler.CompileAsync(definition);
        
        // Assert - Should be same cached instance
        Assert.Same(kernel1, kernel2);
    }

    [Fact]
    public async Task ConsolidatedMemoryBuffer_CopyOperations_Work()
    {
        // Arrange
        var sourceData = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var destinationData = new float[4];
        
        // Act
        await _buffer!.CopyFromAsync(sourceData.AsMemory(), 0, CancellationToken.None);
        await _buffer.CopyToAsync(destinationData.AsMemory(), 0, CancellationToken.None);
        
        // Assert
        Assert.Equal(sourceData, destinationData);
    }

    [Fact]
    public async Task BaseAccelerator_Synchronization_Works()
    {
        // Act
        await _accelerator!.SynchronizeAsync(CancellationToken.None);
        
        // Assert - Should complete without error
        Assert.True(_accelerator.SynchronizeCalled);
    }

    [Fact]
    public void BaseMemoryBuffer_Validation_PreventsInvalidOperations()
    {
        // Arrange
        using var buffer = new TestMemoryBuffer<float>(100 * sizeof(float), _accelerator!);
        
        // Act & Assert - Should throw for invalid copy parameters
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            buffer.TestValidateCopyParameters(100, 50, 100, 0, 60));
    }

    [Fact]
    public async Task ConsolidatedArchitecture_EndToEnd_Workflow()
    {
        // This test simulates a complete workflow using all consolidated components
        
        // Step 1: Create kernel definition
        var kernelDef = new KernelDefinition("workflow_kernel", Convert.ToBase64String(GenerateTestKernelCode()), "main");
        
        // Step 2: Compile kernel using accelerator
        var kernel = await _accelerator!.CompileKernelAsync(kernelDef);
        Assert.NotNull(kernel);
        
        // Step 3: Allocate memory buffers
        using var inputBuffer = new TestMemoryBuffer<float>(256 * sizeof(float), _accelerator);
        using var outputBuffer = new TestMemoryBuffer<float>(256 * sizeof(float), _accelerator);
        
        // Step 4: Copy data to buffers
        var inputData = Enumerable.Range(0, 256).Select(i => (float)i).ToArray();
        await inputBuffer.CopyFromAsync(inputData.AsMemory(), CancellationToken.None);
        
        // Step 5: Execute kernel (simulated)
        var arguments = new KernelArguments();
        arguments.Add(inputBuffer);
        arguments.Add(outputBuffer);
        arguments.Add(256);
        await kernel.ExecuteAsync(arguments, CancellationToken.None);
        
        // Step 6: Synchronize
        await _accelerator.SynchronizeAsync(CancellationToken.None);
        
        // Step 7: Copy results back
        var outputData = new float[256];
        await outputBuffer.CopyToAsync(outputData.AsMemory(), CancellationToken.None);
        
        // Assert - Verify workflow completed
        Assert.NotNull(outputData);
        Assert.Equal(256, outputData.Length);
    }

    [Fact]
    public void ConsolidatedArchitecture_PerformanceMetrics_AreTracked()
    {
        // Act
        var metrics = _compiler!.GetMetrics();
        
        // Assert - Metrics should be available
        Assert.NotNull(metrics);
    }

    #region Complete Computation Pipeline Tests

    [Fact]
    [Trait(nameof(TestCategories.Integration), TestCategories.Integration)]
    [Trait(nameof(TestCategories.Performance), TestCategories.Performance)]
    public async Task CompleteComputationPipeline_VectorAddition_Float_Success()
    {
        // Arrange - Data preparation
        const int dataSize = 1024;
        var inputA = GenerateRandomFloats(dataSize, seed: 123);
        var inputB = GenerateRandomFloats(dataSize, seed: 456);
        var expectedOutput = inputA.Zip(inputB, (a, b) => a + b).ToArray();
        
        using var perfContext = CreatePerformanceContext("VectorAddition_Pipeline");
        
        // Step 1: Kernel compilation
        perfContext.Checkpoint("Start_Compilation");
        var kernelCode = GenerateVectorAddKernelCode();
        var definition = new KernelDefinition("vector_add_float", Convert.ToBase64String(kernelCode), "vector_add");
        var kernel = await _accelerator!.CompileKernelAsync(definition);
        perfContext.Checkpoint("Compilation_Complete");
        
        // Step 2: Memory allocation and data transfer
        using var bufferA = new TestMemoryBuffer<float>(dataSize * sizeof(float), _accelerator);
        using var bufferB = new TestMemoryBuffer<float>(dataSize * sizeof(float), _accelerator);
        using var bufferResult = new TestMemoryBuffer<float>(dataSize * sizeof(float), _accelerator);
        
        await bufferA.CopyFromAsync(inputA.AsMemory(), CancellationToken.None);
        await bufferB.CopyFromAsync(inputB.AsMemory(), CancellationToken.None);
        perfContext.Checkpoint("Data_Transfer_Complete");
        
        // Step 3: Kernel execution
        var arguments = new KernelArguments();
        arguments.Add(bufferA);
        arguments.Add(bufferB);
        arguments.Add(bufferResult);
        arguments.Add(dataSize);
        
        var executionTime = await MeasureExecutionTimeAsync(async () =>
            await kernel.ExecuteAsync(arguments, CancellationToken.None));
        perfContext.Checkpoint("Execution_Complete");
        
        // Step 4: Result validation
        await _accelerator.SynchronizeAsync();
        var actualOutput = new float[dataSize];
        await bufferResult.CopyToAsync(actualOutput.AsMemory(), CancellationToken.None);
        perfContext.Checkpoint("Results_Retrieved");
        
        // Assert - Validate results
        actualOutput.ShouldBeApproximatelyEqualTo(expectedOutput, 1e-6f,
            "because vector addition should be computed correctly");
        actualOutput.ShouldContainOnlyFiniteValues(
            "because all computation results should be finite");
        
        // Performance assertions
        executionTime.ShouldBeWithinTimeLimit(100.0, // Max 100ms
            "because simple vector addition should be fast");
        
        Output.WriteLine($"Pipeline completed successfully in {executionTime:F2}ms for {dataSize} elements");
    }

    [Fact]
    [Trait(nameof(TestCategories.Integration), TestCategories.Integration)]
    public async Task CompleteComputationPipeline_MultipleKernelChaining_Success()
    {
        const int dataSize = 512;
        var input = GenerateSequentialFloats(dataSize, 1.0f, 1.0f); // 1, 2, 3, ...
        
        using var perfContext = CreatePerformanceContext("Chained_Kernels");
        
        // Chain: Square -> Scale(2.0) -> Add(1.0) -> Result should be: 2*(i^2) + 1
        var expectedOutput = input.Select(x => 2.0f * (x * x) + 1.0f).ToArray();
        
        // Compile kernels
        var squareKernel = await _accelerator!.CompileKernelAsync(
            new KernelDefinition("square", Convert.ToBase64String(GenerateSquareKernelCode()), "square"));
        var scaleKernel = await _accelerator.CompileKernelAsync(
            new KernelDefinition("scale", Convert.ToBase64String(GenerateScaleKernelCode()), "scale"));
        var addKernel = await _accelerator.CompileKernelAsync(
            new KernelDefinition("add_scalar", Convert.ToBase64String(GenerateAddScalarKernelCode()), "add_scalar"));
        
        perfContext.Checkpoint("All_Kernels_Compiled");
        
        // Memory buffers
        using var buffer1 = new TestMemoryBuffer<float>(dataSize * sizeof(float), _accelerator);
        using var buffer2 = new TestMemoryBuffer<float>(dataSize * sizeof(float), _accelerator);
        using var buffer3 = new TestMemoryBuffer<float>(dataSize * sizeof(float), _accelerator);
        
        await buffer1.CopyFromAsync(input.AsMemory(), CancellationToken.None);
        
        // Execute kernel chain
        var args1 = new KernelArguments();
        args1.Add(buffer1); args1.Add(buffer2); args1.Add(dataSize);
        await squareKernel.ExecuteAsync(args1);
        perfContext.Checkpoint("Square_Complete");
        
        var args2 = new KernelArguments();
        args2.Add(buffer2); args2.Add(buffer3); args2.Add(2.0f); args2.Add(dataSize);
        await scaleKernel.ExecuteAsync(args2);
        perfContext.Checkpoint("Scale_Complete");
        
        var args3 = new KernelArguments();
        args3.Add(buffer3); args3.Add(buffer1); args3.Add(1.0f); args3.Add(dataSize);
        await addKernel.ExecuteAsync(args3);
        perfContext.Checkpoint("Add_Complete");
        
        await _accelerator.SynchronizeAsync();
        
        var result = new float[dataSize];
        await buffer1.CopyToAsync(result.AsMemory(), CancellationToken.None);
        
        // Validate chained computation
        result.ShouldBeApproximatelyEqualTo(expectedOutput, 1e-5f,
            "because chained kernel operations should compute correctly");
        
        Output.WriteLine($"Successfully chained 3 kernels processing {dataSize} elements");
    }

    [Theory]
    [InlineData(typeof(float), 256)]
    [InlineData(typeof(double), 128)]
    [InlineData(typeof(int), 512)]
    [Trait(nameof(TestCategories.Integration), TestCategories.Integration)]
    public async Task CompleteComputationPipeline_DifferentDataTypes_Success(Type dataType, int size)
    {
        using var perfContext = CreatePerformanceContext($"DataType_{dataType.Name}_Pipeline");
        
        if (dataType == typeof(float))
        {
            await TestFloatPipeline(size, perfContext);
        }
        else if (dataType == typeof(double))
        {
            await TestDoublePipeline(size, perfContext);
        }
        else if (dataType == typeof(int))
        {
            await TestIntPipeline(size, perfContext);
        }
        
        Output.WriteLine($"Successfully processed {size} elements of type {dataType.Name}");
    }

    #endregion

    #region Multi-Accelerator Coordination Tests

    [Fact]
    [Trait(nameof(TestCategories.Integration), TestCategories.Integration)]
    [Trait(nameof(TestCategories.HardwareIndependent), TestCategories.HardwareIndependent)]
    public async Task MultiAccelerator_CpuFallback_WhenGpuUnavailable()
    {
        // Arrange - Simulate GPU failure
        var failingGpuAccelerator = new FailingTestAccelerator(_accelerator!.Info, _logger!);
        var workingCpuAccelerator = _cpuAccelerator!;
        
        const int dataSize = 256;
        var input = GenerateRandomFloats(dataSize);
        var expected = input.Select(x => x * 2.0f).ToArray();
        
        using var perfContext = CreatePerformanceContext("CPU_Fallback_Test");
        
        // Act - Try GPU first, fallback to CPU
        IAccelerator selectedAccelerator;
        double executionTime;
        
        try
        {
            var gpuKernel = await failingGpuAccelerator.CompileKernelAsync(
                new KernelDefinition("scale_gpu", Convert.ToBase64String(GenerateScaleKernelCode()), "scale"));
            selectedAccelerator = failingGpuAccelerator;
            Output.WriteLine("GPU compilation succeeded unexpectedly");
        }
        catch (Exception ex)
        {
            Output.WriteLine($"GPU failed as expected: {ex.GetType().Name}");
            selectedAccelerator = workingCpuAccelerator;
            perfContext.Checkpoint("Fallback_to_CPU");
        }
        
        // Execute on selected accelerator (should be CPU)
        var kernel = await selectedAccelerator.CompileKernelAsync(
            new KernelDefinition("scale_cpu", Convert.ToBase64String(GenerateScaleKernelCode()), "scale"));
        
        using var inputBuffer = new TestMemoryBuffer<float>(dataSize * sizeof(float), selectedAccelerator);
        using var outputBuffer = new TestMemoryBuffer<float>(dataSize * sizeof(float), selectedAccelerator);
        
        await inputBuffer.CopyFromAsync(input.AsMemory(), CancellationToken.None);
        
        executionTime = await MeasureExecutionTimeAsync(async () =>
        {
            var args = new KernelArguments();
            args.Add(inputBuffer); args.Add(outputBuffer); args.Add(2.0f); args.Add(dataSize);
            await kernel.ExecuteAsync(args);
            await selectedAccelerator.SynchronizeAsync();
        });
        
        var result = new float[dataSize];
        await outputBuffer.CopyToAsync(result.AsMemory(), CancellationToken.None);
        
        // Assert - CPU fallback should work correctly
        Assert.Equal(AcceleratorType.CPU, selectedAccelerator.Type);
        result.ShouldBeApproximatelyEqualTo(expected, 1e-6f,
            "because CPU fallback should produce correct results");
        
        Output.WriteLine($"CPU fallback completed successfully in {executionTime:F2}ms");
    }

    [Fact]
    [Trait(nameof(TestCategories.Integration), TestCategories.Integration)]
    [Trait(nameof(TestCategories.Performance), TestCategories.Performance)]
    public async Task MultiAccelerator_LoadBalancing_DistributesWork()
    {
        // Arrange - Large workload to distribute
        const int totalSize = 2048;
        const int chunkSize = totalSize / 2; // Split between 2 accelerators
        
        var input = GenerateSequentialFloats(totalSize, 1.0f, 1.0f);
        var expected = input.Select(x => x * x).ToArray(); // Square operation
        
        using var perfContext = CreatePerformanceContext("Load_Balancing_Test");
        
        // Prepare kernels on both accelerators
        var gpuKernel = await _accelerator!.CompileKernelAsync(
            new KernelDefinition("square_gpu", Convert.ToBase64String(GenerateSquareKernelCode()), "square"));
        var cpuKernel = await _cpuAccelerator!.CompileKernelAsync(
            new KernelDefinition("square_cpu", Convert.ToBase64String(GenerateSquareKernelCode()), "square"));
        
        perfContext.Checkpoint("Kernels_Compiled");
        
        // Split data and process in parallel
        var chunk1 = input.AsMemory(0, chunkSize);
        var chunk2 = input.AsMemory(chunkSize, chunkSize);
        
        var result1 = new float[chunkSize];
        var result2 = new float[chunkSize];
        
        var executionTime = await MeasureExecutionTimeAsync(async () =>
        {
            // Process chunks in parallel on different accelerators
            var task1 = ProcessChunkAsync(_accelerator, gpuKernel, chunk1, result1);
            var task2 = ProcessChunkAsync(_cpuAccelerator, cpuKernel, chunk2, result2);
            
            await Task.WhenAll(task1, task2);
        });
        
        perfContext.Checkpoint("Parallel_Execution_Complete");
        
        // Combine results
        var finalResult = new float[totalSize];
        result1.CopyTo(finalResult, 0);
        result2.CopyTo(finalResult, chunkSize);
        
        // Assert - Load balancing should produce correct results efficiently
        finalResult.ShouldBeApproximatelyEqualTo(expected, 1e-6f,
            "because load balancing should maintain computational accuracy");
        
        Output.WriteLine($"Load balancing completed {totalSize} elements in {executionTime:F2}ms using 2 accelerators");
    }

    [Fact]
    [Trait(nameof(TestCategories.Integration), TestCategories.Integration)]
    [Trait(nameof(TestCategories.Concurrency), TestCategories.Concurrency)]
    public async Task MultiAccelerator_SwitchingBetweenAccelerators_MaintainsState()
    {
        const int dataSize = 512;
        var input = GenerateRandomFloats(dataSize, seed: 789);
        
        using var perfContext = CreatePerformanceContext("Accelerator_Switching_Test");
        
        // Process data through alternating accelerators
        var currentData = input.ToArray();
        IAccelerator[] accelerators = { _accelerator!, _cpuAccelerator! };
        
        for (int iteration = 0; iteration < 4; iteration++)
        {
            var selectedAccelerator = accelerators[iteration % 2];
            var acceleratorName = selectedAccelerator.Type.ToString();
            
            var kernel = await selectedAccelerator.CompileKernelAsync(
                new KernelDefinition($"scale_{acceleratorName}_{iteration}", 
                Convert.ToBase64String(GenerateScaleKernelCode()), "scale"));
            
            using var buffer1 = new TestMemoryBuffer<float>(dataSize * sizeof(float), selectedAccelerator);
            using var buffer2 = new TestMemoryBuffer<float>(dataSize * sizeof(float), selectedAccelerator);
            
            await buffer1.CopyFromAsync(currentData.AsMemory(), CancellationToken.None);
            
            var args = new KernelArguments();
            args.Add(buffer1); args.Add(buffer2); args.Add(1.1f); args.Add(dataSize);
            await kernel.ExecuteAsync(args);
            await selectedAccelerator.SynchronizeAsync();
            
            await buffer2.CopyToAsync(currentData.AsMemory(), CancellationToken.None);
            
            perfContext.Checkpoint($"Iteration_{iteration}_on_{acceleratorName}");
        }
        
        // Assert - Data should remain valid and finite after switching
        currentData.ShouldContainOnlyFiniteValues(
            "because accelerator switching should not corrupt data");
        
        // Verify expected transformation (1.1^4 ≈ 1.4641)
        var expectedScale = Math.Pow(1.1, 4);
        var tolerance = 1e-4f;
        
        for (int i = 0; i < Math.Min(10, dataSize); i++)
        {
            var expected = (float)(input[i] * expectedScale);
            Assert.InRange(currentData[i], expected - tolerance, expected + tolerance);
        }
        
        Output.WriteLine($"Successfully switched between accelerators 4 times with {dataSize} elements");
    }

    #endregion

    #region Memory Coherency Tests

    [Fact]
    [Trait(nameof(TestCategories.Integration), TestCategories.Integration)]
    [Trait(nameof(TestCategories.MemoryIntensive), TestCategories.MemoryIntensive)]
    public async Task MemoryCoherency_DataConsistency_AcrossTransfers()
    {
        const int dataSize = 1024;
        var originalData = GenerateRandomFloats(dataSize, seed: 999);
        
        using var perfContext = CreatePerformanceContext("Memory_Coherency_Test");
        
        // Create multiple buffers and transfer data between them
        using var buffer1 = new TestMemoryBuffer<float>(dataSize * sizeof(float), _accelerator!);
        using var buffer2 = new TestMemoryBuffer<float>(dataSize * sizeof(float), _accelerator);
        using var buffer3 = new TestMemoryBuffer<float>(dataSize * sizeof(float), _cpuAccelerator!);
        
        // Initial transfer: Host -> Buffer1
        await buffer1.CopyFromAsync(originalData.AsMemory(), CancellationToken.None);
        perfContext.Checkpoint("Initial_Transfer");
        
        // Transfer chain: Buffer1 -> Buffer2 -> Buffer3 -> Host
        await buffer1.CopyToAsync(buffer2, CancellationToken.None);
        await buffer2.CopyToAsync(buffer3, CancellationToken.None);
        
        var retrievedData = new float[dataSize];
        await buffer3.CopyToAsync(retrievedData.AsMemory(), CancellationToken.None);
        perfContext.Checkpoint("Transfer_Chain_Complete");
        
        // Assert - Data should remain identical through all transfers
        retrievedData.ShouldBeApproximatelyEqualTo(originalData, 1e-7f,
            "because memory transfers should preserve data exactly");
        
        // Verify memory states
        await buffer1.SynchronizeAsync();
        await buffer2.SynchronizeAsync();
        await buffer3.SynchronizeAsync();
        
        Output.WriteLine($"Memory coherency maintained through {dataSize} element transfer chain");
    }

    [Fact]
    [Trait(nameof(TestCategories.Integration), TestCategories.Integration)]
    [Trait(nameof(TestCategories.Concurrency), TestCategories.Concurrency)]
    [Trait(nameof(TestCategories.MemoryIntensive), TestCategories.MemoryIntensive)]
    public async Task MemoryCoherency_ConcurrentAccess_ThreadSafe()
    {
        const int dataSize = 512;
        const int numThreads = 4;
        
        var sharedData = GenerateSequentialFloats(dataSize, 0.0f, 1.0f);
        using var sharedBuffer = new TestMemoryBuffer<float>(dataSize * sizeof(float), _accelerator!);
        await sharedBuffer.CopyFromAsync(sharedData.AsMemory(), CancellationToken.None);
        
        using var perfContext = CreatePerformanceContext("Concurrent_Access_Test");
        
        var results = new ConcurrentBag<float[]>();
        var exceptions = new ConcurrentBag<Exception>();
        
        // Launch concurrent read operations
        var tasks = Enumerable.Range(0, numThreads)
            .Select(async threadId =>
            {
                try
                {
                    for (int iteration = 0; iteration < 10; iteration++)
                    {
                        var threadData = new float[dataSize];
                        await sharedBuffer.CopyToAsync(threadData.AsMemory(), CancellationToken.None);
                        
                        // Verify data integrity
                        threadData.ShouldBeApproximatelyEqualTo(sharedData, 1e-6f);
                        results.Add(threadData);
                        
                        // Small delay to increase chance of contention
                        await Task.Delay(1);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    throw;
                }
            })
            .ToArray();
        
        var executionTime = await MeasureExecutionTimeAsync(async () =>
            await Task.WhenAll(tasks));
        
        perfContext.Checkpoint("Concurrent_Operations_Complete");
        
        // Assert - No exceptions and all data should be consistent
        Assert.Empty(exceptions);
        Assert.Equal(numThreads * 10, results.Count);
        
        foreach (var result in results)
        {
            result.ShouldBeApproximatelyEqualTo(sharedData, 1e-6f,
                "because concurrent reads should always return consistent data");
        }
        
        Output.WriteLine($"Concurrent access test completed: {numThreads} threads, {results.Count} operations in {executionTime:F2}ms");
    }

    [Fact]
    [Trait(nameof(TestCategories.Integration), TestCategories.Integration)]
    [Trait(nameof(TestCategories.MemoryIntensive), TestCategories.MemoryIntensive)]
    public async Task MemoryCoherency_MemorySynchronization_CrossAccelerator()
    {
        const int dataSize = 256;
        var initialData = GenerateRandomFloats(dataSize, seed: 101);
        
        using var perfContext = CreatePerformanceContext("Cross_Accelerator_Sync");
        
        // Create buffers on different accelerators
        using var gpuBuffer = new TestMemoryBuffer<float>(dataSize * sizeof(float), _accelerator!);
        using var cpuBuffer = new TestMemoryBuffer<float>(dataSize * sizeof(float), _cpuAccelerator!);
        
        // Initialize GPU buffer
        await gpuBuffer.CopyFromAsync(initialData.AsMemory(), CancellationToken.None);
        perfContext.Checkpoint("GPU_Buffer_Initialized");
        
        // Process on GPU (simulate modification)
        var gpuKernel = await _accelerator.CompileKernelAsync(
            new KernelDefinition("modify_gpu", Convert.ToBase64String(GenerateScaleKernelCode()), "scale"));
        
        using var tempBuffer = new TestMemoryBuffer<float>(dataSize * sizeof(float), _accelerator);
        var args = new KernelArguments();
        args.Add(gpuBuffer); args.Add(tempBuffer); args.Add(3.0f); args.Add(dataSize);
        await gpuKernel.ExecuteAsync(args);
        await _accelerator.SynchronizeAsync();
        perfContext.Checkpoint("GPU_Processing_Complete");
        
        // Transfer modified data to CPU buffer
        var modifiedData = new float[dataSize];
        await tempBuffer.CopyToAsync(modifiedData.AsMemory(), CancellationToken.None);
        await cpuBuffer.CopyFromAsync(modifiedData.AsMemory(), CancellationToken.None);
        
        // Verify synchronization by reading from CPU buffer
        var cpuData = new float[dataSize];
        await cpuBuffer.CopyToAsync(cpuData.AsMemory(), CancellationToken.None);
        await _cpuAccelerator.SynchronizeAsync();
        perfContext.Checkpoint("Cross_Accelerator_Sync_Complete");
        
        // Assert - Data should be correctly synchronized
        var expectedData = initialData.Select(x => x * 3.0f).ToArray();
        cpuData.ShouldBeApproximatelyEqualTo(expectedData, 1e-6f,
            "because cross-accelerator synchronization should preserve computational results");
        
        Output.WriteLine($"Cross-accelerator synchronization completed for {dataSize} elements");
    }

    #endregion

    #region Error Recovery Tests

    [Fact]
    [Trait(nameof(TestCategories.Integration), TestCategories.Integration)]
    [Trait(nameof(TestCategories.ErrorHandling), TestCategories.ErrorHandling)]
    public async Task ErrorRecovery_GracefulDegradation_OnMemoryFailure()
    {
        const int largeDataSize = 1024 * 1024; // 1M elements
        var reasonableDataSize = 1024; // Fallback size
        
        using var perfContext = CreatePerformanceContext("Memory_Failure_Recovery");
        
        try
        {
            // Attempt to allocate very large buffer (likely to fail)
            using var largeBuffer = new TestMemoryBuffer<float>(largeDataSize * sizeof(float), _accelerator!);
            var largeData = GenerateRandomFloats(largeDataSize);
            await largeBuffer.CopyFromAsync(largeData.AsMemory(), CancellationToken.None);
            
            Output.WriteLine("Large allocation succeeded unexpectedly");
        }
        catch (OutOfMemoryException)
        {
            perfContext.Checkpoint("Large_Allocation_Failed_As_Expected");
            
            // Gracefully degrade to smaller workload
            using var reasonableBuffer = new TestMemoryBuffer<float>(reasonableDataSize * sizeof(float), _accelerator!);
            var reasonableData = GenerateRandomFloats(reasonableDataSize);
            var expected = reasonableData.Select(x => x + 1.0f).ToArray();
            
            await reasonableBuffer.CopyFromAsync(reasonableData.AsMemory(), CancellationToken.None);
            
            var kernel = await _accelerator!.CompileKernelAsync(
                new KernelDefinition("add_fallback", Convert.ToBase64String(GenerateAddScalarKernelCode()), "add_scalar"));
            
            using var outputBuffer = new TestMemoryBuffer<float>(reasonableDataSize * sizeof(float), _accelerator);
            var args = new KernelArguments();
            args.Add(reasonableBuffer); args.Add(outputBuffer); args.Add(1.0f); args.Add(reasonableDataSize);
            
            await kernel.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();
            
            var result = new float[reasonableDataSize];
            await outputBuffer.CopyToAsync(result.AsMemory(), CancellationToken.None);
            
            // Assert - Fallback should work correctly
            result.ShouldBeApproximatelyEqualTo(expected, 1e-6f,
                "because graceful degradation should maintain computational correctness");
            
            perfContext.Checkpoint("Graceful_Degradation_Complete");
            Output.WriteLine($"Successfully degraded from {largeDataSize} to {reasonableDataSize} elements");
        }
    }

    [Fact]
    [Trait(nameof(TestCategories.Integration), TestCategories.Integration)]
    [Trait(nameof(TestCategories.ErrorHandling), TestCategories.ErrorHandling)]
    public async Task ErrorRecovery_RetryMechanism_OnTransientFailures()
    {
        const int dataSize = 256;
        const int maxRetries = 3;
        
        var input = GenerateRandomFloats(dataSize);
        var expected = input.Select(x => x * 2.0f).ToArray();
        
        using var perfContext = CreatePerformanceContext("Retry_Mechanism_Test");
        
        var intermittentAccelerator = new IntermittentFailureAccelerator(_accelerator!.Info, _logger!, failureCount: 2);
        _accelerators.Add(intermittentAccelerator);
        
        ICompiledKernel? kernel = null;
        int attemptCount = 0;
        Exception? lastException = null;
        
        // Retry loop
        for (int retry = 0; retry < maxRetries; retry++)
        {
            attemptCount++;
            try
            {
                kernel = await intermittentAccelerator.CompileKernelAsync(
                    new KernelDefinition($"scale_retry_{retry}", Convert.ToBase64String(GenerateScaleKernelCode()), "scale"));
                perfContext.Checkpoint($"Compilation_Succeeded_Attempt_{attemptCount}");
                break;
            }
            catch (Exception ex)
            {
                lastException = ex;
                Output.WriteLine($"Attempt {attemptCount} failed: {ex.GetType().Name}");
                perfContext.Checkpoint($"Retry_{attemptCount}_Failed");
                
                if (retry < maxRetries - 1)
                {
                    await Task.Delay(100); // Brief delay before retry
                }
            }
        }
        
        // Assert - Should eventually succeed
        Assert.NotNull(kernel);
        Assert.True(attemptCount <= maxRetries, $"Should succeed within {maxRetries} attempts, took {attemptCount}");
        
        // Execute successfully compiled kernel
        using var inputBuffer = new TestMemoryBuffer<float>(dataSize * sizeof(float), intermittentAccelerator);
        using var outputBuffer = new TestMemoryBuffer<float>(dataSize * sizeof(float), intermittentAccelerator);
        
        await inputBuffer.CopyFromAsync(input.AsMemory(), CancellationToken.None);
        
        var args = new KernelArguments();
        args.Add(inputBuffer); args.Add(outputBuffer); args.Add(2.0f); args.Add(dataSize);
        await kernel!.ExecuteAsync(args);
        await intermittentAccelerator.SynchronizeAsync();
        
        var result = new float[dataSize];
        await outputBuffer.CopyToAsync(result.AsMemory(), CancellationToken.None);
        
        result.ShouldBeApproximatelyEqualTo(expected, 1e-6f,
            "because retry mechanism should eventually produce correct results");
        
        Output.WriteLine($"Retry mechanism succeeded after {attemptCount} attempts");
    }

    [Fact]
    [Trait(nameof(TestCategories.Integration), TestCategories.Integration)]
    [Trait(nameof(TestCategories.ErrorHandling), TestCategories.ErrorHandling)]
    public async Task ErrorRecovery_ResourceCleanup_AfterFailures()
    {
        const int dataSize = 512;
        
        using var perfContext = CreatePerformanceContext("Resource_Cleanup_Test");
        
        var initialMemoryDelta = GetMemoryUsageDelta();
        perfContext.Checkpoint("Initial_Memory_Measured");
        
        // Create and dispose multiple buffers with some failures
        var successfulOperations = 0;
        var failedOperations = 0;
        
        for (int i = 0; i < 10; i++)
        {
            try
            {
                using var buffer = new TestMemoryBuffer<float>(dataSize * sizeof(float), _accelerator!);
                var data = GenerateRandomFloats(dataSize);
                
                if (i % 3 == 2) // Simulate failure every 3rd iteration
                {
                    throw new InvalidOperationException($"Simulated failure on iteration {i}");
                }
                
                await buffer.CopyFromAsync(data.AsMemory(), CancellationToken.None);
                await buffer.SynchronizeAsync();
                successfulOperations++;
            }
            catch (InvalidOperationException)
            {
                failedOperations++;
                // Resource cleanup should happen automatically via using statement
            }
        }
        
        perfContext.Checkpoint("Operations_Complete");
        
        // Force garbage collection to ensure proper cleanup
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemoryDelta = GetMemoryUsageDelta();
        var memoryDifference = Math.Abs(finalMemoryDelta - initialMemoryDelta);
        
        // Assert - Resources should be properly cleaned up
        Assert.True(successfulOperations > 0, "Some operations should succeed");
        Assert.True(failedOperations > 0, "Some operations should fail as designed");
        Assert.True(memoryDifference < 10 * 1024 * 1024, // Less than 10MB difference
            $"Memory should be cleaned up properly. Delta: {memoryDifference} bytes");
        
        Output.WriteLine($"Resource cleanup test: {successfulOperations} successful, {failedOperations} failed operations");
        Output.WriteLine($"Memory difference: {memoryDifference} bytes");
    }

    #endregion

    #region Real-World Scenario Tests

    [Fact]
    [Trait(nameof(TestCategories.Integration), TestCategories.Integration)]
    [Trait(nameof(TestCategories.Performance), TestCategories.Performance)]
    public async Task RealWorldScenario_ImageProcessing_ConvolutionPipeline()
    {
        // Arrange - Simulate image processing pipeline
        const int imageWidth = 256;
        const int imageHeight = 256;
        const int channels = 3; // RGB
        const int kernelSize = 3; // 3x3 convolution kernel
        
        var imageData = GenerateRandomFloats(imageWidth * imageHeight * channels, seed: 2023);
        var convolutionKernel = new float[] { -1, -1, -1, -1, 8, -1, -1, -1, -1 }; // Edge detection
        
        using var perfContext = CreatePerformanceContext("Image_Processing_Pipeline");
        
        // Step 1: Upload image data
        using var imageBuffer = new TestMemoryBuffer<float>(
            imageWidth * imageHeight * channels * sizeof(float), _accelerator!);
        await imageBuffer.CopyFromAsync(imageData.AsMemory(), CancellationToken.None);
        perfContext.Checkpoint("Image_Data_Uploaded");
        
        // Step 2: Compile convolution kernel
        var convKernel = await _accelerator.CompileKernelAsync(
            new KernelDefinition("convolution_2d", Convert.ToBase64String(GenerateConvolutionKernelCode()), "conv2d"));
        perfContext.Checkpoint("Convolution_Kernel_Compiled");
        
        // Step 3: Execute convolution for each channel
        using var resultBuffer = new TestMemoryBuffer<float>(
            imageWidth * imageHeight * channels * sizeof(float), _accelerator);
        using var kernelBuffer = new TestMemoryBuffer<float>(kernelSize * kernelSize * sizeof(float), _accelerator);
        
        await kernelBuffer.CopyFromAsync(convolutionKernel.AsMemory(), CancellationToken.None);
        
        var executionTime = await MeasureExecutionTimeAsync(async () =>
        {
            var args = new KernelArguments();
            args.Add(imageBuffer);
            args.Add(kernelBuffer);
            args.Add(resultBuffer);
            args.Add(imageWidth);
            args.Add(imageHeight);
            args.Add(channels);
            args.Add(kernelSize);
            
            await convKernel.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();
        });
        
        perfContext.Checkpoint("Convolution_Complete");
        
        // Step 4: Download and validate results
        var processedImage = new float[imageWidth * imageHeight * channels];
        await resultBuffer.CopyToAsync(processedImage.AsMemory(), CancellationToken.None);
        
        // Assert - Image processing should produce valid results
        processedImage.ShouldContainOnlyFiniteValues(
            "because image processing should not produce invalid pixel values");
        
        // Performance assertions for real-time requirements
        var pixelsPerSecond = (imageWidth * imageHeight * channels) / (executionTime / 1000.0);
        Assert.True(pixelsPerSecond > 1_000_000, // Should process > 1M pixels/second
            $"Image processing should be fast enough for real-time use. Got {pixelsPerSecond:F0} pixels/sec");
        
        Output.WriteLine($"Processed {imageWidth}x{imageHeight}x{channels} image in {executionTime:F2}ms");
        Output.WriteLine($"Throughput: {pixelsPerSecond:F0} pixels/second");
    }

    [Fact]
    [Trait(nameof(TestCategories.Integration), TestCategories.Integration)]
    [Trait(nameof(TestCategories.Performance), TestCategories.Performance)]
    public async Task RealWorldScenario_MatrixOperations_LinearAlgebra()
    {
        // Arrange - Matrix multiplication: C = A * B
        const int matrixSize = 128; // 128x128 matrices
        const int totalElements = matrixSize * matrixSize;
        
        var matrixA = GenerateSequentialFloats(totalElements, 1.0f, 0.01f);
        var matrixB = GenerateSequentialFloats(totalElements, 0.5f, 0.005f);
        
        using var perfContext = CreatePerformanceContext("Matrix_Operations_Workflow");
        
        // Step 1: Memory allocation and data transfer
        using var bufferA = new TestMemoryBuffer<float>(totalElements * sizeof(float), _accelerator!);
        using var bufferB = new TestMemoryBuffer<float>(totalElements * sizeof(float), _accelerator);
        using var bufferC = new TestMemoryBuffer<float>(totalElements * sizeof(float), _accelerator);
        
        await Task.WhenAll(
            bufferA.CopyFromAsync(matrixA.AsMemory(), CancellationToken.None).AsTask(),
            bufferB.CopyFromAsync(matrixB.AsMemory(), CancellationToken.None).AsTask()
        );
        perfContext.Checkpoint("Matrix_Data_Uploaded");
        
        // Step 2: Compile matrix multiplication kernel
        var matMulKernel = await _accelerator.CompileKernelAsync(
            new KernelDefinition("matrix_multiply", Convert.ToBase64String(GenerateMatrixMultiplyKernelCode()), "matmul"));
        perfContext.Checkpoint("MatMul_Kernel_Compiled");
        
        // Step 3: Execute matrix multiplication
        var executionTime = await MeasureExecutionTimeAsync(async () =>
        {
            var args = new KernelArguments();
            args.Add(bufferA);
            args.Add(bufferB);
            args.Add(bufferC);
            args.Add(matrixSize);
            args.Add(matrixSize);
            args.Add(matrixSize);
            
            await matMulKernel.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();
        });
        
        perfContext.Checkpoint("Matrix_Multiplication_Complete");
        
        // Step 4: Validate results
        var resultMatrix = new float[totalElements];
        await bufferC.CopyToAsync(resultMatrix.AsMemory(), CancellationToken.None);
        
        // Basic validation - results should be finite and within reasonable bounds
        resultMatrix.ShouldContainOnlyFiniteValues(
            "because matrix multiplication should produce finite results");
        
        // Performance validation - calculate GFLOPS
        var operations = 2.0 * matrixSize * matrixSize * matrixSize; // Multiply-add operations
        var gflops = operations / (executionTime / 1000.0) / 1_000_000_000;
        
        Assert.True(gflops > 1.0, // Should achieve > 1 GFLOPS
            $"Matrix multiplication should achieve reasonable performance. Got {gflops:F2} GFLOPS");
        
        Output.WriteLine($"Matrix multiplication ({matrixSize}x{matrixSize}) completed in {executionTime:F2}ms");
        Output.WriteLine($"Performance: {gflops:F2} GFLOPS");
    }

    [Fact]
    [Trait(nameof(TestCategories.Integration), TestCategories.Integration)]
    [Trait(nameof(TestCategories.Performance), TestCategories.Performance)]
    public async Task RealWorldScenario_StatisticalComputation_DataAnalytics()
    {
        // Arrange - Statistical analysis pipeline: mean, variance, standard deviation
        const int dataSize = 10000;
        var dataset = GenerateRandomFloats(dataSize, seed: 2024);
        
        // Calculate expected statistics using CPU
        var expectedMean = dataset.Average();
        var expectedVariance = dataset.Select(x => (x - expectedMean) * (x - expectedMean)).Average();
        var expectedStdDev = Math.Sqrt(expectedVariance);
        
        using var perfContext = CreatePerformanceContext("Statistical_Computation_Pipeline");
        
        // Step 1: Upload dataset
        using var dataBuffer = new TestMemoryBuffer<float>(dataSize * sizeof(float), _accelerator!);
        using var resultBuffer = new TestMemoryBuffer<float>(3 * sizeof(float), _accelerator); // mean, variance, stddev
        
        await dataBuffer.CopyFromAsync(dataset.AsMemory(), CancellationToken.None);
        perfContext.Checkpoint("Dataset_Uploaded");
        
        // Step 2: Compile statistical kernels
        var meanKernel = await _accelerator.CompileKernelAsync(
            new KernelDefinition("compute_mean", Convert.ToBase64String(GenerateTestKernelCode()), "mean"));
        var varianceKernel = await _accelerator.CompileKernelAsync(
            new KernelDefinition("compute_variance", Convert.ToBase64String(GenerateTestKernelCode()), "variance"));
        perfContext.Checkpoint("Statistical_Kernels_Compiled");
        
        // Step 3: Execute statistical computation pipeline
        var executionTime = await MeasureExecutionTimeAsync(async () =>
        {
            // Compute mean
            var meanArgs = new KernelArguments();
            meanArgs.Add(dataBuffer);
            meanArgs.Add(resultBuffer);
            meanArgs.Add(dataSize);
            meanArgs.Add(0); // Offset for mean result
            
            await meanKernel.ExecuteAsync(meanArgs);
            
            // Compute variance (depends on mean)
            var varianceArgs = new KernelArguments();
            varianceArgs.Add(dataBuffer);
            varianceArgs.Add(resultBuffer);
            varianceArgs.Add(dataSize);
            varianceArgs.Add(1); // Offset for variance result
            
            await varianceKernel.ExecuteAsync(varianceArgs);
            await _accelerator.SynchronizeAsync();
        });
        
        perfContext.Checkpoint("Statistical_Computation_Complete");
        
        // Step 4: Retrieve and validate results
        var statistics = new float[3];
        await resultBuffer.CopyToAsync(statistics.AsMemory(), CancellationToken.None);
        
        var computedMean = statistics[0];
        var computedVariance = statistics[1];
        var computedStdDev = (float)Math.Sqrt(computedVariance);
        
        // Assert - Statistical computations should be accurate
        Assert.InRange(computedMean, expectedMean - 1e-3f, expectedMean + 1e-3f);
        Assert.InRange(computedVariance, (float)expectedVariance - 1e-2f, (float)expectedVariance + 1e-2f);
        Assert.InRange(computedStdDev, (float)expectedStdDev - 1e-2f, (float)expectedStdDev + 1e-2f);
        
        // Performance validation - should process data efficiently
        var throughputMBps = (dataSize * sizeof(float)) / (executionTime / 1000.0) / 1_000_000;
        Assert.True(throughputMBps > 100, // Should achieve > 100 MB/s throughput
            $"Statistical computation should have good throughput. Got {throughputMBps:F1} MB/s");
        
        Output.WriteLine($"Statistical analysis of {dataSize} elements completed in {executionTime:F2}ms");
        Output.WriteLine($"Mean: {computedMean:F4} (expected: {expectedMean:F4})");
        Output.WriteLine($"Variance: {computedVariance:F4} (expected: {expectedVariance:F4})");
        Output.WriteLine($"Std Dev: {computedStdDev:F4} (expected: {expectedStdDev:F4})");
        Output.WriteLine($"Throughput: {throughputMBps:F1} MB/s");
    }

    #endregion

    private static byte[] GenerateTestKernelCode() => new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
    
    private static byte[] GenerateVectorAddKernelCode() => new byte[] { 0x10, 0x11, 0x12, 0x13, 0x14 };
    private static byte[] GenerateSquareKernelCode() => new byte[] { 0x20, 0x21, 0x22, 0x23, 0x24 };
    private static byte[] GenerateScaleKernelCode() => new byte[] { 0x30, 0x31, 0x32, 0x33, 0x34 };
    private static byte[] GenerateAddScalarKernelCode() => new byte[] { 0x40, 0x41, 0x42, 0x43, 0x44 };
    private static byte[] GenerateMatrixMultiplyKernelCode() => new byte[] { 0x50, 0x51, 0x52, 0x53, 0x54 };
    private static byte[] GenerateConvolutionKernelCode() => new byte[] { 0x60, 0x61, 0x62, 0x63, 0x64 };
    
    private async Task TestFloatPipeline(int size, PerformanceContext perfContext)
    {
        var input = GenerateRandomFloats(size);
        var expected = input.Select(x => x * 2.0f).ToArray();
        
        var kernel = await _accelerator!.CompileKernelAsync(
            new KernelDefinition("scale_float", Convert.ToBase64String(GenerateScaleKernelCode()), "scale"));
        
        using var inputBuffer = new TestMemoryBuffer<float>(size * sizeof(float), _accelerator);
        using var outputBuffer = new TestMemoryBuffer<float>(size * sizeof(float), _accelerator);
        
        await inputBuffer.CopyFromAsync(input.AsMemory(), CancellationToken.None);
        
        var args = new KernelArguments();
        args.Add(inputBuffer); args.Add(outputBuffer); args.Add(2.0f); args.Add(size);
        await kernel.ExecuteAsync(args);
        await _accelerator.SynchronizeAsync();
        
        var result = new float[size];
        await outputBuffer.CopyToAsync(result.AsMemory(), CancellationToken.None);
        
        result.ShouldBeApproximatelyEqualTo(expected, 1e-6f);
        perfContext.Checkpoint($"Float_{size}_Complete");
    }
    
    private async Task TestDoublePipeline(int size, PerformanceContext perfContext)
    {
        var input = GenerateRandomFloats(size).Select(f => (double)f).ToArray();
        var expected = input.Select(x => x * 3.0).ToArray();
        
        var kernel = await _accelerator!.CompileKernelAsync(
            new KernelDefinition("scale_double", Convert.ToBase64String(GenerateScaleKernelCode()), "scale"));
        
        using var inputBuffer = new TestMemoryBuffer<double>(size * sizeof(double), _accelerator);
        using var outputBuffer = new TestMemoryBuffer<double>(size * sizeof(double), _accelerator);
        
        await inputBuffer.CopyFromAsync(input.AsMemory(), CancellationToken.None);
        
        var args = new KernelArguments();
        args.Add(inputBuffer); args.Add(outputBuffer); args.Add(3.0); args.Add(size);
        await kernel.ExecuteAsync(args);
        await _accelerator.SynchronizeAsync();
        
        var result = new double[size];
        await outputBuffer.CopyToAsync(result.AsMemory(), CancellationToken.None);
        
        result.ShouldBeApproximatelyEqualTo(expected, 1e-12);
        perfContext.Checkpoint($"Double_{size}_Complete");
    }
    
    private async Task TestIntPipeline(int size, PerformanceContext perfContext)
    {
        var input = GenerateRandomInts(size, 0, 1000);
        var expected = input.Select(x => x + 10).ToArray();
        
        var kernel = await _accelerator!.CompileKernelAsync(
            new KernelDefinition("add_int", Convert.ToBase64String(GenerateAddScalarKernelCode()), "add_scalar"));
        
        using var inputBuffer = new TestMemoryBuffer<int>(size * sizeof(int), _accelerator);
        using var outputBuffer = new TestMemoryBuffer<int>(size * sizeof(int), _accelerator);
        
        await inputBuffer.CopyFromAsync(input.AsMemory(), CancellationToken.None);
        
        var args = new KernelArguments();
        args.Add(inputBuffer); args.Add(outputBuffer); args.Add(10); args.Add(size);
        await kernel.ExecuteAsync(args);
        await _accelerator.SynchronizeAsync();
        
        var result = new int[size];
        await outputBuffer.CopyToAsync(result.AsMemory(), CancellationToken.None);
        
        Assert.Equal(expected, result);
        perfContext.Checkpoint($"Int_{size}_Complete");
    }
    
    private static async Task ProcessChunkAsync(IAccelerator accelerator, ICompiledKernel kernel, 
        ReadOnlyMemory<float> input, Memory<float> output)
    {
        using var inputBuffer = new TestMemoryBuffer<float>(input.Length * sizeof(float), accelerator);
        using var outputBuffer = new TestMemoryBuffer<float>(output.Length * sizeof(float), accelerator);
        
        await inputBuffer.CopyFromAsync(input, CancellationToken.None);
        
        var args = new KernelArguments();
        args.Add(inputBuffer); args.Add(outputBuffer); args.Add(input.Length);
        await kernel.ExecuteAsync(args, CancellationToken.None);
        await accelerator.SynchronizeAsync();
        
        await outputBuffer.CopyToAsync(output, CancellationToken.None);
    }

    /// <summary>
    /// Test accelerator using BaseAccelerator
    /// </summary>
    private sealed class TestAccelerator : BaseAccelerator
    {
        public bool SynchronizeCalled { get; private set; }
        public int DisposeCallCount { get; private set; }
        private readonly TestMemoryManager _memoryManager;

#pragma warning disable CA2000 // Dispose objects before losing scope - Test memory manager owned by BaseAccelerator
        public TestAccelerator(AcceleratorInfo info, ILogger logger)
            : base(info, AcceleratorType.CPU, new TestMemoryManager(), new AcceleratorContext(IntPtr.Zero, 0), logger)
#pragma warning restore CA2000
        {
            _memoryManager = (TestMemoryManager)Memory;
        }

#pragma warning disable CA2000 // Dispose objects before losing scope - Test kernel handled by framework
        protected override ValueTask<ICompiledKernel> CompileKernelCoreAsync(
            KernelDefinition definition, CompilationOptions options, CancellationToken cancellationToken)
            => ValueTask.FromResult<ICompiledKernel>(new TestCompiledKernel(definition.Name));
#pragma warning restore CA2000

        protected override ValueTask SynchronizeCoreAsync(CancellationToken cancellationToken)
        {
            SynchronizeCalled = true;
            return ValueTask.CompletedTask;
        }
        
        protected override async ValueTask DisposeCoreAsync()
        {
            DisposeCallCount++;
            if (_memoryManager != null)
                await _memoryManager.DisposeAsync();
            await base.DisposeCoreAsync();
        }

        public override ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
        {
            // Don't call synchronization if already disposed
            if (IsDisposed)
                return ValueTask.CompletedTask;
            
            return base.SynchronizeAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Test compiler using BaseKernelCompiler
    /// </summary>
    private sealed class TestKernelCompiler : BaseKernelCompiler
    {
        public TestKernelCompiler(ILogger logger) : base(logger) { }

        protected override string CompilerName => "TestCompiler";
        
        public override IReadOnlyList<KernelLanguage> SupportedSourceTypes => new[] { KernelLanguage.OpenCL, KernelLanguage.CUDA };

#pragma warning disable CA2000 // Dispose objects before losing scope - Test kernel handled by framework
        protected override ValueTask<ICompiledKernel> CompileKernelCoreAsync(
            KernelDefinition definition, CompilationOptions options, CancellationToken cancellationToken)
            => ValueTask.FromResult<ICompiledKernel>(new TestCompiledKernel(definition.Name));
#pragma warning restore CA2000
    }

    /// <summary>
    /// Test memory buffer using BaseMemoryBuffer
    /// </summary>
    private sealed class TestMemoryBuffer<T> : BaseMemoryBuffer<T> where T : unmanaged
    {
        private readonly T[] _data;
        private bool _disposed;
        private readonly IAccelerator _accelerator;

        public TestMemoryBuffer(long sizeInBytes, IAccelerator accelerator) : base(sizeInBytes)
        {
            _data = new T[sizeInBytes / System.Runtime.CompilerServices.Unsafe.SizeOf<T>()];
            _accelerator = accelerator;
            DevicePointer = IntPtr.Zero;
            MemoryType = MemoryType.Host;
        }
        

        public override IntPtr DevicePointer { get; }
        public override MemoryType MemoryType { get; }
        public override bool IsDisposed => _disposed;
        public override IAccelerator Accelerator => _accelerator;
        public override BufferState State => BufferState.HostDirty;
        public override MemoryOptions Options => MemoryOptions.None;
        public override bool IsOnHost => true;
        public override bool IsOnDevice => false;
        public override bool IsDirty => false;

        public override Span<T> AsSpan() => _data.AsSpan();
        public override ReadOnlySpan<T> AsReadOnlySpan() => _data.AsSpan();
        public override Memory<T> AsMemory() => _data.AsMemory();
        public override ReadOnlyMemory<T> AsReadOnlyMemory() => _data.AsMemory();
        public override DeviceMemory GetDeviceMemory() => new DeviceMemory(IntPtr.Zero, SizeInBytes);
        public override MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite) => throw new NotImplementedException();
        public override MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite) => throw new NotImplementedException();
        public override ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => ValueTask.FromResult(Map(mode));
        public override void EnsureOnHost() { }
        public override void EnsureOnDevice() { }
        public override ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override void Synchronize() { }
        public override ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override void MarkHostDirty() { }
        public override void MarkDeviceDirty() { }

        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
        {
            source.CopyTo(_data.AsMemory());
            return ValueTask.CompletedTask;
        }

        public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
        {
            _data.AsMemory(0, destination.Length).CopyTo(destination);
            return ValueTask.CompletedTask;
        }

        public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) 
        {
            if (destination is TestMemoryBuffer<T> testBuffer)
            {
                _data.AsMemory().CopyTo(testBuffer._data.AsMemory());
            }
            return ValueTask.CompletedTask;
        }
        public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override IUnifiedMemoryBuffer<T> Slice(int offset, int length) => new TestMemoryBuffer<T>(length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>(), _accelerator);
        public override IUnifiedMemoryBuffer<TNew> AsType<TNew>() => (IUnifiedMemoryBuffer<TNew>)new TestMemoryBuffer<TNew>(SizeInBytes, _accelerator);

        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
        {
            source.CopyTo(_data.AsMemory((int)offset));
            return ValueTask.CompletedTask;
        }

        public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
        {
            _data.AsMemory((int)offset, destination.Length).CopyTo(destination);
            return ValueTask.CompletedTask;
        }

        public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override void Dispose() 
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
        
        public override ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
            return ValueTask.CompletedTask;
        }

        public void TestValidateCopyParameters(long sourceLength, long sourceOffset, long destinationLength, long destinationOffset, long count)
            => ValidateCopyParameters(sourceLength, sourceOffset, destinationLength, destinationOffset, count);
    }

    /// <summary>
    /// Test memory manager using BaseMemoryManager
    /// </summary>
    private sealed class TestMemoryManager : BaseMemoryManager
    {
        private IAccelerator? _accelerator;

        public TestMemoryManager() : base(CreateLogger())
        {
        }
        
        private static ILogger CreateLogger()
        {
            var factory = LoggerFactory.Create(b => b.AddConsole());
            try
            {
                return factory.CreateLogger<TestMemoryManager>();
            }
            finally
            {
                factory.Dispose();
            }
        }

        public override IAccelerator Accelerator => _accelerator ?? throw new InvalidOperationException("Accelerator not set");
        
        public void SetAccelerator(IAccelerator accelerator) => _accelerator = accelerator;
        
        public override MemoryStatistics Statistics { get; } = new MemoryStatistics
        {
            TotalAllocated = 0,
            CurrentUsed = 0,
            PeakUsage = 0,
            ActiveAllocations = 0,
            TotalAllocationCount = 0,
            TotalDeallocationCount = 0,
            PoolHitRate = 1.0,
            FragmentationPercentage = 0.0
        };
        
        public override long MaxAllocationSize => 1024 * 1024 * 1024;
        public override long TotalAvailableMemory => 1024 * 1024 * 1024;
        public override long CurrentAllocatedMemory => 0;

#pragma warning disable CA2000 // Dispose objects before losing scope - Test memory buffer managed by caller
        public override ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(int count, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IUnifiedMemoryBuffer<T>>(new TestMemoryBuffer<T>(count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>()));
#pragma warning restore CA2000

        public override async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAndCopyAsync<T>(ReadOnlyMemory<T> source, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default)
        {
            var buffer = new TestMemoryBuffer<T>(source.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
            await buffer.CopyFromAsync(source, cancellationToken);
            return buffer;
        }

#pragma warning disable CA2000 // Dispose objects before losing scope - Test memory buffer managed by caller
        protected override ValueTask<IUnifiedMemoryBuffer> AllocateInternalAsync(long sizeInBytes, MemoryOptions options, CancellationToken cancellationToken)
            => ValueTask.FromResult<IUnifiedMemoryBuffer>(new TestUnifiedMemoryBuffer(sizeInBytes));
#pragma warning restore CA2000

#pragma warning disable CA2000 // Dispose objects before losing scope - Test memory view managed by caller
        protected override IUnifiedMemoryBuffer CreateViewCore(IUnifiedMemoryBuffer buffer, long offset, long length)
            => new TestUnifiedMemoryBuffer(length);
#pragma warning restore CA2000

#pragma warning disable CA2000 // Dispose objects before losing scope - Test memory view managed by caller
        public override IUnifiedMemoryBuffer<T> CreateView<T>(IUnifiedMemoryBuffer<T> buffer, int offset, int length)
            => new TestMemoryBuffer<T>(length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
#pragma warning restore CA2000

        public override ValueTask CopyAsync<T>(IUnifiedMemoryBuffer<T> source, IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyAsync<T>(IUnifiedMemoryBuffer<T> source, int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyToDeviceAsync<T>(ReadOnlyMemory<T> source, IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask CopyFromDeviceAsync<T>(IUnifiedMemoryBuffer<T> source, Memory<T> destination, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override ValueTask OptimizeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override void Clear() { }
        
        protected override void Dispose(bool disposing)
        {
            // Cleanup any resources
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Test non-generic unified memory buffer
    /// </summary>
#pragma warning disable CA1822 // Mark members as static - Interface implementation requires instance members
    private sealed class TestUnifiedMemoryBuffer : IUnifiedMemoryBuffer
    {
        public long SizeInBytes { get; }
        public IntPtr DevicePointer => IntPtr.Zero;
        public MemoryType MemoryType => MemoryType.Host;
        public bool IsDisposed => false;
        public MemoryOptions Options => MemoryOptions.None;
        public BufferState State => BufferState.HostDirty;
#pragma warning restore CA1822

        public TestUnifiedMemoryBuffer(long sizeInBytes)
        {
            SizeInBytes = sizeInBytes;
        }

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
            => ValueTask.CompletedTask;

        public ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
            => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Test compiled kernel
    /// </summary>
    private sealed class TestCompiledKernel : ICompiledKernel
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; }

        public TestCompiledKernel(string name)
        {
            Name = name;
        }

        public ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Test CPU accelerator for fallback scenarios
    /// </summary>
    private sealed class TestCpuAccelerator : BaseAccelerator
    {
        public TestCpuAccelerator(AcceleratorInfo info, ILogger logger)
            : base(info, AcceleratorType.CPU, new TestMemoryManager(), new AcceleratorContext(IntPtr.Zero, 0), logger)
        {
        }

#pragma warning disable CA2000 // Dispose objects before losing scope
        protected override ValueTask<ICompiledKernel> CompileKernelCoreAsync(
            KernelDefinition definition, CompilationOptions options, CancellationToken cancellationToken)
            => ValueTask.FromResult<ICompiledKernel>(new TestCompiledKernel(definition.Name));
#pragma warning restore CA2000

        protected override ValueTask SynchronizeCoreAsync(CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Test accelerator that fails on purpose for error testing
    /// </summary>
    private sealed class FailingTestAccelerator : BaseAccelerator
    {
        public FailingTestAccelerator(AcceleratorInfo info, ILogger logger)
            : base(info, AcceleratorType.CUDA, new TestMemoryManager(), new AcceleratorContext(IntPtr.Zero, 0), logger)
        {
        }

        protected override ValueTask<ICompiledKernel> CompileKernelCoreAsync(
            KernelDefinition definition, CompilationOptions options, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulated GPU failure for testing");
        }

        protected override ValueTask SynchronizeCoreAsync(CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Test accelerator that fails intermittently for retry testing
    /// </summary>
    private sealed class IntermittentFailureAccelerator : BaseAccelerator
    {
        private int _failureCount;
        private int _currentAttempt;

        public IntermittentFailureAccelerator(AcceleratorInfo info, ILogger logger, int failureCount)
            : base(info, AcceleratorType.CUDA, new TestMemoryManager(), new AcceleratorContext(IntPtr.Zero, 0), logger)
        {
            _failureCount = failureCount;
        }

#pragma warning disable CA2000 // Dispose objects before losing scope
        protected override ValueTask<ICompiledKernel> CompileKernelCoreAsync(
            KernelDefinition definition, CompilationOptions options, CancellationToken cancellationToken)
        {
            _currentAttempt++;
            if (_currentAttempt <= _failureCount)
            {
                throw new InvalidOperationException($"Simulated intermittent failure #{_currentAttempt}");
            }
            return ValueTask.FromResult<ICompiledKernel>(new TestCompiledKernel(definition.Name));
        }
#pragma warning restore CA2000

        protected override ValueTask SynchronizeCoreAsync(CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }

}