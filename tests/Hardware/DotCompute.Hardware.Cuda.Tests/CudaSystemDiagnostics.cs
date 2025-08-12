// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Tests.Hardware;

/// <summary>
/// Comprehensive system diagnostics for CUDA backend
/// </summary>
[Collection("Hardware")]
public class CudaSystemDiagnostics : IDisposable
{
    private readonly ILogger<CudaSystemDiagnostics> _logger;
    private readonly CudaBackend? _backend;
    private readonly CudaAccelerator? _accelerator;
    private bool _disposed;

    public CudaSystemDiagnostics(ITestOutputHelper output)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Debug));
        
        _logger = loggerFactory.CreateLogger<CudaSystemDiagnostics>();

        if (CudaBackend.IsAvailable())
        {
            _backend = new CudaBackend(loggerFactory.CreateLogger<CudaBackend>());
            _accelerator = _backend.GetDefaultAccelerator();
        }
    }

    [SkippableFact]
    public void CudaRuntime_SystemDiagnostics()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");

        _logger.LogInformation("=== CUDA Runtime System Diagnostics ===");

        // 1. Driver and Runtime Versions
        var runtimeResult = CudaRuntime.cudaRuntimeGetVersion(out var runtimeVersion);
        var driverResult = CudaRuntime.cudaDriverGetVersion(out var driverVersion);

        if (runtimeResult == CudaError.Success)
        {
            var runtimeMajor = runtimeVersion / 1000;
            var runtimeMinor = (runtimeVersion % 1000) / 10;
            _logger.LogInformation("CUDA Runtime Version: {Major}.{Minor}", runtimeMajor, runtimeMinor);
        }

        if (driverResult == CudaError.Success)
        {
            var driverMajor = driverVersion / 1000;
            var driverMinor = (driverVersion % 1000) / 10;
            _logger.LogInformation("CUDA Driver Version: {Major}.{Minor}", driverMajor, driverMinor);
        }

        // 2. Device Count and Properties
        var deviceCountResult = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
        Assert.Equal(CudaError.Success, deviceCountResult);
        _logger.LogInformation("CUDA Devices Found: {DeviceCount}", deviceCount);

        for (int i = 0; i < deviceCount; i++)
        {
            var props = new CudaDeviceProperties();
            var propResult = CudaRuntime.cudaGetDeviceProperties(ref props, i);
            
            if (propResult == CudaError.Success)
            {
                _logger.LogInformation("Device {Id}: {Name}", i, props.Name);
                _logger.LogInformation("  Compute Capability: {Major}.{Minor}", props.Major, props.Minor);
                _logger.LogInformation("  Global Memory: {Memory:N0} bytes ({MemoryGB:F1} GB)", 
                    props.TotalGlobalMem, props.TotalGlobalMem / (1024.0 * 1024 * 1024));
                _logger.LogInformation("  Multiprocessors: {SMs}", props.MultiProcessorCount);
                _logger.LogInformation("  Max Threads per Block: {MaxThreads}", props.MaxThreadsPerBlock);
                _logger.LogInformation("  Shared Memory per Block: {SharedMem:N0} bytes", props.SharedMemPerBlock);
                _logger.LogInformation("  Warp Size: {WarpSize}", props.WarpSize);
                _logger.LogInformation("  Clock Rate: {ClockRate} kHz", props.ClockRate);
                _logger.LogInformation("  Memory Clock: {MemoryClock} kHz", props.MemoryClockRate);
                _logger.LogInformation("  Memory Bus Width: {BusWidth} bits", props.MemoryBusWidth);
                _logger.LogInformation("  ECC Enabled: {ECC}", props.ECCEnabled > 0);
                _logger.LogInformation("  Unified Addressing: {UVA}", props.UnifiedAddressing > 0);
                _logger.LogInformation("  Concurrent Kernels: {ConcurrentKernels}", props.ConcurrentKernels > 0);
            }
        }

        // 3. NVRTC Availability
        if (CudaKernelCompiler.IsNvrtcAvailable())
        {
            var (nvrtcMajor, nvrtcMinor) = CudaKernelCompiler.GetNvrtcVersion();
            _logger.LogInformation("NVRTC Available: Version {Major}.{Minor}", nvrtcMajor, nvrtcMinor);
        }
        else
        {
            _logger.LogWarning("NVRTC Not Available - kernel compilation may not work");
        }
    }

    [SkippableFact]
    public void AcceleratorInfo_ShouldBeComplete()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");
        Assert.NotNull(_accelerator);

        _logger.LogInformation("=== Accelerator Information ===");

        var info = _accelerator.Info;
        
        Assert.NotNull(info);
        Assert.Equal("CUDA", info.Type);
        Assert.False(string.IsNullOrEmpty(info.Name));
        Assert.True(info.TotalMemory > 0);
        Assert.True(info.ComputeUnits > 0);
        Assert.NotNull(info.ComputeCapability);
        Assert.NotNull(info.Capabilities);

        _logger.LogInformation("Accelerator Type: {Type}", info.Type);
        _logger.LogInformation("Device Name: {Name}", info.Name);
        _logger.LogInformation("Driver Version: {Version}", info.DriverVersion);
        _logger.LogInformation("Total Memory: {Memory:N0} bytes", info.TotalMemory);
        _logger.LogInformation("Compute Units: {Units}", info.ComputeUnits);
        _logger.LogInformation("Max Clock: {Clock} MHz", info.MaxClockFrequency);
        _logger.LogInformation("Compute Capability: {Capability}", info.ComputeCapability);
        _logger.LogInformation("Unified Memory: {Unified}", info.IsUnifiedMemory);

        // Validate specific capabilities
        var caps = info.Capabilities;
        var expectedCapabilities = new[]
        {
            "ComputeCapabilityMajor", "ComputeCapabilityMinor", "SharedMemoryPerBlock",
            "ConstantMemory", "MultiprocessorCount", "MaxThreadsPerBlock",
            "WarpSize", "ClockRate", "MemoryClockRate"
        };

        foreach (var expectedCap in expectedCapabilities)
        {
            Assert.True(caps.ContainsKey(expectedCap), $"Missing capability: {expectedCap}");
            _logger.LogInformation("  {Capability}: {Value}", expectedCap, caps[expectedCap]);
        }
    }

    [SkippableFact]
    public async Task MemoryManager_ShouldHandleAllOperations()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");
        Assert.NotNull(_accelerator);

        _logger.LogInformation("=== Memory Manager Diagnostics ===");

        var memory = _accelerator.Memory;
        // Note: GetStatistics() method doesn't exist in new IMemoryManager interface
        
        _logger.LogInformation("Memory Manager Test - Testing various allocation sizes");

        // Test various allocation sizes
        var testSizes = new[] { 1024, 1024*1024, 16*1024*1024, 64*1024*1024 };
        var buffers = new List<IMemoryBuffer>();

        try
        {
            foreach (var size in testSizes)
            {
                _logger.LogInformation("Allocating {Size:N0} bytes", size);
                var buffer = await memory.AllocateAsync(size);
                buffers.Add(buffer);

                // Test memory operations
                var testData = new byte[Math.Min(size, 1024)];
                new Random().NextBytes(testData);

                await buffer.CopyFromHostAsync<byte>(testData);
                var readBack = new byte[testData.Length];
                await buffer.CopyToHostAsync<byte>(readBack);

                Assert.Equal(testData, readBack);
                _logger.LogInformation("  Copy operations verified for {Size:N0} bytes", size);

                // Test fill operation - this would need to be implemented differently
                // Fill operation is not part of the new IMemoryBuffer interface
                _logger.LogInformation("  Fill operation test skipped (not available in new API)");

                // Test slicing
                if (size > 2048)
                {
                    var slice = memory.CreateView(buffer, 1024, 1024);
                    Assert.Equal(1024, slice.SizeInBytes);
                    _logger.LogInformation("  Slicing verified");
                }
            }

            _logger.LogInformation("Memory allocation tests completed successfully");
        }
        finally
        {
            foreach (var buffer in buffers)
            {
                await buffer.DisposeAsync();
            }
        }
    }

    [SkippableFact]
    public async Task KernelCompiler_ShouldHandleAllSourceTypes()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");
        Assert.NotNull(_accelerator);

        _logger.LogInformation("=== Kernel Compiler Diagnostics ===");

        // Test CUDA source compilation
        var cudaSource = @"
extern ""C"" __global__ void testKernel(float* input, float* output, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < n) {
        output[idx] = input[idx] * 2.0f + 1.0f;
    }
}";

        var kernelSource = new TextKernelSource(cudaSource, "testKernel", KernelLanguage.Cuda, "testKernel");
        var definition = new KernelDefinition("testKernel", kernelSource, new CompilationOptions());

        // Test different optimization levels
        var optimizationLevels = Enum.GetValues<OptimizationLevel>();
        
        foreach (var optLevel in optimizationLevels)
        {
            var options = new CompilationOptions 
            { 
                OptimizationLevel = optLevel,
                EnableDebugInfo = optLevel == OptimizationLevel.None
            };

            _logger.LogInformation("Compiling with optimization level: {Level}", optLevel);
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var compiledKernel = await _accelerator.CompileKernelAsync(definition, options);
            stopwatch.Stop();

            Assert.NotNull(compiledKernel);
            _logger.LogInformation("  Compilation successful in {Time}ms", stopwatch.ElapsedMilliseconds);

            // Test execution
            const int N = 1000;
            var input = Enumerable.Range(0, N).Select(i => (float)i).ToArray();
            var output = new float[N];

            var inputBuffer = await _accelerator.Memory.AllocateAsync(N * sizeof(float));
            var outputBuffer = await _accelerator.Memory.AllocateAsync(N * sizeof(float));

            try
            {
                await inputBuffer.CopyFromHostAsync<float>(input);
                
                var arguments = new KernelArguments(inputBuffer, outputBuffer, N);
                await compiledKernel.ExecuteAsync(arguments);
                
                await outputBuffer.CopyToHostAsync<float>(output);

                // Verify results
                for (int i = 0; i < N; i++)
                {
                    var expected = input[i] * 2.0f + 1.0f;
                    Assert.True(Math.Abs(output[i] - expected) < 0.001f,
                        $"Incorrect result at {i}: expected {expected}, got {output[i]}");
                }

                _logger.LogInformation("  Execution and verification successful");
            }
            finally
            {
                await inputBuffer.DisposeAsync();
                await outputBuffer.DisposeAsync();
                await compiledKernel.DisposeAsync();
            }
        }
    }

    [SkippableFact]
    public async Task LaunchConfiguration_ShouldOptimizeForDevice()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");
        Assert.NotNull(_accelerator);

        _logger.LogInformation("=== Launch Configuration Diagnostics ===");

        var kernelSource = @"
extern ""C"" __global__ void configTest(int* data, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < n) {
        data[idx] = blockIdx.x * 1000 + threadIdx.x;
    }
}";

        var kernelSourceObj = new TextKernelSource(kernelSource, "configTest", KernelLanguage.Cuda, "configTest");
        var definition = new KernelDefinition("configTest", kernelSourceObj, new CompilationOptions());
        var compiledKernel = await _accelerator.CompileKernelAsync(definition) as CudaCompiledKernel;
        Assert.NotNull(compiledKernel);

        try
        {
            // Test different problem sizes
            var problemSizes = new[] { 1000, 10000, 100000, 1000000 };
            
            foreach (var problemSize in problemSizes)
            {
                var config = compiledKernel.GetOptimalLaunchConfig(problemSize);
                
                _logger.LogInformation("Problem Size {Size:N0}:", problemSize);
                _logger.LogInformation("  Grid: ({X}, {Y}, {Z})", config.GridX, config.GridY, config.GridZ);
                _logger.LogInformation("  Block: ({X}, {Y}, {Z})", config.BlockX, config.BlockY, config.BlockZ);
                _logger.LogInformation("  Total Threads: {Threads:N0}", config.GridX * config.GridY * config.GridZ * config.BlockX * config.BlockY * config.BlockZ);

                // Verify configuration covers the problem
                var totalThreads = config.GridX * config.BlockX;
                Assert.True(totalThreads >= problemSize, 
                    $"Configuration doesn't cover problem size: {totalThreads} < {problemSize}");

                // Test execution with this configuration
                var data = new int[problemSize];
                var buffer = await _accelerator.Memory.AllocateAsync(problemSize * sizeof(int));

                try
                {
                    var arguments = new KernelArguments(buffer, problemSize);
                    await compiledKernel.ExecuteWithConfigAsync(arguments, config);
                    
                    await buffer.CopyToHostAsync<int>(data);

                    // Verify some results (first few elements)
                    for (int i = 0; i < Math.Min(100, problemSize); i++)
                    {
                        var expectedBlock = i / (int)config.BlockX;
                        var expectedThread = i % (int)config.BlockX;
                        var expected = expectedBlock * 1000 + expectedThread;
                        
                        Assert.Equal(expected, data[i]);
                    }

                    _logger.LogInformation("  Execution successful and verified");
                }
                finally
                {
                    await buffer.DisposeAsync();
                }
            }
        }
        finally
        {
            await compiledKernel.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task ErrorHandling_ShouldProvideDetailedInformation()
    {
        Skip.IfNot(CudaBackend.IsAvailable(), "CUDA runtime not available");
        Assert.NotNull(_accelerator);

        _logger.LogInformation("=== Error Handling Diagnostics ===");

        // Test compilation error handling
        var invalidKernelSource = @"
extern ""C"" __global__ void invalidKernel(float* data)
{
    undeclared_variable = data[threadIdx.x]; // This should cause compilation error
}";

        var kernelSourceObj = new TextKernelSource(invalidKernelSource, "invalidKernel", KernelLanguage.Cuda, "invalidKernel");
        var definition = new KernelDefinition("invalidKernel", kernelSourceObj, new CompilationOptions());

        var compilationException = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _accelerator.CompileKernelAsync(definition));

        Assert.NotNull(compilationException);
        Assert.Contains("Failed to compile", compilationException.Message);
        _logger.LogInformation("Compilation error handled correctly: {Message}", compilationException.Message);

        // Test memory allocation error handling (try to allocate very large amount)
        var oversizeAllocation = long.MaxValue / 2; // Very large allocation

        var memoryException = await Assert.ThrowsAsync<OutOfMemoryException>(
            async () => await _accelerator.Memory.AllocateAsync(oversizeAllocation));

        Assert.NotNull(memoryException);
        _logger.LogInformation("Memory allocation error handled correctly: {Message}", memoryException.Message);

        // Test execution error handling (null arguments)
        var validSource = @"extern ""C"" __global__ void validKernel(float* data, int n) { }";
        var validKernelSource = new TextKernelSource(validSource, "validKernel", KernelLanguage.Cuda, "validKernel");
        var validDefinition = new KernelDefinition("validKernel", validKernelSource, new CompilationOptions());
        var validKernel = await _accelerator.CompileKernelAsync(validDefinition);

        try
        {
            var executionException = await Assert.ThrowsAsync<ArgumentException>(
                async () => await validKernel.ExecuteAsync(new KernelArguments()));

            Assert.NotNull(executionException);
            _logger.LogInformation("Execution error handled correctly: {Message}", executionException.Message);
        }
        finally
        {
            await validKernel.DisposeAsync();
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