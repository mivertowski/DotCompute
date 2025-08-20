// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Types;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Recovery;

/// <summary>
/// Central configuration for the Recovery Coordinator
/// </summary>
public class RecoveryCoordinatorConfiguration
{
    public TimeSpan MetricsReportInterval { get; set; } = TimeSpan.FromMinutes(5);
    public bool EnableGlobalRecovery { get; set; } = true;
    public bool EnableMetricsReporting { get; set; } = true;
    public int MaxConcurrentRecoveries { get; set; } = 10;
    
    // Individual component configurations
    public GpuRecoveryConfiguration GpuRecoveryConfig { get; set; } = GpuRecoveryConfiguration.Default;
    public MemoryRecoveryConfiguration MemoryRecoveryConfig { get; set; } = MemoryRecoveryConfiguration.Default;
    public CompilationFallbackConfiguration CompilationFallbackConfig { get; set; } = CompilationFallbackConfiguration.Default;
    public CircuitBreakerConfiguration CircuitBreakerConfig { get; set; } = CircuitBreakerConfiguration.Default;
    public PluginRecoveryConfiguration PluginRecoveryConfig { get; set; } = PluginRecoveryConfiguration.Default;
    
    public static RecoveryCoordinatorConfiguration Default => new();
    
    public override string ToString() => 
        $"MetricsInterval={MetricsReportInterval}, MaxConcurrent={MaxConcurrentRecoveries}, GlobalEnabled={EnableGlobalRecovery}";
}

/// <summary>
/// Configuration for compilation fallback behavior
/// </summary>
public class CompilationFallbackConfiguration
{
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromHours(4);
    public TimeSpan CacheCleanupInterval { get; set; } = TimeSpan.FromHours(1);
    public int MaxCacheEntries { get; set; } = 1000;
    public bool EnableProgressiveFallback { get; set; } = true;
    public bool EnableKernelSimplification { get; set; } = true;
    public bool EnableInterpreterMode { get; set; } = true;
    
    public List<CompilationFallbackStrategy> FallbackStrategies { get; set; } = new()
    {
        CompilationFallbackStrategy.ReduceOptimizations,
        CompilationFallbackStrategy.DisableFastMath,
        CompilationFallbackStrategy.SimplifyKernel,
        CompilationFallbackStrategy.AlternativeCompiler,
        CompilationFallbackStrategy.InterpreterMode
    };
    
    public static CompilationFallbackConfiguration Default => new();
    
    public override string ToString() => 
        $"CacheExpiration={CacheExpiration}, Strategies={FallbackStrategies.Count}, ProgressiveFallback={EnableProgressiveFallback}";
}

/// <summary>
/// Compilation fallback strategies
/// </summary>
public enum CompilationFallbackStrategy
{
    ReduceOptimizations,
    DisableFastMath,
    SimplifyKernel,
    AlternativeCompiler,
    InterpreterMode,
    CachedVersion,
    RollbackVersion
}

/// <summary>
/// Recovery statistics aggregated across all components
/// </summary>
public class RecoveryStatistics
{
    public DateTimeOffset Timestamp { get; set; }
    public RecoveryMetrics GlobalMetrics { get; set; } = null!;
    public DeviceHealthReport GpuHealthReport { get; set; } = null!;
    public MemoryPressureInfo MemoryPressureInfo { get; set; } = null!;
    public CompilationStatistics CompilationStatistics { get; set; } = null!;
    public CircuitBreakerStatistics CircuitBreakerStatistics { get; set; } = null!;
    public PluginHealthReport PluginHealthReport { get; set; } = null!;
    public int RegisteredStrategies { get; set; }
    public double OverallSystemHealth { get; set; }
    
    public override string ToString() => 
        $"SystemHealth={OverallSystemHealth:P1}, Strategies={RegisteredStrategies}, " +
        $"GlobalSuccess={GlobalMetrics.SuccessRate:P1}";
}

/// <summary>
/// Result of system health check
/// </summary>
public class SystemHealthResult
{
    public bool IsHealthy { get; set; }
    public double OverallHealth { get; set; }
    public List<ComponentHealthResult> ComponentResults { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? Error { get; set; }
    
    public override string ToString() => 
        IsHealthy 
            ? $"HEALTHY ({OverallHealth:P1}) - {ComponentResults.Count} components checked"
            : $"UNHEALTHY ({OverallHealth:P1}) - Error: {Error}";
}

/// <summary>
/// Health result for a specific system component
/// </summary>
public class ComponentHealthResult
{
    public string Component { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public double Health { get; set; }
    public string Message { get; set; } = string.Empty;
    public double OverallHealth { get; set; }
    public int HealthyPlugins { get; set; }
    public int TotalPlugins { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public DateTimeOffset LastCheck { get; set; } = DateTimeOffset.UtcNow;
    
    public override string ToString() => 
        $"{Component}: {(IsHealthy ? "HEALTHY" : "UNHEALTHY")} ({Health:P1}) - {Message}";
}

/// <summary>
/// Compilation statistics and performance metrics
/// </summary>
public class CompilationStatistics
{
    public int TotalKernels { get; set; }
    public int SuccessfulCompilations { get; set; }
    public double SuccessRate { get; set; }
    public double AverageAttemptsPerKernel { get; set; }
    public Dictionary<CompilationFallbackStrategy, int> StrategySuccessRates { get; set; } = new();
    public Dictionary<string, int> MostCommonErrors { get; set; } = new();
    public int CacheSize { get; set; }
    public double CacheHitRate { get; set; }
    
    public override string ToString() => 
        $"Success={SuccessRate:P1} ({SuccessfulCompilations}/{TotalKernels}), " +
        $"Cache={CacheSize} entries ({CacheHitRate:P1} hit rate)";
}

/// <summary>
/// Circuit breaker statistics
/// </summary>
public class CircuitBreakerStatistics
{
    public CircuitState GlobalState { get; set; }
    public double OverallFailureRate { get; set; }
    public long TotalRequests { get; set; }
    public long FailedRequests { get; set; }
    public int ConsecutiveFailures { get; set; }
    public Dictionary<string, ServiceStatistics> ServiceStatistics { get; set; } = new();
    public DateTimeOffset LastStateChange { get; set; }
    public int ActiveServices { get; set; }
    
    public override string ToString() => 
        $"State={GlobalState}, FailureRate={OverallFailureRate:P1} ({FailedRequests}/{TotalRequests}), " +
        $"Services={ActiveServices}";
}

/// <summary>
/// Statistics for individual services in circuit breaker
/// </summary>
public class ServiceStatistics
{
    public string ServiceName { get; set; } = string.Empty;
    public CircuitState State { get; set; }
    public double FailureRate { get; set; }
    public long TotalRequests { get; set; }
    public long FailedRequests { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public DateTimeOffset LastFailure { get; set; }
    
    public override string ToString() => 
        $"{ServiceName}: {State}, {FailureRate:P1} failure rate, {TotalRequests} requests";
}

/// <summary>
/// Context for compilation recovery operations
/// </summary>
public class CompilationRecoveryContext
{
    public string KernelName { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public CompilationOptions CompilationOptions { get; set; } = null!;
    public string TargetPlatform { get; set; } = string.Empty;
    public bool IsCompilationTimeout { get; set; }
    public bool IsSimplified { get; set; }
    public bool UseInterpreter { get; set; }
    public string? PluginPath { get; set; }
    
    // Modified during recovery
    public CompilationOptions? ModifiedOptions { get; set; }
    public KernelInterpreter? InterpreterInstance { get; set; }
    public CachedCompilationResult? CachedResult { get; set; }
    
    public CompilationRecoveryContext Clone()
    {
        return new CompilationRecoveryContext
        {
            KernelName = KernelName,
            SourceCode = SourceCode,
            CompilationOptions = CompilationOptions.Clone(),
            TargetPlatform = TargetPlatform,
            IsCompilationTimeout = IsCompilationTimeout,
            IsSimplified = IsSimplified,
            UseInterpreter = UseInterpreter,
            PluginPath = PluginPath
        };
    }
}

/// <summary>
/// Result of compilation fallback operation
/// </summary>
public class CompilationFallbackResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public CompilationFallbackStrategy? FinalStrategy { get; set; }
    public CompilationOptions? FinalOptions { get; set; }
    public object? CompiledKernel { get; set; }
    public List<CompilationAttempt> Attempts { get; set; } = new();
    public double TotalDuration { get; set; }
    
    public override string ToString() => 
        Success 
            ? $"SUCCESS with {FinalStrategy} in {TotalDuration}ms ({Attempts.Count} attempts)"
            : $"FAILED after {Attempts.Count} attempts: {Error}";
}

/// <summary>
/// Individual compilation attempt information
/// </summary>
public class CompilationAttempt
{
    public CompilationFallbackStrategy Strategy { get; set; }
    public CompilationOptions Options { get; set; } = null!;
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Tracks compilation history for a specific kernel
/// </summary>
public class CompilationHistory : IDisposable
{
    private readonly string _kernelName;
    private readonly List<Exception> _recentErrors = new();
    private readonly List<CompilationFallbackStrategy> _attemptedStrategies = new();
    private int _failureCount = 0;
    private int _totalAttempts = 0;
    private CompilationFallbackStrategy? _successfulStrategy;
    private bool _disposed;

    public string KernelName => _kernelName;
    public int FailureCount => _failureCount;
    public int TotalAttempts => _totalAttempts;
    public bool LastCompilationSuccessful => _successfulStrategy.HasValue;
    public CompilationFallbackStrategy? SuccessfulStrategy => _successfulStrategy;
    public IReadOnlyList<Exception> RecentErrors => _recentErrors.AsReadOnly();

    public CompilationHistory(string kernelName)
    {
        _kernelName = kernelName ?? throw new ArgumentNullException(nameof(kernelName));
    }

    public void RecordFailure(Exception error, CompilationOptions options)
    {
        _failureCount++;
        _totalAttempts++;
        _recentErrors.Add(error);
        
        // Keep only recent errors
        if (_recentErrors.Count > 20)
        {
            _recentErrors.RemoveAt(0);
        }
    }

    public void RecordSuccess(CompilationFallbackStrategy strategy)
    {
        _successfulStrategy = strategy;
        _totalAttempts++;
        _failureCount = 0; // Reset consecutive failures
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _recentErrors.Clear();
            _attemptedStrategies.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Cached compilation result
/// </summary>
public class CachedCompilationResult
{
    public string KernelHash { get; set; } = string.Empty;
    public string KernelName { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public CompilationOptions SuccessfulOptions { get; set; } = null!;
    public DateTimeOffset Timestamp { get; set; }
    public DateTimeOffset ExpirationTime { get; set; }
    
    public bool IsValid => DateTimeOffset.UtcNow < ExpirationTime;
}

/// <summary>
/// Simplified kernel interpreter for fallback execution
/// </summary>
public class KernelInterpreter : IDisposable
{
    private readonly string _sourceCode;
    private readonly Microsoft.Extensions.Logging.ILogger _logger;
    private bool _prepared;
    private bool _disposed;

    public KernelInterpreter(string sourceCode, Microsoft.Extensions.Logging.ILogger logger)
    {
        _sourceCode = sourceCode ?? throw new ArgumentNullException(nameof(sourceCode));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PrepareAsync(CancellationToken cancellationToken = default)
    {
        if (_prepared)
        {
            _logger.LogDebug("Kernel interpreter already prepared");
            return;
        }
        
        // Kernel interpretation preparation would go here
        await Task.Delay(100, cancellationToken);
        _prepared = true;
        _logger.LogDebug("Kernel interpreter prepared for source code length {Length}", _sourceCode.Length);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// Extension methods for CompilationOptions
/// </summary>
public static class CompilationOptionsExtensions
{
    public static CompilationOptions Clone(this CompilationOptions options)
    {
        return new CompilationOptions
        {
            OptimizationLevel = options.OptimizationLevel,
            FastMath = options.FastMath,
            AggressiveOptimizations = options.AggressiveOptimizations,
            StrictFloatingPoint = options.StrictFloatingPoint,
            CompilerBackend = options.CompilerBackend,
            ForceInterpretedMode = options.ForceInterpretedMode
        };
    }
}