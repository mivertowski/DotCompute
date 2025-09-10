// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Benchmarks;

/// <summary>
/// Simplified LINQ benchmarks focusing on available DotCompute functionality.
/// Tests basic LINQ operations vs accelerated compute operations.
/// </summary>
[Config(typeof(SimplifiedLinqConfig))]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class SimplifiedLinqBenchmark
{
    private IAcceleratorService _acceleratorService = null!;
    private IAccelerator _cpuAccelerator = null!;
    private IAccelerator? _gpuAccelerator;
    
    // Test datasets
    private int[] _intData = null!;
    private float[] _floatData = null!;
    private double[] _doubleData = null!;
    
    [Params(1000, 10000, 100000)]
    public int DataSize { get; set; }
    
    [Params(OperationType.Select, OperationType.Where, OperationType.Aggregate)]
    public OperationType Operation { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Setup DotCompute services
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDotComputeRuntime();
        
        var serviceProvider = services.BuildServiceProvider();
        _acceleratorService = serviceProvider.GetRequiredService<IAcceleratorService>();
        
        // Get available accelerators
        var accelerators = _acceleratorService.GetAccelerators().ToList();
        _cpuAccelerator = accelerators.First(a => a.Info.DeviceType == DeviceType.CPU);
        _gpuAccelerator = accelerators.FirstOrDefault(a => a.Info.DeviceType == DeviceType.CUDA);
        
        SetupTestData();
    }
    
    private void SetupTestData()
    {
        var random = new Random(42); // Deterministic seed
        
        _intData = Enumerable.Range(0, DataSize)
            .Select(_ => random.Next(1, 10000))
            .ToArray();
            
        _floatData = Enumerable.Range(0, DataSize)
            .Select(_ => (float)random.NextDouble() * 10000f)
            .ToArray();
            
        _doubleData = Enumerable.Range(0, DataSize)
            .Select(_ => random.NextDouble() * 10000.0)
            .ToArray();
    }

    #region Standard LINQ Baselines
    
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("StandardLinq")]
    public int[] StandardLinq_IntOperations()
    {
        return Operation switch
        {
            OperationType.Select => _intData.Select(x => x * 2 + 1).ToArray(),
            OperationType.Where => _intData.Where(x => x > 5000).ToArray(),
            OperationType.Aggregate => new[] { _intData.Where(x => x > 1000).Sum() },
            _ => _intData.Select(x => x * 2).ToArray()
        };
    }
    
    [Benchmark]
    [BenchmarkCategory("StandardLinq")]
    public float[] StandardLinq_FloatOperations()
    {
        return Operation switch
        {
            OperationType.Select => _floatData.Select(x => x * 2.5f + 1.0f).ToArray(),
            OperationType.Where => _floatData.Where(x => x > 5000f).ToArray(),
            OperationType.Aggregate => new[] { _floatData.Where(x => x > 1000f).Sum() },
            _ => _floatData.Select(x => x * 2.5f).ToArray()
        };
    }
    
    [Benchmark]
    [BenchmarkCategory("StandardLinq")]
    public double[] StandardLinq_DoubleOperations()
    {
        return Operation switch
        {
            OperationType.Select => _doubleData.Select(x => x * 2.5 + 1.0).ToArray(),
            OperationType.Where => _doubleData.Where(x => x > 5000.0).ToArray(),
            OperationType.Aggregate => new[] { _doubleData.Where(x => x > 1000.0).Average() },
            _ => _doubleData.Select(x => x * 2.5).ToArray()
        };
    }
    
    #endregion

    #region Manual SIMD/Vector Operations (CPU Acceleration)
    
    [Benchmark]
    [BenchmarkCategory("VectorizedLinq")]
    public int[] Vectorized_IntOperations()
    {
        // Simulate vectorized operations using CPU accelerator capabilities
        return SimulateAcceleratedOperation(_intData, Operation);
    }
    
    [Benchmark]
    [BenchmarkCategory("VectorizedLinq")]
    public float[] Vectorized_FloatOperations()
    {
        // Simulate vectorized operations using CPU accelerator capabilities
        return SimulateAcceleratedOperation(_floatData, Operation);
    }
    
    [Benchmark]
    [BenchmarkCategory("VectorizedLinq")]
    public double[] Vectorized_DoubleOperations()
    {
        // Simulate vectorized operations using CPU accelerator capabilities
        return SimulateAcceleratedOperation(_doubleData, Operation);
    }
    
    #endregion

    #region Memory Efficiency Tests
    
    [Benchmark]
    [BenchmarkCategory("Memory")]
    public MemoryEfficiencyResult Memory_StandardLinq()
    {
        var baseline = GC.GetTotalMemory(true);
        var stopwatch = Stopwatch.StartNew();
        
        var result1 = _floatData.Select(x => x * 2.5f).ToArray();
        var result2 = result1.Where(x => x > 1000f).ToArray();
        var result3 = result2.Select(x => x + 1.0f).ToArray();
        
        stopwatch.Stop();
        var peakMemory = GC.GetTotalMemory(false) - baseline;
        
        return new MemoryEfficiencyResult
        {
            ExecutionTime = stopwatch.Elapsed,
            MemoryUsed = peakMemory,
            ResultSize = result3.Length,
            InputSize = _floatData.Length
        };
    }
    
    [Benchmark]
    [BenchmarkCategory("Memory")]
    public MemoryEfficiencyResult Memory_OptimizedOperations()
    {
        var baseline = GC.GetTotalMemory(true);
        var stopwatch = Stopwatch.StartNew();
        
        // Simulate memory-optimized operations
        var result = SimulateMemoryOptimizedPipeline(_floatData);
        
        stopwatch.Stop();
        var peakMemory = GC.GetTotalMemory(false) - baseline;
        
        return new MemoryEfficiencyResult
        {
            ExecutionTime = stopwatch.Elapsed,
            MemoryUsed = peakMemory,
            ResultSize = result.Length,
            InputSize = _floatData.Length
        };
    }
    
    #endregion

    #region Performance Analysis
    
    [Benchmark]
    [BenchmarkCategory("Performance")]
    public PerformanceResult Throughput_StandardLinq()
    {
        var stopwatch = Stopwatch.StartNew();
        var iterations = 10;
        var totalProcessed = 0L;
        
        for (int i = 0; i < iterations; i++)
        {
            var result = _intData.Select(x => x * 2 + 1).Where(x => x > 1000).ToArray();
            totalProcessed += result.Length;
        }
        
        stopwatch.Stop();
        
        return new PerformanceResult
        {
            TotalTime = stopwatch.Elapsed,
            ItemsProcessed = totalProcessed,
            ThroughputOpsPerSec = totalProcessed / stopwatch.Elapsed.TotalSeconds,
            AverageLatency = TimeSpan.FromTicks(stopwatch.Elapsed.Ticks / iterations)
        };
    }
    
    [Benchmark]
    [BenchmarkCategory("Performance")]
    public PerformanceResult Throughput_AcceleratedOperations()
    {
        var stopwatch = Stopwatch.StartNew();
        var iterations = 10;
        var totalProcessed = 0L;
        
        for (int i = 0; i < iterations; i++)
        {
            var result = SimulateAcceleratedPipeline(_intData);
            totalProcessed += result.Length;
        }
        
        stopwatch.Stop();
        
        return new PerformanceResult
        {
            TotalTime = stopwatch.Elapsed,
            ItemsProcessed = totalProcessed,
            ThroughputOpsPerSec = totalProcessed / stopwatch.Elapsed.TotalSeconds,
            AverageLatency = TimeSpan.FromTicks(stopwatch.Elapsed.Ticks / iterations)
        };
    }
    
    #endregion

    #region Real-World Scenarios
    
    [Benchmark]
    [BenchmarkCategory("RealWorld")]
    public double StandardLinq_DataAnalysis()
    {
        // Simulate data analysis workload
        var normalized = _doubleData.Select(x => (x - _doubleData.Average()) / _doubleData.StandardDeviation()).ToArray();
        var filtered = normalized.Where(x => Math.Abs(x) < 2.0).ToArray();
        var transformed = filtered.Select(x => Math.Tanh(x)).ToArray();
        
        return transformed.Average();
    }
    
    [Benchmark]
    [BenchmarkCategory("RealWorld")]
    public double Accelerated_DataAnalysis()
    {
        // Simulate accelerated data analysis
        return SimulateAcceleratedDataAnalysis(_doubleData);
    }
    
    #endregion

    #region Helper Methods
    
    private T[] SimulateAcceleratedOperation<T>(T[] data, OperationType operation) where T : struct, IComparable<T>
    {
        // Simulate accelerated operations with better performance characteristics
        // In a real implementation, this would use the actual accelerator
        
        switch (operation)
        {
            case OperationType.Select:
                return SimulateVectorizedSelect(data);
            case OperationType.Where:
                return SimulateVectorizedWhere(data);
            case OperationType.Aggregate:
                return new[] { SimulateVectorizedAggregate(data) };
            default:
                return SimulateVectorizedSelect(data);
        }
    }
    
    private T[] SimulateVectorizedSelect<T>(T[] data) where T : struct
    {
        // Simulate SIMD-accelerated selection with reduced overhead
        var result = new T[data.Length];
        
        // Simulate vectorized processing (in real implementation, would use SIMD intrinsics)
        Parallel.For(0, data.Length, i =>
        {
            if (typeof(T) == typeof(int))
            {
                var value = (int)(object)data[i];
                result[i] = (T)(object)(value * 2 + 1);
            }
            else if (typeof(T) == typeof(float))
            {
                var value = (float)(object)data[i];
                result[i] = (T)(object)(value * 2.5f + 1.0f);
            }
            else if (typeof(T) == typeof(double))
            {
                var value = (double)(object)data[i];
                result[i] = (T)(object)(value * 2.5 + 1.0);
            }
        });
        
        return result;
    }
    
    private T[] SimulateVectorizedWhere<T>(T[] data) where T : struct, IComparable<T>
    {
        // Simulate vectorized filtering
        var threshold = CreateThreshold<T>();
        return data.AsParallel().Where(x => x.CompareTo(threshold) > 0).ToArray();
    }
    
    private T SimulateVectorizedAggregate<T>(T[] data) where T : struct
    {
        // Simulate vectorized aggregation
        if (typeof(T) == typeof(int))
        {
            var intData = data.Cast<int>().ToArray();
            var sum = intData.AsParallel().Where(x => x > 1000).Sum();
            return (T)(object)sum;
        }
        else if (typeof(T) == typeof(float))
        {
            var floatData = data.Cast<float>().ToArray();
            var sum = floatData.AsParallel().Where(x => x > 1000f).Sum();
            return (T)(object)sum;
        }
        else if (typeof(T) == typeof(double))
        {
            var doubleData = data.Cast<double>().ToArray();
            var avg = doubleData.AsParallel().Where(x => x > 1000.0).Average();
            return (T)(object)avg;
        }
        
        return default(T);
    }
    
    private T CreateThreshold<T>() where T : struct
    {
        if (typeof(T) == typeof(int))
            return (T)(object)5000;
        else if (typeof(T) == typeof(float))
            return (T)(object)5000f;
        else if (typeof(T) == typeof(double))
            return (T)(object)5000.0;
        
        return default(T);
    }
    
    private float[] SimulateMemoryOptimizedPipeline(float[] data)
    {
        // Simulate memory-optimized pipeline with reduced allocations
        var result = new List<float>(data.Length);
        
        // Process in chunks to reduce memory pressure
        const int chunkSize = 1000;
        for (int i = 0; i < data.Length; i += chunkSize)
        {
            var end = Math.Min(i + chunkSize, data.Length);
            var chunk = data.AsSpan(i, end - i);
            
            foreach (var value in chunk)
            {
                var transformed = value * 2.5f;
                if (transformed > 1000f)
                {
                    result.Add(transformed + 1.0f);
                }
            }
        }
        
        return result.ToArray();
    }
    
    private int[] SimulateAcceleratedPipeline(int[] data)
    {
        // Simulate accelerated pipeline processing
        return data.AsParallel()
            .Select(x => x * 2 + 1)
            .Where(x => x > 1000)
            .ToArray();
    }
    
    private double SimulateAcceleratedDataAnalysis(double[] data)
    {
        // Simulate GPU-accelerated data analysis
        var mean = data.AsParallel().Average();
        var variance = data.AsParallel().Select(x => Math.Pow(x - mean, 2)).Average();
        var stdDev = Math.Sqrt(variance);
        
        var normalized = data.AsParallel().Select(x => (x - mean) / stdDev).ToArray();
        var filtered = normalized.AsParallel().Where(x => Math.Abs(x) < 2.0).ToArray();
        var transformed = filtered.AsParallel().Select(x => Math.Tanh(x)).ToArray();
        
        return transformed.Average();
    }
    
    #endregion
}

#region Supporting Types

public enum OperationType
{
    Select,
    Where,
    Aggregate
}

public class MemoryEfficiencyResult
{
    public TimeSpan ExecutionTime { get; set; }
    public long MemoryUsed { get; set; }
    public int ResultSize { get; set; }
    public int InputSize { get; set; }
    
    public double MemoryEfficiencyRatio => (double)ResultSize / MemoryUsed;
    public double ProcessingRatio => (double)ResultSize / InputSize;
}

public class PerformanceResult
{
    public TimeSpan TotalTime { get; set; }
    public long ItemsProcessed { get; set; }
    public double ThroughputOpsPerSec { get; set; }
    public TimeSpan AverageLatency { get; set; }
}

public static class DoubleExtensions
{
    public static double StandardDeviation(this IEnumerable<double> values)
    {
        var mean = values.Average();
        var variance = values.Select(x => Math.Pow(x - mean, 2)).Average();
        return Math.Sqrt(variance);
    }
}

/// <summary>
/// Custom benchmark configuration for simplified LINQ tests.
/// </summary>
public class SimplifiedLinqConfig : ManualConfig
{
    public SimplifiedLinqConfig()
    {
        AddJob(Job.Default
            .WithRuntime(CoreRuntime.Core90)
            .WithJit(Jit.RyuJit)
            .WithPlatform(Platform.X64)
            .WithWarmupCount(3)
            .WithIterationCount(5)
            .WithInvocationCount(1)
            .WithStrategy(RunStrategy.Throughput));
            
        AddDiagnoser(MemoryDiagnoser.Default);
        AddDiagnoser(ThreadingDiagnoser.Default);
        
        AddColumn(StatisticColumn.Mean);
        AddColumn(StatisticColumn.StdDev);
        AddColumn(BaselineRatioColumn.RatioMean);
        AddColumn(RankColumn.Arabic);
        
        AddOrderer(DefaultOrderer.Instance);
        
        WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }
}

#endregion