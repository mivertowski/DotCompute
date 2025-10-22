// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Accelerators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;

namespace DotCompute.Backends.Metal.Factory
{
    /// <summary>
    /// Factory for creating Metal accelerators
    /// </summary>
    public sealed class MetalBackendFactory(
        ILogger<MetalBackendFactory>? logger = null,
        ILoggerFactory? loggerFactory = null,
        IOptions<MetalAcceleratorOptions>? options = null)
    {
        private readonly ILogger<MetalBackendFactory> _logger = logger ?? NullLogger<MetalBackendFactory>.Instance;
        private readonly ILoggerFactory _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        private readonly IOptions<MetalAcceleratorOptions> _options = options ?? Options.Create(new MetalAcceleratorOptions());

        /// <summary>
        /// Check if Metal is supported on this system
        /// </summary>
        public bool IsMetalSupported()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _logger.LogDebug("Metal is not supported on {Platform}", RuntimeInformation.OSDescription);
                return false;
            }

            try
            {
                return MetalBackend.IsAvailable();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check Metal availability");
                return false;
            }
        }

        /// <summary>
        /// Create all available Metal accelerators
        /// </summary>
        public IEnumerable<IAccelerator> CreateAccelerators()
        {
            if (!IsMetalSupported())
            {
                _logger.LogWarning("Metal is not supported on this system");
                yield break;
            }

            var backend = new MetalBackend(
                _loggerFactory.CreateLogger<MetalBackend>(),
                _loggerFactory);

            try
            {
                var accelerators = backend.GetAccelerators();
                _logger.LogInformation("Found {Count} Metal accelerator(s)", accelerators.Count);

                foreach (var accelerator in accelerators)
                {
                    yield return accelerator;
                }
            }
            finally
            {
                // Don't dispose the backend as it owns the accelerators
                // The accelerators will handle their own lifecycle
            }
        }

        /// <summary>
        /// Create the default Metal accelerator
        /// </summary>
        public IAccelerator? CreateDefaultAccelerator()
        {
            if (!IsMetalSupported())
            {
                _logger.LogWarning("Metal is not supported on this system");
                return null;
            }

            try
            {
                var acceleratorLogger = _loggerFactory.CreateLogger<MetalAccelerator>();
                var accelerator = new MetalAccelerator(_options, acceleratorLogger);


                _logger.LogInformation("Created default Metal accelerator: {Name}", accelerator.Info.Name);
                return accelerator;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create default Metal accelerator");
                return null;
            }
        }

        /// <summary>
        /// Create a Metal accelerator for a specific device
        /// </summary>
        public IAccelerator? CreateAccelerator(int deviceIndex)
        {
            if (!IsMetalSupported())
            {
                _logger.LogWarning("Metal is not supported on this system");
                return null;
            }

            var backend = new MetalBackend(
                _loggerFactory.CreateLogger<MetalBackend>(),
                _loggerFactory);

            try
            {
                var accelerators = backend.GetAccelerators();
                if (deviceIndex < 0 || deviceIndex >= accelerators.Count)
                {
                    _logger.LogError("Invalid device index {Index}. Available devices: {Count}",

                        deviceIndex, accelerators.Count);
                    return null;
                }

                var accelerator = accelerators[deviceIndex];
                _logger.LogInformation("Created Metal accelerator for device {Index}: {Name}",

                    deviceIndex, accelerator.Info.Name);


                return accelerator;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Metal accelerator for device {Index}", deviceIndex);
                return null;
            }
        }

        /// <summary>
        /// Create a production-ready Metal accelerator with best available device
        /// </summary>
        public MetalAccelerator? CreateProductionAccelerator(int preferredDeviceIndex = 0)
        {
            if (!IsMetalSupported())
            {
                _logger.LogWarning("Metal is not supported on this system");
                return null;
            }

            try
            {
                // Try to create accelerator for preferred device
                if (preferredDeviceIndex >= 0)
                {
                    var specificAccelerator = CreateAccelerator(preferredDeviceIndex) as MetalAccelerator;
                    if (specificAccelerator != null)
                    {
                        return specificAccelerator;
                    }
                }

                // Fall back to default accelerator
                var defaultAccelerator = CreateDefaultAccelerator() as MetalAccelerator;
                if (defaultAccelerator != null)
                {
                    _logger.LogInformation("Using default Metal accelerator as production accelerator");
                    return defaultAccelerator;
                }

                _logger.LogError("Failed to create any Metal accelerator");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create production Metal accelerator");
                return null;
            }
        }

        /// <summary>
        /// Get information about available Metal devices
        /// </summary>
        internal IReadOnlyList<DeviceInfo> GetAvailableDevices()
        {
            var devices = new List<DeviceInfo>();

            if (!IsMetalSupported())
            {
                return devices.AsReadOnly();
            }

            var backend = new MetalBackend(
                _loggerFactory.CreateLogger<MetalBackend>(),
                _loggerFactory);

            try
            {
                var accelerators = backend.GetAccelerators();


                foreach (var accelerator in accelerators)
                {
                    var info = accelerator.Info;
                    devices.Add(new DeviceInfo
                    {
                        Index = devices.Count,
                        Name = info.Name,
                        DeviceType = AcceleratorType.Metal,
                        TotalMemory = info.TotalMemory,
                        ComputeUnits = info.ComputeUnits,
                        IsAvailable = true
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enumerate Metal devices");
            }

            return devices.AsReadOnly();
        }

        /// <summary>
        /// Device information
        /// </summary>
        internal record DeviceInfo
        {
            public int Index { get; init; }
            public string Name { get; init; } = string.Empty;
            public AcceleratorType DeviceType { get; init; }
            public long TotalMemory { get; init; }
            public int ComputeUnits { get; init; }
            public bool IsAvailable { get; init; }
        }
    }
}