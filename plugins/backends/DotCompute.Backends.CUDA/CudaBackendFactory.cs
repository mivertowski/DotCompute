// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA;

/// <summary>
/// Factory for creating CUDA accelerator instances
/// </summary>
public class CudaBackendFactory : IBackendFactory
{
    private readonly ILogger<CudaBackendFactory> _logger;

    public string Name => "CUDA";
    public string Description => "NVIDIA CUDA GPU Backend";
    public Version Version => new Version(1, 0, 0);

    public CudaBackendFactory(ILogger<CudaBackendFactory>? logger = null)
    {
        _logger = logger ?? new NullLogger<CudaBackendFactory>();
    }

    public bool IsAvailable()
    {
        try
        {
            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            
            if (result != CudaError.Success)
            {
                _logger.LogWarning("CUDA runtime returned error: {Error}", CudaRuntime.GetErrorString(result));
                return false;
            }

            var available = deviceCount > 0;
            _logger.LogInformation("CUDA backend availability check: {Available} ({DeviceCount} devices found)", 
                available, deviceCount);
            
            return available;
        }
        catch (DllNotFoundException)
        {
            _logger.LogWarning("CUDA runtime library not found. CUDA backend is not available.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking CUDA availability");
            return false;
        }
    }

    public IEnumerable<IAccelerator> CreateAccelerators()
    {
        if (!IsAvailable())
        {
            _logger.LogWarning("CUDA backend is not available. No accelerators will be created.");
            yield break;
        }

        var createdAccelerators = new List<IAccelerator>();
        
        try
        {
            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            CudaRuntime.CheckError(result, "Get device count");

            _logger.LogInformation("Creating {DeviceCount} CUDA accelerator(s)", deviceCount);
            
            for (int deviceId = 0; deviceId < deviceCount; deviceId++)
            {
                try
                {
                    // Create logger for this specific device
                    var deviceLogger = _logger is ILoggerFactory loggerFactory
                        ? loggerFactory.CreateLogger<CudaAccelerator>()
                        : new NullLogger<CudaAccelerator>();

                    var accelerator = new CudaAccelerator(deviceId, deviceLogger);
                    createdAccelerators.Add(accelerator);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create CUDA accelerator for device {DeviceId}", deviceId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate CUDA devices");
        }
        
        foreach (var accelerator in createdAccelerators)
        {
            yield return accelerator;
        }
    }

    public IAccelerator? CreateDefaultAccelerator()
    {
        if (!IsAvailable())
        {
            _logger.LogWarning("CUDA backend is not available. Cannot create default accelerator.");
            return null;
        }

        try
        {
            _logger.LogInformation("Creating default CUDA accelerator (device 0)");
            
            var deviceLogger = _logger is ILoggerFactory loggerFactory
                ? loggerFactory.CreateLogger<CudaAccelerator>()
                : new NullLogger<CudaAccelerator>();

            return new CudaAccelerator(0, deviceLogger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create default CUDA accelerator");
            return null;
        }
    }

    public BackendCapabilities GetCapabilities()
    {
        var capabilities = new BackendCapabilities
        {
            SupportsFloat16 = true,
            SupportsFloat32 = true,
            SupportsFloat64 = true,
            SupportsInt8 = true,
            SupportsInt16 = true,
            SupportsInt32 = true,
            SupportsInt64 = true,
            SupportsAsyncExecution = true,
            SupportsMultiDevice = true,
            SupportsUnifiedMemory = CheckUnifiedMemorySupport(),
            MaxDevices = GetMaxDevices(),
            SupportedFeatures = new[]
            {
                "Tensor Cores",
                "Dynamic Parallelism",
                "Cooperative Groups",
                "CUDA Graphs",
                "Memory Pooling",
                "Stream Capture",
                "Multi-GPU",
                "NVLink"
            }
        };

        return capabilities;
    }

    private bool CheckUnifiedMemorySupport()
    {
        try
        {
            if (!IsAvailable()) return false;

            // Check if first device supports unified memory
            var props = new CudaDeviceProperties();
            var result = CudaRuntime.cudaGetDeviceProperties(ref props, 0);
            
            if (result == CudaError.Success)
            {
                return props.ManagedMemory > 0;
            }
        }
        catch
        {
            // Ignore errors in capability check
        }

        return false;
    }

    private int GetMaxDevices()
    {
        try
        {
            if (!IsAvailable()) return 0;

            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            return result == CudaError.Success ? deviceCount : 0;
        }
        catch
        {
            return 0;
        }
    }
}