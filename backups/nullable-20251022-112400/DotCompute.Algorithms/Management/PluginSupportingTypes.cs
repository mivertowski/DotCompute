// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Management.Loading;
using DotCompute.Algorithms.Management.Metadata;
using DotCompute.Algorithms.Types.Enums;

namespace DotCompute.Algorithms.Management
{
    /// <summary>
    /// Represents a loaded plugin with its context and metadata.
    /// </summary>
    public sealed class LoadedPlugin
    {
        /// <summary>
        /// Gets or sets the plugin.
        /// </summary>
        /// <value>The plugin.</value>
        public required IAlgorithmPlugin Plugin { get; init; }
        /// <summary>
        /// Gets or sets the load context.
        /// </summary>
        /// <value>The load context.</value>
        public required PluginAssemblyLoadContext LoadContext { get; init; }
        /// <summary>
        /// Gets or sets the assembly.
        /// </summary>
        /// <value>The assembly.</value>
        public required Assembly Assembly { get; init; }
        /// <summary>
        /// Gets or sets the metadata.
        /// </summary>
        /// <value>The metadata.</value>
        public required PluginMetadata Metadata { get; init; }
        /// <summary>
        /// Gets or sets the load time.
        /// </summary>
        /// <value>The load time.</value>
        public required DateTime LoadTime { get; init; }
        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        /// <value>The state.</value>
        public PluginState State { get; set; } = PluginState.Loaded;
        /// <summary>
        /// Gets or sets the health.
        /// </summary>
        /// <value>The health.</value>
        public PluginHealth Health { get; set; } = PluginHealth.Unknown;
        /// <summary>
        /// Gets or sets the execution count.
        /// </summary>
        /// <value>The execution count.</value>
        public long ExecutionCount { get; set; }
        /// <summary>
        /// Gets or sets the last execution.
        /// </summary>
        /// <value>The last execution.</value>
        public DateTime LastExecution { get; set; }
        /// <summary>
        /// Gets or sets the total execution time.
        /// </summary>
        /// <value>The total execution time.</value>
        public TimeSpan TotalExecutionTime { get; set; }
        /// <summary>
        /// Gets or sets the last error.
        /// </summary>
        /// <value>The last error.</value>
        public Exception? LastError { get; set; }
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

    // PluginAssemblyLoadContext moved to dedicated file: Management/Loading/PluginAssemblyLoadContext.cs

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