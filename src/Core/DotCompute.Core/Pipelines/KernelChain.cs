// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Core.Pipelines.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Pipelines
{
    /// <summary>
    /// Static entry point for creating fluent kernel chains.
    /// Provides an intuitive API for chaining kernel operations with method chaining syntax.
    /// </summary>
    public static class KernelChain
    {
        private static IServiceProvider? _serviceProvider;
        private static ILogger<KernelChainBuilder>? _logger;

        /// <summary>
        /// Configures the kernel chain system with a service provider.
        /// This should be called during application startup after building the service provider.
        /// </summary>
        /// <param name="serviceProvider">The configured service provider containing DotCompute services</param>
        public static void Configure(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = serviceProvider.GetService<ILogger<KernelChainBuilder>>();
        }

        /// <summary>
        /// Creates a new kernel chain builder with dependency injection support.
        /// </summary>
        /// <returns>A new kernel chain builder instance</returns>
        /// <exception cref="InvalidOperationException">Thrown when Configure has not been called</exception>
        public static IKernelChainBuilder Create()
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException(
                    "KernelChain.Configure must be called with a service provider before creating chains. " +
                    "Add services.AddKernelChaining() and call KernelChain.Configure(serviceProvider) during startup.");
            }

            var orchestrator = _serviceProvider.GetRequiredService<IComputeOrchestrator>();
            var kernelResolver = _serviceProvider.GetService<IKernelResolver>();
            var profiler = _serviceProvider.GetService<IKernelChainProfiler>();
            var validator = _serviceProvider.GetService<IKernelChainValidator>();
            var cacheService = _serviceProvider.GetService<IKernelChainCacheService>();

            return new KernelChainBuilder(
                orchestrator,
                kernelResolver,
                profiler,
                validator,
                cacheService,
                _logger);
        }

        /// <summary>
        /// Quick execution utility for single kernel operations.
        /// Executes a kernel directly without building a chain.
        /// </summary>
        /// <typeparam name="T">The expected return type</typeparam>
        /// <param name="kernelName">The name of the kernel to execute</param>
        /// <param name="args">Arguments to pass to the kernel</param>
        /// <returns>Task containing the kernel execution result</returns>
        public static async Task<T> QuickAsync<T>(string kernelName, params object[] args)
        {
            await using var chain = Create();
            return await chain.Kernel(kernelName, args).ExecuteAsync<T>();
        }

        /// <summary>
        /// Quick execution utility for single kernel operations without return value.
        /// </summary>
        /// <param name="kernelName">The name of the kernel to execute</param>
        /// <param name="args">Arguments to pass to the kernel</param>
        /// <returns>Task representing the kernel execution</returns>
        public static async Task QuickAsync(string kernelName, params object[] args)
        {
            await using var chain = Create();
            _ = await chain.Kernel(kernelName, args).ExecuteAsync<object>();
        }

        /// <summary>
        /// Creates a kernel chain builder with specific backend preference.
        /// </summary>
        /// <param name="preferredBackend">The preferred backend for execution</param>
        /// <returns>A new kernel chain builder configured for the specified backend</returns>
        public static IKernelChainBuilder OnBackend(string preferredBackend) => Create().OnBackend(preferredBackend);

        /// <summary>
        /// Creates a kernel chain builder with profiling enabled.
        /// </summary>
        /// <param name="profileName">Optional profile name for identification</param>
        /// <returns>A new kernel chain builder with profiling enabled</returns>
        public static IKernelChainBuilder WithProfiling(string? profileName = null) => Create().WithProfiling(profileName);

        /// <summary>
        /// Gets diagnostic information about the kernel chain system.
        /// </summary>
        /// <returns>Dictionary containing system diagnostic information</returns>
        public static IReadOnlyDictionary<string, object> GetDiagnostics()
        {
            var diagnostics = new Dictionary<string, object>();

            if (_serviceProvider != null)
            {
                diagnostics["IsConfigured"] = true;
                diagnostics["ServiceProvider"] = _serviceProvider.GetType().Name;

                try
                {
                    var orchestrator = _serviceProvider.GetService<IComputeOrchestrator>();
                    diagnostics["HasOrchestrator"] = orchestrator != null;
                    diagnostics["OrchestratorType"] = orchestrator?.GetType().Name ?? "None";
                }
                catch (Exception ex)
                {
                    diagnostics["OrchestratorError"] = ex.Message;
                }

                try
                {
                    var kernelResolver = _serviceProvider.GetService<IKernelResolver>();
                    diagnostics["HasKernelResolver"] = kernelResolver != null;
                }
                catch (Exception ex)
                {
                    diagnostics["KernelResolverError"] = ex.Message;
                }
            }
            else
            {
                diagnostics["IsConfigured"] = false;
                diagnostics["ConfigurationRequired"] = "Call KernelChain.Configure(serviceProvider)";
            }

            return diagnostics;
        }

        /// <summary>
        /// Resets the kernel chain configuration (primarily for testing).
        /// </summary>
        internal static void Reset()
        {
            _serviceProvider = null;
            _logger = null;
        }
    }
}
