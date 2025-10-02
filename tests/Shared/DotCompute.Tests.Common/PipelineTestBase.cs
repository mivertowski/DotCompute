// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Core.Pipelines;
using DotCompute.Tests.Common.Generators;
using DotCompute.Tests.Common.Mocks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Tests.Common;

/// <summary>
/// Base class for pipeline testing providing shared functionality and infrastructure.
/// </summary>
public abstract class PipelineTestBase : IDisposable
{
    private bool _disposed;
    private readonly ServiceProvider _serviceProvider;

    protected PipelineTestBase()
    {
        var services = new ServiceCollection();
        ConfigureServicesInternal(services);
        _serviceProvider = services.BuildServiceProvider();


        Services = _serviceProvider;
        DataGenerator = Services.GetRequiredService<PipelineTestDataGenerator>();
        Logger = Services.GetService<ILogger<PipelineTestBase>>();
    }

    /// <summary>
    /// Gets the configured service provider for dependency injection.
    /// </summary>
    protected IServiceProvider Services { get; }

    /// <summary>
    /// Gets the test data generator for creating synthetic test datasets.
    /// </summary>
    protected PipelineTestDataGenerator DataGenerator { get; }

    /// <summary>
    /// Gets the logger for test diagnostic information.
    /// </summary>
    protected ILogger<PipelineTestBase>? Logger { get; }

    /// <summary>
    /// Creates a new kernel chain builder for testing pipeline functionality.
    /// </summary>
    /// <returns>Configured kernel chain builder instance</returns>
    protected IKernelChainBuilder CreatePipelineBuilder() => Services.GetRequiredService<IKernelChainBuilder>();

    /// <summary>
    /// Asserts that pipeline execution produces expected results with proper validation.
    /// </summary>
    /// <typeparam name="T">The data type being processed</typeparam>
    /// <param name="input">Input data for the pipeline</param>
    /// <param name="expected">Expected output results</param>
    /// <param name="tolerance">Tolerance for floating-point comparisons</param>
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code.", Justification = "Test code uses dynamic for type-specific floating point comparison.")]
    protected static void AssertPipelineExecution<T>(T[] input, T[] expected, double tolerance = 1e-6)

        where T : IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(expected);

        Assert.Equal(expected.Length, input.Length);

        for (var i = 0; i < expected.Length; i++)
        {
            if (typeof(T) == typeof(float) || typeof(T) == typeof(double))
            {
                AssertFloatingPointEqual((dynamic)expected[i], (dynamic)input[i], tolerance);
            }
            else
            {
                Assert.Equal(expected[i], input[i]);
            }
        }
    }

    /// <summary>
    /// Validates that actual performance meets or exceeds baseline expectations.
    /// </summary>
    /// <param name="actual">Actual execution time measured</param>
    /// <param name="baseline">Baseline performance expectation</param>
    /// <param name="tolerance">Performance tolerance factor (1.5 = 50% slower allowed)</param>
    protected static void ValidatePerformanceBaseline(TimeSpan actual, TimeSpan baseline, double tolerance = 1.5)
    {
        var maxAllowedTime = TimeSpan.FromTicks((long)(baseline.Ticks * tolerance));


        Assert.True(
            actual <= maxAllowedTime,
            $"Performance regression detected. Actual: {actual.TotalMilliseconds:F2}ms, " +
            $"Expected: ≤{maxAllowedTime.TotalMilliseconds:F2}ms (baseline: {baseline.TotalMilliseconds:F2}ms)");
    }

    /// <summary>
    /// Generates test data of specified size with configurable patterns.
    /// </summary>
    /// <typeparam name="T">Type of test data to generate</typeparam>
    /// <param name="size">Number of elements to generate</param>
    /// <param name="pattern">Data pattern type for generation</param>
    /// <returns>Generated test data array</returns>
    protected T[] GenerateTestData<T>(int size, DataPattern pattern = DataPattern.Random) where T : struct => DataGenerator.GenerateNumericData<T>(size, pattern);

    /// <summary>
    /// Measures execution time for a pipeline operation with high precision.
    /// </summary>
    /// <param name="operation">The operation to measure</param>
    /// <returns>Precise execution time measurement</returns>
    protected static async Task<TimeSpan> MeasureExecutionTimeAsync(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // Warm up
        await operation();

        // Force garbage collection before measurement

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var stopwatch = Stopwatch.StartNew();
        await operation();
        stopwatch.Stop();

        return stopwatch.Elapsed;
    }

    /// <summary>
    /// Measures execution time for a synchronous pipeline operation.
    /// </summary>
    /// <param name="operation">The operation to measure</param>
    /// <returns>Precise execution time measurement</returns>
    protected static TimeSpan MeasureExecutionTime(Action operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // Warm up
        operation();

        // Force garbage collection before measurement

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var stopwatch = Stopwatch.StartNew();
        operation();
        stopwatch.Stop();

        return stopwatch.Elapsed;
    }

    /// <summary>
    /// Validates memory usage stays within acceptable bounds during pipeline execution.
    /// </summary>
    /// <param name="operation">The operation to monitor</param>
    /// <param name="maxMemoryIncreaseMB">Maximum allowed memory increase in MB</param>
    /// <returns>Memory usage statistics</returns>
    protected static async Task<MemoryUsageMetrics> ValidateMemoryUsageAsync(
        Func<Task> operation,

        int maxMemoryIncreaseMB = 50)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // Force clean state
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var initialMemory = GC.GetTotalMemory(false);


        await operation();


        GC.Collect();
        GC.WaitForPendingFinalizers();


        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;
        var memoryIncreaseMB = memoryIncrease / (1024.0 * 1024.0);

        Assert.True(
            memoryIncreaseMB <= maxMemoryIncreaseMB,
            $"Memory usage exceeded limit. Increase: {memoryIncreaseMB:F2}MB, Limit: {maxMemoryIncreaseMB}MB");

        return new MemoryUsageMetrics
        {
            InitialMemoryBytes = initialMemory,
            FinalMemoryBytes = finalMemory,
            MemoryIncreaseBytes = memoryIncrease,
            MemoryIncreaseMB = memoryIncreaseMB
        };
    }

    /// <summary>
    /// Creates a test execution context with cancellation support and timeout.
    /// </summary>
    /// <param name="timeoutSeconds">Timeout in seconds</param>
    /// <returns>Cancellation token with configured timeout</returns>
    protected static CancellationToken CreateTestTimeout(int timeoutSeconds = 30)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        return cts.Token;
    }

    /// <summary>
    /// Configures services for the test environment internally (non-virtual).
    /// </summary>
    /// <param name="services">Service collection to configure</param>
    private void ConfigureServicesInternal(IServiceCollection services)
    {
        _ = services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug));
        _ = services.AddSingleton<PipelineTestDataGenerator>();

        // Register core DotCompute services (would normally be done by extension methods)

        _ = services.AddSingleton<IKernelChainBuilder, KernelChainBuilder>();

        // Mock services for testing

        _ = services.AddSingleton<IComputeOrchestrator, MockComputeOrchestrator>();

        // Call virtual method for derived class customization

        ConfigureAdditionalServices(services);
    }

    /// <summary>
    /// Override this method to configure additional services. Called after base services are configured.
    /// </summary>
    /// <param name="services">Service collection to configure</param>
    protected virtual void ConfigureAdditionalServices(IServiceCollection services)
    {
        // Default implementation does nothing
    }

    private static void AssertFloatingPointEqual(float expected, float actual, double tolerance)
    {
        var diff = Math.Abs(expected - actual);
        Assert.True(diff <= tolerance, $"Float values not equal within tolerance. Expected: {expected}, Actual: {actual}, Tolerance: {tolerance}, Diff: {diff}");
    }

    private static void AssertFloatingPointEqual(double expected, double actual, double tolerance)
    {
        var diff = Math.Abs(expected - actual);
        Assert.True(diff <= tolerance, $"Double values not equal within tolerance. Expected: {expected}, Actual: {actual}, Tolerance: {tolerance}, Diff: {diff}");
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _serviceProvider?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Memory usage metrics captured during pipeline execution.
/// </summary>
public sealed class MemoryUsageMetrics
{
    /// <summary>
    /// Gets the initial memory usage before operation.
    /// </summary>
    public required long InitialMemoryBytes { get; init; }

    /// <summary>
    /// Gets the final memory usage after operation.
    /// </summary>
    public required long FinalMemoryBytes { get; init; }

    /// <summary>
    /// Gets the memory increase in bytes.
    /// </summary>
    public required long MemoryIncreaseBytes { get; init; }

    /// <summary>
    /// Gets the memory increase in megabytes.
    /// </summary>
    public required double MemoryIncreaseMB { get; init; }
}

/// <summary>
/// Data generation patterns for test scenarios.
/// </summary>
public enum DataPattern
{
    /// <summary>Random data distribution.</summary>
    Random,

    /// <summary>Sequential ascending values.</summary>
    Sequential,

    /// <summary>All values are identical.</summary>
    Uniform,

    /// <summary>Normal (Gaussian) distribution.</summary>
    Normal,

    /// <summary>Values alternate between extremes.</summary>
    Alternating,


    /// <summary>Values include edge cases (min, max, zero).</summary>
    EdgeCases
}