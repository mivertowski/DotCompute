// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DotCompute.Runtime.Services.Memory;

/// <summary>
/// Implementation of unified memory service
/// </summary>
public class UnifiedMemoryService : IUnifiedMemoryService
{
    private readonly ILogger<UnifiedMemoryService> _logger;
    private readonly ConcurrentDictionary<IMemoryBuffer, HashSet<string>> _bufferAccelerators = new();
    private readonly ConcurrentDictionary<IMemoryBuffer, MemoryCoherenceStatus> _coherenceStatus = new();

    public UnifiedMemoryService(ILogger<UnifiedMemoryService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IMemoryBuffer> AllocateUnifiedAsync(long sizeInBytes, params string[] acceleratorIds)
    {
        ArgumentNullException.ThrowIfNull(acceleratorIds);

        if (sizeInBytes <= 0)
        {
            throw new ArgumentException("Size must be positive", nameof(sizeInBytes));
        }

        _logger.LogDebug("Allocating {SizeMB}MB of unified memory for accelerators: {AcceleratorIds}",
            sizeInBytes / 1024 / 1024, string.Join(", ", acceleratorIds));

        // Create a unified memory buffer (mock implementation)
        var buffer = new UnifiedMemoryBuffer(sizeInBytes);

        _bufferAccelerators[buffer] = new HashSet<string>(acceleratorIds);
        _coherenceStatus[buffer] = MemoryCoherenceStatus.Coherent;

        await Task.CompletedTask; // Placeholder for async allocation
        return buffer;
    }

    public async Task MigrateAsync(IMemoryBuffer buffer, string sourceAcceleratorId, string targetAcceleratorId)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAcceleratorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetAcceleratorId);

        _logger.LogDebug("Migrating memory buffer from {SourceId} to {TargetId}",
            sourceAcceleratorId, targetAcceleratorId);

        if (_bufferAccelerators.TryGetValue(buffer, out var accelerators))
        {
            _ = accelerators.Remove(sourceAcceleratorId);
            _ = accelerators.Add(targetAcceleratorId);
            _coherenceStatus[buffer] = MemoryCoherenceStatus.Synchronizing;
        }

        await Task.Delay(10); // Simulate migration time
        _coherenceStatus[buffer] = MemoryCoherenceStatus.Coherent;
    }

    public async Task SynchronizeCoherenceAsync(IMemoryBuffer buffer, params string[] acceleratorIds)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(acceleratorIds);

        _logger.LogDebug("Synchronizing memory coherence for buffer across {AcceleratorCount} accelerators",
            acceleratorIds.Length);

        _coherenceStatus[buffer] = MemoryCoherenceStatus.Synchronizing;
        await Task.Delay(5); // Simulate synchronization time
        _coherenceStatus[buffer] = MemoryCoherenceStatus.Coherent;
    }

    public MemoryCoherenceStatus GetCoherenceStatus(IMemoryBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        return _coherenceStatus.GetValueOrDefault(buffer, MemoryCoherenceStatus.Unknown);
    }
}