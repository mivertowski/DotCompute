// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Diagnostics;
using Xunit.Abstractions;

namespace DotCompute.SharedTestUtilities.Performance;

/// <summary>
/// Adapter for PerformanceMeasurement that provides backward compatibility
/// with tests that use simpler constructor overloads.
/// </summary>
public static class PerformanceMeasurementAdapter
{
    /// <summary>
    /// Creates a PerformanceMeasurement with XUnit output support.
    /// </summary>
    public static PerformanceMeasurement Create(string operationName, ITestOutputHelper output)
    {
        // Create without logger - it will use internal console logger
        return new PerformanceMeasurement(operationName, trackMemory: false);
    }

    /// <summary>
    /// Creates a PerformanceMeasurement with optional memory tracking.
    /// </summary>
    public static PerformanceMeasurement Create(string operationName, bool trackMemory = true)
    {
        return new PerformanceMeasurement(operationName, trackMemory);
    }
}

/// <summary>
/// Extension methods for simplified PerformanceMeasurement usage in tests.
/// </summary>
public static class PerformanceMeasurementExtensions
{

    /// <summary>
    /// Simple performance measurement helper that works with existing test code.
    /// </summary>
    public sealed class SimplePerformanceMeasurement : IDisposable
    {
        private readonly string _operationName;
        private readonly ITestOutputHelper? _output;
        private readonly Stopwatch _stopwatch;
        private bool _stopped;

        public TimeSpan Duration => _stopwatch.Elapsed;
        public TimeSpan ElapsedTime => _stopwatch.Elapsed;
        public TimeSpan Elapsed => _stopwatch.Elapsed;

        public SimplePerformanceMeasurement(string operationName, ITestOutputHelper? output = null)
        {
            _operationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
            _output = output;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Start()
        {
            if (!_stopwatch.IsRunning)
                _stopwatch.Start();
        }

        public PerformanceResult Stop()
        {
            if (!_stopped)
            {
                _stopwatch.Stop();
                _stopped = true;
            }

            return new PerformanceResult
            {
                Duration = _stopwatch.Elapsed,
                OperationName = _operationName,
                Checkpoints = Array.Empty<Checkpoint>(),
                Timestamp = DateTime.UtcNow
            };
        }

        public void LogResults(long dataSize = 0)
        {
            if (_output != null)
            {
                var elapsed = _stopwatch.Elapsed;
                _output.WriteLine($"{_operationName}: {elapsed.TotalMilliseconds:F2}ms");

                if (dataSize > 0)
                {
                    var throughput = dataSize / elapsed.TotalSeconds;
                    var gbps = throughput / (1024.0 * 1024.0 * 1024.0);
                    _output.WriteLine($"  Throughput: {gbps:F2} GB/s");
                }
            }
        }

        public void Dispose()
        {
            if (!_stopped)
            {
                Stop();
            }
        }
    }
}