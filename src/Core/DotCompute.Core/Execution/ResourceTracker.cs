// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Logging;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Execution;

/// <summary>
/// Tracks resource usage across multiple devices during execution.
/// </summary>
public partial class ResourceTracker : IAsyncDisposable
{
    #region LoggerMessage Delegates

    [LoggerMessage(EventId = 10701, Level = MsLogLevel.Trace, Message = "Started resource tracking for {DeviceCount} devices")]
    private static partial void LogResourceTrackingStarted(ILogger logger, int deviceCount);

    [LoggerMessage(EventId = 10702, Level = MsLogLevel.Trace, Message = "Ended resource tracking, total execution time: {MaxExecutionTimeMs:F2}ms")]
    private static partial void LogResourceTrackingEnded(ILogger logger, double maxExecutionTimeMs);

    #endregion

    private readonly ILogger _logger;
    private readonly Dictionary<string, DeviceResourceUsage> _deviceUsage;

    /// <summary>
    /// Initializes a new instance of the ResourceTracker class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ResourceTracker(ILogger logger)
    {
        _logger = logger;
        _deviceUsage = [];
    }
    /// <summary>
    /// Gets track execution start asynchronously.
    /// </summary>
    /// <param name="devices">The devices.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
#pragma warning disable CA1822 // Mark members as static - uses instance fields not shown in snippet
    public async ValueTask TrackExecutionStartAsync(IAccelerator[] devices, CancellationToken cancellationToken)
#pragma warning restore CA1822
    {
        foreach (var device in devices)
        {
            _deviceUsage[device.Info.Id] = new DeviceResourceUsage
            {
                DeviceId = device.Info.Id,
                StartTime = DateTimeOffset.UtcNow,
                InitialMemoryUsage = device.Info.TotalMemory - device.Info.AvailableMemory
            };
        }

        LogResourceTrackingStarted(_logger, devices.Length);
        await ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets track execution end asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
#pragma warning disable CA1822 // Mark members as static - uses instance fields not shown in snippet
    public async ValueTask TrackExecutionEndAsync(CancellationToken cancellationToken = default)
#pragma warning restore CA1822
    {
        var endTime = DateTimeOffset.UtcNow;

        foreach (var usage in _deviceUsage.Values)
        {
            usage.EndTime = endTime;
            usage.TotalExecutionTime = endTime - usage.StartTime;
        }

        LogResourceTrackingEnded(_logger, _deviceUsage.Values.Max(u => u.TotalExecutionTime.TotalMilliseconds));

        await ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets the resource usage as a defensive copy.
    /// </summary>
#pragma warning disable CA1822 // Mark members as static - uses instance fields not shown in snippet
    public Dictionary<string, DeviceResourceUsage> ResourceUsage => new(_deviceUsage);
#pragma warning restore CA1822
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    private bool _disposed;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _deviceUsage.Clear();
        GC.SuppressFinalize(this);
        _disposed = true;
        await ValueTask.CompletedTask;
    }
}
