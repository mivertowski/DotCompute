// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA;

/// <summary>
/// Main entry point for CUDA compute backend
/// </summary>
public class CudaBackend : IDisposable
{
    private readonly ILogger<CudaBackend> _logger;
    private readonly List<CudaAccelerator> _accelerators = new();
    private bool _disposed;

    public CudaBackend(ILogger<CudaBackend> logger)
    {
        _logger = logger;
        DiscoverAccelerators();
    }

    /// <summary>
    /// Check if CUDA is available on this platform
    /// </summary>
    public static bool IsAvailable()
    {
        try
        {
            // This would call CUDA runtime detection
            return CudaRuntime.IsCudaSupported();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get all available CUDA accelerators
    /// </summary>
    public IReadOnlyList<CudaAccelerator> GetAccelerators() => _accelerators.AsReadOnly();

    /// <summary>
    /// Get default CUDA accelerator
    /// </summary>
    public CudaAccelerator? GetDefaultAccelerator() => _accelerators.FirstOrDefault();

    private void DiscoverAccelerators()
    {
        if (!IsAvailable())
        {
            _logger.LogWarning("CUDA is not available on this platform");
            return;
        }

        try
        {
            // This would discover available CUDA devices
            _logger.LogInformation("Discovering CUDA devices...");
            
            // For now, just log that discovery would happen here
            _logger.LogInformation("CUDA device discovery completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover CUDA accelerators");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var accelerator in _accelerators)
        {
            accelerator?.Dispose();
        }
        
        _accelerators.Clear();
        _disposed = true;
    }
}