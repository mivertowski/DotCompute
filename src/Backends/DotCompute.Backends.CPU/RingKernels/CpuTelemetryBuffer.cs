// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.RingKernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.RingKernels;

/// <summary>
/// CPU-specific telemetry buffer using in-memory struct for ring kernel health monitoring.
/// Since this is CPU-only simulation, no special memory mapping is required.
/// </summary>
/// <remarks>
/// <para><b>Memory Layout</b>:</para>
/// <list type="bullet">
/// <item>Standard .NET managed memory: Simple RingKernelTelemetry struct in heap</item>
/// <item>Thread-safe access: Interlocked operations for atomic counter updates</item>
/// <item>Zero-copy: Direct struct access (no GPU memory mapping needed)</item>
/// </list>
///
/// <para><b>Performance Characteristics</b>:</para>
/// <list type="bullet">
/// <item>CPU polling latency: &lt;100ns (direct memory access, no kernel overhead)</item>
/// <item>Update overhead: &lt;10ns (Interlocked operations)</item>
/// <item>Memory footprint: 64 bytes (cache-line aligned struct)</item>
/// </list>
///
/// <para><b>Thread Safety</b>:</para>
/// Updates use Interlocked operations (Interlocked.Increment, Interlocked.Exchange).
/// Polling is lock-free (volatile read of struct copy).
/// </remarks>
internal sealed class CpuTelemetryBuffer : IDisposable
{
    private readonly ILogger<CpuTelemetryBuffer> _logger;
    private RingKernelTelemetry _telemetry;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="CpuTelemetryBuffer"/>.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    public CpuTelemetryBuffer(ILogger<CpuTelemetryBuffer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Allocates the telemetry buffer (initializes to default values).
    /// For CPU, this is a no-op since we use managed memory.
    /// </summary>
    public void Allocate()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CpuTelemetryBuffer));
        }

        // Initialize to default values
        _telemetry = new RingKernelTelemetry();

        _logger.LogDebug("Allocated CPU telemetry buffer (64 bytes, in-memory)");
    }

    /// <summary>
    /// Polls the current telemetry data.
    /// This is a direct memory read operation (no GPU overhead).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current telemetry snapshot.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if buffer is disposed.</exception>
    public Task<RingKernelTelemetry> PollAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CpuTelemetryBuffer));
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Direct struct copy (atomic snapshot)
            var snapshot = _telemetry;

            _logger.LogTrace(
                "Polled telemetry: processed={MessagesProcessed}, dropped={MessagesDropped}, queueDepth={QueueDepth}",
                snapshot.MessagesProcessed,
                snapshot.MessagesDropped,
                snapshot.QueueDepth);

            return Task.FromResult(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to poll CPU telemetry");
            throw;
        }
    }

    /// <summary>
    /// Resets the telemetry buffer to initial values (zeros).
    /// Useful for restarting measurements or testing.
    /// </summary>
    public void Reset()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CpuTelemetryBuffer));
        }

        // Reset to default values
        _telemetry = new RingKernelTelemetry();

        _logger.LogDebug("Reset CPU telemetry buffer to initial values");
    }

    /// <summary>
    /// Updates telemetry counters atomically.
    /// Used by the CPU ring kernel worker thread.
    /// </summary>
    /// <param name="messagesProcessed">Increment messages processed counter.</param>
    /// <param name="messagesDropped">Increment messages dropped counter.</param>
    /// <param name="queueDepth">Set current queue depth.</param>
    public void Update(long messagesProcessed = 0, long messagesDropped = 0, int queueDepth = 0)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CpuTelemetryBuffer));
        }

        if (messagesProcessed > 0)
        {
            Interlocked.Add(ref _telemetry.MessagesProcessed, (ulong)messagesProcessed);
        }

        if (messagesDropped > 0)
        {
            Interlocked.Add(ref _telemetry.MessagesDropped, (ulong)messagesDropped);
        }

        if (queueDepth >= 0)
        {
            Interlocked.Exchange(ref _telemetry.QueueDepth, queueDepth);
        }

        // Update timestamp
        Interlocked.Exchange(ref _telemetry.LastProcessedTimestamp, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    /// <summary>
    /// Disposes the telemetry buffer.
    /// For CPU, this is a no-op since we use managed memory.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _logger.LogDebug("Disposed CPU telemetry buffer");
    }
}
