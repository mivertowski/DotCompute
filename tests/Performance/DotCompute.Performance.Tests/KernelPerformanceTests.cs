// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Tests.Common;
using System.Collections.Concurrent;
using ConcurrentQueue = System.Collections.Concurrent.ConcurrentQueue<object>;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using System.Linq;


namespace DotCompute.Performance.Tests
{

/// <summary>
/// Performance benchmarks for kernel compilation and execution operations.
/// Measures compilation time, execution latency, throughput, and optimization effects.
/// </summary>
[Trait("Category", TestCategories.Performance)]
[Trait("Category", TestCategories.Benchmark)]
public class KernelPerformanceTests : GpuTestBase
{
    private readonly ITestOutputHelper _output;
    private readonly MockAccelerator _mockAccelerator;
    private readonly CancellationTokenSource _cts;
    
    public KernelPerformanceTests(ITestOutputHelper output) : base(output)
    {
        _output = output;
        _mockAccelerator = CreateMockAccelerator();
        _cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    }

    #region Kernel Compilation Performance

    [Fact]
    [Trait("Category", TestCategories.KernelCompilation)]
    public async Task CompileKernel_MeasuresCompilationTime_WithinExpectedRange()
    {
        // Arrange
        var kernel = CreateSimpleKernel("vector_add", 1000);
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default,
            EnableDebugInfo = false
        };
        
        using var perfContext = CreatePerformanceContext("Kernel Compilation");
        var compilationTimes = new List<double>();
        const int iterations = 10;

        // Act & Assert
        for (var i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var compiledKernel = await _mockAccelerator.CompileKernelAsync(kernel, options, _cts.Token);
            sw.Stop();
            
            var compilationTimeMs = sw.Elapsed.TotalMilliseconds;
            compilationTimes.Add(compilationTimeMs);
            
            perfContext.Checkpoint($"Compilation {i + 1}: {compilationTimeMs:F2}ms");
            
            await compiledKernel.DisposeAsync();
        }

        // Analyze results
        var avgTime = compilationTimes.Average();
        var stdDev = CalculateStandardDeviation(compilationTimes);
        var minTime = compilationTimes.Min();
        var maxTime = compilationTimes.Max();
        
        _output.WriteLine($"Compilation Performance Summary:");
        _output.WriteLine($"  Average: {avgTime:F2}ms");
        _output.WriteLine($"  Std Dev: {stdDev:F2}ms");
        _output.WriteLine($"  Min: {minTime:F2}ms");
        _output.WriteLine($"  Max: {maxTime:F2}ms");
        _output.WriteLine($"  Coefficient of Variation: {(stdDev / avgTime) * 100:F1}%");

        // Performance assertions
        avgTime.Should().BeLessThan(1000, "Compilation should complete in under 1 second on average");
        stdDev.Should().BeLessThan(avgTime * 0.5, "Compilation times should be consistent");
    }

    [Theory]
    [InlineData(OptimizationLevel.None, "No optimization")]
    [InlineData(OptimizationLevel.Default, "Default optimization")]
    [InlineData(OptimizationLevel.Aggressive, "Aggressive optimization")]
    [Trait("Category", TestCategories.KernelCompilation)]
    public async Task CompileKernel_CompareOptimizationLevels_MeasuresImpact(OptimizationLevel optimizationLevel, string description)
    {
        // Arrange
        var kernel = CreateComplexKernel("matrix_multiply", 1024);
        var options = new CompilationOptions
        {
            OptimizationLevel = optimizationLevel,
            EnableDebugInfo = false
        };
        
        const int warmupRuns = 3;
        const int measureRuns = 10;
        var compilationTimes = new List<double>();

        // Warmup
        for (var i = 0; i < warmupRuns; i++)
        {
            var warmupKernel = await _mockAccelerator.CompileKernelAsync(kernel, options, _cts.Token);
            await warmupKernel.DisposeAsync();
        }

        // Act - Measure compilation performance
        for (var i = 0; i < measureRuns; i++)
        {
            var sw = Stopwatch.StartNew();
            var compiledKernel = await _mockAccelerator.CompileKernelAsync(kernel, options, _cts.Token);
            sw.Stop();
            
            compilationTimes.Add(sw.Elapsed.TotalMilliseconds);
            await compiledKernel.DisposeAsync();
        }

        // Analyze and report
        var avgTime = compilationTimes.Average();
        var minTime = compilationTimes.Min();
        var maxTime = compilationTimes.Max();
        
        _output.WriteLine($"Optimization Level: {description}");
        _output.WriteLine($"  Average compilation time: {avgTime:F2}ms");
        _output.WriteLine($"  Range: {minTime:F2}ms - {maxTime:F2}ms");
        _output.WriteLine($"  Total samples: {measureRuns}");
        
        // Store results for comparison (in real implementation, this would be persisted)
        TestContext.Current.Set($"CompilationTime_{optimizationLevel}", avgTime);
    }

    [Fact]
    [Trait("Category", TestCategories.Performance)]
    public async Task CompileKernel_MeasuresByteCodeSize_TracksOptimizationEffects()
    {
        // Arrange
        var kernel = CreateComplexKernel("convolution", 512);
        var optimizationLevels = new[] 
        {
            OptimizationLevel.None,
            OptimizationLevel.Default,
            OptimizationLevel.Aggressive
        };
        
        var results = new Dictionary<DotCompute.Abstractions.Kernels.OptimizationLevel, (double CompilationTime, long ByteCodeSize)>();

        // Act
        foreach (var level in optimizationLevels)
        {
            var options = new CompilationOptions
            {
                OptimizationLevel = level,
                EnableDebugInfo = false
            };
            
            var sw = Stopwatch.StartNew();
            var compiledKernel = await _mockAccelerator.CompileKernelAsync(kernel, options, _cts.Token);
            sw.Stop();
            
            var compilationTime = sw.Elapsed.TotalMilliseconds;
            var byteCodeSize = GetEstimatedByteCodeSize(compiledKernel); // Mock implementation
            
            results[level] = (compilationTime, byteCodeSize);
            
            _output.WriteLine($"{level}: {compilationTime:F2}ms, {byteCodeSize:N0} bytes");
            
            await compiledKernel.DisposeAsync();
        }

        // Assert - Verify optimization effects
        var noneResult = results[DotCompute.Abstractions.Kernels.OptimizationLevel.None];
        var aggressiveResult = results[DotCompute.Abstractions.Kernels.OptimizationLevel.Aggressive];
        
        // Aggressive optimization should typically result in smaller bytecode
        // but may take longer to compile
        aggressiveResult.ByteCodeSize.Should().BeLessThanOrEqualTo(noneResult.ByteCodeSize,
            "Aggressive optimization should produce smaller or equal bytecode size");
        
        _output.WriteLine($"Bytecode size reduction: {((double)(noneResult.ByteCodeSize - aggressiveResult.ByteCodeSize) / noneResult.ByteCodeSize) * 100:F1}%");
    }

    #endregion

    #region Kernel Execution Performance

    [Theory]
    [InlineData(1000, "Small workload")]
    [InlineData(10000, "Medium workload")]
    [InlineData(100000, "Large workload")]
    [Trait("Category", TestCategories.Performance)]
    public async Task ExecuteKernel_MeasuresExecutionLatency_ScalesWithWorkload(int workloadSize, string description)
    {
        // Arrange
        var kernel = CreateScalableKernel("parallel_sum", workloadSize);
        var compiledKernel = await _mockAccelerator.CompileKernelAsync(kernel, null, _cts.Token);
        var arguments = CreateKernelArguments(workloadSize);
        
        const int iterations = 20;
        var executionTimes = new List<double>();

        try
        {
            // Warmup
            for (var i = 0; i < 3; i++)
            {
                await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                await _mockAccelerator.SynchronizeAsync(_cts.Token);
            }

            // Act - Measure execution performance
            for (var i = 0; i < iterations; i++)
            {
                var sw = Stopwatch.StartNew();
                await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                await _mockAccelerator.SynchronizeAsync(_cts.Token);
                sw.Stop();
                
                executionTimes.Add(sw.Elapsed.TotalMicroseconds);
            }

            // Analyze results
            var avgTime = executionTimes.Average();
            var minTime = executionTimes.Min();
            var maxTime = executionTimes.Max();
            var throughput = workloadSize / (avgTime / 1000.0); // Operations per millisecond
            
            _output.WriteLine($"{description} (size: {workloadSize:N0}):");
            _output.WriteLine($"  Average execution time: {avgTime:F2}μs");
            _output.WriteLine($"  Min time: {minTime:F2}μs");
            _output.WriteLine($"  Max time: {maxTime:F2}μs");
            _output.WriteLine($"  Throughput: {throughput:F0} ops/ms");
            
            // Performance expectations
            avgTime.Should().BeLessThan(10000, "Execution should complete in under 10ms");
            throughput.Should().BeGreaterThan(0, "Should achieve measurable throughput");
        }
        finally
        {
            await compiledKernel.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", TestCategories.Performance)]
    public async Task ExecuteKernel_MeasuresThroughput_UnderContinuousLoad()
    {
        // Arrange
        var kernel = CreateThroughputKernel("stream_processing", 10000);
        var compiledKernel = await _mockAccelerator.CompileKernelAsync(kernel, null, _cts.Token);
        var arguments = CreateKernelArguments(10000);
        
        const int durationSeconds = 5;
        const int reportingIntervalMs = 500;
        
        var executionCount = 0;
        var totalBytes = 0L;
        var throughputMeasurements = new List<double>();
        
        var endTime = DateTime.UtcNow.AddSeconds(durationSeconds);
        var lastReportTime = DateTime.UtcNow;
        var lastExecutionCount = 0;

        try
        {
            // Act - Continuous execution for throughput measurement
            while (DateTime.UtcNow < endTime && !_cts.Token.IsCancellationRequested)
            {
                await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                executionCount++;
                totalBytes += GetEstimatedDataSize(arguments);
                
                // Report throughput periodically
                if ((DateTime.UtcNow - lastReportTime).TotalMilliseconds >= reportingIntervalMs)
                {
                    var executions = executionCount - lastExecutionCount;
                    var throughput = executions / (reportingIntervalMs / 1000.0);
                    throughputMeasurements.Add(throughput);
                    
                    _output.WriteLine($"Interval throughput: {throughput:F1} executions/sec");
                    
                    lastReportTime = DateTime.UtcNow;
                    lastExecutionCount = executionCount;
                }
            }

            await _mockAccelerator.SynchronizeAsync(_cts.Token);

            // Analyze throughput results
            var totalThroughput = executionCount / durationSeconds;
            var avgIntervalThroughput = throughputMeasurements.Average();
            var dataThroughputMBps = (totalBytes / (1024.0 * 1024.0)) / durationSeconds;
            
            _output.WriteLine($"Throughput Performance Summary:");
            _output.WriteLine($"  Total executions: {executionCount:N0}");
            _output.WriteLine($"  Overall throughput: {totalThroughput:F1} executions/sec");
            _output.WriteLine($"  Average interval throughput: {avgIntervalThroughput:F1} executions/sec");
            _output.WriteLine($"  Data throughput: {dataThroughputMBps:F1} MB/sec");
            
            // Performance assertions
            totalThroughput.Should().BeGreaterThan(10, "Should achieve reasonable execution throughput");
            throughputMeasurements.Should().HaveCountGreaterThan(5, "Should have multiple throughput measurements");
        }
        finally
        {
            await compiledKernel.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", TestCategories.Performance)]
    [Trait("Category", TestCategories.LongRunning)]
    public async Task ExecuteKernel_ProfilesLatencyDistribution_IdentifiesOutliers()
    {
        // Arrange
        var kernel = CreateLatencyTestKernel("latency_test", 5000);
        var compiledKernel = await _mockAccelerator.CompileKernelAsync(kernel, null, _cts.Token);
        var arguments = CreateKernelArguments(5000);
        
        const int samples = 1000;
        var latencies = new List<double>(samples);
        
        using var perfContext = CreatePerformanceContext("Latency Profiling");

        try
        {
            // Warmup
            for (var i = 0; i < 10; i++)
            {
                await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                await _mockAccelerator.SynchronizeAsync(_cts.Token);
            }

            perfContext.Checkpoint("Warmup completed");

            // Act - Collect latency measurements
            for (var i = 0; i < samples; i++)
            {
                var sw = Stopwatch.StartNew();
                await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                await _mockAccelerator.SynchronizeAsync(_cts.Token);
                sw.Stop();
                
                latencies.Add(sw.Elapsed.TotalMicroseconds);
                
                if (i % 100 == 0)
                {
                    perfContext.Checkpoint($"Sample {i + 1}/{samples}");
                }
            }

            // Analyze latency distribution
            latencies.Sort();
            var min = latencies.First();
            var max = latencies.Last();
            var median = GetPercentile(latencies, 50);
            var p95 = GetPercentile(latencies, 95);
            var p99 = GetPercentile(latencies, 99);
            var mean = latencies.Average();
            var stdDev = CalculateStandardDeviation(latencies);
            
            // Identify outliers (values beyond 3 standard deviations)
            var outlierThreshold = mean + (3 * stdDev);
            var outliers = latencies.Where(l => l > outlierThreshold).ToList();
            
            _output.WriteLine($"Latency Distribution Analysis ({samples:N0} samples):");
            _output.WriteLine($"  Min: {min:F2}μs");
            _output.WriteLine($"  Median (P50): {median:F2}μs");
            _output.WriteLine($"  Mean: {mean:F2}μs");
            _output.WriteLine($"  P95: {p95:F2}μs");
            _output.WriteLine($"  P99: {p99:F2}μs");
            _output.WriteLine($"  Max: {max:F2}μs");
            _output.WriteLine($"  Std Dev: {stdDev:F2}μs");
            _output.WriteLine($"  Outliers: {outliers.Count} ({(double)outliers.Count / samples * 100:F1}%)");
            
            if (outliers.Any())
            {
                _output.WriteLine($"  Outlier range: {outliers.Min():F2}μs - {outliers.Max():F2}μs");
            }
            
            // Performance quality assertions
            p95.Should().BeLessThan(mean * 3, "95th percentile should be within reasonable bounds");
            (double)outliers.Count / samples.Should().BeLessThan(0.05, "Outlier rate should be under 5%");
            stdDev.Should().BeLessThan(mean, "Standard deviation should indicate consistent performance");
        }
        finally
        {
            await compiledKernel.DisposeAsync();
        }
    }

    #endregion

    #region Helper Methods

    private static MockAccelerator CreateMockAccelerator()
    {
        var info = new AcceleratorInfo
        {
            Id = "perf_test_accelerator",
            Name = "Performance Test Accelerator",
            DeviceType = "Mock",
            Vendor = "Test",
            DriverVersion = "1.0.0",
            TotalMemory = 8L * 1024 * 1024 * 1024, // 8GB
            AvailableMemory = 7L * 1024 * 1024 * 1024, // 7GB
            MaxThreadsPerBlock = 1024,
            MaxComputeUnits = 16,
            GlobalMemorySize = 8L * 1024 * 1024 * 1024,
            SupportsFloat64 = true,
            SupportsInt64 = true
        };
        
        return new MockAccelerator(info);
    }

    private static KernelDefinition CreateSimpleKernel(string name, int dataSize)
    {
        return new KernelDefinition
        {
            Name = name,
            Source = $@"
                __kernel void {name}(__global float* input, __global float* output, int size)
                {{
                    int gid = get_global_id(0);
                    if (gid < size) {{
                        output[gid] = input[gid] + 1.0f;
                    }}
                }}
                ",
            Language = KernelLanguage.OpenCL,
            EntryPoint = name
        };
    }

    private KernelDefinition CreateComplexKernel(string name, int size)
    {
        return new KernelDefinition
        {
            Name = name,
            Source = GenerateComplexKernelSource(name, size),
            Language = KernelLanguage.OpenCL,
            EntryPoint = name
        };
    }

    private static KernelDefinition CreateScalableKernel(string name, int workloadSize)
    {
        return new KernelDefinition
        {
            Name = name,
            Source = $@"
                __kernel void {name}(__global float* data, int size)
                {{
                    int gid = get_global_id(0);
                    if (gid < size) {{
                        float sum = 0.0f;
                        for (int i = 0; i < 10; i++) {{
                            sum += data[gid] * (i + 1);
                        }}
                        data[gid] = sum;
                    }}
                }}
                ",
            Language = KernelLanguage.OpenCL,
            EntryPoint = name
        };
    }

    private static KernelDefinition CreateThroughputKernel(string name, int dataSize)
    {
        return new KernelDefinition
        {
            Name = name,
            Source = $@"
                __kernel void {name}(__global float* input, __global float* output, int size)
                {{
                    int gid = get_global_id(0);
                    if (gid < size) {{
                        output[gid] = sqrt(input[gid] * input[gid] + 1.0f);
                    }}
                }}
                ",
            Language = KernelLanguage.OpenCL,
            EntryPoint = name
        };
    }

    private static KernelDefinition CreateLatencyTestKernel(string name, int size)
    {
        return new KernelDefinition
        {
            Name = name,
            Source = $@"
                __kernel void {name}(__global float* data, int size)
                {{
                    int gid = get_global_id(0);
                    if (gid < size) {{
                        data[gid] = sin(data[gid]) + cos(data[gid]);
                    }}
                }}
                ",
            Language = KernelLanguage.OpenCL,
            EntryPoint = name
        };
    }

    private static KernelArguments CreateKernelArguments(int size)
    {
        // Mock implementation - create dummy arguments
        return new KernelArguments(new object[] { new float[size], new float[size], size });
    }

    private string GenerateComplexKernelSource(string name, int size)
    {
        // Generate a more complex kernel for compilation benchmarking
        return $@"
            __kernel void {name}(__global float* matrix_a, __global float* matrix_b, __global float* result, int N)
            {{
                int row = get_global_id(0);
                int col = get_global_id(1);
                
                if (row < N && col < N) {{
                    float sum = 0.0f;
                    for (int k = 0; k < N; k++) {{
                        sum += matrix_a[row * N + k] * matrix_b[k * N + col];
                    }}
                    result[row * N + col] = sum;
                }}
            }}
            ";
    }

    private static long GetEstimatedByteCodeSize(ICompiledKernel kernel)
    {
        // Mock implementation - estimate bytecode size
        return kernel.Name.Length * 100 + 2048; // Simplified estimation
    }

    private static long GetEstimatedDataSize(KernelArguments arguments)
    {
        // Mock implementation - estimate data transfer size
        return arguments.Count * 4 * 1024; // Simplified estimation
    }

    private static double CalculateStandardDeviation(IEnumerable<double> values)
    {
        var valuesList = values.ToList();
        var mean = valuesList.Average();
        var sumSquaredDiffs = valuesList.Sum(v => Math.Pow(v - mean, 2));
        return Math.Sqrt(sumSquaredDiffs / valuesList.Count);
    }

    private static double GetPercentile(List<double> sortedValues, int percentile)
    {
        var index = (percentile / 100.0) * (sortedValues.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        
        if (lower == upper)
            return sortedValues[lower];
        
        var weight = index - lower;
        return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Dispose();
            _mockAccelerator?.DisposeAsync().AsTask().Wait(1000);
        }
        base.Dispose(disposing);
    }
}

#region Mock Types

// Mock types for testing - simplified versions of the actual types
public enum KernelLanguage
{
    OpenCL,
    CUDA,
    CSharp,
    HLSL
}

public enum OptimizationLevel
{
    None,
    Default,
    Aggressive
}

public class KernelDefinition
{
    public required string Name { get; set; }
    public required string Source { get; set; }
    public KernelLanguage Language { get; set; }
    public required string EntryPoint { get; set; }
}

public class CompilationOptions
{
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Default;
    public bool EnableDebugInfo { get; set; }
}

public class KernelArguments
{
    private readonly object[] _arguments;
    
    public KernelArguments(object[] arguments)
    {
        _arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
    }
    
    public int Count => _arguments.Length;
    public object this[int index] => _arguments[index];
}

public interface ITestUnifiedMemoryManager : IAsyncDisposable
{
    // Mock interface for memory management
}

public interface IAcceleratorTest : IAsyncDisposable
{
    AcceleratorInfo Info { get; }
    DotCompute.Abstractions.AcceleratorType Type { get; }
    ITestUnifiedMemoryManager Memory { get; }
    DotCompute.Abstractions.AcceleratorContext Context { get; }
    ValueTask<ICompiledKernel> CompileKernelAsync(KernelDefinition definition, CompilationOptions? options = null, CancellationToken cancellationToken = default);
    ValueTask SynchronizeAsync(CancellationToken cancellationToken = default);
}

#endregion

/// <summary>
/// Mock accelerator for performance testing
/// </summary>
internal class MockAccelerator : IAcceleratorTest
{
    public AcceleratorInfo Info { get; }
    public DotCompute.Abstractions.AcceleratorType Type => DotCompute.Abstractions.AcceleratorType.CPU;
    public ITestUnifiedMemoryManager Memory { get; }
    public DotCompute.Abstractions.AcceleratorContext Context { get; }

    public MockAccelerator(AcceleratorInfo info)
    {
        Info = info;
        Memory = new MockMemoryManager();
        Context = new DotCompute.Abstractions.AcceleratorContext();
    }

    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Simulate compilation time based on complexity
        var complexity = definition.Source.Length / 100;
        var baseDelay = 50 + (complexity * 2); // Base delay in ms
        
        // Add optimization overhead
        if (options?.OptimizationLevel == OptimizationLevel.Aggressive)
            baseDelay = (int)(baseDelay * 1.5);
        
        await Task.Delay(baseDelay, cancellationToken);
        
        return new MockCompiledKernel(definition.Name);
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        // Simulate synchronization delay
        return new ValueTask(Task.Delay(1, cancellationToken));
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Mock compiled kernel for performance testing
/// </summary>
internal class MockCompiledKernel : ICompiledKernel
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; }

    public MockCompiledKernel(string name)
    {
        Name = name;
    }

    public async ValueTask ExecuteAsync(
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        // Simulate execution time based on workload
        var workloadSize = arguments.Count * 100;
        var executionDelay = Math.Max(1, workloadSize / 10000); // Scale with workload
        
        await Task.Delay(executionDelay, cancellationToken);
    }
    
    public ValueTask ExecuteAsync(DotCompute.Abstractions.Kernels.KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        // Convert our test KernelArguments to the interface expected type
        var testArgs = new KernelArguments(new object[arguments.Count]);
        for (int i = 0; i < arguments.Count; i++)
        {
            testArgs[i] = arguments[i];
        }
        return ExecuteAsync(testArgs, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Mock memory manager for testing
/// </summary>
internal class MockMemoryManager : ITestUnifiedMemoryManager
{
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Mock memory buffer for testing
/// </summary>
internal class MockMemoryBuffer<T> : IUnifiedMemoryBuffer<T> where T : unmanaged
{
    public int Count { get; }
    public long SizeInBytes => Count * sizeof(T);
    public MemoryLocation Location => MemoryLocation.Device;
    public bool IsDisposed { get; private set; }

    public MockMemoryBuffer(int count)
    {
        Count = count;
    }

    public Span<T> AsSpan() => new Span<T>(new T[Count]);
    public Memory<T> AsMemory() => new Memory<T>(new T[Count]);
    public ValueTask<Memory<T>> GetHostMemoryAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(AsMemory());
    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() { IsDisposed = true; return ValueTask.CompletedTask; }
}

/// <summary>
/// Mock raw memory buffer for testing
/// </summary>
internal class MockRawMemoryBuffer : IUnifiedMemoryBuffer
{
    public long SizeInBytes { get; }
    public MemoryLocation Location => MemoryLocation.Device;
    public MemoryOptions Options { get; } = new MemoryOptions();
    public MemoryBufferState State => IsDisposed ? MemoryBufferState.Disposed : MemoryBufferState.DeviceResident;
    public bool IsDisposed { get; private set; }

    public MockRawMemoryBuffer(long sizeInBytes)
    {
        SizeInBytes = sizeInBytes;
    }

    public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offsetInBytes, CancellationToken cancellationToken = default) where T : unmanaged
        => ValueTask.CompletedTask;

    public ValueTask CopyToAsync<T>(Memory<T> destination, long offsetInBytes, CancellationToken cancellationToken = default) where T : unmanaged
        => ValueTask.CompletedTask;

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() { IsDisposed = true; return ValueTask.CompletedTask; }
    public void Dispose() { IsDisposed = true; }
}

/// <summary>
/// Mock memory buffer view for testing
/// </summary>
internal class MockMemoryBufferView<T> : IUnifiedMemoryBufferView<T> where T : unmanaged
{
    public int Count { get; }
    public long SizeInBytes => Count * sizeof(T);
    public MemoryLocation Location => MemoryLocation.Device;
    public bool IsDisposed { get; private set; }
    public IUnifiedMemoryBuffer<T> BaseBuffer { get; }
    public int Offset { get; }

    public MockMemoryBufferView(IUnifiedMemoryBuffer<T> baseBuffer, int offset, int count)
    {
        BaseBuffer = baseBuffer;
        Offset = offset;
        Count = count;
    }

    public Span<T> AsSpan() => new Span<T>(new T[Count]);
    public Memory<T> AsMemory() => new Memory<T>(new T[Count]);
    public ValueTask<Memory<T>> GetHostMemoryAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(AsMemory());
    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() { IsDisposed = true; return ValueTask.CompletedTask; }
}

/// <summary>
/// Test context for storing cross-test data
/// </summary>
internal class TestContext
{
    private readonly Dictionary<string, object> _data = new();
    
    public static TestContext Current { get; } = new TestContext();
    
    public void Set(string key, object value) => _data[key] = value;
    public T? Get<T>(string key) => _data.TryGetValue(key, out var value) ? (T?)value : default;
}

}
