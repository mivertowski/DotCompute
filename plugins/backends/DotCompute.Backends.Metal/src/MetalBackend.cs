// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal;

/// <summary>
/// Main entry point for Metal compute backend
/// </summary>
public class MetalBackend : IDisposable
{
    private readonly ILogger<MetalBackend> _logger;
    private readonly List<MetalAccelerator> _accelerators = new();
    private bool _disposed;

    public MetalBackend(ILogger<MetalBackend> logger)
    {
        _logger = logger;
        DiscoverAccelerators();
    }

    /// <summary>
    /// Check if Metal is available on this platform
    /// </summary>
    public static bool IsAvailable()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return false;
        }

        try
        {
            // This would call native Metal detection
            return MetalNative.IsMetalSupported();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get all available Metal accelerators
    /// </summary>
    public IReadOnlyList<MetalAccelerator> GetAccelerators() => _accelerators.AsReadOnly();

    /// <summary>
    /// Get default Metal accelerator
    /// </summary>
    public MetalAccelerator? GetDefaultAccelerator() => _accelerators.FirstOrDefault();

    private void DiscoverAccelerators()
    {
        if (!IsAvailable())
        {
            _logger.LogWarning("Metal is not available on this platform");
            return;
        }

        try
        {
            // This would discover available Metal devices
            _logger.LogInformation("Discovering Metal devices...");
            
            // For now, just log that discovery would happen here
            _logger.LogInformation("Metal device discovery completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover Metal accelerators");
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