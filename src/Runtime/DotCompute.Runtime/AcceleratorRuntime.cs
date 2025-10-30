// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using DotCompute.Runtime.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime;


/// <summary>
/// Main runtime for accelerator management and execution
/// </summary>
[SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Simple logging in runtime layer")]
public class AcceleratorRuntime(IServiceProvider serviceProvider, ILogger<AcceleratorRuntime> logger) : IDisposable, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<AcceleratorRuntime> _logger = logger;
    private readonly List<IAccelerator> _accelerators = [];
    private readonly SemaphoreSlim _disposeLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initialize the runtime and discover available accelerators
    /// </summary>
    public async Task InitializeAsync()
    {
        _logger.LogInfoMessage("Initializing DotCompute Runtime...");

        // Discover and register accelerators
        await DiscoverAcceleratorsAsync().ConfigureAwait(false);

        _logger.LogInfoMessage("Runtime initialized successfully. Found {_accelerators.Count} accelerators.");
    }

    /// <summary>
    /// Get all available accelerators
    /// </summary>
    public IReadOnlyList<IAccelerator> GetAccelerators() => _accelerators.AsReadOnly();

    /// <summary>
    /// Get accelerator by type
    /// </summary>
    public IAccelerator? GetAccelerator(string deviceType) => _accelerators.FirstOrDefault(a => a.Info.DeviceType == deviceType);

    private Task DiscoverAcceleratorsAsync()
    {
        _logger.LogDebugMessage("Discovering available accelerators...");

        // Get the accelerator manager from DI
        var acceleratorManager = _serviceProvider.GetRequiredService<IAcceleratorManager>();

        // Get all available accelerators
        var availableAccelerators = acceleratorManager.AvailableAccelerators;

        foreach (var accelerator in availableAccelerators)
        {
            _accelerators.Add(accelerator);
            _logger.LogInfoMessage("Discovered {Type} accelerator: {accelerator.Info.DeviceType, accelerator.Info.Name}");
        }

        if (_accelerators.Count == 0)
        {
            _logger.LogWarningMessage("No accelerators discovered. This may indicate a configuration issue.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes of the runtime and all managed accelerators asynchronously
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCoreAsync().ConfigureAwait(false);
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
                _logger.LogInfoMessage("Disposing AcceleratorRuntime. For proper cleanup, use DisposeAsync().");

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
                _ = (_disposeLock?.Release());
            }
        }
    }

    /// <summary>
    /// Protected async implementation of Dispose pattern
    /// </summary>
    protected virtual async ValueTask DisposeAsyncCoreAsync()
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
                _logger.LogInfoMessage("Disposing {disposeTasks.Length} accelerators...");
                await Task.WhenAll(disposeTasks).ConfigureAwait(false);
            }

            _accelerators.Clear();
            _disposed = true;
        }
        finally
        {
            _ = (_disposeLock?.Release());
        }
    }
}
