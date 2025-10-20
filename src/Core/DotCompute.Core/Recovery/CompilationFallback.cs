// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Recovery.Types;
using Microsoft.Extensions.Logging;
using CompilationRecoveryContext = DotCompute.Core.Recovery.Compilation.CompilationRecoveryContext;
using CompilationHistory = DotCompute.Core.Recovery.Compilation.CompilationHistory;
using CachedCompilationResult = DotCompute.Core.Recovery.Compilation.CachedCompilationResult;
using CompilationFallbackResult = DotCompute.Core.Recovery.Compilation.CompilationFallbackResult;
using DotCompute.Core.Recovery.Configuration;
using DotCompute.Core.Recovery.Compilation;
using CompilationStatistics = DotCompute.Core.Recovery.Statistics.CompilationStatistics;
using CompilationAttempt = DotCompute.Core.Recovery.Compilation.CompilationAttempt;
using System;

namespace DotCompute.Core.Recovery;

/// <summary>
/// Comprehensive compilation fallback system with progressive optimization reduction,
/// alternative compilation strategies, and interpreter fallbacks
/// </summary>
public sealed partial class CompilationFallback : BaseRecoveryStrategy<CompilationRecoveryContext>
{
    private readonly ConcurrentDictionary<string, CompilationHistory> _compilationHistory;
    private readonly ConcurrentDictionary<string, CachedCompilationResult> _compilationCache;
    private readonly CompilationFallbackConfiguration _config;
    private readonly Timer _cacheCleanupTimer;
    private bool _disposed;
    /// <summary>
    /// Gets or sets the capability.
    /// </summary>
    /// <value>The capability.</value>

    public override RecoveryCapability Capability => RecoveryCapability.MemoryErrors;
    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    /// <value>The priority.</value>
    public override int Priority => 90;
    /// <summary>
    /// Initializes a new instance of the CompilationFallback class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="config">The config.</param>

    public CompilationFallback(ILogger<CompilationFallback> logger, CompilationFallbackConfiguration? config = null)
        : base(logger)
    {
        _config = config ?? CompilationFallbackConfiguration.Default;
        _compilationHistory = new ConcurrentDictionary<string, CompilationHistory>();
        _compilationCache = new ConcurrentDictionary<string, CachedCompilationResult>();

        // Periodic cache cleanup

        _cacheCleanupTimer = new Timer(CleanupCache, null,
            _config.CacheCleanupInterval, _config.CacheCleanupInterval);

        LogSystemInitialized(Logger, _config.FallbackStrategies.Count);
    }
    /// <summary>
    /// Determines whether handle.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="context">The context.</param>
    /// <returns>true if the condition is met; otherwise, false.</returns>

    public override bool CanHandle(Exception exception, CompilationRecoveryContext context)
    {
        return exception switch
        {
            CompilationException => true,
            InvalidOperationException ioEx when ioEx.Message.Contains("compil", StringComparison.OrdinalIgnoreCase) => true,
            ArgumentException argEx when argEx.Message.Contains("kernel", StringComparison.OrdinalIgnoreCase) => true,
            TimeoutException when context.IsCompilationTimeout => true,
            _ => false
        };
    }
    /// <summary>
    /// Gets recover asynchronously.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="context">The context.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override async Task<RecoveryResult> RecoverAsync(
        Exception exception,
        CompilationRecoveryContext context,
        RecoveryOptions options,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var kernelHash = CalculateKernelHash(context.SourceCode);


        LogCompilationErrorDetected(Logger, context.KernelName, exception.Message);

        // Check cache first
        if (_compilationCache.TryGetValue(kernelHash, out var cached) && cached.IsValid)
        {
            LogUsingCachedResult(Logger, context.KernelName);
            return Success($"Used cached compilation result", stopwatch.Elapsed);
        }

        // Get or create compilation history
        var history = _compilationHistory.GetOrAdd(kernelHash, _ => new CompilationHistory(context.KernelName));
        history.RecordFailure(exception, context.CompilationOptions);

        try
        {
            // Determine fallback strategy based on error and history
            var strategy = DetermineFallbackStrategy(exception, context, history);
            LogUsingFallbackStrategy(Logger, strategy.ToString());

            var result = await ExecuteFallbackStrategyAsync(strategy, context, history, options, cancellationToken);


            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;


            if (result.Success)
            {
                history.RecordSuccess((CompilationFallbackStrategy)strategy);

                // Cache successful compilation

                CacheCompilationResult(kernelHash, context, result);


                LogFallbackSuccessful(Logger, strategy.ToString(), context.KernelName, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                LogFallbackFailed(Logger, strategy.ToString(), context.KernelName, result.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogExceptionDuringFallback(Logger, ex, context.KernelName);
            return Failure($"Fallback process failed: {ex.Message}", ex, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Attempts compilation with progressive optimization reduction
    /// </summary>
    public async Task<CompilationFallbackResult> CompileWithProgressiveFallbackAsync(
        string kernelName,
        string sourceCode,
        CompilationOptions originalOptions,
        CancellationToken cancellationToken = default)
    {
        var context = new CompilationRecoveryContext
        {
            KernelName = kernelName,
            SourceCode = sourceCode,
            CompilationOptions = originalOptions,
            TargetPlatform = DetectTargetPlatform(originalOptions)
        };

        var attempts = new List<CompilationAttempt>();
        var currentOptions = originalOptions.Clone();


        foreach (var strategy in _config.FallbackStrategies)
        {
            try
            {
                LogAttemptingStrategy(Logger, strategy.ToString(), kernelName);


                var modifiedOptions = ApplyFallbackStrategy((CompilationFallbackStrategy)strategy, currentOptions, context);
                var attempt = new CompilationAttempt
                {
                    Strategy = strategy,
                    Options = modifiedOptions,
                    StartTime = DateTimeOffset.UtcNow
                };

                // Simulate compilation attempt (would integrate with actual compiler)
                var compileResult = await SimulateCompilationAsync(context, modifiedOptions, cancellationToken);


                attempt.EndTime = DateTimeOffset.UtcNow;
                attempt.Duration = attempt.EndTime - attempt.StartTime;
                attempt.Success = compileResult.Success;
                attempt.Error = compileResult.Error;


                attempts.Add(attempt);

                if (compileResult.Success)
                {
                    LogCompilationSuccessful(Logger, strategy.ToString(), kernelName);


                    return new CompilationFallbackResult
                    {
                        Success = true,
                        FinalStrategy = strategy,
                        FinalOptions = modifiedOptions,
                        CompiledKernel = compileResult.CompiledKernel,
                        Attempts = attempts,
                        TotalDuration = attempts.Sum(a => a.Duration.TotalMilliseconds)
                    };
                }


                currentOptions = modifiedOptions; // Use for next iteration
            }
            catch (Exception ex)
            {
                LogStrategyFailed(Logger, ex, strategy.ToString(), kernelName);


                attempts.Add(new CompilationAttempt
                {
                    Strategy = strategy,
                    StartTime = DateTimeOffset.UtcNow,
                    EndTime = DateTimeOffset.UtcNow,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        // All strategies failed
        LogAllStrategiesFailed(Logger, kernelName);


        return new CompilationFallbackResult
        {
            Success = false,
            Error = "All fallback compilation strategies failed",
            Attempts = attempts,
            TotalDuration = attempts.Sum(a => a.Duration.TotalMilliseconds)
        };
    }

    /// <summary>
    /// Gets compilation statistics and recommendations
    /// </summary>
    public CompilationStatistics GetCompilationStatistics()
    {
        var totalCompilations = _compilationHistory.Count;
        var successfulCompilations = _compilationHistory.Values.Count(h => h.LastCompilationSuccessful);
        var averageAttemptsPerKernel = _compilationHistory.Values.Average(h => h.TotalAttempts);


        var strategySuccess = _compilationHistory.Values
            .Where(h => h.SuccessfulStrategy.HasValue)
            .GroupBy(h => h.SuccessfulStrategy!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var mostCommonErrors = _compilationHistory.Values
            .SelectMany(h => h.RecentErrors)
            .GroupBy(e => e.Message)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .ToDictionary(g => g.Key, g => g.Count());

        return new CompilationStatistics
        {
            TotalKernels = totalCompilations,
            SuccessfulCompilations = successfulCompilations,
            SuccessRate = totalCompilations > 0 ? (double)successfulCompilations / totalCompilations : 0.0,
            AverageAttemptsPerKernel = averageAttemptsPerKernel,
            StrategySuccessRates = strategySuccess,
            MostCommonErrors = mostCommonErrors,
            CacheSize = _compilationCache.Count,
            CacheHitRate = CalculateCacheHitRate()
        };
    }

    private static CompilationFallbackStrategy DetermineFallbackStrategy(
        Exception error,
        CompilationRecoveryContext context,
        CompilationHistory history)
    {
        // If a strategy worked before for this kernel, try it first
        if (history.SuccessfulStrategy.HasValue)
        {
            return history.SuccessfulStrategy.Value;
        }

        // Strategy based on error type
        return error switch
        {
            CompilationException compEx when compEx.Message.Contains("optimization", StringComparison.CurrentCulture) => CompilationFallbackStrategy.ReduceOptimizations,
            CompilationException compEx when compEx.Message.Contains("memory", StringComparison.CurrentCulture) => CompilationFallbackStrategy.SimplifyKernel,
            TimeoutException => CompilationFallbackStrategy.DisableFastMath,
            _ => history.FailureCount > 2 ? CompilationFallbackStrategy.InterpreterMode : CompilationFallbackStrategy.ReduceOptimizations
        };
    }

    private async Task<RecoveryResult> ExecuteFallbackStrategyAsync(
        CompilationFallbackStrategy strategy,
        CompilationRecoveryContext context,
        CompilationHistory history,
        RecoveryOptions options,
        CancellationToken cancellationToken)
    {
        return strategy switch
        {
            CompilationFallbackStrategy.ReduceOptimizations => await ReduceOptimizationsAsync(context, cancellationToken),
            CompilationFallbackStrategy.DisableFastMath => await DisableFastMathAsync(context, cancellationToken),
            CompilationFallbackStrategy.SimplifyKernel => await SimplifyKernelAsync(context, cancellationToken),
            CompilationFallbackStrategy.AlternativeCompiler => await AlternativeCompilerAsync(context, cancellationToken),
            CompilationFallbackStrategy.InterpreterMode => await InterpreterModeAsync(context, cancellationToken),
            CompilationFallbackStrategy.CachedVersion => await UseCachedVersionAsync(context, cancellationToken),
            _ => Failure("Unknown compilation fallback strategy")
        };
    }

    private async Task<RecoveryResult> ReduceOptimizationsAsync(CompilationRecoveryContext context, CancellationToken cancellationToken)
    {
        LogReducingOptimization(Logger, context.KernelName);


        var modifiedOptions = context.CompilationOptions.Clone();
        modifiedOptions.OptimizationLevel = OptimizationLevel.None;
        modifiedOptions.AggressiveOptimizations = false;


        context.ModifiedOptions = modifiedOptions;

        // Simulate compilation with reduced optimizations

        var result = await SimulateCompilationAsync(context, modifiedOptions, cancellationToken);


        return result.Success

            ? Success("Compilation successful with reduced optimizations", TimeSpan.FromMilliseconds(200))
            : Failure($"Compilation failed even with reduced optimizations: {result.Error}");
    }

    private async Task<RecoveryResult> DisableFastMathAsync(CompilationRecoveryContext context, CancellationToken cancellationToken)
    {
        LogDisablingFastMath(Logger, context.KernelName);


        var modifiedOptions = context.CompilationOptions.Clone();
        modifiedOptions.FastMath = false;
        modifiedOptions.StrictFloatingPoint = true;


        context.ModifiedOptions = modifiedOptions;


        var result = await SimulateCompilationAsync(context, modifiedOptions, cancellationToken);


        return result.Success

            ? Success("Compilation successful with disabled fast math", TimeSpan.FromMilliseconds(250))
            : Failure($"Compilation failed with disabled fast math: {result.Error}");
    }

    private async Task<RecoveryResult> SimplifyKernelAsync(CompilationRecoveryContext context, CancellationToken cancellationToken)
    {
        LogAttemptingSimplification(Logger, context.KernelName);


        try
        {
            var simplifiedSource = SimplifyKernelSource(context.SourceCode);
            var simplifiedContext = context.Clone();
            simplifiedContext.SourceCode = simplifiedSource;
            simplifiedContext.IsSimplified = true;


            var result = await SimulateCompilationAsync(simplifiedContext, context.CompilationOptions, cancellationToken);


            return result.Success

                ? Success("Compilation successful with simplified kernel", TimeSpan.FromMilliseconds(300))
                : Failure($"Simplified kernel compilation failed: {result.Error}");
        }
        catch (Exception ex)
        {
            LogSimplificationFailed(Logger, ex, context.KernelName);
            return Failure($"Kernel simplification failed: {ex.Message}", ex);
        }
    }

    private async Task<RecoveryResult> AlternativeCompilerAsync(CompilationRecoveryContext context, CancellationToken cancellationToken)
    {
        LogTryingAlternativeCompiler(Logger, context.KernelName);

        // Try different compiler backends

        var alternativeCompilers = new[] { "LLVM", "GCC", "Clang", "CPU" };


        foreach (var compiler in alternativeCompilers)
        {
            try
            {
                var modifiedOptions = context.CompilationOptions.Clone();
                modifiedOptions.CompilerBackend = compiler;


                var result = await SimulateCompilationAsync(context, modifiedOptions, cancellationToken);


                if (result.Success)
                {
                    return Success($"Compilation successful with {compiler} compiler", TimeSpan.FromMilliseconds(400));
                }
            }
            catch (Exception ex)
            {
                LogAlternativeCompilerFailed(Logger, ex, compiler, context.KernelName);
            }
        }


        return Failure("All alternative compilers failed");
    }

    private async Task<RecoveryResult> InterpreterModeAsync(CompilationRecoveryContext context, CancellationToken cancellationToken)
    {
        LogFallingBackToInterpreter(Logger, context.KernelName);


        try
        {
            // Create interpreted version of the kernel
            var interpreter = new KernelInterpreter(context.SourceCode, Logger);
            await interpreter.PrepareAsync(cancellationToken);


            context.InterpreterInstance = interpreter;
            context.UseInterpreter = true;


            return Success("Kernel prepared for interpreter mode", TimeSpan.FromMilliseconds(100));
        }
        catch (Exception ex)
        {
            LogInterpreterModeSetupFailed(Logger, ex, context.KernelName);
            return Failure($"Interpreter mode failed: {ex.Message}", ex);
        }
    }

    private Task<RecoveryResult> UseCachedVersionAsync(CompilationRecoveryContext context, CancellationToken cancellationToken)
    {
        var kernelHash = CalculateKernelHash(context.SourceCode);

        // Look for similar cached compilations

        var similarCached = _compilationCache.Values
            .Where(c => c.IsValid && c.KernelName == context.KernelName)
            .OrderByDescending(c => c.Timestamp)
            .FirstOrDefault();


        if (similarCached != null)
        {
            LogUsingSimilarCachedCompilation(Logger, context.KernelName);
            context.CachedResult = similarCached;
            return Task.FromResult(Success("Using similar cached compilation result", TimeSpan.FromMilliseconds(10)));
        }


        return Task.FromResult(Failure("No suitable cached version found"));
    }

    private static CompilationOptions ApplyFallbackStrategy(
        CompilationFallbackStrategy strategy,

        CompilationOptions options,

        CompilationRecoveryContext context)
    {
        var modifiedOptions = options.Clone();


        switch (strategy)
        {
            case CompilationFallbackStrategy.ReduceOptimizations:
                modifiedOptions.OptimizationLevel = OptimizationLevel.None;
                modifiedOptions.AggressiveOptimizations = false;
                break;


            case CompilationFallbackStrategy.DisableFastMath:
                modifiedOptions.FastMath = false;
                modifiedOptions.StrictFloatingPoint = true;
                break;


            case CompilationFallbackStrategy.SimplifyKernel:
                // Kernel source modification handled separately
                modifiedOptions.OptimizationLevel = OptimizationLevel.Size;
                break;


            case CompilationFallbackStrategy.AlternativeCompiler:
                modifiedOptions.CompilerBackend = "CPU"; // Fallback to CPU
                break;


            case CompilationFallbackStrategy.InterpreterMode:
                modifiedOptions.ForceInterpretedMode = true;
                break;
        }


        return modifiedOptions;
    }

    private static async Task<(bool Success, string? Error, object? CompiledKernel)> SimulateCompilationAsync(
        CompilationRecoveryContext context,
        CompilationOptions options,
        CancellationToken cancellationToken)
    {
        // Simulate compilation time
        var delay = context.IsSimplified ? 100 :

                   options.OptimizationLevel == OptimizationLevel.None ? 200 : 500;


        await Task.Delay(delay, cancellationToken);

        // Simulate success rate based on strategy

        var successRate = options.OptimizationLevel switch
        {
            OptimizationLevel.None => 0.95,
            OptimizationLevel.O1 => 0.90,
            OptimizationLevel.Default => 0.80,
            OptimizationLevel.O3 => 0.70,
            OptimizationLevel.Size => 0.65,
            _ => 0.85
        };


        if (context.UseInterpreter)
        {
            successRate = 0.99; // Interpreter almost always works
        }

#pragma warning disable CA5394 // Random is used for compilation simulation/testing, not security
        var success = Random.Shared.NextDouble() < successRate;
#pragma warning restore CA5394

        return success

            ? (true, null, new { KernelName = context.KernelName, Options = options })
            : (false, "Simulated compilation error", null);
    }

    private static string SimplifyKernelSource(string sourceCode)
    {
        // Simplified kernel source transformation
        var simplified = new StringBuilder(sourceCode);

        // Remove complex optimizations

        _ = simplified.Replace("#pragma unroll", "// #pragma unroll");
        _ = simplified.Replace("__attribute__((always_inline))", "");

        // Simplify complex expressions (basic pattern matching)
        // This would be more sophisticated in a real implementation

        _ = simplified.Replace("mad24", "*"); // Replace multiply-add with simple multiply
        _ = simplified.Replace("rsqrt", "1.0f/sqrt"); // Replace fast inverse square root


        return simplified.ToString();
    }

    private static string CalculateKernelHash(string sourceCode)
    {
        var hash = global::System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(sourceCode));
        return Convert.ToBase64String(hash);
    }

    private static string DetectTargetPlatform(CompilationOptions options) => options.CompilerBackend ?? "Unknown";

    private void CacheCompilationResult(string kernelHash, CompilationRecoveryContext context, RecoveryResult result)
    {
        var cachedResult = new CachedCompilationResult
        {
            KernelHash = kernelHash,
            KernelName = context.KernelName,
            SourceCode = context.SourceCode,
            SuccessfulOptions = context.ModifiedOptions ?? context.CompilationOptions,
            Timestamp = DateTimeOffset.UtcNow,
            ExpirationTime = DateTimeOffset.UtcNow.Add(_config.CacheExpiration)
        };


        _ = _compilationCache.TryAdd(kernelHash, cachedResult);
    }

    private static double CalculateCacheHitRate()
        // This would track actual cache hits/misses in a real implementation




        => 0.75; // 75% hit rate placeholder

    private void CleanupCache(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var now = DateTimeOffset.UtcNow;
            var expiredKeys = _compilationCache
                .Where(kvp => kvp.Value.ExpirationTime < now)
                .Select(kvp => kvp.Key)
                .ToList();


            foreach (var key in expiredKeys)
            {
                _ = _compilationCache.TryRemove(key, out _);
            }


            if (expiredKeys.Count > 0)
            {
                LogCleanedUpCacheEntries(Logger, expiredKeys.Count);
            }
        }
        catch (Exception ex)
        {
            LogCacheCleanupError(Logger, ex);
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _cacheCleanupTimer?.Dispose();

            // Dispose interpreter instances

            foreach (var history in _compilationHistory.Values)
            {
                history.Dispose();
            }


            _disposed = true;
            LogSystemDisposed(Logger);
        }

        base.Dispose(disposing);
    }

    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 13100,
        Level = LogLevel.Information,
        Message = "Compilation Fallback system initialized with {Strategies} fallback strategies")]
    private static partial void LogSystemInitialized(ILogger logger, int strategies);

    [LoggerMessage(
        EventId = 13101,
        Level = LogLevel.Warning,
        Message = "Compilation error detected for kernel {KernelName}: {Error}")]
    private static partial void LogCompilationErrorDetected(ILogger logger, string kernelName, string error);

    [LoggerMessage(
        EventId = 13102,
        Level = LogLevel.Information,
        Message = "Using cached compilation result for kernel {KernelName}")]
    private static partial void LogUsingCachedResult(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 13103,
        Level = LogLevel.Information,
        Message = "Using compilation fallback strategy: {Strategy}")]
    private static partial void LogUsingFallbackStrategy(ILogger logger, string strategy);

    [LoggerMessage(
        EventId = 13104,
        Level = LogLevel.Information,
        Message = "Compilation fallback successful using {Strategy} for kernel {KernelName} in {Duration}ms")]
    private static partial void LogFallbackSuccessful(ILogger logger, string strategy, string kernelName, long duration);

    [LoggerMessage(
        EventId = 13105,
        Level = LogLevel.Warning,
        Message = "Compilation fallback failed using {Strategy} for kernel {KernelName}: {Message}")]
    private static partial void LogFallbackFailed(ILogger logger, string strategy, string kernelName, string message);

    [LoggerMessage(
        EventId = 13106,
        Level = LogLevel.Error,
        Message = "Exception during compilation fallback for kernel {KernelName}")]
    private static partial void LogExceptionDuringFallback(ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 13107,
        Level = LogLevel.Debug,
        Message = "Attempting compilation with strategy {Strategy} for kernel {KernelName}")]
    private static partial void LogAttemptingStrategy(ILogger logger, string strategy, string kernelName);

    [LoggerMessage(
        EventId = 13108,
        Level = LogLevel.Information,
        Message = "Compilation successful using strategy {Strategy} for kernel {KernelName}")]
    private static partial void LogCompilationSuccessful(ILogger logger, string strategy, string kernelName);

    [LoggerMessage(
        EventId = 13109,
        Level = LogLevel.Warning,
        Message = "Compilation strategy {Strategy} failed for kernel {KernelName}")]
    private static partial void LogStrategyFailed(ILogger logger, Exception ex, string strategy, string kernelName);

    [LoggerMessage(
        EventId = 13110,
        Level = LogLevel.Error,
        Message = "All compilation fallback strategies failed for kernel {KernelName}")]
    private static partial void LogAllStrategiesFailed(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 13111,
        Level = LogLevel.Information,
        Message = "Reducing optimization level for kernel {KernelName}")]
    private static partial void LogReducingOptimization(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 13112,
        Level = LogLevel.Information,
        Message = "Disabling fast math for kernel {KernelName}")]
    private static partial void LogDisablingFastMath(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 13113,
        Level = LogLevel.Information,
        Message = "Attempting kernel simplification for {KernelName}")]
    private static partial void LogAttemptingSimplification(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 13114,
        Level = LogLevel.Warning,
        Message = "Kernel simplification failed for {KernelName}")]
    private static partial void LogSimplificationFailed(ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 13115,
        Level = LogLevel.Information,
        Message = "Trying alternative compiler for kernel {KernelName}")]
    private static partial void LogTryingAlternativeCompiler(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 13116,
        Level = LogLevel.Debug,
        Message = "Alternative compiler {Compiler} failed for kernel {KernelName}")]
    private static partial void LogAlternativeCompilerFailed(ILogger logger, Exception ex, string compiler, string kernelName);

    [LoggerMessage(
        EventId = 13117,
        Level = LogLevel.Information,
        Message = "Falling back to interpreter mode for kernel {KernelName}")]
    private static partial void LogFallingBackToInterpreter(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 13118,
        Level = LogLevel.Error,
        Message = "Interpreter mode setup failed for kernel {KernelName}")]
    private static partial void LogInterpreterModeSetupFailed(ILogger logger, Exception ex, string kernelName);

    [LoggerMessage(
        EventId = 13119,
        Level = LogLevel.Information,
        Message = "Using similar cached compilation for kernel {KernelName}")]
    private static partial void LogUsingSimilarCachedCompilation(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 13120,
        Level = LogLevel.Debug,
        Message = "Cleaned up {Count} expired compilation cache entries")]
    private static partial void LogCleanedUpCacheEntries(ILogger logger, int count);

    [LoggerMessage(
        EventId = 13121,
        Level = LogLevel.Warning,
        Message = "Error during compilation cache cleanup")]
    private static partial void LogCacheCleanupError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 13122,
        Level = LogLevel.Information,
        Message = "Compilation Fallback system disposed")]
    private static partial void LogSystemDisposed(ILogger logger);

    #endregion
}





// Supporting types continue in next file...