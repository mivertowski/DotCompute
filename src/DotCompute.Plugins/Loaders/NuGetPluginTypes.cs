// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using System.Text.Json.Serialization;
using DotCompute.Plugins.Interfaces;

namespace DotCompute.Plugins.Loaders
{
    /// <summary>
    /// Manifest for NuGet-based plugins with comprehensive metadata.
    /// </summary>
    public class NuGetPluginManifest
    {
        /// <summary>
        /// Gets or sets the unique plugin identifier.
        /// </summary>
        public required string Id { get; set; }

        /// <summary>
        /// Gets or sets the display name of the plugin.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the plugin version.
        /// </summary>
        public required string Version { get; set; }

        /// <summary>
        /// Gets or sets the plugin description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the plugin author.
        /// </summary>
        public string? Author { get; set; }

        /// <summary>
        /// Gets or sets the path to the main assembly.
        /// </summary>
        public required string AssemblyPath { get; set; }

        /// <summary>
        /// Gets or sets the entry point type name.
        /// </summary>
        public string? EntryPointType { get; set; }

        /// <summary>
        /// Gets or sets the minimum framework version required.
        /// </summary>
        public string? MinFrameworkVersion { get; set; }

        /// <summary>
        /// Gets or sets the target framework moniker.
        /// </summary>
        public string? TargetFramework { get; set; }

        /// <summary>
        /// Gets or sets the plugin dependencies.
        /// </summary>
        public List<NuGetPackageDependency> Dependencies { get; set; } = [];

        /// <summary>
        /// Gets or sets the required interfaces that must be present.
        /// </summary>
        public List<string> RequiredInterfaces { get; set; } = [];

        /// <summary>
        /// Gets or sets the security permissions required by the plugin.
        /// </summary>
        public List<string> RequiredPermissions { get; set; } = [];

        /// <summary>
        /// Gets or sets the plugin capabilities.
        /// </summary>
        public PluginCapabilities Capabilities { get; set; } = PluginCapabilities.None;

        /// <summary>
        /// Gets or sets supported platforms.
        /// </summary>
        public List<string> SupportedPlatforms { get; set; } = [];

        /// <summary>
        /// Gets or sets the plugin tags for categorization.
        /// </summary>
        public List<string> Tags { get; set; } = [];

        /// <summary>
        /// Gets or sets additional metadata.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = [];

        /// <summary>
        /// Gets or sets the package source URL if loaded from a NuGet package.
        /// </summary>
        public string? PackageSource { get; set; }

        /// <summary>
        /// Gets or sets the license information.
        /// </summary>
        public PluginLicense? License { get; set; }

        /// <summary>
        /// Gets or sets the digital signature information.
        /// </summary>
        public PluginSignature? Signature { get; set; }
    }

    /// <summary>
    /// Represents a NuGet package dependency.
    /// </summary>
    public class NuGetPackageDependency
    {
        /// <summary>
        /// Gets or sets the package ID.
        /// </summary>
        public required string Id { get; set; }

        /// <summary>
        /// Gets or sets the version range (e.g., "[1.0.0,2.0.0)").
        /// </summary>
        public required string VersionRange { get; set; }

        /// <summary>
        /// Gets or sets the target framework for this dependency.
        /// </summary>
        public string? TargetFramework { get; set; }

        /// <summary>
        /// Gets or sets whether this dependency is optional.
        /// </summary>
        public bool IsOptional { get; set; }

        /// <summary>
        /// Gets or sets the dependency exclusions.
        /// </summary>
        public List<string> Exclude { get; set; } = [];
    }

    /// <summary>
    /// License information for a plugin.
    /// </summary>
    public class PluginLicense
    {
        /// <summary>
        /// Gets or sets the license type (e.g., "MIT", "Apache-2.0").
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Gets or sets the license URL.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets the license expression (SPDX format).
        /// </summary>
        public string? Expression { get; set; }

        /// <summary>
        /// Gets or sets whether the license requires acceptance.
        /// </summary>
        public bool RequiresAcceptance { get; set; }
    }

    /// <summary>
    /// Digital signature information for a plugin.
    /// </summary>
    public class PluginSignature
    {
        /// <summary>
        /// Gets or sets whether the plugin is signed.
        /// </summary>
        public bool IsSigned { get; set; }

        /// <summary>
        /// Gets or sets the signer certificate thumbprint.
        /// </summary>
        public string? CertificateThumbprint { get; set; }

        /// <summary>
        /// Gets or sets the signing algorithm used.
        /// </summary>
        public string? SigningAlgorithm { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of signing.
        /// </summary>
        public DateTimeOffset? SignedAt { get; set; }

        /// <summary>
        /// Gets or sets the trusted publisher information.
        /// </summary>
        public string? Publisher { get; set; }

        /// <summary>
        /// Gets or sets whether the signature is valid.
        /// </summary>
        public bool IsValid { get; set; }
    }

    /// <summary>
    /// Result of loading a NuGet plugin.
    /// </summary>
    public class NuGetPluginLoadResult
    {
        /// <summary>
        /// Gets or sets whether the plugin was loaded successfully.
        /// </summary>
        public bool IsLoaded { get; set; }

        /// <summary>
        /// Gets or sets the loaded plugin information.
        /// </summary>
        public LoadedNuGetPlugin? Plugin { get; set; }

        /// <summary>
        /// Gets or sets the error message if loading failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the load result type.
        /// </summary>
        public PluginLoadResultType ResultType { get; set; }

        /// <summary>
        /// Gets or sets additional error details.
        /// </summary>
        public List<string> ErrorDetails { get; set; } = [];

        /// <summary>
        /// Creates a successful load result.
        /// </summary>
        public static NuGetPluginLoadResult Success(LoadedNuGetPlugin plugin) => new()
        {
            IsLoaded = true,
            Plugin = plugin,
            ResultType = PluginLoadResultType.Success
        };

        /// <summary>
        /// Creates a result indicating the plugin is already loaded.
        /// </summary>
        public static NuGetPluginLoadResult AlreadyLoaded(string pluginId) => new()
        {
            IsLoaded = true,
            ResultType = PluginLoadResultType.AlreadyLoaded,
            ErrorMessage = $"Plugin {pluginId} is already loaded"
        };

        /// <summary>
        /// Creates a result indicating validation failed.
        /// </summary>
        public static NuGetPluginLoadResult ValidationFailed(string pluginId, IEnumerable<string> errors) => new()
        {
            IsLoaded = false,
            ResultType = PluginLoadResultType.ValidationFailed,
            ErrorMessage = $"Validation failed for plugin {pluginId}",
            ErrorDetails = errors.ToList()
        };

        /// <summary>
        /// Creates a result indicating dependency resolution failed.
        /// </summary>
        public static NuGetPluginLoadResult DependencyResolutionFailed(string pluginId, IEnumerable<string> errors) => new()
        {
            IsLoaded = false,
            ResultType = PluginLoadResultType.DependencyResolutionFailed,
            ErrorMessage = $"Dependency resolution failed for plugin {pluginId}",
            ErrorDetails = errors.ToList()
        };

        /// <summary>
        /// Creates a result indicating a load error occurred.
        /// </summary>
        public static NuGetPluginLoadResult LoadError(string pluginId, string error) => new()
        {
            IsLoaded = false,
            ResultType = PluginLoadResultType.LoadError,
            ErrorMessage = $"Failed to load plugin {pluginId}: {error}"
        };
    }

    /// <summary>
    /// Type of plugin load result.
    /// </summary>
    public enum PluginLoadResultType
    {
        /// <summary>
        /// Plugin loaded successfully.
        /// </summary>
        Success,

        /// <summary>
        /// Plugin was already loaded.
        /// </summary>
        AlreadyLoaded,

        /// <summary>
        /// Plugin validation failed.
        /// </summary>
        ValidationFailed,

        /// <summary>
        /// Dependency resolution failed.
        /// </summary>
        DependencyResolutionFailed,

        /// <summary>
        /// Load error occurred.
        /// </summary>
        LoadError
    }

    /// <summary>
    /// Information about a loaded NuGet plugin.
    /// </summary>
    public class LoadedNuGetPlugin
    {
        /// <summary>
        /// Gets or sets the plugin manifest.
        /// </summary>
        public required NuGetPluginManifest Manifest { get; set; }

        /// <summary>
        /// Gets or sets the loaded plugin instance.
        /// </summary>
        public IBackendPlugin? Plugin { get; set; }

        /// <summary>
        /// Gets or sets the assembly load context.
        /// </summary>
        public NuGetPluginLoadContext? LoadContext { get; set; }

        /// <summary>
        /// Gets or sets the main assembly.
        /// </summary>
        public Assembly? Assembly { get; set; }

        /// <summary>
        /// Gets or sets the resolved dependency graph.
        /// </summary>
        public DependencyGraph DependencyGraph { get; set; } = new();

        /// <summary>
        /// Gets or sets when the plugin was loaded.
        /// </summary>
        public DateTimeOffset LoadedAt { get; set; }

        /// <summary>
        /// Gets or sets the load duration.
        /// </summary>
        public TimeSpan LoadDuration { get; set; }

        /// <summary>
        /// Gets or sets the runtime metrics for the plugin.
        /// </summary>
        public PluginRuntimeMetrics Metrics { get; set; } = new();
    }

    /// <summary>
    /// Runtime metrics for a loaded plugin.
    /// </summary>
    public class PluginRuntimeMetrics
    {
        /// <summary>
        /// Gets or sets the memory usage in bytes.
        /// </summary>
        public long MemoryUsage { get; set; }

        /// <summary>
        /// Gets or sets the CPU usage percentage.
        /// </summary>
        public double CpuUsage { get; set; }

        /// <summary>
        /// Gets or sets the number of active requests.
        /// </summary>
        public int ActiveRequests { get; set; }

        /// <summary>
        /// Gets or sets the total number of processed requests.
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Gets or sets the error count.
        /// </summary>
        public long ErrorCount { get; set; }

        /// <summary>
        /// Gets or sets the average response time in milliseconds.
        /// </summary>
        public double AverageResponseTime { get; set; }

        /// <summary>
        /// Gets or sets custom metrics specific to the plugin.
        /// </summary>
        public Dictionary<string, object> CustomMetrics { get; set; } = [];
    }

    /// <summary>
    /// Result of plugin validation.
    /// </summary>
    public class NuGetPluginValidationResult
    {
        /// <summary>
        /// Gets or sets whether the plugin is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the validation errors.
        /// </summary>
        public List<string> ValidationErrors { get; set; } = [];

        /// <summary>
        /// Gets or sets the validation warnings.
        /// </summary>
        public List<string> ValidationWarnings { get; set; } = [];

        /// <summary>
        /// Gets or sets the security scan results.
        /// </summary>
        public SecurityScanResult? SecurityScan { get; set; }

        /// <summary>
        /// Gets or sets the compatibility check results.
        /// </summary>
        public CompatibilityCheckResult? CompatibilityCheck { get; set; }

        /// <summary>
        /// Gets or sets additional validation metadata.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = [];
    }

    /// <summary>
    /// Configuration options for the NuGet plugin loader.
    /// </summary>
    public class NuGetPluginLoaderOptions
    {
        /// <summary>
        /// Gets or sets the directories to search for plugins.
        /// </summary>
        public List<string> PluginDirectories { get; set; } = [];

        /// <summary>
        /// Gets or sets the NuGet package sources to use.
        /// </summary>
        public List<string> PackageSources { get; set; } = [];

        /// <summary>
        /// Gets or sets the security policy for plugin loading.
        /// </summary>
        public SecurityPolicy? SecurityPolicy { get; set; }

        /// <summary>
        /// Gets or sets the dependency resolution settings.
        /// </summary>
        public DependencyResolutionSettings DependencyResolution { get; set; } = new();

        /// <summary>
        /// Gets or sets the compatibility checking settings.
        /// </summary>
        public CompatibilitySettings CompatibilitySettings { get; set; } = new();

        /// <summary>
        /// Gets or sets whether to enable parallel plugin loading.
        /// </summary>
        public bool EnableParallelLoading { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of concurrent plugin loads.
        /// </summary>
        public int MaxConcurrentLoads { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// Gets or sets the plugin load timeout.
        /// </summary>
        public TimeSpan LoadTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets whether to cache resolved dependencies.
        /// </summary>
        public bool CacheDependencies { get; set; } = true;

        /// <summary>
        /// Gets or sets the dependency cache expiration time.
        /// </summary>
        public TimeSpan DependencyCacheExpiration { get; set; } = TimeSpan.FromHours(1);
    }

    /// <summary>
    /// Information about a NuGet package for caching purposes.
    /// </summary>
    public class NuGetPackageInfo
    {
        /// <summary>
        /// Gets or sets the package ID.
        /// </summary>
        public required string Id { get; set; }

        /// <summary>
        /// Gets or sets the package version.
        /// </summary>
        public required string Version { get; set; }

        /// <summary>
        /// Gets or sets the package path.
        /// </summary>
        public string? PackagePath { get; set; }

        /// <summary>
        /// Gets or sets the package dependencies.
        /// </summary>
        public List<NuGetPackageDependency> Dependencies { get; set; } = [];

        /// <summary>
        /// Gets or sets the package metadata.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = [];

        /// <summary>
        /// Gets or sets when the package was cached.
        /// </summary>
        public DateTimeOffset CachedAt { get; set; }

        /// <summary>
        /// Gets or sets whether the package has been verified.
        /// </summary>
        public bool IsVerified { get; set; }
    }
}