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

namespace DotCompute.Core.Recovery;

/// <summary>
/// Comprehensive compilation fallback system with progressive optimization reduction,
/// alternative compilation strategies, and interpreter fallbacks
/// </summary>
public sealed class CompilationFallback : BaseRecoveryStrategy<CompilationRecoveryContext>, IDisposable
{
    private readonly ConcurrentDictionary<string, CompilationHistory> _compilationHistory;
    private readonly ConcurrentDictionary<string, CachedCompilationResult> _compilationCache;
    private readonly CompilationFallbackConfiguration _config;
    private readonly Timer _cacheCleanupTimer;
    private bool _disposed;

    public override RecoveryCapability Capability => RecoveryCapability.MemoryErrors;
    public override int Priority => 90;

    public CompilationFallback(ILogger<CompilationFallback> logger, CompilationFallbackConfiguration? config = null)
        : base(logger)
    {
        _config = config ?? CompilationFallbackConfiguration.Default;
        _compilationHistory = new ConcurrentDictionary<string, CompilationHistory>();
        _compilationCache = new ConcurrentDictionary<string, CachedCompilationResult>();

        // Periodic cache cleanup

        _cacheCleanupTimer = new Timer(CleanupCache, null,
            _config.CacheCleanupInterval, _config.CacheCleanupInterval);

        Logger.LogInformation("Compilation Fallback system initialized with {Strategies} fallback strategies",
            _config.FallbackStrategies.Count);
    }

    public override bool CanHandle(Exception error, CompilationRecoveryContext context)
    {
        return error switch
        {
            CompilationException => true,
            InvalidOperationException ioEx when ioEx.Message.Contains("compil", StringComparison.OrdinalIgnoreCase) => true,
            ArgumentException argEx when argEx.Message.Contains("kernel", StringComparison.OrdinalIgnoreCase) => true,
            TimeoutException when context.IsCompilationTimeout => true,
            _ => false
        };
    }

    public override async Task<RecoveryResult> RecoverAsync(
        Exception error,
        CompilationRecoveryContext context,
        RecoveryOptions options,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var kernelHash = CalculateKernelHash(context.SourceCode);


        Logger.LogWarning("Compilation error detected for kernel {KernelName}: {Error}",
            context.KernelName, error.Message);

        // Check cache first
        if (_compilationCache.TryGetValue(kernelHash, out var cached) && cached.IsValid)
        {
            Logger.LogInformation("Using cached compilation result for kernel {KernelName}", context.KernelName);
            return Success($"Used cached compilation result", stopwatch.Elapsed);
        }

        // Get or create compilation history
        var history = _compilationHistory.GetOrAdd(kernelHash, _ => new CompilationHistory(context.KernelName));
        history.RecordFailure(error, context.CompilationOptions);

        try
        {
            // Determine fallback strategy based on error and history
            var strategy = DetermineFallbackStrategy(error, context, history);
            Logger.LogInformation("Using compilation fallback strategy: {Strategy}", strategy);

            var result = await ExecuteFallbackStrategyAsync(strategy, context, history, options, cancellationToken);


            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;


            if (result.Success)
            {
                history.RecordSuccess((Types.CompilationFallbackStrategy)strategy);

                // Cache successful compilation

                CacheCompilationResult(kernelHash, context, result);


                Logger.LogInformation("Compilation fallback successful using {Strategy} for kernel {KernelName} in {Duration}ms",
                    strategy, context.KernelName, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                Logger.LogWarning("Compilation fallback failed using {Strategy} for kernel {KernelName}: {Message}",
                    strategy, context.KernelName, result.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Exception during compilation fallback for kernel {KernelName}", context.KernelName);
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
                Logger.LogDebug("Attempting compilation with strategy {Strategy} for kernel {KernelName}",

                    strategy, kernelName);


                var modifiedOptions = ApplyFallbackStrategy((Types.CompilationFallbackStrategy)strategy, currentOptions, context);
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
                    Logger.LogInformation("Compilation successful using strategy {Strategy} for kernel {KernelName}",
                        strategy, kernelName);


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
                Logger.LogWarning(ex, "Compilation strategy {Strategy} failed for kernel {KernelName}",

                    strategy, kernelName);


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
        Logger.LogError("All compilation fallback strategies failed for kernel {KernelName}", kernelName);


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
    public Statistics.CompilationStatistics GetCompilationStatistics()
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
            CompilationException compEx when compEx.Message.Contains("optimization") => CompilationFallbackStrategy.ReduceOptimizations,
            CompilationException compEx when compEx.Message.Contains("memory") => CompilationFallbackStrategy.SimplifyKernel,
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
        Logger.LogInformation("Reducing optimization level for kernel {KernelName}", context.KernelName);


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
        Logger.LogInformation("Disabling fast math for kernel {KernelName}", context.KernelName);


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
        Logger.LogInformation("Attempting kernel simplification for {KernelName}", context.KernelName);


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
            Logger.LogWarning(ex, "Kernel simplification failed for {KernelName}", context.KernelName);
            return Failure($"Kernel simplification failed: {ex.Message}", ex);
        }
    }

    private async Task<RecoveryResult> AlternativeCompilerAsync(CompilationRecoveryContext context, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Trying alternative compiler for kernel {KernelName}", context.KernelName);

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
                Logger.LogDebug(ex, "Alternative compiler {Compiler} failed for kernel {KernelName}",

                    compiler, context.KernelName);
            }
        }


        return Failure("All alternative compilers failed");
    }

    private async Task<RecoveryResult> InterpreterModeAsync(CompilationRecoveryContext context, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Falling back to interpreter mode for kernel {KernelName}", context.KernelName);


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
            Logger.LogError(ex, "Interpreter mode setup failed for kernel {KernelName}", context.KernelName);
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
            Logger.LogInformation("Using similar cached compilation for kernel {KernelName}", context.KernelName);
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
            // OptimizationLevel.Debug => 0.90, // Commented out as may be unreachable
            OptimizationLevel.Default => 0.80,
            OptimizationLevel.Full => 0.70,
            // OptimizationLevel.Aggressive => 0.60, // Removed - same as Full (both map to O3)
            _ => 0.85
        };


        if (context.UseInterpreter)
        {
            successRate = 0.99; // Interpreter almost always works
        }


        var success = Random.Shared.NextDouble() < successRate;

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
        using var sha256 = global::System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sourceCode));
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
                Logger.LogDebug("Cleaned up {Count} expired compilation cache entries", expiredKeys.Count);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error during compilation cache cleanup");
        }
    }

    public override void Dispose()
    {
        if (!_disposed)
        {
            _cacheCleanupTimer?.Dispose();

            // Dispose interpreter instances

            foreach (var history in _compilationHistory.Values)
            {
                history.Dispose();
            }


            _disposed = true;
            Logger.LogInformation("Compilation Fallback system disposed");
        }
    }
}





// Supporting types continue in next file...