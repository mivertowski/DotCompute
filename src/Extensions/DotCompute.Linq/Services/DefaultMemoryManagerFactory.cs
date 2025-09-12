// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Memory;
using DotCompute.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Services;

/// <summary>
/// Factory for creating unified memory managers for LINQ operations.
/// </summary>
public class DefaultMemoryManagerFactory
{
    private readonly ILogger<IUnifiedMemoryManager> _logger;
    private readonly Dictionary<IAccelerator, IUnifiedMemoryManager> _managers = new();
    private readonly Lock _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultMemoryManagerFactory"/> class.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public DefaultMemoryManagerFactory(ILogger<IUnifiedMemoryManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets or creates a memory manager for the specified accelerator.
    /// </summary>
    /// <param name="accelerator">The accelerator</param>
    /// <returns>A unified memory manager</returns>
    public IUnifiedMemoryManager GetOrCreateMemoryManager(IAccelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(accelerator);

        lock (_lock)
        {
            if (_managers.TryGetValue(accelerator, out var existingManager))
            {
                return existingManager;
            }

            var newManager = accelerator.Memory;
            _managers[accelerator] = newManager;


            _logger.LogDebug("Created memory manager for accelerator {AcceleratorName}", accelerator.Info.Name);
            return newManager;
        }
    }

    /// <summary>
    /// Releases all memory managers.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var manager in _managers.Values)
            {
                if (manager is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _managers.Clear();
        }
    }
}