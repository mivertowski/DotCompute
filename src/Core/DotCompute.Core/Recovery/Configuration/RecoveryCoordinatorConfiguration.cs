// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Configuration;

/// <summary>
/// Central configuration for the Recovery Coordinator that manages all recovery operations across the system.
/// This configuration controls global recovery behavior, metrics reporting, and individual component recovery settings.
/// </summary>
/// <remarks>
/// The Recovery Coordinator is responsible for orchestrating recovery across multiple components including:
/// - GPU recovery operations
/// - Memory pressure management
/// - Compilation fallback strategies
/// - Circuit breaker patterns
/// - Plugin health monitoring
/// </remarks>
public class RecoveryCoordinatorConfiguration
{
    /// <summary>
    /// Gets or sets the interval at which metrics are reported and collected.
    /// </summary>
    /// <value>The default value is 5 minutes.</value>
    public TimeSpan MetricsReportInterval { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Gets or sets a value indicating whether global recovery operations are enabled.
    /// </summary>
    /// <value>The default value is true.</value>
    public bool EnableGlobalRecovery { get; set; } = true;
    
    /// <summary>
    /// Gets or sets a value indicating whether metrics reporting is enabled.
    /// </summary>
    /// <value>The default value is true.</value>
    public bool EnableMetricsReporting { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the maximum number of concurrent recovery operations that can be executed simultaneously.
    /// </summary>
    /// <value>The default value is 10.</value>
    public int MaxConcurrentRecoveries { get; set; } = 10;

    /// <summary>
    /// Gets or sets the GPU recovery configuration.
    /// </summary>
    /// <value>The default configuration from <see cref="GpuRecoveryConfiguration.Default"/>.</value>
    public GpuRecoveryConfiguration GpuRecoveryConfig { get; set; } = GpuRecoveryConfiguration.Default;
    
    /// <summary>
    /// Gets or sets the memory recovery configuration.
    /// </summary>
    /// <value>The default configuration from <see cref="MemoryRecoveryConfiguration.Default"/>.</value>
    public MemoryRecoveryConfiguration MemoryRecoveryConfig { get; set; } = MemoryRecoveryConfiguration.Default;
    
    /// <summary>
    /// Gets or sets the compilation fallback configuration.
    /// </summary>
    /// <value>The default configuration from <see cref="CompilationFallbackConfiguration.Default"/>.</value>
    public CompilationFallbackConfiguration CompilationFallbackConfig { get; set; } = CompilationFallbackConfiguration.Default;
    
    /// <summary>
    /// Gets or sets the circuit breaker configuration.
    /// </summary>
    /// <value>The default configuration from <see cref="CircuitBreakerConfiguration.Default"/>.</value>
    public CircuitBreakerConfiguration CircuitBreakerConfig { get; set; } = CircuitBreakerConfiguration.Default;
    
    /// <summary>
    /// Gets or sets the plugin recovery configuration.
    /// </summary>
    /// <value>The default configuration from <see cref="PluginRecoveryConfiguration.Default"/>.</value>
    public PluginRecoveryConfiguration PluginRecoveryConfig { get; set; } = PluginRecoveryConfiguration.Default;

    /// <summary>
    /// Gets the default configuration instance.
    /// </summary>
    /// <value>A new instance of <see cref="RecoveryCoordinatorConfiguration"/> with default values.</value>
    public static RecoveryCoordinatorConfiguration Default => new();

    /// <summary>
    /// Returns a string representation of the configuration.
    /// </summary>
    /// <returns>A string containing key configuration values.</returns>
    public override string ToString()
        => $"MetricsInterval={MetricsReportInterval}, MaxConcurrent={MaxConcurrentRecoveries}, GlobalEnabled={EnableGlobalRecovery}";
}