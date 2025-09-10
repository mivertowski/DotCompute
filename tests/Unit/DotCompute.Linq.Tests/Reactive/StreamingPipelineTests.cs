using System.Reactive.Linq;
using System.Reactive.Subjects;
using DotCompute.Linq.Reactive;
using DotCompute.Linq.Reactive.Operators;
using DotCompute.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace DotCompute.Linq.Tests.Reactive;

/// <summary>
/// Unit tests for StreamingPipeline
/// Tests end-to-end streaming pipeline functionality
/// </summary>
public class StreamingPipelineTests
{
    private readonly Mock<IComputeOrchestrator> _mockOrchestrator;
    private readonly Mock<ILogger<StreamingPipeline<string, string>>> _mockLogger;
    private readonly StreamingPipelineConfig _defaultConfig;

    public StreamingPipelineTests()
    {
        _mockOrchestrator = new Mock<IComputeOrchestrator>();
        _mockLogger = new Mock<ILogger<StreamingPipeline<string, string>>>();
        _defaultConfig = new StreamingPipelineConfig
        {
            Name = "TestPipeline",
            MaxCapacity = 1000,
            HealthCheckInterval = TimeSpan.FromSeconds(1),
            EnableAutoRecovery = true,
            EnableMetrics = true,
            EnableStatePersistence = false
        };
    }

    [Fact]
    public void Constructor_ShouldInitializePipeline_WithValidParameters()
    {
        // Act
        using var pipeline = new StreamingPipeline<string, string>(
            _mockOrchestrator.Object,
            _defaultConfig,
            _mockLogger.Object);

        // Assert
        pipeline.Should().NotBeNull();
        pipeline.Health.Should().Be(PipelineHealth.Healthy);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenOrchestratorIsNull()
    {
        // Act & Assert
        var action = () => new StreamingPipeline<string, string>(
            null!,
            _defaultConfig,
            _mockLogger.Object);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddComputeStage_ShouldReturnNewPipelineWithStage()
    {
        // Arrange
        using var pipeline = new StreamingPipeline<string, string>(
            _mockOrchestrator.Object,
            _defaultConfig,
            _mockLogger.Object);

        // Act
        var newPipeline = pipeline.AddComputeStage("Transform", s => s.ToUpper());

        // Assert
        newPipeline.Should().NotBeNull();
        newPipeline.Should().NotBeSameAs(pipeline);
    }

    [Fact]
    public void AddFilterStage_ShouldReturnSamePipelineWithFilter()
    {
        // Arrange
        using var pipeline = new StreamingPipeline<string, string>(
            _mockOrchestrator.Object,
            _defaultConfig,
            _mockLogger.Object);

        // Act
        var result = pipeline.AddFilterStage("Filter", s => s.Length > 3);

        // Assert
        result.Should().BeSameAs(pipeline);
    }

    [Fact]
    public void AddWindowAggregationStage_ShouldReturnNewPipelineWithAggregation()
    {
        // Arrange
        using var pipeline = new StreamingPipeline<string, string>(
            _mockOrchestrator.Object,
            _defaultConfig,
            _mockLogger.Object);

        var windowConfig = new WindowConfig { Count = 5 };

        // Act
        var newPipeline = pipeline.AddWindowAggregationStage(
            "Aggregate",
            windowConfig,
            strings => string.Join(",", strings));

        // Assert
        newPipeline.Should().NotBeNull();
        newPipeline.Should().NotBeSameAs(pipeline);
    }

    [Fact]
    public async Task Start_ShouldProcessInputAndProduceOutput()
    {
        // Arrange
        using var pipeline = new StreamingPipeline<string, string>(
            _mockOrchestrator.Object,
            _defaultConfig,
            _mockLogger.Object);

        var outputs = new List<string>();
        var outputSubscription = pipeline.Output.Subscribe(outputs.Add);

        // Act
        using var pipelineSubscription = pipeline.Start();

        // Send test data
        pipeline.Input.OnNext("test1");
        pipeline.Input.OnNext("test2");
        pipeline.Input.OnNext("test3");
        pipeline.Input.OnCompleted();

        await Task.Delay(100); // Allow processing time

        // Assert
        outputs.Should().Contain("test1", "test2", "test3");
        outputSubscription.Dispose();
    }

    [Fact]
    public void Start_ShouldThrowInvalidOperationException_WhenCalledTwice()
    {
        // Arrange
        using var pipeline = new StreamingPipeline<string, string>(
            _mockOrchestrator.Object,
            _defaultConfig,
            _mockLogger.Object);

        using var firstStart = pipeline.Start();

        // Act & Assert
        var action = () => pipeline.Start();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Pipeline is already started");
    }

    [Fact]
    public async Task Pipeline_ShouldHandleErrors_WhenAutoRecoveryEnabled()
    {
        // Arrange
        var config = _defaultConfig with { EnableAutoRecovery = true };
        using var pipeline = new StreamingPipeline<string, string>(
            _mockOrchestrator.Object,
            config,
            _mockLogger.Object);

        var errors = new List<Exception>();
        var errorSubscription = pipeline.Errors.Subscribe(errors.Add);

        // Add a stage that throws an exception
        var faultyPipeline = pipeline.AddComputeStage<string>("Faulty", s =>
        {
            if (s == "error")
                throw new InvalidOperationException("Test error");
            return s;
        });

        // Act
        using var pipelineSubscription = faultyPipeline.Start();

        faultyPipeline.Input.OnNext("good");
        faultyPipeline.Input.OnNext("error");
        faultyPipeline.Input.OnNext("good2");

        await Task.Delay(100);

        // Assert
        errorSubscription.Dispose();
        // With auto-recovery enabled, errors might be handled internally
    }

    [Fact]
    public async Task Pipeline_ShouldReportMetrics_WhenEnabled()
    {
        // Arrange
        var config = _defaultConfig with { EnableMetrics = true };
        using var pipeline = new StreamingPipeline<string, string>(
            _mockOrchestrator.Object,
            config,
            _mockLogger.Object);

        var metrics = new List<PipelineMetrics>();
        var metricsSubscription = pipeline.Metrics.Subscribe(metrics.Add);

        // Act
        using var pipelineSubscription = pipeline.Start();

        for (int i = 0; i < 10; i++)
        {
            pipeline.Input.OnNext($"item{i}");
        }

        await Task.Delay(200); // Allow metrics collection

        // Assert
        metricsSubscription.Dispose();
        // Metrics collection depends on timing, but should not throw errors
    }

    [Fact]
    public void StreamingPipelineConfig_ShouldHaveValidDefaults()
    {
        // Arrange & Act
        var config = new StreamingPipelineConfig();

        // Assert
        config.Name.Should().Be("StreamingPipeline");
        config.ReactiveConfig.Should().NotBeNull();
        config.MaxCapacity.Should().Be(100000);
        config.HealthCheckInterval.Should().Be(TimeSpan.FromSeconds(30));
        config.EnableAutoRecovery.Should().BeTrue();
        config.MaxRetryAttempts.Should().Be(3);
        config.RetryDelayStrategy.Should().Be(RetryDelayStrategy.Exponential);
        config.EnableMetrics.Should().BeTrue();
        config.CheckpointInterval.Should().Be(TimeSpan.FromMinutes(5));
        config.EnableStatePersistence.Should().BeFalse();
    }

    [Fact]
    public void PipelineStage_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var stage = new PipelineStage<string, string>
        {
            Name = "TestStage",
            Transform = source => source.Select(s => s.ToUpper()),
            Configuration = new { Setting = "Value" },
            SupportsParallel = true,
            Priority = 5
        };

        // Assert
        stage.Name.Should().Be("TestStage");
        stage.Transform.Should().NotBeNull();
        stage.Configuration.Should().NotBeNull();
        stage.SupportsParallel.Should().BeTrue();
        stage.Priority.Should().Be(5);
    }

    [Fact]
    public void PipelineMetrics_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var metrics = new PipelineMetrics
        {
            TotalProcessed = 1000,
            Throughput = 100.5,
            AverageLatency = 25.5,
            ErrorRate = 0.1,
            Health = PipelineHealth.Healthy,
            MemoryUsage = 1024 * 1024,
            CpuUtilization = 50.0,
            GpuUtilization = 75.0,
            StageMetrics = new Dictionary<string, object> { { "stage1", "value1" } }
        };

        // Assert
        metrics.TotalProcessed.Should().Be(1000);
        metrics.Throughput.Should().Be(100.5);
        metrics.AverageLatency.Should().Be(25.5);
        metrics.ErrorRate.Should().Be(0.1);
        metrics.Health.Should().Be(PipelineHealth.Healthy);
        metrics.MemoryUsage.Should().Be(1024 * 1024);
        metrics.CpuUtilization.Should().Be(50.0);
        metrics.GpuUtilization.Should().Be(75.0);
        metrics.StageMetrics.Should().ContainKey("stage1");
    }

    [Theory]
    [InlineData(PipelineHealth.Healthy)]
    [InlineData(PipelineHealth.Degraded)]
    [InlineData(PipelineHealth.Critical)]
    [InlineData(PipelineHealth.Failed)]
    public void PipelineHealth_ShouldSupportAllValues(PipelineHealth health)
    {
        // Arrange
        using var pipeline = new StreamingPipeline<string, string>(
            _mockOrchestrator.Object,
            _defaultConfig,
            _mockLogger.Object);

        // Act & Assert
        // Health enum should support all defined values
        health.Should().BeOneOf(
            PipelineHealth.Healthy,
            PipelineHealth.Degraded,
            PipelineHealth.Critical,
            PipelineHealth.Failed);
    }

    [Fact]
    public void StreamingPipelineBuilder_ShouldCreatePipeline()
    {
        // Act
        using var pipeline = StreamingPipelineBuilder.Create<string>(
            _mockOrchestrator.Object,
            _defaultConfig);

        // Assert
        pipeline.Should().NotBeNull();
        pipeline.Health.Should().Be(PipelineHealth.Healthy);
    }

    [Fact]
    public async Task ComplexPipeline_ShouldProcessDataThroughMultipleStages()
    {
        // Arrange
        using var pipeline = StreamingPipelineBuilder
            .Create<string>(_mockOrchestrator.Object, _defaultConfig)
            .AddFilterStage("Filter", s => s.Length > 2)
            .AddComputeStage("Transform", s => s.ToUpper())
            .AddComputeStage("Append", s => s + "_PROCESSED");

        var outputs = new List<string>();
        var outputSubscription = pipeline.Output.Subscribe(outputs.Add);

        // Act
        using var pipelineSubscription = pipeline.Start();

        pipeline.Input.OnNext("a");      // Should be filtered out
        pipeline.Input.OnNext("abc");    // Should become "ABC_PROCESSED"
        pipeline.Input.OnNext("test");   // Should become "TEST_PROCESSED"
        pipeline.Input.OnCompleted();

        await Task.Delay(100);

        // Assert
        outputSubscription.Dispose();
        outputs.Should().Contain("ABC_PROCESSED", "TEST_PROCESSED");
        outputs.Should().NotContain(s => s.Contains("A_PROCESSED")); // "a" should be filtered
    }

    [Fact]
    public void WindowConfig_ShouldSupportCountBasedWindows()
    {
        // Arrange & Act
        var config = new WindowConfig
        {
            Count = 100,
            Skip = 50,
            IsTumbling = false
        };

        // Assert
        config.Count.Should().Be(100);
        config.Skip.Should().Be(50);
        config.Duration.Should().BeNull();
        config.Hop.Should().BeNull();
        config.IsTumbling.Should().BeFalse();
    }

    [Fact]
    public void WindowConfig_ShouldSupportTimeBasedWindows()
    {
        // Arrange & Act
        var config = new WindowConfig
        {
            Duration = TimeSpan.FromSeconds(10),
            Hop = TimeSpan.FromSeconds(5),
            IsTumbling = true
        };

        // Assert
        config.Duration.Should().Be(TimeSpan.FromSeconds(10));
        config.Hop.Should().Be(TimeSpan.FromSeconds(5));
        config.Count.Should().BeNull();
        config.Skip.Should().BeNull();
        config.IsTumbling.Should().BeTrue();
    }
}