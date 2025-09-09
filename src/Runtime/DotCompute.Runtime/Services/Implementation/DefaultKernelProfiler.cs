// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using DotCompute.Runtime.Logging;
using DotCompute.Runtime.Services.Interfaces;
using DotCompute.Runtime.Services.Performance.Results;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Services.Implementation;

/// <summary>
/// Default implementation of kernel profiler with in-memory storage and basic export capabilities.
/// </summary>
public class DefaultKernelProfiler : IKernelProfiler, IDisposable
{
    private readonly ILogger<DefaultKernelProfiler> _logger;
    private readonly ConcurrentDictionary<Guid, ProfilingSession> _activeSessions;
    private readonly ConcurrentDictionary<Guid, ProfilingResults> _completedSessions;
    private readonly ConcurrentDictionary<string, List<ProfilingResults>> _kernelHistory;
    private readonly ReaderWriterLockSlim _lock;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the DefaultKernelProfiler class.
    /// </summary>
    public DefaultKernelProfiler(ILogger<DefaultKernelProfiler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activeSessions = new ConcurrentDictionary<Guid, ProfilingSession>();
        _completedSessions = new ConcurrentDictionary<Guid, ProfilingResults>();
        _kernelHistory = new ConcurrentDictionary<string, List<ProfilingResults>>();
        _lock = new ReaderWriterLockSlim();
    }

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
            sessions = sessions.Where(s => s.SessionId != Guid.Empty).ToList();
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

    internal void CompleteSession(ProfilingSession session, ProfilingResults results)
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
        if (sessionName.StartsWith("KernelExecution_"))
        {
            var parts = sessionName.Split('_');
            if (parts.Length >= 2)
            {
                return parts[1];
            }
        }
        return sessionName;
    }

    private static TimeSpan CalculatePercentile(List<TimeSpan> values, double percentile)
    {
        if (values.Count == 0) return TimeSpan.Zero;
        
        var sorted = values.OrderBy(t => t).ToList();
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }

    private static TimeSpan CalculateStandardDeviation(List<TimeSpan> values)
    {
        if (values.Count == 0) return TimeSpan.Zero;
        
        var mean = values.Average(t => t.TotalMilliseconds);
        var variance = values.Average(t => Math.Pow(t.TotalMilliseconds - mean, 2));
        return TimeSpan.FromMilliseconds(Math.Sqrt(variance));
    }

    private static async Task ExportToCsvAsync(List<ProfilingResults> sessions, string outputPath)
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
        if (_disposed) return;

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

        public string SessionId { get; }
        public string OperationName { get; }
        public DateTime StartTime { get; }
        
        public Guid InternalSessionId => _internalId;

        public ProfilingSession(string name, DefaultKernelProfiler profiler)
        {
            _internalId = Guid.NewGuid();
            SessionId = _internalId.ToString();
            OperationName = name;
            StartTime = DateTime.UtcNow;
            _profiler = profiler;
            _stopwatch = Stopwatch.StartNew();
            _metrics = new Dictionary<string, double>();
            _tags = new Dictionary<string, string>();
            _checkpoints = new List<TimingCheckpoint>();
            _memorySnapshots = new List<MemorySnapshot>();
            _context = new Dictionary<string, object>();
            _lastCheckpointTime = DateTime.UtcNow;
        }

        public void RecordMetric(string metricName, double value)
        {
            _metrics[metricName] = value;
        }

        public void AddTag(string key, string value)
        {
            _tags[key] = value;
        }
        
        public SessionMetrics GetMetrics()
        {
            return new SessionMetrics
            {
                Duration = _stopwatch.Elapsed,
                CustomMetrics = new Dictionary<string, double>(_metrics),
                Tags = new Dictionary<string, string>(_tags)
            };
        }
        
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
                Duration = _stopwatch.Elapsed,
                Metrics = new Dictionary<string, double>(_metrics),
                Tags = new Dictionary<string, string>(_tags)
            };
        }
        
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

        public void AddContext(string key, object value)
        {
            _context[key] = value;
        }

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
                Checkpoints = new List<TimingCheckpoint>(_checkpoints),
                MemorySnapshots = new List<MemorySnapshot>(_memorySnapshots),
                Context = new Dictionary<string, object>(_context)
            };
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            var results = GetCurrentResults();
            _profiler.CompleteSession(this, results);
        }
    }
}