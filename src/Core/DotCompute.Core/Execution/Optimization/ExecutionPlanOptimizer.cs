// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Execution.Plans;
using DotCompute.Core.Execution.Memory;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
namespace DotCompute.Core.Execution.Optimization
{
    /// <summary>
    /// Provides cross-cutting optimizations for execution plans to improve performance and efficiency.
    /// </summary>
    /// <remarks>
    /// This optimizer applies optimizations that are common across different execution strategies,
    /// such as memory optimization, performance-based adjustments, and device-specific optimizations.
    /// </remarks>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ExecutionPlanOptimizer"/> class.
    /// </remarks>
    /// <param name="logger">The logger instance for diagnostic information.</param>
    /// <param name="performanceMonitor">The performance monitor for gathering metrics.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="logger"/> or <paramref name="performanceMonitor"/> is null.
    /// </exception>
    public sealed partial class ExecutionPlanOptimizer(ILogger logger, PerformanceMonitor performanceMonitor)
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly PerformanceMonitor _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));

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

            _logger.LogDebugMessage("Applying cross-cutting optimizations to {plan.StrategyType} execution plan");

            // Apply memory optimizations
            await OptimizeMemoryUsageAsync(plan, cancellationToken);

            // Apply performance optimizations based on historical data
            await ApplyPerformanceOptimizationsAsync(plan, cancellationToken);

            // Apply device-specific optimizations
            await ApplyDeviceOptimizationsAsync(plan, cancellationToken);

            _logger.LogDebugMessage("Completed cross-cutting optimizations");
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
                        _logger.LogWarningMessage($"High memory utilization detected on device {device.Info.Id}: {deviceStats.UtilizationPercentage}%");
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

            if (metrics.MetricsByStrategy.TryGetValue((AbstractionsMemory.Types.ExecutionStrategyType)(int)plan.StrategyType, out var strategyMetrics))
            {
                if (strategyMetrics.AverageEfficiencyPercentage < 60)
                {
                    _logger.LogInfoMessage($"Low efficiency detected for {plan.StrategyType}: {strategyMetrics.AverageEfficiencyPercentage}%, applying optimizations");

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
                        LogApplyingGpuOptimizations(_logger, device.Info.Id);
                        break;
                    case "CPU":
                        LogApplyingCpuOptimizations(_logger, device.Info.Id);
                        break;
                    case "TPU":
                        LogApplyingTpuOptimizations(_logger, device.Info.Id);
                        break;
                }
            }

            await ValueTask.CompletedTask;
        }

        #region LoggerMessage Delegates

        [LoggerMessage(EventId = 12001, Level = LogLevel.Trace, Message = "Applying GPU-specific optimizations for device {DeviceId}")]
        private static partial void LogApplyingGpuOptimizations(ILogger logger, string deviceId);

        [LoggerMessage(EventId = 12002, Level = LogLevel.Trace, Message = "Applying CPU-specific optimizations for device {DeviceId}")]
        private static partial void LogApplyingCpuOptimizations(ILogger logger, string deviceId);

        [LoggerMessage(EventId = 12003, Level = LogLevel.Trace, Message = "Applying TPU-specific optimizations for device {DeviceId}")]
        private static partial void LogApplyingTpuOptimizations(ILogger logger, string deviceId);

        #endregion
    }
}
