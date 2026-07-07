// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Logging;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - CUDA backend has dynamic logging requirements

namespace DotCompute.Backends.CUDA
{

    /// <summary>
    /// Factory for creating CUDA accelerator instances
    /// </summary>
    public class CudaBackendFactory(ILogger<CudaBackendFactory>? logger = null) : IBackendFactory
    {
        private readonly ILogger<CudaBackendFactory> _logger = logger ?? new NullLogger<CudaBackendFactory>();

        // IsAvailable() is invoked several times during discovery/validation; log the detailed
        // toolkit-missing diagnosis once per process instead of once per call.
        private static int _toolkitMissingDiagnosisLogged;
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>

        public string Name => "CUDA";
        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        /// <value>The description.</value>
        public string Description => "NVIDIA CUDA GPU Backend";
        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        /// <value>The version.</value>
        public Version Version => new(1, 0, 0);
        /// <summary>
        /// Determines whether available.
        /// </summary>
        /// <returns>true if the condition is met; otherwise, false.</returns>

        public bool IsAvailable()
        {
            try
            {
                var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);

                if (result != CudaError.Success)
                {
                    _logger.LogWarningMessage(
                        $"cudaGetDeviceCount failed with {result}. CUDA backend is not available (the CUDA runtime loaded, but no usable device/driver was found — check that the NVIDIA driver version supports the installed CUDA Toolkit).");
                    return false;
                }

                var available = deviceCount > 0;
                _logger.LogInfoMessage($"CUDA backend availability check: {available} ({deviceCount} devices found)");

                return available;
            }
            catch (DllNotFoundException)
            {
                // cudart (part of the CUDA *Toolkit*) is missing. Use the *driver* API — nvcuda.dll /
                // libcuda.so ships with the GPU driver on every machine with an NVIDIA driver — to
                // tell the user whether they have a usable GPU that merely lacks the toolkit (GH #182:
                // driver-only frameworks like ILGPU work on such machines, so a bare "CUDA not found"
                // reads as a DotCompute bug instead of a missing prerequisite).
                try
                {
                    if (CudaRuntime.cuInit(0) == CudaError.Success
                        && CudaRuntime.cuDeviceGetCount(out var driverDeviceCount) == CudaError.Success
                        && driverDeviceCount > 0)
                    {
                        if (Interlocked.Exchange(ref _toolkitMissingDiagnosisLogged, 1) == 0)
                        {
                            var driverCuda = CudaRuntime.cuDriverGetVersion(out var driverVersion) == CudaError.Success
                                ? $"{driverVersion / 1000}.{driverVersion % 1000 / 10}"
                                : "unknown";
                            _logger.LogWarningMessage(
                                $"An NVIDIA GPU and driver were detected ({driverDeviceCount} device(s), driver supports CUDA {driverCuda}), " +
                                "but the CUDA Toolkit runtime (cudart64_*.dll / libcudart.so) was not found. " +
                                "Easiest fix: add the NuGet package DotCompute.Backends.CUDA.Natives.CU13.V2 (drivers r580+) " +
                                "or DotCompute.Backends.CUDA.Natives.CU12.V2 (drivers r525+) — no toolkit install needed. " +
                                "Alternatively install the CUDA Toolkit 12.0+ from https://developer.nvidia.com/cuda-downloads " +
                                "or ensure its bin directory is on PATH (the CUDA_PATH environment variable is also probed).");
                        }
                        return false;
                    }
                }
                catch (DllNotFoundException)
                {
                    // No driver library either — genuinely no NVIDIA GPU/driver on this machine.
                }
                catch (EntryPointNotFoundException)
                {
                    // Driver library present but too old to export the probed entry points.
                }

                _logger.LogWarningMessage("CUDA runtime library not found and no NVIDIA driver detected. CUDA backend is not available.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error checking CUDA availability");
                return false;
            }
        }
        /// <summary>
        /// Creates a new accelerators.
        /// </summary>
        /// <returns>The created accelerators.</returns>

        public IEnumerable<IAccelerator> CreateAccelerators()
        {
            if (!IsAvailable())
            {
                _logger.LogWarningMessage("CUDA backend is not available. No accelerators will be created.");
                yield break;
            }

            var createdAccelerators = new List<IAccelerator>();

            try
            {
                // Use CudaDevice.DetectAll for enhanced device detection
                var devices = CudaDevice.DetectAll(_logger).ToList();
                _logger.LogInfoMessage(" CUDA device(s)");

                foreach (var device in devices)
                {
                    try
                    {
                        _logger.LogInfoMessage($"Creating accelerator for {device.Name} (ID: {device.DeviceId}, Arch: {device.ArchitectureGeneration}, RTX2000Ada: {device.IsRTX2000Ada})");

                        // Create logger for this specific device
                        var loggerFactory = _logger is ILoggerFactory factory ? factory : null;
                        var acceleratorLogger = loggerFactory?.CreateLogger<CudaAccelerator>() ?? new NullLogger<CudaAccelerator>();
                        var accelerator = new CudaAccelerator(device.DeviceId, acceleratorLogger);
                        createdAccelerators.Add(accelerator);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogErrorMessage(ex, $"Failed to create CUDA accelerator for device {device.DeviceId}: {device.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Failed to detect CUDA devices");
            }

            foreach (var accelerator in createdAccelerators)
            {
                yield return accelerator;
            }
        }
        /// <summary>
        /// Creates a new default accelerator.
        /// </summary>
        /// <returns>The created default accelerator.</returns>

        public IAccelerator? CreateDefaultAccelerator()
        {
            if (!IsAvailable())
            {
                _logger.LogWarningMessage("CUDA backend is not available. Cannot create default accelerator.");
                return null;
            }

            try
            {
                // Detect the default device (device 0) with enhanced detection
                var deviceLogger = _logger is ILoggerFactory factory ? factory.CreateLogger<CudaDevice>() : new NullLogger<CudaDevice>();
                var defaultDevice = CudaDevice.Detect(0, deviceLogger);
                if (defaultDevice == null)
                {
                    _logger.LogWarningMessage("Default CUDA device (device 0) not found");
                    return null;
                }

                _logger.LogInfoMessage($"Creating default CUDA accelerator for {defaultDevice.Name} (Arch: {defaultDevice.ArchitectureGeneration}, RTX2000Ada: {defaultDevice.IsRTX2000Ada})");

                var loggerFactory = _logger is ILoggerFactory lf ? lf : null;
                var acceleratorLogger = loggerFactory?.CreateLogger<CudaAccelerator>() ?? new NullLogger<CudaAccelerator>();
                return new CudaAccelerator(0, acceleratorLogger);
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Failed to create default CUDA accelerator");
                return null;
            }
        }
        /// <summary>
        /// Gets the capabilities.
        /// </summary>
        /// <returns>The capabilities.</returns>

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
                SupportedFeatures =
                [
                    "Tensor Cores",
                "Dynamic Parallelism",
                "Cooperative Groups",
                "CUDA Graphs",
                "Memory Pooling",
                "Stream Capture",
                "Multi-GPU",
                "NVLink"
                ]
            };

            return capabilities;
        }

        private bool CheckUnifiedMemorySupport()
        {
            try
            {
                if (!IsAvailable())
                {
                    return false;
                }

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
                if (!IsAvailable())
                {
                    return 0;
                }

                var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
                return result == CudaError.Success ? deviceCount : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
