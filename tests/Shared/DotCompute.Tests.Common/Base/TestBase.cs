using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DotCompute.Tests.Common.Assertions;
using DotCompute.Tests.Common.Fixtures;
using DotCompute.Tests.Common.Helpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Tests.Common.Base;

/// <summary>
/// Base class for all test classes in the DotCompute test suite.
/// Provides common functionality, setup, and teardown operations.
/// </summary>
public abstract class TestBase : IDisposable
{
    private bool _disposed;
    
    /// <summary>
    /// Gets the test output helper for logging test information.
    /// </summary>
    protected ITestOutputHelper Output { get; }
    
    /// <summary>
    /// Gets the common test fixture providing shared resources.
    /// </summary>
    protected CommonTestFixture Fixture { get; }
    
    /// <summary>
    /// Gets hardware information for conditional test execution.
    /// </summary>
    protected HardwareDetection.HardwareInfo HardwareInfo => Fixture.HardwareInfo;
    
    // TestDataGenerator is a static class - access it directly via TestDataGenerator.Method()
    
    /// <summary>
    /// Gets the temporary directory for this test instance.
    /// </summary>
    protected string TempDirectory { get; }
    
    /// <summary>
    /// Gets the stopwatch for measuring test execution time.
    /// </summary>
    protected Stopwatch Stopwatch { get; } = new();
    
    /// <summary>
    /// Initializes a new instance of the TestBase class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    /// <param name="fixture">The common test fixture.</param>
    protected TestBase(ITestOutputHelper output, CommonTestFixture fixture)
    {
        Output = output ?? throw new ArgumentNullException(nameof(output));
        Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        
        // Set output in fixture for logging
        Fixture.SetOutput(output);
        
        // Create test-specific temp directory
        TempDirectory = CreateTestTempDirectory();
        
        // Start timing
        Stopwatch.Start();
        
        LogTestStart();
    }
    
    #region Hardware Requirements
    
    /// <summary>
    /// Ensures CUDA is available, skipping the test if not.
    /// </summary>
    protected void RequireCuda() => Fixture.RequireCuda();
    
    /// <summary>
    /// Ensures OpenCL is available, skipping the test if not.
    /// </summary>
    protected void RequireOpenCL() => Fixture.RequireOpenCL();
    
    /// <summary>
    /// Ensures GPU support is available, skipping the test if not.
    /// </summary>
    protected void RequireGpu() => Fixture.RequireGpu();
    
    /// <summary>
    /// Ensures multi-GPU setup is available, skipping the test if not.
    /// </summary>
    protected void RequireMultiGpu() => Fixture.RequireMultiGpu();
    
    /// <summary>
    /// Ensures high-performance CPU features are available, skipping the test if not.
    /// </summary>
    protected void RequireHighPerformanceCpu() => Fixture.RequireHighPerformanceCpu();
    
    /// <summary>
    /// Ensures sufficient memory is available, skipping the test if not.
    /// </summary>
    protected void RequireHighMemory() => Fixture.RequireHighMemory();

    #endregion

    #region Logging and Output


    /// <summary>
    /// Logs a message with timestamp.
    /// </summary>
    /// <param name="message">The message to log.</param>
    protected void Log(string message) => Output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");


    /// <summary>
    /// Logs a formatted message with timestamp.
    /// </summary>
    /// <param name="format">The format string.</param>
    /// <param name="args">The format arguments.</param>
    protected void Log(string format, params object[] args) => Log(string.Format(format, args));


    /// <summary>
    /// Logs test start information.
    /// </summary>
    private void LogTestStart()
    {
        var testName = GetType().Name;
        Log($"=== Starting Test: {testName} ===");
        Log($"Temp Directory: {TempDirectory}");
    }
    
    /// <summary>
    /// Logs test completion information.
    /// </summary>
    private void LogTestCompletion()
    {
        Stopwatch.Stop();
        var testName = GetType().Name;
        Log($"=== Completed Test: {testName} in {Stopwatch.Elapsed} ===");
    }
    
    #endregion
    
    #region File and Directory Management
    
    /// <summary>
    /// Creates a temporary file in the test's temp directory.
    /// </summary>
    /// <param name="fileName">Optional file name. If not provided, a random name is generated.</param>
    /// <param name="content">Optional content to write to the file.</param>
    /// <returns>The full path to the created temporary file.</returns>
    protected string CreateTempFile(string? fileName = null, string? content = null)
    {
        fileName ??= $"test_{Guid.NewGuid():N}.tmp";
        var filePath = Path.Combine(TempDirectory, fileName);
        
        content ??= "";
        File.WriteAllText(filePath, content);
        
        return filePath;
    }
    
    /// <summary>
    /// Creates a temporary subdirectory in the test's temp directory.
    /// </summary>
    /// <param name="directoryName">Optional directory name. If not provided, a random name is generated.</param>
    /// <returns>The full path to the created temporary directory.</returns>
    protected string CreateTempSubDirectory(string? directoryName = null)
    {
        directoryName ??= $"subdir_{Guid.NewGuid():N}";
        var dirPath = Path.Combine(TempDirectory, directoryName);

        _ = Directory.CreateDirectory(dirPath);
        
        return dirPath;
    }
    
    /// <summary>
    /// Creates the test-specific temporary directory.
    /// </summary>
    /// <returns>The path to the created temporary directory.</returns>
    private string CreateTestTempDirectory()
    {
        var testName = GetType().Name;
        var tempDir = Path.Combine(Fixture.TempDirectory, $"{testName}_{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempDir);
        return tempDir;
    }
    
    #endregion
    
    #region Performance Testing Utilities
    
    /// <summary>
    /// Measures the execution time of an action.
    /// </summary>
    /// <param name="action">The action to measure.</param>
    /// <param name="iterations">The number of iterations to run.</param>
    /// <returns>The average execution time per iteration.</returns>
    protected TimeSpan MeasureExecutionTime(Action action, int iterations = 1)
    {
        _ = action.Should().NotBeNull();
        _ = iterations.Should().BePositive();
        
        // Warm-up
        if (iterations > 1)
        {
            action();
        }
        
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            action();
        }
        stopwatch.Stop();
        
        var averageTime = new TimeSpan(stopwatch.Elapsed.Ticks / iterations);
        Log($"Execution time: {averageTime.TotalMilliseconds:F2} ms (average of {iterations} iterations)");
        
        return averageTime;
    }
    
    /// <summary>
    /// Measures the execution time of an async action.
    /// </summary>
    /// <param name="action">The async action to measure.</param>
    /// <param name="iterations">The number of iterations to run.</param>
    /// <returns>The average execution time per iteration.</returns>
    protected async Task<TimeSpan> MeasureExecutionTimeAsync(Func<Task> action, int iterations = 1)
    {
        _ = action.Should().NotBeNull();
        _ = iterations.Should().BePositive();
        
        // Warm-up
        if (iterations > 1)
        {
            await action();
        }
        
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            await action();
        }
        stopwatch.Stop();
        
        var averageTime = new TimeSpan(stopwatch.Elapsed.Ticks / iterations);
        Log($"Async execution time: {averageTime.TotalMilliseconds:F2} ms (average of {iterations} iterations)");
        
        return averageTime;
    }
    
    /// <summary>
    /// Benchmarks an operation and asserts it completes within the specified time limit.
    /// </summary>
    /// <param name="action">The action to benchmark.</param>
    /// <param name="timeLimit">The time limit for the operation.</param>
    /// <param name="iterations">The number of iterations to run.</param>
    protected void BenchmarkOperation(Action action, TimeSpan timeLimit, int iterations = 1)
    {
        var averageTime = MeasureExecutionTime(action, iterations);
        _ = averageTime.Should().BeLessThanOrEqualTo(timeLimit,
            $"operation should complete within {timeLimit.TotalMilliseconds:F2} ms on average, but took {averageTime.TotalMilliseconds:F2} ms");
    }
    
    /// <summary>
    /// Benchmarks an async operation and asserts it completes within the specified time limit.
    /// </summary>
    /// <param name="action">The async action to benchmark.</param>
    /// <param name="timeLimit">The time limit for the operation.</param>
    /// <param name="iterations">The number of iterations to run.</param>
    protected async Task BenchmarkOperationAsync(Func<Task> action, TimeSpan timeLimit, int iterations = 1)
    {
        var averageTime = await MeasureExecutionTimeAsync(action, iterations);
        _ = averageTime.Should().BeLessThanOrEqualTo(timeLimit,
            $"async operation should complete within {timeLimit.TotalMilliseconds:F2} ms on average, but took {averageTime.TotalMilliseconds:F2} ms");
    }
    
    #endregion
    
    #region Memory Testing Utilities
    
    /// <summary>
    /// Measures the memory usage of an action.
    /// </summary>
    /// <param name="action">The action to measure.</param>
    /// <returns>The memory increase in bytes.</returns>
    protected long MeasureMemoryUsage(Action action)
    {
        _ = action.Should().NotBeNull();
        
        // Force garbage collection before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var initialMemory = GC.GetTotalMemory(false);
        action();
        
        // Force garbage collection after execution
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;
        
        Log($"Memory usage: {memoryIncrease:N0} bytes increase");
        
        return memoryIncrease;
    }
    
    /// <summary>
    /// Asserts that an operation does not exceed the specified memory increase.
    /// </summary>
    /// <param name="action">The action to test.</param>
    /// <param name="maxMemoryIncrease">The maximum allowed memory increase in bytes.</param>
    protected static void AssertMemoryUsage(Action action, long maxMemoryIncrease)
    {
        action.ShouldNotExceedMemoryIncrease(maxMemoryIncrease, 
            $"operation should not increase memory by more than {maxMemoryIncrease:N0} bytes");
    }
    
    #endregion
    
    #region Exception Testing Utilities
    
    /// <summary>
    /// Asserts that an action throws an exception of the specified type.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The action that should throw.</param>
    /// <param name="expectedMessage">Optional expected exception message.</param>
    /// <returns>The thrown exception for further assertions.</returns>
    protected TException AssertThrows<TException>(Action action, string? expectedMessage = null) 
        where TException : Exception
    {
        var exception = action.Should().Throw<TException>().Which;
        
        if (!string.IsNullOrEmpty(expectedMessage))
        {
            _ = exception.Message.Should().Contain(expectedMessage);
        }
        
        Log($"Expected exception thrown: {typeof(TException).Name}: {exception.Message}");
        
        return exception;
    }
    
    /// <summary>
    /// Asserts that an async action throws an exception of the specified type.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The async action that should throw.</param>
    /// <param name="expectedMessage">Optional expected exception message.</param>
    /// <returns>The thrown exception for further assertions.</returns>
    protected async Task<TException> AssertThrowsAsync<TException>(Func<Task> action, string? expectedMessage = null) 
        where TException : Exception
    {
        var exception = (await action.Should().ThrowAsync<TException>()).Which;
        
        if (!string.IsNullOrEmpty(expectedMessage))
        {
            _ = exception.Message.Should().Contain(expectedMessage);
        }
        
        Log($"Expected async exception thrown: {typeof(TException).Name}: {exception.Message}");
        
        return exception;
    }
    
    #endregion
    
    #region Cleanup
    
    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// Releases the unmanaged resources and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                LogTestCompletion();
                
                // Clean up test-specific temp directory
                try
                {
                    if (Directory.Exists(TempDirectory))
                    {
                        Directory.Delete(TempDirectory, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                    Log($"Warning: Could not clean up temp directory: {TempDirectory}");
                }
            }
            
            _disposed = true;
        }
    }
    
    /// <summary>
    /// Finalizer for TestBase.
    /// </summary>
    ~TestBase()
    {
        Dispose(false);
    }
    
    #endregion
}