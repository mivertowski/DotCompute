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

namespace DotCompute.Tests.Hardware;


/// <summary>
/// Basic validation tests for CUDA backend functionality
/// </summary>
[Trait("Category", "HardwareRequired")]
[Trait("Category", "CudaRequired")]
[Trait("Category", "Hardware")]
[Collection("Hardware")]
public sealed class CudaBasicTests : IDisposable
{
    private readonly ILogger<CudaBasicTests> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly CudaBackend? _backend;
    private readonly CudaAccelerator? _accelerator;
    private bool _disposed;

    // LoggerMessage delegates for performance
    private static readonly Action<ILogger, string, Exception?> LogDeviceInfo =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, nameof(LogDeviceInfo)),
            "Device: {Name}");

    private static readonly Action<ILogger, string, Exception?> LogComputeCapability =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, nameof(LogComputeCapability)),
            "Compute Capability: {Capability}");

    private static readonly Action<ILogger, double, Exception?> LogMemory =
        LoggerMessage.Define<double>(
            LogLevel.Information,
            new EventId(3, nameof(LogMemory)),
            "Memory: {Memory:F2} GB");

    private static readonly Action<ILogger, int, Exception?> LogComputeUnits =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(4, nameof(LogComputeUnits)),
            "Compute Units: {Units}");

    private static readonly Action<ILogger, int, Exception?> LogMemoryOperationsTest =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(5, nameof(LogMemoryOperationsTest)),
            "Memory operations test passed for {Size} elements");

    private static readonly Action<ILogger, Exception?> LogKernelCompilationSuccess =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(6, nameof(LogKernelCompilationSuccess)),
            "Kernel compilation successful");

    private static readonly Action<ILogger, Exception?> LogSimpleKernelExecutionTest =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(7, nameof(LogSimpleKernelExecutionTest)),
            "Simple kernel execution test passed");

    private static readonly Action<ILogger, int, uint, uint, uint, Exception?> LogOptimalConfig =
        LoggerMessage.Define<int, uint, uint, uint>(
            LogLevel.Information,
            new EventId(8, nameof(LogOptimalConfig)),
            "Optimal config for {Elements} elements: Grid={Grid}, Block={Block}, Total Threads={Total}");

    private static readonly Action<ILogger, Exception?> LogLaunchConfigurationTest =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(9, nameof(LogLaunchConfigurationTest)),
            "Launch configuration test passed");

    public CudaBasicTests(ITestOutputHelper output)
    {
        _loggerFactory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Information));

        _logger = _loggerFactory.CreateLogger<CudaBasicTests>();

        if (CudaBackend.IsAvailable())
        {
            _backend = new CudaBackend(_loggerFactory.CreateLogger<CudaBackend>());
            _accelerator = _backend.GetDefaultAccelerator();
        }
        else
        {
            // CUDA not available - tests will be skipped  
        }
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    public void CudaRuntime_ShouldBeAvailable()
    {
        Assert.True(CudaBackend.IsAvailable(), "CUDA runtime should be available");

        Assert.NotNull(_backend);
        Assert.NotNull(_accelerator);

        // CUDA backend and accelerator initialized successfully
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    public void CudaDevice_ShouldHaveValidProperties()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        Assert.NotNull(_accelerator);
        var info = _accelerator.Info;

        Assert.Equal("CUDA", info.Type);
        Assert.False(string.IsNullOrEmpty(info.Name));
        Assert.True(info.TotalMemory > 0);
        Assert.True(info.ComputeUnits > 0);
        Assert.NotNull(info.ComputeCapability);

        LogDeviceInfo(_logger, info.Name, null);
        LogComputeCapability(_logger, info.ComputeCapability?.ToString() ?? "Unknown", null);
        LogMemory(_logger, info.TotalMemory / (1024.0 * 1024 * 1024), null);
        LogComputeUnits(_logger, info.ComputeUnits, null);
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    public async Task MemoryOperations_ShouldWork()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        Assert.NotNull(_accelerator);

        const int SIZE = 1000;
        var testData = new float[SIZE];
        for (var i = 0; i < SIZE; i++)
        {
            testData[i] = i * 0.5f;
        }

        var buffer = await _accelerator.Memory.AllocateAsync(SIZE * sizeof(float));

        try
        {
            // Copy to GPU
            await buffer.CopyFromHostAsync<float>(testData);

            // Copy back from GPU
            var result = new float[SIZE];
            await buffer.CopyToHostAsync<float>(result);

            // Verify data
            for (var i = 0; i < SIZE; i++)
            {
                Assert.Equal(testData[i], result[i]);
            }

            LogMemoryOperationsTest(_logger, SIZE, null);
        }
        finally
        {
            await buffer.DisposeAsync();
        }
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    public async Task KernelCompilation_ShouldWork()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        Assert.NotNull(_accelerator);

        var kernelSourceCode = @"
extern ""C"" __global__ void addOne(float* data, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        data[idx] += 1.0f;
    }
}";

        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Default };
        var kernelSource = new TextKernelSource(kernelSourceCode, "addOne", DotCompute.Abstractions.KernelLanguage.Cuda, "addOne");
        var definition = new KernelDefinition("addOne", kernelSource, options);

        var compiledKernel = await _accelerator.CompileKernelAsync(definition, options);
        Assert.NotNull(compiledKernel);

        LogKernelCompilationSuccess(_logger, null);

        await compiledKernel.DisposeAsync();
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    public async Task SimpleKernel_ShouldExecute()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        Assert.NotNull(_accelerator);

        const int N = 1000;
        var data = new float[N];
        for (var i = 0; i < N; i++)
        {
            data[i] = i;
        }

        var buffer = await _accelerator.Memory.AllocateAsync(N * sizeof(float));

        try
        {
            await buffer.CopyFromHostAsync<float>(data);

            var kernelSource = @"
extern ""C"" __global__ void multiply(float* data, float factor, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        data[idx] *= factor;
    }
}";

            var kernelSourceObj = new TextKernelSource(kernelSource, "multiply", DotCompute.Abstractions.KernelLanguage.Cuda, "multiply");
            var options = new CompilationOptions();
            var definition = new KernelDefinition("multiply", kernelSourceObj, options);
            var compiledKernel = await _accelerator.CompileKernelAsync(definition);

            const float FACTOR = 2.5f;
            var arguments = new KernelArguments(buffer, FACTOR, N);
            await compiledKernel.ExecuteAsync(arguments);

            var result = new float[N];
            await buffer.CopyToHostAsync<float>(result);

            // Verify results
            for (var i = 0; i < N; i++)
            {
                var expected = data[i] * FACTOR;
                Assert.True(Math.Abs(result[i] - expected) < 0.001f,
                    $"Incorrect result at {i}: expected {expected}, got {result[i]}");
            }

            LogSimpleKernelExecutionTest(_logger, null);

            await compiledKernel.DisposeAsync();
        }
        finally
        {
            await buffer.DisposeAsync();
        }
    }

    [SkippableFact]
    [Trait("Category", "CudaRequired")]
    public async Task LaunchConfiguration_ShouldOptimizeCorrectly()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        Assert.NotNull(_accelerator);

        var kernelSourceCode = @"
extern ""C"" __global__ void testConfig(int* data, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        data[idx] = blockIdx.x;
    }
}";

        var kernelSource = new TextKernelSource(kernelSourceCode, "testConfig", DotCompute.Abstractions.KernelLanguage.Cuda, "testConfig");
        var options = new CompilationOptions();
        var definition = new KernelDefinition("testConfig", kernelSource, options);
        var compiledKernel = await _accelerator.CompileKernelAsync(definition) as CudaCompiledKernel;
        Assert.NotNull(compiledKernel);

        try
        {
            const int N = 10000;
            var config = compiledKernel.GetOptimalLaunchConfig(N);

            // Verify configuration makes sense
            var totalThreads = config.GridX * config.BlockX;
            _ = (totalThreads >= N).Should().BeTrue();
            _ = config.BlockX.Should().BeLessThanOrEqualTo(1024, "Block size should be reasonable"); // Max for most GPUs
            _ = config.BlockX.Should().BeGreaterThanOrEqualTo(32, "Block size should be at least one warp");

            LogOptimalConfig(_logger, N, config.GridX, config.BlockX, totalThreads, null);

            // Test execution with custom config
            var data = new int[N];
            var buffer = await _accelerator.Memory.AllocateAsync(N * sizeof(int));

            try
            {
                var arguments = new KernelArguments(buffer, N);
                await compiledKernel.ExecuteWithConfigAsync(arguments, config);

                await buffer.CopyToHostAsync<int>(data);

                // Verify some results(block IDs should be reasonable)
                _ = data[0].Should().BeGreaterThanOrEqualTo(0, "Block ID should be non-negative");
                if (N > 100)
                {
                    _ = data[N - 1].Should().BeGreaterThanOrEqualTo(0, "Last element should have valid block ID");
                }

                LogLaunchConfigurationTest(_logger, null);
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
        if (_disposed)
            return;

        _accelerator?.Dispose();
        _backend?.Dispose();
        _loggerFactory?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
