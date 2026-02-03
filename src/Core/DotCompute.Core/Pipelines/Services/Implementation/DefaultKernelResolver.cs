// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Kernels;
using Microsoft.Extensions.Logging;

// Resolve ambiguity between ICompiledKernel definitions
using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;

namespace DotCompute.Core.Pipelines.Services.Implementation
{
    /// <summary>
    /// Default implementation of kernel resolution service.
    /// Resolves kernel names to compiled kernel instances using the generated kernel discovery service.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the DefaultKernelResolver class.
    /// </remarks>
    /// <param name="kernelManager">Optional kernel manager for compilation</param>
    /// <param name="defaultAccelerator">Optional default accelerator for compilation</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    public sealed partial class DefaultKernelResolver(
        IKernelManager? kernelManager = null,
        IAccelerator? defaultAccelerator = null,
        ILogger<DefaultKernelResolver>? logger = null) : IKernelResolver
    {
        private readonly IKernelManager? _kernelManager = kernelManager;
        private readonly IAccelerator? _defaultAccelerator = defaultAccelerator;
        private readonly ILogger<DefaultKernelResolver>? _logger = logger;
        private readonly ConcurrentDictionary<string, ICompiledKernel?> _kernelCache = new();
        private readonly ConcurrentDictionary<string, KernelDefinition> _registeredKernels = new();
        private volatile bool _initialized;

        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 10100,
            Level = LogLevel.Debug,
            Message = "Kernel '{KernelName}' found in cache")]
        private static partial void LogKernelFoundInCache(ILogger logger, string kernelName);

        [LoggerMessage(
            EventId = 10101,
            Level = LogLevel.Debug,
            Message = "Successfully resolved kernel '{KernelName}'")]
        private static partial void LogKernelResolved(ILogger logger, string kernelName);

        [LoggerMessage(
            EventId = 10102,
            Level = LogLevel.Warning,
            Message = "Kernel '{KernelName}' not found")]
        private static partial void LogKernelNotFound(ILogger logger, string kernelName);

        [LoggerMessage(
            EventId = 10103,
            Level = LogLevel.Error,
            Message = "Error resolving kernel '{KernelName}'")]
        private static partial void LogKernelResolutionError(ILogger logger, Exception ex, string kernelName);

        [LoggerMessage(
            EventId = 10104,
            Level = LogLevel.Debug,
            Message = "Found {Count} available kernels")]
        private static partial void LogAvailableKernelsCount(ILogger logger, int count);

        [LoggerMessage(
            EventId = 10105,
            Level = LogLevel.Error,
            Message = "Error getting available kernel names")]
        private static partial void LogGetKernelNamesError(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 10106,
            Level = LogLevel.Debug,
            Message = "Initializing kernel resolver")]
        private static partial void LogInitializing(ILogger logger);

        [LoggerMessage(
            EventId = 10107,
            Level = LogLevel.Information,
            Message = "Kernel resolver initialized successfully")]
        private static partial void LogInitialized(ILogger logger);

        [LoggerMessage(
            EventId = 10108,
            Level = LogLevel.Error,
            Message = "Error initializing kernel resolver")]
        private static partial void LogInitializationError(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 10109,
            Level = LogLevel.Debug,
            Message = "Pre-populated kernel cache with {Count} kernels")]
        private static partial void LogCachePrePopulated(ILogger logger, int count);

        [LoggerMessage(
            EventId = 10110,
            Level = LogLevel.Warning,
            Message = "Error pre-populating kernel cache")]
        private static partial void LogCachePrePopulationWarning(ILogger logger, Exception ex);

        #endregion

        /// <inheritdoc/>
        public async Task<ICompiledKernel?> ResolveKernelAsync(string kernelName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(kernelName))
            {
                throw new ArgumentException("Kernel name cannot be null or whitespace", nameof(kernelName));
            }

            // Check cache first
            if (_kernelCache.TryGetValue(kernelName, out var cachedKernel))
            {
                if (_logger != null)
                {
                    LogKernelFoundInCache(_logger, kernelName);
                }
                return cachedKernel;
            }

            // Ensure initialization
            if (!_initialized)
            {
                await InitializeAsync(cancellationToken);
            }

            // Try to resolve the kernel
            try
            {
                // This is a simplified implementation - in practice, this would interface
                // with the kernel discovery service to resolve kernels by name
                var kernel = await ResolveKernelFromDiscoveryServiceAsync(kernelName, cancellationToken);

                // Cache the result (even if null to avoid repeated lookups)

                _ = _kernelCache.TryAdd(kernelName, kernel);


                if (kernel != null)
                {
                    if (_logger != null)
                    {
                        LogKernelResolved(_logger, kernelName);
                    }
                }
                else
                {
                    if (_logger != null)
                    {
                        LogKernelNotFound(_logger, kernelName);
                    }
                }

                return kernel;
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    LogKernelResolutionError(_logger, ex, kernelName);
                }
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyCollection<string>> GetAvailableKernelNamesAsync(CancellationToken cancellationToken = default)
        {
            if (!_initialized)
            {
                await InitializeAsync(cancellationToken);
            }

            try
            {
                // This would interface with the discovery service to get all available kernel names
                var kernelNames = await GetKernelNamesFromDiscoveryServiceAsync(cancellationToken);

                if (_logger != null)
                {
                    LogAvailableKernelsCount(_logger, kernelNames.Count);
                }

                return kernelNames;
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    LogGetKernelNamesError(_logger, ex);
                }
                return Array.Empty<string>();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> KernelExistsAsync(string kernelName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(kernelName))
            {
                return false;
            }

            // Check cache first
            if (_kernelCache.TryGetValue(kernelName, out var cachedKernel))
            {
                return cachedKernel != null;
            }

            // Try to resolve to check existence
            var kernel = await ResolveKernelAsync(kernelName, cancellationToken);
            return kernel != null;
        }

        /// <summary>
        /// Initializes the kernel resolver.
        /// </summary>
        private async Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (_initialized)
            {
                return;
            }


            try
            {
                if (_logger != null)
                {
                    LogInitializing(_logger);
                }

                // Pre-populate cache with known kernels if available

                await PrePopulateCacheAsync(cancellationToken);


                _initialized = true;
                if (_logger != null)
                {
                    LogInitialized(_logger);
                }
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    LogInitializationError(_logger, ex);
                }
                throw;
            }
        }

        /// <summary>
        /// Pre-populates the kernel cache with known kernels.
        /// </summary>
        private async Task PrePopulateCacheAsync(CancellationToken cancellationToken)
        {
            try
            {
                // This would get all known kernels from the discovery service
                var availableKernels = await GetKernelNamesFromDiscoveryServiceAsync(cancellationToken);


                foreach (var kernelName in availableKernels)
                {
                    if (!_kernelCache.ContainsKey(kernelName))
                    {
                        // Pre-resolve kernels for better performance
                        var kernel = await ResolveKernelFromDiscoveryServiceAsync(kernelName, cancellationToken);
                        _ = _kernelCache.TryAdd(kernelName, kernel);
                    }
                }

                if (_logger != null)
                {
                    LogCachePrePopulated(_logger, _kernelCache.Count);
                }
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    LogCachePrePopulationWarning(_logger, ex);
                }
                // Don't fail initialization if pre-population fails
            }
        }

        /// <summary>
        /// Resolves a kernel from the discovery service.
        /// Uses the kernel manager to compile kernels if a definition is registered.
        /// </summary>
        private async Task<ICompiledKernel?> ResolveKernelFromDiscoveryServiceAsync(string kernelName, CancellationToken cancellationToken)
        {
            // Check if kernel definition is registered
            if (_registeredKernels.TryGetValue(kernelName, out var kernelDefinition))
            {
                // If we have a kernel manager and accelerator, compile the kernel
                if (_kernelManager != null && _defaultAccelerator != null)
                {
                    try
                    {
                        var managedKernel = await _kernelManager.GetOrCompileOperationKernelAsync(
                            kernelName,
                            kernelDefinition.ParameterTypes ?? [],
                            kernelDefinition.ReturnType ?? typeof(void),
                            _defaultAccelerator,
                            null, // context
                            kernelDefinition.Options,
                            cancellationToken).ConfigureAwait(false);

                        return managedKernel.Kernel;
                    }
                    catch (Exception ex)
                    {
                        if (_logger != null)
                        {
                            LogKernelResolutionError(_logger, ex, kernelName);
                        }
                        return null;
                    }
                }

                // If no kernel manager, try to get from the definition's pre-compiled kernel
                return kernelDefinition.PreCompiledKernel;
            }

            // Kernel not found in registered kernels
            return null;
        }

        /// <summary>
        /// Gets kernel names from the discovery service.
        /// Returns all registered kernel names.
        /// </summary>
        private Task<IReadOnlyCollection<string>> GetKernelNamesFromDiscoveryServiceAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Return all registered kernel names
            var kernelNames = _registeredKernels.Keys.ToArray();
            return Task.FromResult<IReadOnlyCollection<string>>(kernelNames);
        }

        /// <summary>
        /// Registers a kernel definition for resolution.
        /// </summary>
        /// <param name="kernelName">The kernel name.</param>
        /// <param name="definition">The kernel definition.</param>
        public void RegisterKernel(string kernelName, KernelDefinition definition)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);
            ArgumentNullException.ThrowIfNull(definition);

            _registeredKernels[kernelName] = definition;

            if (_logger != null)
            {
                _logger.LogDebug("Registered kernel '{KernelName}' for resolution", kernelName);
            }
        }

        /// <summary>
        /// Registers a pre-compiled kernel for resolution.
        /// </summary>
        /// <param name="kernelName">The kernel name.</param>
        /// <param name="compiledKernel">The pre-compiled kernel.</param>
        public void RegisterCompiledKernel(string kernelName, ICompiledKernel compiledKernel)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);
            ArgumentNullException.ThrowIfNull(compiledKernel);

            // Cache directly and create minimal definition
            _kernelCache[kernelName] = compiledKernel;
            _registeredKernels[kernelName] = new KernelDefinition
            {
                Name = kernelName,
                PreCompiledKernel = compiledKernel
            };

            if (_logger != null)
            {
                LogKernelResolved(_logger, kernelName);
            }
        }

        /// <summary>
        /// Unregisters a kernel from resolution.
        /// </summary>
        /// <param name="kernelName">The kernel name to unregister.</param>
        /// <returns>True if the kernel was unregistered; otherwise false.</returns>
        public bool UnregisterKernel(string kernelName)
        {
            if (string.IsNullOrWhiteSpace(kernelName))
            {
                return false;
            }

            var removed = _registeredKernels.TryRemove(kernelName, out _);
            _kernelCache.TryRemove(kernelName, out _);

            return removed;
        }

        /// <summary>
        /// Clears the kernel cache, forcing recompilation on next resolution.
        /// </summary>
        public void ClearCache()
        {
            _kernelCache.Clear();

            if (_logger != null)
            {
                _logger.LogDebug("Kernel cache cleared");
            }
        }
    }

    /// <summary>
    /// Definition of a kernel for registration with the resolver.
    /// </summary>
    public sealed class KernelDefinition
    {
        /// <summary>
        /// Gets or sets the kernel name.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the kernel source code.
        /// </summary>
        public string? SourceCode { get; init; }

        /// <summary>
        /// Gets or sets the parameter types for compilation.
        /// </summary>
        public Type[]? ParameterTypes { get; init; }

        /// <summary>
        /// Gets or sets the return type for compilation.
        /// </summary>
        public Type? ReturnType { get; init; }

        /// <summary>
        /// Gets or sets the compilation options.
        /// </summary>
        public CompilationOptions? Options { get; init; }

        /// <summary>
        /// Gets or sets a pre-compiled kernel (if available).
        /// </summary>
        public ICompiledKernel? PreCompiledKernel { get; init; }
    }
}
