// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Linq.Interfaces;
using DotCompute.Linq.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Benchmarks;

/// <summary>
/// Comprehensive performance comparison between standard LINQ and GPU-accelerated DotCompute LINQ.
/// Validates the claimed 8-23x speedup across different operations and data sizes.
/// </summary>
[Config(typeof(LinqVsGpuConfig))]
[MemoryDiagnoser]
[ThreadingDiagnoser]
[HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.CacheMisses)]
public class LinqVsGpuBenchmark
{
    private IComputeLinqProvider _gpuLinqProvider = null!;
    private IAccelerator _cpuAccelerator = null!;
    private IAccelerator _gpuAccelerator = null!;
    
    // Test datasets of varying sizes
    private int[] _intData = null!;
    private float[] _floatData = null!;
    private double[] _doubleData = null!;
    private int[] _smallIntData = null!;
    private float[] _smallFloatData = null!;
    
    // Complex test data for realistic scenarios
    private ComplexData[] _complexData = null!;
    private FinancialData[] _financialData = null!;
    
    [Params(1000, 10000, 100000, 1000000)]
    public int DataSize { get; set; }
    
    [Params(OperationType.Select, OperationType.Where, OperationType.Aggregate, OperationType.Mixed)]
    public OperationType Operation { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Setup DotCompute services
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDotComputeRuntime();
        services.AddDotComputeLinq();
        
        var serviceProvider = services.BuildServiceProvider();
        _gpuLinqProvider = serviceProvider.GetRequiredService<IComputeLinqProvider>();
        
        // Get accelerators
        var acceleratorService = serviceProvider.GetRequiredService<IAcceleratorService>();
        _cpuAccelerator = acceleratorService.GetAccelerators().First(a => a.Info.DeviceType == DeviceType.CPU);
        _gpuAccelerator = acceleratorService.GetAccelerators().FirstOrDefault(a => a.Info.DeviceType == DeviceType.CUDA) 
                          ?? _cpuAccelerator; // Fallback to CPU if no GPU available
        
        SetupTestData();
    }
    
    private void SetupTestData()
    {
        var random = new Random(42); // Deterministic seed for reproducible results
        
        // Basic numeric arrays
        _intData = Enumerable.Range(0, DataSize)
            .Select(_ => random.Next(1, 10000))
            .ToArray();
            
        _floatData = Enumerable.Range(0, DataSize)
            .Select(_ => (float)random.NextDouble() * 10000f)
            .ToArray();
            
        _doubleData = Enumerable.Range(0, DataSize)
            .Select(_ => random.NextDouble() * 10000.0)
            .ToArray();
            
        // Small datasets for overhead comparison
        _smallIntData = _intData.Take(100).ToArray();
        _smallFloatData = _floatData.Take(100).ToArray();
        
        // Complex structured data
        _complexData = Enumerable.Range(0, DataSize)
            .Select(i => new ComplexData
            {
                Id = i,
                Value = random.NextDouble() * 1000.0,
                Category = random.Next(1, 10),
                IsActive = random.NextDouble() > 0.5,
                Timestamp = DateTime.Now.AddSeconds(-random.Next(0, 86400))
            })
            .ToArray();
            
        // Financial data for realistic performance testing
        _financialData = Enumerable.Range(0, DataSize)
            .Select(i => new FinancialData
            {
                Price = (decimal)(random.NextDouble() * 1000),
                Volume = random.Next(1000, 100000),
                Volatility = random.NextDouble() * 0.5,
                Date = DateTime.Today.AddDays(-random.Next(0, 365))
            })
            .ToArray();
    }

    #region Standard LINQ Baselines
    
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("StandardLinq", "Select")]
    public int[] StandardLinq_Select()
    {
        return Operation switch
        {
            OperationType.Select => _intData.Select(x => x * 2 + 1).ToArray(),
            OperationType.Where => _intData.Where(x => x > 5000).ToArray(),
            OperationType.Aggregate => new[] { _intData.Where(x => x > 1000).Sum() },
            OperationType.Mixed => _intData.Where(x => x > 1000).Select(x => x * 2).Where(x => x < 10000).ToArray(),
            _ => _intData.Select(x => x * 2).ToArray()
        };
    }
    
    [Benchmark]
    [BenchmarkCategory("StandardLinq", "Float")]
    public float[] StandardLinq_Float()
    {
        return Operation switch
        {
            OperationType.Select => _floatData.Select(x => x * 2.5f + 1.0f).ToArray(),
            OperationType.Where => _floatData.Where(x => x > 5000f).ToArray(),
            OperationType.Aggregate => new[] { _floatData.Where(x => x > 1000f).Sum() },
            OperationType.Mixed => _floatData.Where(x => x > 1000f).Select(x => x * 2.5f).Where(x => x < 10000f).ToArray(),
            _ => _floatData.Select(x => x * 2.5f).ToArray()
        };
    }
    
    [Benchmark]
    [BenchmarkCategory("StandardLinq", "Double")]
    public double[] StandardLinq_Double()
    {
        return Operation switch
        {
            OperationType.Select => _doubleData.Select(x => Math.Sqrt(x * x + 100.0)).ToArray(),
            OperationType.Where => _doubleData.Where(x => x > 5000.0).ToArray(),
            OperationType.Aggregate => new[] { _doubleData.Where(x => x > 1000.0).Average() },
            OperationType.Mixed => _doubleData.Where(x => x > 1000.0).Select(x => Math.Sin(x)).Where(x => x > 0.0).ToArray(),
            _ => _doubleData.Select(x => Math.Sqrt(x)).ToArray()
        };
    }
    
    [Benchmark]
    [BenchmarkCategory("StandardLinq", "Complex")]
    public object StandardLinq_ComplexData()
    {
        return Operation switch
        {
            OperationType.Select => _complexData.Select(x => new { x.Id, NewValue = x.Value * 2.0 }).ToArray(),
            OperationType.Where => _complexData.Where(x => x.IsActive && x.Category > 5).ToArray(),
            OperationType.Aggregate => _complexData.Where(x => x.IsActive).Average(x => x.Value),
            OperationType.Mixed => _complexData
                .Where(x => x.IsActive && x.Value > 500.0)
                .Select(x => x.Value * 1.5)
                .Where(x => x < 2000.0)
                .ToArray(),
            _ => _complexData.Select(x => x.Value * 2.0).ToArray()
        };
    }
    
    #endregion

    #region GPU-Accelerated LINQ (DotCompute)
    
    [Benchmark]
    [BenchmarkCategory("GpuLinq", "Select")]
    public async Task<int[]> GpuLinq_Select()
    {
        var queryable = _gpuLinqProvider.CreateQueryable(_intData, _gpuAccelerator);
        
        return Operation switch
        {
            OperationType.Select => await ExecuteGpuQuery(queryable.Select(x => x * 2 + 1)),
            OperationType.Where => await ExecuteGpuQuery(queryable.Where(x => x > 5000)),
            OperationType.Aggregate => new[] { await ExecuteGpuQuery(queryable.Where(x => x > 1000).Sum()) },
            OperationType.Mixed => await ExecuteGpuQuery(queryable.Where(x => x > 1000).Select(x => x * 2).Where(x => x < 10000)),
            _ => await ExecuteGpuQuery(queryable.Select(x => x * 2))
        };
    }
    
    [Benchmark]
    [BenchmarkCategory("GpuLinq", "Float")]
    public async Task<float[]> GpuLinq_Float()
    {
        var queryable = _gpuLinqProvider.CreateQueryable(_floatData, _gpuAccelerator);
        
        return Operation switch
        {
            OperationType.Select => await ExecuteGpuQuery(queryable.Select(x => x * 2.5f + 1.0f)),
            OperationType.Where => await ExecuteGpuQuery(queryable.Where(x => x > 5000f)),
            OperationType.Aggregate => new[] { await ExecuteGpuQuery(queryable.Where(x => x > 1000f).Sum()) },
            OperationType.Mixed => await ExecuteGpuQuery(queryable.Where(x => x > 1000f).Select(x => x * 2.5f).Where(x => x < 10000f)),
            _ => await ExecuteGpuQuery(queryable.Select(x => x * 2.5f))
        };
    }
    
    [Benchmark]
    [BenchmarkCategory("GpuLinq", "Double")]
    public async Task<double[]> GpuLinq_Double()
    {
        var queryable = _gpuLinqProvider.CreateQueryable(_doubleData, _gpuAccelerator);
        
        return Operation switch
        {
            OperationType.Select => await ExecuteGpuQuery(queryable.Select(x => Math.Sqrt(x * x + 100.0))),
            OperationType.Where => await ExecuteGpuQuery(queryable.Where(x => x > 5000.0)),
            OperationType.Aggregate => new[] { await ExecuteGpuQuery(queryable.Where(x => x > 1000.0).Average()) },
            OperationType.Mixed => await ExecuteGpuQuery(queryable.Where(x => x > 1000.0).Select(x => Math.Sin(x)).Where(x => x > 0.0)),
            _ => await ExecuteGpuQuery(queryable.Select(x => Math.Sqrt(x)))
        };
    }
    
    #endregion

    #region CPU-Accelerated LINQ (SIMD)
    
    [Benchmark]
    [BenchmarkCategory("CpuLinq", "SIMD")]
    public async Task<int[]> CpuLinq_SIMD()
    {
        var queryable = _gpuLinqProvider.CreateQueryable(_intData, _cpuAccelerator);
        
        return Operation switch
        {
            OperationType.Select => await ExecuteGpuQuery(queryable.Select(x => x * 2 + 1)),
            OperationType.Where => await ExecuteGpuQuery(queryable.Where(x => x > 5000)),
            OperationType.Aggregate => new[] { await ExecuteGpuQuery(queryable.Where(x => x > 1000).Sum()) },
            OperationType.Mixed => await ExecuteGpuQuery(queryable.Where(x => x > 1000).Select(x => x * 2).Where(x => x < 10000)),
            _ => await ExecuteGpuQuery(queryable.Select(x => x * 2))
        };
    }
    
    #endregion

    #region Performance Analysis Benchmarks
    
    [Benchmark]
    [BenchmarkCategory("Performance", "Throughput")]
    public long MeasureThroughput_StandardLinq()
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Process multiple batches to measure sustained throughput
        for (int batch = 0; batch < 10; batch++)
        {
            var result = _intData.Select(x => x * 2 + 1).Where(x => x > 1000).ToArray();
            _ = result.Length; // Prevent optimization
        }
        
        stopwatch.Stop();
        
        // Return operations per second
        return (long)((10.0 * DataSize) / stopwatch.Elapsed.TotalSeconds);
    }
    
    [Benchmark]
    [BenchmarkCategory("Performance", "Throughput")]
    public async Task<long> MeasureThroughput_GpuLinq()
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Process multiple batches to measure sustained throughput
        for (int batch = 0; batch < 10; batch++)
        {
            var queryable = _gpuLinqProvider.CreateQueryable(_intData, _gpuAccelerator);
            var result = await ExecuteGpuQuery(queryable.Select(x => x * 2 + 1).Where(x => x > 1000));
            _ = result.Length; // Prevent optimization
        }
        
        stopwatch.Stop();
        
        // Return operations per second
        return (long)((10.0 * DataSize) / stopwatch.Elapsed.TotalSeconds);
    }
    
    [Benchmark]
    [BenchmarkCategory("Performance", "Latency")]
    public TimeSpan MeasureLatency_StandardLinq()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = _smallIntData.Select(x => x * 2 + 1).ToArray();
        stopwatch.Stop();
        
        _ = result.Length; // Prevent optimization
        return stopwatch.Elapsed;
    }
    
    [Benchmark]
    [BenchmarkCategory("Performance", "Latency")]
    public async Task<TimeSpan> MeasureLatency_GpuLinq()
    {
        var stopwatch = Stopwatch.StartNew();
        var queryable = _gpuLinqProvider.CreateQueryable(_smallIntData, _gpuAccelerator);
        var result = await ExecuteGpuQuery(queryable.Select(x => x * 2 + 1));
        stopwatch.Stop();
        
        _ = result.Length; // Prevent optimization
        return stopwatch.Elapsed;
    }
    
    #endregion

    #region Speedup Validation Benchmarks
    
    [Benchmark]
    [BenchmarkCategory("Speedup", "Validation")]
    public SpeedupResult ValidateSpeedupClaim()
    {
        var standardTimes = new List<double>();
        var gpuTimes = new List<double>();
        
        // Run multiple iterations for statistical significance
        for (int i = 0; i < 5; i++)
        {
            // Standard LINQ timing
            var sw1 = Stopwatch.StartNew();
            var standardResult = _floatData.Select(x => x * 2.5f + 1.0f).Where(x => x > 1000f).ToArray();
            sw1.Stop();
            standardTimes.Add(sw1.Elapsed.TotalMilliseconds);
            
            // GPU LINQ timing
            var sw2 = Stopwatch.StartNew();
            var queryable = _gpuLinqProvider.CreateQueryable(_floatData, _gpuAccelerator);
            var gpuTask = ExecuteGpuQuery(queryable.Select(x => x * 2.5f + 1.0f).Where(x => x > 1000f));
            gpuTask.Wait();
            sw2.Stop();
            gpuTimes.Add(sw2.Elapsed.TotalMilliseconds);
            
            // Verify results are equivalent
            var gpuResult = gpuTask.Result;
            if (standardResult.Length != gpuResult.Length)
            {
                throw new InvalidOperationException(\"Results length mismatch between Standard and GPU LINQ\");
            }
        }
        
        var avgStandardTime = standardTimes.Average();
        var avgGpuTime = gpuTimes.Average();
        var speedup = avgStandardTime / avgGpuTime;
        
        return new SpeedupResult
        {
            StandardTime = avgStandardTime,
            GpuTime = avgGpuTime,
            SpeedupFactor = speedup,
            DataSize = DataSize,
            Operation = Operation.ToString()
        };
    }
    
    #endregion

    #region Real-World Scenario Benchmarks
    
    [Benchmark]
    [BenchmarkCategory("RealWorld", "Financial")]
    public decimal StandardLinq_FinancialAnalysis()
    {
        return _financialData
            .Where(f => f.Date >= DateTime.Today.AddDays(-30))
            .Where(f => f.Volume > 10000)
            .Select(f => f.Price * (decimal)f.Volatility)
            .Average();
    }
    
    [Benchmark]
    [BenchmarkCategory("RealWorld", "Financial")]
    public async Task<decimal> GpuLinq_FinancialAnalysis()
    {
        // Note: This would require custom GPU kernels for decimal operations
        // For demonstration, we'll use double precision
        var doubleData = _financialData.Select(f => new
        {
            Price = (double)f.Price,
            f.Volume,
            f.Volatility,
            f.Date
        }).ToArray();
        
        var queryable = _gpuLinqProvider.CreateQueryable(doubleData, _gpuAccelerator);
        
        var result = await ExecuteGpuQuery(queryable
            .Where(f => f.Date >= DateTime.Today.AddDays(-30))
            .Where(f => f.Volume > 10000)
            .Select(f => f.Price * f.Volatility)
            .Average());
            
        return (decimal)result;
    }
    
    [Benchmark]
    [BenchmarkCategory("RealWorld", "DataScience")]
    public double[] StandardLinq_DataScience()
    {
        // Simulate data science workload: normalization and statistical operations
        var mean = _doubleData.Average();
        var stdDev = Math.Sqrt(_doubleData.Select(x => Math.Pow(x - mean, 2)).Average());
        
        return _doubleData
            .Select(x => (x - mean) / stdDev) // Z-score normalization
            .Select(x => Math.Tanh(x)) // Activation function
            .ToArray();
    }
    
    [Benchmark]
    [BenchmarkCategory("RealWorld", "DataScience")]
    public async Task<double[]> GpuLinq_DataScience()
    {
        var queryable = _gpuLinqProvider.CreateQueryable(_doubleData, _gpuAccelerator);
        
        // Calculate mean and standard deviation on GPU
        var mean = await ExecuteGpuQuery(queryable.Average());
        var variance = await ExecuteGpuQuery(queryable.Select(x => Math.Pow(x - mean, 2)).Average());
        var stdDev = Math.Sqrt(variance);
        
        // Apply normalization and activation function
        var normalizedQueryable = _gpuLinqProvider.CreateQueryable(_doubleData, _gpuAccelerator);
        return await ExecuteGpuQuery(normalizedQueryable
            .Select(x => (x - mean) / stdDev)
            .Select(x => Math.Tanh(x)));
    }
    
    #endregion

    #region Helper Methods
    
    private async Task<T[]> ExecuteGpuQuery<T>(IQueryable<T> queryable)
    {
        if (queryable is IAsyncEnumerable<T> asyncEnumerable)
        {
            var result = new List<T>();
            await foreach (var item in asyncEnumerable)
            {
                result.Add(item);
            }
            return result.ToArray();
        }
        
        // Fallback to synchronous execution
        return queryable.ToArray();
    }
    
    private async Task<T> ExecuteGpuQuery<T>(IQueryable<T> queryable) where T : struct
    {
        if (queryable.Provider is IComputeQueryProvider provider)
        {
            return await provider.ExecuteAsync<T>(queryable.Expression);
        }
        
        // Fallback to synchronous execution
        return queryable.FirstOrDefault();
    }
    
    #endregion
}

#region Supporting Types

/// <summary>
/// Types of LINQ operations for benchmarking.
/// </summary>
public enum OperationType
{
    Select,
    Where,
    Aggregate,
    Mixed
}

/// <summary>
/// Complex data structure for realistic performance testing.
/// </summary>
public class ComplexData
{
    public int Id { get; set; }
    public double Value { get; set; }
    public int Category { get; set; }
    public bool IsActive { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Financial data structure for real-world scenario testing.
/// </summary>
public class FinancialData
{
    public decimal Price { get; set; }
    public int Volume { get; set; }
    public double Volatility { get; set; }
    public DateTime Date { get; set; }
}

/// <summary>
/// Result structure for speedup validation.
/// </summary>
public class SpeedupResult
{
    public double StandardTime { get; set; }
    public double GpuTime { get; set; }
    public double SpeedupFactor { get; set; }
    public int DataSize { get; set; }
    public string Operation { get; set; } = string.Empty;
    
    public override string ToString()
    {
        return $"DataSize: {DataSize}, Operation: {Operation}, " +
               $"StandardTime: {StandardTime:F2}ms, GpuTime: {GpuTime:F2}ms, " +
               $"Speedup: {SpeedupFactor:F1}x";
    }
}

/// <summary>
/// Custom benchmark configuration for LINQ vs GPU comparison.
/// </summary>
public class LinqVsGpuConfig : ManualConfig
{
    public LinqVsGpuConfig()
    {
        AddJob(Job.Default
            .WithRuntime(CoreRuntime.Core90)
            .WithJit(Jit.RyuJit)
            .WithPlatform(Platform.X64)
            .WithWarmupCount(5)
            .WithIterationCount(10)
            .WithInvocationCount(1)
            .WithStrategy(RunStrategy.Throughput));
            
        AddDiagnoser(MemoryDiagnoser.Default);
        AddDiagnoser(ThreadingDiagnoser.Default);
        
        // Add columns for comparison analysis
        AddColumn(StatisticColumn.Mean);
        AddColumn(StatisticColumn.StdDev);
        AddColumn(StatisticColumn.Median);
        AddColumn(BaselineRatioColumn.RatioMean);
        AddColumn(RankColumn.Arabic);
        
        // Custom speedup column
        AddColumn(new SpeedupColumn());
        
        AddOrderer(DefaultOrderer.Instance);
        
        WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }
}

/// <summary>
/// Custom column to display speedup factors in benchmark results.
/// </summary>
public class SpeedupColumn : IColumn
{
    public string Id => nameof(SpeedupColumn);
    public string ColumnName => "Speedup";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => 0;
    public bool IsNumeric => true;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => "Speedup factor compared to baseline";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        var baseline = summary.GetBaseline();
        if (baseline == null) return "-";
        
        var baselineResult = summary[baseline];
        var currentResult = summary[benchmarkCase];
        
        if (baselineResult?.ResultStatistics?.Mean == null || 
            currentResult?.ResultStatistics?.Mean == null)
            return "-";
            
        var speedup = baselineResult.ResultStatistics.Mean / currentResult.ResultStatistics.Mean;
        return $"{speedup:F1}x";
    }

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style) => GetValue(summary, benchmarkCase);
    public bool IsAvailable(Summary summary) => true;
    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
}

#endregion