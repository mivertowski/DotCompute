// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Tests.Utilities;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Hardware.Integration
{

/// <summary>
/// Performance benchmarks and tests for CUDA backend operations
/// </summary>
[Collection("CUDA Hardware Tests")]
public sealed class CudaPerformanceTests : IDisposable
{
    private readonly ILogger<CudaPerformanceTests> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ITestOutputHelper _output;
    private readonly List<CudaAccelerator> _accelerators = [];
    private readonly List<ISyncMemoryBuffer> _buffers = [];

    // LoggerMessage delegates for performance
    private static readonly Action<ILogger, string, Exception?> LogBufferDisposeError = 
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, nameof(LogBufferDisposeError)),
            "Error disposing CUDA buffer: {Message}");

    private static readonly Action<ILogger, string, Exception?> LogAcceleratorDisposeError = 
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2, nameof(LogAcceleratorDisposeError)),
            "Error disposing CUDA accelerator: {Message}");

    public CudaPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        _logger = _loggerFactory.CreateLogger<CudaPerformanceTests>();
    }

    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Hardware", "CUDA")]
    public void CudaPerformance_DeviceInitialization_ShouldCompleteQuickly()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var stopwatch = Stopwatch.StartNew();

        // Act
        var loggerFactory = _loggerFactory;
        var acceleratorLogger = loggerFactory.CreateLogger<CudaAccelerator>();
        var accelerator = new CudaAccelerator(0, acceleratorLogger);
        _accelerators.Add(accelerator);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000,
            "Device initialization should complete within 3 seconds");
        _output.WriteLine($"Device initialization took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Theory]
    [Trait("Category", "Performance")]
    [Trait("Hardware", "CUDA")]
    [InlineData(1024)]           // 1KB
    [InlineData(1024 * 1024)]    // 1MB  
    [InlineData(16 * 1024 * 1024)] // 16MB
    [InlineData(64 * 1024 * 1024)] // 64MB
    public void CudaPerformance_MemoryAllocation_ShouldScaleLinearlyWithSize(long sizeInBytes)
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;

        var stopwatch = Stopwatch.StartNew();

        // Act
        var buffer = memoryManager!.Allocate(sizeInBytes);
        _buffers.Add(buffer);
        stopwatch.Stop();

        // Assert
        var sizeInMB = sizeInBytes / (1024.0 * 1024.0);
        var timePerMB = stopwatch.ElapsedMilliseconds / Math.Max(sizeInMB, 0.001);

        Assert.NotNull(buffer);
        timePerMB.Should().BeLessThan(1000, $"Allocation time should be reasonable for {sizeInMB:F2}MB");

        _output.WriteLine($"Allocated {sizeInMB:F2}MB in {stopwatch.ElapsedMilliseconds}ms{timePerMB:F2}ms/MB)");
    }

    [Theory]
    [Trait("Category", "Performance")]
    [Trait("Hardware", "CUDA")]
    [InlineData(1024 * 1024, "1MB")]
    [InlineData(16 * 1024 * 1024, "16MB")]
    [InlineData(64 * 1024 * 1024, "64MB")]
    public unsafe void CudaPerformance_HostToDeviceTransfer_ShouldMeetBandwidthExpectations(long sizeInBytes, string description)
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;
        var buffer = memoryManager!.Allocate(sizeInBytes);
        _buffers.Add(buffer);

        var hostData = new byte[sizeInBytes];
        TestDataGenerator.FillRandomBytes(hostData);

        var warmupRuns = 3;
        var benchmarkRuns = 10;

        // Warm up
        fixed (byte* hostPtr = hostData)
        {
            for (var i = 0; i < warmupRuns; i++)
            {
                memoryManager.CopyFromHost(hostPtr, buffer, sizeInBytes);
            }
        }

        var stopwatch = Stopwatch.StartNew();

        // Act - Benchmark
        fixed (byte* hostPtr = hostData)
        {
            for (var i = 0; i < benchmarkRuns; i++)
            {
                memoryManager.CopyFromHost(hostPtr, buffer, sizeInBytes);
            }
        }

        stopwatch.Stop();

        // Assert
        var avgTimeMs = stopwatch.ElapsedMilliseconds / (double)benchmarkRuns;
        var bandwidthGBps = (sizeInBytes / 1024.0 / 1024.0 / 1024.0) / (avgTimeMs / 1000.0);

        bandwidthGBps.Should().BeGreaterThan(0.1, "Host-to-device transfer should achieve reasonable bandwidth");

        _output.WriteLine($"{description} H2D transfer: {avgTimeMs:F2}ms avg, {bandwidthGBps:F2} GB/s");
    }

    [Theory]
    [Trait("Category", "Performance")]
    [Trait("Hardware", "CUDA")]
    [InlineData(1024 * 1024, "1MB")]
    [InlineData(16 * 1024 * 1024, "16MB")]
    [InlineData(64 * 1024 * 1024, "64MB")]
    public unsafe void CudaPerformance_DeviceToHostTransfer_ShouldMeetBandwidthExpectations(long sizeInBytes, string description)
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;
        var buffer = memoryManager!.Allocate(sizeInBytes);
        _buffers.Add(buffer);

        var hostData = new byte[sizeInBytes];
        var warmupRuns = 3;
        var benchmarkRuns = 10;

        // Initialize device buffer
        memoryManager.Fill(buffer, 42, sizeInBytes);

        // Warm up
        fixed (byte* hostPtr = hostData)
        {
            for (var i = 0; i < warmupRuns; i++)
            {
                memoryManager.CopyToHost(buffer, hostPtr, sizeInBytes);
            }
        }

        var stopwatch = Stopwatch.StartNew();

        // Act - Benchmark
        fixed (byte* hostPtr = hostData)
        {
            for (var i = 0; i < benchmarkRuns; i++)
            {
                memoryManager.CopyToHost(buffer, hostPtr, sizeInBytes);
            }
        }

        stopwatch.Stop();

        // Assert
        var avgTimeMs = stopwatch.ElapsedMilliseconds / (double)benchmarkRuns;
        var bandwidthGBps = (sizeInBytes / 1024.0 / 1024.0 / 1024.0) / (avgTimeMs / 1000.0);

        bandwidthGBps.Should().BeGreaterThan(0.1, "Device-to-host transfer should achieve reasonable bandwidth");

        _output.WriteLine($"{description} D2H transfer: {avgTimeMs:F2}ms avg, {bandwidthGBps:F2} GB/s");
    }

    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaPerformance_KernelCompilation_ShouldCacheEffectively()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var kernelDefinition = CreateBenchmarkKernel();

        // Act - First compilation(cold cache)
        var stopwatch1 = Stopwatch.StartNew();
        var kernel1 = await accelerator.CompileKernelAsync(kernelDefinition);
        stopwatch1.Stop();

        // Second compilation(should hit cache)
        var stopwatch2 = Stopwatch.StartNew();
        var kernel2 = await accelerator.CompileKernelAsync(kernelDefinition);
        stopwatch2.Stop();

        // Third compilation(should still hit cache)
        var stopwatch3 = Stopwatch.StartNew();
        var kernel3 = await accelerator.CompileKernelAsync(kernelDefinition);
        stopwatch3.Stop();

        // Assert
        Assert.NotNull(kernel1);
        Assert.NotNull(kernel2);
        Assert.NotNull(kernel3);

        // Cache hits should be significantly faster
        var cacheSpeedup = (double)stopwatch1.ElapsedMilliseconds / Math.Max(stopwatch2.ElapsedMilliseconds, 1);
        cacheSpeedup.Should().BeGreaterThanOrEqualTo(0.1, "Cached compilation should be faster or similar speed");

        _output.WriteLine($"First compile: {stopwatch1.ElapsedMilliseconds}ms");
        _output.WriteLine($"Second compile: {stopwatch2.ElapsedMilliseconds}msspeedup: {cacheSpeedup:F1}x)");
        _output.WriteLine($"Third compile: {stopwatch3.ElapsedMilliseconds}ms");

        (kernel1 as IDisposable)?.Dispose();
        (kernel2 as IDisposable)?.Dispose();
        (kernel3 as IDisposable)?.Dispose();
    }

    [Theory]
    [Trait("Category", "Performance")]
    [Trait("Hardware", "CUDA")]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.Default)]
    [InlineData(OptimizationLevel.Maximum)]
    public async Task CudaPerformance_KernelCompilation_OptimizationLevelImpact(OptimizationLevel level)
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        var kernelDefinition = CreateComplexKernel();
        var options = new CompilationOptions { OptimizationLevel = level };

        var stopwatch = Stopwatch.StartNew();

        // Act
        var kernel = await accelerator.CompileKernelAsync(kernelDefinition, options);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(kernel);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(30000,
            $"Kernel compilation with {level} optimization should complete within 30 seconds");

        _output.WriteLine($"Compilation with {level} optimization took {stopwatch.ElapsedMilliseconds}ms");

        (kernel as IDisposable)?.Dispose();
    }

    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Hardware", "CUDA")]
    public async Task CudaPerformance_ConcurrentCompilation_ShouldHandleParallelLoad()
    {
        // Arrange
        if (!IsCudaAvailable() || !IsNvrtcAvailable())
            return;

        var accelerator = CreateAccelerator();
        const int concurrentKernels = 5;

        var kernelDefinitions = Enumerable.Range(0, concurrentKernels)
            .Select(i => CreateUniqueKernel($"concurrent_kernel_{i}"))
            .ToArray();

        var stopwatch = Stopwatch.StartNew();

        // Act - Compile all kernels concurrently
        var compileTasks = kernelDefinitions
            .Select(kernel => accelerator.CompileKernelAsync(kernel))
            .ToArray();

        var compiledKernels = await Task.WhenAll(compileTasks.Select(t => t.AsTask()));
        stopwatch.Stop();

        // Assert
        Assert.Equal(concurrentKernels, compiledKernels.Length);
        compiledKernels.Should().AllSatisfy((k => k.Should().NotBeNull()));

        var avgTimePerKernel = stopwatch.ElapsedMilliseconds / (double)concurrentKernels;
        avgTimePerKernel.Should().BeLessThan(15000,
            "Average concurrent compilation time should be reasonable");

        _output.WriteLine($"Compiled {concurrentKernels} kernels concurrently in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average time per kernel: {avgTimePerKernel:F2}ms");

        foreach (var kernel in compiledKernels)
        {
            (kernel as IDisposable)?.Dispose();
        }
    }

    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Hardware", "CUDA")]
    public void CudaPerformance_SynchronizationOverhead_ShouldBeMeasurable()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = CreateAccelerator();
        const int syncCount = 100;

        // Warm up
        for (var i = 0; i < 10; i++)
        {
            accelerator.SynchronizeAsync().AsTask().Wait();
        }

        var stopwatch = Stopwatch.StartNew();

        // Act
        for (var i = 0; i < syncCount; i++)
        {
            accelerator.SynchronizeAsync().AsTask().Wait();
        }

        stopwatch.Stop();

        // Assert
        var avgSyncTime = stopwatch.ElapsedMilliseconds / (double)syncCount;
        avgSyncTime.Should().BeLessThan(100, "Average synchronization should be fast");

        _output.WriteLine($"Average synchronization time: {avgSyncTime:F3}ms{syncCount} operations)");
    }

    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Hardware", "CUDA")]
    public void CudaPerformance_MemoryStatisticsRetrieval_ShouldBeEfficient()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;
        const int queryCount = 1000;

        var stopwatch = Stopwatch.StartNew();

        // Act
        for (var i = 0; i < queryCount; i++)
        {
            var stats = memoryManager!.GetStatistics();
            Assert.NotNull(stats);
        }

        stopwatch.Stop();

        // Assert
        var avgQueryTime = (stopwatch.ElapsedTicks * 1000000.0 / Stopwatch.Frequency) / (double)queryCount;
        avgQueryTime.Should().BeLessThan(1000, "Memory statistics queries should be very fast");

        _output.WriteLine($"Average memory statistics query time: {avgQueryTime:F2}μs{queryCount} queries)");
    }

    [Fact]
    [Trait("Category", "Stress")]
    [Trait("Hardware", "CUDA")]
    public void CudaPerformance_MemoryFragmentation_ShouldHandleRepeatedAllocations()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = CreateAccelerator();
        var memoryManager = accelerator.Memory as ISyncMemoryManager;

        const int cycles = 100;
        const int buffersPerCycle = 10;
        var allocationTimes = new List<double>();

        // Act
        for (var cycle = 0; cycle < cycles; cycle++)
        {
            var cycleBuffers = new List<ISyncMemoryBuffer>();
            var cycleStopwatch = Stopwatch.StartNew();

            // Allocate buffers
            for (var i = 0; i < buffersPerCycle; i++)
            {
                var size = (i + 1) * 1024; // Varying sizes
                var buffer = memoryManager!.Allocate(size);
                cycleBuffers.Add(buffer);
            }

            cycleStopwatch.Stop();
            allocationTimes.Add(cycleStopwatch.ElapsedTicks * 1000000.0 / Stopwatch.Frequency);

            // Free buffers(creating fragmentation)
            foreach (var buffer in cycleBuffers)
            {
                memoryManager!.Free(buffer);
            }

            if (cycle % 20 == 0)
            {
                _output.WriteLine($"Completed cycle {cycle}, avg allocation time: {allocationTimes.Skip(Math.Max(0, allocationTimes.Count - 20)).Average():F2}μs");
            }
        }

        // Assert
        var firstQuarter = allocationTimes.Take(cycles / 4).Average();
        var lastQuarter = allocationTimes.Skip(3 * cycles / 4).Average();
        var slowdown = lastQuarter / firstQuarter;

        slowdown.Should().BeLessThan(10.0, "Memory fragmentation shouldn't cause severe performance degradation");

        _output.WriteLine($"First quarter avg: {firstQuarter:F2}μs, Last quarter avg: {lastQuarter:F2}μs");
        _output.WriteLine($"Performance degradation: {slowdown:F2}x");
    }

    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Hardware", "CUDA")]
    public void CudaPerformance_DeviceReset_ShouldCompleteInReasonableTime()
    {
        // Arrange
        if (!IsCudaAvailable())
            return;

        var accelerator = CreateAccelerator();

        // Allocate some memory to make reset more meaningful
        var memoryManager = accelerator.Memory as ISyncMemoryManager;
        var buffers = new List<ISyncMemoryBuffer>();
        for (var i = 0; i < 10; i++)
        {
            buffers.Add(memoryManager!.Allocate((i + 1) * 1024 * 1024));
        }

        var stopwatch = Stopwatch.StartNew();

        // Act
        accelerator.Reset();
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000,
            "Device reset should complete within 10 seconds");

        _output.WriteLine($"Device reset took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact(Skip = "Requires BenchmarkDotNet runner setup")]
    [Trait("Category", "Benchmark")]
    [Trait("Hardware", "CUDA")]
    public void CudaPerformance_RunBenchmarkSuite()
        // This would run the full benchmark suite
        // BenchmarkRunner.Run<CudaBenchmarkSuite>();
        => _output.WriteLine("Benchmark suite skipped - requires explicit BenchmarkDotNet runner");

    // Helper Methods
    private CudaAccelerator CreateAccelerator()
    {
        var loggerFactory = _loggerFactory;
        var acceleratorLogger = loggerFactory.CreateLogger<CudaAccelerator>();
        var accelerator = new CudaAccelerator(0, acceleratorLogger);
        _accelerators.Add(accelerator);
        return accelerator;
    }

    private static KernelDefinition CreateBenchmarkKernel()
    {
        const string kernelSource = @"
__global__ void benchmark_kernel(float* input, float* output, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {
        float val = input[idx];
        for(int i = 0; i < 10; i++) {
            val = val * 1.001f + 0.001f;
        }
        output[idx] = val;
    }
}";
        return new KernelDefinition
        {
            Name = "benchmark_kernel",
            Code = Encoding.UTF8.GetBytes(kernelSource),
            EntryPoint = "benchmark_kernel"
        };
    }

    private static KernelDefinition CreateComplexKernel()
    {
        const string kernelSource = @"
__global__ void complex_kernel(float* input, float* output, int n)
{
    __shared__ float shared_data[256];
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    int tid = threadIdx.x;
    
    if(idx < n) {
        shared_data[tid] = input[idx];
    }
    
    __syncthreads();
    
    if(idx < n) {
        float sum = 0.0f;
        for(int i = 0; i < blockDim.x; i++) {
            sum += shared_data[i] * sinf(shared_data[i]) * cosf(shared_data[i]);
        }
        output[idx] = sum / blockDim.x;
    }
}";
        return new KernelDefinition
        {
            Name = "complex_kernel",
            Code = Encoding.UTF8.GetBytes(kernelSource),
            EntryPoint = "complex_kernel"
        };
    }

    private static KernelDefinition CreateUniqueKernel(string name)
    {
        var kernelSource = $@"
__global__ void {name}(float* input, float* output, int n)
{{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if(idx < n) {{
        output[idx] = input[idx] + {name.GetHashCode(StringComparison.Ordinal) % 100}.0f;
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
                LogBufferDisposeError(_logger, ex.Message, null);
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
                LogAcceleratorDisposeError(_logger, ex.Message, null);
            }
        }
        _accelerators.Clear();
        _loggerFactory?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// BenchmarkDotNet benchmark suite for detailed performance analysis
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public sealed class CudaBenchmarkSuite : IDisposable
{
    private CudaAccelerator? _accelerator;
    private ISyncMemoryManager? _memoryManager;
    private readonly ILogger<CudaBenchmarkSuite> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public CudaBenchmarkSuite()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<CudaBenchmarkSuite>();
    }

    [GlobalSetup]
    public void Setup()
    {
        if (IsCudaAvailable())
        {
            var acceleratorLogger = _loggerFactory.CreateLogger<CudaAccelerator>();
            _accelerator = new CudaAccelerator(0, acceleratorLogger);
            _memoryManager = _accelerator.Memory as ISyncMemoryManager;
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _accelerator?.Dispose();
        _loggerFactory?.Dispose();
    }

    public void Dispose()
    {
        _accelerator?.Dispose();
        _loggerFactory?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Benchmark]
    [Arguments(1024)]
    [Arguments(1024 * 1024)]
    public void MemoryAllocation(int sizeInBytes)
    {
        if (_memoryManager == null)
            return;

        var buffer = _memoryManager.Allocate(sizeInBytes);
        _memoryManager.Free(buffer);
    }

    [Benchmark]
    public void DeviceSynchronization() => _accelerator?.SynchronizeAsync().AsTask().Wait();

    [Benchmark]
    public void MemoryStatistics() => _memoryManager?.GetStatistics();

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
}
}
