// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Compilation;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Hardware.Cuda.Tests;


/// <summary>
/// Comprehensive real hardware tests for CUDA backend on RTX 2000 Ada Gen
/// </summary>
[Collection("Hardware")]
public sealed class CudaRealHardwareFullSystemTests : IDisposable
{
    private readonly ILogger<CudaRealHardwareFullSystemTests> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly CudaBackend? _backend;
    private readonly CudaAccelerator? _accelerator;
    private bool _disposed;

    // LoggerMessage delegates for performance
    private static readonly Action<ILogger, string, Exception?> LogDetectedDevice =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, nameof(LogDetectedDevice)),
            "Detected CUDA device: {DeviceName}");

    private static readonly Action<ILogger, string, Exception?> LogComputeCapability =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, nameof(LogComputeCapability)),
            "Compute capability: {ComputeCapability}");

    private static readonly Action<ILogger, long, double, Exception?> LogTotalMemory =
        LoggerMessage.Define<long, double>(
            LogLevel.Information,
            new EventId(3, nameof(LogTotalMemory)),
            "Total memory: {TotalMemory:N0} bytes ({MemoryGB:F2} GB)");

    private static readonly Action<ILogger, int, Exception?> LogVectorAdditionTest =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(4, nameof(LogVectorAdditionTest)),
            "Vector addition test passed for {ElementCount} elements");

    private static readonly Action<ILogger, int, int, Exception?> LogMatrixMultiplicationTest =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(5, nameof(LogMatrixMultiplicationTest)),
            "Matrix multiplication test passed for {Size}x{Size} matrices");

    private static readonly Action<ILogger, Exception?> LogTestingLargeMemory =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(6, nameof(LogTestingLargeMemory)),
            "Testing large memory allocations");

    private static readonly Action<ILogger, double, Exception?> LogLargeMemoryTest =
        LoggerMessage.Define<double>(
            LogLevel.Information,
            new EventId(7, nameof(LogLargeMemoryTest)),
            "Large memory allocation test passed: {AllocationMB:F1} MB");

    private static readonly Action<ILogger, int, Exception?> LogConcurrentKernelCompleted =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(8, nameof(LogConcurrentKernelCompleted)),
            "Concurrent kernel {Index} completed successfully");

    private static readonly Action<ILogger, int, Exception?> LogAllConcurrentKernelsCompleted =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(9, nameof(LogAllConcurrentKernelsCompleted)),
            "All {Count} concurrent kernels completed successfully");

    private static readonly Action<ILogger, string, long, long, Exception?> LogOptimizationLevel =
        LoggerMessage.Define<string, long, long>(
            LogLevel.Information,
            new EventId(10, nameof(LogOptimizationLevel)),
            "Optimization {Level}: Compile={CompileMs}ms, Execute={ExecuteMs}ms");

    private static readonly Action<ILogger, Exception?> LogDeviceValidationPassed =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(11, nameof(LogDeviceValidationPassed)),
            "Device validation passed for RTX 2000 Ada Gen specs");

    public CudaRealHardwareFullSystemTests(ITestOutputHelper output)
    {
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Debug));

        _logger = _loggerFactory.CreateLogger<CudaRealHardwareFullSystemTests>();

        // Only run tests if CUDA is available
        if (CudaBackend.IsAvailable())
        {
            _backend = new CudaBackend(_loggerFactory.CreateLogger<CudaBackend>());
            _accelerator = _backend.GetDefaultAccelerator();
        }
        // CUDA not available - tests will be skipped
    }

    [SkippableFact]
    public void CudaRuntime_ShouldDetectRTX2000AdaGeneration()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        Assert.NotNull(_backend);
        Assert.NotNull(_accelerator);

        var info = _accelerator.Info;

        LogDetectedDevice(_logger, info.Name, null);
        LogComputeCapability(_logger, info.ComputeCapability?.ToString() ?? "Unknown", null);
        LogTotalMemory(_logger, info.TotalMemory, info.TotalMemory / (1024.0 * 1024 * 1024), null);

        // Verify compute capability is 8.9 for RTX 2000 Ada Gen
        Assert.NotNull(info.ComputeCapability);
        _ = info.ComputeCapability.Major.Should().BeGreaterThanOrEqualTo(8,
            $"Expected compute capability 8.x or higher, got {info.ComputeCapability}");

        // RTX 2000 Ada Gen should have compute capability 8.9
        if (info.Name.Contains("RTX 2000 Ada", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Equal(8, info.ComputeCapability.Major);
            Assert.Equal(9, info.ComputeCapability.Minor);
        }

        _ = info.TotalMemory.Should().BeGreaterThan(0, "Device should have memory");
        _ = info.ComputeUnits.Should().BeGreaterThan(0, "Device should have compute units");
    }

    [SkippableFact]
    public async Task VectorAddition_ShouldExecuteOnRealHardware()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        Assert.NotNull(_accelerator);

        const int N = 1024 * 1024; // 1M elements
        var a = CreateSequentialArray(N, 1.0f);
        var b = CreateSequentialArray(N, 2.0f);
        var c = new float[N];

        // Allocate GPU memory
        var bufferA = await _accelerator.Memory.AllocateAsync(N * sizeof(float));
        var bufferB = await _accelerator.Memory.AllocateAsync(N * sizeof(float));
        var bufferC = await _accelerator.Memory.AllocateAsync(N * sizeof(float));

        try
        {
            // Copy data to GPU
            await bufferA.CopyFromHostAsync<float>(a);
            await bufferB.CopyFromHostAsync<float>(b);

            // Create CUDA kernel source
            var kernelSource = @"
extern ""C"" __global__ void vectorAdd(float* a, float* b, float* c, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        c[idx] = a[idx] + b[idx];
    }
}";

            var options = new CompilationOptions
            {
                OptimizationLevel = OptimizationLevel.Maximum,
                EnableDebugInfo = false
            };

            var kernelSourceObj = new TextKernelSource(kernelSource, "vectorAdd", Abstractions.KernelLanguage.Cuda, "vectorAdd");
            var kernelDefinition = new KernelDefinition("vectorAdd", kernelSourceObj, options);

            // Compile kernel
            var compiledKernel = await _accelerator.CompileKernelAsync(kernelDefinition, options);

            // Execute kernel
            var arguments = new KernelArguments(bufferA, bufferB, bufferC, N);
            await compiledKernel.ExecuteAsync(arguments);

            // Copy result back to host
            await bufferC.CopyToHostAsync<float>(c);

            // Verify results
            for (var i = 0; i < N; i++)
            {
                var expected = a[i] + b[i];
                Assert.True(Math.Abs(c[i] - expected) < 0.001f,
                    $"Mismatch at index {i}: expected {expected}, got {c[i]}");
            }

            LogVectorAdditionTest(_logger, N, null);
        }
        finally
        {
            await bufferA.DisposeAsync();
            await bufferB.DisposeAsync();
            await bufferC.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task MatrixMultiplication_ShouldExecuteOnRealHardware()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        Assert.NotNull(_accelerator);

        const int SIZE = 512;
        var matrixA = CreateMatrix(SIZE, SIZE, 1.0f);
        var matrixB = CreateMatrix(SIZE, SIZE, 2.0f);
        var matrixC = new float[SIZE * SIZE];

        // Allocate GPU memory
        var bufferA = await _accelerator.Memory.AllocateAsync(SIZE * SIZE * sizeof(float));
        var bufferB = await _accelerator.Memory.AllocateAsync(SIZE * SIZE * sizeof(float));
        var bufferC = await _accelerator.Memory.AllocateAsync(SIZE * SIZE * sizeof(float));

        try
        {
            // Copy matrices to GPU
            await bufferA.CopyFromHostAsync<float>(matrixA);
            await bufferB.CopyFromHostAsync<float>(matrixB);

            // Matrix multiplication kernel optimized for RTX GPUs
            var kernelSource = @"
extern ""C"" __global__ void matrixMul(float* A, float* B, float* C, int size)
{
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    
    if(row < size && col < size) {
        float sum = 0.0f;
        for(int k = 0; k < size; k++) {
            sum += A[row * size + k] * B[k * size + col];
        }
        C[row * size + col] = sum;
    }
}";

            var options = new CompilationOptions
            {
                OptimizationLevel = OptimizationLevel.Maximum,
                EnableDebugInfo = false
            };

            var kernelSourceObj = new TextKernelSource(kernelSource, "matrixMul", Abstractions.KernelLanguage.Cuda, "matrixMul");
            var kernelDefinition = new KernelDefinition("matrixMul", kernelSourceObj, options);

            var compiledKernel = await _accelerator.CompileKernelAsync(kernelDefinition, options);

            // Get optimal launch configuration for 2D workload
            var cudaKernel = compiledKernel as CudaCompiledKernel;
            Assert.NotNull(cudaKernel);

            var config = CudaLaunchConfig.Create2D(SIZE, SIZE, 16, 16);

            // Execute with custom configuration
            var arguments = new KernelArguments(bufferA, bufferB, bufferC, SIZE);
            await cudaKernel.ExecuteWithConfigAsync(arguments, config);

            // Copy result back
            await bufferC.CopyToHostAsync<float>(matrixC);

            // Verify a few key results(full verification would be too slow)
            for (var i = 0; i < Math.Min(10, SIZE); i++)
            {
                for (var j = 0; j < Math.Min(10, SIZE); j++)
                {
                    var expected = SIZE * 1.0f * 2.0f; // Each cell should be sum of 1*2 for SIZE terms
                    var actual = matrixC[i * SIZE + j];
                    Assert.True(Math.Abs(actual - expected) < 0.001f,
                        $"Matrix result mismatch at [{i},{j}]: expected {expected}, got {actual}");
                }
            }

            LogMatrixMultiplicationTest(_logger, SIZE, SIZE, null);
        }
        finally
        {
            await bufferA.DisposeAsync();
            await bufferB.DisposeAsync();
            await bufferC.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task MemoryIntensive_ShouldHandleLargeAllocations()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        Assert.NotNull(_accelerator);

        // Note: GetStatistics() method doesn't exist in new IMemoryManager interface
        LogTestingLargeMemory(_logger, null);

        // Try to allocate a significant amount of memory
        var allocationSize = 1024L * 1024 * 1024; // 1GB

        var buffer = await _accelerator.Memory.AllocateAsync(allocationSize);

        try
        {
            // Fill with pattern - Fill method not available in new API, using copy instead
            var fillData = new byte[Math.Min(allocationSize, 1024)];
            Array.Fill(fillData, (byte)0xAB);
            await buffer.CopyFromHostAsync<byte>(fillData, 0);

            // Test partial copy operations
            var testData = new byte[1024];
            for (var i = 0; i < testData.Length; i++)
            {
                testData[i] = (byte)(i % 256);
            }

            // Copy test pattern to different offsets
            await buffer.CopyFromHostAsync<byte>(testData, 0);
            await buffer.CopyFromHostAsync<byte>(testData, allocationSize - 1024);

            // Read back and verify
            var readBack1 = new byte[1024];
            var readBack2 = new byte[1024];

            await buffer.CopyToHostAsync<byte>(readBack1, 0);
            await buffer.CopyToHostAsync<byte>(readBack2, allocationSize - 1024);

            Assert.Equal(testData, readBack1);
            Assert.Equal(testData, readBack2);

            LogLargeMemoryTest(_logger, allocationSize / (1024.0 * 1024), null);
        }
        finally
        {
            await buffer.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task ConcurrentKernels_ShouldExecuteInParallel()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        Assert.NotNull(_accelerator);

        const int KERNEL_COUNT = 4;
        const int ELEMENTS_PER_KERNEL = 256 * 1024;

        var tasks = new List<Task>();
        var buffers = new List<IMemoryBuffer>();

        try
        {
            // Create simple addition kernel
            var kernelSource = @"
extern ""C"" __global__ void simpleAdd(float* data, float value, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        data[idx] += value;
    }
}";

            var kernelSourceObj = new TextKernelSource(kernelSource, "simpleAdd", Abstractions.KernelLanguage.Cuda, "simpleAdd");
            var kernelDefinition = new KernelDefinition("simpleAdd", kernelSourceObj, new CompilationOptions());

            var compiledKernel = await _accelerator.CompileKernelAsync(kernelDefinition);

            // Launch multiple concurrent kernels
            for (var i = 0; i < KERNEL_COUNT; i++)
            {
                var kernelIndex = i;
                var task = Task.Run(async () =>
                {
                    var data = CreateSequentialArray(ELEMENTS_PER_KERNEL, kernelIndex * 10.0f);
                    var buffer = await _accelerator.Memory.AllocateAsync(ELEMENTS_PER_KERNEL * sizeof(float));
                    buffers.Add(buffer);

                    await buffer.CopyFromHostAsync<float>(data);

                    var arguments = new KernelArguments(buffer, (float)(kernelIndex + 1), ELEMENTS_PER_KERNEL);
                    await compiledKernel.ExecuteAsync(arguments);

                    var result = new float[ELEMENTS_PER_KERNEL];
                    await buffer.CopyToHostAsync<float>(result);

                    // Verify results
                    for (var j = 0; j < ELEMENTS_PER_KERNEL; j++)
                    {
                        var expected = data[j] + (kernelIndex + 1);
                        Assert.True(Math.Abs(result[j] - expected) < 0.001f,
                            $"Kernel {kernelIndex}, element {j}: expected {expected}, got {result[j]}");
                    }

                    LogConcurrentKernelCompleted(_logger, kernelIndex, null);
                });

                tasks.Add(task);
            }

            // Wait for all kernels to complete
            await Task.WhenAll(tasks);

            LogAllConcurrentKernelsCompleted(_logger, KERNEL_COUNT, null);
        }
        finally
        {
            // Clean up buffers
            foreach (var buffer in buffers)
            {
                await buffer.DisposeAsync();
            }
        }
    }

    [SkippableFact]
    public async Task CompilerOptimizations_ShouldProduceFastCode()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        Assert.NotNull(_accelerator);

        const int N = 1024 * 1024;

        // Test different optimization levels
        var optimizationLevels = new[]
        {
        OptimizationLevel.None,
        OptimizationLevel.Default,
        OptimizationLevel.Maximum
    };

        var kernelSource = @"
extern ""C"" __global__ void computeIntensive(float* input, float* output, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        float val = input[idx];
        // Compute-intensive operations
        for(int i = 0; i < 100; i++) {
            val = sinf(val) + cosf(val * 2.0f) + expf(val * 0.1f);
        }
        output[idx] = val;
    }
}";

        var inputData = CreateSequentialArray(N, 0.1f);
        var bufferInput = await _accelerator.Memory.AllocateAsync(N * sizeof(float));
        var bufferOutput = await _accelerator.Memory.AllocateAsync(N * sizeof(float));

        try
        {
            await bufferInput.CopyFromHostAsync<float>(inputData);

            foreach (var optLevel in optimizationLevels)
            {
                var kernelSourceObj = new TextKernelSource(kernelSource, "computeIntensive", Abstractions.KernelLanguage.Cuda, "computeIntensive");
                var options = new CompilationOptions
                {
                    OptimizationLevel = optLevel,
                    EnableDebugInfo = false
                };

                var kernelDefinition = new KernelDefinition($"computeIntensive_{optLevel}", kernelSourceObj, options);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var compiledKernel = await _accelerator.CompileKernelAsync(kernelDefinition, options);
                var compileTime = stopwatch.ElapsedMilliseconds;

                stopwatch.Restart();
                var arguments = new KernelArguments(bufferInput, bufferOutput, N);
                await compiledKernel.ExecuteAsync(arguments);
                var executeTime = stopwatch.ElapsedMilliseconds;

                LogOptimizationLevel(_logger, optLevel.ToString(), compileTime, executeTime, null);

                // Verify kernel actually executed(results should be different from input)
                var outputData = new float[Math.Min(100, N)];
                await bufferOutput.CopyToHostAsync<float>(outputData, 0);

                Assert.True(outputData.Any(f => Math.Abs(f - 0.1f) > 0.001f),
                    "Kernel should have modified the data");
            }
        }
        finally
        {
            await bufferInput.DisposeAsync();
            await bufferOutput.DisposeAsync();
        }
    }

    [SkippableFact]
    public void DeviceProperties_ShouldReflectRTX2000Specs()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        Assert.NotNull(_accelerator);
        var info = _accelerator.Info;

        // RTX 2000 Ada Gen expected specifications
        _ = (info.TotalMemory >= 6L * 1024 * 1024 * 1024).Should().BeTrue( // At least 6GB
            $"Expected at least 6GB memory, got {info.TotalMemory / (1024.0 * 1024 * 1024):F1}GB");

        _ = info.ComputeUnits.Should().BeGreaterThanOrEqualTo(20, // At least 20 SMs
            $"Expected at least 20 compute units, got {info.ComputeUnits}");

        _ = info.MaxClockFrequency.Should().BeGreaterThan(1000, // > 1GHz
            $"Expected clock > 1GHz, got {info.MaxClockFrequency}MHz");

        // Verify CUDA capabilities are available
        var capabilities = info.Capabilities;
        Assert.NotNull(capabilities);

        if (capabilities.TryGetValue("WarpSize", out var warpSize))
        {
            Assert.Equal(32, warpSize); // CUDA warp size is always 32
        }

        if (capabilities.TryGetValue("MaxThreadsPerBlock", out var maxThreads))
        {
            Assert.True((int)maxThreads >= 1024, "Should support at least 1024 threads per block");
        }

        LogDeviceValidationPassed(_logger, null);
    }

    private static float[] CreateSequentialArray(int count, float start = 0.0f)
    {
        var array = new float[count];
        for (var i = 0; i < count; i++)
        {
            array[i] = start + i;
        }
        return array;
    }

    private static float[] CreateMatrix(int rows, int cols, float value)
    {
        var matrix = new float[rows * cols];
        Array.Fill(matrix, value);
        return matrix;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _accelerator?.Dispose();
        _backend?.Dispose();
        _loggerFactory?.Dispose();
        _disposed = true;
    }
}
