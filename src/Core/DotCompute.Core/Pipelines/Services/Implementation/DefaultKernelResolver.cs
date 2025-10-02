// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DotCompute.Core.Pipelines.Services.Implementation
{
    /// <summary>
    /// Default implementation of kernel resolution service.
    /// Resolves kernel names to compiled kernel instances using the generated kernel discovery service.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the DefaultKernelResolver class.
    /// </remarks>
    /// <param name="serviceProvider">The service provider for kernel discovery</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    public sealed class DefaultKernelResolver(
        IServiceProvider serviceProvider,
        ILogger<DefaultKernelResolver>? logger = null) : IKernelResolver
    {
        private readonly ILogger<DefaultKernelResolver>? _logger = logger;
        private readonly ConcurrentDictionary<string, ICompiledKernel?> _kernelCache = new();
        private volatile bool _initialized;

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
                _logger?.LogDebug("Kernel '{KernelName}' found in cache", kernelName);
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
                    _logger?.LogDebug("Successfully resolved kernel '{KernelName}'", kernelName);
                }
                else
                {
                    _logger?.LogWarning("Kernel '{KernelName}' not found", kernelName);
                }

                return kernel;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error resolving kernel '{KernelName}'", kernelName);
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


                _logger?.LogDebug("Found {Count} available kernels", kernelNames.Count);


                return kernelNames;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting available kernel names");
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
            if (_kernelCache.ContainsKey(kernelName))
            {
                return _kernelCache[kernelName] != null;
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
                _logger?.LogDebug("Initializing kernel resolver");

                // Pre-populate cache with known kernels if available

                await PrePopulateCacheAsync(cancellationToken);


                _initialized = true;
                _logger?.LogInformation("Kernel resolver initialized successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing kernel resolver");
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


                _logger?.LogDebug("Pre-populated kernel cache with {Count} kernels", _kernelCache.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error pre-populating kernel cache");
                // Don't fail initialization if pre-population fails
            }
        }

        /// <summary>
        /// Resolves a kernel from the discovery service.
        /// This is a placeholder that would interface with the actual kernel discovery and compilation system.
        /// </summary>
        private static async Task<ICompiledKernel?> ResolveKernelFromDiscoveryServiceAsync(string kernelName, CancellationToken cancellationToken)
        {
            // TODO: This needs to interface with the actual kernel discovery and compilation system
            // For now, return null to indicate kernel not found
            // In a complete implementation, this would:
            // 1. Check if the kernel is registered with the discovery service
            // 2. Get the kernel definition
            // 3. Compile the kernel for the appropriate backend(s)
            // 4. Return the compiled kernel


            await Task.Delay(1, cancellationToken); // Simulate async operation
            return null;
        }

        /// <summary>
        /// Gets kernel names from the discovery service.
        /// This is a placeholder that would interface with the actual kernel discovery system.
        /// </summary>
        private static async Task<IReadOnlyCollection<string>> GetKernelNamesFromDiscoveryServiceAsync(CancellationToken cancellationToken)
        {
            // TODO: This needs to interface with the actual kernel discovery system
            // For now, return empty collection
            // In a complete implementation, this would get all registered kernel names


            await Task.Delay(1, cancellationToken); // Simulate async operation
            return Array.Empty<string>();
        }
    }
}
