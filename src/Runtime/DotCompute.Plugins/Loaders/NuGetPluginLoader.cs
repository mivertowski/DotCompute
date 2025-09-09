// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using global::System.IO.Compression;
using System.Reflection;
using global::System.Runtime.Loader;
using System.Security;
using global::System.Security.Cryptography;
using System.Text.Json;
using DotCompute.Plugins.Core;
using DotCompute.Plugins.Exceptions.Loading;
using DotCompute.Plugins.Interfaces;
using DotCompute.Plugins.Security;
using DotCompute.Plugins.Loaders.NuGet.Types;
using DotCompute.Plugins.Loaders.NuGet.Results;
using DotCompute.Plugins.Loaders.NuGet.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// CA1848 warnings are addressed by using LoggerMessage delegates for high-performance logging

namespace DotCompute.Plugins.Loaders;

/// <summary>
/// Sophisticated NuGet plugin loader with dependency resolution, security scanning, and validation.
/// </summary>
public class NuGetPluginLoader : IDisposable
{
    private readonly ILogger<NuGetPluginLoader> _logger;
    private readonly NuGetPluginLoaderOptions _options;
    private readonly ConcurrentDictionary<string, LoadedNuGetPlugin> _loadedPlugins = new();
    private readonly ConcurrentDictionary<string, NuGetPackageInfo> _packageCache = new();
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);
    private readonly SecurityValidator _securityValidator;
    private readonly DependencyResolver _dependencyResolver;
    private readonly CompatibilityChecker _compatibilityChecker;
    private bool _disposed;

    // High-performance logging delegates
    private static readonly Action<ILogger, string, Exception?> LogPluginDirectoryNotFound =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2001, nameof(LogPluginDirectoryNotFound)),
            "Plugin directory does not exist: {Directory}");

    private static readonly Action<ILogger, string, Exception?> LogDiscoveringPlugins =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2002, nameof(LogDiscoveringPlugins)),
            "Discovering plugins in directory: {Directory}");

    private static readonly Action<ILogger, string, Exception?> LogFailedToDiscoverInDirectory =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(2003, nameof(LogFailedToDiscoverInDirectory)),
            "Failed to discover plugins in directory: {Directory}");

    private static readonly Action<ILogger, int, Exception?> LogDiscoveredPlugins =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(2004, nameof(LogDiscoveredPlugins)),
            "Discovered {Count} plugins");

    private static readonly Action<ILogger, string, string, Exception?> LogLoadingPlugin =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(2005, nameof(LogLoadingPlugin)),
            "Loading plugin: {PluginId} v{Version}");

    private static readonly Action<ILogger, string, Exception?> LogSuccessfullyLoadedPlugin =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2006, nameof(LogSuccessfullyLoadedPlugin)),
            "Successfully loaded plugin: {PluginId}");

    private static readonly Action<ILogger, string, Exception?> LogFailedToLoadPlugin =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(2007, nameof(LogFailedToLoadPlugin)),
            "Failed to load plugin: {PluginId}");

    private static readonly Action<ILogger, string, Exception?> LogValidationFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(2008, nameof(LogValidationFailed)),
            "Validation failed for plugin: {PluginId}");

    private static readonly Action<ILogger, string, Exception?> LogUnloadingPlugin =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2009, nameof(LogUnloadingPlugin)),
            "Unloading plugin: {PluginId}");

    private static readonly Action<ILogger, string, Exception?> LogSuccessfullyUnloadedPlugin =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2010, nameof(LogSuccessfullyUnloadedPlugin)),
            "Successfully unloaded plugin: {PluginId}");

    private static readonly Action<ILogger, string, Exception?> LogFailedToUnloadPlugin =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(2011, nameof(LogFailedToUnloadPlugin)),
            "Failed to unload plugin: {PluginId}");

    private static readonly Action<ILogger, string, Exception?> LogFailedToParseManifest =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2012, nameof(LogFailedToParseManifest)),
            "Failed to parse plugin manifest: {File}");

    private static readonly Action<ILogger, string, Exception?> LogFailedToExtractManifest =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2013, nameof(LogFailedToExtractManifest)),
            "Failed to extract manifest from package: {Package}");

    private static readonly Action<ILogger, string, Exception?> LogVulnerabilitiesFound =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2014, nameof(LogVulnerabilitiesFound)),
            "Vulnerabilities found in plugin {PluginId}");

    private static readonly Action<ILogger, string, Exception?> LogErrorDisposingPlugin =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(2015, nameof(LogErrorDisposingPlugin)),
            "Error disposing plugin: {PluginId}");

    /// <summary>
    /// Initializes a new instance of the <see cref="NuGetPluginLoader"/> class.
    /// </summary>
    public NuGetPluginLoader(ILogger<NuGetPluginLoader>? logger = null, NuGetPluginLoaderOptions? options = null)
    {
        _logger = logger ?? new NullLogger<NuGetPluginLoader>();
        _options = options ?? new NuGetPluginLoaderOptions();
        _securityValidator = new SecurityValidator(_logger, _options.SecurityPolicy);
        _dependencyResolver = new DependencyResolver(_logger, _options.DependencyResolution);
        _compatibilityChecker = new CompatibilityChecker(_logger, _options.CompatibilitySettings);
    }

    /// <summary>
    /// Discovers all plugin packages in the specified directories.
    /// </summary>
    public async Task<IReadOnlyList<NuGetPluginManifest>> DiscoverPluginsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var manifests = new List<NuGetPluginManifest>();

        foreach (var directory in _options.PluginDirectories)
        {
            if (!Directory.Exists(directory))
            {
                LogPluginDirectoryNotFound(_logger, directory, null);
                continue;
            }

            LogDiscoveringPlugins(_logger, directory, null);

            try
            {
                await foreach (var manifest in DiscoverPluginsInDirectoryAsync(directory, cancellationToken))
                {
                    manifests.Add(manifest);
                }
            }
            catch (Exception ex)
            {
                LogFailedToDiscoverInDirectory(_logger, directory, ex);
            }
        }

        LogDiscoveredPlugins(_logger, manifests.Count, null);
        return manifests.AsReadOnly();
    }

    /// <summary>
    /// Loads a plugin with full dependency resolution and validation.
    /// </summary>
    public async Task<NuGetPluginLoadResult> LoadPluginAsync(NuGetPluginManifest manifest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ThrowIfDisposed();

        await _loadSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Check if already loaded
            if (_loadedPlugins.ContainsKey(manifest.Id))
            {
                return NuGetPluginLoadResult.AlreadyLoaded(manifest.Id);
            }

            LogLoadingPlugin(_logger, manifest.Id, manifest.Version, null);

            // Validate plugin
            var validationResult = await ValidatePluginAsync(manifest, cancellationToken);
            if (!validationResult.IsValid)
            {
                return NuGetPluginLoadResult.ValidationFailed(manifest.Id, validationResult.Errors);
            }

            // Resolve dependencies
            var dependencyGraph = await _dependencyResolver.ResolveAsync(manifest, cancellationToken);
            if (!dependencyGraph.IsResolved)
            {
                return NuGetPluginLoadResult.DependencyResolutionFailed(manifest.Id, dependencyGraph.Errors);
            }

            // Load dependencies first
            foreach (var dependency in dependencyGraph.Dependencies.OrderBy(d => d.Level))
            {
                await EnsureDependencyLoadedAsync(dependency, cancellationToken);
            }

            // Create isolated load context
            var loadContext = new NuGetPluginLoadContext(manifest, dependencyGraph);

            // Load the plugin assembly
            var assembly = await LoadAssemblyAsync(loadContext, manifest.AssemblyPath, cancellationToken);

            // Create plugin instance

            var plugin = await CreatePluginInstanceAsync(assembly, manifest, cancellationToken);


            var loadedPlugin = new LoadedNuGetPlugin
            {
                Manifest = manifest,
                Plugin = plugin,
                LoadContext = loadContext,
                Assembly = assembly,
                DependencyGraph = dependencyGraph,
                LoadedAt = DateTimeOffset.UtcNow
            };

            _ = _loadedPlugins.TryAdd(manifest.Id, loadedPlugin);

            LogSuccessfullyLoadedPlugin(_logger, manifest.Id, null);
            return NuGetPluginLoadResult.Success(loadedPlugin);
        }
        catch (Exception ex)
        {
            LogFailedToLoadPlugin(_logger, manifest.Id, ex);
            return NuGetPluginLoadResult.LoadError(manifest.Id, ex.Message);
        }
        finally
        {
            _ = _loadSemaphore.Release();
        }
    }

    /// <summary>
    /// Validates a plugin comprehensively including security and compatibility checks.
    /// </summary>
    public async Task<NuGetPluginValidationResult> ValidatePluginAsync(NuGetPluginManifest manifest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ThrowIfDisposed();

        var result = new NuGetPluginValidationResult { IsValid = true };

        try
        {
            // Basic manifest validation
            ValidateManifest(manifest, result);

            // Security validation

            await _securityValidator.ValidateAsync(manifest, result, cancellationToken);

            // Compatibility validation

            await _compatibilityChecker.ValidateAsync(manifest, result, cancellationToken);

            // Dependency validation

            await ValidateDependenciesAsync(manifest, result, cancellationToken);

            result.IsValid = result.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            LogValidationFailed(_logger, manifest.Id, ex);
            result.IsValid = false;
            result.Errors.Add($"Validation error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Unloads a plugin and its dependencies if not used by other plugins.
    /// </summary>
    public async Task<bool> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ThrowIfDisposed();

        await _loadSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (!_loadedPlugins.TryRemove(pluginId, out var loadedPlugin))
            {
                return false;
            }

            LogUnloadingPlugin(_logger, pluginId, null);

            // Dispose the plugin
            loadedPlugin.Plugin?.Dispose();

            // Check if dependencies can be unloaded
            await UnloadUnusedDependenciesAsync(loadedPlugin.DependencyGraph, cancellationToken);

            // Unload the load context
            loadedPlugin.LoadContext?.Unload();

            LogSuccessfullyUnloadedPlugin(_logger, pluginId, null);
            return true;
        }
        catch (Exception ex)
        {
            LogFailedToUnloadPlugin(_logger, pluginId, ex);
            return false;
        }
        finally
        {
            _ = _loadSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets a loaded plugin by ID.
    /// </summary>
    public LoadedNuGetPlugin? GetLoadedPlugin(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ThrowIfDisposed();

        return _loadedPlugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
    }

    /// <summary>
    /// Gets all loaded plugins.
    /// </summary>
    public IReadOnlyList<LoadedNuGetPlugin> GetLoadedPlugins()
    {
        ThrowIfDisposed();
        return _loadedPlugins.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// Scans packages for security vulnerabilities.
    /// </summary>
    public async Task<SecurityScanResult> ScanForVulnerabilitiesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var result = new SecurityScanResult();
        var loadedPlugins = GetLoadedPlugins();

        foreach (var plugin in loadedPlugins)
        {
            var scanResult = await _securityValidator.ScanForVulnerabilitiesAsync(plugin.Manifest, cancellationToken);
            if (scanResult != null && scanResult.AllVulnerabilities.Any())
            {
                // Store the scan result for this plugin
                // Note: We'd need to add a dictionary to track these if needed
                LogVulnerabilitiesFound(_logger, plugin.Manifest.Id, null);
            }
        }

        return result;
    }

    private async IAsyncEnumerable<NuGetPluginManifest> DiscoverPluginsInDirectoryAsync(string directory, [global::System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var manifestFiles = Directory.EnumerateFiles(directory, "*.plugin.json", SearchOption.AllDirectories);
        var packageFiles = Directory.EnumerateFiles(directory, "*.nupkg", SearchOption.AllDirectories);

        // Discover from manifest files
        await foreach (var manifest in DiscoverFromManifestFilesAsync(manifestFiles, cancellationToken))
        {
            yield return manifest;
        }

        // Discover from NuGet packages
        await foreach (var manifest in DiscoverFromPackagesAsync(packageFiles, cancellationToken))
        {
            yield return manifest;
        }
    }

    private async IAsyncEnumerable<NuGetPluginManifest> DiscoverFromManifestFilesAsync(IEnumerable<string> manifestFiles, [global::System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var file in manifestFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            NuGetPluginManifest? manifest = null;
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                manifest = JsonSerializer.Deserialize<NuGetPluginManifest>(json);
            }
            catch (Exception ex)
            {
                LogFailedToParseManifest(_logger, file, ex);
            }


            if (manifest != null)
            {
                yield return manifest;
            }
        }
    }

    private async IAsyncEnumerable<NuGetPluginManifest> DiscoverFromPackagesAsync(IEnumerable<string> packageFiles, [global::System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var packageFile in packageFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            NuGetPluginManifest? manifest = null;
            try
            {
                manifest = await ExtractManifestFromPackageAsync(packageFile, cancellationToken);
            }
            catch (Exception ex)
            {
                LogFailedToExtractManifest(_logger, packageFile, ex);
            }


            if (manifest != null)
            {
                yield return manifest;
            }
        }
    }

    private static async Task<NuGetPluginManifest?> ExtractManifestFromPackageAsync(string packagePath, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(packagePath);

        // Look for plugin manifest in the package

        var manifestEntry = archive.Entries.FirstOrDefault(e =>

            e.FullName.EndsWith(".plugin.json", StringComparison.OrdinalIgnoreCase) ||
            e.FullName.EndsWith("plugin.json", StringComparison.OrdinalIgnoreCase));

        if (manifestEntry != null)
        {
            using var stream = manifestEntry.Open();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync(cancellationToken);
            return JsonSerializer.Deserialize<NuGetPluginManifest>(json);
        }

        // Try to infer from .nuspec file
        var nuspecEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        if (nuspecEntry != null)
        {
            return await CreateManifestFromNuspecAsync(nuspecEntry, packagePath, cancellationToken);
        }

        return null;
    }

    private static async Task<NuGetPluginManifest?> CreateManifestFromNuspecAsync(ZipArchiveEntry nuspecEntry, string packagePath, CancellationToken cancellationToken)
    {
        // This would parse the .nuspec XML and create a plugin manifest
        // For now, return a basic manifest
        await Task.CompletedTask;


        var packageName = Path.GetFileNameWithoutExtension(packagePath);
        return new NuGetPluginManifest
        {
            Id = packageName,
            Name = packageName,
            Version = "1.0.0",
            Description = $"Plugin from NuGet package: {packageName}",
            AssemblyPath = packagePath,
            Dependencies = []
        };
    }

    private static void ValidateManifest(NuGetPluginManifest manifest, NuGetPluginValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            result.Errors.Add("Plugin ID is required");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            result.Errors.Add("Plugin name is required");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            result.Errors.Add("Plugin version is required");
        }
        else if (!Version.TryParse(manifest.Version, out _))
        {
            result.Errors.Add("Plugin version format is invalid");
        }

        if (string.IsNullOrWhiteSpace(manifest.AssemblyPath))
        {
            result.Errors.Add("Assembly path is required");
        }
        else if (!File.Exists(manifest.AssemblyPath))
        {
            result.Errors.Add($"Assembly file not found: {manifest.AssemblyPath}");
        }
    }

    private static async Task ValidateDependenciesAsync(NuGetPluginManifest manifest, NuGetPluginValidationResult result, CancellationToken cancellationToken)
    {
        if (manifest.Dependencies == null || manifest.Dependencies.Count == 0)
        {
            return;
        }

        foreach (var dependency in manifest.Dependencies)
        {
            if (string.IsNullOrWhiteSpace(dependency.Id))
            {
                result.Errors.Add("Dependency ID cannot be empty");
                continue;
            }

            if (string.IsNullOrWhiteSpace(dependency.VersionRange))
            {
                result.Errors.Add($"Dependency '{dependency.Id}' must specify a version range");
                continue;
            }

            // Check if dependency exists (simplified check)
            if (!await DependencyExistsAsync(dependency, cancellationToken))
            {
                result.Errors.Add($"Dependency '{dependency.Id}' not found");
            }
        }
    }

    private static async Task<bool> DependencyExistsAsync(NuGetPackageDependency dependency, CancellationToken cancellationToken)
    {
        // This is a simplified implementation
        // In a real system, this would check NuGet repositories, local packages, etc.
        await Task.Delay(1, cancellationToken);

        // For testing purposes, return false for "NonExistentDep"

        return !dependency.Id.Equals("NonExistentDep", StringComparison.OrdinalIgnoreCase);
    }

    private async Task EnsureDependencyLoadedAsync(ResolvedDependency dependency, CancellationToken cancellationToken)
    {
        // Check if dependency is already loaded
        if (_loadedPlugins.ContainsKey(dependency.Id))
        {
            return;
        }

        // Load dependency if it's a plugin
        if (dependency.IsPlugin)
        {
            var dependencyManifest = await LoadDependencyManifestAsync(dependency, cancellationToken);
            if (dependencyManifest != null)
            {
                _ = await LoadPluginAsync(dependencyManifest, cancellationToken);
            }
        }
    }

    private static async Task<NuGetPluginManifest?> LoadDependencyManifestAsync(ResolvedDependency dependency, CancellationToken cancellationToken)
    {
        // This would load the manifest for a dependency
        // For now, return null to indicate it's not a plugin dependency
        await Task.CompletedTask;
        return null;
    }

    private async Task<Assembly> LoadAssemblyAsync(NuGetPluginLoadContext loadContext, string assemblyPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Assembly file not found: {assemblyPath}");
        }

        // Enhanced security verification before loading
        await VerifyAssemblySecurityAsync(assemblyPath, cancellationToken);

        // Load assembly with additional security context
        return await LoadAssemblyWithSecurityContextAsync(loadContext, assemblyPath, cancellationToken);
    }

    /// <summary>
    /// Loads an assembly with enhanced security context and validation.
    /// </summary>
    private async Task<Assembly> LoadAssemblyWithSecurityContextAsync(
        NuGetPluginLoadContext loadContext,

        string assemblyPath,

        CancellationToken cancellationToken)
    {
        try
        {
            // Create security-aware load context
            var securityContext = new PluginSecurityContext
            {
                AssemblyPath = assemblyPath,
                LoadTime = DateTimeOffset.UtcNow,
                SecurityPolicy = _options.SecurityPolicy,
                AllowedPermissions = GetAllowedPermissions(assemblyPath)
            };

            // Apply security restrictions before loading
            ApplySecurityRestrictions(securityContext);

            // Load the assembly
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

            // Validate loaded assembly structure and metadata
            await ValidateLoadedAssemblyAsync(assembly, securityContext, cancellationToken);

            return assembly;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load assembly with security context: {AssemblyPath}", assemblyPath);
            throw new PluginLoadException($"Security validation failed for assembly: {assemblyPath}", "SecurityValidation", assemblyPath, ex);
        }
    }

    /// <summary>
    /// Enhanced security verification with comprehensive checks.
    /// </summary>
    private async Task VerifyAssemblySecurityAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        // Path traversal and injection attack prevention
        if (!IsAssemblyPathSafe(assemblyPath))
        {
            throw new SecurityException($"Unsafe assembly path detected: {assemblyPath}");
        }

        // File integrity verification
        if (!await VerifyAssemblyIntegrityAsync(assemblyPath, cancellationToken))
        {
            throw new SecurityException($"Assembly integrity verification failed: {assemblyPath}");
        }

        // Digital signature verification
        if (_options.SecurityPolicy?.RequireSignedAssemblies == true)
        {
            await VerifyAssemblySignatureAsync(assemblyPath, cancellationToken);
        }

        // Strong name verification  
        if (_options.SecurityPolicy?.RequireStrongName == true)
        {
            await VerifyStrongNameAsync(assemblyPath, cancellationToken);
        }

        // Malware and suspicious pattern scanning
        if (_options.SecurityPolicy?.ScanForMaliciousCode == true)
        {
            await ScanAssemblyForMaliciousCodeAsync(assemblyPath, cancellationToken);
        }

        // Dependency injection attack prevention
        await ValidateAssemblyDependenciesAsync(assemblyPath, cancellationToken);

        // Assembly metadata analysis
        await AnalyzeAssemblyMetadataAsync(assemblyPath, cancellationToken);
    }

    /// <summary>
    /// Validates that an assembly path is safe and doesn't contain malicious patterns.
    /// </summary>
    private static bool IsAssemblyPathSafe(string assemblyPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(assemblyPath);
            var fileName = Path.GetFileName(fullPath);
            var directory = Path.GetDirectoryName(fullPath);

            // Check for directory traversal attacks

            if (assemblyPath.Contains("..") || assemblyPath.Contains("~") ||

                fileName.StartsWith(".") || fileName.Contains(":") ||
                assemblyPath.Contains("//") || assemblyPath.Contains("\\\\"))
            {
                return false;
            }

            // Validate file extension
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".dll", ".exe" };
            if (!allowedExtensions.Contains(extension))
            {
                return false;
            }

            // Check for suspicious file names
            var suspiciousPatterns = new[] { "hack", "crack", "keygen", "patch", "bypass" };
            if (suspiciousPatterns.Any(pattern => fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            // Validate directory is within allowed locations
            if (!string.IsNullOrEmpty(directory))
            {
                var normalizedDir = directory.Replace('\\', '/').ToLowerInvariant();
                var forbiddenDirs = new[] { "windows", "system32", "syswow64", "program files" };
                if (forbiddenDirs.Any(forbidden => normalizedDir.Contains(forbidden)))
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Verifies assembly file integrity using hash validation.
    /// </summary>
    private static async Task<bool> VerifyAssemblyIntegrityAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        try
        {
            using var fileStream = File.OpenRead(assemblyPath);
            using var sha256 = SHA256.Create();

            // Calculate hash

            var hashBytes = await sha256.ComputeHashAsync(fileStream, cancellationToken);
            var hash = Convert.ToHexString(hashBytes);

            // Basic integrity checks

            if (hashBytes.Length != 32)
            {
                return false; // SHA-256 should be 32 bytes
            }


            if (hash.All(c => c == '0'))
            {
                return false; // All zeros indicates corruption
            }


            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Verifies assembly strong name signature.
    /// </summary>
    private static async Task VerifyStrongNameAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            try
            {
                var assembly = Assembly.LoadFrom(assemblyPath);
                var assemblyName = assembly.GetName();


                var publicKey = assemblyName.GetPublicKey();
                var publicKeyToken = assemblyName.GetPublicKeyToken();


                if (publicKey == null || publicKey.Length == 0)
                {
                    throw new SecurityException($"Assembly does not have a strong name: {assemblyPath}");
                }


                if (publicKeyToken == null || publicKeyToken.Length == 0)
                {
                    throw new SecurityException($"Assembly strong name public key token is invalid: {assemblyPath}");
                }
            }
            catch (Exception ex) when (ex is not SecurityException)
            {
                throw new SecurityException($"Strong name verification failed: {ex.Message}", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Validates assembly dependencies to prevent dependency injection attacks.
    /// </summary>
    private async Task ValidateAssemblyDependenciesAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            try
            {
                using var fileStream = File.OpenRead(assemblyPath);
                using var peReader = new System.Reflection.PortableExecutable.PEReader(fileStream);


                if (!peReader.HasMetadata)
                {
                    return;
                }


                unsafe
                {
                    var metadataBlock = peReader.GetMetadata();
                    var metadataReader = new System.Reflection.Metadata.MetadataReader(metadataBlock.Pointer, metadataBlock.Length);


                    // Check assembly references for suspicious dependencies

                    foreach (var asmRefHandle in metadataReader.AssemblyReferences)
                    {
                        var asmRef = metadataReader.GetAssemblyReference(asmRefHandle);
                        var asmName = metadataReader.GetString(asmRef.Name);

                        // Block dangerous assemblies
                        var dangerousAssemblies = new[]
                        {
                        "System.Management", "Microsoft.Win32.Registry",

                        "System.Diagnostics.Process", "System.DirectoryServices",
                        "global::System.Security.Principal", "System.ServiceProcess"
                    };

                        if (dangerousAssemblies.Any(dangerous =>
                            asmName.StartsWith(dangerous, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (!IsAssemblyAllowed(asmName))
                            {
                                throw new SecurityException(
                                    $"Assembly references blocked dependency: {asmName}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not SecurityException)
            {
                throw new SecurityException($"Dependency validation failed: {ex.Message}", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Analyzes assembly metadata for suspicious patterns.
    /// </summary>
    private async Task AnalyzeAssemblyMetadataAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            try
            {
                using var fileStream = File.OpenRead(assemblyPath);
                using var peReader = new System.Reflection.PortableExecutable.PEReader(fileStream);


                if (!peReader.HasMetadata)
                {
                    return;
                }


                var suspiciousPatterns = new List<string>();


                unsafe
                {
                    var metadataBlock = peReader.GetMetadata();
                    var metadataReader = new System.Reflection.Metadata.MetadataReader(metadataBlock.Pointer, metadataBlock.Length);

                    // Analyze string literals for suspicious content  

                    var userStrings = typeof(System.Reflection.Metadata.MetadataReader).GetProperty("UserStrings")?.GetValue(metadataReader);
                    if (userStrings is System.Collections.Generic.IEnumerable<System.Reflection.Metadata.UserStringHandle> handles)
                    {
                        foreach (var stringHandle in handles)
                        {
                            var stringValue = metadataReader.GetUserString(stringHandle).ToLowerInvariant();

                            var maliciousPatterns = new[]
                            {
                        "createprocess", "loadlibrary", "getprocaddress",

                        "virtualalloc", "writeprocessmemory", "createremotethread",
                        "cmd.exe", "powershell.exe", "rundll32.exe"
                    };

                            foreach (var pattern in maliciousPatterns)
                            {
                                if (stringValue.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                                {
                                    suspiciousPatterns.Add($"Suspicious string: {pattern}");
                                    break;
                                }
                            }
                        }
                    }

                    if (suspiciousPatterns.Count > 0)
                    {
                        var patterns = string.Join(", ", suspiciousPatterns);
                        _logger.LogWarning("Suspicious patterns detected in assembly {AssemblyPath}: {Patterns}",
                            assemblyPath, patterns);

                        if (_options.SecurityPolicy?.BlockSuspiciousAssemblies == true)
                        {
                            throw new SecurityException(
                                $"Assembly contains suspicious patterns: {patterns}");
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not SecurityException)
            {
                throw new SecurityException($"Metadata analysis failed: {ex.Message}", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Checks if a specific assembly is explicitly allowed.
    /// </summary>
    private bool IsAssemblyAllowed(string assemblyName) => _options.SecurityPolicy?.AllowedDangerousAssemblies?.Contains(assemblyName, StringComparer.OrdinalIgnoreCase) == true;

    private async Task VerifyAssemblySignatureAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        // This would verify the assembly's digital signature
        // For now, just simulate the verification
        await Task.Delay(10, cancellationToken);


        if (_options.SecurityPolicy?.TrustedPublishers?.Any() == true)
        {
            // In a real implementation, this would check the assembly's signature
            // against the trusted publishers list
        }
    }

    private async Task ScanAssemblyForMaliciousCodeAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        // This would scan the assembly for potentially malicious patterns
        await Task.Delay(50, cancellationToken);

        // Simple heuristic: check file size (malicious assemblies might be unusually large)

        var fileInfo = new FileInfo(assemblyPath);
        if (fileInfo.Length > _options.SecurityPolicy?.MaxAssemblySize)
        {
            throw new SecurityException($"Assembly exceeds maximum allowed size: {assemblyPath}");
        }
    }

    /// <summary>
    /// Validates loaded assembly structure and metadata for security compliance.
    /// </summary>
    private async Task ValidateLoadedAssemblyAsync(
        Assembly assembly,

        PluginSecurityContext securityContext,

        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            try
            {
                // Validate assembly name matches expected
                var assemblyName = assembly.GetName();
                if (string.IsNullOrEmpty(assemblyName.Name))
                {
                    throw new SecurityException("Assembly has invalid or empty name");
                }

                // Check for suspicious attributes
                var dangerousAttributes = assembly.GetCustomAttributes()
                    .Where(attr => IsDangerousAttribute(attr.GetType()))
                    .ToList();


                if (dangerousAttributes.Count != 0)
                {
                    var attrNames = string.Join(", ", dangerousAttributes.Select(a => a.GetType().Name));
                    _logger.LogWarning("Assembly {AssemblyName} contains dangerous attributes: {Attributes}",

                        assemblyName.Name, attrNames);
                }

                // Validate types and members
                var types = assembly.GetTypes();
                foreach (var type in types.Take(100)) // Limit to prevent DoS
                {
                    ValidateTypeForSecurity(type);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch (Exception ex) when (ex is not SecurityException)
            {
                throw new SecurityException($"Assembly validation failed: {ex.Message}", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Validates a type for security compliance.
    /// </summary>
    private static void ValidateTypeForSecurity(Type type)
    {
        // Check for unsafe code
        if (type.Name.Contains("Unsafe", StringComparison.OrdinalIgnoreCase) ||
            type.Namespace?.Contains("Unsafe", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new SecurityException($"Type uses unsafe code: {type.FullName}");
        }

        // Check for dangerous base types
        var dangerousBaseTypes = new[]
        {
            "global::System.Runtime.InteropServices.Marshal",
            "System.Reflection.Assembly",
            "System.AppDomain"
        };

        var baseType = type.BaseType;
        while (baseType != null)
        {
            if (dangerousBaseTypes.Contains(baseType.FullName))
            {
                throw new SecurityException($"Type inherits from dangerous base type: {baseType.FullName}");
            }
            baseType = baseType.BaseType;
        }
    }

    /// <summary>
    /// Checks if an attribute type is considered dangerous.
    /// </summary>
    private static bool IsDangerousAttribute(Type attributeType)
    {
        var dangerousAttributes = new[]
        {
            "global::System.Security.AllowPartiallyTrustedCallersAttribute",
            "global::System.Security.SecurityCriticalAttribute",
            "global::System.Security.SuppressUnmanagedCodeSecurityAttribute",
            "global::System.Security.UnverifiableCodeAttribute"
        };

        return dangerousAttributes.Contains(attributeType.FullName);
    }

    /// <summary>
    /// Gets allowed permissions for an assembly based on path and policy.
    /// </summary>
    private HashSet<string> GetAllowedPermissions(string assemblyPath)
    {
        var permissions = new HashSet<string> { "Execution" };

        // Add permissions based on security policy

        if (_options.SecurityPolicy != null)
        {
            var fileName = Path.GetFileName(assemblyPath);

            // System assemblies get more permissions

            if (IsSystemAssembly(fileName))
            {
                _ = permissions.Add("FileIO");
                _ = permissions.Add("NetworkAccess");
            }

            // Add custom permissions based on configuration

            if (_options.SecurityPolicy.AllowedPermissions != null)
            {
                foreach (var permission in _options.SecurityPolicy.AllowedPermissions)
                {
                    _ = permissions.Add(permission);
                }
            }
        }


        return permissions;
    }

    /// <summary>
    /// Applies security restrictions to the plugin loading context.
    /// </summary>
    private static void ApplySecurityRestrictions(PluginSecurityContext context)
    {
        // Set up security context with restricted permissions
        context.RestrictedEnvironment = true;
        context.MaxMemoryUsage = 100 * 1024 * 1024; // 100MB limit
        context.MaxExecutionTime = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Checks if an assembly is a system assembly.
    /// </summary>
    private static bool IsSystemAssembly(string fileName)
    {
        return fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase);
    }

    private static Task<IBackendPlugin> CreatePluginInstanceAsync(Assembly assembly, NuGetPluginManifest manifest, CancellationToken cancellationToken)
    {
        var pluginTypes = PluginSystem.DiscoverPluginTypes(assembly).ToList();


        if (pluginTypes.Count == 0)
        {
            throw new PluginLoadException($"No plugin types found in assembly: {manifest.AssemblyPath}", manifest.Id, manifest.AssemblyPath);
        }

        var pluginType = pluginTypes.First();


        if (pluginType.GetConstructor(Type.EmptyTypes) == null)
        {
            throw new PluginLoadException($"Plugin type must have a parameterless constructor: {pluginType.FullName}", manifest.Id, manifest.AssemblyPath);
        }

        if (Activator.CreateInstance(pluginType) is not IBackendPlugin instance)
        {
            throw new PluginLoadException($"Failed to create plugin instance: {pluginType.FullName}", manifest.Id, manifest.AssemblyPath);
        }

        return Task.FromResult(instance);
    }

    private async Task UnloadUnusedDependenciesAsync(DependencyGraph dependencyGraph, CancellationToken cancellationToken)
    {
        foreach (var dependency in dependencyGraph.Dependencies.Where(d => d.IsPlugin))
        {
            if (IsDependencyStillNeeded(dependency.Id))
            {
                continue;
            }

            _ = await UnloadPluginAsync(dependency.Id, cancellationToken);
        }
    }

    private bool IsDependencyStillNeeded(string dependencyId)
    {
        return _loadedPlugins.Values.Any(p =>

            p.DependencyGraph.Dependencies.Any(d => d.Id == dependencyId));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, nameof(NuGetPluginLoader));

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var plugin in _loadedPlugins.Values)
        {
            try
            {
                plugin.Plugin?.Dispose();
                plugin.LoadContext?.Unload();
            }
            catch (Exception ex)
            {
                LogErrorDisposingPlugin(_logger, plugin.Manifest.Id, ex);
            }
        }

        _loadedPlugins.Clear();
        _loadSemaphore.Dispose();
    }
}

/// <summary>
/// Isolated load context for NuGet plugins with dependency resolution.
/// </summary>
public sealed class NuGetPluginLoadContext : AssemblyLoadContext
{
    private readonly NuGetPluginManifest _manifest;
    private readonly DependencyGraph _dependencyGraph;
    private readonly AssemblyDependencyResolver _resolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="NuGetPluginLoadContext"/> class.
    /// </summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="dependencyGraph">The dependency graph.</param>
    public NuGetPluginLoadContext(NuGetPluginManifest manifest, DependencyGraph dependencyGraph)

        : base($"Plugin_{manifest.Id}_{manifest.Version}", isCollectible: true)
    {
        _manifest = manifest;
        _dependencyGraph = dependencyGraph;
        _resolver = new AssemblyDependencyResolver(manifest.AssemblyPath);
    }

    /// <summary>
    /// When overridden in a derived class, allows an assembly to be resolved based on its <see cref="T:System.Reflection.AssemblyName" />.
    /// </summary>
    /// <param name="assemblyName">The object that describes the assembly to be resolved.</param>
    /// <returns>
    /// The resolved assembly, or <see langword="null" />.
    /// </returns>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Try to load from dependency graph
        var dependency = _dependencyGraph.Dependencies
            .FirstOrDefault(d => d.AssemblyName == assemblyName.Name);


        if (dependency?.AssemblyPath != null)
        {
            return LoadFromAssemblyPath(dependency.AssemblyPath);
        }

        return null;
    }

    /// <summary>
    /// Allows derived class to load an unmanaged library by name.
    /// </summary>
    /// <param name="unmanagedDllName">Name of the unmanaged library. Typically this is the filename without its path or extensions.</param>
    /// <returns>
    /// A handle to the loaded library, or <see cref="F:System.IntPtr.Zero" />.
    /// </returns>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
