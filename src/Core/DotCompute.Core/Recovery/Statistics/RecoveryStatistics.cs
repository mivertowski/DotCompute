// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Recovery.Models;

namespace DotCompute.Core.Recovery.Statistics;

/// <summary>
/// Aggregated recovery statistics from all system components, providing a comprehensive view
/// of system health and recovery performance across the entire DotCompute infrastructure.
/// </summary>
/// <remarks>
/// This class consolidates statistics from multiple recovery subsystems including:
/// - Global recovery metrics and success rates
/// - GPU health and device status
/// - Memory pressure and utilization
/// - Compilation success rates and fallback usage
/// - Circuit breaker states and failure rates
/// - Plugin health and availability
/// 
/// The statistics are collected periodically and used for monitoring, alerting, and
/// system optimization decisions.
/// </remarks>
public class RecoveryStatistics
{
    /// <summary>
    /// Gets or sets the timestamp when these statistics were collected.
    /// </summary>
    /// <value>The UTC timestamp of statistics collection.</value>
    public DateTimeOffset Timestamp { get; set; }
    
    /// <summary>
    /// Gets or sets the global recovery metrics aggregated across all components.
    /// </summary>
    /// <value>The global metrics including success rates and recovery counts.</value>
    public RecoveryMetrics GlobalMetrics { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the GPU health report containing device status and performance metrics.
    /// </summary>
    /// <value>The current GPU health status and diagnostics.</value>
    public DeviceHealthReport GpuHealthReport { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the memory pressure information including utilization and availability.
    /// </summary>
    /// <value>The current memory status and pressure indicators.</value>
    public MemoryPressureInfo MemoryPressureInfo { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the compilation statistics including success rates and fallback usage.
    /// </summary>
    /// <value>The compilation performance and error statistics.</value>
    public CompilationStatistics CompilationStatistics { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the circuit breaker statistics including state and failure rates.
    /// </summary>
    /// <value>The circuit breaker status and performance metrics.</value>
    public CircuitBreakerStatistics CircuitBreakerStatistics { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the plugin health report containing plugin status and availability.
    /// </summary>
    /// <value>The current plugin health and diagnostics.</value>
    public PluginHealthReport PluginHealthReport { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the total number of registered recovery strategies across all components.
    /// </summary>
    /// <value>The count of active recovery strategies.</value>
    public int RegisteredStrategies { get; set; }
    
    /// <summary>
    /// Gets or sets the overall system health as a percentage (0.0 to 1.0).
    /// This value is calculated based on all component health metrics.
    /// </summary>
    /// <value>A value between 0.0 (completely unhealthy) and 1.0 (completely healthy).</value>
    public double OverallSystemHealth { get; set; }

    /// <summary>
    /// Returns a string representation of the recovery statistics.
    /// </summary>
    /// <returns>A formatted string containing key statistics values.</returns>
    public override string ToString()
        => $"SystemHealth={OverallSystemHealth:P1}, Strategies={RegisteredStrategies}, " +
        $"GlobalSuccess={GlobalMetrics.SuccessRate:P1}";
}