// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using DotCompute.Algorithms.Management.Metadata;
using Microsoft.Extensions.Logging;
using System;

namespace DotCompute.Algorithms.Management.Infrastructure;

/// <summary>
/// Handles plugin metadata loading, validation, and management.
/// </summary>
public sealed partial class AlgorithmMetadata(ILogger<AlgorithmMetadata> logger) : IDisposable
{
    private readonly ILogger<AlgorithmMetadata> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<string, PluginMetadata> _metadataCache = new();
    private bool _disposed;

    /// <summary>
    /// Loads plugin metadata from a manifest file or assembly attributes.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly.</param>
    /// <returns>Plugin metadata if found; otherwise, null.</returns>
    public async Task<PluginMetadata?> LoadMetadataAsync(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Check cache first
        if (_metadataCache.TryGetValue(assemblyPath, out var cachedMetadata))
        {
            return cachedMetadata;
        }

        try
        {
            // Try to load from manifest file first
            var metadata = await LoadFromManifestAsync(assemblyPath).ConfigureAwait(false) ?? await LoadFromAssemblyAttributesAsync(assemblyPath).ConfigureAwait(false);

            // Cache the result (even if null)
            if (metadata != null)
            {
                _ = _metadataCache.TryAdd(assemblyPath, metadata);
                LogMetadataLoaded(_logger, assemblyPath, metadata.Id);
            }

            return metadata;
        }
        catch (Exception ex)
        {
            LogMetadataLoadFailed(_logger, assemblyPath, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Validates plugin metadata for completeness and correctness.
    /// </summary>
    /// <param name="metadata">The metadata to validate.</param>
    /// <returns>Validation result with any issues found.</returns>
    public MetadataValidationResult ValidateMetadata(PluginMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = new MetadataValidationResult { IsValid = true };
        var issues = new List<string>();

        // Required field validation
        if (string.IsNullOrWhiteSpace(metadata.Id))
        {
            issues.Add("Plugin ID is required");
        }

        if (string.IsNullOrWhiteSpace(metadata.Name))
        {
            issues.Add("Plugin name is required");
        }

        if (string.IsNullOrWhiteSpace(metadata.Version))
        {
            issues.Add("Plugin version is required");
        }
        else if (!Version.TryParse(metadata.Version, out _))
        {
            issues.Add($"Invalid version format: {metadata.Version}");
        }

        if (string.IsNullOrWhiteSpace(metadata.Description))
        {
            issues.Add("Plugin description is required");
        }

        if (string.IsNullOrWhiteSpace(metadata.Author))
        {
            issues.Add("Plugin author is required");
        }

        if (string.IsNullOrWhiteSpace(metadata.AssemblyPath))
        {
            issues.Add("Assembly path is required");
        }
        else if (!File.Exists(metadata.AssemblyPath))
        {
            issues.Add($"Assembly file not found: {metadata.AssemblyPath}");
        }

        // Framework version validation
        if (metadata.RequiredFrameworkVersion != null)
        {
            var currentVersion = Environment.Version;
            if (currentVersion < metadata.RequiredFrameworkVersion)
            {
                issues.Add($"Framework version {metadata.RequiredFrameworkVersion} is higher than current {currentVersion}");
            }
        }

        // Dependency validation
        foreach (var dependency in metadata.Dependencies)
        {
            if (string.IsNullOrWhiteSpace(dependency))
            {
                issues.Add("Empty dependency found");
            }
        }

        result.IsValid = issues.Count == 0;
        result.Issues = issues;

        if (!result.IsValid)
        {
            LogMetadataValidationFailed(_logger, metadata.Id, string.Join(", ", issues));
        }

        return result;
    }

    /// <summary>
    /// Creates default metadata for a plugin when no manifest is available.
    /// </summary>
    /// <param name="assemblyPath">The assembly path.</param>
    /// <param name="pluginType">The plugin type.</param>
    /// <returns>Default metadata instance.</returns>
    public PluginMetadata CreateDefaultMetadata(string assemblyPath, Type pluginType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ArgumentNullException.ThrowIfNull(pluginType);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
        var assembly = pluginType.Assembly;

        return new PluginMetadata
        {
            Id = pluginType.FullName ?? pluginType.Name,
            Name = pluginType.Name,
            Version = assembly.GetName().Version?.ToString() ?? "1.0.0",
            Description = $"Algorithm plugin from {assemblyName}",
            Author = "Unknown",
            AssemblyPath = assemblyPath,
            LoadTime = DateTime.UtcNow,
            RequiredFrameworkVersion = Environment.Version,
            AssemblyName = assemblyName,
            TypeName = pluginType.FullName ?? pluginType.Name,
            Capabilities = [],
            SupportedAccelerators = [],
            LoadContextName = "Default",
            AdditionalMetadata = []
        };
    }

    /// <summary>
    /// Updates existing metadata with runtime information.
    /// </summary>
    /// <param name="metadata">The metadata to update.</param>
    /// <param name="runtimeInfo">Runtime information to add.</param>
    public void UpdateMetadataWithRuntimeInfo(PluginMetadata metadata, RuntimeInfo runtimeInfo)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(runtimeInfo);
        ObjectDisposedException.ThrowIf(_disposed, this);

        metadata.AdditionalMetadata["RuntimeVersion"] = runtimeInfo.RuntimeVersion;
        metadata.AdditionalMetadata["LoadTime"] = runtimeInfo.LoadTime;
        metadata.AdditionalMetadata["InitializationTime"] = runtimeInfo.InitializationTime;
        metadata.AdditionalMetadata["MemoryUsage"] = runtimeInfo.InitialMemoryUsage;
        metadata.AdditionalMetadata["ProcessorArchitecture"] = runtimeInfo.ProcessorArchitecture;
        metadata.AdditionalMetadata["OperatingSystem"] = runtimeInfo.OperatingSystem;

        LogMetadataUpdated(_logger, metadata.Id);
    }

    /// <summary>
    /// Clears metadata cache.
    /// </summary>
    public void ClearCache()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _metadataCache.Clear();
        LogMetadataCacheCleared(_logger);
    }

    /// <summary>
    /// Gets cached metadata for an assembly.
    /// </summary>
    /// <param name="assemblyPath">The assembly path.</param>
    /// <returns>Cached metadata if available; otherwise, null.</returns>
    public PluginMetadata? GetCachedMetadata(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _metadataCache.TryGetValue(assemblyPath, out var metadata) ? metadata : null;
    }

    /// <summary>
    /// Loads metadata from a JSON manifest file.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute",
        Justification = "JSON serialization used for plugin metadata only. Types are well-defined and preserved.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCodeAttribute",
        Justification = "JSON serialization used for plugin metadata only.")]
    private async Task<PluginMetadata?> LoadFromManifestAsync(string assemblyPath)
    {
        var manifestPath = Path.ChangeExtension(assemblyPath, ".json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath).ConfigureAwait(false);
            var metadata = JsonSerializer.Deserialize<PluginMetadata>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            });

            if (metadata != null)
            {
                // Ensure assembly path is correct
                metadata.AssemblyPath = assemblyPath;
                metadata.LoadTime = DateTime.UtcNow;
            }

            return metadata;
        }
        catch (Exception ex)
        {
            LogManifestLoadFailed(_logger, manifestPath, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Loads metadata from assembly attributes.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCodeAttribute",
        Justification = "Plugin infrastructure requires dynamic assembly inspection for metadata extraction.")]
    private async Task<PluginMetadata?> LoadFromAssemblyAttributesAsync(string assemblyPath)
    {
        try
        {
            // Load assembly for attribute inspection (in default context to avoid isolation issues)
            var assembly = Assembly.LoadFrom(assemblyPath);

            // Extract metadata from assembly attributes
            var titleAttribute = assembly.GetCustomAttribute<AssemblyTitleAttribute>();
            var descriptionAttribute = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();
            var companyAttribute = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
            var versionAttribute = assembly.GetCustomAttribute<AssemblyVersionAttribute>();

            if (titleAttribute == null && descriptionAttribute == null)
            {
                return null; // No meaningful attributes found
            }

            var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
            var metadata = new PluginMetadata
            {
                Id = assembly.GetName().Name ?? assemblyName,
                Name = titleAttribute?.Title ?? assembly.GetName().Name ?? assemblyName,
                Version = versionAttribute?.Version ?? assembly.GetName().Version?.ToString() ?? "1.0.0",
                Description = descriptionAttribute?.Description ?? "Plugin loaded from assembly attributes",
                Author = companyAttribute?.Company ?? "Unknown",
                AssemblyPath = assemblyPath,
                LoadTime = DateTime.UtcNow,
                RequiredFrameworkVersion = ParseFrameworkVersion(assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName),
                AssemblyName = assemblyName,
                TypeName = "Unknown",
                Capabilities = [],
                SupportedAccelerators = [],
                LoadContextName = "Default",
                AdditionalMetadata = []
            };

            await Task.CompletedTask; // Make method async for consistency
            return metadata;
        }
        catch (Exception ex)
        {
            LogAttributeLoadFailed(_logger, assemblyPath, ex.Message);
            return null;
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _metadataCache.Clear();
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Metadata loaded for assembly {AssemblyPath}, plugin ID: {PluginId}")]
    private static partial void LogMetadataLoaded(ILogger logger, string assemblyPath, string pluginId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load metadata from {AssemblyPath}: {Reason}")]
    private static partial void LogMetadataLoadFailed(ILogger logger, string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Metadata validation failed for plugin {PluginId}: {Issues}")]
    private static partial void LogMetadataValidationFailed(ILogger logger, string pluginId, string issues);

    [LoggerMessage(Level = LogLevel.Information, Message = "Metadata updated for plugin {PluginId}")]
    private static partial void LogMetadataUpdated(ILogger logger, string pluginId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Metadata cache cleared")]
    private static partial void LogMetadataCacheCleared(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load manifest from {ManifestPath}: {Reason}")]
    private static partial void LogManifestLoadFailed(ILogger logger, string manifestPath, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load attributes from {AssemblyPath}: {Reason}")]
    private static partial void LogAttributeLoadFailed(ILogger logger, string assemblyPath, string reason);

    #endregion

    /// <summary>
    /// Parses a framework version string to extract the Version object.
    /// Handles framework names like ".NETCoreApp,Version=v9.0" or ".NET Framework 4.8".
    /// </summary>
    /// <param name="frameworkName">The framework name string.</param>
    /// <returns>The parsed Version object, or null if parsing fails.</returns>
    private static Version? ParseFrameworkVersion(string? frameworkName)
    {
        if (string.IsNullOrWhiteSpace(frameworkName))
        {
            return null;
        }

        try
        {
            // Handle formats like ".NETCoreApp,Version=v9.0"
            if (frameworkName.Contains("Version=", StringComparison.OrdinalIgnoreCase))
            {
                var versionPart = frameworkName.Substring(frameworkName.IndexOf("Version=", StringComparison.OrdinalIgnoreCase) + 8);
                if (versionPart.StartsWith("v", StringComparison.CurrentCulture))
                {
                    versionPart = versionPart.Substring(1);
                }
                return Version.Parse(versionPart);
            }

            // Handle formats like ".NET Framework 4.8"
            var parts = frameworkName.Split(' ');
            if (parts.Length >= 2 && Version.TryParse(parts[^1], out var version))
            {
                return version;
            }

            // Try direct parsing
            if (Version.TryParse(frameworkName, out var directVersion))
            {
                return directVersion;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Result of metadata validation.
/// </summary>
public sealed class MetadataValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether sues.
    /// </summary>
    /// <value>The issues.</value>
    public IList<string> Issues { get; } = [];
}

/// <summary>
/// Runtime information for metadata enhancement.
/// </summary>
public sealed class RuntimeInfo
{
    /// <summary>
    /// Gets or sets the runtime version.
    /// </summary>
    /// <value>The runtime version.</value>
    public required string RuntimeVersion { get; set; }
    /// <summary>
    /// Gets or sets the load time.
    /// </summary>
    /// <value>The load time.</value>
    public DateTime LoadTime { get; set; }
    /// <summary>
    /// Gets or sets the initialization time.
    /// </summary>
    /// <value>The initialization time.</value>
    public TimeSpan InitializationTime { get; set; }
    /// <summary>
    /// Gets or sets the initial memory usage.
    /// </summary>
    /// <value>The initial memory usage.</value>
    public long InitialMemoryUsage { get; set; }
    /// <summary>
    /// Gets or sets the processor architecture.
    /// </summary>
    /// <value>The processor architecture.</value>
    public required string ProcessorArchitecture { get; set; }
    /// <summary>
    /// Gets or sets the operating system.
    /// </summary>
    /// <value>The operating system.</value>
    public required string OperatingSystem { get; set; }
}