// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;

namespace DotCompute.Linq.Benchmarks;

/// <summary>
/// Custom benchmark configuration for optimization impact tests.
/// </summary>
public class OptimizationImpactConfig : ManualConfig
{
    public OptimizationImpactConfig()
    {
        AddJob(Job.Default
            .WithRuntime(CoreRuntime.Core90)
            .WithJit(Jit.RyuJit)
            .WithPlatform(Platform.X64)
            .WithWarmupCount(3)
            .WithIterationCount(7)
            .WithInvocationCount(1)
            .WithStrategy(RunStrategy.Throughput));
            
        AddDiagnoser(MemoryDiagnoser.Default);
        AddDiagnoser(ThreadingDiagnoser.Default);
        
        // Add columns for optimization analysis
        AddColumn(StatisticColumn.Mean);
        AddColumn(StatisticColumn.StdDev);
        AddColumn(StatisticColumn.Median);
        AddColumn(BaselineRatioColumn.RatioMean);
        AddColumn(RankColumn.Arabic);
        
        // Custom optimization impact column
        AddColumn(new OptimizationImpactColumn());
        
        AddOrderer(DefaultOrderer.Instance);
        
        WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }
}