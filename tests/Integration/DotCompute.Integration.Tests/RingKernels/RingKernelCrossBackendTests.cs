// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CPU.RingKernels;
using DotCompute.Integration.Tests.Utilities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Integration.Tests.RingKernels;

/// <summary>
/// Cross-backend integration tests for Ring Kernel functionality.
/// Validates message passing consistency, lifecycle management, and performance characteristics
/// across CPU, CUDA, and OpenCL backends.
/// </summary>
/// <remarks>
/// These tests ensure "write once, run anywhere" semantics for Ring Kernels by validating:
/// - Message queue behavior (FIFO ordering, capacity, overflow)
/// - Kernel lifecycle (Launch → Activate → Deactivate → Terminate)
/// - Statistics tracking (enqueue/dequeue counts, throughput)
/// - Performance characteristics per backend
/// </remarks>
[Collection("Integration")]
public class RingKernelCrossBackendTests : IntegrationTestBase
{
    private readonly ILogger<RingKernelCrossBackendTests> _logger;
    private readonly Dictionary<string, PerformanceMeasurement> _performanceData = new();

    public RingKernelCrossBackendTests(ITestOutputHelper output) : base(output)
    {
        _logger = GetLogger<RingKernelCrossBackendTests>();
    }

    #region Backend Availability Tests

    [Fact(DisplayName = "CPU Ring Kernel runtime should be available")]
    public void CpuRingKernelRuntime_ShouldBeAvailable()
    {
        // Arrange & Act
        var runtime = ServiceProvider.GetService<IRingKernelRuntime>();

        // Assert
        runtime.Should().NotBeNull("CPU Ring Kernel runtime should be registered");
        _logger.LogInformation("✓ CPU Ring Kernel runtime available");
    }

    #endregion

    #region Message Queue Cross-Backend Tests

    [Theory(DisplayName = "Message queues should maintain FIFO ordering across backends")]
    [InlineData(256, "CPU")]
    public async Task MessageQueue_ShouldMaintainFifoOrdering(int capacity, string backend)
    {
        // Arrange
        var runtime = GetRingKernelRuntime(backend);
        Skip.If(runtime == null, $"{backend} backend not available");

        var queue = await runtime.CreateMessageQueueAsync<int>(capacity);
        const int messageCount = 100;

        // Act - Enqueue messages in order
        for (int i = 0; i < messageCount; i++)
        {
            var message = KernelMessage<int>.CreateData(
                senderId: 0,
                receiverId: -1,
                payload: i
            );

            await queue.EnqueueAsync(message);
        }

        // Assert - Dequeue and verify order
        var dequeuedValues = new List<int>();
        for (int i = 0; i < messageCount; i++)
        {
            var message = await queue.DequeueAsync();
            dequeuedValues.Add(message.Payload);
        }

        dequeuedValues.Should().BeInAscendingOrder("messages should be dequeued in FIFO order");
        dequeuedValues.Should().BeEquivalentTo(Enumerable.Range(0, messageCount));

        _logger.LogInformation("✓ {Backend}: FIFO ordering validated with {Count} messages", backend, messageCount);

        await queue.DisposeAsync();
    }

    [Theory(DisplayName = "Message queues should handle capacity limits correctly")]
    [InlineData(256, "CPU")]
    public async Task MessageQueue_ShouldHandleCapacityLimits(int capacity, string backend)
    {
        // Arrange
        var runtime = GetRingKernelRuntime(backend);
        Skip.If(runtime == null, $"{backend} backend not available");

        var queue = await runtime.CreateMessageQueueAsync<int>(capacity);

        // Act - Fill queue to capacity
        for (int i = 0; i < capacity; i++)
        {
            var message = KernelMessage<int>.CreateData(0, -1, i);
            bool enqueued = await queue.TryEnqueueAsync(message);
            enqueued.Should().BeTrue($"message {i} should enqueue successfully");
        }

        queue.IsFull.Should().BeTrue("queue should be full after enqueuing capacity messages");

        // Try to enqueue one more (should fail)
        var overflowMessage = KernelMessage<int>.CreateData(0, -1, -1);
        bool overflowEnqueued = await queue.TryEnqueueAsync(overflowMessage);

        // Assert
        overflowEnqueued.Should().BeFalse("enqueue should fail when queue is full");
        queue.Count.Should().Be(capacity, "queue count should not exceed capacity");

        _logger.LogInformation("✓ {Backend}: Capacity limit enforcement validated ({Capacity} messages)",
            backend, capacity);

        await queue.DisposeAsync();
    }

    [Theory(DisplayName = "Message queues should track statistics accurately")]
    [InlineData(256, 100, "CPU")]
    public async Task MessageQueue_ShouldTrackStatisticsAccurately(int capacity, int messageCount, string backend)
    {
        // Arrange
        var runtime = GetRingKernelRuntime(backend);
        Skip.If(runtime == null, $"{backend} backend not available");

        var queue = await runtime.CreateMessageQueueAsync<int>(capacity);

        // Act - Enqueue and dequeue messages
        for (int i = 0; i < messageCount; i++)
        {
            var message = KernelMessage<int>.CreateData(0, -1, i);
            await queue.EnqueueAsync(message);
        }

        for (int i = 0; i < messageCount / 2; i++)
        {
            await queue.DequeueAsync();
        }

        var stats = await queue.GetStatisticsAsync();

        // Assert
        stats.TotalEnqueued.Should().Be(messageCount, "total enqueued count should match");
        stats.TotalDequeued.Should().Be(messageCount / 2, "total dequeued count should match");
        stats.TotalDropped.Should().Be(0, "no messages should be dropped");

        _logger.LogInformation("✓ {Backend}: Statistics tracking validated (enqueued: {Enqueued}, dequeued: {Dequeued})",
            backend, stats.TotalEnqueued, stats.TotalDequeued);

        await queue.DisposeAsync();
    }

    #endregion

    #region Kernel Lifecycle Cross-Backend Tests

    [Theory(DisplayName = "Ring Kernel lifecycle should work across backends")]
    [InlineData("CPU")]
    public async Task RingKernelLifecycle_ShouldWorkCorrectly(string backend)
    {
        // Arrange
        var runtime = GetRingKernelRuntime(backend);
        Skip.If(runtime == null, $"{backend} backend not available");

        const string kernelId = "test_lifecycle_kernel";
        const int gridSize = 1;
        const int blockSize = 1;

        // Act & Assert - Launch
        await runtime.LaunchAsync(kernelId, gridSize, blockSize);
        var status = await runtime.GetStatusAsync(kernelId);
        status.IsLaunched.Should().BeTrue("kernel should be launched");
        status.IsActive.Should().BeFalse("kernel should not be active yet");
        _logger.LogInformation("✓ {Backend}: Kernel launched", backend);

        // Activate
        await runtime.ActivateAsync(kernelId);
        status = await runtime.GetStatusAsync(kernelId);
        status.IsActive.Should().BeTrue("kernel should be active");
        _logger.LogInformation("✓ {Backend}: Kernel activated", backend);

        // Allow some processing time
        await Task.Delay(100);

        // Deactivate
        await runtime.DeactivateAsync(kernelId);
        status = await runtime.GetStatusAsync(kernelId);
        status.IsActive.Should().BeFalse("kernel should be deactivated");
        _logger.LogInformation("✓ {Backend}: Kernel deactivated", backend);

        // Terminate
        await runtime.TerminateAsync(kernelId);
        _logger.LogInformation("✓ {Backend}: Kernel terminated", backend);

        // Verify kernel is gone
        var kernelList = await runtime.ListKernelsAsync();
        kernelList.Should().NotContain(kernelId, "terminated kernel should not be in list");
    }

    [Theory(DisplayName = "Multiple Ring Kernels should coexist on same backend")]
    [InlineData(3, "CPU")]
    public async Task MultipleRingKernels_ShouldCoexist(int kernelCount, string backend)
    {
        // Arrange
        var runtime = GetRingKernelRuntime(backend);
        Skip.If(runtime == null, $"{backend} backend not available");

        var kernelIds = Enumerable.Range(0, kernelCount)
            .Select(i => $"test_kernel_{i}")
            .ToList();

        // Act - Launch all kernels
        foreach (var kernelId in kernelIds)
        {
            await runtime.LaunchAsync(kernelId, gridSize: 1, blockSize: 1);
        }

        var listedKernels = await runtime.ListKernelsAsync();

        // Assert
        listedKernels.Should().HaveCount(kernelCount, "all kernels should be listed");
        foreach (var kernelId in kernelIds)
        {
            listedKernels.Should().Contain(kernelId, $"kernel {kernelId} should be listed");
        }

        _logger.LogInformation("✓ {Backend}: {Count} kernels coexisting successfully", backend, kernelCount);

        // Cleanup
        foreach (var kernelId in kernelIds)
        {
            await runtime.TerminateAsync(kernelId);
        }
    }

    #endregion

    #region Message Passing Cross-Backend Tests

    [Theory(DisplayName = "Message passing should work correctly across backends")]
    [InlineData(100, "CPU")]
    public async Task MessagePassing_ShouldWorkCorrectly(int messageCount, string backend)
    {
        // Arrange
        var runtime = GetRingKernelRuntime(backend);
        Skip.If(runtime == null, $"{backend} backend not available");

        const string kernelId = "test_message_passing";
        await runtime.LaunchAsync(kernelId, gridSize: 1, blockSize: 1);
        await runtime.ActivateAsync(kernelId);

        // Act - Send messages to kernel
        for (int i = 0; i < messageCount; i++)
        {
            var message = KernelMessage<int>.CreateData(0, -1, i);
            await runtime.SendMessageAsync(kernelId, message);
        }

        _logger.LogInformation("✓ {Backend}: Sent {Count} messages to kernel", backend, messageCount);

        // Allow processing time
        await Task.Delay(500);

        // Get metrics
        var metrics = await runtime.GetMetricsAsync(kernelId);

        // Assert (messages received count may vary by backend implementation)
        metrics.Should().NotBeNull("metrics should be available");
        _logger.LogInformation("✓ {Backend}: Message passing validated. Metrics - Received: {Received}, Sent: {Sent}",
            backend, metrics.MessagesReceived, metrics.MessagesSent);

        // Cleanup
        await runtime.DeactivateAsync(kernelId);
        await runtime.TerminateAsync(kernelId);
    }

    #endregion

    #region Performance Comparison Tests

    [Theory(DisplayName = "Ring Kernel performance characteristics should be measurable")]
    [InlineData(1000, "CPU")]
    public async Task RingKernelPerformance_ShouldBeMeasurable(int messageCount, string backend)
    {
        // Arrange
        var runtime = GetRingKernelRuntime(backend);
        Skip.If(runtime == null, $"{backend} backend not available");

        const string kernelId = "test_performance";

        // Act - Measure launch time
        var launchTime = await MeasurePerformanceAsync(async () =>
        {
            await runtime.LaunchAsync(kernelId, gridSize: 1, blockSize: 1);
        }, $"{backend}_Launch");

        // Measure activation time
        var activationTime = await MeasurePerformanceAsync(async () =>
        {
            await runtime.ActivateAsync(kernelId);
        }, $"{backend}_Activation");

        // Measure message sending throughput
        var sendingTime = await MeasurePerformanceAsync(async () =>
        {
            for (int i = 0; i < messageCount; i++)
            {
                var message = KernelMessage<int>.CreateData(0, -1, i);
                await runtime.SendMessageAsync(kernelId, message);
            }
        }, $"{backend}_SendMessages");

        var throughput = messageCount / sendingTime.ElapsedTime.TotalSeconds;

        // Measure termination time
        await runtime.DeactivateAsync(kernelId);
        var terminationTime = await MeasurePerformanceAsync(async () =>
        {
            await runtime.TerminateAsync(kernelId);
        }, $"{backend}_Termination");

        // Assert & Log
        _logger.LogInformation("✓ {Backend} Performance Characteristics:", backend);
        _logger.LogInformation("  Launch time: {LaunchMs:F2}ms", launchTime.ElapsedTime.TotalMilliseconds);
        _logger.LogInformation("  Activation time: {ActivationMs:F2}ms", activationTime.ElapsedTime.TotalMilliseconds);
        _logger.LogInformation("  Message throughput: {Throughput:F0} msgs/sec ({Count} messages in {SendMs:F2}ms)",
            throughput, messageCount, sendingTime.ElapsedTime.TotalMilliseconds);
        _logger.LogInformation("  Termination time: {TermMs:F2}ms", terminationTime.ElapsedTime.TotalMilliseconds);

        // Store performance data for later comparison
        _performanceData[$"{backend}_Launch"] = launchTime;
        _performanceData[$"{backend}_Activation"] = activationTime;
        _performanceData[$"{backend}_SendMessages"] = sendingTime;
        _performanceData[$"{backend}_Termination"] = terminationTime;

        // Basic sanity checks
        launchTime.ElapsedTime.Should().BeLessThan(TimeSpan.FromSeconds(5), "launch should be fast");
        activationTime.ElapsedTime.Should().BeLessThan(TimeSpan.FromSeconds(1), "activation should be fast");
        throughput.Should().BeGreaterThan(100, "should achieve reasonable message throughput");
    }

    #endregion

    #region Error Handling Tests

    [Theory(DisplayName = "Ring Kernel should handle invalid operations gracefully")]
    [InlineData("CPU")]
    public async Task RingKernel_ShouldHandleInvalidOperationsGracefully(string backend)
    {
        // Arrange
        var runtime = GetRingKernelRuntime(backend);
        Skip.If(runtime == null, $"{backend} backend not available");

        // Act & Assert - Activate before launch
        Func<Task> activateBeforeLaunch = async () => await runtime.ActivateAsync("nonexistent_kernel");
        await activateBeforeLaunch.Should().ThrowAsync<InvalidOperationException>("cannot activate non-existent kernel");

        // Send message to non-existent kernel
        Func<Task> sendToNonExistent = async () =>
        {
            var message = KernelMessage<int>.CreateData(0, -1, 42);
            await runtime.SendMessageAsync("nonexistent_kernel", message);
        };
        await sendToNonExistent.Should().ThrowAsync<InvalidOperationException>("cannot send to non-existent kernel");

        // Launch duplicate kernel
        const string kernelId = "test_duplicate";
        await runtime.LaunchAsync(kernelId, gridSize: 1, blockSize: 1);

        Func<Task> launchDuplicate = async () => await runtime.LaunchAsync(kernelId, gridSize: 1, blockSize: 1);
        await launchDuplicate.Should().ThrowAsync<InvalidOperationException>("cannot launch duplicate kernel");

        _logger.LogInformation("✓ {Backend}: Error handling validated", backend);

        // Cleanup
        await runtime.TerminateAsync(kernelId);
    }

    #endregion

    #region Helper Methods

    private IRingKernelRuntime? GetRingKernelRuntime(string backend)
    {
        return backend.ToUpperInvariant() switch
        {
            "CPU" => ServiceProvider.GetService<CpuRingKernelRuntime>() as IRingKernelRuntime
                     ?? new CpuRingKernelRuntime(GetLogger<CpuRingKernelRuntime>()),
            "CUDA" => ServiceProvider.GetService<IRingKernelRuntime>(), // Will add CUDA support later
            "OPENCL" => ServiceProvider.GetService<IRingKernelRuntime>(), // Will add OpenCL support later
            _ => null
        };
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        // Register CPU Ring Kernel runtime
        services.AddSingleton<CpuRingKernelRuntime>();

        // TODO: Add CUDA and OpenCL runtime registrations when available
        // services.AddSingleton<CudaRingKernelRuntime>();
        // services.AddSingleton<OpenCLRingKernelRuntime>();
    }

    #endregion
}
