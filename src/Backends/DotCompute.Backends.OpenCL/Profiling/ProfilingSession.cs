// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;

namespace DotCompute.Backends.OpenCL.Profiling;

/// <summary>
/// Represents a profiling session that collects and aggregates profiled events.
/// </summary>
/// <remarks>
/// A profiling session is a logical grouping of profiled events that occur during
/// a specific time period or workflow. Sessions can be used to profile:
/// - Entire application runs
/// - Specific algorithm executions
/// - Batch processing operations
/// - Performance regression testing
///
/// Sessions are thread-safe and support concurrent event recording.
/// </remarks>
public sealed class ProfilingSession
{
    private readonly ConcurrentBag<ProfiledEvent> _events;
    private ProfilingStatistics? _cachedStatistics;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfilingSession"/> class.
    /// </summary>
    /// <param name="sessionId">Unique identifier for the session.</param>
    /// <param name="name">Descriptive name for the session.</param>
    /// <param name="startTime">Start time of the session.</param>
    internal ProfilingSession(Guid sessionId, string name, DateTime startTime)
    {
        ArgumentNullException.ThrowIfNull(name);

        SessionId = sessionId;
        Name = name;
        StartTime = startTime;
        _events = new ConcurrentBag<ProfiledEvent>();
    }

    /// <summary>
    /// Gets the unique identifier for this profiling session.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Gets the descriptive name of the profiling session.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the start time of the profiling session (UTC).
    /// </summary>
    public DateTime StartTime { get; }

    /// <summary>
    /// Gets the end time of the profiling session (UTC).
    /// Null if the session has not been finalized.
    /// </summary>
    public DateTime? EndTime { get; private set; }

    /// <summary>
    /// Gets whether the session has been finalized.
    /// </summary>
    public bool IsFinalized => EndTime.HasValue;

    /// <summary>
    /// Gets the collection of profiled events recorded in this session.
    /// </summary>
    public IReadOnlyCollection<ProfiledEvent> Events => _events.ToArray();

    /// <summary>
    /// Records a profiled event to this session.
    /// </summary>
    /// <param name="evt">The profiled event to record.</param>
    /// <exception cref="ArgumentNullException">Thrown if evt is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if session is already finalized.</exception>
    public void RecordEvent(ProfiledEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        if (IsFinalized)
        {
            throw new InvalidOperationException(
                "Cannot record events to a finalized session. Create a new session instead.");
        }

        _events.Add(evt);
        _cachedStatistics = null; // Invalidate cache
    }

    /// <summary>
    /// Completes the session, setting the end time and preventing further event recording.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if session is already finalized.</exception>
    public void Complete()
    {
        if (IsFinalized)
        {
            throw new InvalidOperationException("Session is already finalized.");
        }

        EndTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets comprehensive statistics for all events in this session.
    /// Results are cached after first calculation until new events are recorded.
    /// </summary>
    /// <returns>Aggregated statistics for the session.</returns>
    public ProfilingStatistics GetStatistics()
    {
        // Return cached statistics if available
        if (_cachedStatistics != null)
        {
            return _cachedStatistics;
        }

        var events = _events.ToArray();
        var operationStats = new Dictionary<ProfiledOperation, OperationStatistics>();

        // Group events by operation type
        var groupedEvents = events.GroupBy(e => e.Operation);

        foreach (var group in groupedEvents)
        {
            var stats = CalculateStatistics(group);
            operationStats[group.Key] = stats;
        }

        var totalSessionTime = EndTime.HasValue
            ? EndTime.Value - StartTime
            : TimeSpan.Zero;

        _cachedStatistics = new ProfilingStatistics
        {
            OperationStats = operationStats,
            TotalSessionTime = totalSessionTime,
            TotalEvents = events.Length
        };

        return _cachedStatistics;
    }

    /// <summary>
    /// Calculates statistical metrics for a collection of profiled events.
    /// </summary>
    /// <param name="events">The events to analyze.</param>
    /// <returns>Operation statistics including min/max/avg/percentiles.</returns>
    private static OperationStatistics CalculateStatistics(IEnumerable<ProfiledEvent> events)
    {
        var durations = events
            .Select(e => e.DurationMilliseconds)
            .OrderBy(d => d)
            .ToArray();

        if (durations.Length == 0)
        {
            return new OperationStatistics
            {
                Count = 0,
                MinDurationMs = 0,
                MaxDurationMs = 0,
                AvgDurationMs = 0,
                MedianDurationMs = 0,
                P95DurationMs = 0,
                P99DurationMs = 0,
                TotalDurationMs = 0
            };
        }

        var count = durations.Length;
        var min = durations[0];
        var max = durations[^1];
        var avg = durations.Average();
        var total = durations.Sum();

        // Calculate median
        var median = count % 2 == 0
            ? (durations[count / 2 - 1] + durations[count / 2]) / 2.0
            : durations[count / 2];

        // Calculate percentiles
        var p95 = CalculatePercentile(durations, 0.95);
        var p99 = CalculatePercentile(durations, 0.99);

        return new OperationStatistics
        {
            Count = count,
            MinDurationMs = min,
            MaxDurationMs = max,
            AvgDurationMs = avg,
            MedianDurationMs = median,
            P95DurationMs = p95,
            P99DurationMs = p99,
            TotalDurationMs = total
        };
    }

    /// <summary>
    /// Calculates the specified percentile from a sorted array of durations.
    /// </summary>
    /// <param name="sortedDurations">Sorted array of durations.</param>
    /// <param name="percentile">Percentile to calculate (0.0 to 1.0).</param>
    /// <returns>The duration at the specified percentile.</returns>
    private static double CalculatePercentile(double[] sortedDurations, double percentile)
    {
        if (sortedDurations.Length == 0)
        {
            return 0;
        }

        var index = percentile * (sortedDurations.Length - 1);
        var lowerIndex = (int)Math.Floor(index);
        var upperIndex = (int)Math.Ceiling(index);

        if (lowerIndex == upperIndex)
        {
            return sortedDurations[lowerIndex];
        }

        // Linear interpolation between adjacent values
        var lowerValue = sortedDurations[lowerIndex];
        var upperValue = sortedDurations[upperIndex];
        var fraction = index - lowerIndex;

        return lowerValue + (upperValue - lowerValue) * fraction;
    }

    /// <summary>
    /// Returns a string representation of this profiling session.
    /// </summary>
    public override string ToString()
    {
        var duration = EndTime.HasValue
            ? (EndTime.Value - StartTime).TotalSeconds
            : (DateTime.UtcNow - StartTime).TotalSeconds;

        return $"Session '{Name}' (ID: {SessionId}): {_events.Count} events, " +
               $"{duration:F2}s duration, {(IsFinalized ? "finalized" : "active")}";
    }
}
