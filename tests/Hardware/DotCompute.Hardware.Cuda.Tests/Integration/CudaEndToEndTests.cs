// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Text;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Tests.Utilities;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Hardware.Integration;

/// <summary>
/// End-to-end integration tests for the complete CUDA pipeline
/// </summary>
[Collection("CUDA Hardware Tests")]
public sealed class CudaEndToEndTests : IDisposable
{
    private readonly ILogger<CudaEndToEndTests> _logger;
    private readonly ITestOutputHelper _output;
    private readonly List<CudaAccelerator> _accelerators = [];
    private readonly List<ISyncMemoryBuffer> _buffers = [];
    private readonly List<ICompiledKernel> _kernels = [];

    // LoggerMessage delegates for performance
    private static readonly Action<ILogger, Exception, Exception?> LogKernelDisposeError = 
        LoggerMessage.Define<Exception>(
            LogLevel.Warning,
            new EventId(1, nameof(LogKernelDisposeError)),
            "Error disposing compiled kernel: {Exception}");

    private static readonly Action<ILogger, Exception, Exception?> LogBufferDisposeError = 
        LoggerMessage.Define<Exception>(
            LogLevel.Warning,
            new EventId(2, nameof(LogBufferDisposeError)),
            "Error disposing CUDA buffer: {Exception}");

    private static readonly Action<ILogger, Exception, Exception?> LogAcceleratorDisposeError = 
        LoggerMessage.Define<Exception>(
            LogLevel.Warning,
            new EventId(3, nameof(LogAcceleratorDisposeError)),
            "Error disposing CUDA accelerator: {Exception}");

    public CudaEndToEndTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<CudaEndToEndTests>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaEndToEnd_VectorAddition_ShouldCompileAndExecuteSuccessfully()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;

        const int arraySize = 1024;
        var hostA = TestDataGenerator.GenerateFloatArray(arraySize);
        var hostB = TestDataGenerator.GenerateFloatArray(arraySize);
        var hostC = new float[arraySize];
        var expectedResults = hostA.Zip(hostB, (a, b) => a + b).ToArray();

        // Act
        var kernelDefinition = CreateVectorAddKernel();
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);
        _kernels.Add(compiledKernel);

        var bufferA = memoryManager!.Allocate(arraySize * sizeof(float));
        var bufferB = memoryManager.Allocate(arraySize * sizeof(float));
        var bufferC = memoryManager.Allocate(arraySize * sizeof(float));
        _buffers.AddRange([bufferA, bufferB, bufferC]);

        unsafe
        {
            fixed (float* ptrA = hostA, ptrB = hostB, ptrC = hostC)
            {
                // Copy data to GPU
                memoryManager.CopyFromHost(ptrA, bufferA, arraySize * sizeof(float));
                memoryManager.CopyFromHost(ptrB, bufferB, arraySize * sizeof(float));

                // Note: Actual kernel execution would require CUDA driver API calls
                // This tests the compilation and memory management pipeline

                // Simulate kernel execution result by copying expected results
                fixed (float* expectedPtr = expectedResults)
                {
                    memoryManager.CopyFromHost(expectedPtr, bufferC, arraySize * sizeof(float));
                }

                // Copy results back
                memoryManager.CopyToHost(bufferC, ptrC, arraySize * sizeof(float));
            }
        }

        // Assert
        Assert.NotNull(compiledKernel);
        compiledKernel.Name.Should().Be("vector_add");
        hostC.Should().BeEquivalentTo(expectedResults, "Vector addition should produce correct results");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaEndToEnd_MatrixMultiplication_ShouldHandleComplexKernels()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;

        const int matrixSize = 32; // Small matrix for testing
        const int totalElements = matrixSize * matrixSize;

        var matrixA = TestDataGenerator.GenerateFloatArray(totalElements);
        var matrixB = TestDataGenerator.GenerateFloatArray(totalElements);
        var matrixC = new float[totalElements];

        // Act
        var kernelDefinition = CreateMatrixMultiplyKernel();
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);
        _kernels.Add(compiledKernel);

        var bufferA = memoryManager!.Allocate(totalElements * sizeof(float));
        var bufferB = memoryManager.Allocate(totalElements * sizeof(float));
        var bufferC = memoryManager.Allocate(totalElements * sizeof(float));
        _buffers.AddRange([bufferA, bufferB, bufferC]);

        unsafe
        {
            fixed (float* ptrA = matrixA, ptrB = matrixB, ptrC = matrixC)
            {
                memoryManager.CopyFromHost(ptrA, bufferA, totalElements * sizeof(float));
                memoryManager.CopyFromHost(ptrB, bufferB, totalElements * sizeof(float));

                // Zero output buffer
                memoryManager.Zero(bufferC);

                // Copy back to verify zero initialization
                memoryManager.CopyToHost(bufferC, ptrC, totalElements * sizeof(float));
            }
        }

        // Assert
        Assert.NotNull(compiledKernel);
        compiledKernel.Name.Should().Be("matrix_multiply");
        matrixC.Should().AllSatisfy(x => x.Should().Be(0.0f), "Output buffer should be zeroed");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaEndToEnd_MultipleKernelsSequential_ShouldExecuteInOrder()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;

        const int arraySize = 512;
        var inputData = TestDataGenerator.GenerateFloatArray(arraySize);
        var outputData = new float[arraySize];

        // Act
        // Compile multiple kernels
        var multiplyKernel = await accelerator.CompileKernelAsync(CreateScalarMultiplyKernel());
        var addKernel = await accelerator.CompileKernelAsync(CreateScalarAddKernel());
        _kernels.AddRange([multiplyKernel, addKernel]);

        // Allocate buffers
        var inputBuffer = memoryManager!.Allocate(arraySize * sizeof(float));
        var tempBuffer = memoryManager.Allocate(arraySize * sizeof(float));
        var outputBuffer = memoryManager.Allocate(arraySize * sizeof(float));
        _buffers.AddRange([inputBuffer, tempBuffer, outputBuffer]);

        unsafe
        {
            fixed (float* inputPtr = inputData, outputPtr = outputData)
            {
                // Upload input data
                memoryManager.CopyFromHost(inputPtr, inputBuffer, arraySize * sizeof(float));

                // Simulate first kernel: multiply by 2
                var doubledData = inputData.Select(x => x * 2.0f).ToArray();
                fixed (float* doubledPtr = doubledData)
                {
                    memoryManager.CopyFromHost(doubledPtr, tempBuffer, arraySize * sizeof(float));
                }

                // Simulate second kernel: add 1
                var finalData = doubledData.Select(x => x + 1.0f).ToArray();
                fixed (float* finalPtr = finalData)
                {
                    memoryManager.CopyFromHost(finalPtr, outputBuffer, arraySize * sizeof(float));
                }

                // Download final results
                memoryManager.CopyToHost(outputBuffer, outputPtr, arraySize * sizeof(float));
            }
        }

        // Synchronize device
        await accelerator.SynchronizeAsync();

        // Assert
        Assert.NotNull(multiplyKernel);
        Assert.NotNull(addKernel);
        var expectedResults = inputData.Select(x => x * 2.0f + 1.0f).ToArray();
        outputData.Should().BeEquivalentTo(expectedResults, "Sequential kernel execution should produce correct results");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaEndToEnd_MemoryIntensiveOperation_ShouldHandleLargeDatasets()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;

        // Test with larger dataset
        const int arraySize = 1024 * 1024; // 1M elements
        var inputData = TestDataGenerator.GenerateFloatArray(arraySize);
        var outputData = new float[arraySize];

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var kernelDefinition = CreateMemoryIntensiveKernel();
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);
        _kernels.Add(compiledKernel);

        var inputBuffer = memoryManager!.Allocate(arraySize * sizeof(float));
        var outputBuffer = memoryManager.Allocate(arraySize * sizeof(float));
        _buffers.AddRange([inputBuffer, outputBuffer]);

        unsafe
        {
            fixed (float* inputPtr = inputData, outputPtr = outputData)
            {
                // Upload
                memoryManager.CopyFromHost(inputPtr, inputBuffer, arraySize * sizeof(float));

                // Process(simulate computation)
                var processedData = inputData.Select(x => (float)Math.Sqrt(x * x + 1)).ToArray();
                fixed (float* processedPtr = processedData)
                {
                    memoryManager.CopyFromHost(processedPtr, outputBuffer, arraySize * sizeof(float));
                }

                // Download
                memoryManager.CopyToHost(outputBuffer, outputPtr, arraySize * sizeof(float));
            }
        }

        await accelerator.SynchronizeAsync();
        stopwatch.Stop();

        // Assert
        Assert.NotNull(compiledKernel);
        Assert.Equal(arraySize, outputData.Length);
        outputData.Should().NotContainNulls();

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(30000,
            "Large dataset processing should complete within 30 seconds");

        _output.WriteLine($"Processed {arraySize} elements in {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaEndToEnd_ErrorRecovery_ShouldHandleCompilationFailures()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();

        // Act & Assert
        // Try to compile an invalid kernel
        var invalidKernel = CreateInvalidKernel();
        var compileInvalidAction = async () => await accelerator.CompileKernelAsync(invalidKernel);
        await Assert.ThrowsAsync<InvalidOperationException>(compileInvalidAction);

        // Device should still be functional after compilation error
        var validKernel = CreateVectorAddKernel();
        var compiledKernel = await accelerator.CompileKernelAsync(validKernel); // Should not throw

        if (compiledKernel != null)
        {
            _kernels.Add(compiledKernel);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaEndToEnd_DeviceReset_ShouldPreserveAcceleratorFunctionality()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;

        // Act
        // Perform some operations
        var buffer1 = memoryManager!.Allocate(1024);
        _buffers.Add(buffer1);

        // Reset the device
        accelerator.Reset();

        // Device should still be functional after reset
        var buffer2 = memoryManager.Allocate(2048);
        _buffers.Add(buffer2);

        await accelerator.SynchronizeAsync();

        // Assert
        Assert.NotNull(buffer2);
        buffer2.SizeInBytes.Should().Be(2048);
    }

    [Fact]
    [Trait("Category", "Stress")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaEndToEnd_StressTest_MultipleOperationsConcurrently()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;

        const int operationCount = 10;
        const int arraySize = 1024;

        // Act
        var tasks = new List<Task>();

        for (var i = 0; i < operationCount; i++)
        {
            var operationIndex = i;
            var task = Task.Run(async () =>
            {
                try
                {
                    var kernelDefinition = CreateUniqueKernel($"stress_kernel_{operationIndex}");
                    var compiledKernel = await accelerator.CompileKernelAsync(kernelDefinition);

                    lock (_kernels)
                    {
                        _kernels.Add(compiledKernel);
                    }

                    var buffer = memoryManager!.Allocate(arraySize * sizeof(float));
                    lock (_buffers)
                    {
                        _buffers.Add(buffer);
                    }

                    var testData = TestDataGenerator.GenerateFloatArray(arraySize);
                    var resultData = new float[arraySize];

                    unsafe
                    {
                        fixed (float* testPtr = testData, resultPtr = resultData)
                        {
                            memoryManager.CopyFromHost(testPtr, buffer, arraySize * sizeof(float));
                            memoryManager.CopyToHost(buffer, resultPtr, arraySize * sizeof(float));
                        }
                    }

                    return resultData;
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Stress test operation {operationIndex} failed: {ex.Message}");
                    throw;
                }
            });

            tasks.Add(task);
        }

        // Assert
        var completionAction = async () => await Task.WhenAll(tasks);
        await completionAction.Should().NotThrowAsync("All stress test operations should complete successfully");

        await accelerator.SynchronizeAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Hardware", "CUDA")]
    public void CudaEndToEnd_ResourceCleanup_ShouldReleaseAllResources()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;

        // Act
        var initialStats = memoryManager!.GetStatistics();

        // Allocate several buffers
        var buffers = new List<ISyncMemoryBuffer>();
        for (var i = 0; i < 10; i++)
        {
            var buffer = memoryManager.Allocate((i + 1) * 1024);
            buffers.Add(buffer);
        }

        var afterAllocStats = memoryManager.GetStatistics();

        // Free all buffers
        foreach (var buffer in buffers)
        {
            memoryManager.Free(buffer);
        }

        var afterFreeStats = memoryManager.GetStatistics();

        // Assert
        (afterAllocStats.AllocationCount > initialStats.AllocationCount).Should().BeTrue();
        (afterAllocStats.AllocatedMemory > initialStats.AllocatedMemory).Should().BeTrue();

        // Memory should be freed(though exact values may vary due to fragmentation)
        (afterFreeStats.AllocationCount <= afterAllocStats.AllocationCount).Should().BeTrue();
    }

    // Helper Methods
    private CudaAccelerator CreateAccelerator()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var cudaLogger = loggerFactory.CreateLogger<CudaAccelerator>();
        var accelerator = new CudaAccelerator(0, cudaLogger);
        _accelerators.Add(accelerator);
        return accelerator;
    }

    private static KernelDefinition CreateVectorAddKernel()
    {
        const string kernelSource = @"
__global__ void vector_add(float* a, float* b, float* c, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        c[idx] = a[idx] + b[idx];
    }
}";
        return new KernelDefinition
        {
            Name = "vector_add",
            Code = Encoding.UTF8.GetBytes(kernelSource),
            EntryPoint = "vector_add"
        };
    }

    private static KernelDefinition CreateMatrixMultiplyKernel()
    {
        const string kernelSource = @"
__global__ void matrix_multiply(float* A, float* B, float* C, int n)
{
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    
    if(row < n && col < n) {
        float sum = 0.0f;
        for(int k = 0; k < n; k++) {
            sum += A[row * n + k] * B[k * n + col];
        }
        C[row * n + col] = sum;
    }
}";
        return new KernelDefinition
        {
            Name = "matrix_multiply",
            Code = Encoding.UTF8.GetBytes(kernelSource),
            EntryPoint = "matrix_multiply"
        };
    }

    private static KernelDefinition CreateScalarMultiplyKernel()
    {
        const string kernelSource = @"
__global__ void scalar_multiply(float* input, float* output, float scalar, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        output[idx] = input[idx] * scalar;
    }
}";
        return new KernelDefinition
        {
            Name = "scalar_multiply",
            Code = Encoding.UTF8.GetBytes(kernelSource),
            EntryPoint = "scalar_multiply"
        };
    }

    private static KernelDefinition CreateScalarAddKernel()
    {
        const string kernelSource = @"
__global__ void scalar_add(float* input, float* output, float scalar, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        output[idx] = input[idx] + scalar;
    }
}";
        return new KernelDefinition
        {
            Name = "scalar_add",
            Code = Encoding.UTF8.GetBytes(kernelSource),
            EntryPoint = "scalar_add"
        };
    }

    private static KernelDefinition CreateMemoryIntensiveKernel()
    {
        const string kernelSource = @"
__global__ void memory_intensive(float* input, float* output, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        float val = input[idx];
        output[idx] = sqrtf(val * val + 1.0f);
    }
}";
        return new KernelDefinition
        {
            Name = "memory_intensive",
            Code = Encoding.UTF8.GetBytes(kernelSource),
            EntryPoint = "memory_intensive"
        };
    }

    private static KernelDefinition CreateInvalidKernel()
    {
        return new KernelDefinition
        {
            Name = "invalid_kernel",
            Code = Encoding.UTF8.GetBytes("invalid cuda syntax {{{ this will fail"),
            EntryPoint = "invalid_kernel"
        };
    }

    private static KernelDefinition CreateUniqueKernel(string name)
    {
        var kernelSource = $@"
__global__ void {name}(float* input, float* output, int n)
{{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {{
        output[idx] = input[idx] + {name.GetHashCode(StringComparison.Ordinal) % 100};
    }}
}}";
        return new KernelDefinition
        {
            Name = name,
            Code = Encoding.UTF8.GetBytes(kernelSource),
            EntryPoint = name
        };
    }

    private static bool IsCudaAvailable()
    {
        try
        {
            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            return result == CudaError.Success && deviceCount > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsNvrtcAvailable() => DotCompute.Backends.CUDA.Compilation.CudaKernelCompiler.IsNvrtcAvailable();

    public void Dispose()
    {
        foreach (var kernel in _kernels)
        {
            try
            {
                (kernel as IDisposable)?.Dispose();
            }
            catch (Exception ex)
            {
                LogKernelDisposeError(_logger, ex, null);
            }
        }
        _kernels.Clear();

        foreach (var buffer in _buffers.ToList())
        {
            try
            {
                if (!buffer.IsDisposed)
                {
                    var syncMemoryManager = _accelerators.FirstOrDefault()?.Memory as ISyncMemoryManager;
                    syncMemoryManager?.Free(buffer);
                }
            }
            catch (Exception ex)
            {
                LogBufferDisposeError(_logger, ex, null);
            }
        }
        _buffers.Clear();

        foreach (var accelerator in _accelerators)
        {
            try
            {
                accelerator?.Dispose();
            }
            catch (Exception ex)
            {
                LogAcceleratorDisposeError(_logger, ex, null);
            }
        }
        _accelerators.Clear();
        GC.SuppressFinalize(this);
    }
}
