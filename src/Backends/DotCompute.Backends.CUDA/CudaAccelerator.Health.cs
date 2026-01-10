// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#pragma warning disable XFIX003 // Use LoggerMessage.Define - TODO: Convert to LoggerMessage.Define in future refactoring
#pragma warning disable CA2213  // Disposable fields should be disposed - TODO: Add NvmlWrapper disposal in DisposeCoreAsync

using DotCompute.Abstractions.Health;
using DotCompute.Backends.CUDA.Monitoring;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA
{
    /// <summary>
    /// CUDA accelerator health monitoring implementation.
    /// </summary>
    public sealed partial class CudaAccelerator
    {
        private NvmlWrapper? _nvmlWrapper;
        private bool _nvmlInitialized;
        private readonly object _nvmlLock = new();

        /// <summary>
        /// Gets a comprehensive health snapshot of the CUDA device.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task containing the device health snapshot.</returns>
        /// <remarks>
        /// <para>
        /// This method queries NVIDIA Management Library (NVML) for real-time GPU metrics including:
        /// - Temperature, power consumption, and fan speed
        /// - GPU and memory utilization percentages
        /// - Clock frequencies (graphics and memory)
        /// - Memory usage statistics
        /// - PCIe throughput measurements
        /// - Throttling status and reasons
        /// </para>
        ///
        /// <para>
        /// <b>Performance:</b> Typically takes 2-5ms to collect all metrics via NVML.
        /// Results are collected synchronously but wrapped in ValueTask for consistency.
        /// </para>
        ///
        /// <para>
        /// <b>Requirements:</b> NVIDIA driver with NVML support. Falls back to unavailable
        /// snapshot if NVML is not available or initialization fails.
        /// </para>
        /// </remarks>
        public override ValueTask<DeviceHealthSnapshot> GetHealthSnapshotAsync(CancellationToken cancellationToken = default)
        {
            // Ensure NVML is initialized
            EnsureNvmlInitialized();

            // If NVML is not available, return unavailable snapshot
            if (!_nvmlInitialized || _nvmlWrapper == null)
            {
                return ValueTask.FromResult(DeviceHealthSnapshot.CreateUnavailable(
                    deviceId: Info.Id,
                    deviceName: Info.Name,
                    backendType: "CUDA",
                    reason: "NVML not available - install NVIDIA drivers with management library support"
                ));
            }

            try
            {
                // Get metrics from NVML
                var gpuMetrics = _nvmlWrapper.GetDeviceMetrics(DeviceId);

                if (!gpuMetrics.IsAvailable)
                {
                    return ValueTask.FromResult(DeviceHealthSnapshot.CreateUnavailable(
                        deviceId: Info.Id,
                        deviceName: Info.Name,
                        backendType: "CUDA",
                        reason: "Failed to query GPU metrics from NVML"
                    ));
                }

                // Convert to sensor readings
                var sensorReadings = ConvertToSensorReadings(gpuMetrics);

                // Calculate health score
                var healthScore = CalculateHealthScore(gpuMetrics);

                // Determine status
                var status = DetermineHealthStatus(healthScore, gpuMetrics);

                // Build status message
                var statusMessage = BuildStatusMessage(gpuMetrics, status);

                var snapshot = new DeviceHealthSnapshot
                {
                    DeviceId = Info.Id,
                    DeviceName = Info.Name,
                    BackendType = "CUDA",
                    Timestamp = DateTimeOffset.UtcNow,
                    HealthScore = healthScore,
                    Status = status,
                    IsAvailable = true,
                    SensorReadings = sensorReadings,
                    ErrorCount = 0, // TODO: Track errors in CudaAccelerator
                    ConsecutiveFailures = 0, // TODO: Track failures in CudaAccelerator
                    IsThrottling = gpuMetrics.IsThrottling,
                    StatusMessage = statusMessage
                };

                return ValueTask.FromResult(snapshot);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to collect health snapshot for CUDA device {DeviceName}", Info.Name);
                return ValueTask.FromResult(DeviceHealthSnapshot.CreateUnavailable(
                    deviceId: Info.Id,
                    deviceName: Info.Name,
                    backendType: "CUDA",
                    reason: $"Error collecting metrics: {ex.Message}"
                ));
            }
        }

        /// <summary>
        /// Gets current sensor readings from the CUDA device.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task containing the collection of sensor readings.</returns>
        public override ValueTask<IReadOnlyList<SensorReading>> GetSensorReadingsAsync(CancellationToken cancellationToken = default)
        {
            // Ensure NVML is initialized
            EnsureNvmlInitialized();

            if (!_nvmlInitialized || _nvmlWrapper == null)
            {
                return ValueTask.FromResult<IReadOnlyList<SensorReading>>(Array.Empty<SensorReading>());
            }

            try
            {
                var gpuMetrics = _nvmlWrapper.GetDeviceMetrics(DeviceId);

                if (!gpuMetrics.IsAvailable)
                {
                    return ValueTask.FromResult<IReadOnlyList<SensorReading>>(Array.Empty<SensorReading>());
                }

                var readings = ConvertToSensorReadings(gpuMetrics);
                return ValueTask.FromResult<IReadOnlyList<SensorReading>>(readings);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to collect sensor readings for CUDA device {DeviceName}", Info.Name);
                return ValueTask.FromResult<IReadOnlyList<SensorReading>>(Array.Empty<SensorReading>());
            }
        }

        /// <summary>
        /// Ensures NVML wrapper is initialized (lazy initialization).
        /// </summary>
        private void EnsureNvmlInitialized()
        {
            if (_nvmlInitialized)
            {
                return;
            }

            lock (_nvmlLock)
            {
                if (_nvmlInitialized)
                {
                    return;
                }

                try
                {
                    _nvmlWrapper = new NvmlWrapper(Logger);
                    _nvmlInitialized = _nvmlWrapper.Initialize();

                    if (!_nvmlInitialized)
                    {
                        Logger.LogWarning("NVML initialization failed for device {DeviceName}. Health monitoring will be unavailable.", Info.Name);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to initialize NVML for device {DeviceName}. Health monitoring will be unavailable.", Info.Name);
                    _nvmlInitialized = false;
                }
            }
        }

        /// <summary>
        /// Converts GPU metrics from NVML to sensor reading collection.
        /// </summary>
        private static IReadOnlyList<SensorReading> ConvertToSensorReadings(GpuMetrics metrics)
        {
            var readings = new List<SensorReading>(14);

            // Temperature
            readings.Add(SensorReading.Create(
                SensorType.Temperature,
                metrics.Temperature,
                minValue: 20.0,
                maxValue: 95.0,
                name: "GPU Core Temperature"
            ));

            // Power draw
            readings.Add(SensorReading.Create(
                SensorType.PowerDraw,
                metrics.PowerUsage,
                minValue: 0.0,
                maxValue: 450.0, // Typical max for high-end GPUs
                name: "GPU Power Consumption"
            ));

            // Compute utilization
            readings.Add(SensorReading.Create(
                SensorType.ComputeUtilization,
                metrics.GpuUtilization,
                minValue: 0.0,
                maxValue: 100.0,
                name: "GPU Utilization"
            ));

            // Memory utilization (bandwidth)
            readings.Add(SensorReading.Create(
                SensorType.MemoryUtilization,
                metrics.MemoryBandwidthUtilization,
                minValue: 0.0,
                maxValue: 100.0,
                name: "Memory Bandwidth Utilization"
            ));

            // Fan speed
            readings.Add(SensorReading.Create(
                SensorType.FanSpeed,
                metrics.FanSpeedPercent,
                minValue: 0.0,
                maxValue: 100.0,
                name: "Fan Speed"
            ));

            // Graphics clock
            readings.Add(SensorReading.Create(
                SensorType.GraphicsClock,
                metrics.GraphicsClockMHz,
                minValue: 0.0,
                name: "Graphics Clock Frequency"
            ));

            // Memory clock
            readings.Add(SensorReading.Create(
                SensorType.MemoryClock,
                metrics.MemoryClockMHz,
                minValue: 0.0,
                name: "Memory Clock Frequency"
            ));

            // Memory used
            readings.Add(SensorReading.Create(
                SensorType.MemoryUsedBytes,
                metrics.MemoryUsed,
                minValue: 0.0,
                maxValue: metrics.MemoryTotal,
                name: "Memory Used"
            ));

            // Memory total
            readings.Add(SensorReading.Create(
                SensorType.MemoryTotalBytes,
                metrics.MemoryTotal,
                name: "Total Memory Capacity"
            ));

            // Memory free
            readings.Add(SensorReading.Create(
                SensorType.MemoryFreeBytes,
                metrics.MemoryFree,
                minValue: 0.0,
                maxValue: metrics.MemoryTotal,
                name: "Free Memory"
            ));

            // PCIe throughput TX
            readings.Add(SensorReading.Create(
                SensorType.PcieThroughputTx,
                metrics.PcieTxBytes,
                minValue: 0.0,
                name: "PCIe TX Throughput"
            ));

            // PCIe throughput RX
            readings.Add(SensorReading.Create(
                SensorType.PcieThroughputRx,
                metrics.PcieRxBytes,
                minValue: 0.0,
                name: "PCIe RX Throughput"
            ));

            // Throttling status
            readings.Add(SensorReading.Create(
                SensorType.ThrottlingStatus,
                metrics.IsThrottling ? 1.0 : 0.0,
                minValue: 0.0,
                maxValue: 3.0,
                name: "Thermal Throttling"
            ));

            // Power throttling (derived from throttle reasons)
            var powerThrottling = metrics.ThrottleReasons.Contains("Power", StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
            readings.Add(SensorReading.Create(
                SensorType.PowerThrottlingStatus,
                powerThrottling,
                minValue: 0.0,
                maxValue: 3.0,
                name: "Power Throttling"
            ));

            return readings;
        }

        /// <summary>
        /// Calculates overall health score from GPU metrics.
        /// </summary>
        /// <param name="metrics">GPU metrics from NVML.</param>
        /// <returns>Health score from 0.0 (critical) to 1.0 (perfect).</returns>
        private static double CalculateHealthScore(GpuMetrics metrics)
        {
            // Temperature component (weight: 0.3)
            // Optimal: < 70°C, Warning: 70-80°C, Critical: > 85°C
            var tempScore = metrics.Temperature switch
            {
                < 70 => 1.0,
                < 75 => 0.9,
                < 80 => 0.7,
                < 85 => 0.5,
                < 90 => 0.3,
                _ => 0.1
            };

            // Utilization component (weight: 0.2)
            // Healthy utilization patterns (not constantly maxed out)
            var utilizationScore = metrics.GpuUtilization switch
            {
                < 90 => 1.0,
                < 95 => 0.9,
                >= 95 => 0.8 // High but acceptable for compute workloads
            };

            // Throttling component (weight: 0.3)
            var throttleScore = metrics.IsThrottling ? 0.5 : 1.0;

            // Memory pressure component (weight: 0.1)
            var memoryUsagePercent = (double)metrics.MemoryUsed / metrics.MemoryTotal * 100.0;
            var memoryScore = memoryUsagePercent switch
            {
                < 80 => 1.0,
                < 90 => 0.9,
                < 95 => 0.7,
                _ => 0.5
            };

            // Power component (weight: 0.1)
            // Assuming typical max TDP of 350W for high-end GPUs
            var powerPercent = (metrics.PowerUsage / 350.0) * 100.0;
            var powerScore = powerPercent switch
            {
                < 80 => 1.0,
                < 90 => 0.9,
                < 95 => 0.8,
                _ => 0.7
            };

            // Weighted composite score
            var healthScore =
                (tempScore * 0.3) +
                (throttleScore * 0.3) +
                (utilizationScore * 0.2) +
                (memoryScore * 0.1) +
                (powerScore * 0.1);

            return Math.Clamp(healthScore, 0.0, 1.0);
        }

        /// <summary>
        /// Determines health status from health score and metrics.
        /// </summary>
        private static DeviceHealthStatus DetermineHealthStatus(double healthScore, GpuMetrics metrics)
        {
            // Critical conditions override score
            if (metrics.Temperature >= 90)
            {
                return DeviceHealthStatus.Critical;
            }

            // Score-based status
            return healthScore switch
            {
                >= 0.9 => DeviceHealthStatus.Healthy,
                >= 0.7 => DeviceHealthStatus.Warning,
                >= 0.5 => DeviceHealthStatus.Critical,
                _ => DeviceHealthStatus.Error
            };
        }

        /// <summary>
        /// Builds human-readable status message.
        /// </summary>
        private static string BuildStatusMessage(GpuMetrics metrics, DeviceHealthStatus status)
        {
            var messages = new List<string>();

            if (status == DeviceHealthStatus.Healthy)
            {
                messages.Add("Operating normally");
            }

            if (metrics.Temperature >= 80)
            {
                messages.Add($"High temperature: {metrics.Temperature}°C");
            }

            if (metrics.IsThrottling)
            {
                messages.Add($"Throttling active: {metrics.ThrottleReasons}");
            }

            var memoryUsagePercent = (double)metrics.MemoryUsed / metrics.MemoryTotal * 100.0;
            if (memoryUsagePercent >= 90)
            {
                messages.Add($"High memory usage: {memoryUsagePercent:F1}%");
            }

            return messages.Count > 0 ? string.Join("; ", messages) : "Operating normally";
        }
    }
}
