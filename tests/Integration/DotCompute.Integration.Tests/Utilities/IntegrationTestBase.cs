// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Backends.CPU;
using DotCompute.Core.Debugging;
using DotCompute.Core.Optimization;
using DotCompute.Memory;
using DotCompute.Runtime.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Integration.Tests.Utilities;

/// <summary>
/// Base class for DotCompute integration tests providing common infrastructure.
/// </summary>
public abstract class IntegrationTestBase : IAsyncDisposable
{
    protected readonly ITestOutputHelper Output;
    protected readonly ServiceProvider ServiceProvider;
    protected readonly IConfiguration Configuration;
    protected readonly CancellationTokenSource CancellationTokenSource;
    protected readonly PerformanceCounter PerformanceCounter;
    protected readonly TestSettings TestSettings;

    private readonly List<IDisposable> _disposables = [];
    private long _initialMemoryUsage;

    protected IntegrationTestBase(ITestOutputHelper output)
    {
        Output = output;
        CancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(300));
        
        // Load configuration
        Configuration = new ConfigurationBuilder()
            .AddJsonFile("testsettings.json", optional: false)
            .Build();
            
        TestSettings = Configuration.GetSection("TestSettings").Get<TestSettings>() ?? new TestSettings();
        
        // Build service provider
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
        
        PerformanceCounter = new PerformanceCounter();
        _initialMemoryUsage = GC.GetTotalMemory(false);
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(Configuration.GetSection("Logging"));
            builder.AddConsole();
        });

        // DotCompute Core Services
        services.AddDotComputeRuntime();
        services.AddProductionOptimization();
        services.AddProductionDebugging();
        
        // Memory management
        services.AddSingleton<IUnifiedMemoryManager, UnifiedMemoryManager>();
        
        // Backend services
        services.AddSingleton<CpuAccelerator>();
        
        // Test utilities
        services.AddSingleton(TestSettings);
        services.AddSingleton<TestDataGenerator>();
        services.AddSingleton<MockAcceleratorProvider>();
    }

    protected T GetService<T>() where T : notnull => ServiceProvider.GetRequiredService<T>();

    protected ILogger<T> GetLogger<T>() => ServiceProvider.GetRequiredService<ILogger<T>>();

    /// <summary>
    /// Creates a unified buffer with test data.
    /// </summary>
    protected async Task<UnifiedBuffer<T>> CreateTestBufferAsync<T>(int length, Func<int, T>? generator = null) 
        where T : unmanaged
    {
        var memoryManager = GetService<IUnifiedMemoryManager>();
        var buffer = await memoryManager.AllocateAsync<T>(length);
        
        if (generator != null)
        {
            var span = await buffer.GetHostSpanAsync();
            for (int i = 0; i < length; i++)
            {
                span[i] = generator(i);
            }
        }
        
        _disposables.Add(buffer);
        return (UnifiedBuffer<T>)buffer;
    }

    /// <summary>
    /// Measures execution time and memory usage of an operation.
    /// </summary>
    protected async Task<PerformanceMeasurement> MeasurePerformanceAsync(
        Func<Task> operation, 
        string operationName = "Operation")
    {
        var startMemory = GC.GetTotalMemory(true);
        var stopwatch = Stopwatch.StartNew();
        
        await operation();
        
        stopwatch.Stop();
        var endMemory = GC.GetTotalMemory(false);
        
        var measurement = new PerformanceMeasurement
        {
            OperationName = operationName,
            ElapsedTime = stopwatch.Elapsed,
            MemoryUsed = endMemory - startMemory,
            StartMemory = startMemory,
            EndMemory = endMemory
        };
        
        Output.WriteLine($"Performance: {operationName} took {measurement.ElapsedTime.TotalMilliseconds:F2}ms, used {measurement.MemoryUsed / 1024:F1}KB");
        
        return measurement;
    }

    /// <summary>
    /// Asserts that an operation completes within the expected time tolerance.
    /// </summary>
    protected void AssertPerformance(PerformanceMeasurement measurement, TimeSpan expectedTime)
    {
        var tolerancePercent = TestSettings.PerformanceTolerancePercent / 100.0;
        var maxAllowedTime = expectedTime.Add(TimeSpan.FromTicks((long)(expectedTime.Ticks * tolerancePercent)));
        
        measurement.ElapsedTime.Should().BeLessThan(maxAllowedTime,
            $"Operation {measurement.OperationName} took {measurement.ElapsedTime.TotalMilliseconds:F2}ms, " +
            $"expected maximum {maxAllowedTime.TotalMilliseconds:F2}ms");
    }

    /// <summary>
    /// Asserts that memory usage is within acceptable bounds.
    /// </summary>
    protected void AssertMemoryUsage(long memoryUsed, long maxExpectedMemory)
    {
        memoryUsed.Should().BeLessThan(maxExpectedMemory,
            $"Memory usage {memoryUsed / 1024 / 1024:F1}MB exceeded maximum {maxExpectedMemory / 1024 / 1024:F1}MB");
    }

    /// <summary>
    /// Checks for memory leaks after test completion.
    /// </summary>
    protected void AssertNoMemoryLeaks()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemoryUsage = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemoryUsage - _initialMemoryUsage;
        var thresholdBytes = TestSettings.MemoryLeakThresholdMB * 1024 * 1024;
        
        memoryIncrease.Should().BeLessThan(thresholdBytes,
            $"Potential memory leak detected. Memory increased by {memoryIncrease / 1024 / 1024:F1}MB, " +
            $"threshold is {TestSettings.MemoryLeakThresholdMB}MB");
    }

    /// <summary>
    /// Executes an operation multiple times concurrently to test thread safety.
    /// </summary>
    protected async Task<List<T>> ExecuteConcurrentlyAsync<T>(
        Func<int, Task<T>> operation, 
        int threadCount = 0)
    {
        threadCount = threadCount == 0 ? TestSettings.ConcurrencyTestThreads : threadCount;
        
        var tasks = new List<Task<T>>();
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            tasks.Add(Task.Run(() => operation(threadId)));
        }
        
        var results = await Task.WhenAll(tasks);
        return new List<T>(results);
    }

    /// <summary>
    /// Skips test if CUDA is not available and test requires it.
    /// </summary>
    protected void SkipIfCudaNotAvailable()
    {
        Skip.IfNot(TestSettings.EnableCudaTests && IsCudaAvailable(), "CUDA not available or disabled");
    }

    /// <summary>
    /// Checks if CUDA is available on the system.
    /// </summary>
    private static bool IsCudaAvailable()
    {
        try
        {
            // Simple CUDA availability check
            return System.IO.Directory.Exists("/usr/local/cuda") || 
                   System.IO.File.Exists(@"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.0\bin\nvcc.exe");
        }
        catch
        {
            return false;
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        try
        {
            // Dispose all tracked disposables
            foreach (var disposable in _disposables)
            {
                try
                {
                    if (disposable is IAsyncDisposable asyncDisposable)
                        await asyncDisposable.DisposeAsync();
                    else
                        disposable.Dispose();
                }
                catch (Exception ex)
                {
                    Output.WriteLine($"Warning: Failed to dispose {disposable.GetType().Name}: {ex.Message}");
                }
            }
            
            _disposables.Clear();
            
            // Check for memory leaks
            if (TestSettings.EnablePerformanceTests)
            {
                AssertNoMemoryLeaks();
            }
            
            CancellationTokenSource.Dispose();
            await ServiceProvider.DisposeAsync();
        }
        catch (Exception ex)
        {
            Output.WriteLine($"Error during test cleanup: {ex}");
            throw;
        }
    }
}

/// <summary>
/// Test settings loaded from configuration.
/// </summary>
public class TestSettings
{
    public int TimeoutSeconds { get; set; } = 300;
    public int MaxRetries { get; set; } = 3;
    public bool EnableCudaTests { get; set; } = true;
    public bool EnablePerformanceTests { get; set; } = true;
    public double PerformanceTolerancePercent { get; set; } = 20;
    public long MemoryLeakThresholdMB { get; set; } = 100;
    public int ConcurrencyTestThreads { get; set; } = 10;
    public long LargeDatasetSizeMB { get; set; } = 100;
}

/// <summary>
/// Performance measurement data.
/// </summary>
public class PerformanceMeasurement
{
    public required string OperationName { get; init; }
    public TimeSpan ElapsedTime { get; init; }
    public long MemoryUsed { get; init; }
    public long StartMemory { get; init; }
    public long EndMemory { get; init; }
}

/// <summary>
/// Simple performance counter for tracking operations.
/// </summary>
public class PerformanceCounter
{
    private readonly Dictionary<string, List<TimeSpan>> _timings = new();
    
    public void RecordTiming(string operation, TimeSpan duration)
    {
        if (!_timings.ContainsKey(operation))
            _timings[operation] = new List<TimeSpan>();
            
        _timings[operation].Add(duration);
    }
    
    public TimeSpan GetAverageTime(string operation)
    {
        if (!_timings.ContainsKey(operation) || _timings[operation].Count == 0)
            return TimeSpan.Zero;
            
        var total = _timings[operation].Aggregate(TimeSpan.Zero, (sum, time) => sum.Add(time));
        return TimeSpan.FromTicks(total.Ticks / _timings[operation].Count);
    }
}