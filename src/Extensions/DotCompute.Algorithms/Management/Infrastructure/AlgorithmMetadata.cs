// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Infrastructure;

/// <summary>
/// Handles plugin metadata loading, validation, and management.
/// </summary>
public sealed class AlgorithmMetadata : IDisposable
{
    private readonly ILogger<AlgorithmMetadata> _logger;
    private readonly ConcurrentDictionary<string, PluginMetadata> _metadataCache;
    private bool _disposed;

    public AlgorithmMetadata(ILogger<AlgorithmMetadata> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metadataCache = new ConcurrentDictionary<string, PluginMetadata>();
    }

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
            var metadata = await LoadFromManifestAsync(assemblyPath).ConfigureAwait(false);

            // If no manifest, try to extract from assembly attributes
            if (metadata == null)
            {
                metadata = await LoadFromAssemblyAttributesAsync(assemblyPath).ConfigureAwait(false);
            }

            // Cache the result (even if null)
            if (metadata != null)
            {
                _metadataCache.TryAdd(assemblyPath, metadata);
                LogMetadataLoaded(assemblyPath, metadata.Id);
            }

            return metadata;
        }
        catch (Exception ex)
        {
            LogMetadataLoadFailed(assemblyPath, ex.Message);
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
        if (!string.IsNullOrEmpty(metadata.RequiredFrameworkVersion))
        {
            if (!Version.TryParse(metadata.RequiredFrameworkVersion, out var requiredVersion))
            {
                issues.Add($"Invalid framework version format: {metadata.RequiredFrameworkVersion}");
            }
            else
            {
                var currentVersion = Environment.Version;
                if (currentVersion < requiredVersion)
                {
                    issues.Add($"Framework version {requiredVersion} is higher than current {currentVersion}");
                }
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
            LogMetadataValidationFailed(metadata.Id, string.Join(", ", issues));
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
            RequiredFrameworkVersion = Environment.Version.ToString()
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

        LogMetadataUpdated(metadata.Id);
    }

    /// <summary>
    /// Clears metadata cache.
    /// </summary>
    public void ClearCache()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _metadataCache.Clear();
        LogMetadataCacheCleared();
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
            LogManifestLoadFailed(manifestPath, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Loads metadata from assembly attributes.
    /// </summary>
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

            var metadata = new PluginMetadata
            {
                Id = assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(assemblyPath),
                Name = titleAttribute?.Title ?? assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(assemblyPath),
                Version = versionAttribute?.Version ?? assembly.GetName().Version?.ToString() ?? "1.0.0",
                Description = descriptionAttribute?.Description ?? "Plugin loaded from assembly attributes",
                Author = companyAttribute?.Company ?? "Unknown",
                AssemblyPath = assemblyPath,
                LoadTime = DateTime.UtcNow,
                RequiredFrameworkVersion = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName
            };

            await Task.CompletedTask; // Make method async for consistency
            return metadata;
        }
        catch (Exception ex)
        {
            LogAttributeLoadFailed(assemblyPath, ex.Message);
            return null;
        }
    }

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
    private static partial void LogMetadataLoaded(string assemblyPath, string pluginId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load metadata from {AssemblyPath}: {Reason}")]
    private static partial void LogMetadataLoadFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Metadata validation failed for plugin {PluginId}: {Issues}")]
    private static partial void LogMetadataValidationFailed(string pluginId, string issues);

    [LoggerMessage(Level = LogLevel.Information, Message = "Metadata updated for plugin {PluginId}")]
    private static partial void LogMetadataUpdated(string pluginId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Metadata cache cleared")]
    private static partial void LogMetadataCacheCleared();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load manifest from {ManifestPath}: {Reason}")]
    private static partial void LogManifestLoadFailed(string manifestPath, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load attributes from {AssemblyPath}: {Reason}")]
    private static partial void LogAttributeLoadFailed(string assemblyPath, string reason);

    #endregion
}

/// <summary>
/// Metadata about a plugin loaded from manifest or assembly.
/// </summary>
public sealed class PluginMetadata
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Version { get; set; }
    public required string Description { get; set; }
    public required string Author { get; set; }
    public required string AssemblyPath { get; set; }
    public DateTime LoadTime { get; set; }
    public string? RequiredFrameworkVersion { get; set; }
    public List<string> Dependencies { get; } = [];
    public Dictionary<string, object> AdditionalMetadata { get; } = [];
}

/// <summary>
/// Result of metadata validation.
/// </summary>
public sealed class MetadataValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Issues { get; set; } = [];
}

/// <summary>
/// Runtime information for metadata enhancement.
/// </summary>
public sealed class RuntimeInfo
{
    public required string RuntimeVersion { get; set; }
    public DateTime LoadTime { get; set; }
    public TimeSpan InitializationTime { get; set; }
    public long InitialMemoryUsage { get; set; }
    public required string ProcessorArchitecture { get; set; }
    public required string OperatingSystem { get; set; }
}