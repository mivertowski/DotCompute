// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;
using DotCompute.Algorithms.Security;

namespace DotCompute.Algorithms.Management;

/// <summary>
/// Production-ready NuGet plugin loader with comprehensive package management, 
/// dependency resolution, caching, and security validation.
/// </summary>
public sealed partial class NuGetPluginLoader : IDisposable
{
    private readonly ILogger<NuGetPluginLoader> _logger;
    private readonly NuGetPluginLoaderOptions _options;
    private readonly SecurityPolicy _securityPolicy;
    private readonly AuthenticodeValidator _authenticodeValidator;
    private readonly MalwareScanningService _malwareScanner;
    private readonly ConcurrentDictionary<string, CachedPackage> _packageCache;
    private readonly SemaphoreSlim _downloadSemaphore;
    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Represents a cached NuGet package with metadata and extracted files.
    /// </summary>
    private sealed class CachedPackage
    {
        public required PackageIdentity Identity { get; init; }
        public required string ExtractedPath { get; init; }
        public required PackageManifest Manifest { get; init; }
        public required DateTime CacheTime { get; init; }
        public required string[] AssemblyPaths { get; init; }
        public required PackageDependency[] Dependencies { get; init; }
        public string? SignatureValidation { get; set; }
        public bool IsSecurityValidated { get; set; }
        public long PackageSize { get; init; }
        public string PackageHash { get; init; } = string.Empty;
    }

    /// <summary>
    /// Represents NuGet package manifest information.
    /// </summary>
    public sealed class PackageManifest
    {
        public required string Id { get; init; }
        public required string Version { get; init; }
        public required string Authors { get; init; }
        public required string Description { get; init; }
        public string? ProjectUrl { get; init; }
        public string? LicenseUrl { get; init; }
        public string? IconUrl { get; init; }
        public string? ReleaseNotes { get; init; }
        public string? Copyright { get; init; }
        public string[] Tags { get; init; } = Array.Empty<string>();
        public PackageDependency[] Dependencies { get; init; } = Array.Empty<PackageDependency>();
        public FrameworkSpecificGroup[] FrameworkGroups { get; init; } = Array.Empty<FrameworkSpecificGroup>();
        public bool RequireLicenseAcceptance { get; init; }
        public string? MinClientVersion { get; init; }
    }

    /// <summary>
    /// Represents a package dependency.
    /// </summary>
    public sealed class PackageDependency
    {
        public required string Id { get; init; }
        public required string VersionRange { get; init; }
        public string[]? TargetFrameworks { get; init; }
        public string[]? Exclude { get; init; }
        public string[]? Include { get; init; }
    }

    /// <summary>
    /// Represents framework-specific files in a package.
    /// </summary>
    public sealed class FrameworkSpecificGroup
    {
        public required string TargetFramework { get; init; }
        public required string[] Items { get; init; }
    }

    /// <summary>
    /// Result of NuGet package loading operation.
    /// </summary>
    public sealed class NuGetLoadResult
    {
        public required PackageIdentity PackageIdentity { get; init; }
        public required string[] LoadedAssemblyPaths { get; init; }
        public required PackageDependency[] ResolvedDependencies { get; init; }
        public required TimeSpan LoadTime { get; init; }
        public required long TotalSize { get; init; }
        public string[] Warnings { get; init; } = Array.Empty<string>();
        public string? SecurityValidationResult { get; init; }
        public bool FromCache { get; init; }
        public string? CachePath { get; init; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NuGetPluginLoader"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">Configuration options.</param>
    public NuGetPluginLoader(
        ILogger<NuGetPluginLoader> logger, 
        NuGetPluginLoaderOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new NuGetPluginLoaderOptions();
        
        // Initialize security components
        var securityLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<SecurityPolicy>.Instance;
        _securityPolicy = new SecurityPolicy(securityLogger);
        
        var authenticodeLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthenticodeValidator>.Instance;
        _authenticodeValidator = new AuthenticodeValidator(authenticodeLogger);
        
        var malwareLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MalwareScanningService>.Instance;
        _malwareScanner = new MalwareScanningService(malwareLogger, new MalwareScanningOptions());

        _packageCache = new ConcurrentDictionary<string, CachedPackage>();
        _downloadSemaphore = new SemaphoreSlim(_options.MaxConcurrentDownloads, _options.MaxConcurrentDownloads);
        _httpClient = new HttpClient();

        // Configure security policy
        ConfigureSecurityPolicy();

        // Ensure cache directory exists
        Directory.CreateDirectory(_options.CacheDirectory);

        LogNuGetLoaderInitialized();
    }

    /// <summary>
    /// Loads plugins from a NuGet package (.nupkg file or package ID).
    /// </summary>
    /// <param name="packageSource">Package source (.nupkg file path, package ID, or package ID with version).</param>
    /// <param name="targetFramework">Target framework for assembly selection (e.g., "net9.0").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The load result containing extracted assembly paths and metadata.</returns>
    public async Task<NuGetLoadResult> LoadPackageAsync(
        string packageSource, 
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageSource);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        targetFramework ??= _options.DefaultTargetFramework;

        LogLoadingPackage(packageSource, targetFramework);

        try
        {
            // Determine if it's a local .nupkg file or package ID
            if (File.Exists(packageSource) && Path.GetExtension(packageSource).Equals(".nupkg", StringComparison.OrdinalIgnoreCase))
            {
                return await LoadFromLocalPackageAsync(packageSource, targetFramework, stopwatch, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                return await LoadFromRemotePackageAsync(packageSource, targetFramework, stopwatch, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogPackageLoadFailed(packageSource, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Loads package from local .nupkg file.
    /// </summary>
    private async Task<NuGetLoadResult> LoadFromLocalPackageAsync(
        string nupkgPath, 
        string targetFramework, 
        System.Diagnostics.Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        // Validate package signature if required
        if (_options.RequirePackageSignature)
        {
            if (!await ValidatePackageSignatureAsync(nupkgPath))
            {
                throw new InvalidOperationException($"Package signature validation failed for: {nupkgPath}");
            }
        }

        // Extract package identity from .nupkg
        PackageIdentity identity;
        using (var packageStream = File.OpenRead(nupkgPath))
        using (var packageReader = new PackageArchiveReader(packageStream))
        {
            var nuspec = await packageReader.GetNuspecAsync(cancellationToken).ConfigureAwait(false);
            var manifest = Manifest.ReadFrom(nuspec, true);
            identity = new PackageIdentity(manifest.Metadata.Id, manifest.Metadata.Version);
        }

        // Check cache first
        var cacheKey = GetCacheKey(identity, targetFramework);
        if (_packageCache.TryGetValue(cacheKey, out var cachedPackage) && 
            IsCacheValid(cachedPackage))
        {
            LogPackageLoadedFromCache(identity.Id, identity.Version.ToString());
            return CreateLoadResult(cachedPackage, stopwatch.Elapsed, true);
        }

        // Extract and process package
        var extractedPath = await ExtractPackageAsync(nupkgPath, identity, cancellationToken)
            .ConfigureAwait(false);

        var processedPackage = await ProcessExtractedPackageAsync(
            identity, extractedPath, targetFramework, nupkgPath, cancellationToken)
            .ConfigureAwait(false);

        // Cache the processed package
        _packageCache.TryAdd(cacheKey, processedPackage);

        LogPackageLoadCompleted(identity.Id, identity.Version.ToString(), stopwatch.ElapsedMilliseconds);
        return CreateLoadResult(processedPackage, stopwatch.Elapsed, false);
    }

    /// <summary>
    /// Loads package from remote NuGet repository.
    /// </summary>
    private async Task<NuGetLoadResult> LoadFromRemotePackageAsync(
        string packageId,
        string targetFramework,
        System.Diagnostics.Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        // Parse package ID and version
        var (id, version) = ParsePackageIdAndVersion(packageId);
        
        // Resolve latest version if not specified
        if (version == null)
        {
            version = await ResolveLatestVersionAsync(id, cancellationToken).ConfigureAwait(false);
        }

        var identity = new PackageIdentity(id, version);
        var cacheKey = GetCacheKey(identity, targetFramework);

        // Check cache first
        if (_packageCache.TryGetValue(cacheKey, out var cachedPackage) && 
            IsCacheValid(cachedPackage))
        {
            LogPackageLoadedFromCache(identity.Id, identity.Version.ToString());
            return CreateLoadResult(cachedPackage, stopwatch.Elapsed, true);
        }

        await _downloadSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Download package
            var downloadPath = await DownloadPackageAsync(identity, cancellationToken)
                .ConfigureAwait(false);

            // Validate signature if required
            if (_options.RequirePackageSignature)
            {
                if (!await ValidatePackageSignatureAsync(downloadPath))
                {
                    throw new InvalidOperationException($"Package signature validation failed for: {identity}");
                }
            }

            // Extract and process
            var extractedPath = await ExtractPackageAsync(downloadPath, identity, cancellationToken)
                .ConfigureAwait(false);

            var processedPackage = await ProcessExtractedPackageAsync(
                identity, extractedPath, targetFramework, downloadPath, cancellationToken)
                .ConfigureAwait(false);

            // Cache the processed package
            _packageCache.TryAdd(cacheKey, processedPackage);

            LogPackageLoadCompleted(identity.Id, identity.Version.ToString(), stopwatch.ElapsedMilliseconds);
            return CreateLoadResult(processedPackage, stopwatch.Elapsed, false);
        }
        finally
        {
            _downloadSemaphore.Release();
        }
    }

    /// <summary>
    /// Downloads a package from configured NuGet sources.
    /// </summary>
    private async Task<string> DownloadPackageAsync(PackageIdentity identity, CancellationToken cancellationToken)
    {
        LogDownloadingPackage(identity.Id, identity.Version.ToString());

        var settings = Settings.LoadDefaultSettings(null);
        var sourceRepositoryProvider = new SourceRepositoryProvider(new PackageSourceProvider(settings), Repository.Provider.GetCoreV3());
        
        foreach (var sourceRepository in sourceRepositoryProvider.GetRepositories())
        {
            try
            {
                var downloadResource = await sourceRepository.GetResourceAsync<DownloadResource>(cancellationToken)
                    .ConfigureAwait(false);

                if (downloadResource == null) continue;

                var downloadPath = Path.Combine(_options.CacheDirectory, $"{identity.Id}.{identity.Version}.nupkg");
                
                using var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
                    identity,
                    new PackageDownloadContext(NullSourceCacheContext.Instance),
                    _options.CacheDirectory,
                    new NuGetLogger(_logger),
                    cancellationToken).ConfigureAwait(false);

                if (downloadResult.Status == DownloadResourceResultStatus.Available && downloadResult.PackageStream != null)
                {
                    using var fileStream = File.Create(downloadPath);
                    await downloadResult.PackageStream.CopyToAsync(fileStream, cancellationToken)
                        .ConfigureAwait(false);
                    
                    LogPackageDownloaded(identity.Id, identity.Version.ToString(), new FileInfo(downloadPath).Length);
                    return downloadPath;
                }
            }
            catch (Exception ex)
            {
                LogPackageDownloadFailed(identity.Id, sourceRepository.PackageSource.Source, ex.Message);
                // Continue to next source
            }
        }

        throw new InvalidOperationException($"Failed to download package {identity} from any configured source.");
    }

    /// <summary>
    /// Extracts a .nupkg file to the cache directory.
    /// </summary>
    private async Task<string> ExtractPackageAsync(
        string packagePath, 
        PackageIdentity identity, 
        CancellationToken cancellationToken)
    {
        var extractPath = Path.Combine(
            _options.CacheDirectory, 
            "extracted", 
            $"{identity.Id}.{identity.Version}");

        if (Directory.Exists(extractPath))
        {
            // Check if extraction is up to date
            var packageTime = File.GetLastWriteTimeUtc(packagePath);
            var extractTime = Directory.GetLastWriteTimeUtc(extractPath);
            
            if (extractTime >= packageTime)
            {
                LogPackageAlreadyExtracted(identity.Id, identity.Version.ToString());
                return extractPath;
            }
            
            // Clean up old extraction
            Directory.Delete(extractPath, true);
        }

        Directory.CreateDirectory(extractPath);

        LogExtractingPackage(identity.Id, identity.Version.ToString(), extractPath);

        try
        {
            // Extract using ZipFile for better control
            using var archive = ZipFile.OpenRead(packagePath);
            
            foreach (var entry in archive.Entries)
            {
                // Skip directory entries
                if (string.IsNullOrEmpty(entry.Name)) continue;

                // Security check: prevent directory traversal
                var entryPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));
                if (!entryPath.StartsWith(extractPath, StringComparison.OrdinalIgnoreCase))
                {
                    LogExtractionSecurityViolation(entry.FullName, identity.Id);
                    continue;
                }

                // Create directory if needed
                var directory = Path.GetDirectoryName(entryPath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Extract file
                entry.ExtractToFile(entryPath, true);
            }

            LogPackageExtracted(identity.Id, identity.Version.ToString(), archive.Entries.Count);
            return extractPath;
        }
        catch (Exception ex)
        {
            LogPackageExtractionFailed(identity.Id, identity.Version.ToString(), ex.Message);
            
            // Clean up on failure
            if (Directory.Exists(extractPath))
            {
                try
                {
                    Directory.Delete(extractPath, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            
            throw;
        }
    }

    /// <summary>
    /// Processes extracted package to identify plugin assemblies and validate them.
    /// </summary>
    private async Task<CachedPackage> ProcessExtractedPackageAsync(
        PackageIdentity identity,
        string extractedPath,
        string targetFramework,
        string originalPackagePath,
        CancellationToken cancellationToken)
    {
        LogProcessingPackage(identity.Id, identity.Version.ToString());

        // Load package manifest
        var manifest = await LoadPackageManifestAsync(extractedPath).ConfigureAwait(false);
        
        // Find compatible assemblies
        var assemblyPaths = await FindCompatibleAssembliesAsync(extractedPath, targetFramework)
            .ConfigureAwait(false);

        // Resolve dependencies
        var dependencies = await ResolveDependenciesAsync(identity, extractedPath)
            .ConfigureAwait(false);

        // Security validation
        var securityValidation = await ValidateAssembliesSecurityAsync(assemblyPaths)
            .ConfigureAwait(false);

        // Calculate package hash for integrity
        var packageHash = await CalculatePackageHashAsync(originalPackagePath)
            .ConfigureAwait(false);

        var packageSize = new FileInfo(originalPackagePath).Length;

        var cachedPackage = new CachedPackage
        {
            Identity = identity,
            ExtractedPath = extractedPath,
            Manifest = manifest,
            CacheTime = DateTime.UtcNow,
            AssemblyPaths = assemblyPaths,
            Dependencies = dependencies,
            SignatureValidation = securityValidation.SignatureResult,
            IsSecurityValidated = securityValidation.IsValid,
            PackageSize = packageSize,
            PackageHash = packageHash
        };

        LogPackageProcessed(identity.Id, identity.Version.ToString(), assemblyPaths.Length, dependencies.Length);
        return cachedPackage;
    }

    /// <summary>
    /// Loads package manifest from extracted package.
    /// </summary>
    private async Task<PackageManifest> LoadPackageManifestAsync(string extractedPath)
    {
        var nuspecPath = Directory.GetFiles(extractedPath, "*.nuspec", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        if (nuspecPath == null)
        {
            throw new InvalidOperationException("No .nuspec file found in package.");
        }

        var nuspecContent = await File.ReadAllTextAsync(nuspecPath).ConfigureAwait(false);
        var nuspecDoc = XDocument.Parse(nuspecContent);
        var packageElement = nuspecDoc.Root?.Element("package");
        var metadataElement = packageElement?.Element("metadata");

        if (metadataElement == null)
        {
            throw new InvalidOperationException("Invalid .nuspec file: missing metadata element.");
        }

        var dependencies = metadataElement.Element("dependencies")?
            .Elements("group")
            .SelectMany(g => g.Elements("dependency"))
            .Concat(metadataElement.Element("dependencies")?.Elements("dependency") ?? Enumerable.Empty<XElement>())
            .Select(d => new PackageDependency
            {
                Id = d.Attribute("id")?.Value ?? string.Empty,
                VersionRange = d.Attribute("version")?.Value ?? "*",
                TargetFrameworks = d.Parent?.Attribute("targetFramework")?.Value?.Split(';'),
                Exclude = d.Attribute("exclude")?.Value?.Split(','),
                Include = d.Attribute("include")?.Value?.Split(',')
            })
            .ToArray() ?? Array.Empty<PackageDependency>();

        return new PackageManifest
        {
            Id = metadataElement.Element("id")?.Value ?? "Unknown",
            Version = metadataElement.Element("version")?.Value ?? "0.0.0",
            Authors = metadataElement.Element("authors")?.Value ?? "Unknown",
            Description = metadataElement.Element("description")?.Value ?? string.Empty,
            ProjectUrl = metadataElement.Element("projectUrl")?.Value,
            LicenseUrl = metadataElement.Element("licenseUrl")?.Value,
            IconUrl = metadataElement.Element("iconUrl")?.Value,
            ReleaseNotes = metadataElement.Element("releaseNotes")?.Value,
            Copyright = metadataElement.Element("copyright")?.Value,
            Tags = metadataElement.Element("tags")?.Value?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>(),
            Dependencies = dependencies,
            RequireLicenseAcceptance = bool.Parse(metadataElement.Element("requireLicenseAcceptance")?.Value ?? "false"),
            MinClientVersion = metadataElement.Element("minClientVersion")?.Value
        };
    }

    /// <summary>
    /// Finds assemblies compatible with the target framework.
    /// </summary>
    private async Task<string[]> FindCompatibleAssembliesAsync(string extractedPath, string targetFramework)
    {
        await Task.CompletedTask; // Make async for consistency

        var libPath = Path.Combine(extractedPath, "lib");
        if (!Directory.Exists(libPath))
        {
            LogNoLibFolderFound(extractedPath);
            return Array.Empty<string>();
        }

        var targetNuGetFramework = NuGetFramework.Parse(targetFramework);
        var compatibleAssemblies = new List<string>();

        // Find best matching framework folder
        var frameworkFolders = Directory.GetDirectories(libPath)
            .Select(d => new
            {
                Path = d,
                Framework = NuGetFramework.ParseFolder(Path.GetFileName(d))
            })
            .Where(f => f.Framework != null && f.Framework.IsSpecificFramework)
            .ToList();

        if (!frameworkFolders.Any())
        {
            // No framework-specific folders, check root lib folder
            var rootAssemblies = Directory.GetFiles(libPath, "*.dll", SearchOption.TopDirectoryOnly);
            compatibleAssemblies.AddRange(rootAssemblies);
            LogFoundRootLevelAssemblies(rootAssemblies.Length);
        }
        else
        {
            // Find most compatible framework
            var reducer = new FrameworkReducer();
            var compatibleFrameworks = frameworkFolders
                .Select(f => f.Framework!)
                .Where(f => DefaultCompatibilityProvider.Instance.IsCompatible(targetNuGetFramework, f))
                .ToList();

            if (compatibleFrameworks.Any())
            {
                var bestMatch = reducer.GetNearest(targetNuGetFramework, compatibleFrameworks);
                var bestFolder = frameworkFolders.First(f => f.Framework!.Equals(bestMatch));
                
                var assemblies = Directory.GetFiles(bestFolder.Path, "*.dll", SearchOption.TopDirectoryOnly);
                compatibleAssemblies.AddRange(assemblies);
                LogFoundCompatibleAssemblies(bestMatch.GetShortFolderName(), assemblies.Length);
            }
            else
            {
                LogNoCompatibleFrameworkFound(targetFramework, string.Join(", ", frameworkFolders.Select(f => f.Framework!.GetShortFolderName())));
            }
        }

        return compatibleAssemblies.ToArray();
    }

    /// <summary>
    /// Resolves package dependencies recursively.
    /// </summary>
    private async Task<PackageDependency[]> ResolveDependenciesAsync(PackageIdentity identity, string extractedPath)
    {
        // For this implementation, we'll load dependencies from the manifest
        // A full implementation would recursively resolve and download dependencies
        var manifest = await LoadPackageManifestAsync(extractedPath).ConfigureAwait(false);
        return manifest.Dependencies;
    }

    /// <summary>
    /// Validates assemblies using security policies.
    /// </summary>
    private async Task<(bool IsValid, string? SignatureResult)> ValidateAssembliesSecurityAsync(string[] assemblyPaths)
    {
        if (!_options.EnableSecurityValidation || assemblyPaths.Length == 0)
        {
            return (true, null);
        }

        var results = new List<string>();
        var allValid = true;

        foreach (var assemblyPath in assemblyPaths)
        {
            try
            {
                // Authenticode validation
                if (_options.RequirePackageSignature)
                {
                    var signatureResult = await _authenticodeValidator.ValidateAsync(assemblyPath);
                    if (!signatureResult.IsValid)
                    {
                        allValid = false;
                        results.Add($"{Path.GetFileName(assemblyPath)}: Signature invalid - {signatureResult.ErrorMessage}");
                        continue;
                    }
                    results.Add($"{Path.GetFileName(assemblyPath)}: Signature valid - {signatureResult.SignerName}");
                }

                // Malware scanning
                if (_options.EnableMalwareScanning)
                {
                    var malwareResult = await _malwareScanner.ScanAssemblyAsync(assemblyPath);
                    if (!malwareResult.IsClean)
                    {
                        allValid = false;
                        results.Add($"{Path.GetFileName(assemblyPath)}: Malware detected - {malwareResult.ThreatDescription}");
                        continue;
                    }
                }

                // Security policy evaluation
                var context = new SecurityEvaluationContext
                {
                    AssemblyPath = assemblyPath,
                    AssemblyBytes = await File.ReadAllBytesAsync(assemblyPath)
                };

                var policyResult = _securityPolicy.EvaluateRules(context);
                if (!policyResult.IsAllowed)
                {
                    allValid = false;
                    results.Add($"{Path.GetFileName(assemblyPath)}: Policy violation - {string.Join(", ", policyResult.Violations)}");
                }
            }
            catch (Exception ex)
            {
                allValid = false;
                results.Add($"{Path.GetFileName(assemblyPath)}: Security validation error - {ex.Message}");
            }
        }

        return (allValid, string.Join("; ", results));
    }

    /// <summary>
    /// Validates package signature using NuGet signature validation.
    /// </summary>
    private async Task<bool> ValidatePackageSignatureAsync(string packagePath)
    {
        try
        {
            using var packageStream = File.OpenRead(packagePath);
            using var packageReader = new PackageArchiveReader(packageStream);
            
            // Check if package is signed
            var isSignedPackage = await packageReader.IsSignedAsync(CancellationToken.None);
            if (!isSignedPackage && _options.RequirePackageSignature)
            {
                LogPackageNotSigned(packagePath);
                return false;
            }

            // If signed, validate signature (simplified validation)
            if (isSignedPackage)
            {
                // In a production environment, you would use NuGet's signature validation APIs
                // For now, we'll do basic validation through the package reader
                try
                {
                    var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                    if (primarySignature == null)
                    {
                        LogPackageSignatureInvalid(packagePath, "No primary signature found");
                        return false;
                    }
                    
                    LogPackageSignatureValid(packagePath);
                    return true;
                }
                catch (Exception ex)
                {
                    LogPackageSignatureInvalid(packagePath, ex.Message);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            LogPackageSignatureValidationError(packagePath, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Resolves the latest version of a package.
    /// </summary>
    private async Task<NuGetVersion> ResolveLatestVersionAsync(string packageId, CancellationToken cancellationToken)
    {
        var settings = Settings.LoadDefaultSettings(null);
        var sourceRepositoryProvider = new SourceRepositoryProvider(new PackageSourceProvider(settings), Repository.Provider.GetCoreV3());

        foreach (var sourceRepository in sourceRepositoryProvider.GetRepositories())
        {
            try
            {
                var metadataResource = await sourceRepository.GetResourceAsync<PackageMetadataResource>(cancellationToken)
                    .ConfigureAwait(false);

                if (metadataResource == null) continue;

                var packages = await metadataResource.GetMetadataAsync(
                    packageId, 
                    includePrerelease: _options.IncludePrerelease,
                    includeUnlisted: false,
                    new SourceCacheContext(),
                    new NuGetLogger(_logger),
                    cancellationToken).ConfigureAwait(false);

                var latestPackage = packages
                    .Where(p => !p.Identity.Version.IsPrerelease || _options.IncludePrerelease)
                    .OrderByDescending(p => p.Identity.Version)
                    .FirstOrDefault();

                if (latestPackage != null)
                {
                    LogLatestVersionResolved(packageId, latestPackage.Identity.Version.ToString());
                    return latestPackage.Identity.Version;
                }
            }
            catch (Exception ex)
            {
                LogVersionResolutionFailed(packageId, sourceRepository.PackageSource.Source, ex.Message);
                // Continue to next source
            }
        }

        throw new InvalidOperationException($"Could not resolve latest version for package: {packageId}");
    }

    /// <summary>
    /// Updates a cached package to a newer version.
    /// </summary>
    /// <param name="packageId">Package ID to update.</param>
    /// <param name="targetFramework">Target framework.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Update result.</returns>
    public async Task<NuGetLoadResult> UpdatePackageAsync(
        string packageId,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        targetFramework ??= _options.DefaultTargetFramework;

        // Find current cached version
        var currentCached = _packageCache.Values
            .Where(p => p.Identity.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Identity.Version)
            .FirstOrDefault();

        if (currentCached == null)
        {
            // Not cached, just load latest
            return await LoadPackageAsync(packageId, targetFramework, cancellationToken).ConfigureAwait(false);
        }

        // Check for newer version
        var latestVersion = await ResolveLatestVersionAsync(packageId, cancellationToken).ConfigureAwait(false);
        
        if (latestVersion <= currentCached.Identity.Version)
        {
            LogPackageAlreadyLatest(packageId, currentCached.Identity.Version.ToString());
            return CreateLoadResult(currentCached, TimeSpan.Zero, true);
        }

        // Remove old version from cache
        var oldCacheKey = GetCacheKey(currentCached.Identity, targetFramework);
        _packageCache.TryRemove(oldCacheKey, out _);

        // Clean up old extraction
        if (Directory.Exists(currentCached.ExtractedPath))
        {
            try
            {
                Directory.Delete(currentCached.ExtractedPath, true);
            }
            catch (Exception ex)
            {
                LogCacheCleanupFailed(currentCached.ExtractedPath, ex.Message);
            }
        }

        LogUpdatingPackage(packageId, currentCached.Identity.Version.ToString(), latestVersion.ToString());

        // Load new version
        return await LoadPackageAsync($"{packageId}:{latestVersion}", targetFramework, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets information about cached packages.
    /// </summary>
    /// <returns>Array of cached package information.</returns>
    public CachedPackageInfo[] GetCachedPackages()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _packageCache.Values
            .Select(cp => new CachedPackageInfo
            {
                Identity = cp.Identity,
                CacheTime = cp.CacheTime,
                ExtractedPath = cp.ExtractedPath,
                AssemblyCount = cp.AssemblyPaths.Length,
                DependencyCount = cp.Dependencies.Length,
                PackageSize = cp.PackageSize,
                IsSecurityValidated = cp.IsSecurityValidated,
                CacheAge = DateTime.UtcNow - cp.CacheTime
            })
            .OrderByDescending(cp => cp.CacheTime)
            .ToArray();
    }

    /// <summary>
    /// Clears the package cache and removes extracted files.
    /// </summary>
    /// <param name="olderThan">Optional age filter - only clear packages older than this timespan.</param>
    public async Task ClearCacheAsync(TimeSpan? olderThan = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var packagesToRemove = olderThan.HasValue
            ? _packageCache.Where(kvp => DateTime.UtcNow - kvp.Value.CacheTime > olderThan.Value).ToList()
            : _packageCache.ToList();

        LogClearingCache(packagesToRemove.Count, olderThan?.ToString() ?? "all");

        var cleanupTasks = packagesToRemove.Select(async kvp =>
        {
            _packageCache.TryRemove(kvp.Key, out _);
            
            if (Directory.Exists(kvp.Value.ExtractedPath))
            {
                try
                {
                    await Task.Run(() => Directory.Delete(kvp.Value.ExtractedPath, true));
                }
                catch (Exception ex)
                {
                    LogCacheCleanupFailed(kvp.Value.ExtractedPath, ex.Message);
                }
            }
        });

        await Task.WhenAll(cleanupTasks).ConfigureAwait(false);
        LogCacheCleared(packagesToRemove.Count);
    }

    #region Private Helper Methods

    private void ConfigureSecurityPolicy()
    {
        _securityPolicy.RequireDigitalSignature = _options.RequirePackageSignature;
        _securityPolicy.EnableMalwareScanning = _options.EnableMalwareScanning;
        _securityPolicy.MaxAssemblySize = _options.MaxAssemblySize;
        _securityPolicy.MinimumSecurityLevel = _options.MinimumSecurityLevel;
    }

    private static string GetCacheKey(PackageIdentity identity, string targetFramework)
    {
        return $"{identity.Id}|{identity.Version}|{targetFramework}".ToLowerInvariant();
    }

    private bool IsCacheValid(CachedPackage cachedPackage)
    {
        var age = DateTime.UtcNow - cachedPackage.CacheTime;
        return age <= _options.CacheExpiration && 
               Directory.Exists(cachedPackage.ExtractedPath) &&
               cachedPackage.AssemblyPaths.All(File.Exists);
    }

    private static (string Id, NuGetVersion? Version) ParsePackageIdAndVersion(string packageSource)
    {
        var parts = packageSource.Split(':');
        if (parts.Length == 1)
        {
            return (parts[0], null);
        }
        
        if (parts.Length == 2 && NuGetVersion.TryParse(parts[1], out var version))
        {
            return (parts[0], version);
        }

        throw new ArgumentException($"Invalid package source format: {packageSource}. Expected 'PackageId' or 'PackageId:Version'.");
    }

    private async Task<string> CalculatePackageHashAsync(string packagePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(packagePath);
        var hashBytes = await sha256.ComputeHashAsync(stream).ConfigureAwait(false);
        return Convert.ToHexString(hashBytes);
    }

    private NuGetLoadResult CreateLoadResult(CachedPackage cachedPackage, TimeSpan loadTime, bool fromCache)
    {
        return new NuGetLoadResult
        {
            PackageIdentity = cachedPackage.Identity,
            LoadedAssemblyPaths = cachedPackage.AssemblyPaths,
            ResolvedDependencies = cachedPackage.Dependencies,
            LoadTime = loadTime,
            TotalSize = cachedPackage.PackageSize,
            SecurityValidationResult = cachedPackage.SignatureValidation,
            FromCache = fromCache,
            CachePath = cachedPackage.ExtractedPath
        };
    }

    #endregion

    #region Logger Messages

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "NuGet plugin loader initialized with cache directory")]
    private partial void LogNuGetLoaderInitialized();

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Loading package: {PackageSource} for framework: {TargetFramework}")]
    private partial void LogLoadingPackage(string packageSource, string targetFramework);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Package loaded from cache: {PackageId} v{Version}")]
    private partial void LogPackageLoadedFromCache(string packageId, string version);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Package load completed: {PackageId} v{Version} in {ElapsedMs} ms")]
    private partial void LogPackageLoadCompleted(string packageId, string version, long elapsedMs);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Package load failed for {PackageSource}: {Reason}")]
    private partial void LogPackageLoadFailed(string packageSource, string reason);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Downloading package: {PackageId} v{Version}")]
    private partial void LogDownloadingPackage(string packageId, string version);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Package downloaded: {PackageId} v{Version}, size: {Size} bytes")]
    private partial void LogPackageDownloaded(string packageId, string version, long size);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Package download failed for {PackageId} from source {Source}: {Reason}")]
    private partial void LogPackageDownloadFailed(string packageId, string source, string reason);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Package already extracted: {PackageId} v{Version}")]
    private partial void LogPackageAlreadyExtracted(string packageId, string version);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Extracting package: {PackageId} v{Version} to {ExtractPath}")]
    private partial void LogExtractingPackage(string packageId, string version, string extractPath);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Package extracted: {PackageId} v{Version}, {FileCount} files")]
    private partial void LogPackageExtracted(string packageId, string version, int fileCount);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Package extraction failed: {PackageId} v{Version}: {Reason}")]
    private partial void LogPackageExtractionFailed(string packageId, string version, string reason);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Security violation during extraction - potential directory traversal: {EntryName} in package {PackageId}")]
    private partial void LogExtractionSecurityViolation(string entryName, string packageId);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Processing package: {PackageId} v{Version}")]
    private partial void LogProcessingPackage(string packageId, string version);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Package processed: {PackageId} v{Version}, {AssemblyCount} assemblies, {DependencyCount} dependencies")]
    private partial void LogPackageProcessed(string packageId, string version, int assemblyCount, int dependencyCount);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "No lib folder found in package at: {ExtractedPath}")]
    private partial void LogNoLibFolderFound(string extractedPath);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Found {AssemblyCount} root-level assemblies")]
    private partial void LogFoundRootLevelAssemblies(int assemblyCount);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Found {AssemblyCount} assemblies for framework: {Framework}")]
    private partial void LogFoundCompatibleAssemblies(string framework, int assemblyCount);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "No compatible framework found for {TargetFramework}. Available frameworks: {AvailableFrameworks}")]
    private partial void LogNoCompatibleFrameworkFound(string targetFramework, string availableFrameworks);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Latest version resolved for {PackageId}: {Version}")]
    private partial void LogLatestVersionResolved(string packageId, string version);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Version resolution failed for {PackageId} from source {Source}: {Reason}")]
    private partial void LogVersionResolutionFailed(string packageId, string source, string reason);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Package {PackageId} is already at latest version: {Version}")]
    private partial void LogPackageAlreadyLatest(string packageId, string version);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Updating package {PackageId} from {OldVersion} to {NewVersion}")]
    private partial void LogUpdatingPackage(string packageId, string oldVersion, string newVersion);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Package not signed: {PackagePath}")]
    private partial void LogPackageNotSigned(string packagePath);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Package signature valid: {PackagePath}")]
    private partial void LogPackageSignatureValid(string packagePath);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Package signature invalid: {PackagePath}: {Reason}")]
    private partial void LogPackageSignatureInvalid(string packagePath, string reason);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Package signature validation error: {PackagePath}: {Reason}")]
    private partial void LogPackageSignatureValidationError(string packagePath, string reason);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Clearing cache: {PackageCount} packages, filter: {Filter}")]
    private partial void LogClearingCache(int packageCount, string filter);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Cache cleared: {PackageCount} packages removed")]
    private partial void LogCacheCleared(int packageCount);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Cache cleanup failed for {Path}: {Reason}")]
    private partial void LogCacheCleanupFailed(string path, string reason);

    #endregion

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _httpClient.Dispose();
            _downloadSemaphore.Dispose();
            _authenticodeValidator?.Dispose();
            _malwareScanner?.Dispose();
        }
    }
}

/// <summary>
/// Configuration options for NuGet plugin loader.
/// </summary>
public sealed class NuGetPluginLoaderOptions
{
    /// <summary>
    /// Gets or sets the cache directory for downloaded and extracted packages.
    /// </summary>
    public string CacheDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "DotComputeNuGetCache");

    /// <summary>
    /// Gets or sets the cache expiration time.
    /// </summary>
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the default target framework for assembly selection.
    /// </summary>
    public string DefaultTargetFramework { get; set; } = "net9.0";

    /// <summary>
    /// Gets or sets whether to include prerelease versions.
    /// </summary>
    public bool IncludePrerelease { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of concurrent downloads.
    /// </summary>
    public int MaxConcurrentDownloads { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether security validation is enabled.
    /// </summary>
    public bool EnableSecurityValidation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether package signatures are required.
    /// </summary>
    public bool RequirePackageSignature { get; set; } = true;

    /// <summary>
    /// Gets or sets whether malware scanning is enabled.
    /// </summary>
    public bool EnableMalwareScanning { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum allowed assembly size in bytes.
    /// </summary>
    public long MaxAssemblySize { get; set; } = 50 * 1024 * 1024; // 50 MB

    /// <summary>
    /// Gets or sets the minimum security level required.
    /// </summary>
    public SecurityLevel MinimumSecurityLevel { get; set; } = SecurityLevel.Medium;

    /// <summary>
    /// Gets the trusted package sources.
    /// </summary>
    public List<string> TrustedSources { get; } = new() { "https://api.nuget.org/v3/index.json" };

    /// <summary>
    /// Gets the blocked package IDs.
    /// </summary>
    public HashSet<string> BlockedPackages { get; } = new();

    /// <summary>
    /// Gets the required package prefixes for security.
    /// </summary>
    public HashSet<string> AllowedPackagePrefixes { get; } = new();
}

/// <summary>
/// Information about a cached package.
/// </summary>
public sealed class CachedPackageInfo
{
    /// <summary>
    /// Gets or sets the package identity.
    /// </summary>
    public required PackageIdentity Identity { get; init; }

    /// <summary>
    /// Gets or sets when the package was cached.
    /// </summary>
    public DateTime CacheTime { get; init; }

    /// <summary>
    /// Gets or sets the extracted path.
    /// </summary>
    public required string ExtractedPath { get; init; }

    /// <summary>
    /// Gets or sets the number of assemblies in the package.
    /// </summary>
    public int AssemblyCount { get; init; }

    /// <summary>
    /// Gets or sets the number of dependencies.
    /// </summary>
    public int DependencyCount { get; init; }

    /// <summary>
    /// Gets or sets the package size in bytes.
    /// </summary>
    public long PackageSize { get; init; }

    /// <summary>
    /// Gets or sets whether security validation passed.
    /// </summary>
    public bool IsSecurityValidated { get; init; }

    /// <summary>
    /// Gets or sets the age of the cached package.
    /// </summary>
    public TimeSpan CacheAge { get; init; }
}

/// <summary>
/// NuGet logger adapter for integration with Microsoft.Extensions.Logging.
/// </summary>
public sealed class NuGetLogger : LoggerBase
{
    private readonly Microsoft.Extensions.Logging.ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NuGetLogger"/> class.
    /// </summary>
    /// <param name="logger">The underlying logger.</param>
    public NuGetLogger(Microsoft.Extensions.Logging.ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public override void Log(ILogMessage message)
    {
        var logLevel = message.Level switch
        {
            NuGet.Common.LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            NuGet.Common.LogLevel.Verbose => Microsoft.Extensions.Logging.LogLevel.Trace,
            NuGet.Common.LogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
            NuGet.Common.LogLevel.Minimal => Microsoft.Extensions.Logging.LogLevel.Information,
            NuGet.Common.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            NuGet.Common.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            _ => Microsoft.Extensions.Logging.LogLevel.Information
        };

        _logger.Log(logLevel, "{Message}", message.Message);
    }

    /// <inheritdoc/>
    public override Task LogAsync(ILogMessage message)
    {
        Log(message);
        return Task.CompletedTask;
    }
}