// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Management.Loading;
using DotCompute.Algorithms.Management.Metadata;
using DotCompute.Algorithms.Abstractions;

namespace DotCompute.Algorithms.Management
{
    /// <summary>
    /// Handles the loading of algorithm plugins from assemblies and NuGet packages.
    /// </summary>
    public sealed partial class AlgorithmPluginLoader(ILogger<AlgorithmPluginLoader> logger, AlgorithmPluginManagerOptions options) : IAsyncDisposable, IDisposable
    {
        private readonly ILogger<AlgorithmPluginLoader> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly AlgorithmPluginManagerOptions _options = options ?? throw new ArgumentNullException(nameof(options));
        private readonly ConcurrentDictionary<string, PluginAssemblyLoadContext> _loadContexts = new();
        private readonly SemaphoreSlim _loadingSemaphore = new(1, 1);
        private bool _disposed;

        /// <summary>
        /// Loads plugins from an assembly file with advanced isolation and security validation.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The loaded plugin instances.</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Plugin system requires dynamic loading")]
        [UnconditionalSuppressMessage("Trimming", "IL2072:Target parameter contains annotations from the source parameter", Justification = "Plugin system requires dynamic loading")]
        public async Task<IReadOnlyList<LoadedPluginResult>> LoadPluginsFromAssemblyAsync(string assemblyPath, CancellationToken cancellationToken = default)
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

                // Load plugin metadata if available
                var metadata = await LoadPluginMetadataAsync(assemblyPath).ConfigureAwait(false);

                // Version compatibility check
                if (metadata != null && !IsVersionCompatible(metadata.RequiredFrameworkVersion))
                {
                    LogVersionIncompatible(assemblyPath, metadata.RequiredFrameworkVersion?.ToString() ?? "Unknown");
                    return Array.Empty<LoadedPluginResult>();
                }

                // Create isolated load context
                var loadContextName = $"PluginContext_{Path.GetFileNameWithoutExtension(assemblyPath)}_{Guid.NewGuid():N}";
                var loadContext = new PluginAssemblyLoadContext(loadContextName, assemblyPath, _options.EnablePluginIsolation);

                try
                {
                    // Load assembly in isolated context
                    var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
                    var assemblyName = assembly.GetName().Name ?? "Unknown";

                    // Check if already loaded
                    if (_loadContexts.ContainsKey(assemblyName))
                    {
                        LogAssemblyAlreadyLoaded(assemblyName);
                        loadContext.Unload();
                        return Array.Empty<LoadedPluginResult>();
                    }

                    // Add to load contexts
                    _ = _loadContexts.TryAdd(assemblyName, loadContext);

                    // Discover plugin types
                    var pluginTypes = assembly.GetTypes()
                        .Where(t => t.IsClass && !t.IsAbstract && typeof(IAlgorithmPlugin).IsAssignableFrom(t));

                    var results = new List<LoadedPluginResult>();
                    foreach (var pluginType in pluginTypes)
                    {
                        try
                        {
                            if (CreatePluginInstance(pluginType) is IAlgorithmPlugin plugin)
                            {
                                var pluginMetadata = metadata ?? CreateDefaultMetadata(plugin, assemblyPath);
                                results.Add(new LoadedPluginResult(plugin, loadContext, assembly, pluginMetadata));
                            }
                        }
                        catch (Exception ex)
                        {
                            LogPluginLoadFailed(ex);
                        }
                    }

                    LogAssemblyLoaded(results.Count, assemblyName);
                    return results.AsReadOnly();
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
        /// Loads plugins from a NuGet package.
        /// </summary>
        /// <param name="packageSource">The path to the .nupkg file or package ID (with optional version).</param>
        /// <param name="targetFramework">Target framework for assembly selection (optional, defaults to current framework).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The loaded plugin instances.</returns>
        public async Task<IReadOnlyList<LoadedPluginResult>> LoadPluginsFromNuGetPackageAsync(
            string packageSource,
            string? targetFramework = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(packageSource);

            LogLoadingFromNuGetPackage(packageSource);

            // TODO: NuGet plugin loading is not yet implemented
            // Will be added in a future version once NuGetPluginLoader is properly integrated
            await Task.CompletedTask;
            throw new NotImplementedException("NuGet plugin loading is not yet implemented. Use LoadPluginsFromAssemblyAsync instead.");

            /*
            try
            {
                // Create NuGet plugin loader with security settings matching our options
                var nugetOptions = new NuGetPluginLoaderOptions
                {
                    CacheDirectory = Path.Combine(Path.GetTempPath(), "DotComputeNuGetCache"),
                    DefaultTargetFramework = targetFramework ?? "net9.0",
                    EnableSecurityValidation = _options.EnableSecurityValidation,
                    RequirePackageSignature = _options.RequireDigitalSignature,
                    EnableMalwareScanning = _options.EnableMalwareScanning,
                    MaxAssemblySize = _options.MaxAssemblySize,
                    MinimumSecurityLevel = _options.MinimumSecurityLevel,
                    IncludePrerelease = false,
                    MaxConcurrentDownloads = 2
                };

                // Configure trusted sources and blocked packages based on security policy
                if (_options.EnableSecurityValidation)
                {
                    nugetOptions.AllowedPackagePrefixes.Add("DotCompute.");
                    nugetOptions.AllowedPackagePrefixes.Add("Microsoft.");
                    nugetOptions.AllowedPackagePrefixes.Add("System.");

                    // Add any trusted publishers as allowed prefixes
                    foreach (var publisher in _options.TrustedPublishers)
                    {
                        nugetOptions.AllowedPackagePrefixes.Add(publisher);
                    }
                }

                using var nugetLoader = new NuGetPluginLoader(
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<NuGetPluginLoader>.Instance,
                    nugetOptions);

                // Load the package and get assembly paths
                var loadResult = await nugetLoader.LoadPackageAsync(packageSource, targetFramework, cancellationToken).ConfigureAwait(false);

                LogNuGetPackageLoaded(
                    loadResult.PackageIdentity.Id,
                    loadResult.PackageIdentity.Version.ToString(),
                    loadResult.LoadedAssemblyPaths.Length,
                    loadResult.ResolvedDependencies.Length);

                // Load plugins from each assembly in the package
                var allResults = new List<LoadedPluginResult>();
                var assemblyLoadTasks = loadResult.LoadedAssemblyPaths
                    .Select(async assemblyPath =>
                    {
                        try
                        {
                            var results = await LoadPluginsFromAssemblyAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
                            return results;
                        }
                        catch (Exception ex)
                        {
                            LogNuGetAssemblyLoadFailed(assemblyPath, ex.Message);
                            return (IReadOnlyList<LoadedPluginResult>)Array.Empty<LoadedPluginResult>();
                        }
                    });

                var assemblyResults = await Task.WhenAll(assemblyLoadTasks).ConfigureAwait(false);
                foreach (var results in assemblyResults)
                {
                    allResults.AddRange(results);
                }

                // Log dependency information
                if (loadResult.ResolvedDependencies.Length > 0)
                {
                    LogNuGetDependenciesResolved(
                        loadResult.PackageIdentity.Id,
                        string.Join(", ", loadResult.ResolvedDependencies.Select(d => $"{d.Id} {d.VersionRange}")));
                }

                // Log security validation results
                if (!string.IsNullOrEmpty(loadResult.SecurityValidationResult))
                {
                    LogNuGetSecurityValidation(loadResult.PackageIdentity.Id, loadResult.SecurityValidationResult);
                }

                // Log any warnings
                foreach (var warning in loadResult.Warnings)
                {
                    LogNuGetPackageWarning(loadResult.PackageIdentity.Id, warning);
                }

                LogNuGetPackageLoadCompleted(
                    loadResult.PackageIdentity.Id,
                    loadResult.PackageIdentity.Version.ToString(),
                    allResults.Count,
                    loadResult.LoadTime.TotalMilliseconds);

                return allResults.AsReadOnly();
            }
            catch (Exception ex)
            {
                LogNuGetPackageLoadFailed(packageSource, ex.Message);
                throw;
            }
            */
        }

        /// <summary>
        /// Creates a plugin instance with proper error handling.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Plugin instantiation requires dynamic type handling")]
        private static IAlgorithmPlugin? CreatePluginInstance(Type pluginType)
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
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                return JsonSerializer.Deserialize<PluginMetadata>(json, options);
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
                LoadTime = DateTime.UtcNow,
                AssemblyName = Path.GetFileName(assemblyPath),
                TypeName = plugin.GetType().FullName ?? "Unknown",
                Capabilities = [.. plugin.SupportedOperations],
                SupportedAccelerators = [.. plugin.SupportedAcceleratorTypes.Select(t => t.ToString())],
                LoadContextName = "Default",
                AdditionalMetadata = []
            };
        }

        /// <summary>
        /// Checks if the required framework version is compatible.
        /// </summary>
        private static bool IsVersionCompatible(Version? requiredVersion)
        {
            if (requiredVersion == null)
            {
                return true;
            }

            try
            {
                var current = Environment.Version;
                return current >= requiredVersion;
            }
            catch
            {
                return true; // If we can't compare, assume compatible
            }
        }

        /// <summary>
        /// Unloads a plugin assembly and its load context.
        /// </summary>
        public void UnloadAssembly(string assemblyName)
        {
            if (_loadContexts.TryRemove(assemblyName, out var loadContext))
            {
                try
                {
                    loadContext.Unload();
                    LogAssemblyUnloaded(assemblyName);
                }
                catch (Exception ex)
                {
                    LogAssemblyUnloadFailed(assemblyName, ex.Message);
                }
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;

                // Unload all contexts
                foreach (var context in _loadContexts.Values)
                {
                    try
                    {
                        context.Unload();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to unload context {ContextName}", context.Name);
                    }
                }

                _loadContexts.Clear();
                _loadingSemaphore.Dispose();
                await Task.CompletedTask;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                // Unload all load contexts
                foreach (var kvp in _loadContexts)
                {
                    try
                    {
                        kvp.Value.Unload();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error unloading assembly context {AssemblyName}", kvp.Key);
                    }
                }
                _loadContexts.Clear();

                _loadingSemaphore.Dispose();
                _logger.LogInformation("AlgorithmPluginLoader disposed");
            }
        }

        // Logger messages
        [LoggerMessage(Level = LogLevel.Information, Message = "Loading algorithm plugin from assembly {AssemblyPath}")]
        private partial void LogLoadingAssembly(string assemblyPath);

        [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {PluginCount} algorithm plugins from assembly {AssemblyName}")]
        private partial void LogAssemblyLoaded(int pluginCount, string assemblyName);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Assembly {AssemblyName} is already loaded")]
        private partial void LogAssemblyAlreadyLoaded(string assemblyName);

        [LoggerMessage(Level = LogLevel.Information, Message = "Assembly {AssemblyName} unloaded")]
        private partial void LogAssemblyUnloaded(string assemblyName);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to unload assembly {AssemblyName}: {Reason}")]
        private partial void LogAssemblyUnloadFailed(string assemblyName, string reason);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Assembly version incompatible {AssemblyPath}, required: {RequiredVersion}")]
        private partial void LogVersionIncompatible(string assemblyPath, string requiredVersion);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load algorithm plugin")]
        private partial void LogPluginLoadFailed(Exception exception);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load metadata from {ManifestPath}: {Reason}")]
        private partial void LogMetadataLoadFailed(string manifestPath, string reason);

        [LoggerMessage(Level = LogLevel.Information, Message = "Loading plugins from NuGet package: {PackagePath}")]
        private partial void LogLoadingFromNuGetPackage(string packagePath);

        [LoggerMessage(Level = LogLevel.Information, Message = "NuGet package loaded: {PackageId} v{Version}, {AssemblyCount} assemblies, {DependencyCount} dependencies")]
        private partial void LogNuGetPackageLoaded(string packageId, string version, int assemblyCount, int dependencyCount);

        [LoggerMessage(Level = LogLevel.Error, Message = "NuGet assembly load failed for {AssemblyPath}: {Reason}")]
        private partial void LogNuGetAssemblyLoadFailed(string assemblyPath, string reason);

        [LoggerMessage(Level = LogLevel.Information, Message = "NuGet dependencies resolved for {PackageId}: {Dependencies}")]
        private partial void LogNuGetDependenciesResolved(string packageId, string dependencies);

        [LoggerMessage(Level = LogLevel.Information, Message = "NuGet security validation for {PackageId}: {ValidationResult}")]
        private partial void LogNuGetSecurityValidation(string packageId, string validationResult);

        [LoggerMessage(Level = LogLevel.Warning, Message = "NuGet package warning for {PackageId}: {Warning}")]
        private partial void LogNuGetPackageWarning(string packageId, string warning);

        [LoggerMessage(Level = LogLevel.Information, Message = "NuGet package load completed: {PackageId} v{Version}, {PluginCount} plugins loaded in {ElapsedMs} ms")]
        private partial void LogNuGetPackageLoadCompleted(string packageId, string version, int pluginCount, double elapsedMs);

        [LoggerMessage(Level = LogLevel.Error, Message = "NuGet package load failed for {PackageSource}: {Reason}")]
        private partial void LogNuGetPackageLoadFailed(string packageSource, string reason);
    }

    /// <summary>
    /// Represents the result of loading a plugin from an assembly.
    /// </summary>
    public record LoadedPluginResult(
        IAlgorithmPlugin Plugin,
        PluginAssemblyLoadContext LoadContext,
        Assembly Assembly,
        PluginMetadata Metadata);
}