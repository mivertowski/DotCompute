// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Memory;
using DotCompute.Tests.Common;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Performance.Tests;

/// <summary>
/// Scalability tests that measure performance under increasing workload sizes,
/// multi-threaded scaling, resource utilization, and performance degradation curves.
/// </summary>
[Trait("Category", TestCategories.Performance)]
[Trait("Category", TestCategories.LongRunning)]
public class ScalabilityTests : GpuTestBase
{
    private readonly ITestOutputHelper _output;
    private readonly MockScalableAccelerator _accelerator;
    private readonly CancellationTokenSource _cts;
    
    // Workload sizes for scalability testing
    private static readonly int[] WorkloadSizes = 
    {
        1_000,      // 1K
        10_000,     // 10K
        100_000,    // 100K
        1_000_000,  // 1M
        10_000_000, // 10M
        50_000_000  // 50M
    };
    
    // Thread counts for multi-threading tests
    private static readonly int[] ThreadCounts = { 1, 2, 4, 8, 16, 32 };
    
    public ScalabilityTests(ITestOutputHelper output) : base(output)
    {
        _output = output;
        _accelerator = new MockScalableAccelerator();
        _cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
    }

    #region Workload Size Scalability

    [Theory]
    [MemberData(nameof(GetWorkloadScalabilityData))]
    [Trait("Category", TestCategories.Performance)]
    public async Task ExecuteKernel_ScalesWithWorkloadSize_MaintainsPerformanceCharacteristics(int workloadSize, string description)
    {
        // Arrange
        using var perfContext = CreatePerformanceContext($"Workload Scalability - {description}");
        
        var kernel = CreateScalableKernel("vector_operation", workloadSize);
        var compiledKernel = await _accelerator.CompileKernelAsync(kernel, null, _cts.Token);
        var arguments = CreateKernelArguments(workloadSize);
        
        const int iterations = 10;
        var executionTimes = new List<double>();
        var memoryUsages = new List<long>();
        
        try
        {
            // Warmup
            for (var i = 0; i < 3; i++)
            {
                await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                await _accelerator.SynchronizeAsync(_cts.Token);
            }
            
            perfContext.Checkpoint("Warmup completed");

            // Act - Measure execution performance across iterations
            for (var i = 0; i < iterations; i++)
            {
                var initialMemory = GC.GetTotalMemory(false);
                
                var sw = Stopwatch.StartNew();
                await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                await _accelerator.SynchronizeAsync(_cts.Token);
                sw.Stop();
                
                var finalMemory = GC.GetTotalMemory(false);
                
                executionTimes.Add(sw.Elapsed.TotalMilliseconds);
                memoryUsages.Add(finalMemory - initialMemory);
                
                if (i % 2 == 0)
                {
                    perfContext.Checkpoint($"Iteration {i + 1}/{iterations}");
                }
            }

            // Analyze scalability metrics
            var avgTime = executionTimes.Average();
            var minTime = executionTimes.Min();
            var maxTime = executionTimes.Max();
            var stdDev = CalculateStandardDeviation(executionTimes);
            var throughput = workloadSize / avgTime; // Operations per millisecond
            var efficiency = CalculateComputationalEfficiency(workloadSize, avgTime);
            var avgMemoryUsage = memoryUsages.Average();
            
            _output.WriteLine($"Workload Scalability - {description} ({workloadSize:N0} elements):");
            _output.WriteLine($"  Average execution time: {avgTime:F2}ms");
            _output.WriteLine($"  Time range: {minTime:F2}ms - {maxTime:F2}ms");
            _output.WriteLine($"  Standard deviation: {stdDev:F2}ms");
            _output.WriteLine($"  Coefficient of variation: {(stdDev / avgTime) * 100:F1}%");
            _output.WriteLine($"  Throughput: {throughput:F0} ops/ms ({throughput * 1000:F0} ops/sec)");
            _output.WriteLine($"  Computational efficiency: {efficiency:F2} MFLOPS/ms");
            _output.WriteLine($"  Average memory overhead: {avgMemoryUsage / 1024.0:F1} KB");
            
            // Store scalability metrics for trend analysis
            ScalabilityMetrics.RecordWorkloadMetrics(workloadSize, avgTime, throughput, efficiency);
            
            // Performance quality assertions
            (stdDev / avgTime).Should().BeLessThan(0.2, "Execution times should be consistent");
            throughput.Should().BeGreaterThan(0, "Should achieve measurable throughput");
            
            // Efficiency should not degrade significantly for reasonable workload sizes
            if (workloadSize <= 1_000_000)
            {
                efficiency.Should().BeGreaterThan(1.0, "Should maintain reasonable computational efficiency");
            }
        }
        finally
        {
            await compiledKernel.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", TestCategories.Performance)]
    [Trait("Category", TestCategories.LongRunning)]
    public async Task ExecuteKernel_AnalyzesWorkloadScalingTrends_IdentifiesPerformanceBreakpoints()
    {
        // Arrange
        using var perfContext = CreatePerformanceContext("Workload Scaling Trend Analysis");
        var scalabilityResults = new List<WorkloadScalabilityResult>();
        
        // Act - Test across all workload sizes
        foreach (var workloadSize in WorkloadSizes)
        {
            var kernel = CreateScalableKernel($"scaling_test_{workloadSize}", workloadSize);
            var compiledKernel = await _accelerator.CompileKernelAsync(kernel, null, _cts.Token);
            var arguments = CreateKernelArguments(workloadSize);
            
            const int measurements = 8;
            var times = new List<double>();
            var resourceUsages = new List<ResourceUsage>();

            try
            {
                // Warmup
                for (var i = 0; i < 2; i++)
                {
                    await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                    await _accelerator.SynchronizeAsync(_cts.Token);
                }

                // Measure performance
                for (var i = 0; i < measurements; i++)
                {
                    var preExecution = _accelerator.GetResourceUsage();
                    
                    var sw = Stopwatch.StartNew();
                    await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                    await _accelerator.SynchronizeAsync(_cts.Token);
                    sw.Stop();
                    
                    var postExecution = _accelerator.GetResourceUsage();
                    
                    times.Add(sw.Elapsed.TotalMilliseconds);
                    resourceUsages.Add(new ResourceUsage
                    {
                        CpuUsagePercent = postExecution.CpuUsagePercent - preExecution.CpuUsagePercent,
                        MemoryUsageMB = postExecution.MemoryUsageMB - preExecution.MemoryUsageMB,
                        GpuUtilizationPercent = postExecution.GpuUtilizationPercent
                    });
                }

                var result = new WorkloadScalabilityResult
                {
                    WorkloadSize = workloadSize,
                    AverageTimeMs = times.Average(),
                    MinTimeMs = times.Min(),
                    MaxTimeMs = times.Max(),
                    StandardDeviation = CalculateStandardDeviation(times),
                    Throughput = workloadSize / times.Average(),
                    AverageResourceUsage = new ResourceUsage
                    {
                        CpuUsagePercent = resourceUsages.Average(r => r.CpuUsagePercent),
                        MemoryUsageMB = resourceUsages.Average(r => r.MemoryUsageMB),
                        GpuUtilizationPercent = resourceUsages.Average(r => r.GpuUtilizationPercent)
                    }
                };
                
                scalabilityResults.Add(result);
                
                _output.WriteLine($"Workload {workloadSize:N0}: {result.AverageTimeMs:F2}ms, {result.Throughput:F0} ops/ms, " +
                                $"CPU: {result.AverageResourceUsage.CpuUsagePercent:F1}%, " +
                                $"GPU: {result.AverageResourceUsage.GpuUtilizationPercent:F1}%");
                
                perfContext.Checkpoint($"Completed workload size {workloadSize:N0}");
            }
            finally
            {
                await compiledKernel.DisposeAsync();
            }
        }

        // Analyze scaling trends
        AnalyzeScalingTrends(scalabilityResults);
        IdentifyPerformanceBreakpoints(scalabilityResults);
        ValidateScalingEfficiency(scalabilityResults);
    }

    #endregion

    #region Multi-Threading Scalability

    [Theory]
    [MemberData(nameof(GetThreadScalabilityData))]
    [Trait("Category", TestCategories.Concurrency)]
    [Trait("Category", TestCategories.Performance)]
    public async Task ExecuteKernel_ScalesWithThreadCount_ShowsParallelEfficiency(int threadCount, string description)
    {
        // Arrange
        using var perfContext = CreatePerformanceContext($"Thread Scalability - {description}");
        
        const int workloadSize = 1_000_000; // Fixed workload size
        const int executionsPerThread = 20;
        
        var kernel = CreateThreadScalableKernel("parallel_execution", workloadSize);
        var compiledKernels = new List<ICompiledKernel>();
        
        // Pre-compile kernels for each thread to avoid compilation overhead
        for (var i = 0; i < threadCount; i++)
        {
            compiledKernels.Add(await _accelerator.CompileKernelAsync(kernel, null, _cts.Token));
        }
        
        perfContext.Checkpoint($"Pre-compiled {threadCount} kernels");
        
        var threadResults = new ConcurrentQueue<ThreadExecutionResult>();
        var overallSw = Stopwatch.StartNew();

        try
        {
            // Act - Execute kernels concurrently across threads
            var threadTasks = Enumerable.Range(0, threadCount)
                .Select(async threadId =>
                {
                    var compiledKernel = compiledKernels[threadId];
                    var arguments = CreateKernelArguments(workloadSize);
                    var executionTimes = new List<double>();
                    
                    // Warmup for this thread
                    await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                    await _accelerator.SynchronizeAsync(_cts.Token);
                    
                    // Execute multiple times per thread
                    for (var i = 0; i < executionsPerThread; i++)
                    {
                        var sw = Stopwatch.StartNew();
                        await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                        await _accelerator.SynchronizeAsync(_cts.Token);
                        sw.Stop();
                        
                        executionTimes.Add(sw.Elapsed.TotalMilliseconds);
                    }
                    
                    threadResults.Enqueue(new ThreadExecutionResult
                    {
                        ThreadId = threadId,
                        ExecutionTimes = executionTimes.ToArray(),
                        AverageTimeMs = executionTimes.Average(),
                        TotalExecutions = executionsPerThread
                    });
                });

            await Task.WhenAll(threadTasks);
            overallSw.Stop();
            
            perfContext.Checkpoint("All threads completed");

            // Analyze multi-threading performance
            var results = threadResults.ToList();
            var allExecutionTimes = results.SelectMany(r => r.ExecutionTimes).ToList();
            var totalExecutions = results.Sum(r => r.TotalExecutions);
            var overallThroughput = totalExecutions / overallSw.Elapsed.TotalSeconds;
            var avgExecutionTime = allExecutionTimes.Average();
            var threadEfficiency = CalculateThreadEfficiency(results, threadCount);
            
            _output.WriteLine($"Multi-Threading Scalability - {description}:");
            _output.WriteLine($"  Thread count: {threadCount}");
            _output.WriteLine($"  Total executions: {totalExecutions:N0}");
            _output.WriteLine($"  Overall duration: {overallSw.Elapsed.TotalMilliseconds:F0}ms");
            _output.WriteLine($"  Overall throughput: {overallThroughput:F1} executions/sec");
            _output.WriteLine($"  Average execution time: {avgExecutionTime:F2}ms");
            _output.WriteLine($"  Thread efficiency: {threadEfficiency:F2} (1.0 = perfect scaling)");
            
            // Per-thread analysis
            for (var i = 0; i < Math.Min(threadCount, 8); i++) // Limit output for readability
            {
                var threadResult = results[i];
                _output.WriteLine($"  Thread {i}: Avg {threadResult.AverageTimeMs:F2}ms, " +
                                $"Range {threadResult.ExecutionTimes.Min():F2}-{threadResult.ExecutionTimes.Max():F2}ms");
            }
            
            // Store thread scaling metrics
            ScalabilityMetrics.RecordThreadMetrics(threadCount, overallThroughput, threadEfficiency);
            
            // Performance assertions
            threadEfficiency.Should().BeGreaterThan(0.5, $"Thread efficiency should be reasonable for {threadCount} threads");
            overallThroughput.Should().BeGreaterThan(threadCount * 5, "Should show throughput scaling with thread count");
            
            if (threadCount <= 8) // Expect better efficiency with fewer threads
            {
                threadEfficiency.Should().BeGreaterThan(0.7, "Should maintain high efficiency with moderate thread counts");
            }
        }
        finally
        {
            // Cleanup
            foreach (var kernel in compiledKernels)
            {
                await kernel.DisposeAsync();
            }
        }
    }

    [Fact]
    [Trait("Category", TestCategories.Performance)]
    [Trait("Category", TestCategories.Concurrency)]
    public async Task ExecuteKernel_MeasuresResourceContention_UnderHighConcurrency()
    {
        // Arrange
        using var perfContext = CreatePerformanceContext("Resource Contention Analysis");
        
        const int maxThreads = 32;
        const int workloadSize = 500_000;
        const int executionsPerThread = 10;
        
        var contentionResults = new List<ContentionResult>();
        
        // Test increasing levels of concurrency
        var threadCounts = new[] { 1, 2, 4, 8, 16, 32 };
        
        foreach (var threadCount in threadCounts)
        {
            var kernel = CreateResourceIntensiveKernel("resource_contention", workloadSize);
            var compiledKernels = new List<ICompiledKernel>();
            
            // Pre-compile kernels
            for (var i = 0; i < threadCount; i++)
            {
                compiledKernels.Add(await _accelerator.CompileKernelAsync(kernel, null, _cts.Token));
            }
            
            try
            {
                var preTestResources = _accelerator.GetResourceUsage();
                var executionResults = new ConcurrentQueue<ContentionExecutionResult>();
                var overallSw = Stopwatch.StartNew();
                
                // Act - Execute with current thread count
                var tasks = Enumerable.Range(0, threadCount)
                    .Select(async threadId =>
                    {
                        var compiledKernel = compiledKernels[threadId];
                        var arguments = CreateKernelArguments(workloadSize);
                        var waitTimes = new List<double>();
                        var executionTimes = new List<double>();
                        
                        for (var i = 0; i < executionsPerThread; i++)
                        {
                            var waitSw = Stopwatch.StartNew();
                            // Simulate resource acquisition wait
                            await Task.Delay(_accelerator.GetContentionDelay(), _cts.Token);
                            waitSw.Stop();
                            waitTimes.Add(waitSw.Elapsed.TotalMicroseconds);
                            
                            var execSw = Stopwatch.StartNew();
                            await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                            await _accelerator.SynchronizeAsync(_cts.Token);
                            execSw.Stop();
                            executionTimes.Add(execSw.Elapsed.TotalMilliseconds);
                        }
                        
                        executionResults.Enqueue(new ContentionExecutionResult
                        {
                            ThreadId = threadId,
                            AverageWaitTimeMicros = waitTimes.Average(),
                            AverageExecutionTimeMs = executionTimes.Average(),
                            MaxWaitTimeMicros = waitTimes.Max(),
                            MaxExecutionTimeMs = executionTimes.Max()
                        });
                    });
                    
                await Task.WhenAll(tasks);
                overallSw.Stop();
                
                var postTestResources = _accelerator.GetResourceUsage();
                
                // Analyze contention results
                var results = executionResults.ToList();
                var avgWaitTime = results.Average(r => r.AverageWaitTimeMicros);
                var maxWaitTime = results.Max(r => r.MaxWaitTimeMicros);
                var avgExecutionTime = results.Average(r => r.AverageExecutionTimeMs);
                var resourceUtilization = postTestResources.GpuUtilizationPercent - preTestResources.GpuUtilizationPercent;
                
                var contentionResult = new ContentionResult
                {
                    ThreadCount = threadCount,
                    AverageWaitTimeMicros = avgWaitTime,
                    MaxWaitTimeMicros = maxWaitTime,
                    AverageExecutionTimeMs = avgExecutionTime,
                    ResourceUtilizationPercent = resourceUtilization,
                    OverallDurationMs = overallSw.Elapsed.TotalMilliseconds
                };
                
                contentionResults.Add(contentionResult);
                
                _output.WriteLine($"Threads {threadCount:2}: Wait {avgWaitTime:6.1f}μs, " +
                                $"Exec {avgExecutionTime:6.2f}ms, Util {resourceUtilization:5.1f}%");
                
                perfContext.Checkpoint($"Completed contention test with {threadCount} threads");
            }
            finally
            {
                foreach (var kernel in compiledKernels)
                {
                    await kernel.DisposeAsync();
                }
            }
        }
        
        // Analyze contention trends
        AnalyzeContentionTrends(contentionResults);
    }

    #endregion

    #region Resource Utilization Analysis

    [Fact]
    [Trait("Category", TestCategories.Performance)]
    [Trait("Category", TestCategories.MemoryIntensive)]
    public async Task ExecuteKernel_MonitorsResourceUtilization_AcrossWorkloadSizes()
    {
        // Arrange
        using var perfContext = CreatePerformanceContext("Resource Utilization Monitoring");
        var utilizationResults = new List<ResourceUtilizationResult>();
        
        var testWorkloads = new[] { 10_000, 100_000, 1_000_000, 5_000_000 };
        
        foreach (var workloadSize in testWorkloads)
        {
            var kernel = CreateResourceMonitoringKernel("resource_monitor", workloadSize);
            var compiledKernel = await _accelerator.CompileKernelAsync(kernel, null, _cts.Token);
            var arguments = CreateKernelArguments(workloadSize);
            
            const int monitoringDuration = 5; // seconds
            const int samplingIntervalMs = 100;
            
            var resourceSamples = new List<ResourceUsage>();
            var baselineUsage = _accelerator.GetResourceUsage();
            
            try
            {
                // Warmup
                await compiledKernel.ExecuteAsync(arguments, _cts.Token);
                await _accelerator.SynchronizeAsync(_cts.Token);
                
                // Act - Monitor resource usage during continuous execution
                var monitoringCts = new CancellationTokenSource(TimeSpan.FromSeconds(monitoringDuration));
                var executionTask = ContinuousExecution(compiledKernel, arguments, monitoringCts.Token);
                
                // Resource monitoring loop
                var monitoringTask = Task.Run(async () =>
                {
                    while (!monitoringCts.Token.IsCancellationRequested)
                    {
                        var usage = _accelerator.GetResourceUsage();
                        resourceSamples.Add(usage);
                        
                        try
                        {
                            await Task.Delay(samplingIntervalMs, monitoringCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                });
                
                await Task.WhenAll(executionTask, monitoringTask);
                
                // Analyze resource utilization
                var utilizationResult = new ResourceUtilizationResult
                {
                    WorkloadSize = workloadSize,
                    AverageCpuUsage = resourceSamples.Average(s => s.CpuUsagePercent) - baselineUsage.CpuUsagePercent,
                    PeakCpuUsage = resourceSamples.Max(s => s.CpuUsagePercent) - baselineUsage.CpuUsagePercent,
                    AverageMemoryUsage = resourceSamples.Average(s => s.MemoryUsageMB) - baselineUsage.MemoryUsageMB,
                    PeakMemoryUsage = resourceSamples.Max(s => s.MemoryUsageMB) - baselineUsage.MemoryUsageMB,
                    AverageGpuUtilization = resourceSamples.Average(s => s.GpuUtilizationPercent),
                    PeakGpuUtilization = resourceSamples.Max(s => s.GpuUtilizationPercent),
                    SampleCount = resourceSamples.Count
                };
                
                utilizationResults.Add(utilizationResult);
                
                _output.WriteLine($"Workload {workloadSize:N0}:");
                _output.WriteLine($"  CPU: Avg {utilizationResult.AverageCpuUsage:F1}%, Peak {utilizationResult.PeakCpuUsage:F1}%");
                _output.WriteLine($"  Memory: Avg {utilizationResult.AverageMemoryUsage:F1}MB, Peak {utilizationResult.PeakMemoryUsage:F1}MB");
                _output.WriteLine($"  GPU: Avg {utilizationResult.AverageGpuUtilization:F1}%, Peak {utilizationResult.PeakGpuUtilization:F1}%");
                _output.WriteLine($"  Samples: {utilizationResult.SampleCount}");
                
                perfContext.Checkpoint($"Completed resource monitoring for workload {workloadSize:N0}");
            }
            finally
            {
                await compiledKernel.DisposeAsync();
            }
        }
        
        // Validate resource utilization patterns
        ValidateResourceUtilizationTrends(utilizationResults);
    }

    #endregion

    #region Analysis Methods

    private void AnalyzeScalingTrends(List<WorkloadScalabilityResult> results)
    {
        _output.WriteLine("\n=== Workload Scaling Trend Analysis ===");
        
        // Calculate scaling factors
        for (var i = 1; i < results.Count; i++)
        {
            var current = results[i];
            var previous = results[i - 1];
            
            var workloadRatio = (double)current.WorkloadSize / previous.WorkloadSize;
            var timeRatio = current.AverageTimeMs / previous.AverageTimeMs;
            var scalingEfficiency = workloadRatio / timeRatio;
            
            _output.WriteLine($"Workload {previous.WorkloadSize:N0} → {current.WorkloadSize:N0}:");
            _output.WriteLine($"  Workload ratio: {workloadRatio:F1}x");
            _output.WriteLine($"  Time ratio: {timeRatio:F2}x");
            _output.WriteLine($"  Scaling efficiency: {scalingEfficiency:F2} (1.0 = linear)");
            
            if (scalingEfficiency < 0.8)
            {
                _output.WriteLine($"  ⚠️  Poor scaling efficiency detected");
            }
            else if (scalingEfficiency > 0.95)
            {
                _output.WriteLine($"  ✅ Excellent scaling efficiency");
            }
        }
    }

    private void IdentifyPerformanceBreakpoints(List<WorkloadScalabilityResult> results)
    {
        _output.WriteLine("\n=== Performance Breakpoint Analysis ===");
        
        // Look for significant performance degradation points
        var throughputs = results.Select(r => r.Throughput).ToList();
        var peakThroughput = throughputs.Max();
        
        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            var throughputRatio = result.Throughput / peakThroughput;
            
            if (throughputRatio < 0.5)
            {
                _output.WriteLine($"⚠️  Performance breakpoint at workload {result.WorkloadSize:N0}: " +
                                $"Throughput dropped to {throughputRatio * 100:F1}% of peak");
            }
        }
        
        // Identify memory scaling issues
        var memoryUsages = results.Select(r => r.AverageResourceUsage.MemoryUsageMB).ToList();
        for (var i = 1; i < results.Count; i++)
        {
            var memoryRatio = memoryUsages[i] / memoryUsages[i - 1];
            var workloadRatio = (double)results[i].WorkloadSize / results[i - 1].WorkloadSize;
            
            if (memoryRatio > workloadRatio * 1.5)
            {
                _output.WriteLine($"⚠️  Memory scaling issue at workload {results[i].WorkloadSize:N0}: " +
                                $"Memory growth ({memoryRatio:F1}x) exceeds workload growth ({workloadRatio:F1}x)");
            }
        }
    }

    private void ValidateScalingEfficiency(List<WorkloadScalabilityResult> results)
    {
        var baseResult = results.First();
        var largestResult = results.Last();
        
        var workloadRatio = (double)largestResult.WorkloadSize / baseResult.WorkloadSize;
        var timeRatio = largestResult.AverageTimeMs / baseResult.AverageTimeMs;
        var overallEfficiency = workloadRatio / timeRatio;
        
        _output.WriteLine($"\n=== Overall Scaling Validation ===");
        _output.WriteLine($"Workload range: {baseResult.WorkloadSize:N0} → {largestResult.WorkloadSize:N0} ({workloadRatio:F1}x)");
        _output.WriteLine($"Time scaling: {baseResult.AverageTimeMs:F2}ms → {largestResult.AverageTimeMs:F2}ms ({timeRatio:F2}x)");
        _output.WriteLine($"Overall efficiency: {overallEfficiency:F2}");
        
        // Assertions for overall scaling quality
        overallEfficiency.Should().BeGreaterThan(0.5, "Overall scaling efficiency should be reasonable");
        timeRatio.Should().BeLessThan(workloadRatio * 2, "Execution time should not scale worse than quadratically");
    }

    private static double CalculateThreadEfficiency(List<ThreadExecutionResult> results, int threadCount)
    {
        if (threadCount == 1) return 1.0;
        
        var avgThroughputPerThread = results.Average(r => r.TotalExecutions / r.ExecutionTimes.Sum() * 1000);
        var totalThroughput = avgThroughputPerThread * threadCount;
        
        // Efficiency compared to single-threaded baseline (stored in metrics)
        var singleThreadThroughput = ScalabilityMetrics.GetSingleThreadThroughput();
        if (singleThreadThroughput > 0)
        {
            return (totalThroughput / singleThreadThroughput) / threadCount;
        }
        
        return 0.8; // Default reasonable efficiency
    }

    private void AnalyzeContentionTrends(List<ContentionResult> results)
    {
        _output.WriteLine("\n=== Resource Contention Analysis ===");
        
        var baselineWait = results.First().AverageWaitTimeMicros;
        
        foreach (var result in results.Skip(1))
        {
            var contentionFactor = result.AverageWaitTimeMicros / baselineWait;
            var efficiencyLoss = 1.0 - (1.0 / contentionFactor);
            
            _output.WriteLine($"Threads {result.ThreadCount}: Contention factor {contentionFactor:F2}, " +
                            $"Efficiency loss {efficiencyLoss * 100:F1}%");
            
            if (contentionFactor > 3.0)
            {
                _output.WriteLine($"  ⚠️  High contention detected");
            }
        }
    }

    private void ValidateResourceUtilizationTrends(List<ResourceUtilizationResult> results)
    {
        _output.WriteLine("\n=== Resource Utilization Validation ===");
        
        foreach (var result in results)
        {
            // CPU utilization should increase with workload but not exceed reasonable bounds
            result.AverageCpuUsage.Should().BeLessThan(90, $"CPU usage should be reasonable for workload {result.WorkloadSize:N0}");
            result.AverageCpuUsage.Should().BeGreaterThan(5, "Should show measurable CPU utilization");
            
            // Memory usage should scale reasonably with workload size
            var expectedMemoryMB = result.WorkloadSize * sizeof(float) / (1024.0 * 1024.0);
            result.PeakMemoryUsage.Should().BeGreaterThan(expectedMemoryMB * 0.5, "Should allocate reasonable memory for data");
            result.PeakMemoryUsage.Should().BeLessThan(expectedMemoryMB * 10, "Memory usage should not be excessive");
            
            // GPU utilization should be meaningful for GPU workloads
            if (result.WorkloadSize > 10_000)
            {
                result.AverageGpuUtilization.Should().BeGreaterThan(10, "Should show GPU utilization for larger workloads");
            }
        }
    }

    #endregion

    #region Helper Methods

    public static IEnumerable<object[]> GetWorkloadScalabilityData()
    {
        yield return new object[] { 1_000, "1K elements" };
        yield return new object[] { 10_000, "10K elements" };
        yield return new object[] { 100_000, "100K elements" };
        yield return new object[] { 1_000_000, "1M elements" };
        yield return new object[] { 10_000_000, "10M elements" };
    }
    
    public static IEnumerable<object[]> GetThreadScalabilityData()
    {
        yield return new object[] { 1, "Single thread" };
        yield return new object[] { 2, "2 threads" };
        yield return new object[] { 4, "4 threads" };
        yield return new object[] { 8, "8 threads" };
        yield return new object[] { 16, "16 threads" };
    }

    private KernelDefinition CreateScalableKernel(string name, int workloadSize)
    {
        return new KernelDefinition
        {
            Name = name,
            Source = @"
                __kernel void {name}(__global float* data, int size)
                {{
                    int gid = get_global_id(0);
                    if (gid < size) {{
                        float value = data[gid];
                        for (int i = 0; i < 10; i++) {{
                            value = sqrt(value * value + 1.0f);
                        }}
                        data[gid] = value;
                    }}
                }}
                ",
            Language = KernelLanguage.OpenCL,
            EntryPoint = name
        };
    }

    private KernelDefinition CreateThreadScalableKernel(string name, int workloadSize) => CreateScalableKernel(name, workloadSize);

    private KernelDefinition CreateResourceIntensiveKernel(string name, int workloadSize)
    {
        return new KernelDefinition
        {
            Name = name,
            Source = @"
                __kernel void {name}(__global float* input, __global float* output, int size)
                {{
                    int gid = get_global_id(0);
                    if (gid < size) {{
                        float sum = 0.0f;
                        for (int i = 0; i < 100; i++) {{
                            sum += sin(input[gid] + i) * cos(input[gid] + i);
                        }}
                        output[gid] = sum;
                    }}
                }}
                ",
            Language = KernelLanguage.OpenCL,
            EntryPoint = name
        };
    }

    private KernelDefinition CreateResourceMonitoringKernel(string name, int workloadSize) => CreateResourceIntensiveKernel(name, workloadSize);

    private static KernelArguments CreateKernelArguments(int size)
    {
        var data = GenerateRandomFloats(size);
        return new KernelArguments(new object[] { data, new float[size], size });
    }

    private async Task ContinuousExecution(ICompiledKernel kernel, KernelArguments arguments, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await kernel.ExecuteAsync(arguments, cancellationToken);
                await _accelerator.SynchronizeAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static double CalculateStandardDeviation(IEnumerable<double> values)
    {
        var valuesList = values.ToList();
        var mean = valuesList.Average();
        var sumSquaredDiffs = valuesList.Sum(v => Math.Pow(v - mean, 2));
        return Math.Sqrt(sumSquaredDiffs / valuesList.Count);
    }

    private static double CalculateComputationalEfficiency(int workloadSize, double timeMs)
    {
        // Estimate FLOPS based on kernel operations (simplified)
        var operationsPerElement = 20; // Approximate operations in scalable kernel
        var totalOperations = workloadSize * operationsPerElement;
        var mflops = (totalOperations / 1_000_000.0) / (timeMs / 1000.0);
        return mflops / timeMs; // MFLOPS per millisecond
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Dispose();
            _accelerator?.DisposeAsync().AsTask().Wait(1000);
        }
        base.Dispose(disposing);
    }
}

#region Support Classes and Data Structures

/// <summary>
/// Result of workload scalability testing
/// </summary>
internal class WorkloadScalabilityResult
{
    public int WorkloadSize { get; set; }
    public double AverageTimeMs { get; set; }
    public double MinTimeMs { get; set; }
    public double MaxTimeMs { get; set; }
    public double StandardDeviation { get; set; }
    public double Throughput { get; set; }
    public ResourceUsage AverageResourceUsage { get; set; } = new();
}

/// <summary>
/// Resource usage measurements
/// </summary>
internal class ResourceUsage
{
    public double CpuUsagePercent { get; set; }
    public double MemoryUsageMB { get; set; }
    public double GpuUtilizationPercent { get; set; }
}

/// <summary>
/// Result of thread execution testing
/// </summary>
internal class ThreadExecutionResult
{
    public int ThreadId { get; set; }
    public double[] ExecutionTimes { get; set; } = [];
    public double AverageTimeMs { get; set; }
    public int TotalExecutions { get; set; }
}

/// <summary>
/// Result of resource contention testing
/// </summary>
internal class ContentionResult
{
    public int ThreadCount { get; set; }
    public double AverageWaitTimeMicros { get; set; }
    public double MaxWaitTimeMicros { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public double ResourceUtilizationPercent { get; set; }
    public double OverallDurationMs { get; set; }
}

/// <summary>
/// Execution result with contention measurements
/// </summary>
internal class ContentionExecutionResult
{
    public int ThreadId { get; set; }
    public double AverageWaitTimeMicros { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public double MaxWaitTimeMicros { get; set; }
    public double MaxExecutionTimeMs { get; set; }
}

/// <summary>
/// Resource utilization analysis result
/// </summary>
internal class ResourceUtilizationResult
{
    public int WorkloadSize { get; set; }
    public double AverageCpuUsage { get; set; }
    public double PeakCpuUsage { get; set; }
    public double AverageMemoryUsage { get; set; }
    public double PeakMemoryUsage { get; set; }
    public double AverageGpuUtilization { get; set; }
    public double PeakGpuUtilization { get; set; }
    public int SampleCount { get; set; }
}

/// <summary>
/// Mock scalable accelerator for testing
/// </summary>
internal class MockScalableAccelerator : IAccelerator
{
    private readonly Random _random = new(42);
    private int _contentionLevel = 0;
    
    public AcceleratorInfo Info { get; } = new AcceleratorInfo
    {
        Id = "scalable_test_accelerator",
        Name = "Scalable Test Accelerator",
        DeviceType = "Mock",
        Vendor = "Test",
        TotalMemory = 16L * 1024 * 1024 * 1024, // 16GB
        MaxComputeUnits = 32
    };
    
    public AcceleratorType Type => AcceleratorType.CPU;
    public IUnifiedMemoryManager Memory { get; } = new MockMemoryManager();
    public AcceleratorContext Context { get; } = new AcceleratorContext();

    public ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Simulate compilation time
        var complexity = definition.Source.Length / 50;
        var delay = Task.Delay(Math.Max(10, complexity), cancellationToken);
        return new ValueTask<ICompiledKernel>(delay.ContinueWith(_ => (ICompiledKernel)new MockScalableKernel(definition.Name, this), cancellationToken));
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => new(Task.Delay(_random.Next(1, 5), cancellationToken));

    public ResourceUsage GetResourceUsage()
    {
        // Simulate resource usage that varies with contention
        var baseUsage = new ResourceUsage
        {
            CpuUsagePercent = 15 + _random.NextDouble() * 30,
            MemoryUsageMB = 100 + _random.NextDouble() * 200,
            GpuUtilizationPercent = 20 + _random.NextDouble() * 60
        };
        
        // Increase usage with contention
        baseUsage.CpuUsagePercent += _contentionLevel * 5;
        baseUsage.GpuUtilizationPercent += _contentionLevel * 10;
        
        return baseUsage;
    }
    
    public int GetContentionDelay()
    {
        _contentionLevel = Math.Min(10, _contentionLevel + 1);
        return _contentionLevel * 2; // Increasing delay with contention
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Mock scalable compiled kernel
/// </summary>
internal class MockScalableKernel : ICompiledKernel
{
    private readonly MockScalableAccelerator _accelerator;
    private readonly Random _random = new(42);
    
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; }

    public MockScalableKernel(string name, MockScalableAccelerator accelerator)
    {
        Name = name;
        _accelerator = accelerator;
    }

    public ValueTask ExecuteAsync(
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        // Simulate execution time that scales with workload
        var workloadSize = GetWorkloadSize(arguments);
        var baseTime = Math.Max(1, workloadSize / 100_000); // 1ms per 100K elements
        var variability = _random.NextDouble() * 0.3; // ±30% variability
        var executionTime = (int)(baseTime * (1 + variability));
        
        return new ValueTask(Task.Delay(executionTime, cancellationToken));
    }
    
    private static int GetWorkloadSize(KernelArguments arguments)
    {
        // Extract workload size from arguments (simplified)
        if (arguments.Count > 0 && arguments[0] is float[] array)
        {
            return array.Length;
        }
        return 10_000; // Default size
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Static class for storing scalability metrics across tests
/// </summary>
internal static class ScalabilityMetrics
{
    private static readonly Dictionary<string, object> _metrics = [];
    
    public static void RecordWorkloadMetrics(int workloadSize, double timeMs, double throughput, double efficiency)
    {
        _metrics[$"workload_{workloadSize}_time"] = timeMs;
        _metrics[$"workload_{workloadSize}_throughput"] = throughput;
        _metrics[$"workload_{workloadSize}_efficiency"] = efficiency;
    }
    
    public static void RecordThreadMetrics(int threadCount, double throughput, double efficiency)
    {
        _metrics[$"thread_{threadCount}_throughput"] = throughput;
        _metrics[$"thread_{threadCount}_efficiency"] = efficiency;
        
        if (threadCount == 1)
        {
            _metrics["single_thread_baseline"] = throughput;
        }
    }


    public static double GetSingleThreadThroughput() => _metrics.TryGetValue("single_thread_baseline", out var value) ? (double)value : 0.0;
}

#endregion
