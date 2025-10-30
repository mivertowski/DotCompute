// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Initialization
{
    /// <summary>
    /// Production-grade CUDA initialization helper that ensures proper runtime initialization
    /// before any CUDA operations. Critical for WSL and containerized environments.
    /// </summary>
    public static class CudaInitializer
    {
        private static readonly Lock _initLock = new();
        private static bool _initialized;
        private static CudaError? _initializationError;
        private static string? _initializationErrorMessage;
        private static int? _deviceCount;
        private static ILogger? _logger;

        /// <summary>
        /// Gets whether CUDA has been successfully initialized.
        /// </summary>
        public static bool IsInitialized
        {
            get
            {
                lock (_initLock)
                {
                    return _initialized;
                }
            }
        }

        /// <summary>
        /// Gets the CUDA initialization error if any.
        /// </summary>
        public static CudaError? InitializationError => _initializationError;

        /// <summary>
        /// Gets the initialization error message if any.
        /// </summary>
        public static string? InitializationErrorMessage => _initializationErrorMessage;

        /// <summary>
        /// Gets the number of CUDA devices available after initialization.
        /// </summary>
        public static int DeviceCount => _deviceCount ?? 0;

        /// <summary>
        /// Sets the logger to use for initialization diagnostics.
        /// </summary>
        public static void SetLogger(ILogger logger) => _logger = logger;

        /// <summary>
        /// Ensures CUDA runtime is properly initialized. Safe to call multiple times.
        /// </summary>
        /// <returns>True if initialization succeeded, false otherwise.</returns>
        public static bool EnsureInitialized()
        {
            lock (_initLock)
            {
                if (_initialized)
                {
                    return true;
                }

                _logger?.LogDebug("Initializing CUDA runtime...");

                try
                {
                    // Step 1: Initialize CUDA runtime
                    // This is critical for WSL and ensures proper driver initialization
                    var initResult = CudaRuntime.cudaFree(IntPtr.Zero);

                    // cudaFree(0) is a common idiom to initialize CUDA runtime
                    // It should return Success even with null pointer

                    if (initResult != CudaError.Success)
                    {
                        _initializationError = initResult;
                        _initializationErrorMessage = $"CUDA runtime initialization failed: {CudaRuntime.GetErrorString(initResult)}";
                        _logger?.LogError("CUDA initialization failed: {Error}", _initializationErrorMessage);
                        return false;
                    }

                    // Step 2: Get device count to verify CUDA is functional
                    var deviceCountResult = CudaRuntime.cudaGetDeviceCount(out var count);
                    if (deviceCountResult != CudaError.Success)
                    {
                        _initializationError = deviceCountResult;
                        _initializationErrorMessage = $"Failed to get device count: {CudaRuntime.GetErrorString(deviceCountResult)}";
                        _logger?.LogError("Failed to get device count: {Error}", _initializationErrorMessage);
                        return false;
                    }

                    if (count == 0)
                    {
                        _initializationError = CudaError.NoDevice;
                        _initializationErrorMessage = "No CUDA devices found";
                        _logger?.LogWarning("No CUDA devices found");
                        return false;
                    }

                    _deviceCount = count;
                    _logger?.LogInformation("Found {DeviceCount} CUDA device(s)", count);

                    // Step 3: Set device 0 as current to ensure context creation
                    var setDeviceResult = CudaRuntime.cudaSetDevice(0);
                    if (setDeviceResult != CudaError.Success)
                    {
                        _initializationError = setDeviceResult;
                        _initializationErrorMessage = $"Failed to set device 0: {CudaRuntime.GetErrorString(setDeviceResult)}";
                        _logger?.LogError("Failed to set device 0: {Error}", _initializationErrorMessage);
                        return false;
                    }

                    // Step 4: Verify we can get device properties (this often fails in WSL without proper init)
                    var props = new CudaDeviceProperties();
                    var propsResult = CudaRuntime.cudaGetDeviceProperties(ref props, 0);
                    if (propsResult != CudaError.Success)
                    {
                        // Don't fail completely, but log the warning
                        _logger?.LogWarning("Failed to get device properties: {Error}. This may be a WSL limitation.",

                            CudaRuntime.GetErrorString(propsResult));
                    }
                    else
                    {
                        _logger?.LogInformation("Primary device: {DeviceName} (CC {Major}.{Minor})",

                            props.DeviceName, props.Major, props.Minor);
                    }

                    _initialized = true;
                    _logger?.LogInformation("CUDA runtime initialized successfully");
                    return true;
                }
                catch (DllNotFoundException ex)
                {
                    _initializationErrorMessage = $"CUDA runtime library not found: {ex.Message}";
                    _logger?.LogError(ex, "CUDA runtime library not found");
                    return false;
                }
                catch (Exception ex)
                {
                    _initializationErrorMessage = $"Unexpected error during CUDA initialization: {ex.Message}";
                    _logger?.LogError(ex, "Unexpected error during CUDA initialization");
                    return false;
                }
            }
        }

        /// <summary>
        /// Resets the initialization state. Useful for testing.
        /// </summary>
        public static void Reset()
        {
            lock (_initLock)
            {
                _initialized = false;
                _initializationError = null;
                _initializationErrorMessage = null;
                _deviceCount = null;
            }
        }

        /// <summary>
        /// Gets diagnostic information about the CUDA initialization state.
        /// </summary>
        public static string GetDiagnosticInfo()
        {
            lock (_initLock)
            {
                var info = new System.Text.StringBuilder();
                _ = info.AppendLine("=== CUDA Initialization Diagnostics ===");
                _ = info.AppendLine(CultureInfo.InvariantCulture, $"Initialized: {_initialized}");
                _ = info.AppendLine(CultureInfo.InvariantCulture, $"Device Count: {_deviceCount ?? -1}");


                if (_initializationError.HasValue)
                {
                    _ = info.AppendLine(CultureInfo.InvariantCulture, $"Error Code: {_initializationError.Value}");
                    _ = info.AppendLine(CultureInfo.InvariantCulture, $"Error Message: {_initializationErrorMessage}");
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Check for WSL
                    if (File.Exists("/proc/version"))
                    {
                        try
                        {
                            var version = File.ReadAllText("/proc/version");
                            if (version.Contains("microsoft", StringComparison.OrdinalIgnoreCase))
                            {
                                _ = info.AppendLine("Environment: WSL detected");
                            }
                        }
                        catch { }
                    }
                }

                return info.ToString();
            }
        }
    }
}
