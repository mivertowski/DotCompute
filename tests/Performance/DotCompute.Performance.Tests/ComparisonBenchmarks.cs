// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Memory;
using DotCompute.Tests.Common;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Performance.Tests;

/// <summary>
/// Comprehensive benchmark comparisons between CPU vs GPU performance, different optimization levels,
/// algorithm implementations, and backend comparisons with statistical analysis and speedup calculations.
/// </summary>
[Trait("Category", TestCategories.Performance)]
[Trait("Category", TestCategories.Benchmark)]
public class ComparisonBenchmarks : GpuTestBase
{
    private readonly ITestOutputHelper _output;
    private readonly MockCpuAccelerator _cpuAccelerator;
    private readonly MockGpuAccelerator _gpuAccelerator;
    private readonly CancellationTokenSource _cts;
    
    // Standard workload sizes for comparison
    private static readonly int[] BenchmarkSizes = 
    {
        1_000,      // 1K - Small
        10_000,     // 10K - Medium
        100_000,    // 100K - Large  
        1_000_000,  // 1M - Very Large
        10_000_000  // 10M - Extreme
    };
    
    public ComparisonBenchmarks(ITestOutputHelper output) : base(output)
    {
        _output = output;
        _cpuAccelerator = new MockCpuAccelerator();
        _gpuAccelerator = new MockGpuAccelerator();
        _cts = new CancellationTokenSource(TimeSpan.FromMinutes(20));
    }

    #region CPU vs GPU Performance Comparisons

    [Theory]
    [MemberData(nameof(GetCpuVsGpuTestData))]
    [Trait("Category", TestCategories.Performance)]
    public async Task ComparePerformance_CpuVsGpu_ShowsExpectedSpeedup(int workloadSize, string algorithm, string description)
    {
        // Arrange
        using var perfContext = CreatePerformanceContext($"CPU vs GPU - {description}");
        
        var cpuKernel = CreateKernelForBackend(algorithm, workloadSize, "CPU");
        var gpuKernel = CreateKernelForBackend(algorithm, workloadSize, "GPU");
        
        var cpuCompiledKernel = await _cpuAccelerator.CompileKernelAsync(cpuKernel, null, _cts.Token);
        var gpuCompiledKernel = await _gpuAccelerator.CompileKernelAsync(gpuKernel, null, _cts.Token);
        
        var arguments = CreateBenchmarkArguments(workloadSize);
        const int iterations = 15;
        
        var cpuTimes = new List<double>();
        var gpuTimes = new List<double>();
        
        try
        {
            perfContext.Checkpoint("Kernels compiled, starting warmup");
            
            // Warmup both backends
            for (var i = 0; i < 3; i++)
            {
                await cpuCompiledKernel.ExecuteAsync(arguments, _cts.Token);
                await _cpuAccelerator.SynchronizeAsync(_cts.Token);
                
                await gpuCompiledKernel.ExecuteAsync(arguments, _cts.Token);
                await _gpuAccelerator.SynchronizeAsync(_cts.Token);
            }
            
            perfContext.Checkpoint("Warmup completed, starting CPU measurements");

            // Measure CPU performance
            for (var i = 0; i < iterations; i++)
            {
                var sw = Stopwatch.StartNew();
                await cpuCompiledKernel.ExecuteAsync(arguments, _cts.Token);
                await _cpuAccelerator.SynchronizeAsync(_cts.Token);
                sw.Stop();
                
                cpuTimes.Add(sw.Elapsed.TotalMilliseconds);
            }
            
            perfContext.Checkpoint("CPU measurements completed, starting GPU measurements");

            // Measure GPU performance
            for (var i = 0; i < iterations; i++)
            {
                var sw = Stopwatch.StartNew();
                await gpuCompiledKernel.ExecuteAsync(arguments, _cts.Token);
                await _gpuAccelerator.SynchronizeAsync(_cts.Token);
                sw.Stop();
                
                gpuTimes.Add(sw.Elapsed.TotalMilliseconds);
            }
            
            perfContext.Checkpoint("GPU measurements completed");

            // Analyze performance comparison
            var cpuStats = CalculateStatistics(cpuTimes);
            var gpuStats = CalculateStatistics(gpuTimes);
            var speedup = cpuStats.Mean / gpuStats.Mean;
            var efficiencyRatio = CalculateEfficiencyRatio(workloadSize, cpuStats.Mean, gpuStats.Mean);
            
            _output.WriteLine($"CPU vs GPU Performance - {description} ({workloadSize:N0} elements):");
            _output.WriteLine($"  Algorithm: {algorithm}");
            _output.WriteLine($"  CPU Performance:");
            _output.WriteLine($"    Mean: {cpuStats.Mean:F2}ms");
            _output.WriteLine($"    Std Dev: {cpuStats.StdDev:F2}ms");
            _output.WriteLine($"    Min: {cpuStats.Min:F2}ms");
            _output.WriteLine($"    Max: {cpuStats.Max:F2}ms");
            _output.WriteLine($"    Throughput: {workloadSize / cpuStats.Mean:F0} ops/ms");
            _output.WriteLine($"  GPU Performance:");
            _output.WriteLine($"    Mean: {gpuStats.Mean:F2}ms");
            _output.WriteLine($"    Std Dev: {gpuStats.StdDev:F2}ms");
            _output.WriteLine($"    Min: {gpuStats.Min:F2}ms");
            _output.WriteLine($"    Max: {gpuStats.Max:F2}ms");
            _output.WriteLine($"    Throughput: {workloadSize / gpuStats.Mean:F0} ops/ms");
            _output.WriteLine($"  Comparison:");
            _output.WriteLine($"    GPU Speedup: {speedup:F2}x");
            _output.WriteLine($"    Efficiency Ratio: {efficiencyRatio:F2}");
            _output.WriteLine($"    Statistical Significance: {CalculateStatisticalSignificance(cpuTimes, gpuTimes)}");
            
            // Store results for cross-test analysis
            BenchmarkResults.RecordComparison(algorithm, workloadSize, "CPU_vs_GPU", cpuStats.Mean, gpuStats.Mean, speedup);
            
            // Performance expectations based on algorithm and workload size
            ValidateSpeedupExpectations(algorithm, workloadSize, speedup, efficiencyRatio);
        }
        finally
        {
            await cpuCompiledKernel.DisposeAsync();
            await gpuCompiledKernel.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", TestCategories.Performance)]
    [Trait("Category", TestCategories.LongRunning)]
    public async Task ComparePerformance_CpuVsGpuAcrossWorkloadSizes_AnalyzesScalingBreakpoints()
    {
        // Arrange
        using var perfContext = CreatePerformanceContext("CPU vs GPU Scaling Analysis");
        const string algorithm = "matrix_multiply";
        
        var scalingResults = new List<CpuGpuScalingResult>();
        
        // Test across all workload sizes
        foreach (var workloadSize in BenchmarkSizes)
        {
            var cpuKernel = CreateKernelForBackend(algorithm, workloadSize, "CPU");
            var gpuKernel = CreateKernelForBackend(algorithm, workloadSize, "GPU");
            
            var cpuCompiledKernel = await _cpuAccelerator.CompileKernelAsync(cpuKernel, null, _cts.Token);
            var gpuCompiledKernel = await _gpuAccelerator.CompileKernelAsync(gpuKernel, null, _cts.Token);
            
            var arguments = CreateBenchmarkArguments(workloadSize);
            const int measurements = 10;
            
            try
            {
                // Warmup
                await cpuCompiledKernel.ExecuteAsync(arguments, _cts.Token);
                await gpuCompiledKernel.ExecuteAsync(arguments, _cts.Token);
                await _cpuAccelerator.SynchronizeAsync(_cts.Token);
                await _gpuAccelerator.SynchronizeAsync(_cts.Token);
                
                // Measure CPU
                var cpuTimes = new List<double>();
                for (var i = 0; i < measurements; i++)
                {
                    var sw = Stopwatch.StartNew();
                    await cpuCompiledKernel.ExecuteAsync(arguments, _cts.Token);
                    await _cpuAccelerator.SynchronizeAsync(_cts.Token);
                    sw.Stop();
                    cpuTimes.Add(sw.Elapsed.TotalMilliseconds);
                }
                
                // Measure GPU
                var gpuTimes = new List<double>();
                for (var i = 0; i < measurements; i++)
                {
                    var sw = Stopwatch.StartNew();
                    await gpuCompiledKernel.ExecuteAsync(arguments, _cts.Token);
                    await _gpuAccelerator.SynchronizeAsync(_cts.Token);
                    sw.Stop();
                    gpuTimes.Add(sw.Elapsed.TotalMilliseconds);
                }
                
                var cpuAvg = cpuTimes.Average();
                var gpuAvg = gpuTimes.Average();
                var speedup = cpuAvg / gpuAvg;
                var cpuThroughput = workloadSize / cpuAvg;
                var gpuThroughput = workloadSize / gpuAvg;
                
                scalingResults.Add(new CpuGpuScalingResult
                {
                    WorkloadSize = workloadSize,
                    CpuTimeMs = cpuAvg,
                    GpuTimeMs = gpuAvg,
                    Speedup = speedup,
                    CpuThroughput = cpuThroughput,
                    GpuThroughput = gpuThroughput
                });
                
                _output.WriteLine($"Size {workloadSize:N0}: CPU {cpuAvg:F2}ms, GPU {gpuAvg:F2}ms, Speedup {speedup:F2}x");
                
                perfContext.Checkpoint($"Completed workload size {workloadSize:N0}");
            }
            finally
            {
                await cpuCompiledKernel.DisposeAsync();
                await gpuCompiledKernel.DisposeAsync();
            }
        }
        
        // Analyze scaling patterns
        AnalyzeCpuGpuScalingPatterns(scalingResults);
        IdentifyScalingBreakpoints(scalingResults);
        ValidateScalingTrends(scalingResults);
    }

    #endregion

    #region Optimization Level Comparisons

    [Theory]
    [InlineData(OptimizationLevel.None, "No optimization")]
    [InlineData(OptimizationLevel.Default, "Default optimization")]
    [InlineData(OptimizationLevel.Aggressive, "Aggressive optimization")]
    [Trait("Category", TestCategories.Performance)]
    [Trait("Category", TestCategories.KernelCompilation)]
    public async Task CompareOptimizationLevels_MeasuresPerformanceImpact_AcrossComplexity(OptimizationLevel level, string description)
    {
        // Arrange
        using var perfContext = CreatePerformanceContext($"Optimization Comparison - {description}");
        
        var algorithms = new[] { "simple_add", "matrix_multiply", "complex_math" };
        var optimizationResults = new List<OptimizationResult>();
        
        foreach (var algorithm in algorithms)
        {
            const int workloadSize = 100_000;
            var kernel = CreateKernelForBackend(algorithm, workloadSize, "GPU");
            var options = new CompilationOptions
            {
                OptimizationLevel = level,
                EnableDebugInfo = false
            };
            
            // Measure compilation time
            var compilationSw = Stopwatch.StartNew();
            var compiledKernel = await _gpuAccelerator.CompileKernelAsync(kernel, options, _cts.Token);
            compilationSw.Stop();
            
            var arguments = CreateBenchmarkArguments(workloadSize);
            const int executions = 12;
            var executionTimes = new List<double>();
            
            try
            {
                // Warmup
                for (var i = 0; i < 3; i++)
                {
                    await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                    await _gpuAccelerator.SynchronizeAsync(_cts.Token);
                }
                
                // Measure execution performance
                for (var i = 0; i < executions; i++)
                {
                    var sw = Stopwatch.StartNew();
                    await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                    await _gpuAccelerator.SynchronizeAsync(_cts.Token);
                    sw.Stop();
                    
                    executionTimes.Add(sw.Elapsed.TotalMilliseconds);
                }
                
                var result = new OptimizationResult
                {
                    Algorithm = algorithm,
                    OptimizationLevel = level,
                    CompilationTimeMs = compilationSw.Elapsed.TotalMilliseconds,
                    AverageExecutionTimeMs = executionTimes.Average(),
                    ExecutionStdDev = CalculateStandardDeviation(executionTimes),
                    EstimatedCodeSize = GetEstimatedCodeSize(compiledKernel, level)
                };
                
                optimizationResults.Add(result);
                
                _output.WriteLine($"{algorithm} - {description}:");
                _output.WriteLine($"  Compilation: {result.CompilationTimeMs:F2}ms");
                _output.WriteLine($"  Execution: {result.AverageExecutionTimeMs:F2}ms (±{result.ExecutionStdDev:F2}ms)");
                _output.WriteLine($"  Estimated code size: {result.EstimatedCodeSize:N0} bytes");
                
                perfContext.Checkpoint($"Completed {algorithm} with {description}");
            }
            finally
            {
                await compiledKernel.DisposeAsync();
            }
        }
        
        // Store optimization results for cross-level comparison
        BenchmarkResults.RecordOptimizationResults(level, optimizationResults);

        // Validate optimization effects

        ValidateOptimizationEffects(optimizationResults, level);
    }

    [Fact]
    [Trait("Category", TestCategories.Performance)]
    [Trait("Category", TestCategories.KernelCompilation)]
    public async Task CompareOptimizationLevels_AnalyzesTradeoffs_CompilationVsExecution()
    {
        // Arrange
        using var perfContext = CreatePerformanceContext("Optimization Tradeoff Analysis");
        
        const string algorithm = "complex_math";
        const int workloadSize = 1_000_000;
        var optimizationLevels = new[] 
        {
            OptimizationLevel.None,
            OptimizationLevel.Default,
            OptimizationLevel.Aggressive
        };
        
        var tradeoffResults = new Dictionary<OptimizationLevel, OptimizationTradeoffResult>();
        
        foreach (var level in optimizationLevels)
        {
            var kernel = CreateKernelForBackend(algorithm, workloadSize, "GPU");
            var options = new CompilationOptions
            {
                OptimizationLevel = level,
                EnableDebugInfo = false
            };
            
            const int compilationRuns = 5;
            const int executionRuns = 20;
            var compilationTimes = new List<double>();
            var executionTimes = new List<double>();
            
            // Measure multiple compilation runs
            for (var i = 0; i < compilationRuns; i++)
            {
                var sw = Stopwatch.StartNew();
                var compiledKernel = await _gpuAccelerator.CompileKernelAsync(kernel, options, _cts.Token);
                sw.Stop();
                
                compilationTimes.Add(sw.Elapsed.TotalMilliseconds);
                
                // Measure execution with this compiled kernel
                var arguments = CreateBenchmarkArguments(workloadSize);
                
                // Warmup
                await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                await _gpuAccelerator.SynchronizeAsync(_cts.Token);
                
                // Single execution measurement for this compilation
                var execSw = Stopwatch.StartNew();
                await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                await _gpuAccelerator.SynchronizeAsync(_cts.Token);
                execSw.Stop();
                
                executionTimes.Add(execSw.Elapsed.TotalMilliseconds);
                
                await compiledKernel.DisposeAsync();
            }
            
            var tradeoffResult = new OptimizationTradeoffResult
            {
                OptimizationLevel = level,
                AverageCompilationTimeMs = compilationTimes.Average(),
                CompilationStdDev = CalculateStandardDeviation(compilationTimes),
                AverageExecutionTimeMs = executionTimes.Average(),
                ExecutionStdDev = CalculateStandardDeviation(executionTimes),
                CompilationOverhead = compilationTimes.Average() / executionTimes.Average(),
                BreakEvenPoint = CalculateBreakEvenPoint(compilationTimes.Average(), executionTimes.Average())
            };
            
            tradeoffResults[level] = tradeoffResult;
            
            _output.WriteLine($"Optimization Level: {level}");
            _output.WriteLine($"  Compilation: {tradeoffResult.AverageCompilationTimeMs:F2}ms (±{tradeoffResult.CompilationStdDev:F2}ms)");
            _output.WriteLine($"  Execution: {tradeoffResult.AverageExecutionTimeMs:F2}ms (±{tradeoffResult.ExecutionStdDev:F2}ms)");
            _output.WriteLine($"  Compilation overhead: {tradeoffResult.CompilationOverhead:F1}x execution time");
            _output.WriteLine($"  Break-even point: {tradeoffResult.BreakEvenPoint:F0} executions");
            
            perfContext.Checkpoint($"Completed optimization level {level}");
        }
        
        // Compare optimization tradeoffs
        CompareOptimizationTradeoffs(tradeoffResults);
    }

    #endregion

    #region Algorithm Comparisons

    [Theory]
    [MemberData(nameof(GetAlgorithmComparisonData))]
    [Trait("Category", TestCategories.Performance)]
    public async Task CompareAlgorithms_SameTask_DifferentImplementations(string[] algorithms, string taskDescription)
    {
        // Arrange
        using var perfContext = CreatePerformanceContext($"Algorithm Comparison - {taskDescription}");
        
        const int workloadSize = 500_000;
        var algorithmResults = new List<AlgorithmResult>();
        
        foreach (var algorithm in algorithms)
        {
            var kernel = CreateKernelForBackend(algorithm, workloadSize, "GPU");
            var compiledKernel = await _gpuAccelerator.CompileKernelAsync(kernel, null, _cts.Token);
            var arguments = CreateBenchmarkArguments(workloadSize);
            
            const int iterations = 15;
            var executionTimes = new List<double>();
            var memoryUsages = new List<long>();
            
            try
            {
                // Warmup
                for (var i = 0; i < 3; i++)
                {
                    await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                    await _gpuAccelerator.SynchronizeAsync(_cts.Token);
                }
                
                // Measure performance
                for (var i = 0; i < iterations; i++)
                {
                    var initialMemory = GC.GetTotalMemory(false);
                    
                    var sw = Stopwatch.StartNew();
                    await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                    await _gpuAccelerator.SynchronizeAsync(_cts.Token);
                    sw.Stop();
                    
                    var finalMemory = GC.GetTotalMemory(false);
                    
                    executionTimes.Add(sw.Elapsed.TotalMilliseconds);
                    memoryUsages.Add(finalMemory - initialMemory);
                }
                
                var result = new AlgorithmResult
                {
                    Algorithm = algorithm,
                    TaskDescription = taskDescription,
                    WorkloadSize = workloadSize,
                    AverageExecutionTimeMs = executionTimes.Average(),
                    ExecutionStdDev = CalculateStandardDeviation(executionTimes),
                    MinExecutionTimeMs = executionTimes.Min(),
                    MaxExecutionTimeMs = executionTimes.Max(),
                    AverageMemoryUsageBytes = memoryUsages.Average(),
                    Throughput = workloadSize / executionTimes.Average(),
                    Efficiency = CalculateAlgorithmEfficiency(algorithm, executionTimes.Average())
                };
                
                algorithmResults.Add(result);
                
                _output.WriteLine($"{algorithm}:");
                _output.WriteLine($"  Execution time: {result.AverageExecutionTimeMs:F2}ms (±{result.ExecutionStdDev:F2}ms)");
                _output.WriteLine($"  Range: {result.MinExecutionTimeMs:F2}ms - {result.MaxExecutionTimeMs:F2}ms");
                _output.WriteLine($"  Throughput: {result.Throughput:F0} ops/ms");
                _output.WriteLine($"  Memory usage: {result.AverageMemoryUsageBytes / 1024.0:F1} KB");
                _output.WriteLine($"  Efficiency score: {result.Efficiency:F2}");
                
                perfContext.Checkpoint($"Completed algorithm {algorithm}");
            }
            finally
            {
                await compiledKernel.DisposeAsync();
            }
        }
        
        // Analyze algorithm comparison
        AnalyzeAlgorithmComparison(algorithmResults, taskDescription);
        RankAlgorithmsByPerformance(algorithmResults);
        ValidateAlgorithmResults(algorithmResults);
    }

    #endregion

    #region Backend Comparisons

    [Theory]
    [InlineData("CUDA", "OpenCL", "GPU Backend Comparison")]
    [InlineData("CPU", "GPU", "Accelerator Type Comparison")]
    [Trait("Category", TestCategories.Performance)]
    public async Task CompareBackends_SameAlgorithm_DifferentBackends(string backend1, string backend2, string description)
    {
        // Arrange
        using var perfContext = CreatePerformanceContext($"Backend Comparison - {description}");
        
        const string algorithm = "matrix_multiply";
        var workloadSizes = new[] { 10_000, 100_000, 1_000_000 };
        var backendResults = new List<BackendComparisonResult>();
        
        foreach (var workloadSize in workloadSizes)
        {
            var kernel1 = CreateKernelForBackend(algorithm, workloadSize, backend1);
            var kernel2 = CreateKernelForBackend(algorithm, workloadSize, backend2);
            
            var accelerator1 = GetAcceleratorForBackend(backend1);
            var accelerator2 = GetAcceleratorForBackend(backend2);
            
            var compiledKernel1 = await accelerator1.CompileKernelAsync(kernel1, null, _cts.Token);
            var compiledKernel2 = await accelerator2.CompileKernelAsync(kernel2, null, _cts.Token);
            
            var arguments = CreateBenchmarkArguments(workloadSize);
            const int measurements = 12;
            
            try
            {
                // Measure backend 1
                var times1 = new List<double>();
                for (var i = 0; i < measurements + 2; i++) // Include warmup
                {
                    var sw = Stopwatch.StartNew();
                    await compiledKernel1.ExecuteAsync(arguments, _cts.Token);
                    await accelerator1.SynchronizeAsync(_cts.Token);
                    sw.Stop();
                    
                    if (i >= 2) times1.Add(sw.Elapsed.TotalMilliseconds); // Skip warmup
                }
                
                // Measure backend 2
                var times2 = new List<double>();
                for (var i = 0; i < measurements + 2; i++) // Include warmup
                {
                    var sw = Stopwatch.StartNew();
                    await compiledKernel2.ExecuteAsync(arguments, _cts.Token);
                    await accelerator2.SynchronizeAsync(_cts.Token);
                    sw.Stop();
                    
                    if (i >= 2) times2.Add(sw.Elapsed.TotalMilliseconds); // Skip warmup
                }
                
                var avg1 = times1.Average();
                var avg2 = times2.Average();
                var speedup = Math.Max(avg1, avg2) / Math.Min(avg1, avg2);
                var winner = avg1 < avg2 ? backend1 : backend2;
                
                var result = new BackendComparisonResult
                {
                    Algorithm = algorithm,
                    WorkloadSize = workloadSize,
                    Backend1 = backend1,
                    Backend2 = backend2,
                    Backend1TimeMs = avg1,
                    Backend2TimeMs = avg2,
                    Backend1StdDev = CalculateStandardDeviation(times1),
                    Backend2StdDev = CalculateStandardDeviation(times2),
                    Speedup = speedup,
                    Winner = winner,
                    StatisticalSignificance = CalculateStatisticalSignificance(times1, times2)
                };
                
                backendResults.Add(result);
                
                _output.WriteLine($"Workload {workloadSize:N0}:");
                _output.WriteLine($"  {backend1}: {avg1:F2}ms (±{result.Backend1StdDev:F2}ms)");
                _output.WriteLine($"  {backend2}: {avg2:F2}ms (±{result.Backend2StdDev:F2}ms)");
                _output.WriteLine($"  Winner: {winner} ({speedup:F2}x speedup)");
                _output.WriteLine($"  Statistical significance: {result.StatisticalSignificance}");
                
                perfContext.Checkpoint($"Completed workload {workloadSize:N0}");
            }
            finally
            {
                await compiledKernel1.DisposeAsync();
                await compiledKernel2.DisposeAsync();
            }
        }
        
        // Analyze backend comparison trends
        AnalyzeBackendComparisonTrends(backendResults, description);
        ValidateBackendComparison(backendResults);
    }

    #endregion

    #region Analysis and Validation Methods

    private void ValidateSpeedupExpectations(string algorithm, int workloadSize, double speedup, double efficiencyRatio)
    {
        // Set expectations based on algorithm characteristics
        var (expectedMinSpeedup, expectedMaxSpeedup) = GetExpectedSpeedupRange(algorithm, workloadSize);
        
        speedup.Should().BeGreaterThan(expectedMinSpeedup, 
            $"{algorithm} with workload {workloadSize:N0} should achieve at least {expectedMinSpeedup:F1}x speedup");
        
        if (speedup > expectedMaxSpeedup * 2)
        {
            _output.WriteLine($"⚠️  Unexpectedly high speedup ({speedup:F2}x) - verify benchmark accuracy");
        }
        
        efficiencyRatio.Should().BeGreaterThan(0.5, "Efficiency ratio should indicate reasonable resource utilization");
    }

    private void AnalyzeCpuGpuScalingPatterns(List<CpuGpuScalingResult> results)
    {
        _output.WriteLine("\n=== CPU vs GPU Scaling Pattern Analysis ===");
        
        for (var i = 1; i < results.Count; i++)
        {
            var current = results[i];
            var previous = results[i - 1];
            
            var workloadRatio = (double)current.WorkloadSize / previous.WorkloadSize;
            var cpuTimeRatio = current.CpuTimeMs / previous.CpuTimeMs;
            var gpuTimeRatio = current.GpuTimeMs / previous.GpuTimeMs;
            var speedupTrend = current.Speedup / previous.Speedup;
            
            _output.WriteLine($"Size {previous.WorkloadSize:N0} → {current.WorkloadSize:N0} ({workloadRatio:F1}x):");
            _output.WriteLine($"  CPU scaling: {cpuTimeRatio:F2}x");
            _output.WriteLine($"  GPU scaling: {gpuTimeRatio:F2}x");
            _output.WriteLine($"  Speedup trend: {speedupTrend:F2}x ({(speedupTrend > 1 ? "improving" : "declining")})");
            
            if (gpuTimeRatio > cpuTimeRatio * 1.5)
            {
                _output.WriteLine($"  ⚠️  GPU scaling worse than CPU at this size");
            }
        }
    }

    private void IdentifyScalingBreakpoints(List<CpuGpuScalingResult> results)
    {
        _output.WriteLine("\n=== Scaling Breakpoint Analysis ===");
        
        var peakSpeedup = results.Max(r => r.Speedup);
        var peakSpeedupSize = results.First(r => Math.Abs(r.Speedup - peakSpeedup) < 0.01).WorkloadSize;
        
        _output.WriteLine($"Peak GPU speedup: {peakSpeedup:F2}x at workload size {peakSpeedupSize:N0}");
        
        // Find crossover point where GPU becomes faster than CPU
        var crossoverPoint = results.FirstOrDefault(r => r.Speedup > 1.0);
        if (crossoverPoint != null)
        {
            _output.WriteLine($"GPU becomes faster than CPU at workload size: {crossoverPoint.WorkloadSize:N0}");
        }
        
        // Find efficiency decline points
        for (var i = 1; i < results.Count; i++)
        {
            if (results[i].Speedup < results[i - 1].Speedup * 0.9)
            {
                _output.WriteLine($"⚠️  Significant speedup decline at workload size {results[i].WorkloadSize:N0}");
            }
        }
    }

    private static void ValidateOptimizationEffects(List<OptimizationResult> results, OptimizationLevel level)
    {
        foreach (var result in results)
        {
            // Optimization should generally improve execution time
            if (level == OptimizationLevel.Aggressive)
            {
                result.AverageExecutionTimeMs.Should().BeLessThan(1000, 
                    $"Aggressive optimization should produce reasonable execution times for {result.Algorithm}");
            }
            
            // Compilation time increases with optimization level are acceptable
            if (level == OptimizationLevel.Aggressive)
            {
                result.CompilationTimeMs.Should().BeLessThan(10000, 
                    "Compilation time should remain reasonable even with aggressive optimization");
            }
            
            // Consistency should be maintained
            (result.ExecutionStdDev / result.AverageExecutionTimeMs).Should().BeLessThan(0.3, 
                "Execution times should be reasonably consistent regardless of optimization level");
        }
    }

    private void CompareOptimizationTradeoffs(Dictionary<OptimizationLevel, OptimizationTradeoffResult> results)
    {
        _output.WriteLine("\n=== Optimization Tradeoff Analysis ===");
        
        var noneResult = results[OptimizationLevel.None];
        var defaultResult = results[OptimizationLevel.Default];
        var aggressiveResult = results[OptimizationLevel.Aggressive];
        
        _output.WriteLine($"Compilation time progression:");
        _output.WriteLine($"  None → Default: {defaultResult.AverageCompilationTimeMs / noneResult.AverageCompilationTimeMs:F2}x");
        _output.WriteLine($"  Default → Aggressive: {aggressiveResult.AverageCompilationTimeMs / defaultResult.AverageCompilationTimeMs:F2}x");
        
        _output.WriteLine($"Execution time improvement:");
        _output.WriteLine($"  None → Default: {noneResult.AverageExecutionTimeMs / defaultResult.AverageExecutionTimeMs:F2}x faster");
        _output.WriteLine($"  Default → Aggressive: {defaultResult.AverageExecutionTimeMs / aggressiveResult.AverageExecutionTimeMs:F2}x faster");
        
        _output.WriteLine($"Break-even points:");
        _output.WriteLine($"  Default optimization: {defaultResult.BreakEvenPoint:F0} executions");
        _output.WriteLine($"  Aggressive optimization: {aggressiveResult.BreakEvenPoint:F0} executions");
    }

    private void AnalyzeAlgorithmComparison(List<AlgorithmResult> results, string taskDescription)
    {
        _output.WriteLine($"\n=== Algorithm Analysis for {taskDescription} ===");
        
        var sortedResults = results.OrderBy(r => r.AverageExecutionTimeMs).ToList();
        var fastest = sortedResults.First();
        var slowest = sortedResults.Last();
        
        _output.WriteLine($"Fastest algorithm: {fastest.Algorithm} ({fastest.AverageExecutionTimeMs:F2}ms)");
        _output.WriteLine($"Slowest algorithm: {slowest.Algorithm} ({slowest.AverageExecutionTimeMs:F2}ms)");
        _output.WriteLine($"Performance range: {slowest.AverageExecutionTimeMs / fastest.AverageExecutionTimeMs:F2}x");
        
        // Memory efficiency comparison
        var mostMemoryEfficient = results.OrderBy(r => r.AverageMemoryUsageBytes).First();
        var leastMemoryEfficient = results.OrderByDescending(r => r.AverageMemoryUsageBytes).First();
        
        _output.WriteLine($"Most memory efficient: {mostMemoryEfficient.Algorithm} ({mostMemoryEfficient.AverageMemoryUsageBytes / 1024.0:F1} KB)");
        _output.WriteLine($"Least memory efficient: {leastMemoryEfficient.Algorithm} ({leastMemoryEfficient.AverageMemoryUsageBytes / 1024.0:F1} KB)");
    }

    private void RankAlgorithmsByPerformance(List<AlgorithmResult> results)
    {
        _output.WriteLine("\n=== Algorithm Performance Ranking ===");
        
        // Composite score based on execution time, memory usage, and consistency
        var rankedAlgorithms = results
            .Select(r => new 
            {
                Algorithm = r.Algorithm,
                Score = CalculateCompositeScore(r),
                ExecutionTime = r.AverageExecutionTimeMs,
                Throughput = r.Throughput,
                Consistency = 1.0 / (r.ExecutionStdDev / r.AverageExecutionTimeMs + 0.001) // Higher is better
            })
            .OrderByDescending(x => x.Score)
            .ToList();
        
        for (var i = 0; i < rankedAlgorithms.Count; i++)
        {
            var alg = rankedAlgorithms[i];
            _output.WriteLine($"{i + 1}. {alg.Algorithm}: Score {alg.Score:F2}, " +
                            $"Time {alg.ExecutionTime:F2}ms, Throughput {alg.Throughput:F0} ops/ms");
        }
    }

    private void AnalyzeBackendComparisonTrends(List<BackendComparisonResult> results, string description)
    {
        _output.WriteLine($"\n=== {description} Trend Analysis ===");
        
        var winnerCounts = results.GroupBy(r => r.Winner)
            .ToDictionary(g => g.Key, g => g.Count());
        
        foreach (var kvp in winnerCounts)
        {
            _output.WriteLine($"{kvp.Key} wins: {kvp.Value}/{results.Count} workload sizes");
        }
        
        var avgSpeedup = results.Average(r => r.Speedup);
        var maxSpeedup = results.Max(r => r.Speedup);
        
        _output.WriteLine($"Average speedup: {avgSpeedup:F2}x");
        _output.WriteLine($"Maximum speedup: {maxSpeedup:F2}x");
        
        // Analyze trends across workload sizes
        for (var i = 1; i < results.Count; i++)
        {
            var current = results[i];
            var previous = results[i - 1];
            
            if (current.Winner != previous.Winner)
            {
                _output.WriteLine($"Winner changes from {previous.Winner} to {current.Winner} at workload size {current.WorkloadSize:N0}");
            }
        }
    }

    #endregion

    #region Helper Methods and Data

    public static IEnumerable<object[]> GetCpuVsGpuTestData()
    {
        yield return new object[] { 10_000, "vector_add", "Vector Addition - 10K" };
        yield return new object[] { 100_000, "vector_add", "Vector Addition - 100K" };
        yield return new object[] { 1_000_000, "vector_add", "Vector Addition - 1M" };
        yield return new object[] { 100_000, "matrix_multiply", "Matrix Multiplication - 100K" };
        yield return new object[] { 1_000_000, "matrix_multiply", "Matrix Multiplication - 1M" };
        yield return new object[] { 50_000, "fft", "FFT - 50K" };
    }
    
    public static IEnumerable<object[]> GetAlgorithmComparisonData()
    {
        yield return new object[] { new[] { "quicksort", "mergesort", "radixsort" }, "Sorting Algorithms" };
        yield return new object[] { new[] { "matrix_multiply_naive", "matrix_multiply_tiled", "matrix_multiply_shared" }, "Matrix Multiplication" };
        yield return new object[] { new[] { "fft_recursive", "fft_iterative", "fft_optimized" }, "Fast Fourier Transform" };
    }

    private KernelDefinition CreateKernelForBackend(string algorithm, int workloadSize, string backend)
    {
        var kernelSource = algorithm switch
        {
            "vector_add" => GenerateVectorAddKernel(backend),
            "matrix_multiply" => GenerateMatrixMultiplyKernel(backend),
            "complex_math" => GenerateComplexMathKernel(backend),
            "fft" => GenerateFftKernel(backend),
            "simple_add" => GenerateSimpleAddKernel(backend),
            "quicksort" => GenerateQuickSortKernel(backend),
            "mergesort" => GenerateMergeSortKernel(backend),
            _ => GenerateDefaultKernel(algorithm, backend)
        };
        
        return new KernelDefinition
        {
            Name = $"{algorithm}_{backend.ToLower()}",
            Source = kernelSource,
            Language = backend == "CPU" ? KernelLanguage.CSharp : KernelLanguage.OpenCL,
            EntryPoint = algorithm
        };
    }

    private IAccelerator GetAcceleratorForBackend(string backend)
    {
        return backend switch
        {
            "CPU" => _cpuAccelerator,
            "GPU" => _gpuAccelerator,
            "CUDA" => _gpuAccelerator, // Mock treats CUDA as GPU
            "OpenCL" => _gpuAccelerator, // Mock treats OpenCL as GPU
            _ => _cpuAccelerator
        };
    }

    private static KernelArguments CreateBenchmarkArguments(int workloadSize)
    {
        var input = GenerateRandomFloats(workloadSize);
        var output = new float[workloadSize];
        return new KernelArguments(new object[] { input, output, workloadSize });
    }

    private PerformanceStatistics CalculateStatistics(List<double> values)
    {
        return new PerformanceStatistics
        {
            Mean = values.Average(),
            StdDev = CalculateStandardDeviation(values),
            Min = values.Min(),
            Max = values.Max(),
            Median = GetPercentile(values.OrderBy(x => x).ToList(), 50)
        };
    }

    private static double CalculateEfficiencyRatio(int workloadSize, double cpuTimeMs, double gpuTimeMs)
    {
        var cpuThroughput = workloadSize / cpuTimeMs;
        var gpuThroughput = workloadSize / gpuTimeMs;
        return gpuThroughput / cpuThroughput;
    }

    private string CalculateStatisticalSignificance(List<double> group1, List<double> group2)
    {
        // Simplified t-test indicator
        var mean1 = group1.Average();
        var mean2 = group2.Average();
        var std1 = CalculateStandardDeviation(group1);
        var std2 = CalculateStandardDeviation(group2);
        
        var pooledStd = Math.Sqrt((std1 * std1 + std2 * std2) / 2);
        var tStat = Math.Abs(mean1 - mean2) / (pooledStd * Math.Sqrt(2.0 / group1.Count));
        
        return tStat switch
        {
            > 2.5 => "Highly Significant",
            > 2.0 => "Significant",
            > 1.5 => "Moderate",
            _ => "Not Significant"
        };
    }

    private static (double minSpeedup, double maxSpeedup) GetExpectedSpeedupRange(string algorithm, int workloadSize)
    {
        return algorithm switch
        {
            "vector_add" when workloadSize > 100_000 => (2.0, 20.0),
            "vector_add" => (1.5, 10.0),
            "matrix_multiply" when workloadSize > 500_000 => (5.0, 50.0),
            "matrix_multiply" => (2.0, 25.0),
            "fft" => (3.0, 30.0),
            "complex_math" => (4.0, 40.0),
            _ => (1.2, 15.0)
        };
    }

    private static long GetEstimatedCodeSize(ICompiledKernel kernel, OptimizationLevel level)
    {
        var baseSize = kernel.Name.Length * 100 + 1024;
        return level switch
        {
            OptimizationLevel.None => baseSize,
            OptimizationLevel.Default => (long)(baseSize * 0.9),
            OptimizationLevel.Aggressive => (long)(baseSize * 0.7),
            _ => baseSize
        };
    }

    private static int CalculateBreakEvenPoint(double compilationTimeMs, double executionTimeMs)
        // Simplified calculation: how many executions to amortize compilation cost
        => (int)Math.Ceiling(compilationTimeMs / executionTimeMs);

    private static double CalculateAlgorithmEfficiency(string algorithm, double executionTimeMs)
    {
        // Algorithm-specific efficiency metric (simplified)
        return algorithm switch
        {
            "quicksort" => 1000.0 / executionTimeMs,
            "matrix_multiply_naive" => 500.0 / executionTimeMs,
            "matrix_multiply_tiled" => 800.0 / executionTimeMs,
            "fft_recursive" => 1200.0 / executionTimeMs,
            _ => 750.0 / executionTimeMs
        };
    }

    private static double CalculateCompositeScore(AlgorithmResult result)
    {
        var timeScore = 1000.0 / result.AverageExecutionTimeMs;
        var memoryScore = 1000.0 / (result.AverageMemoryUsageBytes / 1024.0 + 1);
        var consistencyScore = 100.0 / (result.ExecutionStdDev / result.AverageExecutionTimeMs + 0.01);
        
        return timeScore * 0.5 + memoryScore * 0.3 + consistencyScore * 0.2;
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

    // Kernel generation methods (simplified mock implementations)
    private static string GenerateVectorAddKernel(string backend) => backend == "CPU" ? "/* CPU Vector Add */" : "__kernel void vector_add(__global float* a, __global float* b, __global float* c, int n) { int i = get_global_id(0); if (i < n) c[i] = a[i] + b[i]; }";
    private static string GenerateMatrixMultiplyKernel(string backend) => backend == "CPU" ? "/* CPU Matrix Multiply */" : "__kernel void matrix_multiply(__global float* a, __global float* b, __global float* c, int n) { /* matrix multiply */ }";
    private static string GenerateComplexMathKernel(string backend) => backend == "CPU" ? "/* CPU Complex Math */" : "__kernel void complex_math(__global float* data, int n) { int i = get_global_id(0); if (i < n) data[i] = sin(data[i]) * cos(data[i]); }";
    private static string GenerateFftKernel(string backend) => backend == "CPU" ? "/* CPU FFT */" : "__kernel void fft(__global float* data, int n) { /* FFT implementation */ }";
    private string GenerateSimpleAddKernel(string backend) => GenerateVectorAddKernel(backend);
    private static string GenerateQuickSortKernel(string backend) => backend == "CPU" ? "/* CPU QuickSort */" : "__kernel void quicksort(__global float* data, int n) { /* quicksort */ }";
    private static string GenerateMergeSortKernel(string backend) => backend == "CPU" ? "/* CPU MergeSort */" : "__kernel void mergesort(__global float* data, int n) { /* mergesort */ }";
    private static string GenerateDefaultKernel(string algorithm, string backend) => backend == "CPU" ? $"/* CPU {algorithm} */" : $"__kernel void {algorithm}(__global float* data, int n) {{ /* {algorithm} */ }}";

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Dispose();
            _cpuAccelerator?.DisposeAsync().AsTask().Wait(1000);
            _gpuAccelerator?.DisposeAsync().AsTask().Wait(1000);
        }
        base.Dispose(disposing);
    }
}

#region Data Structures and Support Classes

/// <summary>
/// Performance statistics for benchmark analysis
/// </summary>
internal class PerformanceStatistics
{
    public double Mean { get; set; }
    public double StdDev { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double Median { get; set; }
}

/// <summary>
/// CPU vs GPU scaling comparison result
/// </summary>
internal class CpuGpuScalingResult
{
    public int WorkloadSize { get; set; }
    public double CpuTimeMs { get; set; }
    public double GpuTimeMs { get; set; }
    public double Speedup { get; set; }
    public double CpuThroughput { get; set; }
    public double GpuThroughput { get; set; }
}

/// <summary>
/// Optimization level comparison result
/// </summary>
internal class OptimizationResult
{
    public string Algorithm { get; set; } = string.Empty;
    public OptimizationLevel OptimizationLevel { get; set; }
    public double CompilationTimeMs { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public double ExecutionStdDev { get; set; }
    public long EstimatedCodeSize { get; set; }
}

/// <summary>
/// Optimization tradeoff analysis result
/// </summary>
internal class OptimizationTradeoffResult
{
    public OptimizationLevel OptimizationLevel { get; set; }
    public double AverageCompilationTimeMs { get; set; }
    public double CompilationStdDev { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public double ExecutionStdDev { get; set; }
    public double CompilationOverhead { get; set; }
    public int BreakEvenPoint { get; set; }
}

/// <summary>
/// Algorithm comparison result
/// </summary>
internal class AlgorithmResult
{
    public string Algorithm { get; set; } = string.Empty;
    public string TaskDescription { get; set; } = string.Empty;
    public int WorkloadSize { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public double ExecutionStdDev { get; set; }
    public double MinExecutionTimeMs { get; set; }
    public double MaxExecutionTimeMs { get; set; }
    public double AverageMemoryUsageBytes { get; set; }
    public double Throughput { get; set; }
    public double Efficiency { get; set; }
}

/// <summary>
/// Backend comparison result
/// </summary>
internal class BackendComparisonResult
{
    public string Algorithm { get; set; } = string.Empty;
    public int WorkloadSize { get; set; }
    public string Backend1 { get; set; } = string.Empty;
    public string Backend2 { get; set; } = string.Empty;
    public double Backend1TimeMs { get; set; }
    public double Backend2TimeMs { get; set; }
    public double Backend1StdDev { get; set; }
    public double Backend2StdDev { get; set; }
    public double Speedup { get; set; }
    public string Winner { get; set; } = string.Empty;
    public string StatisticalSignificance { get; set; } = string.Empty;
}

/// <summary>
/// Mock CPU accelerator for benchmarking
/// </summary>
internal class MockCpuAccelerator : IAccelerator
{
    private readonly Random _random = new(42);
    
    public AcceleratorInfo Info { get; } = new AcceleratorInfo
    {
        Id = "mock_cpu",
        Name = "Mock CPU",
        DeviceType = "CPU",
        Vendor = "Mock",
        MaxComputeUnits = Environment.ProcessorCount
    };
    
    public AcceleratorType Type => AcceleratorType.CPU;
    public IUnifiedMemoryManager Memory { get; } = new MockMemoryManager();
    public AcceleratorContext Context { get; } = new AcceleratorContext();

    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Simulate CPU compilation (faster than GPU)
        await Task.Delay(_random.Next(20, 50), cancellationToken);
        return new MockCpuKernel(definition.Name);
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask; // CPU synchronization is immediate

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Mock GPU accelerator for benchmarking
/// </summary>
internal class MockGpuAccelerator : IAccelerator
{
    private readonly Random _random = new(42);
    
    public AcceleratorInfo Info { get; } = new AcceleratorInfo
    {
        Id = "mock_gpu",
        Name = "Mock GPU",
        DeviceType = "GPU",
        Vendor = "Mock",
        MaxComputeUnits = 32
    };
    
    public AcceleratorType Type => AcceleratorType.GPU;
    public IUnifiedMemoryManager Memory { get; } = new MockMemoryManager();
    public AcceleratorContext Context { get; } = new AcceleratorContext();

    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Simulate GPU compilation (slower than CPU but more optimization)
        var baseDelay = _random.Next(100, 200);
        if (options?.OptimizationLevel == OptimizationLevel.Aggressive)
            baseDelay = (int)(baseDelay * 1.5);
            
        await Task.Delay(baseDelay, cancellationToken);
        return new MockGpuKernel(definition.Name);
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => new(Task.Delay(_random.Next(2, 8), cancellationToken)); // GPU sync takes longer

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Mock CPU compiled kernel
/// </summary>
internal class MockCpuKernel : ICompiledKernel
{
    private readonly Random _random = new(42);
    
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; }

    public MockCpuKernel(string name) => Name = name;

    public async ValueTask ExecuteAsync(
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        var workloadSize = GetWorkloadSize(arguments);
        var executionTime = CalculateCpuExecutionTime(workloadSize);
        await Task.Delay(executionTime, cancellationToken);
    }
    
    private int CalculateCpuExecutionTime(int workloadSize)
    {
        // CPU performance: linear scaling with some variance
        var baseTime = Math.Max(1, workloadSize / 50_000); // 1ms per 50K elements
        var variance = _random.NextDouble() * 0.2; // ±20% variance
        return (int)(baseTime * (1 + variance));
    }
    
    private static int GetWorkloadSize(KernelArguments arguments)
    {
        if (arguments.Count > 0 && arguments[0] is float[] array)
            return array.Length;
        return 10_000;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Mock GPU compiled kernel
/// </summary>
internal class MockGpuKernel : ICompiledKernel
{
    private readonly Random _random = new(42);
    
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; }

    public MockGpuKernel(string name) => Name = name;

    public async ValueTask ExecuteAsync(
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        var workloadSize = GetWorkloadSize(arguments);
        var executionTime = CalculateGpuExecutionTime(workloadSize);
        await Task.Delay(executionTime, cancellationToken);
    }
    
    private int CalculateGpuExecutionTime(int workloadSize)
    {
        // GPU performance: better for larger workloads, overhead for small ones
        var baseTime = workloadSize switch
        {
            < 10_000 => Math.Max(10, workloadSize / 1_000), // Overhead dominates
            < 100_000 => Math.Max(5, workloadSize / 20_000), // Improving efficiency
            _ => Math.Max(2, workloadSize / 100_000) // High efficiency
        };
        
        var variance = _random.NextDouble() * 0.15; // ±15% variance (GPUs more consistent)
        return (int)(baseTime * (1 + variance));
    }
    
    private static int GetWorkloadSize(KernelArguments arguments)
    {
        if (arguments.Count > 0 && arguments[0] is float[] array)
            return array.Length;
        return 10_000;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Static storage for benchmark results across tests
/// </summary>
internal static class BenchmarkResults
{
    private static readonly List<ComparisonRecord> _comparisons = [];
    private static readonly Dictionary<OptimizationLevel, List<OptimizationResult>> _optimizationResults = [];
    
    public static void RecordComparison(string algorithm, int workloadSize, string comparisonType, 
        double time1, double time2, double speedup)
    {
        _comparisons.Add(new ComparisonRecord
        {
            Algorithm = algorithm,
            WorkloadSize = workloadSize,
            ComparisonType = comparisonType,
            Time1 = time1,
            Time2 = time2,
            Speedup = speedup,
            Timestamp = DateTime.UtcNow
        });
    }
    
    public static void RecordOptimizationResults(OptimizationLevel level, List<OptimizationResult> results)
    {
        if (!_optimizationResults.ContainsKey(level))
            _optimizationResults[level] = [];
            
        _optimizationResults[level].AddRange(results);
    }
    
    public static IReadOnlyList<ComparisonRecord> GetComparisons() => _comparisons.AsReadOnly();
    public static IReadOnlyDictionary<OptimizationLevel, List<OptimizationResult>> GetOptimizationResults() 
        => _optimizationResults.AsReadOnly();
}

/// <summary>
/// Record of performance comparisons
/// </summary>
internal class ComparisonRecord
{
    public string Algorithm { get; set; } = string.Empty;
    public int WorkloadSize { get; set; }
    public string ComparisonType { get; set; } = string.Empty;
    public double Time1 { get; set; }
    public double Time2 { get; set; }
    public double Speedup { get; set; }
    public DateTime Timestamp { get; set; }
}

#endregion
