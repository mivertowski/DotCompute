// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// COMMENTED OUT: ProductionOptimizer class does not exist in Runtime module yet
// TODO: Uncomment when ProductionOptimizer is implemented in DotCompute.Runtime.Services.Optimization
/*
using DotCompute.Runtime.Services.Optimization;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.Statistics;

/// <summary>
/// Tests for ProductionOptimizer
/// </summary>
public sealed class ProductionOptimizerTests
{
    private readonly ILogger<ProductionOptimizer> _mockLogger;
    private readonly ProductionOptimizer _optimizer;

    public ProductionOptimizerTests()
    {
        _mockLogger = Substitute.For<ILogger<ProductionOptimizer>>();
        _optimizer = new ProductionOptimizer(_mockLogger);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new ProductionOptimizer(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task OptimizeAsync_WithValidWorkload_ReturnsOptimization()
    {
        // Arrange
        var workload = new Workload { Size = 1000 };

        // Act
        var result = await _optimizer.OptimizeAsync(workload);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task OptimizeAsync_WithNullWorkload_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = async () => await _optimizer.OptimizeAsync(null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SuggestBatchSize_ReturnsOptimalBatchSize()
    {
        // Arrange
        var workloadSize = 10000;

        // Act
        var batchSize = await _optimizer.SuggestBatchSize(workloadSize);

        // Assert
        batchSize.Should().BeGreaterThan(0);
        batchSize.Should().BeLessOrEqualTo(workloadSize);
    }

    [Fact]
    public async Task AnalyzePerformance_ReturnsAnalysis()
    {
        // Arrange
        var metrics = new Dictionary<string, double>
        {
            ["execution_time"] = 100.0,
            ["throughput"] = 1000.0
        };

        // Act
        var analysis = await _optimizer.AnalyzePerformance(metrics);

        // Assert
        analysis.Should().NotBeNull();
    }

    [Fact]
    public async Task GetOptimizationRecommendations_ReturnsRecommendations()
    {
        // Act
        var recommendations = await _optimizer.GetOptimizationRecommendations();

        // Assert
        recommendations.Should().NotBeNull();
    }

    [Fact]
    public async Task CacheOptimizationResult_StoresResult()
    {
        // Arrange
        var workload = new Workload { Size = 1000 };
        var optimization = await _optimizer.OptimizeAsync(workload);

        // Act
        _optimizer.CacheOptimizationResult(workload, optimization);

        // Assert - no exception thrown
    }

    // Helper classes for testing
    private class Workload
    {
        public int Size { get; set; }
    }
}
*/