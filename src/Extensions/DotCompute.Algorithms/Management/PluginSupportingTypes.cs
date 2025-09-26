// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using DotCompute.Algorithms.Types.Abstractions;

namespace DotCompute.Algorithms.Management
{
    /// <summary>
    /// Represents a loaded plugin with its context and metadata.
    /// </summary>
    public sealed class LoadedPlugin
    {
        public required IAlgorithmPlugin Plugin { get; init; }
        public required PluginAssemblyLoadContext LoadContext { get; init; }
        public required Assembly Assembly { get; init; }
        public required PluginMetadata Metadata { get; init; }
        public required DateTime LoadTime { get; init; }
        public PluginState State { get; set; } = PluginState.Loaded;
        public PluginHealth Health { get; set; } = PluginHealth.Unknown;
        public long ExecutionCount { get; set; }
        public DateTime LastExecution { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public Exception? LastError { get; set; }
    }

    /// <summary>
    /// Plugin metadata with comprehensive information.
    /// </summary>
    public sealed class PluginMetadata
    {
        /// <summary>
        /// Gets or sets the plugin ID.
        /// </summary>
        public required string Id { get; init; }

        /// <summary>
        /// Gets or sets the plugin name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets or sets the plugin version.
        /// </summary>
        public required string Version { get; init; }

        /// <summary>
        /// Gets or sets the plugin description.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// Gets or sets the plugin author.
        /// </summary>
        public string? Author { get; init; }

        /// <summary>
        /// Gets or sets the assembly path.
        /// </summary>
        public required string AssemblyPath { get; init; }

        /// <summary>
        /// Gets or sets the assembly name.
        /// </summary>
        public required string AssemblyName { get; init; }

        /// <summary>
        /// Gets or sets the plugin type name.
        /// </summary>
        public required string TypeName { get; init; }

        /// <summary>
        /// Gets or sets the plugin capabilities.
        /// </summary>
        public required string[] Capabilities { get; init; }

        /// <summary>
        /// Gets or sets the supported accelerator types.
        /// </summary>
        public required string[] SupportedAccelerators { get; init; }

        /// <summary>
        /// Gets or sets additional metadata.
        /// </summary>
        public required Dictionary<string, object> AdditionalMetadata { get; init; }

        /// <summary>
        /// Gets or sets the load context name.
        /// </summary>
        public required string LoadContextName { get; set; }
    }

    /// <summary>
    /// Information about loaded plugins.
    /// </summary>
    public sealed class LoadedPluginInfo
    {
        /// <summary>
        /// Gets or sets the plugin ID.
        /// </summary>
        public required string Id { get; init; }

        /// <summary>
        /// Gets or sets the plugin name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets or sets the plugin version.
        /// </summary>
        public required string Version { get; init; }

        /// <summary>
        /// Gets or sets the plugin state.
        /// </summary>
        public PluginState State { get; init; }

        /// <summary>
        /// Gets or sets the plugin health.
        /// </summary>
        public PluginHealth Health { get; init; }

        /// <summary>
        /// Gets or sets the load time.
        /// </summary>
        public DateTime LoadTime { get; init; }

        /// <summary>
        /// Gets or sets the execution count.
        /// </summary>
        public long ExecutionCount { get; init; }

        /// <summary>
        /// Gets or sets the last execution time.
        /// </summary>
        public DateTime LastExecution { get; init; }

        /// <summary>
        /// Gets or sets the total execution time.
        /// </summary>
        public TimeSpan TotalExecutionTime { get; init; }

        /// <summary>
        /// Gets or sets the last error message.
        /// </summary>
        public string? LastError { get; init; }

        /// <summary>
        /// Gets or sets the assembly name.
        /// </summary>
        public required string AssemblyName { get; init; }

        /// <summary>
        /// Gets or sets the load context name.
        /// </summary>
        public required string LoadContextName { get; set; }
    }

    /// <summary>
    /// Plugin state enumeration.
    /// </summary>
    public enum PluginState
    {
        /// <summary>
        /// Unknown state.
        /// </summary>
        Unknown,

        /// <summary>
        /// Plugin is being loaded.
        /// </summary>
        Loading,

        /// <summary>
        /// Plugin has been loaded but not initialized.
        /// </summary>
        Loaded,

        /// <summary>
        /// Plugin is being initialized.
        /// </summary>
        Initializing,

        /// <summary>
        /// Plugin is initialized and running.
        /// </summary>
        Running,

        /// <summary>
        /// Plugin is being stopped.
        /// </summary>
        Stopping,

        /// <summary>
        /// Plugin has failed.
        /// </summary>
        Failed,

        /// <summary>
        /// Plugin has been unloaded.
        /// </summary>
        Unloaded
    }

    /// <summary>
    /// Plugin health enumeration.
    /// </summary>
    public enum PluginHealth
    {
        /// <summary>
        /// Health status unknown.
        /// </summary>
        Unknown,

        /// <summary>
        /// Plugin is healthy.
        /// </summary>
        Healthy,

        /// <summary>
        /// Plugin is degraded but functional.
        /// </summary>
        Degraded,

        /// <summary>
        /// Plugin is unhealthy.
        /// </summary>
        Unhealthy,

        /// <summary>
        /// Plugin is in critical state.
        /// </summary>
        Critical
    }

    /// <summary>
    /// Result of NuGet package validation.
    /// </summary>
    public sealed class NuGetValidationResult
    {
        /// <summary>
        /// Gets or sets the package ID.
        /// </summary>
        public required string PackageId { get; init; }

        /// <summary>
        /// Gets or sets the package version.
        /// </summary>
        public required string Version { get; init; }

        /// <summary>
        /// Gets or sets whether the package is valid.
        /// </summary>
        public bool IsValid { get; init; }

        /// <summary>
        /// Gets or sets the validation error message (if not valid).
        /// </summary>
        public string? ValidationIssue { get; init; }

        /// <summary>
        /// Gets or sets the number of assemblies found in the package.
        /// </summary>
        public int AssemblyCount { get; init; }

        /// <summary>
        /// Gets or sets the number of dependencies.
        /// </summary>
        public int DependencyCount { get; init; }

        /// <summary>
        /// Gets or sets whether security validation passed.
        /// </summary>
        public bool SecurityValidationPassed { get; init; }

        /// <summary>
        /// Gets or sets security validation details.
        /// </summary>
        public required string SecurityDetails { get; init; }

        /// <summary>
        /// Gets or sets validation warnings.
        /// </summary>
        public required string[] Warnings { get; init; }

        /// <summary>
        /// Gets or sets the validation time.
        /// </summary>
        public TimeSpan ValidationTime { get; init; }

        /// <summary>
        /// Gets or sets the package size in bytes.
        /// </summary>
        public long PackageSize { get; init; }
    }

    /// <summary>
    /// Enhanced plugin assembly load context with isolation support.
    /// </summary>
    public sealed class PluginAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly bool _enableIsolation;

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginAssemblyLoadContext"/> class.
        /// </summary>
        /// <param name="name">The name of the load context.</param>
        /// <param name="pluginPath">The path to the plugin assembly.</param>
        /// <param name="enableIsolation">Whether to enable isolation.</param>
        public PluginAssemblyLoadContext(string name, string pluginPath, bool enableIsolation)
            : base(name, isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
            _enableIsolation = enableIsolation;
        }

        /// <inheritdoc/>
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Plugin assembly loading requires dynamic loading")]
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // For isolation, only load plugin dependencies from plugin directory
            if (_enableIsolation)
            {
                var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
                if (assemblyPath != null)
                {
                    return LoadFromAssemblyPath(assemblyPath);
                }

                // Don't load system assemblies in isolated context
                if (IsSystemAssembly(assemblyName))
                {
                    return null; // Let default context handle it
                }
            }
            else
            {
                // Non-isolated: try to resolve dependencies first
                var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
                if (assemblyPath != null)
                {
                    return LoadFromAssemblyPath(assemblyPath);
                }
            }

            return null;
        }

        /// <inheritdoc/>
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Determines if an assembly is a system assembly.
        /// </summary>
        private static bool IsSystemAssembly(AssemblyName assemblyName)
        {
            var name = assemblyName.Name?.ToLowerInvariant();
            return name != null && (
                name.StartsWith("system.", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("microsoft.", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Information about cached NuGet packages used in management operations.
    /// </summary>
    public sealed class ManagementPackageInfo
    {
        /// <summary>
        /// Gets or sets the package ID.
        /// </summary>
        public required string PackageId { get; init; }

        /// <summary>
        /// Gets or sets the package version.
        /// </summary>
        public required string Version { get; init; }

        /// <summary>
        /// Gets or sets the cache path.
        /// </summary>
        public required string CachePath { get; init; }

        /// <summary>
        /// Gets or sets the cached date.
        /// </summary>
        public DateTime CachedDate { get; init; }

        /// <summary>
        /// Gets or sets the package size in bytes.
        /// </summary>
        public long PackageSize { get; init; }

        /// <summary>
        /// Gets or sets the number of assemblies in the package.
        /// </summary>
        public int AssemblyCount { get; init; }
    }
}