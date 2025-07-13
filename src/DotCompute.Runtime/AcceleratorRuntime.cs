// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace DotCompute.Runtime;

/// <summary>
/// Main runtime for accelerator management and execution
/// </summary>
[SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Simple logging in runtime layer")]
public class AcceleratorRuntime : IDisposable, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AcceleratorRuntime> _logger;
    private readonly List<IAccelerator> _accelerators = new();
    private readonly SemaphoreSlim _disposeLock = new(1, 1);
    private bool _disposed;

    public AcceleratorRuntime(IServiceProvider serviceProvider, ILogger<AcceleratorRuntime> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Initialize the runtime and discover available accelerators
    /// </summary>
    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing DotCompute Runtime...");
        
        // Discover and register accelerators
        await DiscoverAcceleratorsAsync();
        
        _logger.LogInformation("Runtime initialized successfully. Found {Count} accelerators.", _accelerators.Count);
    }

    /// <summary>
    /// Get all available accelerators
    /// </summary>
    public IReadOnlyList<IAccelerator> GetAccelerators() => _accelerators.AsReadOnly();

    /// <summary>
    /// Get accelerator by type
    /// </summary>
    public IAccelerator? GetAccelerator(string deviceType)
    {
        return _accelerators.FirstOrDefault(a => a.Info.DeviceType == deviceType);
    }

    private Task DiscoverAcceleratorsAsync()
    {
        _logger.LogDebug("Discovering available accelerators...");
        
        // Get the accelerator manager from DI
        var acceleratorManager = _serviceProvider.GetRequiredService<IAcceleratorManager>();
        
        // Get all available accelerators
        var availableAccelerators = acceleratorManager.AvailableAccelerators;
        
        foreach (var accelerator in availableAccelerators)
        {
            _accelerators.Add(accelerator);
            _logger.LogInformation("Discovered {Type} accelerator: {Name}", accelerator.Info.DeviceType, accelerator.Info.Name);
        }
        
        if (_accelerators.Count == 0)
        {
            _logger.LogWarning("No accelerators discovered. This may indicate a configuration issue.");
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes of the runtime and all managed accelerators asynchronously
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of the runtime and all managed accelerators synchronously
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposeLock.Wait();
        try
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose managed resources synchronously
                // Note: We're intentionally not disposing accelerators here
                // to avoid sync-over-async. Use DisposeAsync for proper cleanup.
                _logger.LogInformation("Disposing AcceleratorRuntime. For proper cleanup, use DisposeAsync().");
                
                // Clear the list but don't dispose accelerators synchronously
                _accelerators.Clear();
                
                // Dispose the semaphore
                _disposeLock?.Dispose();
            }

            _disposed = true;
        }
        finally
        {
            if (!_disposed)
            {
                _disposeLock?.Release();
            }
        }
    }

    /// <summary>
    /// Protected async implementation of Dispose pattern
    /// </summary>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
        {
            return;
        }

        await _disposeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            // Dispose all accelerators asynchronously
            var disposeTasks = _accelerators
                .Where(a => a != null)
                .Select(a => a.DisposeAsync().AsTask())
                .ToArray();

            if (disposeTasks.Length > 0)
            {
                _logger.LogInformation("Disposing {Count} accelerators...", disposeTasks.Length);
                await Task.WhenAll(disposeTasks).ConfigureAwait(false);
            }

            _accelerators.Clear();
            _disposed = true;
        }
        finally
        {
            _disposeLock?.Release();
        }
    }
}