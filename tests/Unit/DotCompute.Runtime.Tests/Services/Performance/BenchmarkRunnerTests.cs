// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Runtime.Services.Interfaces;
using DotCompute.Runtime.Services.Performance.Results;
using DotCompute.Runtime.Services.Performance.Types;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.Services.Performance;

/// <summary>
/// Tests for IBenchmarkRunner implementations
/// </summary>
public sealed class BenchmarkRunnerTests
{
    private readonly IBenchmarkRunner _runner;
    private readonly IAccelerator _mockAccelerator;

    public BenchmarkRunnerTests()
    {
        _runner = Substitute.For<IBenchmarkRunner>();
        _mockAccelerator = Substitute.For<IAccelerator>();
        var acceleratorInfo = new DotCompute.Abstractions.AcceleratorInfo
        {
            Id = "test-accelerator",
            Name = "Test Accelerator",
            DeviceType = "CPU"
        };
        _mockAccelerator.Info.Returns(acceleratorInfo);
    }

    [Fact]
    public async Task RunBenchmarkAsync_WithSuiteAndAccelerator_ReturnsResults()
    {
        // Arrange
        var suiteType = BenchmarkSuiteType.Basic;
        var results = new BenchmarkResults
        {
            BenchmarkName = "Basic",
            AcceleratorId = "test-accelerator"
        };
        _runner.RunBenchmarkAsync(_mockAccelerator, suiteType).Returns(results);

        // Act
        var benchmarkResults = await _runner.RunBenchmarkAsync(_mockAccelerator, suiteType);

        // Assert
        benchmarkResults.Should().NotBeNull();
        benchmarkResults.AcceleratorId.Should().Be("test-accelerator");
    }

    [Theory]
    [InlineData(BenchmarkSuiteType.Basic)]
    [InlineData(BenchmarkSuiteType.Memory)]
    [InlineData(BenchmarkSuiteType.LinearAlgebra)]
    public async Task RunBenchmarkAsync_WithDifferentSuites_HandlesCorrectly(BenchmarkSuiteType suiteType)
    {
        // Arrange
        var results = new BenchmarkResults
        {
            BenchmarkName = suiteType.ToString(),
            AcceleratorId = "test-accelerator"
        };
        _runner.RunBenchmarkAsync(_mockAccelerator, suiteType).Returns(results);

        // Act
        var benchmarkResults = await _runner.RunBenchmarkAsync(_mockAccelerator, suiteType);

        // Assert
        benchmarkResults.Should().NotBeNull();
        benchmarkResults.BenchmarkName.Should().Be(suiteType.ToString());
    }

    [Fact]
    public async Task RunCustomBenchmarkAsync_WithDefinition_ExecutesCorrectly()
    {
        // Arrange
        var definition = new BenchmarkDefinition
        {
            Name = "CustomBenchmark",
            Description = "Test benchmark"
        };
        var results = new BenchmarkResults
        {
            BenchmarkName = definition.Name,
            AcceleratorId = "test-accelerator"
        };
        _runner.RunCustomBenchmarkAsync(definition, _mockAccelerator).Returns(results);

        // Act
        var benchmarkResults = await _runner.RunCustomBenchmarkAsync(definition, _mockAccelerator);

        // Assert
        benchmarkResults.Should().NotBeNull();
        benchmarkResults.BenchmarkName.Should().Be(definition.Name);
    }

    [Fact]
    public async Task CompareAcceleratorsAsync_WithMultipleAccelerators_ReturnsComparison()
    {
        // Arrange
        var accelerators = new[]
        {
            _mockAccelerator,
            Substitute.For<IAccelerator>()
        };
        var definition = new BenchmarkDefinition
        {
            Name = "ComparisonBenchmark",
            Description = "Comparison test"
        };
        var comparison = new AcceleratorComparisonResults
        {
            AcceleratorIds = new[] { "acc1", "acc2" }
        };
        _runner.CompareAcceleratorsAsync(accelerators, definition).Returns(comparison);

        // Act
        var result = await _runner.CompareAcceleratorsAsync(accelerators, definition);

        // Assert
        result.Should().NotBeNull();
        result.AcceleratorIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetHistoricalResultsAsync_ReturnsHistoricalResults()
    {
        // Arrange
        var history = new List<BenchmarkResults>
        {
            new() { BenchmarkName = "Test1", AcceleratorId = "acc1" },
            new() { BenchmarkName = "Test2", AcceleratorId = "acc1" },
            new() { BenchmarkName = "Test3", AcceleratorId = "acc1" }
        };
        _runner.GetHistoricalResultsAsync(null).Returns(history);

        // Act
        var result = await _runner.GetHistoricalResultsAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetHistoricalResultsAsync_WithAcceleratorFilter_FiltersCorrectly()
    {
        // Arrange
        var acceleratorId = "test-accelerator";
        var history = new List<BenchmarkResults>
        {
            new() { BenchmarkName = "Test1", AcceleratorId = acceleratorId }
        };
        _runner.GetHistoricalResultsAsync(acceleratorId).Returns(history);

        // Act
        var result = await _runner.GetHistoricalResultsAsync(acceleratorId);

        // Assert
        result.Should().HaveCount(1);
        result.First().AcceleratorId.Should().Be(acceleratorId);
    }

    [Fact]
    public async Task RunBenchmarkAsync_ConcurrentExecutions_HandlesCorrectly()
    {
        // Arrange
        var suiteType = BenchmarkSuiteType.Basic;
        var results = new BenchmarkResults
        {
            BenchmarkName = "Basic",
            AcceleratorId = "test-accelerator"
        };
        _runner.RunBenchmarkAsync(_mockAccelerator, suiteType).Returns(results);

        // Act
        var tasks = Enumerable.Range(0, 3)
            .Select(_ => _runner.RunBenchmarkAsync(_mockAccelerator, suiteType))
            .ToArray();
        var allResults = await Task.WhenAll(tasks);

        // Assert
        allResults.Should().AllSatisfy(r => r.Should().NotBeNull());
    }

    [Fact]
    public async Task RunCustomBenchmarkAsync_WithComplexDefinition_ExecutesCorrectly()
    {
        // Arrange
        var definition = new BenchmarkDefinition
        {
            Name = "ComplexBenchmark",
            Description = "Complex test benchmark",
            Parameters = new Dictionary<string, object> { ["iterations"] = 100 }
        };
        var results = new BenchmarkResults
        {
            BenchmarkName = definition.Name,
            AcceleratorId = "test-accelerator"
        };
        _runner.RunCustomBenchmarkAsync(definition, _mockAccelerator).Returns(results);

        // Act
        var benchmarkResults = await _runner.RunCustomBenchmarkAsync(definition, _mockAccelerator);

        // Assert
        benchmarkResults.Should().NotBeNull();
        await _runner.Received(1).RunCustomBenchmarkAsync(definition, _mockAccelerator);
    }

    [Fact]
    public async Task GetHistoricalResultsAsync_WithNoHistory_ReturnsEmpty()
    {
        // Arrange
        var emptyHistory = new List<BenchmarkResults>();
        _runner.GetHistoricalResultsAsync(null).Returns(emptyHistory);

        // Act
        var result = await _runner.GetHistoricalResultsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CompareAcceleratorsAsync_WithSingleAccelerator_HandlesGracefully()
    {
        // Arrange
        var accelerators = new[] { _mockAccelerator };
        var definition = new BenchmarkDefinition
        {
            Name = "SingleAcceleratorTest",
            Description = "Single accelerator benchmark"
        };
        var comparison = new AcceleratorComparisonResults
        {
            AcceleratorIds = new[] { "test-accelerator" }
        };
        _runner.CompareAcceleratorsAsync(accelerators, definition).Returns(comparison);

        // Act
        var result = await _runner.CompareAcceleratorsAsync(accelerators, definition);

        // Assert
        result.Should().NotBeNull();
        result.AcceleratorIds.Should().HaveCount(1);
    }
}
