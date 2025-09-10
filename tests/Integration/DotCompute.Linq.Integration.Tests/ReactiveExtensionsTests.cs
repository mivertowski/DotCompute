using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Linq.Reactive;
using DotCompute.Memory;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Linq.Integration.Tests;

/// <summary>
/// Integration tests for reactive extensions and streaming compute operations.
/// Tests real-time processing, batching, windowing, and backpressure handling.
/// </summary>
public class ReactiveExtensionsTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IStreamingComputeProvider _streamingProvider;
    private readonly Mock<IAccelerator> _mockAccelerator;
    private readonly Mock<ICompiledKernel> _mockKernel;
    private readonly Subject<float[]> _dataStream;

    public ReactiveExtensionsTests()
    {
        var services = new ServiceCollection();
        
        // Setup mocks
        _mockAccelerator = new Mock<IAccelerator>();
        _mockKernel = new Mock<ICompiledKernel>();
        _dataStream = new Subject<float[]>();
        
        // Configure services
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<IStreamingComputeProvider, StreamingComputeProvider>();
        services.AddSingleton<IBatchProcessor, BatchProcessor>();
        services.AddSingleton<IBackpressureManager, BackpressureManager>();
        services.AddSingleton(_mockAccelerator.Object);
        
        _serviceProvider = services.BuildServiceProvider();
        _streamingProvider = _serviceProvider.GetRequiredService<IStreamingComputeProvider>();
        
        // Setup mock behavior
        _mockKernel.Setup(x => x.ExecuteAsync(It.IsAny<object[]>()))
            .ReturnsAsync(new KernelExecutionResult 
            { 
                Success = true,
                Result = new float[] { 1.0f, 2.0f, 3.0f }
            });
            
        _mockAccelerator.Setup(x => x.CompileKernel(It.IsAny<KernelDefinition>(), It.IsAny<CompilationOptions>()))
            .ReturnsAsync(_mockKernel.Object);
    }

    [Fact]
    public async Task StreamingSelect_ShouldProcessDataInRealTime()
    {
        // Arrange
        var results = new List<float[]>();
        var completion = new TaskCompletionSource<bool>();
        
        var subscription = _dataStream
            .SelectCompute(x => x.Select(v => v * 2.0f).ToArray())
            .Subscribe(
                result => results.Add(result),
                ex => completion.SetException(ex),
                () => completion.SetResult(true));

        // Act
        _dataStream.OnNext(new float[] { 1.0f, 2.0f, 3.0f });
        _dataStream.OnNext(new float[] { 4.0f, 5.0f, 6.0f });
        _dataStream.OnCompleted();

        await completion.Task.ConfigureAwait(false);

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().BeEquivalentTo(new float[] { 2.0f, 4.0f, 6.0f });
        results[1].Should().BeEquivalentTo(new float[] { 8.0f, 10.0f, 12.0f });
        
        subscription.Dispose();
    }

    [Fact]
    public async Task StreamingBatch_ShouldGroupDataCorrectly()
    {
        // Arrange
        var batches = new List<IList<float[]>>();
        var completion = new TaskCompletionSource<bool>();
        
        var subscription = _dataStream
            .Buffer(3) // Batch every 3 items
            .SelectCompute(batch => batch.SelectMany(x => x).ToArray())
            .Subscribe(
                result => batches.Add((IList<float[]>)result),
                ex => completion.SetException(ex),
                () => completion.SetResult(true));

        // Act
        for (int i = 0; i < 5; i++)
        {
            _dataStream.OnNext(new float[] { i, i + 0.5f });
        }
        _dataStream.OnCompleted();

        await completion.Task.ConfigureAwait(false);

        // Assert
        batches.Should().HaveCount(2); // 3 items + 2 items
        
        subscription.Dispose();
    }

    [Fact]
    public async Task StreamingWindow_ShouldProcessSlidingWindows()
    {
        // Arrange
        var windows = new List<float[]>();
        var completion = new TaskCompletionSource<bool>();
        
        var subscription = _dataStream
            .Window(TimeSpan.FromMilliseconds(100))
            .SelectMany(window => window.ToArray())
            .SelectCompute(batch => batch.SelectMany(x => x).Average())
            .Subscribe(
                avg => windows.Add(new float[] { avg }),
                ex => completion.SetException(ex),
                () => completion.SetResult(true));

        // Act
        _dataStream.OnNext(new float[] { 1.0f, 2.0f });
        await Task.Delay(50);
        _dataStream.OnNext(new float[] { 3.0f, 4.0f });
        await Task.Delay(100);
        _dataStream.OnNext(new float[] { 5.0f, 6.0f });
        _dataStream.OnCompleted();

        await completion.Task.ConfigureAwait(false);

        // Assert
        windows.Should().NotBeEmpty();
        
        subscription.Dispose();
    }

    [Fact]
    public async Task BackpressureHandling_ShouldThrottleWhenOverloaded()
    {
        // Arrange
        var processedCount = 0;
        var droppedCount = 0;
        var completion = new TaskCompletionSource<bool>();
        
        var subscription = _dataStream
            .OnBackpressureDrop(dropped => Interlocked.Increment(ref droppedCount))
            .SelectCompute(x => 
            {
                Thread.Sleep(100); // Simulate slow processing
                return x.Select(v => v * 2.0f).ToArray();
            })
            .Subscribe(
                result => Interlocked.Increment(ref processedCount),
                ex => completion.SetException(ex),
                () => completion.SetResult(true));

        // Act - Send data faster than it can be processed
        var sendTask = Task.Run(async () =>
        {
            for (int i = 0; i < 20; i++)
            {
                _dataStream.OnNext(new float[] { i });
                await Task.Delay(10); // Send every 10ms, process every 100ms
            }
            _dataStream.OnCompleted();
        });

        await sendTask;
        await completion.Task.ConfigureAwait(false);

        // Assert
        processedCount.Should().BeLessThan(20);
        droppedCount.Should().BeGreaterThan(0);
        
        subscription.Dispose();
    }

    [Fact]
    public async Task StreamingWithErrorHandling_ShouldRecoverFromErrors()
    {
        // Arrange
        var results = new List<float[]>();
        var errors = new List<Exception>();
        var completion = new TaskCompletionSource<bool>();
        
        var subscription = _dataStream
            .SelectCompute(x =>
            {
                if (x[0] < 0) throw new InvalidOperationException("Negative value");
                return x.Select(v => v * 2.0f).ToArray();
            })
            .Retry(3)
            .Subscribe(
                result => results.Add(result),
                ex => errors.Add(ex),
                () => completion.SetResult(true));

        // Act
        _dataStream.OnNext(new float[] { 1.0f, 2.0f });
        _dataStream.OnNext(new float[] { -1.0f, 2.0f }); // This will cause error
        _dataStream.OnNext(new float[] { 3.0f, 4.0f });
        _dataStream.OnCompleted();

        await completion.Task.ConfigureAwait(false);

        // Assert
        results.Should().HaveCount(2); // Valid results
        results[0].Should().BeEquivalentTo(new float[] { 2.0f, 4.0f });
        results[1].Should().BeEquivalentTo(new float[] { 6.0f, 8.0f });
        
        subscription.Dispose();
    }

    [Fact]
    public async Task ParallelStreamProcessing_ShouldHandleConcurrentStreams()
    {
        // Arrange
        var stream1 = new Subject<float[]>();
        var stream2 = new Subject<float[]>();
        var results = new List<float[]>();
        var completion = new TaskCompletionSource<bool>();
        
        var subscription = Observable.Merge(stream1, stream2)
            .SelectCompute(x => x.Select(v => v * 2.0f).ToArray())
            .Subscribe(
                result => results.Add(result),
                ex => completion.SetException(ex),
                () => completion.SetResult(true));

        // Act
        var task1 = Task.Run(() =>
        {
            for (int i = 0; i < 5; i++)
            {
                stream1.OnNext(new float[] { i });
                Thread.Sleep(10);
            }
            stream1.OnCompleted();
        });

        var task2 = Task.Run(() =>
        {
            for (int i = 10; i < 15; i++)
            {
                stream2.OnNext(new float[] { i });
                Thread.Sleep(15);
            }
            stream2.OnCompleted();
        });

        await Task.WhenAll(task1, task2);
        await completion.Task.ConfigureAwait(false);

        // Assert
        results.Should().HaveCount(10);
        results.Should().OnlyContain(r => r.Length == 1);
        
        subscription.Dispose();
    }

    [Fact]
    public async Task StreamingAggregation_ShouldMaintainRunningState()
    {
        // Arrange
        var aggregates = new List<float>();
        var completion = new TaskCompletionSource<bool>();
        
        var subscription = _dataStream
            .Scan(0.0f, (acc, batch) => acc + batch.Sum())
            .Subscribe(
                result => aggregates.Add(result),
                ex => completion.SetException(ex),
                () => completion.SetResult(true));

        // Act
        _dataStream.OnNext(new float[] { 1.0f, 2.0f }); // Sum = 3, Running = 3
        _dataStream.OnNext(new float[] { 3.0f, 4.0f }); // Sum = 7, Running = 10
        _dataStream.OnNext(new float[] { 5.0f });       // Sum = 5, Running = 15
        _dataStream.OnCompleted();

        await completion.Task.ConfigureAwait(false);

        // Assert
        aggregates.Should().HaveCount(3);
        aggregates[0].Should().Be(3.0f);
        aggregates[1].Should().Be(10.0f);
        aggregates[2].Should().Be(15.0f);
        
        subscription.Dispose();
    }

    [Fact]
    public async Task StreamingWithCustomScheduler_ShouldRespectScheduling()
    {
        // Arrange
        var threadIds = new List<int>();
        var completion = new TaskCompletionSource<bool>();
        
        var subscription = _dataStream
            .ObserveOn(TaskPoolScheduler.Default)
            .SelectCompute(x =>
            {
                threadIds.Add(Thread.CurrentThread.ManagedThreadId);
                return x.Select(v => v * 2.0f).ToArray();
            })
            .Subscribe(
                result => { },
                ex => completion.SetException(ex),
                () => completion.SetResult(true));

        // Act
        for (int i = 0; i < 5; i++)
        {
            _dataStream.OnNext(new float[] { i });
        }
        _dataStream.OnCompleted();

        await completion.Task.ConfigureAwait(false);

        // Assert
        threadIds.Should().NotBeEmpty();
        threadIds.Should().OnlyContain(id => id != Thread.CurrentThread.ManagedThreadId);
        
        subscription.Dispose();
    }

    [Fact]
    public async Task StreamingMemoryManagement_ShouldReuseBuffers()
    {
        // Arrange
        var memoryUsage = new List<long>();
        var completion = new TaskCompletionSource<bool>();
        
        var subscription = _dataStream
            .SelectCompute(x =>
            {
                memoryUsage.Add(GC.GetTotalMemory(false));
                return x.Select(v => v * 2.0f).ToArray();
            })
            .Subscribe(
                result => { },
                ex => completion.SetException(ex),
                () => completion.SetResult(true));

        // Act
        for (int i = 0; i < 100; i++)
        {
            _dataStream.OnNext(new float[1000]); // Large arrays
            if (i % 10 == 0)
            {
                GC.Collect();
                await Task.Delay(1); // Allow cleanup
            }
        }
        _dataStream.OnCompleted();

        await completion.Task.ConfigureAwait(false);

        // Assert
        memoryUsage.Should().NotBeEmpty();
        
        // Memory should not grow linearly (indicates buffer reuse)
        var firstHalf = memoryUsage.Take(memoryUsage.Count / 2).Average();
        var secondHalf = memoryUsage.Skip(memoryUsage.Count / 2).Average();
        var growthRatio = secondHalf / firstHalf;
        growthRatio.Should().BeLessThan(2.0); // Less than 2x growth indicates efficient memory usage
        
        subscription.Dispose();
    }

    [Fact]
    public async Task StreamingPerformanceMetrics_ShouldTrackThroughput()
    {
        // Arrange
        var metrics = new List<StreamingMetrics>();
        var completion = new TaskCompletionSource<bool>();
        
        var subscription = _dataStream
            .SelectCompute(x => x.Select(v => v * 2.0f).ToArray())
            .WithPerformanceTracking()
            .Subscribe(
                result => metrics.Add(result.Metrics),
                ex => completion.SetException(ex),
                () => completion.SetResult(true));

        // Act
        var start = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            _dataStream.OnNext(new float[] { i });
            await Task.Delay(1);
        }
        _dataStream.OnCompleted();

        await completion.Task.ConfigureAwait(false);

        // Assert
        metrics.Should().NotBeEmpty();
        metrics.Should().OnlyContain(m => m.ThroughputPerSecond > 0);
        metrics.Should().OnlyContain(m => m.ProcessingLatency > TimeSpan.Zero);
        
        var totalThroughput = metrics.Sum(m => m.ThroughputPerSecond);
        totalThroughput.Should().BeGreaterThan(0);
        
        subscription.Dispose();
    }

    [Fact]
    public async Task StreamingWithGpuAcceleration_ShouldUseCudaKernels()
    {
        // Arrange
        var results = new List<float[]>();
        var completion = new TaskCompletionSource<bool>();
        
        var subscription = _dataStream
            .SelectCompute(x => x.Select(v => v * v).ToArray(), 
                options => options.TargetBackend = ComputeBackend.GPU)
            .Subscribe(
                result => results.Add(result),
                ex => completion.SetException(ex),
                () => completion.SetResult(true));

        // Act
        _dataStream.OnNext(new float[] { 1.0f, 2.0f, 3.0f });
        _dataStream.OnNext(new float[] { 4.0f, 5.0f, 6.0f });
        _dataStream.OnCompleted();

        await completion.Task.ConfigureAwait(false);

        // Assert
        results.Should().HaveCount(2);
        
        // Verify GPU kernel was used
        _mockAccelerator.Verify(x => x.CompileKernel(
            It.Is<KernelDefinition>(k => k.TargetBackend == ComputeBackend.GPU),
            It.IsAny<CompilationOptions>()), Times.AtLeastOnce);
        
        subscription.Dispose();
    }

    [Fact]
    public async Task StreamingCancellation_ShouldStopProcessingGracefully()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var processedCount = 0;
        
        var subscription = _dataStream
            .TakeUntil(Observable.Timer(TimeSpan.FromMilliseconds(100)))
            .SelectCompute(x =>
            {
                Interlocked.Increment(ref processedCount);
                return x.Select(v => v * 2.0f).ToArray();
            })
            .Subscribe();

        // Act
        var sendTask = Task.Run(async () =>
        {
            for (int i = 0; i < 1000; i++)
            {
                _dataStream.OnNext(new float[] { i });
                await Task.Delay(1);
            }
        });

        await Task.Delay(150); // Wait for timeout
        
        // Assert
        processedCount.Should().BeLessThan(1000);
        processedCount.Should().BeGreaterThan(0);
        
        subscription.Dispose();
    }

    public void Dispose()
    {
        _dataStream?.Dispose();
        _serviceProvider?.Dispose();
    }
}