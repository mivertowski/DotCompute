// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Execution;

/// <summary>
/// Tracks resource usage across multiple devices during execution.
/// </summary>
public class ResourceTracker : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, DeviceResourceUsage> _deviceUsage;
    private bool _disposed;

    public ResourceTracker(ILogger logger)
    {
        _logger = logger;
        _deviceUsage = [];
    }

    public async ValueTask TrackExecutionStartAsync(IAccelerator[] devices, CancellationToken cancellationToken)
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

        _logger.LogTrace("Started resource tracking for {DeviceCount} devices", devices.Length);
        await ValueTask.CompletedTask;
    }

    public async ValueTask TrackExecutionEndAsync(CancellationToken cancellationToken = default)
    {
        var endTime = DateTimeOffset.UtcNow;

        foreach (var usage in _deviceUsage.Values)
        {
            usage.EndTime = endTime;
            usage.TotalExecutionTime = endTime - usage.StartTime;
        }

        _logger.LogTrace("Ended resource tracking, total execution time: {MaxExecutionTime:F2}ms",
            _deviceUsage.Values.Max(u => u.TotalExecutionTime.TotalMilliseconds));

        await ValueTask.CompletedTask;
    }

    public Dictionary<string, DeviceResourceUsage> GetResourceUsage() => new(_deviceUsage);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _deviceUsage.Clear();
        _disposed = true;
        await ValueTask.CompletedTask;
    }
}
