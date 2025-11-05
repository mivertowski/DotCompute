// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Health;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL;

/// <summary>
/// OpenCL accelerator health monitoring implementation.
/// </summary>
public sealed partial class OpenCLAccelerator
{
    /// <summary>
    /// Gets a comprehensive health snapshot of the OpenCL device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the device health snapshot.</returns>
    /// <remarks>
    /// <para>
    /// OpenCL provides limited health monitoring capabilities compared to CUDA/NVML or Metal.
    /// The standard OpenCL API does not expose detailed hardware health metrics like temperature,
    /// power consumption, or real-time utilization.
    /// </para>
    ///
    /// <para>
    /// <b>Available Metrics (Standard OpenCL):</b>
    /// - Device availability status
    /// - Global memory capacity and cache information
    /// - Compute capability (compute units, clock frequency)
    /// - Driver and OpenCL version information
    /// </para>
    ///
    /// <para>
    /// <b>Vendor-Specific Extensions (Not Implemented):</b>
    /// - NVIDIA: cl_nv_device_attribute_query (temperature, thermal throttling)
    /// - AMD: cl_amd_device_attribute_query (temperature, fan speed)
    /// - Intel: cl_intel_device_attribute_query (power, frequency)
    /// </para>
    ///
    /// <para>
    /// <b>Limitations:</b>
    /// - No real-time utilization metrics (GPU/memory usage)
    /// - No temperature sensors (requires vendor extensions)
    /// - No power consumption metrics
    /// - No throttling status detection
    /// - No PCIe metrics
    /// </para>
    ///
    /// <para>
    /// For production health monitoring, consider using vendor-specific tools:
    /// - NVIDIA: nvidia-smi, NVML
    /// - AMD: rocm-smi, ROCm System Management Interface
    /// - Intel: intel-gpu-tools, Level Zero API
    /// </para>
    ///
    /// <para>
    /// <b>Performance:</b> Negligible overhead (sub-millisecond) as metrics are queried
    /// from cached device information.
    /// </para>
    /// </remarks>
    public ValueTask<DeviceHealthSnapshot> GetHealthSnapshotAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // If device not initialized, return unavailable
        if (_selectedDevice == null || _context == null || _context.IsDisposed)
        {
            return ValueTask.FromResult(DeviceHealthSnapshot.CreateUnavailable(
                deviceId: Info.Id,
                deviceName: Info.Name ?? "Unknown OpenCL Device",
                backendType: "OpenCL",
                reason: "OpenCL device not initialized or context disposed"
            ));
        }

        try
        {
            // Build sensor readings from available OpenCL device information
            var sensorReadings = BuildSensorReadings();

            // Calculate basic health score based on device availability
            var healthScore = CalculateHealthScore();

            // Determine status from availability
            var status = _selectedDevice.Available ? DeviceHealthStatus.Healthy : DeviceHealthStatus.Unknown;

            // Build status message
            var statusMessage = BuildStatusMessage();

            var snapshot = new DeviceHealthSnapshot
            {
                DeviceId = Info.Id,
                DeviceName = Info.Name ?? _selectedDevice.Name,
                BackendType = "OpenCL",
                Timestamp = DateTimeOffset.UtcNow,
                HealthScore = healthScore,
                Status = status,
                IsAvailable = _selectedDevice.Available && IsAvailable,
                SensorReadings = sensorReadings,
                ErrorCount = 0, // OpenCL doesn't track error counts natively
                ConsecutiveFailures = 0, // OpenCL doesn't track failure history
                IsThrottling = false, // Cannot detect throttling without vendor extensions
                StatusMessage = statusMessage
            };

            return ValueTask.FromResult(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect health snapshot for OpenCL device {DeviceName}", Info.Name);
            return ValueTask.FromResult(DeviceHealthSnapshot.CreateUnavailable(
                deviceId: Info.Id,
                deviceName: Info.Name ?? "Unknown OpenCL Device",
                backendType: "OpenCL",
                reason: $"Error collecting metrics: {ex.Message}"
            ));
        }
    }

    /// <summary>
    /// Gets current sensor readings from the OpenCL device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the collection of sensor readings.</returns>
    /// <remarks>
    /// OpenCL sensor readings are limited to static device properties.
    /// Real-time metrics (temperature, utilization, power) require vendor extensions.
    /// </remarks>
    public ValueTask<IReadOnlyList<SensorReading>> GetSensorReadingsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_selectedDevice == null || _context == null || _context.IsDisposed)
        {
            return ValueTask.FromResult<IReadOnlyList<SensorReading>>(Array.Empty<SensorReading>());
        }

        try
        {
            var readings = BuildSensorReadings();
            return ValueTask.FromResult<IReadOnlyList<SensorReading>>(readings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect sensor readings for OpenCL device {DeviceName}", Info.Name);
            return ValueTask.FromResult<IReadOnlyList<SensorReading>>(Array.Empty<SensorReading>());
        }
    }

    /// <summary>
    /// Builds sensor readings from OpenCL device information.
    /// </summary>
    private IReadOnlyList<SensorReading> BuildSensorReadings()
    {
        if (_selectedDevice == null)
        {
            return Array.Empty<SensorReading>();
        }

        var readings = new List<SensorReading>(6);

        // Memory metrics (static device properties)
        readings.Add(SensorReading.Create(
            SensorType.MemoryTotalBytes,
            (double)_selectedDevice.GlobalMemorySize,
            name: "Global Memory Capacity"
        ));

        // Memory cache size
        if (_selectedDevice.GlobalMemoryCacheSize > 0)
        {
            readings.Add(SensorReading.Create(
                SensorType.Custom,
                (double)_selectedDevice.GlobalMemoryCacheSize,
                minValue: 0.0,
                name: "Global Memory Cache Size"
            ));
        }

        // Compute capability metrics
        readings.Add(SensorReading.Create(
            SensorType.Custom,
            _selectedDevice.MaxComputeUnits,
            minValue: 1.0,
            name: "Compute Units"
        ));

        readings.Add(SensorReading.Create(
            SensorType.GraphicsClock,
            _selectedDevice.MaxClockFrequency,
            minValue: 0.0,
            name: "Max Clock Frequency"
        ));

        // Estimated compute capability (GFlops)
        readings.Add(SensorReading.Create(
            SensorType.Custom,
            _selectedDevice.EstimatedGFlops,
            minValue: 0.0,
            name: "Estimated Peak GFlops"
        ));

        // Local memory (shared memory equivalent)
        if (_selectedDevice.LocalMemorySize > 0)
        {
            readings.Add(SensorReading.Create(
                SensorType.Custom,
                (double)_selectedDevice.LocalMemorySize,
                minValue: 0.0,
                name: "Local Memory Size"
            ));
        }

        return readings;
    }

    /// <summary>
    /// Calculates health score based on device availability.
    /// </summary>
    private double CalculateHealthScore()
    {
        if (_selectedDevice == null || !_selectedDevice.Available)
        {
            return 0.0;
        }

        // Without real-time metrics, we can only provide binary health
        // Available and functional = 1.0, otherwise 0.0
        var baseScore = IsAvailable ? 1.0 : 0.5;

        // Check for compiler availability (important for OpenCL)
        if (!_selectedDevice.CompilerAvailable)
        {
            baseScore *= 0.8; // 20% penalty for missing compiler
        }

        // Check if device has sufficient memory (>= 256 MB)
        if (_selectedDevice.GlobalMemorySize < 256 * 1024 * 1024)
        {
            baseScore *= 0.9; // 10% penalty for limited memory
        }

        return Math.Clamp(baseScore, 0.0, 1.0);
    }

    /// <summary>
    /// Builds human-readable status message.
    /// </summary>
    private string BuildStatusMessage()
    {
        if (_selectedDevice == null)
        {
            return "Device not initialized";
        }

        var messages = new List<string>();

        if (_selectedDevice.Available && IsAvailable)
        {
            messages.Add("Device available and operational");
        }
        else if (!_selectedDevice.Available)
        {
            messages.Add("Device unavailable");
        }
        else if (!IsAvailable)
        {
            messages.Add("Context disposed or unavailable");
        }

        // Add vendor information
        messages.Add($"Vendor: {_selectedDevice.Vendor}");

        // Add capability information
        messages.Add($"OpenCL {_selectedDevice.OpenCLVersion}");

        // Note about limited monitoring
        messages.Add("Note: Advanced health metrics require vendor-specific extensions");

        return string.Join("; ", messages);
    }
}
