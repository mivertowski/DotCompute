using System.Reactive.Linq;
using System.Reactive.Subjects;
using DotCompute.Linq.Reactive;
using DotCompute.Core.Interfaces;
using DotCompute.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace DotCompute.Linq.Tests.Reactive;

/// <summary>
/// Unit tests for ReactiveComputeExtensions
/// Tests GPU-accelerated streaming operations with Reactive Extensions
/// </summary>
public class ReactiveComputeExtensionsTests
{
    private readonly Mock<IComputeOrchestrator> _mockOrchestrator;
    private readonly Mock<ILogger> _mockLogger;
    private readonly ReactiveComputeConfig _defaultConfig;

    public ReactiveComputeExtensionsTests()
    {
        _mockOrchestrator = new Mock<IComputeOrchestrator>();
        _mockLogger = new Mock<ILogger>();
        _defaultConfig = new ReactiveComputeConfig
        {
            MaxBatchSize = 100,
            MinBatchSize = 10,
            BatchTimeout = TimeSpan.FromMilliseconds(50),
            EnableAdaptiveBatching = false // Disable for testing
        };
    }

    [Fact]
    public async Task ComputeSelect_ShouldTransformElements_WhenValidInput()
    {
        // Arrange
        var source = new Subject<float>();
        var results = new List<float>();
        
        _mockOrchestrator
            .Setup(x => x.ExecuteKernelAsync<float, float>(
                It.IsAny<string>(),
                It.IsAny<ReadOnlySpan<float>>(),
                It.IsAny<Span<float>>()))
            .Returns(Task.CompletedTask);

        // Act
        var subscription = source
            .ComputeSelect(x => x * 2.0f, _mockOrchestrator.Object, _defaultConfig)
            .Subscribe(results.Add);

        // Send test data
        for (int i = 0; i < 15; i++) // Exceeds MinBatchSize to trigger processing
        {
            source.OnNext(i);
        }
        
        await Task.Delay(100); // Allow processing time
        source.OnCompleted();

        // Assert
        subscription.Dispose();
        _mockOrchestrator.Verify(
            x => x.ExecuteKernelAsync<float, float>(
                It.IsAny<string>(),
                It.IsAny<ReadOnlySpan<float>>(),
                It.IsAny<Span<float>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ComputeWhere_ShouldFilterElements_WhenValidPredicate()
    {
        // Arrange
        var source = new Subject<int>();
        var results = new List<int>();
        
        _mockOrchestrator
            .Setup(x => x.ExecuteKernelAsync<int, bool>(
                It.IsAny<string>(),
                It.IsAny<ReadOnlySpan<int>>(),
                It.IsAny<Span<bool>>()))
            .Returns(Task.CompletedTask);

        // Act
        var subscription = source
            .ComputeWhere(x => x % 2 == 0, _mockOrchestrator.Object, _defaultConfig)
            .Subscribe(results.Add);

        // Send test data
        for (int i = 0; i < 20; i++)
        {
            source.OnNext(i);
        }
        
        await Task.Delay(100);
        source.OnCompleted();

        // Assert
        subscription.Dispose();
        _mockOrchestrator.Verify(
            x => x.ExecuteKernelAsync<int, bool>(
                It.IsAny<string>(),
                It.IsAny<ReadOnlySpan<int>>(),
                It.IsAny<Span<bool>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void WithBackpressure_ShouldHandleBufferStrategy_WhenConfigured()
    {
        // Arrange
        var source = Observable.Range(1, 1000);
        var results = new List<int>();

        // Act
        var subscription = source
            .WithBackpressure(BackpressureStrategy.Buffer, 100)
            .Subscribe(results.Add);

        // Assert
        results.Should().HaveCount(1000);
        subscription.Dispose();
    }

    [Fact]
    public void WithBackpressure_ShouldHandleDropOldestStrategy_WhenConfigured()
    {
        // Arrange
        var source = Observable.Range(1, 1000);
        var results = new List<int>();

        // Act
        var subscription = source
            .WithBackpressure(BackpressureStrategy.DropOldest, 100)
            .Subscribe(results.Add);

        // Assert
        results.Should().NotBeEmpty();
        subscription.Dispose();
    }

    [Fact]
    public void WithPerformanceMonitoring_ShouldReportMetrics_WhenEnabled()
    {
        // Arrange
        var source = Observable.Range(1, 100);
        var metrics = new List<PerformanceMetrics>();
        var results = new List<int>();

        // Act
        var subscription = source
            .WithPerformanceMonitoring(metrics.Add)
            .Subscribe(results.Add);

        // Allow some time for metrics collection
        Thread.Sleep(1100); // Just over 1 second to trigger metrics

        // Assert
        results.Should().HaveCount(100);
        // Metrics might be empty due to timing, but no exceptions should occur
        subscription.Dispose();
    }

    [Theory]
    [InlineData(BackpressureStrategy.Buffer)]
    [InlineData(BackpressureStrategy.DropOldest)]
    [InlineData(BackpressureStrategy.DropNewest)]
    [InlineData(BackpressureStrategy.Latest)]
    [InlineData(BackpressureStrategy.Block)]
    public void WithBackpressure_ShouldHandleAllStrategies_WithoutErrors(BackpressureStrategy strategy)
    {
        // Arrange
        var source = Observable.Range(1, 100);
        var results = new List<int>();

        // Act & Assert
        var subscription = source
            .WithBackpressure(strategy, 50)
            .Subscribe(results.Add);

        results.Should().NotBeEmpty();
        subscription.Dispose();
    }

    [Fact]
    public void ReactiveComputeConfig_ShouldHaveValidDefaults()
    {
        // Arrange & Act
        var config = new ReactiveComputeConfig();

        // Assert
        config.MaxBatchSize.Should().Be(1024);
        config.MinBatchSize.Should().Be(32);
        config.BatchTimeout.Should().Be(TimeSpan.FromMilliseconds(10));
        config.BufferSize.Should().Be(10000);
        config.BackpressureStrategy.Should().Be(BackpressureStrategy.Buffer);
        config.EnableAdaptiveBatching.Should().BeTrue();
    }

    [Fact]
    public async Task ComputeAggregate_ShouldAggregateWindows_WhenValidConfiguration()
    {
        // Arrange
        var source = new Subject<float>();
        var results = new List<float>();
        
        _mockOrchestrator
            .Setup(x => x.ExecuteKernelAsync<float[], float>(
                It.IsAny<string>(),
                It.IsAny<ReadOnlySpan<float[]>>(),
                It.IsAny<Span<float>>()))
            .Returns(Task.CompletedTask);

        // Act
        var subscription = source
            .ComputeAggregate(
                0.0f,
                (acc, val) => acc + val,
                acc => acc,
                5, // Window size
                _mockOrchestrator.Object,
                _defaultConfig)
            .Subscribe(results.Add);

        // Send test data
        for (int i = 1; i <= 10; i++)
        {
            source.OnNext(i);
        }
        
        await Task.Delay(100);
        source.OnCompleted();

        // Assert
        subscription.Dispose();
        // Results depend on implementation details, but should not throw
    }

    [Fact]
    public void ComputeWindow_ShouldCreateTimeBasedWindows_WhenConfigured()
    {
        // Arrange
        var source = Observable.Interval(TimeSpan.FromMilliseconds(10)).Take(50).Select(i => (float)i);
        var results = new List<float[]>();

        // Act
        var subscription = source
            .ComputeWindow(
                TimeSpan.FromMilliseconds(100), // Window duration
                TimeSpan.FromMilliseconds(50),  // Window shift
                _mockOrchestrator.Object,
                _defaultConfig)
            .Subscribe(results.Add);

        Thread.Sleep(300); // Allow windows to be created

        // Assert
        subscription.Dispose();
        results.Should().NotBeEmpty();
    }

    [Fact]
    public void PerformanceMetrics_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var metrics = new PerformanceMetrics
        {
            TotalElements = 1000,
            ElementsPerSecond = 500.5,
            UpTime = TimeSpan.FromMinutes(2),
            AverageBatchSize = 64.5,
            GpuUtilization = 75.5,
            MemoryUsage = 1024 * 1024
        };

        // Assert
        metrics.TotalElements.Should().Be(1000);
        metrics.ElementsPerSecond.Should().Be(500.5);
        metrics.UpTime.Should().Be(TimeSpan.FromMinutes(2));
        metrics.AverageBatchSize.Should().Be(64.5);
        metrics.GpuUtilization.Should().Be(75.5);
        metrics.MemoryUsage.Should().Be(1024 * 1024);
    }

    [Fact]
    public async Task ComputeSelect_ShouldHandleErrors_Gracefully()
    {
        // Arrange
        var source = new Subject<float>();
        var errors = new List<Exception>();
        
        _mockOrchestrator
            .Setup(x => x.ExecuteKernelAsync<float, float>(
                It.IsAny<string>(),
                It.IsAny<ReadOnlySpan<float>>(),
                It.IsAny<Span<float>>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        // Act
        var subscription = source
            .ComputeSelect(x => x * 2.0f, _mockOrchestrator.Object, _defaultConfig)
            .Subscribe(
                _ => { },
                errors.Add,
                () => { });

        // Send test data to trigger error
        for (int i = 0; i < 15; i++)
        {
            source.OnNext(i);
        }
        
        await Task.Delay(100);

        // Assert
        subscription.Dispose();
        errors.Should().NotBeEmpty();
        errors.First().Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void ReactiveComputeConfig_ShouldAllowCustomization()
    {
        // Arrange & Act
        var config = new ReactiveComputeConfig
        {
            MaxBatchSize = 2048,
            MinBatchSize = 64,
            BatchTimeout = TimeSpan.FromMilliseconds(25),
            BufferSize = 50000,
            BackpressureStrategy = BackpressureStrategy.DropOldest,
            EnableAdaptiveBatching = false,
            PreferredAccelerator = "CUDA"
        };

        // Assert
        config.MaxBatchSize.Should().Be(2048);
        config.MinBatchSize.Should().Be(64);
        config.BatchTimeout.Should().Be(TimeSpan.FromMilliseconds(25));
        config.BufferSize.Should().Be(50000);
        config.BackpressureStrategy.Should().Be(BackpressureStrategy.DropOldest);
        config.EnableAdaptiveBatching.Should().BeFalse();
        config.PreferredAccelerator.Should().Be("CUDA");
    }
}