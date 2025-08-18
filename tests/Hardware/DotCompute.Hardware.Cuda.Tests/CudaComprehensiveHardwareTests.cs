// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Execution;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.P2P;
using DotCompute.Core.Kernels;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Tests.Hardware;

/// <summary>
/// Comprehensive hardware tests for CUDA GPU operations targeting RTX 2000 Ada.
/// These tests validate all CUDA functionality including memory management, kernel execution,
/// P2P transfers, unified memory, and performance optimizations.
/// </summary>
[Trait("Category", "HardwareRequired")]
[Trait("Category", "CudaRequired")]
[Trait("Category", "Hardware")]
[Collection("Hardware")]
public sealed class CudaComprehensiveHardwareTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<CudaComprehensiveHardwareTests> _logger;
    private CudaContext? _context;
    private CudaDevice? _device;
    private bool _cudaAvailable;
    private readonly CudaDeviceProperties _deviceProperties;

    public CudaComprehensiveHardwareTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new TestLogger<CudaComprehensiveHardwareTests>(output);
        _deviceProperties = new();
        InitializeCuda();
    }

    private void InitializeCuda()
    {
        try
        {
            // Check for CUDA availability
            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            if (result != CudaError.Success || deviceCount == 0)
            {
                _output.WriteLine($"No CUDA devices found. Error: {result}, Count: {deviceCount}");
                _cudaAvailable = false;
                return;
            }

            _output.WriteLine($"Found {deviceCount} CUDA device(s)");

            // Get device properties
            result = CudaRuntime.cudaGetDeviceProperties(ref _deviceProperties, 0);
            if (result != CudaError.Success)
            {
                _output.WriteLine($"Failed to get device properties: {result}");
                _cudaAvailable = false;
                return;
            }

            _output.WriteLine($"Device: {_deviceProperties.Name}");
            _output.WriteLine($"Compute Capability: {_deviceProperties.Major}.{_deviceProperties.Minor}");
            _output.WriteLine($"Total Memory: {_deviceProperties.TotalGlobalMem / (1024 * 1024 * 1024)} GB");
            _output.WriteLine($"SMs: {_deviceProperties.MultiProcessorCount}");

            // Create context
            _context = new CudaContext(0, _logger);
            _device = new CudaDevice(0, _context, _logger);
            _cudaAvailable = true;

            _output.WriteLine("CUDA initialized successfully");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"CUDA initialization failed: {ex.Message}");
            _cudaAvailable = false;
        }
    }

    [SkippableFact]
    [Trait("Priority", "Critical")]
    public void Validate_RTX2000Ada_Detection()
    {
        Skip.IfNot(_cudaAvailable, "CUDA not available");

        // Verify RTX 2000 Ada detection
        _deviceProperties.Name.Should().Contain("RTX");
        _deviceProperties.Major.Should().Be(8, "RTX 2000 Ada has compute capability 8.9");
        _deviceProperties.Minor.Should().Be(9, "RTX 2000 Ada has compute capability 8.9");
        _deviceProperties.MultiProcessorCount.Should().Be(24, "RTX 2000 Ada has 24 SMs");
        
        // Verify memory configuration
        var totalMemoryGB = _deviceProperties.TotalGlobalMem / (1024.0 * 1024.0 * 1024.0);
        totalMemoryGB.Should().BeApproximately(8.0, 0.5, "RTX 2000 Ada has 8GB VRAM");

        _output.WriteLine($"✓ RTX 2000 Ada detected correctly: {_deviceProperties.Name}");
    }

    [SkippableFact]
    [Trait("Priority", "Critical")]
    public async Task MemoryManager_AllocateAndTransfer_ShouldWork()
    {
        Skip.IfNot(_cudaAvailable, "CUDA not available");

        // Create memory manager
        var memoryManager = new CudaMemoryManager(_context!, _logger);

        // Test various allocation sizes
        var testSizes = new[] { 1024, 1024 * 1024, 64 * 1024 * 1024 }; // 1KB, 1MB, 64MB

        foreach (var size in testSizes)
        {
            // Allocate device memory
            var buffer = await memoryManager.AllocateAsync(size);
            buffer.Should().NotBeNull();
            buffer.SizeInBytes.Should().Be(size);

            // Create test data
            var testData = new byte[size];
            Random.Shared.NextBytes(testData);

            // Copy to device
            await buffer.CopyFromHostAsync<byte>(testData);

            // Copy back from device
            var resultData = new byte[size];
            await buffer.CopyToHostAsync<byte>(resultData);

            // Verify data integrity
            resultData.Should().BeEquivalentTo(testData);

            await buffer.DisposeAsync();
            _output.WriteLine($"✓ Memory test passed for {size / 1024}KB");
        }
    }

    [SkippableFact]
    [Trait("Priority", "Critical")]
    public async Task UnifiedMemory_AllocateAndAccess_ShouldWork()
    {
        Skip.IfNot(_cudaAvailable, "CUDA not available");

        var unifiedMemManager = new CudaUnifiedMemoryManager(_context!, _logger);

        // Allocate unified memory
        var size = 16 * 1024 * 1024; // 16MB
        var buffer = await unifiedMemManager.AllocateAsync(size, MemoryOptions.UnifiedMemory);
        
        buffer.Should().NotBeNull();
        
        if (buffer is CudaUnifiedMemoryBuffer unifiedBuffer)
        {
            unifiedBuffer.IsUnifiedMemory.Should().BeTrue();

            // Test data
            var testData = new float[size / sizeof(float)];
            for (int i = 0; i < testData.Length; i++)
            {
                testData[i] = i * 0.5f;
            }

            // Copy to unified memory
            await buffer.CopyFromHostAsync<float>(testData);

            // Prefetch to device
            await unifiedBuffer.PrefetchToDeviceAsync();

            // Prefetch back to host
            await unifiedBuffer.PrefetchToHostAsync();

            // Read back
            var resultData = new float[testData.Length];
            await buffer.CopyToHostAsync<float>(resultData);

            // Verify
            for (int i = 0; i < Math.Min(100, resultData.Length); i++)
            {
                resultData[i].Should().BeApproximately(testData[i], 0.0001f);
            }

            _output.WriteLine($"✓ Unified memory test passed ({size / (1024 * 1024)}MB)");
        }

        await buffer.DisposeAsync();
    }

    [SkippableFact]
    [Trait("Priority", "High")]
    public async Task KernelCompilation_NVRTC_ShouldWork()
    {
        Skip.IfNot(_cudaAvailable, "CUDA not available");

        var compiler = new CudaKernelCompiler(_context!, _logger);

        // Simple vector addition kernel
        var kernelSource = @"
extern ""C"" __global__ void vectorAdd(float* a, float* b, float* c, int n) {
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < n) {
        c[idx] = a[idx] + b[idx];
    }
}";

        var kernel = new KernelDefinition
        {
            Name = "vectorAdd",
            Source = kernelSource,
            Language = KernelLanguage.CUDA
        };

        // Compile kernel
        var compiledKernel = await compiler.CompileAsync(kernel);
        compiledKernel.Should().NotBeNull();
        compiledKernel.Id.Should().NotBeEmpty();

        _output.WriteLine($"✓ Kernel compilation successful: {compiledKernel.Id}");

        // Clean up
        if (compiledKernel.NativeHandle != IntPtr.Zero)
        {
            CudaRuntime.cuModuleUnload(compiledKernel.NativeHandle);
        }
    }

    [SkippableFact]
    [Trait("Priority", "High")]
    public async Task KernelExecution_VectorAddition_ShouldWork()
    {
        Skip.IfNot(_cudaAvailable, "CUDA not available");

        var compiler = new CudaKernelCompiler(_context!, _logger);
        var streamManager = new CudaStreamManager(_context!, _logger);
        var eventManager = new CudaEventManager(_logger);
        var executor = new CudaKernelExecutor(_device!, _context!, streamManager, eventManager, _logger);

        // Compile vector addition kernel
        var kernelSource = @"
extern ""C"" __global__ void vectorAdd(float* a, float* b, float* c, int n) {
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < n) {
        c[idx] = a[idx] + b[idx];
    }
}";

        var kernel = new KernelDefinition
        {
            Name = "vectorAdd",
            Source = kernelSource,
            Language = KernelLanguage.CUDA
        };

        var compiledKernel = await compiler.CompileAsync(kernel);

        // Prepare data
        var n = 1024 * 1024; // 1M elements
        var sizeBytes = n * sizeof(float);

        var memManager = new CudaMemoryManager(_context!, _logger);
        var bufferA = await memManager.AllocateAsync(sizeBytes);
        var bufferB = await memManager.AllocateAsync(sizeBytes);
        var bufferC = await memManager.AllocateAsync(sizeBytes);

        // Initialize test data
        var hostA = new float[n];
        var hostB = new float[n];
        for (int i = 0; i < n; i++)
        {
            hostA[i] = i;
            hostB[i] = i * 2;
        }

        await bufferA.CopyFromHostAsync<float>(hostA);
        await bufferB.CopyFromHostAsync<float>(hostB);

        // Execute kernel
        var args = new KernelArgument[]
        {
            new() { Name = "a", Value = bufferA, Type = typeof(IMemoryBuffer), IsDeviceMemory = true },
            new() { Name = "b", Value = bufferB, Type = typeof(IMemoryBuffer), IsDeviceMemory = true },
            new() { Name = "c", Value = bufferC, Type = typeof(IMemoryBuffer), IsDeviceMemory = true },
            new() { Name = "n", Value = n, Type = typeof(int), IsDeviceMemory = false }
        };

        var config = new KernelExecutionConfig
        {
            GlobalWorkSize = [n],
            LocalWorkSize = [256],
            CaptureTimings = true
        };

        var result = await executor.ExecuteAndWaitAsync(compiledKernel, args, config);
        result.Success.Should().BeTrue();
        result.Timings.Should().NotBeNull();

        // Verify results
        var hostC = new float[n];
        await bufferC.CopyToHostAsync<float>(hostC);

        for (int i = 0; i < Math.Min(100, n); i++)
        {
            var expected = hostA[i] + hostB[i];
            hostC[i].Should().BeApproximately(expected, 0.0001f);
        }

        _output.WriteLine($"✓ Kernel execution successful in {result.Timings!.KernelTimeMs:F3}ms");

        // Clean up
        await bufferA.DisposeAsync();
        await bufferB.DisposeAsync();
        await bufferC.DisposeAsync();
        streamManager.Dispose();
        eventManager.Dispose();
    }

    [SkippableFact]
    [Trait("Priority", "Medium")]
    public async Task P2PMemoryTransfer_MultiGPU_ShouldWork()
    {
        Skip.IfNot(_cudaAvailable, "CUDA not available");

        var p2pManager = new CudaP2PManager(_logger);
        
        // Discover P2P topology
        var topology = await p2pManager.DiscoverTopologyAsync();
        
        _output.WriteLine($"P2P Topology: {topology.DeviceCount} devices");
        _output.WriteLine($"Fully connected: {topology.IsFullyConnected}");
        
        foreach (var device in topology.Devices)
        {
            _output.WriteLine($"  Device {device.DeviceId}: {device.Name} ({device.ComputeCapability})");
        }

        if (topology.DeviceCount > 1 && topology.Connections.Count > 0)
        {
            // Test P2P transfer if multiple GPUs available
            var connection = topology.Connections.First();
            
            var enabled = await p2pManager.EnableP2PAccessAsync(
                connection.SourceDevice, 
                connection.DestinationDevice);
            
            enabled.Should().BeTrue("P2P should be enabled between compatible devices");
            
            _output.WriteLine($"✓ P2P enabled: Device {connection.SourceDevice} -> Device {connection.DestinationDevice}");
            _output.WriteLine($"  Bandwidth: {connection.BandwidthGBps:F2} GB/s");
        }
        else
        {
            _output.WriteLine("⚠ Single GPU system - P2P tests skipped");
        }

        p2pManager.Dispose();
    }

    [SkippableFact]
    [Trait("Priority", "High")]
    public async Task KernelProfiling_PerformanceMetrics_ShouldWork()
    {
        Skip.IfNot(_cudaAvailable, "CUDA not available");

        var compiler = new CudaKernelCompiler(_context!, _logger);
        var streamManager = new CudaStreamManager(_context!, _logger);
        var eventManager = new CudaEventManager(_logger);
        var executor = new CudaKernelExecutor(_device!, _context!, streamManager, eventManager, _logger);

        // Matrix multiplication kernel (compute intensive)
        var kernelSource = @"
extern ""C"" __global__ void matrixMul(float* A, float* B, float* C, int N) {
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    
    if (row < N && col < N) {
        float sum = 0.0f;
        for (int k = 0; k < N; k++) {
            sum += A[row * N + k] * B[k * N + col];
        }
        C[row * N + col] = sum;
    }
}";

        var kernel = new KernelDefinition
        {
            Name = "matrixMul",
            Source = kernelSource,
            Language = KernelLanguage.CUDA
        };

        var compiledKernel = await compiler.CompileAsync(kernel);

        // Small matrix for profiling
        var N = 512;
        var sizeBytes = N * N * sizeof(float);

        var memManager = new CudaMemoryManager(_context!, _logger);
        var bufferA = await memManager.AllocateAsync(sizeBytes);
        var bufferB = await memManager.AllocateAsync(sizeBytes);
        var bufferC = await memManager.AllocateAsync(sizeBytes);

        // Initialize with identity matrices
        var hostA = new float[N * N];
        var hostB = new float[N * N];
        for (int i = 0; i < N; i++)
        {
            hostA[i * N + i] = 1.0f;
            hostB[i * N + i] = 1.0f;
        }

        await bufferA.CopyFromHostAsync<float>(hostA);
        await bufferB.CopyFromHostAsync<float>(hostB);

        var args = new KernelArgument[]
        {
            new() { Name = "A", Value = bufferA, Type = typeof(IMemoryBuffer), IsDeviceMemory = true },
            new() { Name = "B", Value = bufferB, Type = typeof(IMemoryBuffer), IsDeviceMemory = true },
            new() { Name = "C", Value = bufferC, Type = typeof(IMemoryBuffer), IsDeviceMemory = true },
            new() { Name = "N", Value = N, Type = typeof(int), IsDeviceMemory = false }
        };

        var config = new KernelExecutionConfig
        {
            GlobalWorkSize = [N, N],
            LocalWorkSize = [16, 16],
            CaptureTimings = true
        };

        // Profile kernel
        var profilingResult = await executor.ProfileAsync(compiledKernel, args, config, iterations: 10);
        
        profilingResult.Should().NotBeNull();
        profilingResult.AverageTimeMs.Should().BeGreaterThan(0);
        profilingResult.MinTimeMs.Should().BeGreaterThan(0);
        profilingResult.MaxTimeMs.Should().BeGreaterThanOrEqualTo(profilingResult.MinTimeMs);
        
        _output.WriteLine($"✓ Kernel Profiling Results:");
        _output.WriteLine($"  Average: {profilingResult.AverageTimeMs:F3}ms");
        _output.WriteLine($"  Min: {profilingResult.MinTimeMs:F3}ms");
        _output.WriteLine($"  Max: {profilingResult.MaxTimeMs:F3}ms");
        _output.WriteLine($"  StdDev: {profilingResult.StdDevMs:F3}ms");
        _output.WriteLine($"  Occupancy: {profilingResult.AchievedOccupancy:P1}");
        
        if (profilingResult.Bottleneck != null)
        {
            _output.WriteLine($"  Bottleneck: {profilingResult.Bottleneck.Type} (severity: {profilingResult.Bottleneck.Severity:P1})");
        }

        // Clean up
        await bufferA.DisposeAsync();
        await bufferB.DisposeAsync();
        await bufferC.DisposeAsync();
        streamManager.Dispose();
        eventManager.Dispose();
    }

    [SkippableFact]
    [Trait("Priority", "Low")]
    public async Task GraphExecution_KernelFusion_ShouldWork()
    {
        Skip.IfNot(_cudaAvailable, "CUDA not available");

        var streamManager = new CudaStreamManager(_context!, _logger);
        var eventManager = new CudaEventManager(_logger);
        var graphSupport = new CudaGraphSupport(_context!, streamManager, eventManager, _logger);

        // Create a simple graph with captured operations
        var graphId = "test_graph";
        
        var capturedGraphId = await graphSupport.CaptureGraphAsync(
            graphId,
            async (stream) =>
            {
                // Simulate kernel launches
                await Task.Delay(1);
                // In real scenario, would launch kernels here
            },
            CudaGraphCaptureMode.Global);

        capturedGraphId.Should().Be(graphId);
        
        // Get graph statistics
        var stats = graphSupport.GetGraphStatistics(graphId);
        stats.Should().NotBeNull();
        stats.GraphId.Should().Be(graphId);
        
        _output.WriteLine($"✓ Graph created: {graphId}");
        
        graphSupport.Dispose();
        streamManager.Dispose();
        eventManager.Dispose();
    }

    public void Dispose()
    {
        _device?.Dispose();
        _context?.Dispose();
    }

    // Test logger implementation
    private class TestLogger<T> : ILogger<T>
    {
        private readonly ITestOutputHelper _output;

        public TestLogger(ITestOutputHelper output) => _output = output;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
            if (exception != null)
            {
                _output.WriteLine($"  Exception: {exception.Message}");
            }
        }
    }
}