// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Management.Core;
using DotCompute.Algorithms.Management.Metadata;
using DotCompute.Algorithms.Management.Validation;
using DotCompute.Algorithms.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Loading;

/// <summary>
/// Service responsible for discovering and loading plugins from assemblies and directories.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PluginDiscoveryService"/> class.
/// </remarks>
/// <param name="logger">The logger instance.</param>
/// <param name="lifecycleManager">The plugin lifecycle manager.</param>
/// <param name="securityValidator">The security validator.</param>
/// <param name="options">Configuration options.</param>
public sealed partial class PluginDiscoveryService(
    ILogger<PluginDiscoveryService> logger,
    IPluginLifecycleManager lifecycleManager,
    ISecurityValidator securityValidator,
    AlgorithmPluginManagerOptions options) : IPluginDiscoveryService
{
    private readonly ILogger<PluginDiscoveryService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IPluginLifecycleManager _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));
    private readonly ISecurityValidator _securityValidator = securityValidator ?? throw new ArgumentNullException(nameof(securityValidator));
    private readonly AlgorithmPluginManagerOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly SemaphoreSlim _loadingSemaphore = new(1, 1);

    /// <inheritdoc/>
    public async Task<int> DiscoverAndLoadPluginsAsync(string pluginDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

        if (!Directory.Exists(pluginDirectory))
        {
            LogDirectoryNotFound(pluginDirectory);
            return 0;
        }

        LogDiscoveringPlugins(pluginDirectory);

        var pluginFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories)
            .Where(file => !IsSystemAssembly(file))
            .ToList();

        var loadedCount = 0;

        foreach (var pluginFile in pluginFiles)
        {
            try
            {
                var count = await LoadPluginsFromAssemblyAsync(pluginFile, cancellationToken).ConfigureAwait(false);
                loadedCount += count;
            }
            catch (Exception ex)
            {
                LogPluginDiscoveryFailed(pluginFile, ex.Message);
            }
        }

        LogPluginsDiscovered(loadedCount, pluginDirectory);
        return loadedCount;
    }

    /// <inheritdoc/>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Plugin system requires dynamic loading")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Plugin system requires dynamic loading")]
    public async Task<int> LoadPluginsFromAssemblyAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Assembly file not found: {assemblyPath}");
        }

        await _loadingSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LogLoadingAssembly(assemblyPath);

            // Security validation
            if (_options.EnableSecurityValidation && !await _securityValidator.ValidateAssemblySecurityAsync(assemblyPath))
            {
                LogAssemblySecurityValidationFailed(assemblyPath);
                return 0;
            }

            // Load plugin metadata if available
            var metadata = await LoadPluginMetadataAsync(assemblyPath).ConfigureAwait(false);

            // Version compatibility check
            if (metadata != null && !_securityValidator.IsVersionCompatible(metadata.RequiredFrameworkVersion))
            {
                LogVersionIncompatible(assemblyPath, metadata.RequiredFrameworkVersion?.ToString() ?? "Unknown");
                return 0;
            }

            // Create isolated load context
            var loadContextName = $"PluginContext_{Path.GetFileNameWithoutExtension(assemblyPath)}_{Guid.NewGuid():N}";
            var loadContext = new PluginAssemblyLoadContext(loadContextName, assemblyPath, _options.EnablePluginIsolation);

            try
            {
                // Load assembly in isolated context
                var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

                // Discover plugin types
                var pluginTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && typeof(IAlgorithmPlugin).IsAssignableFrom(t));

                var loadedCount = 0;
                foreach (var pluginType in pluginTypes)
                {
                    try
                    {
                        if (CreatePluginInstance(pluginType) is IAlgorithmPlugin plugin)
                        {
                            var pluginMetadata = metadata ?? CreateDefaultMetadata(plugin, assemblyPath);
                            await _lifecycleManager.RegisterPluginAsync(plugin, loadContext, assembly, pluginMetadata, cancellationToken).ConfigureAwait(false);
                            loadedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogPluginLoadFailed(ex);
                    }
                }

                LogAssemblyLoaded(loadedCount, assembly.GetName().Name ?? "Unknown");
                return loadedCount;
            }
            catch
            {
                // Clean up load context on failure
                loadContext.Unload();
                throw;
            }
        }
        catch (Exception ex)
        {
            LogPluginLoadFailed(ex);
            throw;
        }
        finally
        {
            _ = _loadingSemaphore.Release();
        }
    }

    /// <summary>
    /// Creates a plugin instance with proper error handling.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Plugin instantiation requires dynamic type handling")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Plugin system requires dynamic constructor access for ILogger<T> pattern")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Plugin system requires generic type instantiation for ILogger<T> and NullLogger<T> by design")]
    private static IAlgorithmPlugin? CreatePluginInstance(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        Type pluginType)
    {
        try
        {
            // Try parameterless constructor first
            var constructor = pluginType.GetConstructor(Type.EmptyTypes);
            if (constructor != null)
            {
                return constructor.Invoke(null) as IAlgorithmPlugin;
            }

            // Try with logger parameter if available
            var loggerConstructor = pluginType.GetConstructor([typeof(ILogger<>).MakeGenericType(pluginType)]);
            if (loggerConstructor != null)
            {
                // Create a null logger for plugin instantiation
                var nullLoggerType = typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>).MakeGenericType(pluginType);
                var nullLogger = Activator.CreateInstance(nullLoggerType);
                return loggerConstructor.Invoke([nullLogger]) as IAlgorithmPlugin;
            }

            // Fallback to Activator.CreateInstance
            return Activator.CreateInstance(pluginType) as IAlgorithmPlugin;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Loads plugin metadata from a manifest file.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute",
        Justification = "JSON serialization used for plugin metadata only. Types are well-defined and preserved.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCodeAttribute",
        Justification = "JSON serialization used for plugin metadata only.")]
    private async Task<PluginMetadata?> LoadPluginMetadataAsync(string assemblyPath)
    {
        var manifestPath = Path.ChangeExtension(assemblyPath, ".json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath).ConfigureAwait(false);
            return System.Text.Json.JsonSerializer.Deserialize<PluginMetadata>(json);
        }
        catch (Exception ex)
        {
            LogMetadataLoadFailed(manifestPath, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Creates default metadata for a plugin.
    /// </summary>
    private static PluginMetadata CreateDefaultMetadata(IAlgorithmPlugin plugin, string assemblyPath)
    {
        return new PluginMetadata
        {
            Id = plugin.Id,
            Name = plugin.Name,
            Version = plugin.Version.ToString(),
            Description = plugin.Description,
            Author = "Unknown",
            AssemblyPath = assemblyPath,
            LoadTime = DateTime.UtcNow
        };
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

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Warning, Message = "Directory not found: {Directory}")]
    private partial void LogDirectoryNotFound(string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovering plugins in directory: {Directory}")]
    private partial void LogDiscoveringPlugins(string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovered and loaded {Count} plugins from directory: {Directory}")]
    private partial void LogPluginsDiscovered(int count, string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to discover plugin from {AssemblyPath}: {Reason}")]
    private partial void LogPluginDiscoveryFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading algorithm plugin from assembly {AssemblyPath}")]
    private partial void LogLoadingAssembly(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {PluginCount} algorithm plugins from assembly {AssemblyName}")]
    private partial void LogAssemblyLoaded(int pluginCount, string assemblyName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Assembly security validation failed for {AssemblyPath}")]
    private partial void LogAssemblySecurityValidationFailed(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Assembly version incompatible {AssemblyPath}, required: {RequiredVersion}")]
    private partial void LogVersionIncompatible(string assemblyPath, string requiredVersion);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load algorithm plugin")]
    private partial void LogPluginLoadFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load metadata from {ManifestPath}: {Reason}")]
    private partial void LogMetadataLoadFailed(string manifestPath, string reason);

    #endregion
}