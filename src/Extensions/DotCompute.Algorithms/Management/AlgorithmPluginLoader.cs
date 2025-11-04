
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security;
using System.Text.Json;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Management.Loading;
using DotCompute.Algorithms.Management.Metadata;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management
{
    /// <summary>
    /// Handles the loading of algorithm plugins from assemblies and NuGet packages.
    /// </summary>
    public sealed partial class AlgorithmPluginLoader : IAsyncDisposable, IDisposable
    {
        private readonly ILogger<AlgorithmPluginLoader> _logger;
        private readonly AlgorithmPluginManagerOptions _options;
        private readonly ConcurrentDictionary<string, PluginAssemblyLoadContext> _loadContexts = new();
        private readonly SemaphoreSlim _loadingSemaphore = new(1, 1);
        private readonly Security.PluginSecurityValidator? _securityValidator;
        private bool _disposed;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="AlgorithmPluginLoader"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="options">Plugin manager options.</param>
        public AlgorithmPluginLoader(ILogger<AlgorithmPluginLoader> logger, AlgorithmPluginManagerOptions options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            // Initialize security validator if security validation is enabled
            if (_options.EnableSecurityValidation)
            {
                var securityLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<Security.PluginSecurityValidator>.Instance;
                _securityValidator = new Security.PluginSecurityValidator(securityLogger, _options);
            }
        }

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

                // SECURITY: Comprehensive security validation before loading
                if (_securityValidator != null && _options.EnableSecurityValidation)
                {
                    var securityResult = await _securityValidator.ValidatePluginSecurityAsync(assemblyPath, cancellationToken).ConfigureAwait(false);

                    if (!securityResult.IsValid)
                    {
                        var violations = string.Join("; ", securityResult.Violations);
                        LogSecurityValidationFailed(assemblyPath, securityResult.ThreatLevel, violations);

                        // CRITICAL: Do not load plugins that fail security validation
                        throw new SecurityException(
                            $"Plugin security validation failed for '{assemblyPath}' (Threat: {securityResult.ThreatLevel}): {violations}");
                    }

                    // Log warnings even if validation passed
                    if (securityResult.Warnings.Count > 0)
                    {
                        var warnings = string.Join("; ", securityResult.Warnings);
                        LogSecurityValidationWarnings(assemblyPath, warnings);
                    }

                    LogSecurityValidationPassed(assemblyPath, securityResult.ThreatLevel, securityResult.ValidationDuration.TotalMilliseconds);
                }

                // Load plugin metadata if available
                var metadata = await LoadPluginMetadataAsync(assemblyPath).ConfigureAwait(false);

                // Version compatibility check
                if (metadata != null && !IsVersionCompatible(metadata.RequiredFrameworkVersion))
                {
                    LogVersionIncompatible(assemblyPath, metadata.RequiredFrameworkVersion?.ToString() ?? "Unknown");
                    return [];
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
                        return [];
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
                    loadResult.ResolvedDependencies.Count);

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
                if (loadResult.ResolvedDependencies.Count > 0)
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
            Justification = "JSON deserialization for plugin metadata only, types are preserved")]
        [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCodeAttribute",
            Justification = "JSON deserialization for plugin metadata only")]
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
                return JsonSerializer.Deserialize<PluginMetadata>(json, JsonOptions);
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
                LoadContextName = "Default"
                // AdditionalMetadata is initialized by property initializer
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
                        LogContextUnloadFailed(ex, context.Name ?? "Unknown");
                    }
                }

                _loadContexts.Clear();
                _loadingSemaphore.Dispose();

                // Dispose security validator
                if (_securityValidator != null)
                {
                    await _securityValidator.DisposeAsync().ConfigureAwait(false);
                }

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
                        LogAssemblyContextUnloadError(ex, kvp.Key);
                    }
                }
                _loadContexts.Clear();

                _loadingSemaphore.Dispose();

                // Dispose security validator
                _securityValidator?.Dispose();

                LogPluginLoaderDisposed();
            }
        }

        #region LoggerMessage Delegates
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

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to unload context {ContextName}")]
        private partial void LogContextUnloadFailed(Exception ex, string contextName);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Error unloading assembly context {AssemblyName}")]
        private partial void LogAssemblyContextUnloadError(Exception ex, string assemblyName);

        [LoggerMessage(Level = LogLevel.Information, Message = "AlgorithmPluginLoader disposed")]
        private partial void LogPluginLoaderDisposed();

        [LoggerMessage(Level = LogLevel.Error, Message = "SECURITY: Plugin security validation FAILED for {AssemblyPath}, Threat: {ThreatLevel}, Violations: {Violations}")]
        private partial void LogSecurityValidationFailed(string assemblyPath, DotCompute.Abstractions.Security.ThreatLevel threatLevel, string violations);

        [LoggerMessage(Level = LogLevel.Warning, Message = "SECURITY: Plugin security validation warnings for {AssemblyPath}: {Warnings}")]
        private partial void LogSecurityValidationWarnings(string assemblyPath, string warnings);

        [LoggerMessage(Level = LogLevel.Information, Message = "SECURITY: Plugin security validation PASSED for {AssemblyPath}, Threat: {ThreatLevel}, Duration: {DurationMs:F2}ms")]
        private partial void LogSecurityValidationPassed(string assemblyPath, DotCompute.Abstractions.Security.ThreatLevel threatLevel, double durationMs);

        #endregion
    }
    /// <summary>
    /// A class that represents loaded plugin result.
    /// </summary>

    /// <summary>
    /// Represents the result of loading a plugin from an assembly.
    /// </summary>
    public record LoadedPluginResult(
        IAlgorithmPlugin Plugin,
        PluginAssemblyLoadContext LoadContext,
        Assembly Assembly,
        PluginMetadata Metadata);
}
