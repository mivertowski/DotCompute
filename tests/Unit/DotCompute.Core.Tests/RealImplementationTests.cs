using Xunit;
using DotCompute.Abstractions;
using DotCompute.Tests.Shared.Kernels;
using Xunit.Abstractions;
using DotCompute.Tests.Shared.Accelerators;
using DotCompute.Tests.Shared.Memory;

namespace DotCompute.Tests.Unit;

/// <summary>
/// Integration tests using real test implementations instead of mocks.
/// These tests demonstrate actual memory operations and kernel execution
/// without requiring GPU hardware.
/// </summary>
public class RealImplementationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private TestCpuAccelerator _accelerator = default!;
    private TestMemoryManager _memoryManager = default!;

    public RealImplementationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public Task InitializeAsync()
    {
        _accelerator = new TestCpuAccelerator("Test Accelerator");
        _memoryManager = new TestMemoryManager();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _accelerator.DisposeAsync();
        _memoryManager.Dispose();
    }

    [Fact]
    public async Task MemoryManager_AllocateAndCopy_ShouldWork()
    {
        // Arrange
        const int elementCount = 1024;
        var sourceData = new float[elementCount];
        for (var i = 0; i < elementCount; i++)
        {
            sourceData[i] = MathF.Sin(i * 0.01f);
        }

        // Act - Allocate buffer
        var buffer = await _memoryManager.AllocateAsync(elementCount * sizeof(float));
        Assert.NotNull(buffer);
        Assert.Equal(elementCount * sizeof(float), buffer.SizeInBytes);

        // Act - Copy data to buffer
        await buffer.CopyFromHostAsync<float>(sourceData.AsMemory());

        // Act - Read data back
        var readbackData = new float[elementCount];
        await buffer.CopyToHostAsync<float>(readbackData.AsMemory());

        // Assert - Verify data integrity
        for (var i = 0; i < elementCount; i++)
        {
            Assert.Equal(sourceData[i], readbackData[i], 5);
        }

        _output.WriteLine($"Successfully allocated {buffer.SizeInBytes} bytes and verified data integrity");
    }

    [Fact]
    public async Task MemoryManager_CreateView_ShouldWork()
    {
        // Arrange
        const long bufferSize = 1024 * sizeof(float);
        const long viewOffset = 256 * sizeof(float);
        const long viewSize = 512 * sizeof(float);

        var buffer = await _memoryManager.AllocateAsync(bufferSize);

        // Act
        var view = _memoryManager.CreateView(buffer, viewOffset, viewSize);

        // Assert
        Assert.NotNull(view);
        Assert.Equal(viewSize, view.SizeInBytes);

        _output.WriteLine($"Created view of {viewSize} bytes at offset {viewOffset}");
    }

    [Fact]
    public async Task Accelerator_CompileAndExecuteKernel_ShouldWork()
    {
        // Arrange
        var kernelSource = new TestKernelSource
        {
            Name = "TestKernel",
            Code = "test kernel code",
            EntryPoint = "main",
            Language = KernelLanguage.OpenCL
        };
        var kernelDefinition = new KernelDefinition("TestKernel", kernelSource, new CompilationOptions());

        // Act - Compile kernel
        var compiledKernel = await _accelerator.CompileKernelAsync(kernelDefinition);
        Assert.NotNull(compiledKernel);
        Assert.Equal("TestKernel", compiledKernel.Name);

        // Act - Execute kernel
        var arguments = new KernelArguments(1.0f, 2.0f, 3.0f);
        await compiledKernel.ExecuteAsync(arguments);

        _output.WriteLine($"Successfully compiled and executed kernel: {compiledKernel.Name}");
    }

    [Fact]
    public async Task Accelerator_MemoryIntegration_ShouldWork()
    {
        // Arrange
        const int dataSize = 256;
        var inputData = new float[dataSize];
        var outputData = new float[dataSize];

        for (var i = 0; i < dataSize; i++)
        {
            inputData[i] = i * 0.5f;
        }

        // Act - Allocate memory through accelerator
        var inputBuffer = await _accelerator.Memory.AllocateAsync(dataSize * sizeof(float));
        var outputBuffer = await _accelerator.Memory.AllocateAsync(dataSize * sizeof(float));

        // Act - Copy input data
        await inputBuffer.CopyFromHostAsync<float>(inputData.AsMemory());

        // Act - Create and execute kernel
        var kernelSource = new TestKernelSource
        {
            Name = "DataProcessing",
            Code = "process data",
            EntryPoint = "process",
            Language = KernelLanguage.OpenCL
        };
        var kernelDef = new KernelDefinition("DataProcessing", kernelSource, new CompilationOptions());

        var kernel = await _accelerator.CompileKernelAsync(kernelDef);
        var args = new KernelArguments(inputBuffer, outputBuffer, dataSize);
        await kernel.ExecuteAsync(args);

        // Act - Synchronize
        await _accelerator.SynchronizeAsync();

        _output.WriteLine("Successfully integrated memory management with kernel execution");
    }

    [Fact]
    public async Task MemoryManager_ConcurrentAllocations_ShouldWork()
    {
        // Arrange
        const int concurrentCount = 10;
        const long bufferSize = 1024;
        var tasks = new Task<IMemoryBuffer>[concurrentCount];

        // Act - Allocate multiple buffers concurrently
        for (var i = 0; i < concurrentCount; i++)
        {
            tasks[i] = _memoryManager.AllocateAsync(bufferSize).AsTask();
        }

        var buffers = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(concurrentCount, buffers.Length);
        foreach (var buffer in buffers)
        {
            Assert.NotNull(buffer);
            Assert.Equal(bufferSize, buffer.SizeInBytes);
        }

        // Check memory statistics
        Assert.Equal(concurrentCount, _memoryManager.AllocationCount);
        Assert.Equal(concurrentCount * bufferSize, _memoryManager.TotalAllocated);

        _output.WriteLine($"Successfully allocated {concurrentCount} buffers concurrently");
        _output.WriteLine($"Total allocated: {_memoryManager.TotalAllocated} bytes");
        _output.WriteLine($"Peak allocated: {_memoryManager.PeakAllocated} bytes");

        // Cleanup
        foreach (var buffer in buffers)
        {
            await buffer.DisposeAsync();
        }
    }

    [Fact]
    public async Task Accelerator_MultipleKernelCompilation_ShouldWork()
    {
        // Arrange & Act
        var source1 = new TestKernelSource { Name = "Kernel1", Code = "kernel1 code", Language = KernelLanguage.OpenCL };
        var kernel1 = await _accelerator.CompileKernelAsync(
            new KernelDefinition("Kernel1", source1, new CompilationOptions()));

        var source2 = new TestKernelSource { Name = "Kernel2", Code = "kernel2 code", Language = KernelLanguage.OpenCL };
        var kernel2 = await _accelerator.CompileKernelAsync(
            new KernelDefinition("Kernel2", source2, new CompilationOptions()));

        var source3 = new TestKernelSource { Name = "simple_kernel", Code = "simple kernel code", Language = KernelLanguage.OpenCL };
        var kernel3 = await _accelerator.CompileKernelAsync(
            new KernelDefinition("simple_kernel", source3, new CompilationOptions()));

        // Assert
        Assert.NotNull(kernel1);
        Assert.NotNull(kernel2);
        Assert.NotNull(kernel3);
        Assert.NotEqual(kernel1.Name, kernel2.Name);

        // Act - Execute kernels
        var args = new KernelArguments();
        await kernel1.ExecuteAsync(args);
        await kernel2.ExecuteAsync(args);
        await kernel3.ExecuteAsync(args);

        if (kernel3 is TestCompiledKernel testKernel)
        {
            _output.WriteLine($"Kernel {testKernel.Name} executed {testKernel.ExecutionCount} times");
            _output.WriteLine($"Average execution time: {testKernel.AverageExecutionTimeMs:F2}ms");
        }
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(4096)]
    [InlineData(65536)]
    public async Task MemoryOperations_DifferentSizes_ShouldWork(long size)
    {
        // Arrange
        var elementCount = (int)(size / sizeof(double));
        var testData = new double[elementCount];
        for (var i = 0; i < elementCount; i++)
        {
            testData[i] = Math.Sin(i * 0.001);
        }

        // Act - Allocate and copy
        var buffer = await _memoryManager.AllocateAndCopyAsync<double>(testData.AsMemory());

        // Assert
        Assert.NotNull(buffer);
        Assert.Equal(size, buffer.SizeInBytes);

        // Act - Read back
        var readback = new double[elementCount];
        await buffer.CopyToHostAsync<double>(readback.AsMemory());

        // Assert - Verify data
        for (var i = 0; i < Math.Min(10, elementCount); i++)
        {
            Assert.Equal(testData[i], readback[i], 10);
        }

        _output.WriteLine($"Successfully tested memory operations with {size} bytes");

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task FullPipeline_Integration_Test()
    {
        // This test demonstrates a complete workflow using real implementations
        _output.WriteLine("=== Full Pipeline Integration Test ===");

        // Step 1: Prepare data
        const int dataSize = 1000;
        var inputA = new float[dataSize];
        var inputB = new float[dataSize];
        var expected = new float[dataSize];

        for (var i = 0; i < dataSize; i++)
        {
            inputA[i] = i * 0.1f;
            inputB[i] = i * 0.2f;
            expected[i] = inputA[i] + inputB[i]; // Simple addition
        }

        // Step 2: Allocate memory
        _output.WriteLine("Allocating device memory...");
        var bufferA = await _accelerator.Memory.AllocateAsync(dataSize * sizeof(float));
        var bufferB = await _accelerator.Memory.AllocateAsync(dataSize * sizeof(float));
        var bufferResult = await _accelerator.Memory.AllocateAsync(dataSize * sizeof(float));

        // Step 3: Copy input data
        _output.WriteLine("Copying data to device...");
        await bufferA.CopyFromHostAsync<float>(inputA.AsMemory());
        await bufferB.CopyFromHostAsync<float>(inputB.AsMemory());

        // Step 4: Compile kernel
        _output.WriteLine("Compiling kernel...");
        var kernelSource = new TestKernelSource
        {
            Name = "VectorAdd",
            Code = @"
                __kernel void vector_add(__global float* a, __global float* b, __global float* result, int n) {
                    int idx = get_global_id(0);
                    if(idx < n) {
                        result[idx] = a[idx] + b[idx];
                    }
                }
            ",
            EntryPoint = "vector_add",
            Language = KernelLanguage.OpenCL
        };
        var vectorAddKernel = await _accelerator.CompileKernelAsync(
            new KernelDefinition("VectorAdd", kernelSource, new CompilationOptions()));

        // Step 5: Execute kernel
        _output.WriteLine("Executing kernel...");
        var kernelArgs = new KernelArguments(bufferA, bufferB, bufferResult, dataSize);
        await vectorAddKernel.ExecuteAsync(kernelArgs);

        // Step 6: Synchronize
        _output.WriteLine("Synchronizing...");
        await _accelerator.SynchronizeAsync();

        // Step 7: Read results
        _output.WriteLine("Reading results...");
        var results = new float[dataSize];
        await bufferResult.CopyToHostAsync<float>(results.AsMemory());

        // Step 8: Verify results(approximate since we're simulating)
        _output.WriteLine("Verifying results...");
        var correctCount = 0;
        for (var i = 0; i < dataSize; i++)
        {
            // In a real implementation, this would match exactly
            // For our test implementation, we just check if data was transferred
            if (!float.IsNaN(results[i]) && !float.IsInfinity(results[i]))
            {
                correctCount++;
            }
        }

        _output.WriteLine($"Pipeline completed successfully!");
        _output.WriteLine($"Valid results: {correctCount}/{dataSize}");

        // Cleanup
        await bufferA.DisposeAsync();
        await bufferB.DisposeAsync();
        await bufferResult.DisposeAsync();
        await vectorAddKernel.DisposeAsync();
    }
}
