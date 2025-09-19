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
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Core.Optimization;
using DotCompute.Linq.Interfaces;
using DotCompute.Linq.Pipelines.Integration;
using DotCompute.Linq.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Benchmarks;

/// <summary>
/// Comprehensive benchmarks for measuring the impact of different optimization strategies in DotCompute LINQ.
/// Tests kernel fusion, memory optimization, ML-based backend selection, and optimization profiles.
/// </summary>
[Config(typeof(OptimizationImpactConfig))]
[MemoryDiagnoser]
[ThreadingDiagnoser]
[HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.CacheMisses)]
public class OptimizationImpactBenchmark
{
    private IComputeLinqProvider _baseProvider = null!;
    private IComputeLinqProvider _optimizedProvider = null!;
    private IComputeOrchestrator _conservativeOrchestrator = null!;
    private IComputeOrchestrator _balancedOrchestrator = null!;
    private IComputeOrchestrator _aggressiveOrchestrator = null!;
    private IComputeOrchestrator _mlOptimizedOrchestrator = null!;

    private IAccelerator _cpuAccelerator = null!;
    private IAccelerator _gpuAccelerator = null!;

    // Test datasets for different optimization scenarios
    private int[] _smallIntData = null!;
    private int[] _mediumIntData = null!;
    private int[] _largeIntData = null!;
    private float[] _smallFloatData = null!;
    private float[] _mediumFloatData = null!;
    private float[] _largeFloatData = null!;
    private double[] _computeIntensiveData = null!;

    // Complex workload data
    private ComplexWorkloadData[] _complexWorkload = null!;
    private MatrixData[] _matrixWorkload = null!;

    [Params(1000, 10000, 100000, 1000000)]
    public int DataSize { get; set; }

    [Params(OptimizationStrategy.None, OptimizationStrategy.KernelFusion, OptimizationStrategy.MemoryOptimization, OptimizationStrategy.MLOptimization, OptimizationStrategy.All)]
    public OptimizationStrategy Strategy { get; set; }

    [Params(OptimizationLevel.Conservative, OptimizationLevel.Balanced, OptimizationLevel.Aggressive)]
    public OptimizationLevel Level { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Setup DotCompute services with different optimization profiles
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDotComputeRuntime();
        services.AddDotComputeLinq();
        services.AddPipelineLinqServices();

        // Add optimization services
        services.AddProductionOptimization();
        services.AddProductionDebugging();

        var serviceProvider = services.BuildServiceProvider();

        // Get providers with different optimization configurations
        _baseProvider = serviceProvider.GetRequiredService<IComputeLinqProvider>();
        _optimizedProvider = serviceProvider.GetService<RuntimeIntegratedLinqProvider>() ?? _baseProvider;

        // Get orchestrators with different optimization levels
        var orchestratorFactory = serviceProvider.GetRequiredService<IComputeOrchestratorFactory>();
        _conservativeOrchestrator = orchestratorFactory.CreateOrchestrator(OptimizationLevel.Conservative);
        _balancedOrchestrator = orchestratorFactory.CreateOrchestrator(OptimizationLevel.Balanced);
        _aggressiveOrchestrator = orchestratorFactory.CreateOrchestrator(OptimizationLevel.Aggressive);
        _mlOptimizedOrchestrator = orchestratorFactory.CreateOrchestrator(OptimizationLevel.MLOptimized);

        // Get accelerators
        var acceleratorService = serviceProvider.GetRequiredService<IAcceleratorService>();
        _cpuAccelerator = acceleratorService.GetAccelerators().First(a => a.Info.DeviceType == DeviceType.CPU);
        _gpuAccelerator = acceleratorService.GetAccelerators().FirstOrDefault(a => a.Info.DeviceType == DeviceType.CUDA)
                          ?? _cpuAccelerator; // Fallback to CPU if no GPU available

        SetupTestData();
    }

    private void SetupTestData()
    {
        var random = new Random(42); // Deterministic seed

        // Basic numeric arrays of different sizes
        _smallIntData = Enumerable.Range(0, Math.Min(DataSize, 1000))
            .Select(_ => random.Next(1, 10000))
            .ToArray();

        _mediumIntData = Enumerable.Range(0, Math.Min(DataSize, 10000))
            .Select(_ => random.Next(1, 10000))
            .ToArray();

        _largeIntData = Enumerable.Range(0, DataSize)
            .Select(_ => random.Next(1, 10000))
            .ToArray();

        _smallFloatData = Enumerable.Range(0, Math.Min(DataSize, 1000))
            .Select(_ => (float)random.NextDouble() * 10000f)
            .ToArray();

        _mediumFloatData = Enumerable.Range(0, Math.Min(DataSize, 10000))
            .Select(_ => (float)random.NextDouble() * 10000f)
            .ToArray();

        _largeFloatData = Enumerable.Range(0, DataSize)
            .Select(_ => (float)random.NextDouble() * 10000f)
            .ToArray();

        // Compute-intensive data with transcendental operations
        _computeIntensiveData = Enumerable.Range(0, DataSize)
            .Select(_ => random.NextDouble() * Math.PI * 2)
            .ToArray();

        // Complex workload data
        _complexWorkload = Enumerable.Range(0, DataSize)
            .Select(i => new ComplexWorkloadData
            {
                Id = i,
                Values = Enumerable.Range(0, 10).Select(_ => (float)random.NextDouble() * 100f).ToArray(),
                Weights = Enumerable.Range(0, 10).Select(_ => (float)random.NextDouble()).ToArray(),
                Category = random.Next(1, 5),
                Timestamp = DateTime.Now.AddSeconds(-random.Next(0, 86400))
            })
            .ToArray();

        // Matrix workload data
        _matrixWorkload = Enumerable.Range(0, Math.Min(DataSize, 10000))
            .Select(i => new MatrixData
            {
                Matrix = GenerateRandomMatrix(8, 8, random),
                Vector = Enumerable.Range(0, 8).Select(_ => (float)random.NextDouble()).ToArray()
            })
            .ToArray();
    }

    private float[,] GenerateRandomMatrix(int rows, int cols, Random random)
    {
        var matrix = new float[rows, cols];
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                matrix[i, j] = (float)random.NextDouble();
            }
        }
        return matrix;
    }

    #region Kernel Fusion Benchmarks

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("KernelFusion", "Separate")]
    public int[] SeparateKernels_Baseline()
    {
        var data = GetDataBySize();

        // Simulate separate kernel execution without fusion
        var step1 = data.Select(x => x * 2).ToArray();
        var step2 = step1.Select(x => x + 10).ToArray();
        var step3 = step2.Where(x => x > 100).ToArray();
        var step4 = step3.Select(x => x / 3).ToArray();

        return step4;
    }

    [Benchmark]
    [BenchmarkCategory("KernelFusion", "Fused")]
    public async Task<int[]> FusedKernels_Optimized()
    {
        var data = GetDataBySize();
        var queryable = _optimizedProvider.CreateQueryable(data, GetAcceleratorByStrategy());

        // Single fused operation that should be optimized
        var result = await ExecuteOptimizedQuery(queryable
            .Select(x => x * 2)
            .Select(x => x + 10)
            .Where(x => x > 100)
            .Select(x => x / 3));

        return result;
    }

    #endregion

    #region Helper Methods

    private int[] GetDataBySize()
    {
        return DataSize switch
        {
            <= 1000 => _smallIntData,
            <= 10000 => _mediumIntData,
            _ => _largeIntData
        };
    }

    private IAccelerator GetAcceleratorByStrategy()
    {
        return Strategy switch
        {
            OptimizationStrategy.None => _cpuAccelerator,
            OptimizationStrategy.KernelFusion => _cpuAccelerator,
            OptimizationStrategy.MemoryOptimization => _cpuAccelerator,
            OptimizationStrategy.MLOptimization => null!, // Let ML choose
            OptimizationStrategy.All => null!, // Let ML choose
            _ => _cpuAccelerator
        };
    }

    private async Task<T[]> ExecuteOptimizedQuery<T>(IQueryable<T> queryable)
    {
        if (queryable.Provider is IComputeQueryProvider provider)
        {
            var result = await provider.ExecuteAsync<IEnumerable<T>>(queryable.Expression);
            return result.ToArray();
        }

        return queryable.ToArray();
    }

    #endregion
}