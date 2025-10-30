// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Abstractions
{

    /// <summary>
    /// Utility methods for common kernel compilation patterns to reduce code duplication.
    /// </summary>
    public static partial class KernelUtilities
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 3015,
            Level = LogLevel.Debug,
            Message = "Compiling kernel '{KernelName}' for {BackendType} backend")]
        private static partial void LogCompilingKernel(ILogger logger, string kernelName, string backendType);

        [LoggerMessage(
            EventId = 3016,
            Level = LogLevel.Debug,
            Message = "Retrieved kernel '{KernelName}' from cache")]
        private static partial void LogKernelFromCache(ILogger logger, string kernelName);

        [LoggerMessage(
            EventId = 3017,
            Level = LogLevel.Debug,
            Message = "Successfully compiled kernel '{KernelName}'")]
        private static partial void LogKernelCompiled(ILogger logger, string kernelName);

        [LoggerMessage(
            EventId = 3018,
            Level = LogLevel.Error,
            Message = "Failed to compile kernel '{KernelName}' for {BackendType}")]
        private static partial void LogKernelCompileFailed(ILogger logger, Exception ex, string kernelName, string backendType);

        [LoggerMessage(
            EventId = 3019,
            Level = LogLevel.Debug,
            Message = "Initializing {BackendType} kernel compiler{CompilerInfo}")]
        private static partial void LogInitializingCompiler(ILogger logger, string backendType, string compilerInfo);

        [LoggerMessage(
            EventId = 3020,
            Level = LogLevel.Debug,
            Message = "{BackendType} kernel compiler initialized successfully")]
        private static partial void LogCompilerInitialized(ILogger logger, string backendType);

        [LoggerMessage(
            EventId = 3021,
            Level = LogLevel.Error,
            Message = "Failed to initialize {BackendType} kernel compiler")]
        private static partial void LogCompilerInitFailed(ILogger logger, Exception ex, string backendType);

        [LoggerMessage(
            EventId = 3022,
            Level = LogLevel.Information,
            Message = "Disposing {BackendType} kernel compiler")]
        private static partial void LogDisposingCompiler(ILogger logger, string backendType);

        [LoggerMessage(
            EventId = 3023,
            Level = LogLevel.Debug,
            Message = "Clearing kernel compilation cache ({Count} entries)")]
        private static partial void LogClearingCache(ILogger logger, int count);

        [LoggerMessage(
            EventId = 3024,
            Level = LogLevel.Warning,
            Message = "Failed to dispose cached kernel during cache clear")]
        private static partial void LogCachedKernelDisposeFailed(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 3025,
            Level = LogLevel.Error,
            Message = "Error during {BackendType} kernel compiler disposal")]
        private static partial void LogCompilerDisposalError(ILogger logger, Exception ex, string backendType);

        [LoggerMessage(
            EventId = 3026,
            Level = LogLevel.Information,
            Message = "Disposing {BackendType} kernel compiler (synchronous)")]
        private static partial void LogDisposingCompilerSync(ILogger logger, string backendType);

        [LoggerMessage(
            EventId = 3027,
            Level = LogLevel.Error,
            Message = "Error during synchronous {BackendType} kernel compiler disposal")]
        private static partial void LogSyncCompilerDisposalError(ILogger logger, Exception ex, string backendType);

        [LoggerMessage(
            EventId = 3028,
            Level = LogLevel.Debug,
            Message = "Clearing {BackendType} kernel compilation cache ({Count} entries)")]
        private static partial void LogClearingCacheSafely(ILogger logger, string backendType, int count);

        [LoggerMessage(
            EventId = 3029,
            Level = LogLevel.Debug,
            Message = "{BackendType} kernel compilation cache cleared")]
        private static partial void LogCacheCleared(ILogger logger, string backendType);

        #endregion
        /// <summary>
        /// Common pattern for kernel compilation with caching, error handling, and logging.
        /// </summary>
        /// <param name="definition">Kernel definition to compile</param>
        /// <param name="options">Compilation options</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="backendType">Type of backend for logging</param>
        /// <param name="enableCaching">Whether to enable caching</param>
        /// <param name="cache">Cache dictionary for compiled kernels</param>
        /// <param name="compileFunc">The actual compilation function</param>
        /// <param name="cacheKeyFunc">Function to generate cache keys</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The compiled kernel</returns>
        public static async ValueTask<ICompiledKernel> CompileWithCachingAsync(
            KernelDefinition definition,
            CompilationOptions? options,
            ILogger logger,
            string backendType,
            bool enableCaching,
            Dictionary<string, ICompiledKernel> cache,
            Func<KernelDefinition, CompilationOptions, CancellationToken, ValueTask<ICompiledKernel>> compileFunc,
            Func<KernelDefinition, CompilationOptions, string> cacheKeyFunc,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(cache);
            ArgumentNullException.ThrowIfNull(compileFunc);
            ArgumentNullException.ThrowIfNull(cacheKeyFunc);

            options ??= new CompilationOptions();

            LogCompilingKernel(logger, definition.Name, backendType);

            // Check cache first if enabled
            if (enableCaching)
            {
                var cacheKey = cacheKeyFunc(definition, options);
                if (cache.TryGetValue(cacheKey, out var cachedKernel))
                {
                    LogKernelFromCache(logger, definition.Name);
                    return cachedKernel;
                }
            }

            try
            {
                // Validate kernel definition
                ValidateKernelDefinition(definition);

                // Perform compilation
                var compiledKernel = await compileFunc(definition, options, cancellationToken).ConfigureAwait(false);

                // Cache the result if enabled
                if (enableCaching)
                {
                    var cacheKey = cacheKeyFunc(definition, options);
                    cache[cacheKey] = compiledKernel;
                }

                LogKernelCompiled(logger, definition.Name);
                return compiledKernel;
            }
            catch (Exception ex)
            {
                LogKernelCompileFailed(logger, ex, definition.Name, backendType);
                throw new InvalidOperationException($"Kernel compilation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Common pattern for kernel compiler initialization.
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="backendType">Type of backend for logging</param>
        /// <param name="initFunc">Initialization function</param>
        /// <param name="compilerInfo">Optional compiler information for logging</param>
        /// <returns>Result of initialization</returns>
        public static T InitializeCompilerWithLogging<T>(
            ILogger logger,
            string backendType,
            Func<T> initFunc,
            string? compilerInfo = null)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(initFunc);

            try
            {
                LogInitializingCompiler(logger, backendType, compilerInfo != null ? $" ({compilerInfo})" : "");

                var result = initFunc();

                LogCompilerInitialized(logger, backendType);
                return result;
            }
            catch (Exception ex)
            {
                LogCompilerInitFailed(logger, ex, backendType);
                throw;
            }
        }

        /// <summary>
        /// Common pattern for kernel compiler disposal with cache cleanup.
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="backendType">Type of backend for logging</param>
        /// <param name="cache">Cache to clear</param>
        /// <param name="disposables">Objects to dispose</param>
        public static async ValueTask DisposeCompilerWithCacheCleanupAsync(
            ILogger logger,
            string backendType,
            Dictionary<string, ICompiledKernel>? cache,
            params object?[] disposables)
        {
            ArgumentNullException.ThrowIfNull(logger);

            try
            {
                LogDisposingCompiler(logger, backendType);

                // Clear and dispose cache if present
                if (cache != null && cache.Count > 0)
                {
                    LogClearingCache(logger, cache.Count);

                    foreach (var kernel in cache.Values)
                    {
                        try
                        {
                            await DisposalUtilities.SafeDisposeAsync(kernel, logger, "cached kernel").ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            LogCachedKernelDisposeFailed(logger, ex);
                        }
                    }
                    cache.Clear();
                }

                // Dispose other objects
                await DisposalUtilities.SafeDisposeAllAsync(disposables, logger).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogCompilerDisposalError(logger, ex, backendType);
            }
        }

        /// <summary>
        /// Common pattern for kernel compiler disposal with cache cleanup (synchronous).
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="backendType">Type of backend for logging</param>
        /// <param name="cache">Cache to clear</param>
        /// <param name="disposables">Objects to dispose</param>
        public static void DisposeCompilerWithCacheCleanup(
            ILogger logger,
            string backendType,
            Dictionary<string, ICompiledKernel>? cache,
            params object?[] disposables)
        {
            ArgumentNullException.ThrowIfNull(logger);

            try
            {
                LogDisposingCompilerSync(logger, backendType);

                // Clear and dispose cache if present
                if (cache != null && cache.Count > 0)
                {
                    LogClearingCache(logger, cache.Count);

                    foreach (var kernel in cache.Values)
                    {
                        try
                        {
                            DisposalUtilities.SafeDispose(kernel, logger, "cached kernel");
                        }
                        catch (Exception ex)
                        {
                            LogCachedKernelDisposeFailed(logger, ex);
                        }
                    }
                    cache.Clear();
                }

                // Dispose other objects
                DisposalUtilities.SafeDisposeAll(disposables, logger);
            }
            catch (Exception ex)
            {
                LogSyncCompilerDisposalError(logger, ex, backendType);
            }
        }

        /// <summary>
        /// Generates a standard cache key for kernel compilation.
        /// </summary>
        /// <param name="definition">Kernel definition</param>
        /// <param name="options">Compilation options</param>
        /// <returns>Cache key string</returns>
        public static string GenerateStandardCacheKey(KernelDefinition definition, CompilationOptions options)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(options);

            var codeHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(definition.Code ?? ""));
            var codeHashString = Convert.ToHexString(codeHash);

            return $"{definition.Name}_{codeHashString}_{options.OptimizationLevel}_{options.EnableDebugInfo}";
        }

        /// <summary>
        /// Validates a kernel definition before compilation.
        /// </summary>
        /// <param name="definition">Kernel definition to validate</param>
        public static void ValidateKernelDefinition(KernelDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            if (string.IsNullOrEmpty(definition.Name))
            {
                throw new ArgumentException("Kernel name cannot be null or empty");
            }

            if (definition.Code == null || definition.Code.Length == 0)
            {
                throw new ArgumentException("Kernel code cannot be null or empty");
            }
        }

        /// <summary>
        /// Clears a kernel compilation cache safely.
        /// </summary>
        /// <param name="cache">Cache to clear</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="backendType">Backend type for logging</param>
        public static void ClearCacheSafely(
            Dictionary<string, ICompiledKernel> cache,
            ILogger logger,
            string backendType)
        {
            ArgumentNullException.ThrowIfNull(cache);
            ArgumentNullException.ThrowIfNull(logger);

            var count = cache.Count;
            if (count == 0)
            {
                return;
            }

            LogClearingCacheSafely(logger, backendType, count);

            foreach (var kernel in cache.Values)
            {
                try
                {
                    DisposalUtilities.SafeDispose(kernel, logger, "cached kernel");
                }
                catch (Exception ex)
                {
                    LogCachedKernelDisposeFailed(logger, ex);
                }
            }

            cache.Clear();
            LogCacheCleared(logger, backendType);
        }

        /// <summary>
        /// Gets compilation statistics for a cache.
        /// </summary>
        /// <param name="cache">Cache to analyze</param>
        /// <param name="backendType">Backend type</param>
        /// <param name="enableCaching">Whether caching is enabled</param>
        /// <param name="maxCacheSize">Maximum cache size</param>
        /// <returns>Statistics dictionary</returns>
        public static Dictionary<string, object> GetCompilationStatistics(
            Dictionary<string, ICompiledKernel> cache,
            string backendType,
            bool enableCaching,
            int maxCacheSize)
        {
            ArgumentNullException.ThrowIfNull(cache);

            return new Dictionary<string, object>
            {
                ["CachedKernels"] = cache.Count,
                ["CacheEnabled"] = enableCaching,
                ["MaxCacheSize"] = maxCacheSize,
                ["CompilerType"] = backendType
            };
        }
    }
}
