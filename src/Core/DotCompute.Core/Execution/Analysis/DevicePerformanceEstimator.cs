// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
namespace DotCompute.Core.Execution.Analysis
{
    /// <summary>
    /// Estimates and tracks device performance characteristics for optimal resource allocation.
    /// </summary>
    /// <remarks>
    /// This estimator uses both hardware characteristics and historical performance data
    /// to calculate performance scores for device selection and workload distribution.
    /// It maintains a cache of performance data to improve estimation accuracy over time.
    /// </remarks>
    /// <remarks>
    /// Initializes a new instance of the <see cref="DevicePerformanceEstimator"/> class.
    /// </remarks>
    /// <param name="logger">The logger instance for diagnostic information.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is null.</exception>
    public sealed class DevicePerformanceEstimator(ILogger logger)
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly Dictionary<string, DevicePerformanceCache> _performanceCache = [];

        /// <summary>
        /// Calculates a performance score for device selection based on hardware characteristics and historical data.
        /// </summary>
        /// <param name="device">The device to calculate the performance score for.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// A <see cref="ValueTask{T}"/> containing a performance score as a double value.
        /// Higher scores indicate better expected performance.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="device"/> is null.</exception>
        public async ValueTask<double> CalculateDeviceScoreAsync(IAccelerator device, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(device);


            await Task.CompletedTask.ConfigureAwait(false);
            var info = device.Info;

            // Base score from hardware characteristics
            var memoryScore = Math.Log10(info.TotalMemory / (1024.0 * 1024.0 * 1024.0)); // Log scale for GB
            var computeScore = info.ComputeUnits * info.MaxClockFrequency / 100000.0;
            var capabilityScore = info.ComputeCapability?.Major ?? 1;

            // Historical performance factor
            var historicalFactor = GetHistoricalPerformanceFactor(device.Info.Id);

            var score = (memoryScore * 0.3 + computeScore * 0.4 + capabilityScore * 0.2) * historicalFactor;

            _logger.LogTrace("Device {DeviceId} score: {Score:F2} (memory: {MemScore:F2}, compute: {CompScore:F2}, capability: {CapScore:F2}, historical: {HistFactor:F2})",
                device.Info.Id, score, memoryScore, computeScore, capabilityScore, historicalFactor);

            return score;
        }

        /// <summary>
        /// Estimates the execution time for a workload on a specific device.
        /// </summary>
        /// <param name="device">The target device.</param>
        /// <param name="workloadSize">The size of the workload (in computational units).</param>
        /// <param name="workloadType">A string describing the type of workload.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// A <see cref="ValueTask{T}"/> containing the estimated execution time in milliseconds.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="device"/> or <paramref name="workloadType"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="workloadSize"/> is less than or equal to zero.
        /// </exception>
        public async ValueTask<double> EstimateExecutionTimeAsync(
            IAccelerator device,

            long workloadSize,

            string workloadType,

            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(device);


            ArgumentNullException.ThrowIfNull(workloadType);


            if (workloadSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(workloadSize), "Workload size must be greater than zero.");
            }


            await Task.CompletedTask.ConfigureAwait(false);

            var deviceScore = await CalculateDeviceScoreAsync(device, cancellationToken);
            var workloadFactor = GetWorkloadTypeFactor(workloadType);

            // Base estimation: workload size / (device performance * workload efficiency)

            var baseTime = workloadSize / (deviceScore * workloadFactor * 1000.0);

            // Apply overhead factors

            var overheadFactor = GetOverheadFactor(device.Info.DeviceType);
            var estimatedTime = baseTime * overheadFactor;

            _logger.LogTrace("Estimated execution time for device {DeviceId}: {Time:F2}ms (workload: {Size}, type: {Type})",
                device.Info.Id, estimatedTime, workloadSize, workloadType);

            return estimatedTime;
        }

        /// <summary>
        /// Records performance data for a device to improve future estimations.
        /// </summary>
        /// <param name="deviceId">The device identifier.</param>
        /// <param name="actualTime">The actual execution time in milliseconds.</param>
        /// <param name="estimatedTime">The previously estimated execution time in milliseconds.</param>
        /// <param name="succeeded">Whether the execution completed successfully.</param>
        /// <remarks>
        /// This method updates the performance cache with real execution data to improve
        /// the accuracy of future performance estimations.
        /// </remarks>
        public void RecordPerformanceData(string deviceId, double actualTime, double estimatedTime, bool succeeded)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                throw new ArgumentException("Device ID cannot be null or empty.", nameof(deviceId));
            }


            if (!_performanceCache.TryGetValue(deviceId, out var cache))
            {
                cache = new DevicePerformanceCache();
                _performanceCache[deviceId] = cache;
            }

            // Update success rate
            var totalExecutions = cache.TotalExecutions + 1;
            cache.SuccessRate = (cache.SuccessRate * cache.TotalExecutions + (succeeded ? 1.0 : 0.0)) / totalExecutions;

            // Update efficiency based on estimation accuracy
            if (succeeded && estimatedTime > 0)
            {
                var accuracy = 1.0 - Math.Abs(actualTime - estimatedTime) / estimatedTime;
                cache.AverageEfficiency = (cache.AverageEfficiency * cache.TotalExecutions + accuracy * 100.0) / totalExecutions;
            }

            cache.TotalExecutions = totalExecutions;
            cache.LastUpdated = DateTime.UtcNow;

            _logger.LogTrace("Updated performance data for device {DeviceId}: success rate {SuccessRate:P2}, efficiency {Efficiency:F1}%",
                deviceId, cache.SuccessRate, cache.AverageEfficiency);
        }

        /// <summary>
        /// Gets the cached performance data for a device.
        /// </summary>
        /// <param name="deviceId">The device identifier.</param>
        /// <returns>
        /// The <see cref="DevicePerformanceCache"/> for the device, or null if no data is available.
        /// </returns>
        public DevicePerformanceCache? GetPerformanceCache(string deviceId) => _performanceCache.TryGetValue(deviceId, out var cache) ? cache : null;

        /// <summary>
        /// Clears all cached performance data.
        /// </summary>
        public void ClearCache()
        {
            _performanceCache.Clear();
            _logger.LogInfoMessage("Cleared device performance cache");
        }

        /// <summary>
        /// Gets the historical performance factor for a device.
        /// </summary>
        /// <param name="deviceId">The device identifier.</param>
        /// <returns>A factor between 0.1 and 1.5 based on historical performance.</returns>
        private double GetHistoricalPerformanceFactor(string deviceId)
        {
            if (!_performanceCache.TryGetValue(deviceId, out var cache))
            {
                return 1.0; // Default factor for devices with no history
            }

            // Combine success rate and efficiency for historical factor
            var factor = (cache.SuccessRate * 0.6) + (cache.AverageEfficiency / 100.0 * 0.4);

            // Clamp to reasonable bounds

            return Math.Max(0.1, Math.Min(1.5, factor));
        }

        /// <summary>
        /// Gets a workload type factor that affects performance estimation.
        /// </summary>
        /// <param name="workloadType">The type of workload.</param>
        /// <returns>A factor representing the relative efficiency for this workload type.</returns>
        private static double GetWorkloadTypeFactor(string workloadType)
        {
            return workloadType.ToUpper(CultureInfo.InvariantCulture) switch
            {
                "matrix_multiply" => 1.2,
                "convolution" => 1.1,
                "memory_copy" => 0.8,
                "reduce" => 0.9,
                "transform" => 1.0,
                _ => 1.0
            };
        }

        /// <summary>
        /// Gets an overhead factor based on device type.
        /// </summary>
        /// <param name="deviceType">The device type.</param>
        /// <returns>A factor representing typical overhead for this device type.</returns>
        private static double GetOverheadFactor(string deviceType)
        {
            return deviceType.ToUpperInvariant() switch
            {
                "GPU" => 1.1,  // GPU has some launch overhead
                "CPU" => 1.05, // CPU has minimal overhead
                "TPU" => 1.15, // TPU might have higher setup overhead
                _ => 1.1
            };
        }
    }
}
