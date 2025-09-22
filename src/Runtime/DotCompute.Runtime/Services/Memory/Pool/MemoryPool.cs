// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;
using DotCompute.Runtime.Logging;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace DotCompute.Runtime.Services.Memory.Pool;

/// <summary>
/// Memory pool for efficient buffer reuse.
/// </summary>
public sealed class MemoryPool : IDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<long, Queue<IntPtr>> _pools = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public MemoryPool(ILogger logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
    }

    public ValueTask<IntPtr?> TryGetBufferAsync(long size, MemoryOptions options, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return ValueTask.FromResult<IntPtr?>(null);
        }

        // Round up to nearest power of 2 for better pooling
        var poolSize = RoundToPowerOfTwo(size);

        if (_pools.TryGetValue(poolSize, out var queue))
        {
            lock (queue)
            {
                if (queue.Count > 0)
                {
                    var buffer = queue.Dequeue();
                    _logger.LogTrace("Retrieved buffer of size {Size} from pool", poolSize);
                    return ValueTask.FromResult<IntPtr?>(buffer);
                }
            }
        }

        return ValueTask.FromResult<IntPtr?>(null);
    }

    public async ValueTask PerformMaintenanceAsync()
    {
        if (_disposed)
        {
            return;
        }

        await Task.Run(() =>
        {
            var totalFreed = 0;
            foreach (var kvp in _pools)
            {
                var queue = kvp.Value;
                lock (queue)
                {
                    // Keep only recent buffers, free the rest
                    var keepCount = Math.Min(queue.Count, 10);
                    var freeCount = queue.Count - keepCount;

                    for (var i = 0; i < freeCount; i++)
                    {
                        if (queue.TryDequeue(out var buffer))
                        {
                            Marshal.FreeHGlobal(buffer);
                            totalFreed++;
                        }
                    }
                }
            }

            if (totalFreed > 0)
            {
                _logger.LogDebugMessage($"Memory pool maintenance freed {totalFreed} unused buffers");
            }
        });
    }

    private void PerformCleanup(object? state) => _ = PerformMaintenanceAsync();

    private static long RoundToPowerOfTwo(long value)
    {
        if (value <= 0)
        {
            return 1;
        }

        var result = 1L;
        while (result < value)
        {
            result <<= 1;
        }
        return result;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cleanupTimer?.Dispose();

            foreach (var queue in _pools.Values)
            {
                lock (queue)
                {
                    while (queue.TryDequeue(out var buffer))
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
            }

            _pools.Clear();
            _logger.LogDebugMessage("Memory pool disposed");
        }
    }
}