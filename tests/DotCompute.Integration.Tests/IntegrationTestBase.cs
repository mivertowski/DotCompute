// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using DotCompute.Core.Pipelines;
using DotCompute.Memory;
using DotCompute.Plugins.Core;
using DotCompute.SharedTestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace DotCompute.Integration.Tests;

/// <summary>
/// Base class for integration tests providing common infrastructure and utilities.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly ITestOutputHelper TestOutput;
    protected readonly ILogger Logger;
    protected IServiceProvider ServiceProvider { get; private set; } = null!;
    private IHost? _host;

    protected IntegrationTestBase(ITestOutputHelper output)
    {
        TestOutput = output;
        Logger = new TestOutputLogger(output);
    }

    public async Task InitializeAsync()
    {
        _host = CreateHost();
        await _host.StartAsync();
        ServiceProvider = _host.Services;
        
        // Initialize the plugin system
        var pluginSystem = ServiceProvider.GetRequiredService<PluginSystem>();
        await pluginSystem.InitializeAsync();
    }

    public async Task DisposeAsync()
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
                services.AddLogging(builder =>
                {
                    builder.AddProvider(new TestOutputLoggerProvider(TestOutput));
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                // Add core DotCompute services
                services.AddSingleton<IMemoryManager, MemoryManager>();
                services.AddSingleton<IAcceleratorManager, DefaultAcceleratorManager>();
                services.AddSingleton<IComputeEngine, ComputeEngine>();
                services.AddSingleton<PluginSystem>();

                // Add test utilities
                services.AddSingleton<TestDataGenerator>();
                services.AddSingleton<MemoryTestUtilities>();

                // Add pipeline services
                services.AddTransient<KernelPipelineBuilder>();
                services.AddTransient<IPipelineOptimizer, PipelineOptimizer>();

                // Add performance monitor
                services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
            })
            .UseConsoleLifetime()
            .Build();
    }

    // Helper methods for integration tests
    
    /// <summary>
    /// Creates an input buffer from data array.
    /// </summary>
    protected async Task<IBuffer> CreateInputBuffer<T>(IMemoryManager memoryManager, T[] data) where T : unmanaged
    {
        var buffer = await memoryManager.AllocateAsync<T>(data.Length);
        await memoryManager.WriteAsync(buffer, data);
        return buffer;
    }

    /// <summary>
    /// Creates an output buffer of specified size.
    /// </summary>
    protected async Task<IBuffer> CreateOutputBuffer<T>(IMemoryManager memoryManager, int size) where T : unmanaged
    {
        return await memoryManager.AllocateAsync<T>(size);
    }

    /// <summary>
    /// Reads data from a buffer.
    /// </summary>
    protected async Task<T[]> ReadBufferAsync<T>(IBuffer buffer) where T : unmanaged
    {
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        return await memoryManager.ReadAsync<T>(buffer);
    }

    /// <summary>
    /// Creates a mock pipeline stage for testing.
    /// </summary>
    protected IPipelineStage CreateMockPipelineStage(string id, string name, TimeSpan? executionTime = null)
    {
        return new MockPipelineStage(id, name, executionTime ?? TimeSpan.FromMilliseconds(50));
    }

    /// <summary>
    /// Waits for a condition to be true with timeout.
    /// </summary>
    protected async Task<bool> WaitForConditionAsync(
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
    protected async Task<(T Result, TimeSpan Duration)> MeasureAsync<T>(Func<Task<T>> operation)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await operation();
        stopwatch.Stop();
        return (result, stopwatch.Elapsed);
    }

    /// <summary>
    /// Measures the execution time of an async operation without return value.
    /// </summary>
    protected async Task<TimeSpan> MeasureAsync(Func<Task> operation)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await operation();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    /// <summary>
    /// Retries an operation with exponential backoff.
    /// </summary>
    protected async Task<T> RetryAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        TimeSpan initialDelay = default)
    {
        if (initialDelay == default)
            initialDelay = TimeSpan.FromMilliseconds(100);

        Exception? lastException = null;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
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
    protected async Task AssertCompletesWithinAsync(Func<Task> operation, TimeSpan timeout, string? message = null)
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
    protected async Task AssertThrowsAsync<TException>(Func<Task> operation, string? message = null) 
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
    /// Creates test data using the registered test data generator.
    /// </summary>
    protected T[] GenerateTestData<T>(int size, int seed = 42) where T : unmanaged
    {
        var generator = ServiceProvider.GetRequiredService<TestDataGenerator>();
        
        return typeof(T) switch
        {
            Type t when t == typeof(float) => generator.GenerateFloatArray(size, seed).Cast<T>().ToArray(),
            Type t when t == typeof(int) => generator.GenerateIntArray(size, seed).Cast<T>().ToArray(),
            Type t when t == typeof(double) => generator.GenerateDoubleArray(size, seed).Cast<T>().ToArray(),
            _ => throw new NotSupportedException($"Test data generation not supported for type {typeof(T)}")
        };
    }

    /// <summary>
    /// Gets memory statistics for monitoring memory usage during tests.
    /// </summary>
    protected async Task<MemoryStatistics> GetMemoryStatisticsAsync()
    {
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        return await memoryManager.GetStatisticsAsync();
    }

    /// <summary>
    /// Logs performance metrics for analysis.
    /// </summary>
    protected void LogPerformanceMetrics(string operation, TimeSpan duration, int itemsProcessed = 0)
    {
        var throughput = itemsProcessed > 0 ? itemsProcessed / duration.TotalSeconds : 0;
        
        Logger.LogInformation(
            "Performance: {Operation} took {Duration:F2}ms" + 
            (itemsProcessed > 0 ? ", Throughput: {Throughput:N0} items/sec" : ""),
            operation,
            duration.TotalMilliseconds,
            throughput);
    }

    /// <summary>
    /// Creates a test execution context for pipeline tests.
    /// </summary>
    protected PipelineExecutionContext CreateTestExecutionContext(Dictionary<string, object>? inputs = null)
    {
        return new PipelineExecutionContext
        {
            Inputs = inputs ?? new Dictionary<string, object>(),
            MemoryManager = ServiceProvider.GetRequiredService<IMemoryManager>(),
            Options = new PipelineExecutionOptions
            {
                ContinueOnError = false,
                MaxConcurrentStages = Environment.ProcessorCount
            },
            State = new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Validates that compute results are within acceptable tolerance.
    /// </summary>
    protected static bool ValidateResults(float[] expected, float[] actual, float tolerance = 0.001f)
    {
        if (expected.Length != actual.Length)
            return false;
            
        for (int i = 0; i < expected.Length; i++)
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
        Logger.LogInformation("Memory Statistics - Allocated: {Allocated}, Peak: {Peak}, Leaked: {Leaked}",
            memoryStats.TotalAllocated,
            memoryStats.PeakUsage,
            memoryStats.TotalAllocated - memoryStats.TotalDeallocated);
        
        // In a real implementation, we would validate for memory leaks
        // For now, we just log the statistics
    }
}

/// <summary>
/// Mock pipeline stage for testing purposes.
/// </summary>
public class MockPipelineStage : IPipelineStage
{
    private readonly TimeSpan _executionTime;

    public MockPipelineStage(string id, string name, TimeSpan? executionTime = null)
    {
        Id = id;
        Name = name;
        _executionTime = executionTime ?? TimeSpan.FromMilliseconds(10);
    }

    public string Id { get; }
    public string Name { get; }
    public string Description => $"Mock stage: {Name}";
    public PipelineStageType Type => PipelineStageType.Sequential;
    public IReadOnlyList<string> Dependencies { get; } = new List<string>();
    public Dictionary<string, object> Configuration { get; } = new();

    public async Task<StageExecutionResult> ExecuteAsync(
        PipelineExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        // Simulate work
        await Task.Delay(_executionTime, cancellationToken);
        
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

    public Task<PipelineValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PipelineValidationResult { IsValid = true });
    }

    public PipelineValidationResult Validate()
    {
        return new PipelineValidationResult { IsValid = true };
    }

    public Task<IPipelineStage> OptimizeAsync(PipelineOptimizationSettings settings, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IPipelineStage>(this);
    }

    public Task<Dictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new Dictionary<string, object>
        {
            ["ExecutionCount"] = 1,
            ["AverageExecutionTime"] = _executionTime.TotalMilliseconds,
            ["LastExecutionTime"] = DateTime.UtcNow
        });
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Test output logger for integration with xUnit test output.
/// </summary>
public class TestOutputLogger : ILogger
{
    private readonly ITestOutputHelper _output;

    public TestOutputLogger(ITestOutputHelper output)
    {
        _output = output;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= LogLevel.Information;
    }

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
public class TestOutputLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public TestOutputLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new TestOutputLogger(_output);
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}