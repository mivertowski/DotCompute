// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Compilation;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests;

/// <summary>
/// Basic validation tests for CUDA backend functionality
/// </summary>
public class CudaBasicTests : IDisposable
{
    private readonly ILogger<CudaBasicTests> _logger;
    private readonly CudaBackend? _backend;
    private readonly CudaAccelerator? _accelerator;
    private bool _disposed;

    public CudaBasicTests(ITestOutputHelper output)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddXUnit(output).SetMinimumLevel(LogLevel.Information));
        
        _logger = loggerFactory.CreateLogger<CudaBasicTests>();

        if (CudaBackend.IsAvailable())
        {
            _backend = new CudaBackend(loggerFactory.CreateLogger<CudaBackend>());
            _accelerator = _backend.GetDefaultAccelerator();
        }
        else
        {
            _logger.LogInformation("CUDA is not available - tests will be skipped");
        }
    }

    [SkippableFact]
    public void CudaRuntime_ShouldBeAvailable()
    {
        Assert.True(CudaBackend.IsAvailable(), "CUDA runtime should be available");
        
        Assert.NotNull(_backend);
        Assert.NotNull(_accelerator);
        
        _logger.LogInformation("CUDA backend and accelerator initialized successfully");
    }

    [SkippableFact]
    public void CudaDevice_ShouldHaveValidProperties()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        Assert.NotNull(_accelerator);
        var info = _accelerator.Info;

        Assert.Equal(AcceleratorType.CUDA, info.Type);
        Assert.False(string.IsNullOrEmpty(info.Name));
        Assert.True(info.TotalMemory > 0);
        Assert.True(info.ComputeUnits > 0);
        Assert.NotNull(info.ComputeCapability);

        _logger.LogInformation("Device: {Name}", info.Name);
        _logger.LogInformation("Compute Capability: {Capability}", info.ComputeCapability);
        _logger.LogInformation("Memory: {Memory:F2} GB", info.TotalMemory / (1024.0 * 1024 * 1024));
        _logger.LogInformation("Compute Units: {Units}", info.ComputeUnits);
    }

    [SkippableFact]
    public async Task MemoryOperations_ShouldWork()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        Assert.NotNull(_accelerator);

        const int SIZE = 1000;
        var testData = new float[SIZE];
        for (int i = 0; i < SIZE; i++)
        {
            testData[i] = i * 0.5f;
        }

        var buffer = _accelerator.Memory.Allocate(SIZE * sizeof(float));
        
        try
        {
            // Copy to GPU
            await buffer.CopyFromHostAsync<float>(testData);
            
            // Copy back from GPU
            var result = new float[SIZE];
            await buffer.CopyToHostAsync<float>(result);

            // Verify data
            for (int i = 0; i < SIZE; i++)
            {
                Assert.Equal(testData[i], result[i]);
            }

            _logger.LogInformation("Memory operations test passed for {Size} elements", SIZE);
        }
        finally
        {
            await buffer.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task KernelCompilation_ShouldWork()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        Assert.NotNull(_accelerator);

        var kernelSource = @"
extern ""C"" __global__ void addOne(float* data, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < n) {
        data[idx] += 1.0f;
    }
}";

        var definition = new KernelDefinition("addOne", "addOne", Encoding.UTF8.GetBytes(kernelSource));
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Default };

        var compiledKernel = await _accelerator.CompileKernelAsync(definition, options);
        Assert.NotNull(compiledKernel);

        _logger.LogInformation("Kernel compilation successful");
        
        await compiledKernel.DisposeAsync();
    }

    [SkippableFact]
    public async Task SimpleKernel_ShouldExecute()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        Assert.NotNull(_accelerator);

        const int N = 1000;
        var data = new float[N];
        for (int i = 0; i < N; i++)
        {
            data[i] = i;
        }

        var buffer = _accelerator.Memory.Allocate(N * sizeof(float));

        try
        {
            await buffer.CopyFromHostAsync<float>(data);

            var kernelSource = @"
extern ""C"" __global__ void multiply(float* data, float factor, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < n) {
        data[idx] *= factor;
    }
}";

            var definition = new KernelDefinition("multiply", "multiply", Encoding.UTF8.GetBytes(kernelSource));
            var compiledKernel = await _accelerator.CompileKernelAsync(definition);

            const float FACTOR = 2.5f;
            var arguments = new KernelArguments(buffer, FACTOR, N);
            await compiledKernel.ExecuteAsync(arguments);

            var result = new float[N];
            await buffer.CopyToHostAsync<float>(result);

            // Verify results
            for (int i = 0; i < N; i++)
            {
                var expected = data[i] * FACTOR;
                Assert.True(Math.Abs(result[i] - expected) < 0.001f,
                    $"Incorrect result at {i}: expected {expected}, got {result[i]}");
            }

            _logger.LogInformation("Simple kernel execution test passed");

            await compiledKernel.DisposeAsync();
        }
        finally
        {
            await buffer.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task LaunchConfiguration_ShouldOptimizeCorrectly()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        Assert.NotNull(_accelerator);

        var kernelSource = @"
extern ""C"" __global__ void testConfig(int* data, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < n) {
        data[idx] = blockIdx.x;
    }
}";

        var definition = new KernelDefinition("testConfig", "testConfig", Encoding.UTF8.GetBytes(kernelSource));
        var compiledKernel = await _accelerator.CompileKernelAsync(definition) as CudaCompiledKernel;
        Assert.NotNull(compiledKernel);

        try
        {
            const int N = 10000;
            var config = compiledKernel.GetOptimalLaunchConfig(N);

            // Verify configuration makes sense
            var totalThreads = config.GridX * config.BlockX;
            Assert.True(totalThreads >= N, "Launch config should cover all elements");
            Assert.True(config.BlockX <= 1024, "Block size should be reasonable"); // Max for most GPUs
            Assert.True(config.BlockX >= 32, "Block size should be at least one warp");

            _logger.LogInformation("Optimal config for {Elements} elements: Grid={Grid}, Block={Block}, Total Threads={Total}",
                N, config.GridX, config.BlockX, totalThreads);

            // Test execution with custom config
            var data = new int[N];
            var buffer = _accelerator.Memory.Allocate(N * sizeof(int));

            try
            {
                var arguments = new KernelArguments(buffer, N);
                await compiledKernel.ExecuteWithConfigAsync(arguments, config);

                await buffer.CopyToHostAsync<int>(data);

                // Verify some results (block IDs should be reasonable)
                Assert.True(data[0] >= 0, "Block ID should be non-negative");
                if (N > 100)
                {
                    Assert.True(data[N - 1] >= 0, "Last element should have valid block ID");
                }

                _logger.LogInformation("Launch configuration test passed");
            }
            finally
            {
                await buffer.DisposeAsync();
            }
        }
        finally
        {
            await compiledKernel.DisposeAsync();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _accelerator?.Dispose();
        _backend?.Dispose();
        _disposed = true;
    }
}