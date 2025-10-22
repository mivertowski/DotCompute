// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;
using CudaFuncCache = DotCompute.Backends.CUDA.Types.Native.Enums.CudaCacheConfig;
using CudaSharedMemConfig = DotCompute.Backends.CUDA.Types.Native.Enums.CudaSharedMemConfig;

namespace DotCompute.Backends.CUDA.Integration;

/// <summary>
/// Manages CUDA context lifecycle, switching, and optimization
/// </summary>
public sealed partial class CudaContextManager : IDisposable
{
    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 5700,
        Level = LogLevel.Information,
        Message = "CUDA Context Manager initialized for primary device {DeviceId}")]
    private static partial void LogContextManagerInitialized(ILogger logger, int deviceId);

    [LoggerMessage(
        EventId = 5701,
        Level = LogLevel.Debug,
        Message = "Created new context for device {DeviceId}")]
    private static partial void LogContextCreated(ILogger logger, int deviceId);

    [LoggerMessage(
        EventId = 5702,
        Level = LogLevel.Error,
        Message = "Failed to create context for device {DeviceId}")]
    private static partial void LogContextCreationFailed(ILogger logger, Exception ex, int deviceId);

    [LoggerMessage(
        EventId = 5703,
        Level = LogLevel.Debug,
        Message = "Switched to device {DeviceId}")]
    private static partial void LogDeviceSwitched(ILogger logger, int deviceId);

    [LoggerMessage(
        EventId = 5704,
        Level = LogLevel.Error,
        Message = "Failed to switch to device {DeviceId}")]
    private static partial void LogDeviceSwitchFailed(ILogger logger, Exception ex, int deviceId);

    [LoggerMessage(
        EventId = 5705,
        Level = LogLevel.Error,
        Message = "Context synchronization failed")]
    private static partial void LogSynchronizationFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5706,
        Level = LogLevel.Warning,
        Message = "Error calculating context health")]
    private static partial void LogHealthCalculationError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5707,
        Level = LogLevel.Debug,
        Message = "Context for device {DeviceId} optimized")]
    private static partial void LogContextOptimized(ILogger logger, int deviceId);

    [LoggerMessage(
        EventId = 6862,
        Level = LogLevel.Warning,
        Message = "Failed to optimize context for device {DeviceId}")]
    private static partial void LogFailedToOptimizeContext(ILogger logger, Exception ex, int deviceId);

    [LoggerMessage(
        EventId = 5708,
        Level = LogLevel.Warning,
        Message = "Failed to optimize context for device {DeviceId}")]
    private static partial void LogContextOptimizationFailed(ILogger logger, Exception ex, int deviceId);

    [LoggerMessage(
        EventId = 5709,
        Level = LogLevel.Debug,
        Message = "Maintenance completed for device {DeviceId}")]
    private static partial void LogDeviceMaintenanceCompleted(ILogger logger, int deviceId);

    [LoggerMessage(
        EventId = 5710,
        Level = LogLevel.Warning,
        Message = "Maintenance failed for device {DeviceId}")]
    private static partial void LogDeviceMaintenanceFailed(ILogger logger, Exception ex, int deviceId);

    [LoggerMessage(
        EventId = 5711,
        Level = LogLevel.Error,
        Message = "Error during context maintenance")]
    private static partial void LogContextMaintenanceError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5712,
        Level = LogLevel.Warning,
        Message = "Error releasing context for device {DeviceId}")]
    private static partial void LogContextReleaseFailed(ILogger logger, Exception ex, int deviceId);

    [LoggerMessage(
        EventId = 5713,
        Level = LogLevel.Debug,
        Message = "CUDA Context Manager disposed")]
    private static partial void LogContextManagerDisposed(ILogger logger);

    #endregion

    private readonly CudaContext _primaryContext;
    private readonly ILogger _logger;
    private readonly Dictionary<int, CudaContext> _deviceContexts;
    private readonly object _contextLock = new();
    private volatile bool _disposed;
    private int _currentDevice = -1;
    /// <summary>
    /// Initializes a new instance of the CudaContextManager class.
    /// </summary>
    /// <param name="primaryContext">The primary context.</param>
    /// <param name="logger">The logger.</param>

    public CudaContextManager(CudaContext primaryContext, ILogger logger)
    {
        _primaryContext = primaryContext ?? throw new ArgumentNullException(nameof(primaryContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _deviceContexts = new Dictionary<int, CudaContext> { { primaryContext.DeviceId, primaryContext } };
        _currentDevice = primaryContext.DeviceId;

        LogContextManagerInitialized(_logger, primaryContext.DeviceId);
    }

    /// <summary>
    /// Gets the primary CUDA context
    /// </summary>
    public CudaContext PrimaryContext => _primaryContext;

    /// <summary>
    /// Gets the current active device ID
    /// </summary>
    public int CurrentDevice => _currentDevice;

    /// <summary>
    /// Creates or gets a context for the specified device
    /// </summary>
    public CudaContext GetOrCreateContext(int deviceId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_contextLock)
        {
            if (_deviceContexts.TryGetValue(deviceId, out var existingContext))
            {
                return existingContext;
            }

            try
            {
                var newContext = CreateContextForDevice(deviceId);
                _deviceContexts[deviceId] = newContext;
                LogContextCreated(_logger, deviceId);
                return newContext;
            }
            catch (Exception ex)
            {
                LogContextCreationFailed(_logger, ex, deviceId);
                throw;
            }
        }
    }

    /// <summary>
    /// Switches to the specified device context
    /// </summary>
    public void SwitchToDevice(int deviceId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_currentDevice == deviceId)
        {
            return; // Already on the correct device
        }

        lock (_contextLock)
        {
            try
            {
                var context = GetOrCreateContext(deviceId);
                context.MakeCurrent();
                _currentDevice = deviceId;
                LogDeviceSwitched(_logger, deviceId);
            }
            catch (Exception ex)
            {
                LogDeviceSwitchFailed(_logger, ex, deviceId);
                throw;
            }
        }
    }

    /// <summary>
    /// Synchronizes the current context
    /// </summary>
    public void Synchronize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            var result = CudaRuntime.cudaDeviceSynchronize();
            if (result != CudaError.Success)
            {
                throw new InvalidOperationException($"Device synchronization failed: {result}");
            }
        }
        catch (Exception ex)
        {
            LogSynchronizationFailed(_logger, ex);
            throw;
        }
    }

    /// <summary>
    /// Synchronizes the current context asynchronously
    /// </summary>
    public async Task SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Synchronize();
        }, cancellationToken);
    }

    /// <summary>
    /// Gets context health status
    /// </summary>
    public double GetContextHealth()
    {
        if (_disposed)
        {
            return 0.0;
        }

        try
        {
            var totalHealth = 0.0;
            var contextCount = 0;

            lock (_contextLock)
            {
                foreach (var (deviceId, context) in _deviceContexts)
                {
                    var contextHealth = CalculateContextHealth(deviceId, context);
                    totalHealth += contextHealth;
                    contextCount++;
                }
            }

            return contextCount > 0 ? totalHealth / contextCount : 0.0;
        }
        catch (Exception ex)
        {
            LogHealthCalculationError(_logger, ex);
            return 0.0;
        }
    }

    /// <summary>
    /// Optimizes contexts for the given workload
    /// </summary>
    public async Task OptimizeContextAsync(CudaWorkloadProfile profile, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await Task.Run(() =>
        {
            lock (_contextLock)
            {
                foreach (var (deviceId, context) in _deviceContexts)
                {
                    try
                    {
                        OptimizeContextForWorkload(deviceId, context, profile);
                        LogContextOptimized(_logger, deviceId);
                    }
                    catch (Exception ex)
                    {
                        LogContextOptimizationFailed(_logger, ex, deviceId);
                    }
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Performs maintenance on all contexts
    /// </summary>
    public void PerformMaintenance()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            lock (_contextLock)
            {
                foreach (var (deviceId, context) in _deviceContexts)
                {
                    try
                    {
                        // Switch to context and perform cleanup
                        SwitchToDevice(deviceId);

                        // Clear any pending errors

                        _ = CudaRuntime.cudaGetLastError();

                        // Synchronize to ensure all operations complete

                        Synchronize();


                        LogDeviceMaintenanceCompleted(_logger, deviceId);
                    }
                    catch (Exception ex)
                    {
                        LogDeviceMaintenanceFailed(_logger, ex, deviceId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogContextMaintenanceError(_logger, ex);
        }
    }

    /// <summary>
    /// Gets all managed contexts
    /// </summary>
    public IReadOnlyDictionary<int, CudaContext> GetAllContexts()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_contextLock)
        {
            return new Dictionary<int, CudaContext>(_deviceContexts);
        }
    }

    private static CudaContext CreateContextForDevice(int deviceId)
    {
        // Set the device first
        var result = CudaRuntime.cudaSetDevice(deviceId);
        if (result != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to set device {deviceId}: {result}");
        }

        // Create context using primary context APIs for better compatibility
        result = CudaRuntime.cudaDevicePrimaryCtxRetain(out var contextPtr, deviceId);
        if (result != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to retain primary context for device {deviceId}: {result}");
        }

        // Make the context current
        result = CudaRuntime.cudaSetDevice(deviceId);
        if (result != CudaError.Success)
        {
            _ = CudaRuntime.cudaDevicePrimaryCtxRelease(deviceId); // Cleanup on failure
            throw new InvalidOperationException($"Failed to make context current for device {deviceId}: {result}");
        }

        return new CudaContext(contextPtr, deviceId);
    }

    private static double CalculateContextHealth(int deviceId, CudaContext context)
    {
        try
        {
            // Check if context is valid and responsive
            var originalDevice = -1;
            var getDeviceResult = CudaRuntime.cudaGetDevice(out originalDevice);

            // Switch to the context's device

            var setDeviceResult = CudaRuntime.cudaSetDevice(deviceId);
            if (setDeviceResult != CudaError.Success)
            {
                return 0.0; // Context not accessible
            }

            // Check for any pending errors
            var lastError = CudaRuntime.cudaGetLastError();

            // Restore original device

            if (getDeviceResult == CudaError.Success)
            {
                _ = CudaRuntime.cudaSetDevice(originalDevice);
            }

            // Context is healthy if no errors
            return lastError == CudaError.Success ? 1.0 : 0.5;
        }
        catch
        {
            return 0.0; // Any exception indicates unhealthy context
        }
    }

    private void OptimizeContextForWorkload(int deviceId, CudaContext context, CudaWorkloadProfile profile)
    {
        try
        {
            // Switch to the context
            SwitchToDevice(deviceId);

            // Set cache preference based on workload
            if (profile.HasMatrixOperations)
            {
                // Prefer shared memory for matrix operations
                _ = CudaRuntime.cudaDeviceSetCacheConfig(CudaFuncCache.PreferShared);
            }
            else if (profile.IsMemoryIntensive)
            {
                // Prefer L1 cache for memory-intensive workloads
                _ = CudaRuntime.cudaDeviceSetCacheConfig(CudaFuncCache.PreferCache);
            }
            else
            {
                // Use default cache configuration
                _ = CudaRuntime.cudaDeviceSetCacheConfig(CudaFuncCache.PreferNone);
            }

            // Set shared memory bank size for high-precision workloads
            if (profile.RequiresHighPrecision)
            {
                _ = CudaRuntime.cudaDeviceSetSharedMemConfig(CudaSharedMemConfig.BankSizeEightByte);
            }
            else
            {
                _ = CudaRuntime.cudaDeviceSetSharedMemConfig(CudaSharedMemConfig.BankSizeFourByte);
            }
        }
        catch (Exception ex)
        {
            LogFailedToOptimizeContext(_logger, ex, deviceId);
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            lock (_contextLock)
            {
                // Don't dispose the primary context as it's managed externally
                foreach (var (deviceId, context) in _deviceContexts)
                {
                    if (context != _primaryContext)
                    {
                        try
                        {
                            // Release primary context
                            _ = CudaRuntime.cudaDevicePrimaryCtxRelease(deviceId);
                        }
                        catch (Exception ex)
                        {
                            LogContextReleaseFailed(_logger, ex, deviceId);
                        }
                    }
                }


                _deviceContexts.Clear();
            }

            _disposed = true;
            LogContextManagerDisposed(_logger);
        }
    }
}
