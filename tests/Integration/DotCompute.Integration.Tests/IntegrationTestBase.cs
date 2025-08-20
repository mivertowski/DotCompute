// Copyright(c) 2025 Michael Ivertowski

#pragma warning disable CA1848 // Use LoggerMessage delegates - will be migrated in future iteration
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#pragma warning disable CA1851 // Multiple enumeration - acceptable for tests

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using DotCompute.Plugins.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using MemoryOptions = DotCompute.Abstractions.MemoryOptions;
using DotCompute.Core.Pipelines;
using DotCompute.Tests.Integration;
using DotCompute.Tests.Common;

namespace DotCompute.Integration.Tests;


/// <summary>
/// Base class for integration tests providing common infrastructure and utilities.
/// </summary>
public abstract class IntegrationTestBase(ITestOutputHelper output) : IAsyncLifetime
{
    protected ITestOutputHelper TestOutput { get; } = output;
    protected ILogger Logger { get; } = new TestOutputLogger(output);
    protected IServiceProvider ServiceProvider { get; private set; } = default!;
    private IHost? _host;

    public virtual async Task InitializeAsync()
    {
        _host = CreateHost();
        await _host.StartAsync();
        ServiceProvider = _host.Services;

        // Register accelerator providers and initialize the accelerator manager
        var acceleratorManager = ServiceProvider.GetRequiredService<IAcceleratorManager>();
        var cpuProvider = ServiceProvider.GetRequiredService<IAcceleratorProvider>();
        acceleratorManager.RegisterProvider(cpuProvider);
        await acceleratorManager.InitializeAsync();

        // Initialize the plugin system
        var pluginSystem = ServiceProvider.GetRequiredService<PluginSystem>();
        await pluginSystem.InitializeAsync();
    }

    public virtual async Task DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    private IHost CreateHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Configure logging
                _ = services.AddLogging(builder =>
                {
                    _ = builder.AddProvider(new TestOutputLoggerProvider(TestOutput));
                    _ = builder.SetMinimumLevel(LogLevel.Information);
                });

                // Add core DotCompute services  
                _ = services.AddSingleton<IAcceleratorProvider, HighPerformanceCpuAcceleratorProvider>();
                _ = services.AddSingleton<IAcceleratorManager, DefaultAcceleratorManager>();
                _ = services.AddSingleton<PluginSystem>();
                _ = services.AddSingleton<IComputeEngine, DefaultComputeEngine>();

                // Skip pipeline integration for now - these are complex to mock
                // TODO: Add proper pipeline integration when interfaces are stabilized

                // Add a simple memory manager factory
                _ = services.AddSingleton<IMemoryManager>(sp =>
                {
                    var acceleratorManager = sp.GetRequiredService<IAcceleratorManager>();
                    var logger = sp.GetRequiredService<ILogger<IMemoryManager>>();
                    // Use a dummy accelerator for now - in real usage this would be properly initialized
                    // Use a simple memory manager for testing
                    return new SimpleMemoryManager(logger);
                });

                // Add test utilities - commented out non-existent classes
                // TestDataGenerator is now static - no need for DI registration
                // services.AddSingleton<MemoryTestUtilities>();

                // Add pipeline services - commented out non-existent classes
                // services.AddTransient<KernelPipelineBuilder>();
                // services.AddTransient<IPipelineOptimizer, PipelineOptimizer>();

                // Add performance monitor - commented out non-existent classes
                // services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
            })
            .UseConsoleLifetime()
            .Build();
    }

    // Helper methods for integration tests

    /// <summary>
    /// Creates an input buffer from data array.
    /// </summary>
    protected async Task<IMemoryBuffer> CreateInputBuffer<T>(IMemoryManager memoryManager, T[] data) where T : unmanaged
    {
        try
        {
            var elementSize = System.Runtime.InteropServices.Marshal.SizeOf<T>();
            var sizeInBytes = data.Length * elementSize;

            LoggerMessages.CreateInputBufferAllocating(Logger, sizeInBytes, data.Length, typeof(T).Name);

            if (sizeInBytes <= 0 || data.Length <= 0)
            {
                throw new ArgumentException($"Invalid data size: {data.Length} elements, {sizeInBytes} bytes");
            }

            var buffer = await memoryManager.AllocateAsync(sizeInBytes);
            LoggerMessages.BufferAllocatedSuccessfully(Logger);

            // Convert to bytes more safely
            var byteData = System.Runtime.InteropServices.MemoryMarshal.Cast<T, byte>(data).ToArray();
            LoggerMessages.DataConvertedToBytes(Logger, byteData.Length);

            await buffer.CopyFromHostAsync<byte>(byteData.AsMemory());
            LoggerMessages.DataCopiedToBufferSuccessfully(Logger);

            return buffer;
        }
        catch (Exception ex)
        {
            LoggerMessages.FailedToCreateInputBuffer(Logger, ex, data?.Length ?? 0, typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// Creates an output buffer of specified size.
    /// </summary>
    protected static async Task<IMemoryBuffer> CreateOutputBuffer<T>(IMemoryManager memoryManager, int size) where T : unmanaged
    {
        var sizeInBytes = size * System.Runtime.InteropServices.Marshal.SizeOf<T>();
        return await memoryManager.AllocateAsync(sizeInBytes);
    }

    /// <summary>
    /// Reads data from a buffer.
    /// </summary>
    protected static async Task<T[]> ReadBufferAsync<T>(IMemoryBuffer buffer) where T : unmanaged
    {
        var elementSize = System.Runtime.InteropServices.Marshal.SizeOf<T>();
        var elementCount = (int)(buffer.SizeInBytes / elementSize);

        // Validate that the buffer size is reasonable and aligned
        if (buffer.SizeInBytes is <= 0 or > int.MaxValue)
        {
            throw new ArgumentException($"Invalid buffer size: {buffer.SizeInBytes}");
        }

        if (buffer.SizeInBytes % elementSize != 0)
        {
            throw new ArgumentException($"Buffer size {buffer.SizeInBytes} is not aligned to element size {elementSize}");
        }

        // For very large buffers, use chunked reading to avoid large memory allocations
        const int maxChunkSize = 16 * 1024 * 1024; // 16MB chunks
        if (buffer.SizeInBytes > maxChunkSize)
        {
            return await ReadLargeBufferInChunksAsync<T>(buffer, elementCount, elementSize);
        }

        var byteData = new byte[buffer.SizeInBytes];
        await buffer.CopyToHostAsync(byteData.AsMemory());
        return System.Runtime.InteropServices.MemoryMarshal.Cast<byte, T>(byteData).ToArray();
    }

    /// <summary>
    /// Reads a large buffer in chunks to avoid excessive memory allocation.
    /// </summary>
    private static async Task<T[]> ReadLargeBufferInChunksAsync<T>(IMemoryBuffer buffer, int elementCount, int elementSize) where T : unmanaged
    {
        var result = new T[elementCount];
        const int maxChunkSize = 16 * 1024 * 1024; // 16MB chunks
        var elementsPerChunk = maxChunkSize / elementSize;

        for (var offset = 0; offset < elementCount; offset += elementsPerChunk)
        {
            var remainingElements = Math.Min(elementsPerChunk, elementCount - offset);
            var chunkSizeInBytes = remainingElements * elementSize;
            var byteOffset = offset * elementSize;

            var chunkBytes = new byte[chunkSizeInBytes];
            await buffer.CopyToHostAsync(chunkBytes.AsMemory(), byteOffset);

            var chunkData = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, T>(chunkBytes);
            chunkData.CopyTo(result.AsSpan(offset, remainingElements));
        }

        return result;
    }

    /// <summary>
    /// Reads data from a typed buffer. Since IBuffer<T> inherits from IMemoryBuffer, 
    /// we can use the same implementation but need an overload for type safety.
    /// </summary>
    protected static async Task<T[]> ReadBufferAsync<T>(IBuffer<T> typedBuffer) where T : unmanaged
        // Since IBuffer<T> inherits from IMemoryBuffer, we can safely cast and use the base implementation
        => await ReadBufferAsync<T>((IMemoryBuffer)typedBuffer);

    /// <summary>
    /// Creates a mock pipeline stage for testing.
    /// </summary>
    protected static IPipelineStage CreateMockPipelineStage(string id, string name, TimeSpan? executionTime = null) => new MockPipelineStage(id, name, executionTime ?? TimeSpan.FromMilliseconds(50));

    /// <summary>
    /// Waits for a condition to be true with timeout.
    /// </summary>
    protected static async Task<bool> WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan timeout,
        TimeSpan pollInterval = default)
    {
        if (pollInterval == default)
            pollInterval = TimeSpan.FromMilliseconds(100);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            if (condition())
                return true;

            await Task.Delay(pollInterval);
        }

        return false;
    }

    /// <summary>
    /// Measures the execution time of an async operation.
    /// </summary>
    protected static async Task<(T Result, TimeSpan Duration)> MeasureAsync<T>(Func<Task<T>> operation)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await operation();
        stopwatch.Stop();
        return (result, stopwatch.Elapsed);
    }

    /// <summary>
    /// Measures the execution time of an async operation without return value.
    /// </summary>
    protected static async Task<TimeSpan> MeasureAsync(Func<Task> operation)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await operation();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    /// <summary>
    /// Retries an operation with exponential backoff.
    /// </summary>
    protected static async Task<T> RetryAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        TimeSpan initialDelay = default)
    {
        if (initialDelay == default)
            initialDelay = TimeSpan.FromMilliseconds(100);

        Exception? lastException = null;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt == maxAttempts - 1)
                    throw;

                var delay = TimeSpan.FromMilliseconds(initialDelay.TotalMilliseconds * Math.Pow(2, attempt));
                await Task.Delay(delay);
            }
        }

        throw lastException!;
    }

    /// <summary>
    /// Asserts that an operation completes within a specified timeout.
    /// </summary>
    protected static async Task AssertCompletesWithinAsync(Func<Task> operation, TimeSpan timeout, string? message = null)
    {
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            await operation();
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            throw new TimeoutException(message ?? $"Operation did not complete within {timeout}");
        }
    }

    /// <summary>
    /// Asserts that an operation throws a specific exception type.
    /// </summary>
    protected static async Task AssertThrowsAsync<TException>(Func<Task> operation, string? message = null)
        where TException : Exception
    {
        try
        {
            await operation();
            throw new InvalidOperationException(message ?? $"Expected {typeof(TException).Name} but no exception was thrown");
        }
        catch (TException)
        {
            // Expected exception - test passes
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                message ?? $"Expected {typeof(TException).Name} but got {ex.GetType().Name}: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Creates test data using static test data generator.
    /// </summary>
    protected static T[] GenerateTestData<T>(int size, int seed = 42) where T : unmanaged
    {
        return typeof(T) switch
        {
            Type t when t == typeof(float) => [.. TestDataGenerator.GenerateFloatArray(size).Cast<T>()],
            Type t when t == typeof(int) => [.. TestDataGenerator.GenerateIntArray(size).Cast<T>()],
            Type t when t == typeof(double) => [.. TestDataGenerator.GenerateDoubleArray(size).Cast<T>()],
            _ => throw new NotSupportedException($"Test data generation not supported for type {typeof(T)}")
        };
    }

    /// <summary>
    /// Gets memory statistics for monitoring memory usage during tests.
    /// </summary>
    protected static Task<MemoryStatistics> GetMemoryStatisticsAsync()
    {
        // Return mock statistics for now
        return Task.FromResult(new MemoryStatistics
        {
            TotalMemory = 1024 * 1024 * 1024, // 1GB
            UsedMemory = 0,
            FreeMemory = 1024 * 1024 * 1024,
            AllocatedMemory = 0,
            AllocationCount = 0,
            PeakMemory = 0
        });
    }

    /// <summary>
    /// Logs performance metrics for analysis.
    /// </summary>
    protected void LogPerformanceMetrics(string operation, TimeSpan duration, int itemsProcessed = 0)
    {
        var throughput = itemsProcessed > 0 ? itemsProcessed / duration.TotalSeconds : 0;

        if (itemsProcessed > 0)
        {
            Logger.LogInformation("Performance: {Operation} took {Duration:F2}ms, Throughput: {Throughput:N0} items/sec",
                operation, duration.TotalMilliseconds, throughput);
        }
        else
        {
            Logger.LogInformation("Performance: {Operation} took {Duration:F2}ms",
                operation, duration.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Creates a test execution context for pipeline tests.
    /// </summary>
    protected PipelineExecutionContext CreateTestExecutionContext(Dictionary<string, object>? inputs = null)
    {
        return new PipelineExecutionContext
        {
            Inputs = inputs ?? [],
            Device = (Core.IComputeDevice)ServiceProvider.GetRequiredService<IAcceleratorManager>().Default,
            MemoryManager = (IPipelineMemoryManager)ServiceProvider.GetRequiredService<IMemoryManager>(),
            Options = new PipelineExecutionOptions
            {
                ContinueOnError = false,
                MaxParallelStages = Environment.ProcessorCount
            }
        };
    }

    /// <summary>
    /// Validates that compute results are within acceptable tolerance.
    /// </summary>
    protected static bool ValidateResults(float[] expected, float[] actual, float tolerance = 0.001f)
    {
        if (expected.Length != actual.Length)
            return false;

        for (var i = 0; i < expected.Length; i++)
        {
            if (Math.Abs(expected[i] - actual[i]) > tolerance)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that the system is in a clean state after test execution.
    /// </summary>
    protected async Task ValidateSystemStateAsync()
    {
        var memoryStats = await GetMemoryStatisticsAsync();

        // Log memory statistics for monitoring
        Logger.LogInformation("Memory Statistics - Allocated: {Allocated}, Peak: {Peak}, Used: {Used}",
            memoryStats.AllocatedMemory,
            memoryStats.PeakMemory,
            memoryStats.UsedMemory);

        // In a real implementation, we would validate for memory leaks
        // For now, we just log the statistics
    }
}

/// <summary>
/// Mock pipeline stage for testing purposes.
/// </summary>
public sealed class MockPipelineStage : IPipelineStage
{
    private readonly TimeSpan _executionTime;
    private readonly MockStageMetrics _metrics;

    public MockPipelineStage(string id, string name, TimeSpan? executionTime = null)
    {
        Id = id;
        Name = name;
        _executionTime = executionTime ?? TimeSpan.FromMilliseconds(10);
        _metrics = new MockStageMetrics(_executionTime);
        Metadata = new Dictionary<string, object>
        {
            ["Type"] = "Mock",
            ["Category"] = "Test",
            ["Version"] = "1.0"
        };
    }

    public string Id { get; }
    public string Name { get; }
    public PipelineStageType Type => PipelineStageType.Sequential;
    public IReadOnlyList<string> Dependencies { get; } = [];
    public IReadOnlyDictionary<string, object> Metadata { get; }

    public async ValueTask<StageExecutionResult> ExecuteAsync(
        PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        // Simulate work
        await Task.Delay(_executionTime, cancellationToken);

        // Update metrics
        _metrics.RecordExecution(_executionTime);

        // Mock processing - just pass through inputs as outputs
        var outputs = new Dictionary<string, object>(context.Inputs);

        return new StageExecutionResult
        {
            StageId = Id,
            Success = true,
            Duration = DateTime.UtcNow - startTime,
            Outputs = outputs,
            MemoryUsage = new MemoryUsageStats
            {
                AllocatedBytes = 1024,
                PeakBytes = 1024,
                AllocationCount = 1,
                DeallocationCount = 1
            }
        };
    }

    public StageValidationResult Validate() => new() { IsValid = true };

    public IStageMetrics GetMetrics() => _metrics;
}

/// <summary>
/// Mock implementation of IStageMetrics for testing.
/// </summary>
public sealed class MockStageMetrics : IStageMetrics
{
    private readonly TimeSpan _defaultExecutionTime;
    private long _executionCount;
    private TimeSpan _totalExecutionTime;
    private TimeSpan _minExecutionTime;
    private TimeSpan _maxExecutionTime;
    private long _errorCount;

    public MockStageMetrics(TimeSpan defaultExecutionTime)
    {
        _defaultExecutionTime = defaultExecutionTime;
        _minExecutionTime = defaultExecutionTime;
        _maxExecutionTime = defaultExecutionTime;
        CustomMetrics = new Dictionary<string, double>
        {
            ["ThroughputOpsPerSecond"] = 100.0,
            ["CacheHitRate"] = 0.95,
            ["ResourceUtilization"] = 0.75
        };
    }

    public long ExecutionCount => _executionCount;

    public TimeSpan AverageExecutionTime
        => _executionCount > 0
            ? TimeSpan.FromTicks(_totalExecutionTime.Ticks / _executionCount)
            : _defaultExecutionTime;

    public TimeSpan MinExecutionTime => _minExecutionTime;
    public TimeSpan MaxExecutionTime => _maxExecutionTime;
    public TimeSpan TotalExecutionTime => _totalExecutionTime;
    public long ErrorCount => _errorCount;

    public double SuccessRate
        => _executionCount > 0
            ? (double)(_executionCount - _errorCount) / _executionCount
            : 1.0;

    public long AverageMemoryUsage => 1024; // Mock value
    public IReadOnlyDictionary<string, double> CustomMetrics { get; }

    public void RecordExecution(TimeSpan executionTime, bool success = true)
    {
        _executionCount++;
        _totalExecutionTime = _totalExecutionTime.Add(executionTime);

        if (executionTime < _minExecutionTime)
            _minExecutionTime = executionTime;
        if (executionTime > _maxExecutionTime)
            _maxExecutionTime = executionTime;

        if (!success)
            _errorCount++;
    }
}

/// <summary>
/// Test output logger for integration with xUnit test output.
/// </summary>
public sealed class TestOutputLogger(ITestOutputHelper output) : ILogger
{
    private readonly ITestOutputHelper _output = output;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] [{logLevel}] {message}";

        if (exception != null)
        {
            logEntry += Environment.NewLine + exception;
        }

        try
        {
            _output.WriteLine(logEntry);
        }
        catch
        {
            // Ignore output errors - test may have completed
        }
    }
}

/// <summary>
/// Test output logger provider for dependency injection.
/// </summary>
public sealed class TestOutputLoggerProvider(ITestOutputHelper output) : ILoggerProvider
{
    private readonly ITestOutputHelper _output = output;

    public ILogger CreateLogger(string categoryName) => new TestOutputLogger(_output);

    public void Dispose() => GC.SuppressFinalize(this);// Nothing to dispose
}

/// <summary>
/// Simple memory manager for testing purposes with thread-safe tracking.
/// </summary>
internal sealed class SimpleMemoryManager(ILogger<IMemoryManager> logger) : IMemoryManager
{
    private readonly ConcurrentBag<WeakReference<SimpleMemoryBuffer>> _allocatedBuffers = [];
    private long _totalAllocated;

    public ValueTask<IMemoryBuffer> AllocateAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
#pragma warning disable CA2000 // Dispose objects before losing scope - buffer tracked in _allocatedBuffers
        var buffer = new SimpleMemoryBuffer(sizeInBytes, options);
#pragma warning restore CA2000
        _allocatedBuffers.Add(new WeakReference<SimpleMemoryBuffer>(buffer));
        _ = Interlocked.Add(ref _totalAllocated, sizeInBytes);
        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    public ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var sizeInBytes = source.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>();
#pragma warning disable CA2000 // Dispose objects before losing scope - buffer tracked in _allocatedBuffers
        var buffer = new SimpleMemoryBuffer(sizeInBytes, options);
#pragma warning restore CA2000
        var sourceBytes = System.Runtime.InteropServices.MemoryMarshal.Cast<T, byte>(source.Span);
        sourceBytes.CopyTo(buffer.Data.AsSpan());
        _allocatedBuffers.Add(new WeakReference<SimpleMemoryBuffer>(buffer));
        _ = Interlocked.Add(ref _totalAllocated, sizeInBytes);
        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    /// <summary>
    /// Gets the total allocated bytes across all buffers(for debugging).
    /// </summary>
    public long TotalAllocated => Interlocked.Read(ref _totalAllocated);

    /// <summary>
    /// Gets the count of active buffers(for debugging).
    /// </summary>
    public int ActiveBufferCount
    {
        get
        {
            var count = 0;
            foreach (var weakRef in _allocatedBuffers)
            {
                if (weakRef.TryGetTarget(out _))
                    count++;
            }
            return count;
        }
    }

    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        if (buffer is SimpleMemoryBuffer simpleBuffer)
        {
            return new SimpleMemoryBuffer(simpleBuffer.Data.AsMemory().Slice((int)offset, (int)length), buffer.Options);
        }
        throw new NotSupportedException("View creation only supported for SimpleMemoryBuffer");
    }

    // Implement missing IMemoryManager interface members
    public ValueTask<IMemoryBuffer> Allocate<T>(int count) where T : unmanaged
    {
        var sizeInBytes = count * System.Runtime.InteropServices.Marshal.SizeOf<T>();
        return AllocateAsync(sizeInBytes);
    }

    public void CopyToDevice<T>(IMemoryBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        if (buffer is SimpleMemoryBuffer simpleBuffer)
        {
            var sourceBytes = System.Runtime.InteropServices.MemoryMarshal.Cast<T, byte>(data);
            sourceBytes.CopyTo(simpleBuffer.Data.AsSpan());
        }
        else
        {
            throw new NotSupportedException("CopyToDevice only supported for SimpleMemoryBuffer");
        }
    }

    public void CopyFromDevice<T>(Span<T> data, IMemoryBuffer buffer) where T : unmanaged
    {
        if (buffer is SimpleMemoryBuffer simpleBuffer)
        {
            var bufferBytes = simpleBuffer.Data.AsSpan();
            var destBytes = System.Runtime.InteropServices.MemoryMarshal.Cast<T, byte>(data);
            var bytesToCopy = Math.Min(bufferBytes.Length, destBytes.Length);
            bufferBytes[..bytesToCopy].CopyTo(destBytes);
        }
        else
        {
            throw new NotSupportedException("CopyFromDevice only supported for SimpleMemoryBuffer");
        }
    }

    public void Free(IMemoryBuffer buffer)
    {
        if (buffer is SimpleMemoryBuffer simpleBuffer)
        {
            _ = Interlocked.Add(ref _totalAllocated, -simpleBuffer.SizeInBytes);
            simpleBuffer.Dispose();
        }
    }
}

/// <summary>
/// Simple memory buffer implementation for testing.
/// Thread-safe implementation for concurrent stress tests.
/// </summary>
internal sealed class SimpleMemoryBuffer : IMemoryBuffer
{
    internal readonly byte[] Data;
    private volatile bool _disposed;
    private readonly Lock _lock = new();

    public SimpleMemoryBuffer(long sizeInBytes, MemoryOptions options)
    {
        Data = new byte[sizeInBytes];
        Options = options;
    }

    public SimpleMemoryBuffer(Memory<byte> data, MemoryOptions options)
    {
        Data = data.ToArray();
        Options = options;
    }

    public long SizeInBytes => Data.Length;
    public MemoryOptions Options { get; }
    public bool IsDisposed => _disposed;

    public void Dispose()
    {
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        lock (_lock)
        {
            ThrowIfDisposed();
            var sourceBytes = System.Runtime.InteropServices.MemoryMarshal.Cast<T, byte>(source.Span);
            sourceBytes.CopyTo(Data.AsSpan((int)offset));
            return ValueTask.CompletedTask;
        }
    }

    public ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        lock (_lock)
        {
            ThrowIfDisposed();
            var dataSpan = Data.AsSpan((int)offset);
            var destBytes = System.Runtime.InteropServices.MemoryMarshal.Cast<T, byte>(destination.Span);
            var bytesToCopy = Math.Min(dataSpan.Length, destBytes.Length);
            dataSpan[..bytesToCopy].CopyTo(destBytes);
            return ValueTask.CompletedTask;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
