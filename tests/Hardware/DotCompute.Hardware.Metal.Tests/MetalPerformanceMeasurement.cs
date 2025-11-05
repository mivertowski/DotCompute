// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests;

/// <summary>
/// Performance measurement helper for Metal tests.
/// Provides timing and throughput tracking capabilities.
/// </summary>
public sealed class MetalPerformanceMeasurement : IDisposable
{
    private readonly Stopwatch _stopwatch = new();
    private readonly string _operationName;
    private readonly ITestOutputHelper? _output;
    private bool _disposed;

    /// <summary>
    /// Elapsed time in milliseconds
    /// </summary>
    public double ElapsedMilliseconds => _stopwatch.Elapsed.TotalMilliseconds;

    /// <summary>
    /// Total bytes processed during the operation
    /// </summary>
    public long BytesProcessed { get; set; }

    /// <summary>
    /// Throughput in MB/s
    /// </summary>
    public double ThroughputMBps => BytesProcessed > 0 && ElapsedMilliseconds > 0
        ? BytesProcessed / (ElapsedMilliseconds / 1000.0) / (1024 * 1024)
        : 0;

    /// <summary>
    /// Elapsed time as TimeSpan
    /// </summary>
    public TimeSpan ElapsedTime => _stopwatch.Elapsed;

    /// <summary>
    /// Initializes a new instance of the MetalPerformanceMeasurement class
    /// </summary>
    /// <param name="operationName">Name of the operation being measured</param>
    /// <param name="output">Optional test output helper for logging</param>
    public MetalPerformanceMeasurement(string operationName, ITestOutputHelper? output = null)
    {
        _operationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
        _output = output;
    }

    /// <summary>
    /// Starts the performance measurement
    /// </summary>
    public void Start() => _stopwatch.Restart();

    /// <summary>
    /// Stops the performance measurement
    /// </summary>
    public void Stop() => _stopwatch.Stop();

    /// <summary>
    /// Logs the measurement results if an output helper was provided
    /// </summary>
    public void LogResults(long dataSize = 0, int operationCount = 1)
    {
        if (_output == null)
            return;

        var avgTime = _stopwatch.Elapsed.TotalMilliseconds / operationCount;

        _output.WriteLine($"{_operationName} Performance:");
        _output.WriteLine($"  Total Time: {_stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Average Time: {avgTime:F2} ms");

        if (dataSize > 0)
        {
            var throughputGBps = dataSize / (_stopwatch.Elapsed.TotalSeconds * 1024 * 1024 * 1024);
            _output.WriteLine($"  Throughput: {throughputGBps:F2} GB/s");
        }
    }

    /// <summary>
    /// Measures the execution time of an action
    /// </summary>
    /// <param name="action">Action to measure</param>
    /// <param name="bytesProcessed">Optional number of bytes processed</param>
    /// <returns>Performance measurement result</returns>
    public static MetalPerformanceMeasurement Measure(Action action, long bytesProcessed = 0)
    {
        var measurement = new MetalPerformanceMeasurement("Action");
        measurement.Start();
        action();
        measurement.Stop();
        measurement.BytesProcessed = bytesProcessed;
        return measurement;
    }

    /// <summary>
    /// Disposes of the stopwatch resources
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _stopwatch.Stop();
            _disposed = true;
        }
    }
}
