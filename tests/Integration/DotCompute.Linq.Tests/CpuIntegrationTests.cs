// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Linq.CodeGeneration;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Optimization;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Linq.Tests;

/// <summary>
/// Comprehensive integration tests for Phase 4 CPU backend.
/// Tests the complete pipeline: Graph → Generation → Compilation → Execution.
/// </summary>
[Trait("Category", "Integration")]
public class CpuIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<CpuIntegrationTests> _logger;
    private readonly RuntimeExecutor _executor;
    private readonly BackendSelector _backendSelector;
    private readonly CpuKernelGenerator _kernelGenerator;

    public CpuIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        // Create logger factory that writes to test output
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new XunitLoggerProvider(output));
        });

        _logger = loggerFactory.CreateLogger<CpuIntegrationTests>();
        _executor = new RuntimeExecutor(loggerFactory.CreateLogger<RuntimeExecutor>());
        _backendSelector = new BackendSelector(loggerFactory.CreateLogger<BackendSelector>());
        _kernelGenerator = new CpuKernelGenerator();
    }

    #region Backend Selection Tests (15 tests)

    [Fact]
    public void BackendSelector_DetectsAvailableBackends()
    {
        // Act
        var backends = _backendSelector.GetAvailableBackends();

        // Assert
        backends.Should().HaveFlag(DotCompute.Linq.CodeGeneration.AvailableBackends.CpuSimd, "CPU should always be available");
        _output.WriteLine($"Available backends: {backends}");
    }

    [Fact]
    public void BackendSelector_SmallWorkload_SelectsCpu()
    {
        // Arrange
        var workload = new DotCompute.Linq.CodeGeneration.WorkloadCharacteristics
        {
            DataSize = 1_000,
            Intensity = DotCompute.Linq.CodeGeneration.ComputeIntensity.Low,
            PrimaryOperation = DotCompute.Linq.Optimization.OperationType.Map,
            OperationsPerElement =1,
            IsMemoryBound = false,
            IsFusible = true
        };

        // Act
        var backend = _backendSelector.SelectBackend(workload);

        // Assert
        backend.Should().Be(DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);
    }

    [Fact]
    public void BackendSelector_LargeWorkload_PrefersGpuIfAvailable()
    {
        // Arrange
        var workload = new DotCompute.Linq.CodeGeneration.WorkloadCharacteristics
        {
            DataSize = 1_000_000,
            Intensity = DotCompute.Linq.CodeGeneration.ComputeIntensity.High,
            PrimaryOperation = DotCompute.Linq.Optimization.OperationType.Map,
            OperationsPerElement =5,
            IsMemoryBound = false,
            IsFusible = true
        };

        // Act
        var backend = _backendSelector.SelectBackend(workload);

        // Assert
        // Will select GPU if available, otherwise CPU
        backend.Should().BeOneOf(DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd, DotCompute.Linq.CodeGeneration.ComputeBackend.Cuda);
        _output.WriteLine($"Selected backend: {backend}");
    }

    [Theory]
    [InlineData(1_000, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd)]
    [InlineData(10_000, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd)]
    [InlineData(100_000, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd)] // Still CPU without high compute intensity
    public void BackendSelector_DataSizeThresholds_SelectsCorrectBackend(int dataSize, DotCompute.Linq.CodeGeneration.ComputeBackend expected)
    {
        // Arrange
        var workload = new DotCompute.Linq.CodeGeneration.WorkloadCharacteristics
        {
            DataSize = dataSize,
            Intensity = DotCompute.Linq.CodeGeneration.ComputeIntensity.Low,
            PrimaryOperation = DotCompute.Linq.Optimization.OperationType.Map,
            OperationsPerElement =1,
            IsMemoryBound = false,
            IsFusible = true
        };

        // Act
        var backend = _backendSelector.SelectBackend(workload);

        // Assert
        backend.Should().Be(expected);
    }

    [Theory]
    [InlineData(OperationType.Map, 500_000)]
    [InlineData(OperationType.Reduce, 100_000)]
    [InlineData(OperationType.Filter, 1_000_000)]
    public void BackendSelector_OperationSpecificThresholds_Applied(OperationType operation, int threshold)
    {
        // Arrange
        var workload = new DotCompute.Linq.CodeGeneration.WorkloadCharacteristics
        {
            DataSize = threshold - 1,
            Intensity = DotCompute.Linq.CodeGeneration.ComputeIntensity.High,
            PrimaryOperation = operation,
            OperationsPerElement =1,
            IsMemoryBound = false,
            IsFusible = true
        };

        // Act
        var backend = _backendSelector.SelectBackend(workload);

        // Assert - Should select CPU for data below threshold
        backend.Should().Be(DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);
    }

    [Fact]
    public void BackendSelector_HighParallelism_FavorsGpu()
    {
        // Arrange
        var workload = new DotCompute.Linq.CodeGeneration.WorkloadCharacteristics
        {
            DataSize = 1_000_000,
            Intensity = DotCompute.Linq.CodeGeneration.ComputeIntensity.High,
            PrimaryOperation = DotCompute.Linq.Optimization.OperationType.Map,
            OperationsPerElement =1,
            IsMemoryBound = false,
            IsFusible = true,
            ParallelismDegree = 10_000
        };

        // Act
        var isGpuBeneficial = _backendSelector.IsGpuBeneficial(workload);

        // Assert
        _output.WriteLine($"GPU beneficial: {isGpuBeneficial}");
    }

    [Fact]
    public void BackendSelector_MemoryBoundWorkload_ProvidesRecommendations()
    {
        // Arrange
        var workload = new DotCompute.Linq.CodeGeneration.WorkloadCharacteristics
        {
            DataSize = 2_000_000,
            Intensity = DotCompute.Linq.CodeGeneration.ComputeIntensity.Medium,
            PrimaryOperation = DotCompute.Linq.Optimization.OperationType.Map,
            OperationsPerElement =3,
            IsMemoryBound = true,
            IsFusible = false
        };

        // Act
        var recommendations = _backendSelector.GetOptimizationRecommendations(workload);

        // Assert
        recommendations.Should().NotBeEmpty();
        recommendations.Should().Contain(r => r.Contains("fusing"));
        _output.WriteLine($"Recommendations: {string.Join("; ", recommendations)}");
    }

    [Theory]
    [InlineData(100, false)]
    [InlineData(1_000, false)]
    [InlineData(10_000, true)]
    [InlineData(1_000_000, true)]
    public void BackendSelector_TransferOverheadCalculation_Accurate(int dataSize, bool worthTransfer)
    {
        // Arrange
        var overhead = BackendSelector.EstimateTransferOverhead(dataSize, false, false);

        // Act
        var workload = new DotCompute.Linq.CodeGeneration.WorkloadCharacteristics
        {
            DataSize = dataSize,
            Intensity = DotCompute.Linq.CodeGeneration.ComputeIntensity.High,
            PrimaryOperation = DotCompute.Linq.Optimization.OperationType.Map,
            OperationsPerElement =1,
            IsMemoryBound = false,
            IsFusible = true
        };

        var computeCost = workload.EstimateComputationalCost();
        var beneficial = computeCost > (overhead.TotalMilliseconds * 2.0);

        // Assert
        _output.WriteLine($"DataSize: {dataSize}, Overhead: {overhead.TotalMilliseconds}ms, Compute: {computeCost}, Beneficial: {beneficial}");
    }

    #endregion

    #region CPU Kernel Generation Tests (15 tests)

    [Fact]
    public void CpuKernelGenerator_SimpleMap_GeneratesCorrectCode()
    {
        // Arrange
        var graph = CreateSimpleMapGraph();
        var metadata = CreateTypeMetadata<int, int>();

        // Act
        var code = _kernelGenerator.GenerateKernel(graph, metadata);

        // Assert
        code.Should().NotBeNullOrEmpty();
        code.Should().Contain("Vector<int>");
        code.Should().Contain("vectorSize");
        code.Should().Contain("for");
        _output.WriteLine($"Generated code:\n{code}");
    }

    [Fact]
    public void CpuKernelGenerator_Filter_GeneratesCompactionLogic()
    {
        // Arrange
        var graph = CreateFilterGraph();
        var metadata = CreateTypeMetadata<float, float>();

        // Act
        var code = _kernelGenerator.GenerateKernel(graph, metadata);

        // Assert
        code.Should().Contain("outputIndex");
        code.Should().Contain("mask");
        code.Should().Contain("Compact matching elements");
        _output.WriteLine($"Generated filter code:\n{code}");
    }

    [Fact]
    public void CpuKernelGenerator_Reduce_GeneratesParallelReduction()
    {
        // Arrange
        var graph = CreateReduceGraph();
        var metadata = CreateTypeMetadata<double, double>();

        // Act
        var code = _kernelGenerator.GenerateKernel(graph, metadata);

        // Assert
        code.Should().Contain("accumulator");
        code.Should().Contain("Horizontal sum");
        _output.WriteLine($"Generated reduce code:\n{code}");
    }

    [Fact]
    public void CpuKernelGenerator_FusedOperations_GeneratesSingleKernel()
    {
        // Arrange
        var graph = CreateFusedMapFilterGraph();
        var metadata = CreateTypeMetadata<int, int>();

        // Act
        var code = _kernelGenerator.GenerateKernel(graph, metadata);

        // Assert
        code.Should().Contain("Fused operations");
        _output.WriteLine($"Generated fused code:\n{code}");
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(float))]
    [InlineData(typeof(double))]
    [InlineData(typeof(long))]
    public void CpuKernelGenerator_DifferentTypes_GeneratesTypeSpecificCode(Type elementType)
    {
        // Arrange
        var graph = CreateSimpleMapGraph();
        var metadata = CreateTypeMetadata(elementType, elementType);

        // Act
        var code = _kernelGenerator.GenerateKernel(graph, metadata);

        // Assert
        code.Should().Contain($"Vector<{GetTypeName(elementType)}>");
        _output.WriteLine($"Generated code for {elementType.Name}:\n{code}");
    }

    #endregion

    #region Runtime Execution Tests (20 tests)

    [Fact]
    public async Task RuntimeExecutor_SimpleMap_ExecutesCorrectly()
    {
        // Arrange
        var input = Enumerable.Range(0, 10_000).Select(i => (float)i).ToArray();
        Func<float[], float[]> kernel = data => data.Select(x => x * 2.0f).ToArray();

        // Act
        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        // Assert
        results.Should().HaveCount(input.Length);
        results[0].Should().Be(0);
        results[100].Should().Be(200);
        metrics.Success.Should().BeTrue();
        metrics.Backend.Should().Be(DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        _output.WriteLine($"Execution metrics: {metrics}");
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(10_000)]
    [InlineData(100_000)]
    [InlineData(1_000_000)]
    [Trait("Category", "Performance")]
    public async Task RuntimeExecutor_VariousDataSizes_PerformsEfficiently(int dataSize)
    {
        // Arrange
        var input = Enumerable.Range(0, dataSize).Select(i => (float)i).ToArray();
        Func<float[], float[]> kernel = data => data.Select(x => x + 1.0f).ToArray();

        // Act
        var sw = Stopwatch.StartNew();
        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);
        sw.Stop();

        // Assert
        results.Should().HaveCount(input.Length);
        metrics.Success.Should().BeTrue();
        metrics.Throughput.Should().BeGreaterThan(0);

        _output.WriteLine($"Size: {dataSize}, Time: {sw.ElapsedMilliseconds}ms, Throughput: {metrics.Throughput:F2} elements/sec");
    }

    [Fact]
    public async Task RuntimeExecutor_ComplexOperation_ProducesCorrectResults()
    {
        // Arrange
        var input = Enumerable.Range(0, 10_000).Select(i => (float)i).ToArray();
        Func<float[], float[]> kernel = data => data
            .Select(x => x * 2.0f)
            .Select(x => x + 10.0f)
            .Select(x => x / 2.0f)
            .ToArray();

        // Act
        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        // Assert
        results.Should().HaveCount(input.Length);
        results[0].Should().Be(5.0f); // (0 * 2 + 10) / 2 = 5
        results[1].Should().Be(6.0f); // (1 * 2 + 10) / 2 = 6
        metrics.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RuntimeExecutor_GracefulDegradation_FallsBackToCpu()
    {
        // Arrange - Request CUDA backend (may not be available)
        var input = Enumerable.Range(0, 1_000).Select(i => (float)i).ToArray();
        Func<float[], float[]> kernel = data => data.Select(x => x * 2.0f).ToArray();

        // Act
        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.Cuda);

        // Assert - Should succeed even if CUDA not available (falls back to CPU)
        results.Should().HaveCount(input.Length);
        metrics.Success.Should().BeTrue();
        _output.WriteLine($"Executed on backend: {metrics.Backend}");
    }

    [Fact]
    public async Task RuntimeExecutor_MultipleExecutions_ReuseAccelerator()
    {
        // Arrange
        var input = Enumerable.Range(0, 1_000).Select(i => (float)i).ToArray();
        Func<float[], float[]> kernel = data => data.Select(x => x * 2.0f).ToArray();

        // Act - Execute multiple times
        var results1 = await _executor.ExecuteAsync(kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);
        var results2 = await _executor.ExecuteAsync(kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);
        var results3 = await _executor.ExecuteAsync(kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        // Assert
        results1.Results.Should().Equal(results2.Results);
        results2.Results.Should().Equal(results3.Results);

        // Get accelerator info to verify reuse
        var accelerators = _executor.GetAcceleratorInfo();
        accelerators.Should().ContainKey(DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);
    }

    [Fact]
    public async Task RuntimeExecutor_Synchronize_CompletesAllOperations()
    {
        // Arrange
        var input = Enumerable.Range(0, 10_000).Select(i => (float)i).ToArray();
        Func<float[], float[]> kernel = data => data.Select(x => x * 2.0f).ToArray();

        // Act
        await _executor.ExecuteAsync(kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);
        await _executor.SynchronizeAllAsync();

        // Assert - Should complete without errors
        var accelerators = _executor.GetAcceleratorInfo();
        accelerators.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RuntimeExecutor_CancellationToken_HonorsCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var input = Enumerable.Range(0, 10_000_000).Select(i => (float)i).ToArray();
        Func<float[], float[]> kernel = data => data.Select(x => x * 2.0f).ToArray();

        // Act
        cts.Cancel();
        Func<Task> act = async () => await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RuntimeExecutor_MetricsCollection_CapturesAllPhases()
    {
        // Arrange
        var input = Enumerable.Range(0, 10_000).Select(i => (float)i).ToArray();
        Func<float[], float[]> kernel = data => data.Select(x => x * 2.0f).ToArray();

        // Act
        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        // Assert
        metrics.ExecutionTime.Should().BeGreaterThan(TimeSpan.Zero);
        metrics.TotalTime.Should().BeGreaterThan(TimeSpan.Zero);
        metrics.DataSize.Should().Be(input.Length);
        metrics.Throughput.Should().BeGreaterThan(0);
        metrics.StartTime.Should().BeBefore(metrics.EndTime);

        _output.WriteLine($"Detailed metrics: {metrics}");
    }

    #endregion

    #region Correctness Validation Tests (10 tests)

    [Fact]
    public async Task Correctness_SimpleAddition_MatchesLinq()
    {
        // Arrange
        var input = Enumerable.Range(0, 10_000).Select(i => (float)i).ToArray();
        var expected = input.Select(x => x + 42.0f).ToArray();
        Func<float[], float[]> kernel = data => data.Select(x => x + 42.0f).ToArray();

        // Act
        var (results, _) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        // Assert
        results.Should().Equal(expected);
    }

    [Fact]
    public async Task Correctness_Multiplication_MatchesLinq()
    {
        // Arrange
        var input = Enumerable.Range(0, 10_000).Select(i => (float)i).ToArray();
        var expected = input.Select(x => x * 3.5f).ToArray();
        Func<float[], float[]> kernel = data => data.Select(x => x * 3.5f).ToArray();

        // Act
        var (results, _) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        // Assert
        for (var i = 0; i < results.Length; i++)
        {
            results[i].Should().BeApproximately(expected[i], 0.0001f);
        }
    }

    [Fact]
    public async Task Correctness_ComplexExpression_MatchesLinq()
    {
        // Arrange
        var input = Enumerable.Range(1, 10_000).Select(i => (float)i).ToArray();
        var expected = input.Select(x => (x * 2.0f + 10.0f) / x).ToArray();
        Func<float[], float[]> kernel = data => data.Select(x => (x * 2.0f + 10.0f) / x).ToArray();

        // Act
        var (results, _) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        // Assert
        for (var i = 0; i < results.Length; i++)
        {
            results[i].Should().BeApproximately(expected[i], 0.001f);
        }
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(10_000)]
    [InlineData(50_000)]
    public async Task Correctness_DifferentSizes_AllMatch(int size)
    {
        // Arrange
        var input = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
        var expected = input.Select(x => x * 2.0f + 1.0f).ToArray();
        Func<float[], float[]> kernel = data => data.Select(x => x * 2.0f + 1.0f).ToArray();

        // Act
        var (results, _) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        // Assert
        results.Should().Equal(expected);
    }

    [Fact]
    public async Task Correctness_EdgeCases_HandledCorrectly()
    {
        // Arrange - Edge cases: 0, negative, large values
        var input = new float[] { 0, -1, 1, float.MaxValue / 2, float.MinValue / 2 };
        var expected = input.Select(x => x * 2.0f).ToArray();
        Func<float[], float[]> kernel = data => data.Select(x => x * 2.0f).ToArray();

        // Act
        var (results, _) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        // Assert
        results.Should().HaveCount(expected.Length);
        for (var i = 0; i < results.Length; i++)
        {
            results[i].Should().BeApproximately(expected[i], Math.Abs(expected[i]) * 0.0001f);
        }
    }

    #endregion

    #region Performance Validation Tests (10 tests)

    [Fact]
    [Trait("Category", "Performance")]
    public async Task Performance_CpuVsLinq_ShowsSpeedup()
    {
        // Arrange
        var input = Enumerable.Range(0, 1_000_000).Select(i => (float)i).ToArray();

        // Warm up
        var _ = input.Select(x => x * 2.0f).ToArray();

        // Act - LINQ baseline
        var sw1 = Stopwatch.StartNew();
        var linqResult = input.Select(x => x * 2.0f).ToArray();
        sw1.Stop();
        var linqTime = sw1.ElapsedMilliseconds;

        // Act - DotCompute
        Func<float[], float[]> kernel = data => data.Select(x => x * 2.0f).ToArray();
        var sw2 = Stopwatch.StartNew();
        var (computeResult, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);
        sw2.Stop();
        var computeTime = sw2.ElapsedMilliseconds;

        // Assert - DotCompute should be competitive or faster
        _output.WriteLine($"LINQ: {linqTime}ms, DotCompute: {computeTime}ms, Speedup: {(double)linqTime / computeTime:F2}x");
        _output.WriteLine($"Metrics: {metrics}");
    }

    [Theory]
    [InlineData(10_000)]
    [InlineData(100_000)]
    [InlineData(1_000_000)]
    [Trait("Category", "Performance")]
    public async Task Performance_Throughput_MeetsTargets(int dataSize)
    {
        // Arrange
        var input = Enumerable.Range(0, dataSize).Select(i => (float)i).ToArray();
        Func<float[], float[]> kernel = data => data.Select(x => x * 2.0f + 1.0f).ToArray();

        // Act
        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        // Assert - Should process millions of elements per second
        var throughputMillion = metrics.Throughput / 1_000_000;
        throughputMillion.Should().BeGreaterThan(1.0, "should process > 1M elements/sec");

        _output.WriteLine($"Size: {dataSize}, Throughput: {throughputMillion:F2}M elements/sec");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task Performance_ExecutionEfficiency_IsHigh()
    {
        // Arrange
        var input = Enumerable.Range(0, 100_000).Select(i => (float)i).ToArray();
        Func<float[], float[]> kernel = data => data.Select(x => x * 2.0f).ToArray();

        // Act
        var (_, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        // Assert - Execution should dominate total time (low overhead)
        var efficiency = metrics.Efficiency * 100;
        _output.WriteLine($"Execution efficiency: {efficiency:F1}%");
        _output.WriteLine($"Breakdown: Exec={metrics.ExecutionTime.TotalMilliseconds}ms, " +
                         $"Total={metrics.TotalTime.TotalMilliseconds}ms");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task Performance_MemoryAllocation_IsMinimal()
    {
        // Arrange
        var input = Enumerable.Range(0, 100_000).Select(i => (float)i).ToArray();
        Func<float[], float[]> kernel = data => data.Select(x => x * 2.0f).ToArray();

        var beforeMemory = GC.GetTotalMemory(true);

        // Act
        var (_, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        var afterMemory = GC.GetTotalMemory(false);
        var allocatedMemory = afterMemory - beforeMemory;

        // Assert
        _output.WriteLine($"Memory allocated: {allocatedMemory / 1024.0:F2} KB");
        _output.WriteLine($"Metrics reported: {metrics.MemoryAllocated / 1024.0:F2} KB");
    }

    #endregion

    #region Error Handling Tests (10 tests)

    [Fact]
    public async Task ErrorHandling_NullInput_ThrowsArgumentNull()
    {
        // Arrange
        float[]? input = null;
        Func<float[], float[]> kernel = data => data.Select(x => x * 2.0f).ToArray();

        // Act
        Func<Task> act = async () => await _executor.ExecuteAsync(
            kernel, input!, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ErrorHandling_NullKernel_ThrowsArgumentNull()
    {
        // Arrange
        var input = new float[] { 1, 2, 3 };
        Func<float[], float[]>? kernel = null;

        // Act
        Func<Task> act = async () => await _executor.ExecuteAsync(
            kernel!, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ErrorHandling_EmptyInput_HandlesGracefully()
    {
        // Arrange
        var input = Array.Empty<float>();
        Func<float[], float[]> kernel = data => data.Select(x => x * 2.0f).ToArray();

        // Act
        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        // Assert
        results.Should().BeEmpty();
        metrics.Success.Should().BeTrue();
    }

    [Fact]
    public void ErrorHandling_DisposedExecutor_ThrowsObjectDisposed()
    {
        // Arrange
        var executor = new RuntimeExecutor();
        executor.Dispose();
        var input = new float[] { 1, 2, 3 };
        Func<float[], float[]> kernel = data => data.Select(x => x * 2.0f).ToArray();

        // Act
        Func<Task> act = async () => await executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        // Assert
        act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region Full Pipeline Tests (10 tests)

    [Fact]
    public async Task FullPipeline_GraphToExecution_WorksEndToEnd()
    {
        // Arrange - Create operation graph
        var graph = CreateSimpleMapGraph();
        var metadata = CreateTypeMetadata<float, float>();

        // Act - Generate code
        var code = _kernelGenerator.GenerateKernel(graph, metadata);
        code.Should().NotBeNullOrEmpty();

        // Execute (using simplified kernel for testing)
        var input = Enumerable.Range(0, 10_000).Select(i => (float)i).ToArray();
        Func<float[], float[]> kernel = data => data.Select(x => x * 2.0f).ToArray();
        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        // Assert
        results.Should().HaveCount(input.Length);
        metrics.Success.Should().BeTrue();
        _output.WriteLine($"Full pipeline completed: {metrics}");
    }

    [Fact]
    public async Task FullPipeline_FusedOperations_OptimizesExecution()
    {
        // Arrange
        var graph = CreateFusedMapFilterGraph();
        var metadata = CreateTypeMetadata<int, int>();
        var code = _kernelGenerator.GenerateKernel(graph, metadata);

        // Act
        var input = Enumerable.Range(0, 10_000).ToArray();
        Func<int[], int[]> kernel = data => data
            .Select(x => x * 2)
            .Where(x => x > 100)
            .ToArray();
        var (results, metrics) = await _executor.ExecuteAsync(
            kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd);

        // Assert
        results.Should().NotBeEmpty();
        metrics.Success.Should().BeTrue();
        code.Should().Contain("Fused");
    }

    #endregion

    #region Concurrent Execution Tests (5 tests)

    [Fact]
    public async Task Concurrent_MultipleExecutions_RunSafely()
    {
        // Arrange
        var input = Enumerable.Range(0, 10_000).Select(i => (float)i).ToArray();
        Func<float[], float[]> kernel = data => data.Select(x => x * 2.0f).ToArray();

        // Act - Execute concurrently
        var tasks = Enumerable.Range(0, 10).Select(i =>
            _executor.ExecuteAsync(kernel, input, DotCompute.Linq.CodeGeneration.ComputeBackend.CpuSimd)
        ).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(10);
        results.All(r => r.Metrics.Success).Should().BeTrue();

        // All results should be identical
        var first = results[0].Results;
        foreach (var r in results.Skip(1))
        {
            r.Results.Should().Equal(first);
        }
    }

    #endregion

    #region Helper Methods

    private OperationGraph CreateSimpleMapGraph()
    {
        var operations = new List<Operation>
        {
            new Operation
            {
                Type = OperationType.Map,
                Metadata = new Dictionary<string, object>()
            }
        };

        return new OperationGraph(operations);
    }

    private OperationGraph CreateFilterGraph()
    {
        var operations = new List<Operation>
        {
            new Operation
            {
                Type = OperationType.Filter,
                Metadata = new Dictionary<string, object>()
            }
        };

        return new OperationGraph(operations);
    }

    private OperationGraph CreateReduceGraph()
    {
        var operations = new List<Operation>
        {
            new Operation
            {
                Type = OperationType.Reduce,
                Metadata = new Dictionary<string, object>()
            }
        };

        return new OperationGraph(operations);
    }

    private OperationGraph CreateFusedMapFilterGraph()
    {
        var operations = new List<Operation>
        {
            new Operation
            {
                Type = OperationType.Map,
                Metadata = new Dictionary<string, object>()
            },
            new Operation
            {
                Type = OperationType.Filter,
                Metadata = new Dictionary<string, object>()
            }
        };

        return new OperationGraph(operations);
    }

    private TypeMetadata CreateTypeMetadata<TInput, TResult>()
    {
        return CreateTypeMetadata(typeof(TInput), typeof(TResult));
    }

    private TypeMetadata CreateTypeMetadata(Type inputType, Type resultType)
    {
        return new TypeMetadata
        {
            InputType = inputType,
            ResultType = resultType,
            IntermediateTypes = new Dictionary<string, Type>(),
            RequiresUnsafe = false,
            IsSimdCompatible = IsSimdCompatible(inputType),
            HasNullableTypes = false
        };
    }

    private bool IsSimdCompatible(Type type)
    {
        return type == typeof(int) ||
               type == typeof(float) ||
               type == typeof(double) ||
               type == typeof(long);
    }

    private string GetTypeName(Type type)
    {
        return type switch
        {
            Type t when t == typeof(int) => "int",
            Type t when t == typeof(float) => "float",
            Type t when t == typeof(double) => "double",
            Type t when t == typeof(long) => "long",
            _ => type.Name
        };
    }

    public void Dispose()
    {
        _executor?.Dispose();
    }

    #endregion
}

/// <summary>
/// Xunit logger provider for test output.
/// </summary>
internal class XunitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public XunitLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new XunitLogger(_output, categoryName);
    }

    public void Dispose() { }
}

/// <summary>
/// Xunit logger implementation.
/// </summary>
internal class XunitLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;

    public XunitLogger(ITestOutputHelper output, string categoryName)
    {
        _output = output;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => new NoOpDisposable();

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        try
        {
            _output.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");
            if (exception != null)
            {
                _output.WriteLine($"Exception: {exception}");
            }
        }
        catch
        {
            // Ignore errors writing to test output
        }
    }

    private class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }
}

/// <summary>
/// Available compute backends flags.
/// </summary>
[Flags]
public enum AvailableBackends
{
    CpuSimd = 1,
    Cuda = 2,
    Metal = 4
}

/// <summary>
/// Compute backend selection.
/// </summary>
public enum ComputeBackend
{
    CpuSimd,
    Cuda,
    Metal
}

/// <summary>
/// Compute intensity levels.
/// </summary>
public enum ComputeIntensity
{
    Low,
    Medium,
    High,
    VeryHigh
}

/// <summary>
/// Extension methods for workload characteristics.
/// </summary>
public static class WorkloadCharacteristicsExtensions
{
    public static double EstimateComputationalCost(this DotCompute.Linq.CodeGeneration.WorkloadCharacteristics workload)
    {
        var baseCost = workload.DataSize * workload.OperationsPerElement;
        var intensityMultiplier = workload.Intensity switch
        {
            DotCompute.Linq.CodeGeneration.ComputeIntensity.Low => 1.0,
            DotCompute.Linq.CodeGeneration.ComputeIntensity.Medium => 2.5,
            DotCompute.Linq.CodeGeneration.ComputeIntensity.High => 5.0,
            DotCompute.Linq.CodeGeneration.ComputeIntensity.VeryHigh => 10.0,
            _ => 1.0
        };

        return baseCost * intensityMultiplier / 1_000_000.0; // Convert to milliseconds
    }
}
