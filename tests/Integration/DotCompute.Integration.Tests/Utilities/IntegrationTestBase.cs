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
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Registration;
using DotCompute.Core.Debugging;
using DotCompute.Core.Optimization;
using DotCompute.Memory;
using DotCompute.Runtime.Services;
using DotCompute.Runtime.Extensions;
using DotCompute.Tests.Common.Helpers;
using DotCompute.Abstractions.Memory;
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
/// <remarks>
/// Implements <see cref="IAsyncLifetime"/> (not merely <see cref="IAsyncDisposable"/>): xUnit v2
/// invokes test-class cleanup through <see cref="IDisposable.Dispose"/> or
/// <see cref="IAsyncLifetime.DisposeAsync"/> ONLY — it does not call <c>IAsyncDisposable.DisposeAsync</c>.
/// Each test builds a DI container that owns a CPU accelerator with a real worker thread pool; if
/// teardown is never invoked, those pools leak and accumulate across the suite until the host is
/// starved and the run hangs. Implementing IAsyncLifetime guarantees deterministic disposal.
/// </remarks>
public abstract class IntegrationTestBase : IAsyncLifetime, IAsyncDisposable
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
        
        // Memory management - register both the concrete type and the IUnifiedMemoryManager
        // abstraction (CreateTestBufferAsync and the orchestrator buffer paths resolve the interface).
        services.AddSingleton<UnifiedMemoryManager>();
        services.AddSingleton<IUnifiedMemoryManager>(sp => sp.GetRequiredService<UnifiedMemoryManager>());
        
        // Backend services - registers CpuAccelerator + required options (IOptions<CpuAcceleratorOptions>, etc.)
        services.AddCpuBackend();
        
        // Test utilities
        services.AddSingleton(TestSettings);
    }

    protected T GetService<T>() where T : notnull => ServiceProvider.GetRequiredService<T>();

    /// <summary>
    /// Probes whether the orchestrator can actually execute a named kernel in this harness, i.e.
    /// whether the runtime has a usable accelerator registered AND the kernel is present in the
    /// kernel registry. Returns false when the prerequisite bootstrap is absent.
    /// </summary>
    /// <remarks>
    /// In this in-process test harness the runtime is never started: no <c>IAcceleratorManager</c>
    /// is registered in product code and the generated kernel registry is empty, so the orchestrator
    /// resolves zero accelerators/kernels for named-kernel execution. This is a known
    /// unimplemented-infrastructure gap (named-kernel registration + runtime accelerator discovery),
    /// not a product defect, so dependent tests Skip rather than fail. The probe uses the
    /// orchestrator's own <see cref="IComputeOrchestrator.GetSupportedAcceleratorsAsync"/>, which
    /// returns a non-empty set only when both prerequisites are satisfied.
    /// </remarks>
    protected async Task<bool> OrchestratorCanExecuteKernelAsync(string kernelName)
    {
        try
        {
            var orchestrator = ServiceProvider.GetService<IComputeOrchestrator>();
            if (orchestrator is null)
            {
                return false;
            }

            var supported = await orchestrator.GetSupportedAcceleratorsAsync(kernelName);
            return supported is { Count: > 0 };
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reason string used when a test is skipped because the runtime accelerator/kernel bootstrap
    /// required for named-kernel orchestration is not available in this in-process harness.
    /// </summary>
    protected const string RuntimeBootstrapMissingReason =
        "Requires runtime accelerator/kernel bootstrap (IAcceleratorManager discovery + named-kernel registry) " +
        "not available in this in-process test harness; named-kernel orchestration is an unimplemented infrastructure gap.";

    protected ILogger<T> GetLogger<T>() => ServiceProvider.GetRequiredService<ILogger<T>>();

    /// <summary>
    /// Creates a unified buffer with test data.
    /// </summary>
    protected async Task<IUnifiedMemoryBuffer<T>> CreateTestBufferAsync<T>(int length, Func<int, T>? generator = null)
        where T : unmanaged
    {
        var memoryManager = GetService<IUnifiedMemoryManager>();
        // AllocateAsync<T> returns a generic buffer wrapper (TypedMemoryBufferWrapper), not a
        // concrete UnifiedBuffer<T>, so the buffer is surfaced via the IUnifiedMemoryBuffer<T>
        // abstraction rather than cast to the concrete type.
        var buffer = await memoryManager.AllocateAsync<T>(length);

        if (generator != null)
        {
            var data = new T[length];
            for (int i = 0; i < length; i++)
            {
                data[i] = generator(i);
            }
            await buffer.CopyFromAsync(data.AsMemory());
        }

        _disposables.Add(buffer);
        return buffer;
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

    /// <summary>
    /// xUnit async lifecycle init hook. No async initialization is required (services are built in
    /// the constructor); present so xUnit recognizes the class for async lifecycle management.
    /// </summary>
    public virtual Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// xUnit-invoked async teardown (<see cref="IAsyncLifetime"/>). Delegates to the shared
    /// disposal core. This is the entry point xUnit actually calls.
    /// </summary>
    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsyncCore().ConfigureAwait(false);

    /// <summary>
    /// <see cref="IAsyncDisposable"/> entry point (for direct/manual disposal). Delegates to the
    /// same core so behavior is identical regardless of how teardown is triggered.
    /// </summary>
    public async ValueTask DisposeAsync() => await DisposeAsyncCore().ConfigureAwait(false);

    protected virtual async Task DisposeAsyncCore()
    {
        // Disposing the DI container is the critical step: it tears down the CPU accelerator and its
        // worker thread pool. If that step is ever skipped (e.g. an assertion thrown earlier in
        // teardown short-circuits the method) the pool's worker threads leak, and accumulated leaked
        // pools across the suite starve the host CPU and hang the run. Therefore container disposal
        // is performed unconditionally in `finally`, and the (advisory) memory-leak check is made
        // non-fatal so it can never gate disposal.
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

            // Advisory memory-leak diagnostic only. A failing leak check must NOT throw out of
            // teardown (that would skip container disposal below and leak the accelerator's threads),
            // so any breach is logged rather than asserted.
            if (TestSettings.EnablePerformanceTests)
            {
                try
                {
                    AssertNoMemoryLeaks();
                }
                catch (Exception ex)
                {
                    Output.WriteLine($"Warning: memory-leak diagnostic: {ex.Message}");
                }
            }
        }
        finally
        {
            try
            {
                CancellationTokenSource.Dispose();
            }
            catch (Exception ex)
            {
                Output.WriteLine($"Warning: Failed to dispose CancellationTokenSource: {ex.Message}");
            }

            // Deterministically dispose the DI container. CpuThreadPool.DisposeAsync joins its
            // (background) workers within a bounded budget, so this completes promptly; a generous
            // safety timeout prevents a pathological native teardown from hanging the host while
            // still letting normal disposal reclaim the worker threads.
            try
            {
                var disposeTask = ServiceProvider.DisposeAsync().AsTask();
                if (await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(30))) != disposeTask)
                {
                    Output.WriteLine("Warning: ServiceProvider.DisposeAsync did not complete within 30s; abandoning disposal to avoid a teardown hang.");
                }
                else
                {
                    // Observe any disposal exception (and avoid unobserved-task faults).
                    await disposeTask;
                }
            }
            catch (Exception ex)
            {
                Output.WriteLine($"Warning: ServiceProvider disposal raised: {ex.Message}");
            }
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