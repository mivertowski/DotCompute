// Copyright(c) 2025 Michael Ivertowski

#pragma warning disable CA1848 // Use LoggerMessage delegates - will be migrated in future iteration

#pragma warning disable IDE0059 // Unnecessary assignment - test scaffolding
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using DotCompute.Integration.Tests.Infrastructure;

namespace DotCompute.Integration.Tests;


/// <summary>
/// Integration tests for performance validation including benchmarking,
/// throughput measurement, latency analysis, and resource utilization monitoring.
/// </summary>
[Collection("Integration")]
public sealed class PerformanceValidationIntegrationTests : ComputeWorkflowTestBase
{
    private readonly PerformanceBenchmarkSuite _benchmarkSuite;

    public PerformanceValidationIntegrationTests(ITestOutputHelper output) : base(output)
    {
        _benchmarkSuite = new PerformanceBenchmarkSuite(Logger);
    }

    [Fact]
    public async Task ThroughputBenchmark_VectorOperations_ShouldMeetPerformanceThresholds()
    {
        // Arrange
        var benchmarkSizes = new[] { 1024, 4096, 16384, 65536, 262144 };
        var throughputResults = new Dictionary<int, double>();

        // Act
        foreach (var size in benchmarkSizes)
        {
            var benchmark = await PerformanceBenchmarkSuite.CreateVectorOperationBenchmark(size);
            var result = await ExecuteComputeWorkflowAsync($"ThroughputBenchmark_{size}", benchmark);

            _ = result.Success.Should().BeTrue();

            var throughputMBps = result.Metrics?.ThroughputMBps ?? 0;
            throughputResults[size] = throughputMBps;

            Logger.LogInformation("Vector operations throughput{Size} elements): {Throughput:F2} MB/s",
                size, throughputMBps);
        }

        // Assert
        foreach (var (size, throughput) in throughputResults)
        {
            var expectedMinThroughput = CalculateExpectedMinThroughput(size, "vector_ops");
            _ = throughput.Should().BeGreaterThan(expectedMinThroughput,
                $"Throughput for {size} elements should meet minimum threshold");
        }

        // Verify throughput scaling
        ValidateThroughputScaling(throughputResults);
    }

    [Fact]
    public async Task LatencyBenchmark_SmallWorkloads_ShouldMinimizeOverhead()
    {
        // Arrange
        var smallWorkloadSizes = new[] { 16, 64, 256, 1024 };
        var latencyResults = new Dictionary<int, TimeSpan>();

        // Act
        foreach (var size in smallWorkloadSizes)
        {
            var benchmark = await PerformanceBenchmarkSuite.CreateLatencyBenchmark(size);

            // Warm up
            _ = await ExecuteComputeWorkflowAsync($"LatencyWarmup_{size}", benchmark);

            // Measure latency
            var stopwatch = Stopwatch.StartNew();
            var result = await ExecuteComputeWorkflowAsync($"LatencyBenchmark_{size}", benchmark);
            stopwatch.Stop();

            _ = result.Success.Should().BeTrue();
            latencyResults[size] = stopwatch.Elapsed;

            Logger.LogInformation("Latency benchmark{Size} elements): {Latency:F2}ms",
                size, stopwatch.Elapsed.TotalMilliseconds);
        }

        // Assert
        foreach (var (size, latency) in latencyResults)
        {
            var expectedMaxLatency = CalculateExpectedMaxLatency(size);
            _ = (latency < expectedMaxLatency).Should().BeTrue(
                $"Latency for {size} elements should be within acceptable bounds");
        }

        // Verify latency doesn't degrade significantly with size
        ValidateLatencyScaling(latencyResults);
    }

    [Fact]
    public async Task MemoryBandwidthBenchmark_DataTransfer_ShouldOptimizeTransfers()
    {
        // Arrange
        var transferSizes = new[] { 1024, 8192, 65536, 524288 }; // 1KB to 512KB
        var bandwidthResults = new Dictionary<int, MemoryBandwidthResult>();

        // Act
        foreach (var size in transferSizes)
        {
            var benchmark = await PerformanceBenchmarkSuite.CreateMemoryBandwidthBenchmark(size);
            var result = await ExecuteComputeWorkflowAsync($"MemoryBandwidthBenchmark_{size}", benchmark);

            _ = result.Success.Should().BeTrue();

            var transferTime = result.Duration;
            var dataMB = size * sizeof(float) * 2 / 1024.0 / 1024.0; // Round trip
            var bandwidth = dataMB / transferTime.TotalSeconds;

            var bandwidthResult = new MemoryBandwidthResult
            {
                DataSize = size,
                TransferTime = transferTime,
                BandwidthMBps = bandwidth,
                MemoryEfficiency = result.Metrics?.ResourceUtilization.MemoryUsagePercent ?? 0
            };

            bandwidthResults[size] = bandwidthResult;

            Logger.LogInformation("Memory bandwidth{Size} elements): {Bandwidth:F2} MB/s, " +
                                 "Efficiency: {Efficiency:F1}%",
                size, bandwidth, bandwidthResult.MemoryEfficiency);
        }

        // Assert
        foreach (var (size, bandwidthResult) in bandwidthResults)
        {
            var expectedMinBandwidth = CalculateExpectedMinBandwidth(size);
            _ = (bandwidthResult.BandwidthMBps > expectedMinBandwidth).Should().BeTrue(
                $"Memory bandwidth for {size} elements should meet minimum threshold");

            _ = bandwidthResult.MemoryEfficiency.Should().BeLessThan(90,
                "Memory usage efficiency should be reasonable");
        }

        // Verify bandwidth scaling
        ValidateBandwidthScaling(bandwidthResults);
    }

    [Fact]
    public async Task ComputeIntensityBenchmark_ComplexOperations_ShouldUtilizeResources()
    {
        // Arrange
        var complexityLevels = new[] { 1, 2, 4, 8, 16 }; // Increasing computational complexity
        var computeResults = new Dictionary<int, ComputeIntensityResult>();

        // Act
        foreach (var complexity in complexityLevels)
        {
            var benchmark = await PerformanceBenchmarkSuite.CreateComputeIntensityBenchmark(2048, complexity);
            var result = await ExecuteComputeWorkflowAsync($"ComputeIntensityBenchmark_{complexity}", benchmark);

            _ = result.Success.Should().BeTrue();

            var computeResult = new ComputeIntensityResult
            {
                ComplexityLevel = complexity,
                ExecutionTime = result.Duration,
                ThroughputMBps = result.Metrics?.ThroughputMBps ?? 0,
                ResourceUtilization = result.Metrics?.ResourceUtilization ?? new ResourceUtilization()
            };

            computeResults[complexity] = computeResult;

            Logger.LogInformation("Compute intensitylevel {Level}): {Time:F2}ms, " +
                                 "CPU: {CPU:F1}%, GPU: {GPU:F1}%",
                complexity, result.Duration.TotalMilliseconds,
                computeResult.ResourceUtilization.CpuUsagePercent,
                computeResult.ResourceUtilization.GpuUsagePercent);
        }

        // Assert
        ValidateComputeIntensityScaling(computeResults);

        // Higher complexity should show increased resource utilization
        var maxComplexity = computeResults[complexityLevels.Max()];
        _ = maxComplexity.ResourceUtilization.CpuUsagePercent.Should().BeGreaterThan(20,
            "High complexity workloads should utilize CPU resources");
    }

    [Fact]
    public async Task ConcurrentPerformanceBenchmark_ParallelWorkloads_ShouldScaleEfficiently()
    {
        // Arrange
        var concurrencyLevels = new[] { 1, 2, 4, 8 };
        var concurrencyResults = new Dictionary<int, ConcurrencyBenchmarkResult>();

        // Act
        foreach (var concurrency in concurrencyLevels)
        {
            var benchmarks = Enumerable.Range(0, concurrency)
                .Select(i => PerformanceBenchmarkSuite.CreateConcurrentBenchmark($"Concurrent_{i}", 1024))
                .ToArray();

            var stopwatch = Stopwatch.StartNew();

            var tasks = benchmarks.Select((benchmark, index) =>
                ExecuteComputeWorkflowAsync($"ConcurrentBenchmark_{concurrency}_{index}", benchmark))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            var successfulResults = results.Where(r => r.Success).ToArray();
            var totalThroughput = successfulResults.Sum(r => r.Metrics?.ThroughputMBps ?? 0);
            var averageLatency = successfulResults.Average(r => r.Duration.TotalMilliseconds);

            var concurrencyResult = new ConcurrencyBenchmarkResult
            {
                ConcurrencyLevel = concurrency,
                SuccessfulWorkflows = successfulResults.Length,
                TotalDuration = stopwatch.Elapsed,
                AggregatedThroughput = totalThroughput,
                AverageLatency = averageLatency,
                ScalingEfficiency = CalculateScalingEfficiency(concurrency, totalThroughput, concurrencyResults)
            };

            concurrencyResults[concurrency] = concurrencyResult;

            Logger.LogInformation("Concurrent benchmark{Level}x): {Success}/{Total} successful, " +
                                 "Throughput: {Throughput:F2} MB/s, Efficiency: {Efficiency:F1}%",
                concurrency, successfulResults.Length, concurrency,
                totalThroughput, concurrencyResult.ScalingEfficiency * 100);
        }

        // Assert
        ValidateConcurrencyScaling(concurrencyResults);
    }

    [Theory]
    [InlineData("MatrixMultiplication", 256)]
    [InlineData("FFT", 1024)]
    [InlineData("Convolution", 512)]
    [InlineData("Reduction", 4096)]
    public async Task AlgorithmSpecificBenchmark_CommonOperations_ShouldMeetBaselines(
        string algorithmType, int dataSize)
    {
        // Arrange
        var benchmark = await PerformanceBenchmarkSuite.CreateAlgorithmSpecificBenchmark(algorithmType, dataSize);

        // Act
        // Run multiple iterations for statistical accuracy
        var iterations = 5;
        var iterationResults = new List<WorkflowExecutionResult>();

        for (var i = 0; i < iterations; i++)
        {
            var result = await ExecuteComputeWorkflowAsync($"{algorithmType}Benchmark_{i}", benchmark);
            _ = result.Success.Should().BeTrue();
            iterationResults.Add(result);
        }

        // Assert
        var avgThroughput = iterationResults.Average(r => r.Metrics?.ThroughputMBps ?? 0);
        var avgLatency = iterationResults.Average(r => r.Duration.TotalMilliseconds);
        var minLatency = iterationResults.Min(r => r.Duration.TotalMilliseconds);
        var maxLatency = iterationResults.Max(r => r.Duration.TotalMilliseconds);

        Logger.LogInformation("{Algorithm} benchmark{Size}): Avg throughput: {AvgThroughput:F2} MB/s, " +
                             "Avg latency: {AvgLatency:F2}ms, Range: [{Min:F2}, {Max:F2}]ms",
            algorithmType, dataSize, avgThroughput, avgLatency, minLatency, maxLatency);

        var expectedBaseline = GetAlgorithmBaseline(algorithmType, dataSize);
        _ = (avgThroughput > expectedBaseline.MinThroughput).Should().BeTrue(
            $"{algorithmType} should meet minimum throughput baseline");

        _ = (avgLatency < expectedBaseline.MaxLatency).Should().BeTrue(
            $"{algorithmType} should meet maximum latency baseline");

        // Verify consistency(low variance)
        var latencyVariance = maxLatency - minLatency;
        _ = (latencyVariance < avgLatency * 0.5).Should().BeTrue(
            "Algorithm performance should be consistent across iterations");
    }

    [Fact]
    public async Task EndToEndPerformanceBenchmark_RealWorldPipeline_ShouldMeetTargets()
    {
        // Arrange
        var realWorldPipeline = await PerformanceBenchmarkSuite.CreateRealWorldPipelineBenchmark();

        // Act
        var result = await ExecuteComputeWorkflowAsync("EndToEndPerformance", realWorldPipeline);

        // Assert
        _ = result.Success.Should().BeTrue();
        _ = result.ExecutionResults.Should().NotBeEmpty();

        // Verify overall pipeline performance
        var pipelineMetrics = new PipelinePerformanceMetrics
        {
            TotalDuration = result.Duration,
            StageCount = result.ExecutionResults.Count,
            TotalThroughput = result.Metrics?.ThroughputMBps ?? 0,
            ResourceUtilization = result.Metrics?.ResourceUtilization ?? new ResourceUtilization(),
            StageLatencies = [.. result.ExecutionResults.Values.Select(s => s.Duration.TotalMilliseconds)]
        };

        ValidatePipelinePerformance(pipelineMetrics);

        Logger.LogInformation("End-to-end pipeline: {Stages} stages, {Duration:F2}ms total, " +
                             "{Throughput:F2} MB/s overall throughput",
            pipelineMetrics.StageCount, pipelineMetrics.TotalDuration.TotalMilliseconds,
            pipelineMetrics.TotalThroughput);
    }

    [Fact]
    public async Task PerformanceRegression_BaselineComparison_ShouldNotRegress()
    {
        // Arrange
        var regressionTestSuite = await PerformanceBenchmarkSuite.CreateRegressionTestSuite();
        var currentResults = new Dictionary<string, double>();
        var baselineResults = LoadPerformanceBaselines();

        // Act
        foreach (var (testName, testWorkflow) in regressionTestSuite)
        {
            var result = await ExecuteComputeWorkflowAsync($"Regression_{testName}", testWorkflow);
            _ = result.Success.Should().BeTrue();

            currentResults[testName] = result.Metrics?.ThroughputMBps ?? 0;
        }

        // Assert
        var regressions = new List<string>();
        var improvements = new List<string>();

        foreach (var (testName, currentThroughput) in currentResults)
        {
            if (baselineResults.TryGetValue(testName, out var baselineThroughput))
            {
                var performanceRatio = currentThroughput / baselineThroughput;

                if (performanceRatio < 0.9) // 10% degradation threshold
                {
                    regressions.Add($"{testName}: {performanceRatio:P1} of baseline");
                }
                else if (performanceRatio > 1.1) // 10% improvement threshold
                {
                    improvements.Add($"{testName}: {performanceRatio:P1} of baseline");
                }

                Logger.LogInformation("Regression test {Test}: {Current:F2} MB/s vs {Baseline:F2} MB/s " +
                                     "baseline({Ratio:P1})",
                    testName, currentThroughput, baselineThroughput, performanceRatio);
            }
        }

        if (regressions.Count != 0)
        {
            Logger.LogWarning("Performance regressions detected: {Regressions}",
                string.Join(", ", regressions));
        }

        if (improvements.Count != 0)
        {
            Logger.LogInformation("Performance improvements detected: {Improvements}",
                string.Join(", ", improvements));
        }

        _ = regressions.Should().BeEmpty("No performance regressions should be detected");
    }

    // Helper methods

    private static double CalculateExpectedMinThroughput(int dataSize, string operationType)
    {
        return operationType switch
        {
            "vector_ops" => Math.Max(10, dataSize / 1000.0), // Minimum 10 MB/s, scales with size
            "matrix_ops" => Math.Max(5, dataSize / 2000.0),
            "memory_transfer" => Math.Max(50, dataSize / 500.0),
            _ => 1.0
        };
    }

    private static TimeSpan CalculateExpectedMaxLatency(int dataSize)
    {
        // Maximum acceptable latency scales with data size
        var baseLatencyMs = Math.Max(1, dataSize / 1000.0);
        return TimeSpan.FromMilliseconds(baseLatencyMs + 10); // Base latency + 10ms overhead
    }

    private static double CalculateExpectedMinBandwidth(int dataSize)
        // Expected minimum memory bandwidth scales with transfer size
        => Math.Max(20, Math.Min(100, dataSize / 100.0));

    private static double CalculateScalingEfficiency(int concurrency, double totalThroughput,
        Dictionary<int, ConcurrencyBenchmarkResult> previousResults)
    {
        if (concurrency <= 1 || !previousResults.TryGetValue(1, out var baselineResult))
            return 1.0;

        var baselineThroughput = baselineResult.AggregatedThroughput;
        var expectedThroughput = baselineThroughput * concurrency;
        return totalThroughput / expectedThroughput;
    }

    private static void ValidateThroughputScaling(Dictionary<int, double> throughputResults)
    {
        var sizes = throughputResults.Keys.OrderBy(k => k).ToArray();

        for (var i = 1; i < sizes.Length; i++)
        {
            var smallSize = sizes[i - 1];
            var largeSize = sizes[i];

            var smallThroughput = throughputResults[smallSize];
            var largeThroughput = throughputResults[largeSize];

            // Larger workloads should generally have better or similar throughput
            if (largeSize >= smallSize * 4) // Only compare significantly larger workloads
            {
                _ = (largeThroughput >= smallThroughput * 0.8).Should().BeTrue(
                    $"Throughput should scale reasonably from {smallSize} to {largeSize} elements");
            }
        }
    }

    private static void ValidateLatencyScaling(Dictionary<int, TimeSpan> latencyResults)
    {
        var sizes = latencyResults.Keys.OrderBy(k => k).ToArray();

        for (var i = 1; i < sizes.Length; i++)
        {
            var smallSize = sizes[i - 1];
            var largeSize = sizes[i];

            var smallLatency = latencyResults[smallSize].TotalMilliseconds;
            var largeLatency = latencyResults[largeSize].TotalMilliseconds;

            // Latency should scale sublinearly with data size
            var expectedMaxLatency = smallLatency * Math.Pow(largeSize / (double)smallSize, 0.7);
            _ = (largeLatency < expectedMaxLatency).Should().BeTrue(
                $"Latency should scale sublinearly from {smallSize} to {largeSize} elements");
        }
    }

    private static void ValidateBandwidthScaling(Dictionary<int, MemoryBandwidthResult> bandwidthResults)
    {
        var maxBandwidth = bandwidthResults.Values.Max(r => r.BandwidthMBps);
        var minBandwidth = bandwidthResults.Values.Min(r => r.BandwidthMBps);

        // Bandwidth variance should be reasonable
        var bandwidthVariance = (maxBandwidth - minBandwidth) / maxBandwidth;
        _ = bandwidthVariance.Should().BeLessThan(0.8, "Memory bandwidth should be relatively consistent");
    }

    private static void ValidateComputeIntensityScaling(Dictionary<int, ComputeIntensityResult> computeResults)
    {
        var complexityLevels = computeResults.Keys.OrderBy(k => k).ToArray();

        for (var i = 1; i < complexityLevels.Length; i++)
        {
            var lowComplexity = complexityLevels[i - 1];
            var highComplexity = complexityLevels[i];

            var lowTime = computeResults[lowComplexity].ExecutionTime.TotalMilliseconds;
            var highTime = computeResults[highComplexity].ExecutionTime.TotalMilliseconds;

            // Higher complexity should take more time, but not excessively
            var complexityRatio = highComplexity / (double)lowComplexity;
            var timeRatio = highTime / lowTime;

            _ = timeRatio.Should().BeGreaterThan(0.8,
                $"Higher complexity({highComplexity}) should take more time than lower complexity({lowComplexity})");
            _ = (timeRatio < complexityRatio * 1.5).Should().BeTrue(
                "Execution time should scale reasonably with computational complexity");
        }
    }

    private static void ValidateConcurrencyScaling(Dictionary<int, ConcurrencyBenchmarkResult> concurrencyResults)
    {
        var baseline = concurrencyResults[1];

        foreach (var (concurrency, result) in concurrencyResults.Skip(1))
        {
            // Scaling efficiency should be reasonable
            _ = result.ScalingEfficiency.Should().BeGreaterThan(0.3,
                $"Concurrency level {concurrency} should show reasonable scaling efficiency");

            // Success rate should remain high
            var successRate = (double)result.SuccessfulWorkflows / concurrency;
            _ = successRate.Should().BeGreaterThanOrEqualTo(0.8,
                $"Success rate should remain high at concurrency level {concurrency}");
        }
    }

    private static AlgorithmBaseline GetAlgorithmBaseline(string algorithmType, int dataSize)
    {
        return algorithmType switch
        {
            "MatrixMultiplication" => new AlgorithmBaseline
            {
                MinThroughput = Math.Max(5, dataSize / 1000.0),
                MaxLatency = Math.Max(100, dataSize / 10.0)
            },
            "FFT" => new AlgorithmBaseline
            {
                MinThroughput = Math.Max(20, dataSize / 500.0),
                MaxLatency = Math.Max(50, dataSize / 20.0)
            },
            "Convolution" => new AlgorithmBaseline
            {
                MinThroughput = Math.Max(15, dataSize / 800.0),
                MaxLatency = Math.Max(75, dataSize / 15.0)
            },
            "Reduction" => new AlgorithmBaseline
            {
                MinThroughput = Math.Max(50, dataSize / 200.0),
                MaxLatency = Math.Max(25, dataSize / 40.0)
            },
            _ => new AlgorithmBaseline { MinThroughput = 1, MaxLatency = 1000 }
        };
    }

    private static void ValidatePipelinePerformance(PipelinePerformanceMetrics metrics)
    {
        // Overall pipeline should be reasonably efficient
        _ = (metrics.TotalThroughput > 10).Should().BeTrue();

        // No single stage should dominate the pipeline
        var maxStageLatency = metrics.StageLatencies.Max();
        var avgStageLatency = metrics.StageLatencies.Average();

        _ = (maxStageLatency < avgStageLatency * 3).Should().BeTrue(
            "No single stage should be a significant bottleneck");

        // Resource utilization should be reasonable
        _ = (metrics.ResourceUtilization.CpuUsagePercent > 10).Should().BeTrue();
        _ = (metrics.ResourceUtilization.MemoryUsagePercent < 90).Should().BeTrue();
    }

    private static Dictionary<string, double> LoadPerformanceBaselines()
    {
        // In a real implementation, this would load from a baseline file or database
        return new Dictionary<string, double>
        {
            ["VectorAdd"] = 25.0,
            ["MatrixMultiply"] = 15.0,
            ["FFT"] = 30.0,
            ["Convolution"] = 20.0,
            ["Reduction"] = 40.0,
            ["MemoryTransfer"] = 100.0
        };
    }
}

// Helper classes and data structures

public class PerformanceBenchmarkSuite(ILogger logger)
{
    public static Task<ComputeWorkflowDefinition> CreateVectorOperationBenchmark(int size)
    {
        return Task.FromResult(new ComputeWorkflowDefinition
        {
            Name = $"VectorOperationBenchmark_{size}",
            Kernels =
            [
                new WorkflowKernel
            {
                Name = "vector_benchmark",
                SourceCode = BenchmarkKernels.VectorOperations,
                CompilationOptions = new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum }
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "inputA", Data = ComputeWorkflowTestBase.TestDataGenerators.GenerateFloatArray(size) },
            new WorkflowInput { Name = "inputB", Data = ComputeWorkflowTestBase.TestDataGenerators.GenerateFloatArray(size) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "output", Size = size }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "benchmark_stage",
                Order = 1,
                KernelName = "vector_benchmark",
                ArgumentNames = ["inputA", "inputB", "output"]
            }
            ]
        });
    }

    public static Task<ComputeWorkflowDefinition> CreateLatencyBenchmark(int size)
    {
        return Task.FromResult(new ComputeWorkflowDefinition
        {
            Name = $"LatencyBenchmark_{size}",
            Kernels =
            [
                new WorkflowKernel
            {
                Name = "latency_test",
                SourceCode = BenchmarkKernels.MinimalOperation
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = ComputeWorkflowTestBase.TestDataGenerators.GenerateFloatArray(size) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "output", Size = size }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "latency_stage",
                Order = 1,
                KernelName = "latency_test",
                ArgumentNames = ["input", "output"]
            }
            ]
        });
    }

    public static Task<ComputeWorkflowDefinition> CreateMemoryBandwidthBenchmark(int size)
    {
        return Task.FromResult(new ComputeWorkflowDefinition
        {
            Name = $"MemoryBandwidthBenchmark_{size}",
            Kernels =
            [
                new WorkflowKernel
            {
                Name = "memory_bandwidth",
                SourceCode = BenchmarkKernels.MemoryBandwidth
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = ComputeWorkflowTestBase.TestDataGenerators.GenerateFloatArray(size) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "output", Size = size }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "bandwidth_stage",
                Order = 1,
                KernelName = "memory_bandwidth",
                ArgumentNames = ["input", "output"]
            }
            ]
        });
    }

    public static Task<ComputeWorkflowDefinition> CreateComputeIntensityBenchmark(int size, int complexity)
    {
        return Task.FromResult(new ComputeWorkflowDefinition
        {
            Name = $"ComputeIntensityBenchmark_{complexity}",
            Kernels =
            [
                new WorkflowKernel
            {
                Name = "compute_intensive",
                SourceCode = BenchmarkKernels.ComputeIntensive
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = ComputeWorkflowTestBase.TestDataGenerators.GenerateFloatArray(size) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "output", Size = size }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "intensive_stage",
                Order = 1,
                KernelName = "compute_intensive",
                ArgumentNames = ["input", "output"],
                Parameters = new Dictionary<string, object> { ["complexity"] = complexity }
            }
            ]
        });
    }

    public static ComputeWorkflowDefinition CreateConcurrentBenchmark(string name, int size)
    {
        return new ComputeWorkflowDefinition
        {
            Name = name,
            Kernels =
            [
                new WorkflowKernel
            {
                Name = "concurrent_benchmark",
                SourceCode = BenchmarkKernels.ConcurrentOperation
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = ComputeWorkflowTestBase.TestDataGenerators.GenerateFloatArray(size) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "output", Size = size }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "concurrent_stage",
                Order = 1,
                KernelName = "concurrent_benchmark",
                ArgumentNames = ["input", "output"]
            }
            ]
        };
    }

    public static Task<ComputeWorkflowDefinition> CreateAlgorithmSpecificBenchmark(string algorithmType, int dataSize)
    {
        var kernelSource = algorithmType switch
        {
            "MatrixMultiplication" => BenchmarkKernels.MatrixMultiplication,
            "FFT" => BenchmarkKernels.FFT,
            "Convolution" => BenchmarkKernels.Convolution,
            "Reduction" => BenchmarkKernels.Reduction,
            _ => BenchmarkKernels.VectorOperations
        };

        return Task.FromResult(new ComputeWorkflowDefinition
        {
            Name = $"{algorithmType}Benchmark",
            Kernels =
            [
                new WorkflowKernel
            {
                Name = algorithmType.ToUpperInvariant(),
                SourceCode = kernelSource,
                CompilationOptions = new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum }
            }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = ComputeWorkflowTestBase.TestDataGenerators.GenerateFloatArray(dataSize) }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "output", Size = dataSize }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
            {
                Name = "algorithm_stage",
                Order = 1,
                KernelName = algorithmType.ToUpperInvariant(),
                ArgumentNames = ["input", "output"]
            }
            ]
        });
    }

    public static Task<ComputeWorkflowDefinition> CreateRealWorldPipelineBenchmark()
    {
        const int imageSize = 512;
        var imageData = ComputeWorkflowTestBase.TestDataGenerators.GenerateFloatArray(imageSize * imageSize, 0f, 255f);

        return Task.FromResult(new ComputeWorkflowDefinition
        {
            Name = "RealWorldPipelineBenchmark",
            Kernels =
            [
                new WorkflowKernel { Name = "preprocess", SourceCode = BenchmarkKernels.ImagePreprocess },
            new WorkflowKernel { Name = "filter", SourceCode = BenchmarkKernels.ImageFilter },
            new WorkflowKernel { Name = "analyze", SourceCode = BenchmarkKernels.ImageAnalyze },
            new WorkflowKernel { Name = "postprocess", SourceCode = BenchmarkKernels.ImagePostprocess }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "raw_image", Data = imageData }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "processed_image", Size = imageSize * imageSize }
            ],
            IntermediateBuffers =
            [
                new WorkflowIntermediateBuffer { Name = "preprocessed", SizeInBytes = imageSize * imageSize * sizeof(float) },
            new WorkflowIntermediateBuffer { Name = "filtered", SizeInBytes = imageSize * imageSize * sizeof(float) },
            new WorkflowIntermediateBuffer { Name = "analyzed", SizeInBytes = imageSize * imageSize * sizeof(float) }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage { Name = "preprocess_stage", Order = 1, KernelName = "preprocess", ArgumentNames = ["raw_image", "preprocessed"] },
            new WorkflowExecutionStage { Name = "filter_stage", Order = 2, KernelName = "filter", ArgumentNames = ["preprocessed", "filtered"] },
            new WorkflowExecutionStage { Name = "analyze_stage", Order = 3, KernelName = "analyze", ArgumentNames = ["filtered", "analyzed"] },
            new WorkflowExecutionStage { Name = "postprocess_stage", Order = 4, KernelName = "postprocess", ArgumentNames = ["analyzed", "processed_image"] }
            ]
        });
    }

    public static async Task<Dictionary<string, ComputeWorkflowDefinition>> CreateRegressionTestSuite()
    {
        var suite = new Dictionary<string, ComputeWorkflowDefinition>
        {
            ["VectorAdd"] = await CreateVectorOperationBenchmark(1024),
            ["MatrixMultiply"] = await CreateAlgorithmSpecificBenchmark("MatrixMultiplication", 128),
            ["FFT"] = await CreateAlgorithmSpecificBenchmark("FFT", 256),
            ["MemoryTransfer"] = await CreateMemoryBandwidthBenchmark(2048)
        };

        return suite;
    }
}

public record MemoryBandwidthResult
{
    public int DataSize { get; init; }
    public TimeSpan TransferTime { get; init; }
    public double BandwidthMBps { get; init; }
    public double MemoryEfficiency { get; init; }
}

public record ComputeIntensityResult
{
    public int ComplexityLevel { get; init; }
    public TimeSpan ExecutionTime { get; init; }
    public double ThroughputMBps { get; init; }
    public ResourceUtilization ResourceUtilization { get; init; } = new();
}

public record ConcurrencyBenchmarkResult
{
    public int ConcurrencyLevel { get; init; }
    public int SuccessfulWorkflows { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public double AggregatedThroughput { get; init; }
    public double AverageLatency { get; init; }
    public double ScalingEfficiency { get; init; }
}

public record AlgorithmBaseline
{
    public double MinThroughput { get; init; }
    public double MaxLatency { get; init; }
}

public record PipelinePerformanceMetrics
{
    public TimeSpan TotalDuration { get; init; }
    public int StageCount { get; init; }
    public double TotalThroughput { get; init; }
    public ResourceUtilization ResourceUtilization { get; init; } = new();
    public double[] StageLatencies { get; init; } = Array.Empty<double>();
}

/// <summary>
/// Benchmark-specific kernel sources.
/// </summary>
internal static class BenchmarkKernels
{
    public const string VectorOperations = @"
__kernel void vector_benchmark(__global const float* a, __global const float* b, __global float* result) {
    int gid = get_global_id(0);
    float x = a[gid];
    float y = b[gid];
    
    // Multiple vector operations for throughput testing
    result[gid] =(x + y) *(x - y) + sqrt(x * y);
}";

    public const string MinimalOperation = @"
__kernel void latency_test(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    output[gid] = input[gid] + 1.0f;
}";

    public const string MemoryBandwidth = @"
__kernel void memory_bandwidth(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    // Simple copy to test memory bandwidth
    output[gid] = input[gid];
}";

    public const string ComputeIntensive = @"
__kernel void compute_intensive(__global const float* input, __global float* output, int complexity) {
    int gid = get_global_id(0);
    float value = input[gid];
    
    for(int i = 0; i < complexity * 10; i++) {
        value = sin(value) * cos(value) + exp(-value * 0.1f);
    }
    
    output[gid] = value;
}";

    public const string ConcurrentOperation = @"
__kernel void concurrent_benchmark(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    int lid = get_local_id(0);
    __local float shared[256];
    
    shared[lid] = input[gid];
    barrier(CLK_LOCAL_MEM_FENCE);
    
    float sum = 0.0f;
    for(int i = 0; i < get_local_size(0); i++) {
        sum += shared[i];
    }
    
    output[gid] = sum / get_local_size(0);
}";

    public const string MatrixMultiplication = @"
__kernel void matrixmultiplication(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    int size =(int)sqrt((float)get_global_size(0));
    int row = gid / size;
    int col = gid % size;
    
    float sum = 0.0f;
    for(int k = 0; k < size; k++) {
        sum += input[row * size + k] * input[k * size + col];
    }
    output[gid] = sum;
}";

    public const string FFT = @"
__kernel void fft(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    int n = get_global_size(0);
    
    // Simplified FFT computation
    float sum_real = 0.0f, sum_imag = 0.0f;
    for(int k = 0; k < n; k++) {
        float angle = -2.0f * M_PI * gid * k / n;
        sum_real += input[k] * cos(angle);
        sum_imag += input[k] * sin(angle);
    }
    output[gid] = sqrt(sum_real * sum_real + sum_imag * sum_imag);
}";

    public const string Convolution = @"
__kernel void convolution(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    int size =(int)sqrt((float)get_global_size(0));
    int x = gid % size;
    int y = gid / size;
    
    float kernel[9] = {-1, -1, -1, -1, 8, -1, -1, -1, -1};
    float sum = 0.0f;
    
    for(int ky = -1; ky <= 1; ky++) {
        for(int kx = -1; kx <= 1; kx++) {
            int px = clamp(x + kx, 0, size - 1);
            int py = clamp(y + ky, 0, size - 1);
            sum += input[py * size + px] * kernel[(ky + 1) * 3 +(kx + 1)];
        }
    }
    output[gid] = sum;
}";

    public const string Reduction = @"
__kernel void reduction(__global const float* input, __global float* output,
                       __local float* scratch) {
    int gid = get_global_id(0);
    int lid = get_local_id(0);
    
    scratch[lid] = input[gid];
    barrier(CLK_LOCAL_MEM_FENCE);
    
    for(int offset = get_local_size(0) / 2; offset > 0; offset >>= 1) {
        if(lid < offset) {
            scratch[lid] += scratch[lid + offset];
        }
        barrier(CLK_LOCAL_MEM_FENCE);
    }
    
    if(lid == 0) {
        output[get_group_id(0)] = scratch[0];
    }
}";

    public const string ImagePreprocess = @"
__kernel void preprocess(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    output[gid] = clamp((input[gid] - 128.0f) / 128.0f, -1.0f, 1.0f);
}";

    public const string ImageFilter = @"
__kernel void filter(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    output[gid] = input[gid] * 0.9f + 0.1f;
}";

    public const string ImageAnalyze = @"
__kernel void analyze(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    output[gid] = fabs(input[gid]) > 0.5f ? 1.0f : 0.0f;
}";

    public const string ImagePostprocess = @"
__kernel void postprocess(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    output[gid] = input[gid] * 255.0f;
}";
}
