// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using DotCompute.Runtime.Logging;
using DotCompute.Runtime.Services.Interfaces;
using DotCompute.Runtime.Services.Performance.Results;
using Microsoft.Extensions.Logging;
using System;

namespace DotCompute.Runtime.Services.Implementation;

/// <summary>
/// Default implementation of kernel profiler with in-memory storage and basic export capabilities.
/// </summary>
/// <remarks>
/// Initializes a new instance of the DefaultKernelProfiler class.
/// </remarks>
public class DefaultKernelProfiler(ILogger<DefaultKernelProfiler> logger) : IKernelProfiler, IDisposable
{
    private readonly ILogger<DefaultKernelProfiler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<Guid, ProfilingSession> _activeSessions = new();
    private readonly ConcurrentDictionary<Guid, ProfilingResults> _completedSessions = new();
    private readonly ConcurrentDictionary<string, List<ProfilingResults>> _kernelHistory = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private bool _disposed;

    /// <inheritdoc />
    public IProfilingSession StartProfiling(string sessionName)
    {
        var session = new ProfilingSession(sessionName, this);
        _activeSessions[session.InternalSessionId] = session;


        _logger.ProfilingSessionStarted(session.InternalSessionId, sessionName);


        return session;
    }

    /// <inheritdoc />
    public Task<ProfilingResults?> GetResultsAsync(Guid sessionId)
    {
        if (_completedSessions.TryGetValue(sessionId, out var results))
        {
            return Task.FromResult<ProfilingResults?>(results);
        }

        if (_activeSessions.TryGetValue(sessionId, out var activeSession))
        {
            // Session is still active, return partial results
            return Task.FromResult<ProfilingResults?>(activeSession.GetCurrentResults());
        }

        return Task.FromResult<ProfilingResults?>(null);
    }

    /// <inheritdoc />
    public Task<KernelStatistics> GetKernelStatisticsAsync(string kernelName, TimeRange? timeRange = null)
    {
        _lock.EnterReadLock();
        try
        {
            if (!_kernelHistory.TryGetValue(kernelName, out var history))
            {
                return Task.FromResult(new KernelStatistics
                {

                    KernelName = kernelName,
                    ExecutionCount = 0
                });
            }

            var relevantResults = timeRange != null
                ? history.Where(r => r.SessionId != Guid.Empty) // Filter by time if needed
                : history;

            var executionTimes = relevantResults
                .Select(r => r.KernelExecutionTime)
                .Where(t => t > TimeSpan.Zero)
                .ToList();

            if (executionTimes.Count == 0)
            {
                return Task.FromResult(new KernelStatistics
                {

                    KernelName = kernelName,
                    ExecutionCount = 0
                });
            }

            var stats = new KernelStatistics
            {
                KernelName = kernelName,
                ExecutionCount = executionTimes.Count,
                AverageExecutionTime = TimeSpan.FromMilliseconds(executionTimes.Average(t => t.TotalMilliseconds)),
                MinExecutionTime = executionTimes.Min(),
                MaxExecutionTime = executionTimes.Max(),
                P95ExecutionTime = CalculatePercentile(executionTimes, 0.95),
                P99ExecutionTime = CalculatePercentile(executionTimes, 0.99),
                StandardDeviation = CalculateStandardDeviation(executionTimes),
                AverageMemoryBytes = (long)relevantResults.Average(r => r.PeakMemoryBytes),
                SuccessRate = 1.0, // TODO: Track failures
                MostCommonAccelerator = relevantResults
                    .SelectMany(r => r.Context.Values.OfType<string>())
                    .GroupBy(s => s)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key
            };

            return Task.FromResult(stats);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public async Task<string> ExportDataAsync(ExportFormat format, string outputPath, TimeRange? timeRange = null)
    {
        var sessions = _completedSessions.Values.ToList();


        if (timeRange != null)
        {
            // Filter by time range if needed
            sessions = [.. sessions.Where(s => s.SessionId != Guid.Empty)];
        }

        switch (format)
        {
            case ExportFormat.Json:
                var json = JsonSerializer.Serialize(sessions, new JsonSerializerOptions
                {

                    WriteIndented = true

                });
                await File.WriteAllTextAsync(outputPath, json);
                break;


            case ExportFormat.Csv:
                await ExportToCsvAsync(sessions, outputPath);
                break;


            default:
                throw new NotSupportedException($"Export format {format} is not yet supported");
        }

        _logger.ProfilingDataExported(sessions.Count, outputPath);


        return outputPath;
    }

    /// <inheritdoc />
    public Task<int> CleanupOldDataAsync(TimeSpan olderThan)
    {
        var cutoffTime = DateTime.UtcNow - olderThan;
        var removedCount = 0;

        var oldSessions = _completedSessions
            .Where(kvp => kvp.Value.SessionId != Guid.Empty) // Placeholder for time check
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var sessionId in oldSessions)
        {
            if (_completedSessions.TryRemove(sessionId, out _))
            {
                removedCount++;
            }
        }

        _logger.ProfilingDataCleaned(removedCount);
        return Task.FromResult(removedCount);
    }

    private void CompleteSession(ProfilingSession session, ProfilingResults results)
    {
        if (_activeSessions.TryRemove(session.InternalSessionId, out _))
        {
            _completedSessions[session.InternalSessionId] = results;

            // Add to kernel history

            var kernelName = ExtractKernelName(results.SessionName);
            if (!string.IsNullOrEmpty(kernelName))
            {
                _ = _kernelHistory.AddOrUpdate(
                    kernelName,
                    _ => [results],
                    (_, list) =>
                    {
                        list.Add(results);
                        return list;
                    });
            }


            _logger.ProfilingSessionCompleted(session.InternalSessionId);
        }
    }

    private static string ExtractKernelName(string sessionName)
    {
        // Extract kernel name from session name pattern: "KernelExecution_{kernelName}_{backend}"
        if (sessionName.StartsWith("KernelExecution_", StringComparison.OrdinalIgnoreCase))
        {
            var parts = sessionName.Split('_');
            if (parts.Length >= 2)
            {
                return parts[1];
            }
        }
        return sessionName;
    }

    private static TimeSpan CalculatePercentile(IReadOnlyList<TimeSpan> values, double percentile)
    {
        if (values.Count == 0)
        {
            return TimeSpan.Zero;
        }


        var sorted = values.OrderBy(t => t).ToList();
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }

    private static TimeSpan CalculateStandardDeviation(IReadOnlyList<TimeSpan> values)
    {
        if (values.Count == 0)
        {
            return TimeSpan.Zero;
        }


        var mean = values.Average(t => t.TotalMilliseconds);
        var variance = values.Average(t => Math.Pow(t.TotalMilliseconds - mean, 2));
        return TimeSpan.FromMilliseconds(Math.Sqrt(variance));
    }

    private static async Task ExportToCsvAsync(IReadOnlyList<ProfilingResults> sessions, string outputPath)
    {
        using var writer = new StreamWriter(outputPath);
        await writer.WriteLineAsync("SessionId,SessionName,TotalExecutionTime,CompilationTime,KernelExecutionTime,MemoryTransferTime,PeakMemoryBytes");


        foreach (var session in sessions)
        {
            await writer.WriteLineAsync(
                $"{session.SessionId},{session.SessionName},{session.TotalExecutionTime.TotalMilliseconds}," +
                $"{session.CompilationTime.TotalMilliseconds},{session.KernelExecutionTime.TotalMilliseconds}," +
                $"{session.MemoryTransferTime.TotalMilliseconds},{session.PeakMemoryBytes}");
        }
    }

    /// <summary>
    /// Disposes of the profiler resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of the profiler resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }


        if (disposing)
        {
            _lock?.Dispose();
            _activeSessions.Clear();
            _completedSessions.Clear();
            _kernelHistory.Clear();
        }

        _disposed = true;
    }

    private class ProfilingSession : IProfilingSession
    {
        private readonly DefaultKernelProfiler _profiler;
        private readonly Stopwatch _stopwatch;
        private readonly Dictionary<string, double> _metrics;
        private readonly Dictionary<string, string> _tags;
        private readonly List<TimingCheckpoint> _checkpoints;
        private readonly List<MemorySnapshot> _memorySnapshots;
        private readonly Dictionary<string, object> _context;
        private DateTime _lastCheckpointTime;
        private readonly Guid _internalId;
        /// <summary>
        /// Gets or sets the session identifier.
        /// </summary>
        /// <value>The session id.</value>

        public string SessionId { get; }
        /// <summary>
        /// Gets or sets the operation name.
        /// </summary>
        /// <value>The operation name.</value>
        public string OperationName { get; }
        /// <summary>
        /// Gets or sets the start time.
        /// </summary>
        /// <value>The start time.</value>
        public DateTime StartTime { get; }
        /// <summary>
        /// Gets or sets the internal session identifier.
        /// </summary>
        /// <value>The internal session id.</value>


        public Guid InternalSessionId => _internalId;
        /// <summary>
        /// Initializes a new instance of the ProfilingSession class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="profiler">The profiler.</param>

        public ProfilingSession(string name, DefaultKernelProfiler profiler)
        {
            _internalId = Guid.NewGuid();
            SessionId = _internalId.ToString();
            OperationName = name;
            StartTime = DateTime.UtcNow;
            _profiler = profiler;
            _stopwatch = Stopwatch.StartNew();
            _metrics = [];
            _tags = [];
            _checkpoints = [];
            _memorySnapshots = [];
            _context = [];
            _lastCheckpointTime = DateTime.UtcNow;
        }
        /// <summary>
        /// Performs record metric.
        /// </summary>
        /// <param name="metricName">The metric name.</param>
        /// <param name="value">The value.</param>

        public void RecordMetric(string metricName, double value) => _metrics[metricName] = value;
        /// <summary>
        /// Performs add tag.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>

        public void AddTag(string key, string value) => _tags[key] = value;
        /// <summary>
        /// Gets the metrics.
        /// </summary>
        /// <returns>The metrics.</returns>


        public SessionMetrics GetMetrics()
        {
            return new SessionMetrics
            {
                ElapsedTime = _stopwatch.Elapsed,
                Metrics = new Dictionary<string, double>(_metrics),
                Tags = new Dictionary<string, string>(_tags)
            };
        }
        /// <summary>
        /// Gets end.
        /// </summary>
        /// <returns>The result of the operation.</returns>


        public ProfilingSessionResult End()
        {
            _stopwatch.Stop();
            var results = GetCurrentResults();
            _profiler.CompleteSession(this, results);


            return new ProfilingSessionResult
            {
                SessionId = SessionId,
                OperationName = OperationName,
                StartTime = StartTime,
                EndTime = DateTime.UtcNow,
                TotalTime = _stopwatch.Elapsed,
                Metrics = new SessionMetrics
                {
                    ElapsedTime = _stopwatch.Elapsed,
                    Metrics = new Dictionary<string, double>(_metrics),
                    Tags = new Dictionary<string, string>(_tags)
                }
            };
        }
        /// <summary>
        /// Performs record checkpoint.
        /// </summary>
        /// <param name="checkpointName">The checkpoint name.</param>

        // Additional methods for extended functionality

        public void RecordCheckpoint(string checkpointName)
        {
            var now = DateTime.UtcNow;
            var checkpoint = new TimingCheckpoint
            {
                Name = checkpointName,
                ElapsedTime = _stopwatch.Elapsed,
                DeltaTime = now - _lastCheckpointTime
            };
            _checkpoints.Add(checkpoint);
            _lastCheckpointTime = now;
        }
        /// <summary>
        /// Performs record memory usage.
        /// </summary>
        /// <param name="label">The label.</param>

        public void RecordMemoryUsage(string? label = null)
        {
            var snapshot = new MemorySnapshot
            {
                Label = label,
                Timestamp = _stopwatch.Elapsed,
                ManagedMemoryBytes = GC.GetTotalMemory(false),
                DeviceMemoryBytes = 0, // Would need backend-specific implementation
                PinnedMemoryBytes = 0  // Would need runtime tracking
            };
            _memorySnapshots.Add(snapshot);
        }
        /// <summary>
        /// Performs add context.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>

        public void AddContext(string key, object value) => _context[key] = value;
        /// <summary>
        /// Gets the current results.
        /// </summary>
        /// <returns>The current results.</returns>

        public ProfilingResults GetCurrentResults()
        {
            return new ProfilingResults
            {
                SessionId = _internalId,
                SessionName = OperationName,
                TotalExecutionTime = _stopwatch.Elapsed,
                CompilationTime = TimeSpan.FromMilliseconds(_metrics.GetValueOrDefault("CompilationTime", 0)),
                KernelExecutionTime = TimeSpan.FromMilliseconds(_metrics.GetValueOrDefault("KernelExecutionTime", 0)),
                MemoryTransferTime = TimeSpan.FromMilliseconds(_metrics.GetValueOrDefault("MemoryTransferTime", 0)),
                PeakMemoryBytes = _memorySnapshots.Count > 0

                    ? _memorySnapshots.Max(s => s.ManagedMemoryBytes)

                    : 0,
                CustomMetrics = new Dictionary<string, double>(_metrics),
                Checkpoints = [.. _checkpoints],
                MemorySnapshots = [.. _memorySnapshots],
                Context = new Dictionary<string, object>(_context)
            };
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            _stopwatch.Stop();
            var results = GetCurrentResults();
            _profiler.CompleteSession(this, results);
        }
    }
}