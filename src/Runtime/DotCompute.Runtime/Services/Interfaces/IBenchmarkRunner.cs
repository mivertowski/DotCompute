// <copyright file="IBenchmarkRunner.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions;
using DotCompute.Runtime.Services.Performance.Results;
using DotCompute.Runtime.Services.Performance.Types;

namespace DotCompute.Runtime.Services.Interfaces;

/// <summary>
/// Service for running performance benchmarks.
/// Provides standardized and custom benchmarking capabilities for accelerators.
/// </summary>
public interface IBenchmarkRunner
{
    /// <summary>
    /// Runs a standard benchmark suite.
    /// Executes predefined performance tests on the specified accelerator.
    /// </summary>
    /// <param name="accelerator">The accelerator device to benchmark.</param>
    /// <param name="suiteType">The type of benchmark suite to run.</param>
    /// <returns>Comprehensive benchmark results.</returns>
    Task<BenchmarkResults> RunBenchmarkAsync(IAccelerator accelerator, BenchmarkSuiteType suiteType);

    /// <summary>
    /// Runs a custom benchmark.
    /// Executes user-defined performance tests with specific parameters.
    /// </summary>
    /// <param name="benchmarkDefinition">The custom benchmark definition.</param>
    /// <param name="accelerator">The accelerator device to benchmark.</param>
    /// <returns>Custom benchmark results.</returns>
    Task<BenchmarkResults> RunCustomBenchmarkAsync(
        BenchmarkDefinition benchmarkDefinition,
        IAccelerator accelerator);

    /// <summary>
    /// Compares performance across multiple accelerators.
    /// Runs the same benchmark on different devices for comparison.
    /// </summary>
    /// <param name="accelerators">The collection of accelerators to compare.</param>
    /// <param name="benchmarkDefinition">The benchmark definition to use for comparison.</param>
    /// <returns>Comparative performance analysis results.</returns>
    Task<AcceleratorComparisonResults> CompareAcceleratorsAsync(
        IEnumerable<IAccelerator> accelerators,
        BenchmarkDefinition benchmarkDefinition);

    /// <summary>
    /// Gets historical benchmark results.
    /// Retrieves previously recorded benchmark data for trend analysis.
    /// </summary>
    /// <param name="acceleratorId">Optional filter by accelerator ID.</param>
    /// <returns>Collection of historical benchmark results.</returns>
    Task<IEnumerable<BenchmarkResults>> GetHistoricalResultsAsync(string? acceleratorId = null);
}