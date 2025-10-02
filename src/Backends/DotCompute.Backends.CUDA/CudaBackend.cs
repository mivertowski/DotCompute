// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA
{

    /// <summary>
    /// Main entry point for CUDA compute backend
    /// </summary>
    public sealed partial class CudaBackend : IDisposable
    {
        private readonly ILogger<CudaBackend> _logger;
        private readonly List<CudaAccelerator> _accelerators = [];
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
                LogCudaNotAvailable(_logger);
                return;
            }

            try
            {
                LogDiscoveringCudaDevices(_logger);

                // 1. Enumerate CUDA devices using cuDeviceGet
                var deviceCountResult = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
                if (deviceCountResult != CudaError.Success)
                {
                    LogFailedToGetDeviceCount(_logger, CudaRuntime.GetErrorString(deviceCountResult));
                    return;
                }

                if (deviceCount == 0)
                {
                    LogNoCudaDevicesFound(_logger);
                    return;
                }

                LogFoundCudaDevices(_logger, deviceCount);

                // 2. Query device properties for each device
                for (var deviceId = 0; deviceId < deviceCount; deviceId++)
                {
                    try
                    {
                        if (ValidateDeviceAccessibility(deviceId))
                        {
                            var accelerator = CreateAccelerator(deviceId);
                            if (accelerator != null)
                            {
                                _accelerators.Add(accelerator);
                                LogDeviceCapabilities(accelerator);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogFailedToInitializeDevice(_logger, ex, deviceId);
                    }
                }

                LogDeviceDiscoveryCompleted(_logger, _accelerators.Count);
            }
            catch (Exception ex)
            {
                LogFailedToDiscoverAccelerators(_logger, ex);
            }
        }

        private bool ValidateDeviceAccessibility(int deviceId)
        {
            try
            {
                // 3. Check CUDA runtime version compatibility
                var runtimeVersionResult = CudaRuntime.cudaRuntimeGetVersion(out var runtimeVersion);
                if (runtimeVersionResult != CudaError.Success)
                {
                    LogCouldNotDetermineRuntimeVersion(_logger, deviceId);
                    return false;
                }

                var driverVersionResult = CudaRuntime.cudaDriverGetVersion(out var driverVersion);
                if (driverVersionResult != CudaError.Success)
                {
                    LogCouldNotDetermineDriverVersion(_logger, deviceId);
                    return false;
                }

                // Check minimum version requirements (CUDA 11.0+)
                if (runtimeVersion < 11000 || driverVersion < 11000)
                {
                    LogCudaVersionTooLow(_logger, deviceId,
                        $"{runtimeVersion / 1000}.{(runtimeVersion % 1000) / 10}",
                        $"{driverVersion / 1000}.{(driverVersion % 1000) / 10}");
                    return false;
                }

                // 4. Validate device accessibility
                var setDeviceResult = CudaRuntime.cudaSetDevice(deviceId);
                if (setDeviceResult != CudaError.Success)
                {
                    LogCannotAccessDevice(_logger, deviceId, CudaRuntime.GetErrorString(setDeviceResult));
                    return false;
                }

                // Test basic device operation
                var syncResult = CudaRuntime.cudaDeviceSynchronize();
                if (syncResult != CudaError.Success)
                {
                    LogDeviceFailedSynchronization(_logger, deviceId, CudaRuntime.GetErrorString(syncResult));
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogErrorValidatingDevice(_logger, ex, deviceId);
                return false;
            }
        }

        private CudaAccelerator? CreateAccelerator(int deviceId)
        {
            try
            {
                var deviceProps = new CudaDeviceProperties();
                var result = CudaRuntime.cudaGetDeviceProperties(ref deviceProps, deviceId);

                if (result != CudaError.Success)
                {
                    LogFailedToGetDeviceProperties(_logger, deviceId, CudaRuntime.GetErrorString(result));
                    return null;
                }

                // Check compute capability (require 5.0+)
                if (deviceProps.Major < 5)
                {
                    LogSkippingDeviceLowComputeCapability(_logger, deviceId, deviceProps.DeviceName, deviceProps.Major, deviceProps.Minor);
                    return null;
                }

                var acceleratorLogger = _logger is ILoggerFactory loggerFactory
                    ? loggerFactory.CreateLogger<CudaAccelerator>()
                    : new NullLogger<CudaAccelerator>();

                return new CudaAccelerator(deviceId, acceleratorLogger);
            }
            catch (Exception ex)
            {
                LogFailedToCreateAccelerator(_logger, ex, deviceId);
                return null;
            }
        }

        private void LogDeviceCapabilities(CudaAccelerator accelerator)
        {
            var info = accelerator.Info;
            var capabilities = info.Capabilities;

            LogCudaDevice(_logger, info.Name, info.Id.ToString());
            LogComputeCapability(_logger, info.ComputeCapability!);
            LogTotalMemory(_logger, info.TotalMemory, info.TotalMemory / (1024.0 * 1024 * 1024));
            LogMultiprocessors(_logger, info.ComputeUnits);
            LogClockRate(_logger, info.MaxClockFrequency);

            if (capabilities != null && capabilities.TryGetValue("SharedMemoryPerBlock", out var sharedMem) && sharedMem != null)
            {
                LogSharedMemoryPerBlock(_logger, sharedMem);
            }

            if (capabilities != null && capabilities.TryGetValue("MaxThreadsPerBlock", out var maxThreads) && maxThreads != null)
            {
                LogMaxThreadsPerBlock(_logger, maxThreads);
            }

            if (capabilities != null && capabilities.TryGetValue("WarpSize", out var warpSize) && warpSize != null)
            {
                LogWarpSize(_logger, warpSize);
            }

            if (capabilities != null && capabilities.TryGetValue("ECCEnabled", out var ecc) && ecc is bool eccEnabled && eccEnabled)
            {
                LogEccMemoryEnabled(_logger);
            }

            if (capabilities != null && capabilities.TryGetValue("UnifiedAddressing", out var unified) && unified is bool unifiedEnabled && unifiedEnabled)
            {
                LogUnifiedAddressingSupported(_logger);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // BaseAccelerator implements IAsyncDisposable, so we need to dispose asynchronously
            // For synchronous disposal, we'll block on the async operation
            foreach (var accelerator in _accelerators)
            {
                if (accelerator != null)
                {
                    accelerator.DisposeAsync().AsTask().Wait();
                }
            }

            _accelerators.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
