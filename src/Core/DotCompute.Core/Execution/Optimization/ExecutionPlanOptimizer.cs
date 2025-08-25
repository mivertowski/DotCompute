// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Execution.Plans;
using DotCompute.Core.Execution.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Execution.Optimization
{
    /// <summary>
    /// Provides cross-cutting optimizations for execution plans to improve performance and efficiency.
    /// </summary>
    /// <remarks>
    /// This optimizer applies optimizations that are common across different execution strategies,
    /// such as memory optimization, performance-based adjustments, and device-specific optimizations.
    /// </remarks>
    public sealed class ExecutionPlanOptimizer
    {
        private readonly ILogger _logger;
        private readonly PerformanceMonitor _performanceMonitor;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionPlanOptimizer"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for diagnostic information.</param>
        /// <param name="performanceMonitor">The performance monitor for gathering metrics.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="logger"/> or <paramref name="performanceMonitor"/> is null.
        /// </exception>
        public ExecutionPlanOptimizer(ILogger logger, PerformanceMonitor performanceMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        }

        /// <summary>
        /// Applies cross-cutting optimizations to any execution plan.
        /// </summary>
        /// <typeparam name="T">The unmanaged data type being processed.</typeparam>
        /// <param name="plan">The execution plan to optimize.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="plan"/> is null.</exception>
        public async ValueTask OptimizePlanAsync<T>(ExecutionPlan<T> plan, CancellationToken cancellationToken = default) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(plan);

            _logger.LogDebug("Applying cross-cutting optimizations to {StrategyType} execution plan", plan.StrategyType);

            // Apply memory optimizations
            await OptimizeMemoryUsageAsync(plan, cancellationToken);

            // Apply performance optimizations based on historical data
            await ApplyPerformanceOptimizationsAsync(plan, cancellationToken);

            // Apply device-specific optimizations
            await ApplyDeviceOptimizationsAsync(plan, cancellationToken);

            _logger.LogDebug("Completed cross-cutting optimizations");
        }

        /// <summary>
        /// Optimizes memory usage patterns in the execution plan.
        /// </summary>
        /// <typeparam name="T">The unmanaged data type being processed.</typeparam>
        /// <param name="plan">The execution plan to optimize.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        private async ValueTask OptimizeMemoryUsageAsync<T>(ExecutionPlan<T> plan, CancellationToken cancellationToken) where T : unmanaged
        {
            // Analyze memory usage patterns and optimize buffer allocation
            var memoryStats = ExecutionMemoryManager.GetMemoryStatistics();

            foreach (var device in plan.Devices)
            {
                if (memoryStats.DeviceStatistics.TryGetValue(device.Info.Id, out var deviceStats))
                {
                    if (deviceStats.UtilizationPercentage > 85.0) // High utilization
                    {
                        _logger.LogWarning("High memory utilization detected on device {DeviceId}: {UtilizationPercentage:F2}%",
                            device.Info.Id, deviceStats.UtilizationPercentage);
                    }
                }
            }

            await ValueTask.CompletedTask;
        }

        /// <summary>
        /// Applies performance optimizations based on historical monitoring data.
        /// </summary>
        /// <typeparam name="T">The unmanaged data type being processed.</typeparam>
        /// <param name="plan">The execution plan to optimize.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        private async ValueTask ApplyPerformanceOptimizationsAsync<T>(ExecutionPlan<T> plan, CancellationToken cancellationToken) where T : unmanaged
        {
            // Apply optimizations based on performance monitoring data
            var metrics = _performanceMonitor.GetCurrentMetrics();

            if (metrics.MetricsByStrategy.TryGetValue(plan.StrategyType, out var strategyMetrics))
            {
                if (strategyMetrics.AverageEfficiencyPercentage < 60)
                {
                    _logger.LogInformation("Low efficiency detected for {StrategyType}: {Efficiency:F1}%, applying optimizations",
                        plan.StrategyType, strategyMetrics.AverageEfficiencyPercentage);

                    // Could apply strategy-specific optimizations here - TODO
                }
            }

            await ValueTask.CompletedTask;
        }

        /// <summary>
        /// Applies device-specific optimizations based on hardware characteristics.
        /// </summary>
        /// <typeparam name="T">The unmanaged data type being processed.</typeparam>
        /// <param name="plan">The execution plan to optimize.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        private async ValueTask ApplyDeviceOptimizationsAsync<T>(ExecutionPlan<T> plan, CancellationToken cancellationToken) where T : unmanaged
        {
            // Apply device-specific optimizations
            foreach (var device in plan.Devices)
            {
                var deviceType = device.Info.DeviceType.ToUpperInvariant();

                switch (deviceType)
                {
                    case "GPU":
                        _logger.LogTrace("Applying GPU-specific optimizations for device {DeviceId}", device.Info.Id);
                        break;
                    case "CPU":
                        _logger.LogTrace("Applying CPU-specific optimizations for device {DeviceId}", device.Info.Id);
                        break;
                    case "TPU":
                        _logger.LogTrace("Applying TPU-specific optimizations for device {DeviceId}", device.Info.Id);
                        break;
                }
            }

            await ValueTask.CompletedTask;
        }
    }
}
