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
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Initialize the runtime and discover available accelerators.
    /// Idempotent: safe to call from multiple init paths (hosted service, lazy first-use,
    /// or the explicit InitializeDotComputeRuntimeAsync helper) without double-registering.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            _logger.LogInfoMessage("Initializing DotCompute Runtime...");

            // Discover and register accelerators
            await DiscoverAcceleratorsAsync().ConfigureAwait(false);

            _initialized = true;
            _logger.LogInfoMessage("Runtime initialized successfully. Found {_accelerators.Count} accelerators.");
        }
        finally
        {
            _ = _initLock.Release();
        }
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

        // Primary source: the IAccelerator instances the backends register directly via DI
        // (e.g. AddCpuBackend / AddCudaBackend each register an IAccelerator singleton).
        // This is resolved lazily here — not as a constructor dependency — so GPU accelerators
        // are only constructed when initialization actually runs.
        foreach (var accelerator in _serviceProvider.GetServices<IAccelerator>())
        {
            if (accelerator is not null && !_accelerators.Contains(accelerator))
            {
                _accelerators.Add(accelerator);
                _logger.LogInfoMessage("Discovered {Type} accelerator: {accelerator.Info.DeviceType, accelerator.Info.Name}");
            }
        }

        // Optional fallback: if an IAcceleratorManager is registered, merge any accelerators it
        // exposes that the direct registration did not. It is intentionally NOT required — the
        // standard backends do not register one, and GetRequiredService here was the original
        // root cause of "No accelerators are registered" (it threw, was swallowed by graceful
        // degradation, and left the runtime empty).
        var acceleratorManager = _serviceProvider.GetService<IAcceleratorManager>();
        if (acceleratorManager is not null)
        {
            foreach (var accelerator in acceleratorManager.AvailableAccelerators)
            {
                if (accelerator is not null && !_accelerators.Contains(accelerator))
                {
                    _accelerators.Add(accelerator);
                    _logger.LogInfoMessage("Discovered {Type} accelerator: {accelerator.Info.DeviceType, accelerator.Info.Name}");
                }
            }
        }

        if (_accelerators.Count == 0)
        {
            _logger.LogWarningMessage("No accelerators discovered. Register a backend (e.g. services.AddCpuBackend() / AddCudaBackend()) before executing kernels.");
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

                // Dispose the semaphores
                _initLock?.Dispose();
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
