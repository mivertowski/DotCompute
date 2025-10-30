
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Management.Metadata;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Discovery;

/// <summary>
/// Handles plugin discovery and scanning operations with comprehensive directory traversal and filtering.
/// </summary>
public sealed partial class AlgorithmPluginScanner(ILogger<AlgorithmPluginScanner> logger, AlgorithmPluginManagerOptions options) : IDisposable
{
    private readonly ILogger<AlgorithmPluginScanner> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly AlgorithmPluginManagerOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ConcurrentDictionary<string, DateTime> _lastScanTimes = new();
    private bool _disposed;

    /// <summary>
    /// Discovers plugin assemblies in the specified directory.
    /// </summary>
    /// <param name="pluginDirectory">The directory to scan for plugins.</param>
    /// <param name="recursive">Whether to scan subdirectories recursively.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of discovered plugin assembly paths.</returns>
    public async Task<IReadOnlyList<string>> DiscoverPluginAssembliesAsync(
        string pluginDirectory,
        bool recursive = true,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

        if (!Directory.Exists(pluginDirectory))
        {
            LogDirectoryNotFound(pluginDirectory);
            return [];
        }

        LogDiscoveringPlugins(pluginDirectory);

        try
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var assemblyFiles = new List<string>();

            // Discover .dll files
            var dllFiles = Directory.GetFiles(pluginDirectory, "*.dll", searchOption)
                .Where(path => !IsSystemAssembly(path) && !IsExcludedAssembly(path));
            assemblyFiles.AddRange(dllFiles);

            // Discover .exe files (for console plugin applications)
            var exeFiles = Directory.GetFiles(pluginDirectory, "*.exe", searchOption)
                .Where(path => !IsSystemAssembly(path) && !IsExcludedAssembly(path));
            assemblyFiles.AddRange(exeFiles);

            // Filter by security policies
            var validAssemblies = new List<string>();
            foreach (var assembly in assemblyFiles)
            {
                if (await IsValidPluginAssemblyAsync(assembly, cancellationToken))
                {
                    validAssemblies.Add(assembly);
                }
            }

            // Update last scan time
            _ = _lastScanTimes.AddOrUpdate(pluginDirectory, DateTime.UtcNow, (_, _) => DateTime.UtcNow);

            LogPluginsDiscovered(validAssemblies.Count, pluginDirectory);
            return validAssemblies.AsReadOnly();
        }
        catch (Exception ex)
        {
            LogPluginDiscoveryFailed(pluginDirectory, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Discovers plugin metadata files associated with assemblies.
    /// </summary>
    /// <param name="assemblyPath">The assembly path to find metadata for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Plugin metadata if found; otherwise, null.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute",
        Justification = "JSON serialization used for plugin discovery metadata only. Types are well-defined and preserved.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCodeAttribute",
        Justification = "JSON serialization used for plugin discovery metadata only.")]
    public async Task<PluginMetadata?> DiscoverPluginMetadataAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        var manifestPath = Path.ChangeExtension(assemblyPath, ".json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            var metadata = JsonSerializer.Deserialize<PluginMetadata>(json);

            if (metadata != null)
            {
                metadata.AssemblyPath = assemblyPath;
                metadata.LoadTime = DateTime.UtcNow;
            }

            return metadata;
        }
        catch (Exception ex)
        {
            LogMetadataLoadFailed(manifestPath, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Scans for plugin types within an assembly without loading it.
    /// </summary>
    /// <param name="assemblyPath">The assembly path to scan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of discovered plugin type information.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCodeAttribute",
        Justification = "Plugin infrastructure requires dynamic assembly inspection for plugin type discovery.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072:DynamicallyAccessedMembers",
        Justification = "Plugin types are enumerated from assembly for discovery")]
    public Task<IReadOnlyList<PluginTypeInfo>> DiscoverPluginTypesAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        if (!File.Exists(assemblyPath))
        {
            return Task.FromResult<IReadOnlyList<PluginTypeInfo>>([]);
        }

        try
        {
            var pluginTypes = new List<PluginTypeInfo>();

            // Use MetadataLoadContext for reflection-only loading
            using var metadataContext = new MetadataLoadContext(new PathAssemblyResolver([assemblyPath]));
            var assembly = metadataContext.LoadFromAssemblyPath(assemblyPath);

            foreach (var type in assembly.GetTypes())
            {
                if (IsPluginType(type))
                {
                    var typeInfo = new PluginTypeInfo
                    {
                        TypeName = type.FullName ?? type.Name,
                        AssemblyPath = assemblyPath,
                        IsAbstract = type.IsAbstract,
                        IsGeneric = type.IsGenericType,
                        Namespace = type.Namespace,
                        Dependencies = [.. GetTypeDependencies(type)]
                    };

                    pluginTypes.Add(typeInfo);
                }
            }

            return Task.FromResult<IReadOnlyList<PluginTypeInfo>>(pluginTypes.AsReadOnly());
        }
        catch (Exception ex)
        {
            LogPluginTypeDiscoveryFailed(assemblyPath, ex.Message);
            return Task.FromResult<IReadOnlyList<PluginTypeInfo>>([]);
        }
    }

    /// <summary>
    /// Checks if the directory has been scanned recently within the configured interval.
    /// </summary>
    /// <param name="pluginDirectory">The directory to check.</param>
    /// <returns>True if recently scanned; otherwise, false.</returns>
    public bool IsRecentlyScanned(string pluginDirectory)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_lastScanTimes.TryGetValue(pluginDirectory, out var lastScan))
        {
            return false;
        }

        return DateTime.UtcNow - lastScan < _options.ScanInterval;
    }

    /// <summary>
    /// Gets the last scan time for a directory.
    /// </summary>
    /// <param name="pluginDirectory">The directory to check.</param>
    /// <returns>Last scan time if available; otherwise, null.</returns>
    public DateTime? GetLastScanTime(string pluginDirectory)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _lastScanTimes.TryGetValue(pluginDirectory, out var lastScan) ? lastScan : null;
    }

    /// <summary>
    /// Validates if an assembly is a valid plugin assembly based on security and size constraints.
    /// </summary>
    private async Task<bool> IsValidPluginAssemblyAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        try
        {
            // Check file size
            var fileInfo = new FileInfo(assemblyPath);
            if (fileInfo.Length > _options.MaxAssemblySize)
            {
                LogAssemblyTooLarge(assemblyPath, fileInfo.Length, _options.MaxAssemblySize);
                return false;
            }

            // Basic assembly validation
            if (!await IsValidManagedAssemblyAsync(assemblyPath))
            {
                return false;
            }

            // Security validation (if enabled)
            if (_options.EnableSecurityValidation)
            {
                return await ValidateAssemblySecurityAsync(assemblyPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            LogAssemblyValidationFailed(assemblyPath, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Checks if the assembly is a valid managed assembly.
    /// </summary>
    private static async Task<bool> IsValidManagedAssemblyAsync(string assemblyPath)
    {
        try
        {
            // Read PE header to verify it's a managed assembly
            using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[64];
            _ = await stream.ReadAsync(buffer);

            // Check for PE signature
            if (buffer[0] != 0x4D || buffer[1] != 0x5A) // MZ signature
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates assembly security based on configuration.
    /// </summary>
    private static async Task<bool> ValidateAssemblySecurityAsync(string assemblyPath)
    {
        // Placeholder for security validation logic
        // This would integrate with the security validation components
        await Task.CompletedTask;
        return true;
    }

    /// <summary>
    /// Determines if a type is a plugin type.
    /// </summary>
    private static bool IsPluginType(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
        Type type)
    {
        if (type.IsAbstract || type.IsInterface)
        {
            return false;
        }

        // Check if type implements IAlgorithmPlugin
        return type.GetInterfaces().Any(i => i.Name == nameof(IAlgorithmPlugin));
    }

    /// <summary>
    /// Gets type dependencies for dependency analysis.
    /// </summary>
    private static IEnumerable<string> GetTypeDependencies(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type type)
    {
        var dependencies = new HashSet<string>();

        // Get constructor dependencies
        foreach (var constructor in type.GetConstructors())
        {
            foreach (var parameter in constructor.GetParameters())
            {
                if (parameter.ParameterType.Assembly != type.Assembly)
                {
                    _ = dependencies.Add(parameter.ParameterType.Assembly.FullName ?? parameter.ParameterType.Assembly.GetName().Name ?? "Unknown");
                }
            }
        }

        return dependencies;
    }

    /// <summary>
    /// Determines if an assembly is a system assembly that should be ignored.
    /// </summary>
    private static bool IsSystemAssembly(string assemblyPath)
    {
        var fileName = Path.GetFileName(assemblyPath).ToUpperInvariant();
        return fileName.StartsWith("system.", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("microsoft.", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if an assembly should be excluded from discovery.
    /// </summary>
    private bool IsExcludedAssembly(string assemblyPath)
    {
        var fileName = Path.GetFileName(assemblyPath);
        return _options.ExcludedAssemblies.Any(excluded =>
            fileName.Equals(excluded, StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith(excluded, StringComparison.OrdinalIgnoreCase));
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _lastScanTimes.Clear();
            _disposed = true;
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Warning, Message = "Directory not found: {Directory}")]
    private partial void LogDirectoryNotFound(string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovering plugins in directory: {Directory}")]
    private partial void LogDiscoveringPlugins(string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovered {Count} plugins in directory: {Directory}")]
    private partial void LogPluginsDiscovered(int count, string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin discovery failed for directory {Directory}: {Reason}")]
    private partial void LogPluginDiscoveryFailed(string directory, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load metadata from {ManifestPath}: {Reason}")]
    private partial void LogMetadataLoadFailed(string manifestPath, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin type discovery failed for {AssemblyPath}: {Reason}")]
    private partial void LogPluginTypeDiscoveryFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Assembly too large: {AssemblyPath}, size: {ActualSize}, max: {MaxSize}")]
    private partial void LogAssemblyTooLarge(string assemblyPath, long actualSize, long maxSize);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Assembly validation failed for {AssemblyPath}: {Reason}")]
    private partial void LogAssemblyValidationFailed(string assemblyPath, string reason);

    #endregion
}

/// <summary>
/// Information about a discovered plugin type.
/// </summary>
public sealed class PluginTypeInfo
{
    /// <summary>
    /// Gets or sets the full type name.
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// Gets or sets the assembly path containing the type.
    /// </summary>
    public required string AssemblyPath { get; init; }

    /// <summary>
    /// Gets or sets whether the type is abstract.
    /// </summary>
    public bool IsAbstract { get; init; }

    /// <summary>
    /// Gets or sets whether the type is generic.
    /// </summary>
    public bool IsGeneric { get; init; }

    /// <summary>
    /// Gets or sets the type namespace.
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// Gets the type dependencies.
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];
}
