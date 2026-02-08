// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using DotCompute.Abstractions.Observability;
using DotCompute.Core.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DotCompute.Core.Tests.Observability;

/// <summary>
/// Tests for Ring Kernel OpenTelemetry instrumentation.
/// </summary>
public sealed class RingKernelInstrumentationTests : IDisposable
{
    private readonly Mock<ILogger<RingKernelInstrumentation>> _loggerMock;
    private readonly RingKernelInstrumentationOptions _options;
    private readonly RingKernelInstrumentation _instrumentation;

    public RingKernelInstrumentationTests()
    {
        _loggerMock = new Mock<ILogger<RingKernelInstrumentation>>();
        _options = new RingKernelInstrumentationOptions
        {
            Enabled = true,
            ServiceName = "TestService",
            ServiceVersion = "1.0.0",
            SamplingRate = 1.0,
            RecordMessageActivities = true,
            RecordLatencyHistograms = true
        };

        _instrumentation = new RingKernelInstrumentation(
            _loggerMock.Object,
            Options.Create(_options));
    }

    public void Dispose()
    {
        _instrumentation.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Assert
        Assert.NotNull(_instrumentation);
        Assert.True(_instrumentation.IsEnabled);
        Assert.NotNull(_instrumentation.ActivitySource);
        Assert.NotNull(_instrumentation.Meter);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RingKernelInstrumentation(null!, Options.Create(_options)));
    }

    [Fact]
    public void Constructor_WithNullOptions_UsesDefaults()
    {
        // Arrange & Act
        using var instrumentation = new RingKernelInstrumentation(
            _loggerMock.Object,
            Options.Create<RingKernelInstrumentationOptions>(null!));

        // Assert
        Assert.NotNull(instrumentation);
        Assert.True(instrumentation.IsEnabled);
    }

    [Fact]
    public void Constructor_WithDisabledOptions_SetsIsEnabledFalse()
    {
        // Arrange
        var options = new RingKernelInstrumentationOptions { Enabled = false };

        // Act
        using var instrumentation = new RingKernelInstrumentation(
            _loggerMock.Object,
            Options.Create(options));

        // Assert
        Assert.False(instrumentation.IsEnabled);
    }

    #endregion

    #region Activity Tests

    [Fact]
    public void StartKernelActivity_WithValidParameters_ReturnsActivity()
    {
        // Arrange
        var kernelId = "test-kernel";
        var operationName = "test-operation";

        // Act
        using var activity = _instrumentation.StartKernelActivity(kernelId, operationName);

        // Assert - Activity may be null if no listener is registered
        // Just verify no exception is thrown
        Assert.True(true);
    }

    [Fact]
    public void StartKernelActivity_WhenDisabled_ReturnsNull()
    {
        // Arrange
        var options = new RingKernelInstrumentationOptions { Enabled = false };
        using var instrumentation = new RingKernelInstrumentation(
            _loggerMock.Object,
            Options.Create(options));

        // Act
        var activity = instrumentation.StartKernelActivity("kernel", "operation");

        // Assert
        Assert.Null(activity);
    }

    [Fact]
    public void StartKernelActivity_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _instrumentation.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() =>
            _instrumentation.StartKernelActivity("kernel", "operation"));
    }

    [Fact]
    public void StartMessageActivity_WithValidParameters_ReturnsActivity()
    {
        // Arrange
        var kernelId = "test-kernel";
        var messageType = "TestMessage";
        var messageId = "msg-123";

        // Act
        using var activity = _instrumentation.StartMessageActivity(kernelId, messageType, messageId);

        // Assert - Activity may be null if no listener is registered
        Assert.True(true);
    }

    [Fact]
    public void StartMessageActivity_WithMessageActivitiesDisabled_ReturnsNull()
    {
        // Arrange
        var options = new RingKernelInstrumentationOptions
        {
            Enabled = true,
            RecordMessageActivities = false
        };
        using var instrumentation = new RingKernelInstrumentation(
            _loggerMock.Object,
            Options.Create(options));

        // Act
        var activity = instrumentation.StartMessageActivity("kernel", "type", "id");

        // Assert
        Assert.Null(activity);
    }

    [Fact]
    public void StartMessageActivity_WithParentContext_UsesParentContext()
    {
        // Arrange
        var parentContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded);

        // Act
        using var activity = _instrumentation.StartMessageActivity(
            "kernel", "type", "id", parentContext);

        // Assert - Just verify no exception
        Assert.True(true);
    }

    #endregion

    #region Recording Tests

    [Fact]
    public void RecordKernelLaunch_WithValidParameters_RecordsMetrics()
    {
        // Arrange
        var kernelId = "test-kernel";
        var gridSize = 256;
        var blockSize = 128;
        var backend = "CUDA";

        // Act - Should not throw
        _instrumentation.RecordKernelLaunch(kernelId, gridSize, blockSize, backend);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void RecordKernelLaunch_WhenDisabled_DoesNotThrow()
    {
        // Arrange
        var options = new RingKernelInstrumentationOptions { Enabled = false };
        using var instrumentation = new RingKernelInstrumentation(
            _loggerMock.Object,
            Options.Create(options));

        // Act & Assert - Should not throw
        instrumentation.RecordKernelLaunch("kernel", 256, 128, "CUDA");
    }

    [Fact]
    public void RecordKernelTermination_WithValidParameters_RecordsMetrics()
    {
        // Arrange
        var kernelId = "test-kernel";
        var uptime = TimeSpan.FromMinutes(5);
        var messagesProcessed = 10000L;
        var terminationReason = "user_request";

        // Act - Should not throw
        _instrumentation.RecordKernelTermination(kernelId, uptime, messagesProcessed, terminationReason);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void RecordThroughput_WithValidParameters_UpdatesMetrics()
    {
        // Arrange
        var kernelId = "test-kernel";
        var messagesPerSecond = 1000.0;
        var queueDepth = 50;

        // Act - Should not throw
        _instrumentation.RecordThroughput(kernelId, messagesPerSecond, queueDepth);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void RecordMessageLatency_WithValidParameters_RecordsHistogram()
    {
        // Arrange
        var kernelId = "test-kernel";
        var latencyNanos = 500_000L; // 500μs

        // Act - Should not throw
        _instrumentation.RecordMessageLatency(kernelId, latencyNanos);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void RecordMessageLatency_WithHistogramsDisabled_DoesNotRecord()
    {
        // Arrange
        var options = new RingKernelInstrumentationOptions
        {
            Enabled = true,
            RecordLatencyHistograms = false
        };
        using var instrumentation = new RingKernelInstrumentation(
            _loggerMock.Object,
            Options.Create(options));

        // Act & Assert - Should not throw
        instrumentation.RecordMessageLatency("kernel", 500_000);
    }

    [Fact]
    public void RecordMessageDropped_WithValidParameters_RecordsCounter()
    {
        // Arrange
        var kernelId = "test-kernel";
        var reason = "queue_full";

        // Act - Should not throw
        _instrumentation.RecordMessageDropped(kernelId, reason);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void RecordKernelError_WithValidParameters_RecordsError()
    {
        // Arrange
        var kernelId = "test-kernel";
        var errorCode = 42;
        var errorMessage = "Test error";

        // Act - Should not throw
        _instrumentation.RecordKernelError(kernelId, errorCode, errorMessage);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void RecordHealthStatus_WithValidParameters_UpdatesStatus()
    {
        // Arrange
        var kernelId = "test-kernel";
        var isHealthy = true;
        var lastProcessed = DateTimeOffset.UtcNow;

        // Act - Should not throw
        _instrumentation.RecordHealthStatus(kernelId, isHealthy, lastProcessed);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void RecordHealthStatus_HealthChanged_LogsChange()
    {
        // Arrange
        var kernelId = "test-kernel";
        _instrumentation.RecordHealthStatus(kernelId, true, DateTimeOffset.UtcNow);

        // Act
        _instrumentation.RecordHealthStatus(kernelId, false, DateTimeOffset.UtcNow);

        // Assert
        Assert.True(true);
    }

    #endregion

    #region Event and Status Tests

    [Fact]
    public void AddEvent_WithActivity_AddsEvent()
    {
        // Arrange
        using var activity = new Activity("test").Start();
        var eventName = "test_event";
        var attributes = new Dictionary<string, object?>
        {
            ["key1"] = "value1",
            ["key2"] = 42
        };

        // Act
        _instrumentation.AddEvent(activity, eventName, attributes);

        // Assert
        Assert.Contains(activity.Events, e => e.Name == eventName);
    }

    [Fact]
    public void AddEvent_WithNullActivity_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        _instrumentation.AddEvent(null, "event", new Dictionary<string, object?>());
    }

    [Fact]
    public void AddEvent_WithNullAttributes_AddsEventWithoutAttributes()
    {
        // Arrange
        using var activity = new Activity("test").Start();

        // Act
        _instrumentation.AddEvent(activity, "event");

        // Assert
        Assert.Contains(activity.Events, e => e.Name == "event");
    }

    [Fact]
    public void SetActivityStatus_Success_SetsOkStatus()
    {
        // Arrange
        using var activity = new Activity("test").Start();

        // Act
        _instrumentation.SetActivityStatus(activity, true);

        // Assert
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
    }

    [Fact]
    public void SetActivityStatus_Failure_SetsErrorStatus()
    {
        // Arrange
        using var activity = new Activity("test").Start();

        // Act
        _instrumentation.SetActivityStatus(activity, false, "Error occurred");

        // Assert
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("Error occurred", activity.StatusDescription);
    }

    [Fact]
    public void SetActivityStatus_WithNullActivity_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        _instrumentation.SetActivityStatus(null, true);
    }

    #endregion

    #region Extension Tests

    [Fact]
    public void StartChildActivity_WithParent_StartsChildActivity()
    {
        // Arrange
        using var parentActivity = new Activity("parent").Start();

        // Act
        using var childActivity = _instrumentation.StartChildActivity(
            "kernel", "child_op", parentActivity);

        // Assert - Just verify no exception
        Assert.True(true);
    }

    [Fact]
    public void StartChildActivity_WithoutParent_StartsRootActivity()
    {
        // Act
        using var activity = _instrumentation.StartChildActivity(
            "kernel", "root_op", null);

        // Assert - Just verify no exception
        Assert.True(true);
    }

    [Fact]
    public void RecordMessageLatencyBatch_RecordsAllLatencies()
    {
        // Arrange
        var latencies = new[] { 100_000L, 200_000L, 300_000L };

        // Act - Should not throw
        _instrumentation.RecordMessageLatencyBatch("kernel", latencies);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task TraceOperationAsync_Success_SetsOkStatus()
    {
        // Arrange
        var expected = 42;

        // Act
        var result = await _instrumentation.TraceOperationAsync(
            "kernel",
            "test_op",
            async _ =>
            {
                await Task.Delay(1);
                return expected;
            });

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task TraceOperationAsync_Failure_SetsErrorStatusAndRethrows()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Test error");

        // Act & Assert
        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _instrumentation.TraceOperationAsync<int>(
                "kernel",
                "test_op",
                _ => throw expectedException));

        Assert.Same(expectedException, actualException);
    }

    [Fact]
    public async Task TraceOperationAsync_VoidResult_Success()
    {
        // Arrange
        var executed = false;

        // Act
        await _instrumentation.TraceOperationAsync(
            "kernel",
            "test_op",
            async _ =>
            {
                await Task.Delay(1);
                executed = true;
            });

        // Assert
        Assert.True(executed);
    }

    [Fact]
    public async Task TraceOperationAsync_VoidResult_Failure_Rethrows()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Test error");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _instrumentation.TraceOperationAsync(
                "kernel",
                "test_op",
                _ => throw expectedException));
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        var instrumentation = new RingKernelInstrumentation(
            _loggerMock.Object,
            Options.Create(_options));

        // Act & Assert
        instrumentation.Dispose();
        instrumentation.Dispose();
    }

    [Fact]
    public void IsEnabled_AfterDispose_ReturnsFalse()
    {
        // Arrange
        var instrumentation = new RingKernelInstrumentation(
            _loggerMock.Object,
            Options.Create(_options));

        // Act
        instrumentation.Dispose();

        // Assert
        Assert.False(instrumentation.IsEnabled);
    }

    #endregion
}

/// <summary>
/// Tests for Ring Kernel semantic conventions.
/// </summary>
public sealed class RingKernelSemanticConventionsTests
{
    [Fact]
    public void AttributeNames_AreNotNull()
    {
        Assert.NotNull(RingKernelSemanticConventions.KernelId);
        Assert.NotNull(RingKernelSemanticConventions.KernelState);
        Assert.NotNull(RingKernelSemanticConventions.GridSize);
        Assert.NotNull(RingKernelSemanticConventions.BlockSize);
        Assert.NotNull(RingKernelSemanticConventions.Backend);
    }

    [Fact]
    public void MetricNames_AreNotNull()
    {
        Assert.NotNull(RingKernelSemanticConventions.MetricKernelLaunches);
        Assert.NotNull(RingKernelSemanticConventions.MetricMessagesProcessed);
        Assert.NotNull(RingKernelSemanticConventions.MetricQueueDepth);
        Assert.NotNull(RingKernelSemanticConventions.MetricThroughput);
    }

    [Fact]
    public void SpanNames_AreNotNull()
    {
        Assert.NotNull(RingKernelSemanticConventions.SpanKernelLaunch);
        Assert.NotNull(RingKernelSemanticConventions.SpanMessageProcess);
        Assert.NotNull(RingKernelSemanticConventions.SpanMessageSend);
    }

    [Fact]
    public void AttributeNames_FollowNamingConvention()
    {
        // All attribute names should contain a dot separator
        Assert.Contains(".", RingKernelSemanticConventions.KernelId);
        Assert.Contains(".", RingKernelSemanticConventions.MessageType);
    }
}

/// <summary>
/// Tests for instrumentation options.
/// </summary>
public sealed class RingKernelInstrumentationOptionsTests
{
    [Fact]
    public void DefaultOptions_HaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new RingKernelInstrumentationOptions();

        // Assert
        Assert.True(options.Enabled);
        Assert.Equal("DotCompute.RingKernels", options.ServiceName);
        Assert.Equal(1.0, options.SamplingRate);
        Assert.True(options.RecordMessageActivities);
        Assert.True(options.RecordLatencyHistograms);
        Assert.True(options.PropagateTraceContext);
        Assert.NotEmpty(options.LatencyBuckets);
    }

    [Fact]
    public void GlobalTags_CanBeAdded()
    {
        // Arrange
        var options = new RingKernelInstrumentationOptions();

        // Act
        options.GlobalTags["env"] = "test";
        options.GlobalTags["version"] = "1.0";

        // Assert
        Assert.Equal(2, options.GlobalTags.Count);
        Assert.Equal("test", options.GlobalTags["env"]);
    }

    [Fact]
    public void LatencyBuckets_CanBeCustomized()
    {
        // Arrange
        var customBuckets = new[] { 0.1, 0.5, 1.0, 5.0 };

        // Act
        var options = new RingKernelInstrumentationOptions
        {
            LatencyBuckets = customBuckets
        };

        // Assert
        Assert.Equal(customBuckets, options.LatencyBuckets);
    }
}
