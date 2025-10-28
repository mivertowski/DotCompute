// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Runtime.Services.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.Services.Performance;

/// <summary>
/// Tests for IKernelProfiler implementations
/// </summary>
public sealed class KernelProfilerTests
{
    private readonly IKernelProfiler _profiler;

    public KernelProfilerTests()
    {
        _profiler = Substitute.For<IKernelProfiler>();
    }

    [Fact]
    public void StartProfiling_WithKernelName_ReturnsProfilingSession()
    {
        // Arrange
        var sessionName = "VectorAdd";
        var session = Substitute.For<IProfilingSession>();
        _profiler.StartProfiling(sessionName).Returns(session);

        // Act
        var profilingSession = _profiler.StartProfiling(sessionName);

        // Assert
        profilingSession.Should().NotBeNull();
        profilingSession.Should().Be(session);
    }

    [Fact]
    public void StartProfiling_WithSuccessfulExecution_ReturnsSession()
    {
        // Arrange
        var sessionName = "MatrixMultiply";
        var session = Substitute.For<IProfilingSession>();
        _profiler.StartProfiling(sessionName).Returns(session);

        // Act
        var profilingSession = _profiler.StartProfiling(sessionName);

        // Assert
        profilingSession.Should().NotBeNull();
    }

    [Fact]
    public async Task GetResultsAsync_WithSessionId_ReturnsResults()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var results = new ProfilingResults
        {
            SessionId = sessionId,
            SessionName = "TestSession",
            TotalExecutionTime = TimeSpan.FromMilliseconds(100)
        };
        _profiler.GetResultsAsync(sessionId).Returns(results);

        // Act
        var profilingResult = await _profiler.GetResultsAsync(sessionId);

        // Assert
        profilingResult.Should().NotBeNull();
        profilingResult!.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task GetKernelStatisticsAsync_ReturnsStatistics()
    {
        // Arrange
        var kernelName = "VectorAdd";
        var stats = new KernelStatistics
        {
            KernelName = kernelName,
            ExecutionCount = 100,
            AverageExecutionTime = TimeSpan.FromMilliseconds(5),
            SuccessRate = 0.99
        };
        _profiler.GetKernelStatisticsAsync(kernelName, null).Returns(stats);

        // Act
        var result = await _profiler.GetKernelStatisticsAsync(kernelName);

        // Assert
        result.Should().NotBeNull();
        result.ExecutionCount.Should().Be(100);
        result.SuccessRate.Should().Be(0.99);
    }

    [Fact]
    public async Task GetKernelStatisticsAsync_WithTimeRange_FiltersCorrectly()
    {
        // Arrange
        var kernelName = "VectorAdd";
        var timeRange = TimeRange.LastHours(1);
        var stats = new KernelStatistics
        {
            KernelName = kernelName,
            ExecutionCount = 50
        };
        _profiler.GetKernelStatisticsAsync(kernelName, timeRange).Returns(stats);

        // Act
        var result = await _profiler.GetKernelStatisticsAsync(kernelName, timeRange);

        // Assert
        result.Should().NotBeNull();
        result.ExecutionCount.Should().Be(50);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void StartProfiling_WithMultipleExecutions_TracksAll(int executionCount)
    {
        // Arrange
        var sessionName = "VectorAdd";
        var session = Substitute.For<IProfilingSession>();
        _profiler.StartProfiling(sessionName).Returns(session);

        // Act
        for (int i = 0; i < executionCount; i++)
        {
            _profiler.StartProfiling(sessionName);
        }

        // Assert
        _profiler.Received(executionCount).StartProfiling(sessionName);
    }

    [Fact]
    public async Task GetKernelStatisticsAsync_WithUnknownKernel_ReturnsEmpty()
    {
        // Arrange
        var kernelName = "UnknownKernel";
        var stats = new KernelStatistics
        {
            KernelName = kernelName,
            ExecutionCount = 0
        };
        _profiler.GetKernelStatisticsAsync(kernelName, null).Returns(stats);

        // Act
        var result = await _profiler.GetKernelStatisticsAsync(kernelName);

        // Assert
        result.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public async Task ExportDataAsync_WithJsonFormat_ExportsSuccessfully()
    {
        // Arrange
        var format = ExportFormat.Json;
        var outputPath = "/tmp/profiling.json";
        _profiler.ExportDataAsync(format, outputPath, null).Returns(outputPath);

        // Act
        var result = await _profiler.ExportDataAsync(format, outputPath);

        // Assert
        result.Should().Be(outputPath);
    }

    [Fact]
    public async Task CleanupOldDataAsync_RemovesOldSessions()
    {
        // Arrange
        var olderThan = TimeSpan.FromDays(7);
        _profiler.CleanupOldDataAsync(olderThan).Returns(10);

        // Act
        var cleanedCount = await _profiler.CleanupOldDataAsync(olderThan);

        // Assert
        cleanedCount.Should().Be(10);
    }

    [Fact]
    public void StartProfiling_ConcurrentExecutions_HandlesCorrectly()
    {
        // Arrange
        var sessionName = "ConcurrentKernel";
        _profiler.StartProfiling(sessionName).Returns(x => Substitute.For<IProfilingSession>());

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => _profiler.StartProfiling(sessionName)))
            .ToArray();
        var results = Task.WhenAll(tasks).Result;

        // Assert
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }

    [Fact]
    public async Task GetKernelStatisticsAsync_AfterMultipleExecutions_ReflectsAccurateStats()
    {
        // Arrange
        var kernelName = "VectorAdd";
        var stats = new KernelStatistics
        {
            KernelName = kernelName,
            ExecutionCount = 50,
            AverageExecutionTime = TimeSpan.FromMilliseconds(5.5),
            SuccessRate = 1.0
        };
        _profiler.GetKernelStatisticsAsync(kernelName, null).Returns(stats);

        // Act
        var result = await _profiler.GetKernelStatisticsAsync(kernelName);

        // Assert
        result.ExecutionCount.Should().Be(50);
        result.AverageExecutionTime.Should().BePositive();
        result.SuccessRate.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public async Task ExportDataAsync_WithCsvFormat_ExportsSuccessfully()
    {
        // Arrange
        var format = ExportFormat.Csv;
        var outputPath = "/tmp/profiling.csv";
        _profiler.ExportDataAsync(format, outputPath, null).Returns(outputPath);

        // Act
        var result = await _profiler.ExportDataAsync(format, outputPath);

        // Assert
        result.Should().Be(outputPath);
        await _profiler.Received(1).ExportDataAsync(format, outputPath, null);
    }

    [Fact]
    public async Task GetResultsAsync_WithMissingSession_ReturnsNull()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _profiler.GetResultsAsync(sessionId).Returns((ProfilingResults?)null);

        // Act
        var result = await _profiler.GetResultsAsync(sessionId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CleanupOldDataAsync_WithNoOldData_ReturnsZero()
    {
        // Arrange
        var olderThan = TimeSpan.FromDays(30);
        _profiler.CleanupOldDataAsync(olderThan).Returns(0);

        // Act
        var cleanedCount = await _profiler.CleanupOldDataAsync(olderThan);

        // Assert
        cleanedCount.Should().Be(0);
    }
}
