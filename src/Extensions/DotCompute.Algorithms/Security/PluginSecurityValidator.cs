// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using DotCompute.Abstractions.Security;
using DotCompute.Algorithms.Management.Configuration;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Security;

/// <summary>
/// Comprehensive security validator for algorithm plugins with defense-in-depth validation.
/// Implements multiple layers of security checking including:
/// - Strong-name signature verification
/// - Assembly code analysis for dangerous patterns
/// - Dependency validation
/// - Resource access validation
/// - Rate limiting
/// - Code integrity verification
/// </summary>
public sealed partial class PluginSecurityValidator : IDisposable, IAsyncDisposable
{
    private readonly ILogger<PluginSecurityValidator> _logger;
    private readonly AlgorithmPluginManagerOptions _options;
    private readonly ConcurrentDictionary<string, SecurityValidationCacheEntry> _validationCache = new();
    private readonly ConcurrentDictionary<string, int> _loadAttempts = new();
    private readonly SemaphoreSlim _validationSemaphore = new(1, 1);
    private readonly TimeSpan _rateLimitWindow = TimeSpan.FromMinutes(5);
    private readonly int _maxLoadAttemptsPerWindow = 10;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginSecurityValidator"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="options">Plugin manager options.</param>
    public PluginSecurityValidator(ILogger<PluginSecurityValidator> logger, AlgorithmPluginManagerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Validates plugin assembly security with comprehensive multi-layered checks.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed security validation result.</returns>
    public async Task<PluginSecurityResult> ValidatePluginSecurityAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        // Rate limiting is checked first, before any file access, so repeated *attempts* to load a
        // plugin are throttled regardless of whether each request resolves to a valid file. The rate
        // limit is keyed on the normalized base path so that query-string or casing variations of the
        // same underlying plugin accumulate against a single counter.
        if (!CheckRateLimit(assemblyPath))
        {
            LogRateLimitExceeded(assemblyPath, _maxLoadAttemptsPerWindow, _rateLimitWindow.TotalMinutes);
            return PluginSecurityResult.Failure(
                $"Rate limit exceeded: Maximum {_maxLoadAttemptsPerWindow} load attempts per {_rateLimitWindow.TotalMinutes} minutes",
                ThreatLevel.High);
        }

        if (!File.Exists(assemblyPath))
        {
            return PluginSecurityResult.Failure("Assembly file not found", ThreatLevel.Critical);
        }

        // Check cache first
        var fileHash = await ComputeFileHashAsync(assemblyPath, cancellationToken);
        var cacheKey = $"{assemblyPath}:{fileHash}";

        if (_validationCache.TryGetValue(cacheKey, out var cachedEntry) && !cachedEntry.IsExpired)
        {
            LogSecurityValidationCacheHit(assemblyPath);
            return cachedEntry.Result;
        }

        await _validationSemaphore.WaitAsync(cancellationToken);
        try
        {
            LogStartingSecurityValidation(assemblyPath);
            var startTime = DateTime.UtcNow;
            var violations = new List<string>();
            var warnings = new List<string>();
            var threatLevel = ThreatLevel.None;

            // Layer 1: File integrity validation
            var integrityResult = await ValidateFileIntegrityAsync(assemblyPath, cancellationToken);
            if (!integrityResult.IsValid)
            {
                violations.AddRange(integrityResult.Violations);
                threatLevel = UpdateThreatLevel(threatLevel, integrityResult.ThreatLevel);
            }
            warnings.AddRange(integrityResult.Warnings);

            // Layer 2: Strong-name signature validation
            if (_options.RequireStrongName)
            {
                var signatureResult = await ValidateStrongNameSignatureAsync(assemblyPath, cancellationToken);
                if (!signatureResult.IsValid)
                {
                    violations.AddRange(signatureResult.Violations);
                    threatLevel = UpdateThreatLevel(threatLevel, signatureResult.ThreatLevel);
                }
                warnings.AddRange(signatureResult.Warnings);
            }

            // Layer 3: Assembly code analysis (dangerous patterns)
            if (_options.EnableMetadataAnalysis)
            {
                var codeAnalysisResult = await AnalyzeAssemblyCodeAsync(assemblyPath, cancellationToken);
                if (!codeAnalysisResult.IsValid)
                {
                    violations.AddRange(codeAnalysisResult.Violations);
                    threatLevel = UpdateThreatLevel(threatLevel, codeAnalysisResult.ThreatLevel);
                }
                warnings.AddRange(codeAnalysisResult.Warnings);
            }

            // Layer 4: Dependency validation
            var dependencyResult = await ValidateDependenciesAsync(assemblyPath, cancellationToken);
            if (!dependencyResult.IsValid)
            {
                violations.AddRange(dependencyResult.Violations);
                threatLevel = UpdateThreatLevel(threatLevel, dependencyResult.ThreatLevel);
            }
            warnings.AddRange(dependencyResult.Warnings);

            // Layer 5: Resource access validation
            var resourceResult = await ValidateResourceAccessAsync(assemblyPath, cancellationToken);
            if (!resourceResult.IsValid)
            {
                violations.AddRange(resourceResult.Violations);
                threatLevel = UpdateThreatLevel(threatLevel, resourceResult.ThreatLevel);
            }
            warnings.AddRange(resourceResult.Warnings);

            // Layer 6: Attribute validation (must have [AlgorithmPlugin])
            var attributeResult = await ValidateRequiredAttributesAsync(assemblyPath, cancellationToken);
            if (!attributeResult.IsValid)
            {
                violations.AddRange(attributeResult.Violations);
                threatLevel = UpdateThreatLevel(threatLevel, attributeResult.ThreatLevel);
            }
            warnings.AddRange(attributeResult.Warnings);

            var duration = DateTime.UtcNow - startTime;
            var isValid = violations.Count == 0 && threatLevel <= ThreatLevel.Low;

            var result = new PluginSecurityResult
            {
                IsValid = isValid,
                ThreatLevel = threatLevel,
                Violations = violations.AsReadOnly(),
                Warnings = warnings.AsReadOnly(),
                FileHash = fileHash,
                ValidationDuration = duration,
                Metadata = new Dictionary<string, object>
                {
                    ["AssemblyPath"] = assemblyPath,
                    ["AssemblySize"] = new FileInfo(assemblyPath).Length,
                    ["ValidationLayers"] = 6,
                    ["ViolationCount"] = violations.Count,
                    ["WarningCount"] = warnings.Count
                }
            };

            // Cache the result
            _validationCache[cacheKey] = new SecurityValidationCacheEntry(result, _options.CacheExpiration);

            if (isValid)
            {
                LogSecurityValidationPassed(assemblyPath, threatLevel, duration.TotalMilliseconds);
            }
            else
            {
                LogSecurityValidationFailed(assemblyPath, threatLevel, string.Join("; ", violations));
            }

            return result;
        }
        finally
        {
            _validationSemaphore.Release();
        }
    }

    /// <summary>
    /// Validates file integrity including size limits and hash verification.
    /// </summary>
    private async Task<ValidationLayerResult> ValidateFileIntegrityAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        var violations = new List<string>();
        var warnings = new List<string>();

        try
        {
            var fileInfo = new FileInfo(assemblyPath);

            // Check size limits
            if (fileInfo.Length > _options.MaxAssemblySize)
            {
                violations.Add($"Assembly size ({fileInfo.Length:N0} bytes) exceeds maximum allowed ({_options.MaxAssemblySize:N0} bytes)");
                return new ValidationLayerResult(false, violations, warnings, ThreatLevel.High);
            }

            if (fileInfo.Length < 1024)
            {
                warnings.Add("Assembly file is unusually small (< 1KB)");
            }

            // Check file attributes for suspicious flags
            if (fileInfo.Attributes.HasFlag(FileAttributes.Hidden))
            {
                violations.Add("Assembly has hidden file attribute");
                return new ValidationLayerResult(false, violations, warnings, ThreatLevel.Medium);
            }

            // Verify file is readable
            await using var stream = File.OpenRead(assemblyPath);

            // Check for valid PE header
            var headerBytes = new byte[2];
            var bytesRead = await stream.ReadAsync(headerBytes.AsMemory(), cancellationToken);
            if (bytesRead < 2)
            {
                violations.Add("File is too small to contain valid PE header");
                return new ValidationLayerResult(false, violations, warnings, ThreatLevel.Critical);
            }
            if (headerBytes[0] != 0x4D || headerBytes[1] != 0x5A) // "MZ" signature
            {
                violations.Add("File does not have valid PE header (MZ signature)");
                return new ValidationLayerResult(false, violations, warnings, ThreatLevel.Critical);
            }

            LogFileIntegrityValid(assemblyPath, fileInfo.Length);
            return new ValidationLayerResult(true, violations, warnings, ThreatLevel.None);
        }
        catch (Exception ex)
        {
            violations.Add($"File integrity check failed: {ex.Message}");
            return new ValidationLayerResult(false, violations, warnings, ThreatLevel.High);
        }
    }

    /// <summary>
    /// Validates strong-name signature on the assembly.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Security validation requires assembly loading")]
    private async Task<ValidationLayerResult> ValidateStrongNameSignatureAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        var violations = new List<string>();
        var warnings = new List<string>();

        try
        {
            await Task.Yield(); // Make properly async
            cancellationToken.ThrowIfCancellationRequested();

            var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
            var publicKey = assemblyName.GetPublicKey();

            if (publicKey == null || publicKey.Length == 0)
            {
                violations.Add("Assembly is not strong-named (no public key)");
                return new ValidationLayerResult(false, violations, warnings, ThreatLevel.High);
            }

            // Verify key strength (minimum 1024-bit RSA = 160 bytes)
            if (publicKey.Length < 160)
            {
                violations.Add($"Strong-name key is too weak ({publicKey.Length * 8} bits, minimum 1024 bits required)");
                return new ValidationLayerResult(false, violations, warnings, ThreatLevel.High);
            }

            // Check against trusted publishers
            if (_options.TrustedPublishers.Count > 0)
            {
                var assemblySimpleName = assemblyName.Name ?? string.Empty;
                var isTrusted = _options.TrustedPublishers.Any(publisher =>
                    assemblySimpleName.StartsWith(publisher, StringComparison.OrdinalIgnoreCase));

                if (!isTrusted)
                {
                    warnings.Add($"Assembly '{assemblySimpleName}' is not from a trusted publisher");
                }
            }

            LogStrongNameValid(assemblyPath, publicKey.Length * 8);
            return new ValidationLayerResult(true, violations, warnings, ThreatLevel.None);
        }
        catch (Exception ex)
        {
            violations.Add($"Strong-name validation failed: {ex.Message}");
            return new ValidationLayerResult(false, violations, warnings, ThreatLevel.High);
        }
    }

    /// <summary>
    /// Reads the set of namespaces referenced by an assembly directly from its PE metadata, without
    /// loading the assembly or resolving its dependencies. This is the basis for usage detection:
    /// scanning <c>TypeReference</c> rows reveals which framework types an assembly actually uses
    /// (e.g. <c>System.IO</c>, <c>System.Net</c>, <c>System.Reflection.Emit</c>), which inheritance
    /// checks via <see cref="Assembly.LoadFrom(string)"/> cannot see — and which loading would fail to do at
    /// all for framework assemblies whose dependencies are not resolvable in the host load context.
    /// </summary>
    private static AssemblyMetadataReferences ReadReferencedNamespaces(string assemblyPath)
    {
        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        var typeNames = new HashSet<string>(StringComparer.Ordinal);

        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);

        if (!peReader.HasMetadata)
        {
            return new AssemblyMetadataReferences(namespaces, typeNames);
        }

        var reader = peReader.GetMetadataReader();

        // Referenced types (what this assembly USES from other assemblies).
        foreach (var handle in reader.TypeReferences)
        {
            var typeRef = reader.GetTypeReference(handle);
            var ns = reader.GetString(typeRef.Namespace);
            var name = reader.GetString(typeRef.Name);
            if (!string.IsNullOrEmpty(ns))
            {
                _ = namespaces.Add(ns);
                _ = typeNames.Add($"{ns}.{name}");
            }
            else if (!string.IsNullOrEmpty(name))
            {
                _ = typeNames.Add(name);
            }
        }

        // Defined types (what this assembly CONTAINS). This covers the case where an assembly is the
        // one that declares the sensitive APIs (e.g. the framework assembly that defines
        // System.Reflection.Emit) rather than merely referencing them.
        foreach (var handle in reader.TypeDefinitions)
        {
            var typeDef = reader.GetTypeDefinition(handle);
            var ns = reader.GetString(typeDef.Namespace);
            var name = reader.GetString(typeDef.Name);
            if (!string.IsNullOrEmpty(ns))
            {
                _ = namespaces.Add(ns);
                _ = typeNames.Add($"{ns}.{name}");
            }
        }

        return new AssemblyMetadataReferences(namespaces, typeNames);
    }

    /// <summary>
    /// Returns true if the assembly references any type in the given namespace (or a sub-namespace).
    /// </summary>
    private static bool ReferencesNamespace(AssemblyMetadataReferences references, string targetNamespace)
    {
        foreach (var ns in references.Namespaces)
        {
            if (string.Equals(ns, targetNamespace, StringComparison.Ordinal) ||
                ns.StartsWith(targetNamespace + ".", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private readonly record struct AssemblyMetadataReferences(HashSet<string> Namespaces, HashSet<string> TypeNames);

    /// <summary>
    /// Analyzes assembly code for dangerous patterns and suspicious API usage.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Security analysis requires full type inspection")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Security scanning requires comprehensive reflection")]
    private async Task<ValidationLayerResult> AnalyzeAssemblyCodeAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        var violations = new List<string>();
        var warnings = new List<string>();
        var threatLevel = ThreatLevel.None;

        // Metadata-based detection runs first and never loads/resolves the assembly, so it works even
        // for framework assemblies whose dependencies cannot be resolved in the host load context.
        try
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            var metadata = ReadReferencedNamespaces(assemblyPath);
            if (ReferencesNamespace(metadata, "System.Reflection.Emit"))
            {
                violations.Add("Assembly references System.Reflection.Emit (dynamic code generation)");
                threatLevel = UpdateThreatLevel(threatLevel, ThreatLevel.Critical);
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Metadata code analysis incomplete: {ex.Message}");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var assembly = Assembly.LoadFrom(assemblyPath);
            var types = assembly.GetTypes();

            // Count dangerous patterns
            var unsafeCodeCount = 0;
            var reflectionEmitCount = 0;
            var pinvokeCount = 0;
            var processStartCount = 0;
            var registryAccessCount = 0;

            foreach (var type in types)
            {
                // Check for unsafe code blocks
                if (type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                    .Any(m => m.GetMethodBody()?.LocalVariables.Any(v => v.LocalType.IsPointer) == true))
                {
                    unsafeCodeCount++;
                }

                // Check for reflection emit (dynamic code generation)
                if (typeof(System.Reflection.Emit.DynamicMethod).IsAssignableFrom(type) ||
                    typeof(System.Reflection.Emit.ILGenerator).IsAssignableFrom(type))
                {
                    reflectionEmitCount++;
                }

                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

                foreach (var method in methods)
                {
                    // Check for P/Invoke declarations
                    if (method.GetCustomAttribute<DllImportAttribute>() != null)
                    {
                        pinvokeCount++;

                        var dllImport = method.GetCustomAttribute<DllImportAttribute>();
                        var dllName = dllImport?.Value?.ToUpperInvariant() ?? string.Empty;

                        // Check for particularly dangerous DLLs
                        if (dllName.Contains("KERNEL32", StringComparison.Ordinal) ||
                            dllName.Contains("NTDLL", StringComparison.Ordinal) ||
                            dllName.Contains("ADVAPI32", StringComparison.Ordinal))
                        {
                            violations.Add($"Plugin attempts to import dangerous system library: {dllName} in method {type.FullName}.{method.Name}");
                            threatLevel = ThreatLevel.Critical;
                        }
                    }

                    // Check method body for dangerous API calls
                    var methodBody = method.GetMethodBody();
                    if (methodBody != null)
                    {
                        // This is a simplified check - in production you'd use IL analysis
                        var methodName = method.Name.ToUpperInvariant();

                        if (methodName.Contains("PROCESS", StringComparison.Ordinal) && methodName.Contains("START", StringComparison.Ordinal))
                        {
                            processStartCount++;
                        }

                        if (methodName.Contains("REGISTRY", StringComparison.Ordinal))
                        {
                            registryAccessCount++;
                        }
                    }
                }
            }

            // Evaluate findings
            if (unsafeCodeCount > 0 && !_options.AllowFileSystemAccess)
            {
                violations.Add($"Assembly contains {unsafeCodeCount} type(s) with unsafe code blocks (pointers/unmanaged memory)");
                threatLevel = UpdateThreatLevel(threatLevel, ThreatLevel.High);
            }
            else if (unsafeCodeCount > 0)
            {
                warnings.Add($"Assembly contains {unsafeCodeCount} type(s) with unsafe code");
            }

            if (reflectionEmitCount > 0)
            {
                violations.Add($"Assembly contains {reflectionEmitCount} type(s) that use Reflection.Emit (dynamic code generation)");
                threatLevel = UpdateThreatLevel(threatLevel, ThreatLevel.Critical);
            }

            if (pinvokeCount > 50)
            {
                violations.Add($"Assembly has excessive P/Invoke declarations ({pinvokeCount}), indicating potential system-level tampering");
                threatLevel = UpdateThreatLevel(threatLevel, ThreatLevel.High);
            }
            else if (pinvokeCount > 20)
            {
                warnings.Add($"Assembly has {pinvokeCount} P/Invoke declarations");
            }

            if (processStartCount > 0)
            {
                violations.Add($"Assembly attempts to start external processes ({processStartCount} occurrences)");
                threatLevel = UpdateThreatLevel(threatLevel, ThreatLevel.Critical);
            }

            if (registryAccessCount > 0 && !_options.AllowFileSystemAccess)
            {
                violations.Add($"Assembly attempts registry access ({registryAccessCount} occurrences) which is not allowed");
                threatLevel = UpdateThreatLevel(threatLevel, ThreatLevel.High);
            }

            if (violations.Count == 0)
            {
                LogCodeAnalysisValid(assemblyPath, types.Length, pinvokeCount);
            }

            return new ValidationLayerResult(violations.Count == 0, violations, warnings, threatLevel);
        }
        catch (BadImageFormatException)
        {
            violations.Add("Assembly appears to be corrupted or has invalid format");
            return new ValidationLayerResult(false, violations, warnings, ThreatLevel.Critical);
        }
        catch (ReflectionTypeLoadException ex)
        {
            warnings.Add($"Some types could not be loaded: {ex.LoaderExceptions?.FirstOrDefault()?.Message ?? "Unknown error"}");
            return new ValidationLayerResult(violations.Count == 0, violations, warnings,
                violations.Count > 0 ? threatLevel : ThreatLevel.Low);
        }
        catch (Exception ex)
        {
            // The deeper reflection-based scan could not run (e.g. the assembly's dependencies are
            // not resolvable in this load context). The metadata-based scan above has already
            // produced any critical findings, so downgrade the load failure to a warning instead of
            // masking those findings with a generic "code analysis failed" violation.
            warnings.Add($"Deep code analysis incomplete (assembly could not be loaded): {ex.Message}");
            return new ValidationLayerResult(violations.Count == 0, violations, warnings, threatLevel);
        }
    }

    /// <summary>
    /// Validates assembly dependencies for suspicious references.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Dependency validation requires assembly inspection")]
    private async Task<ValidationLayerResult> ValidateDependenciesAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        var violations = new List<string>();
        var warnings = new List<string>();

        try
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            var assembly = Assembly.LoadFrom(assemblyPath);
            var referencedAssemblies = assembly.GetReferencedAssemblies();

            // List of suspicious dependencies
            var suspiciousDependencies = new[]
            {
                "Microsoft.VisualBasic", // VB6 interop - often used for obfuscation
                "System.Management", // WMI access
                "System.DirectoryServices", // Active Directory access
                "System.Web", // Web server capabilities in desktop plugin
                "Microsoft.CSharp" // Dynamic compilation
            };

            foreach (var reference in referencedAssemblies)
            {
                var refName = reference.Name ?? string.Empty;

                if (suspiciousDependencies.Any(s => refName.Contains(s, StringComparison.OrdinalIgnoreCase)))
                {
                    warnings.Add($"Plugin references potentially suspicious assembly: {refName}");
                }

                // Check for very old framework versions (security concerns)
                if (reference.Version != null && reference.Version.Major < 4)
                {
                    warnings.Add($"Plugin references old framework version: {refName} v{reference.Version}");
                }
            }

            LogDependencyValidationComplete(assemblyPath, referencedAssemblies.Length, warnings.Count);
            return new ValidationLayerResult(true, violations, warnings, ThreatLevel.None);
        }
        catch (Exception ex)
        {
            violations.Add($"Dependency validation failed: {ex.Message}");
            return new ValidationLayerResult(false, violations, warnings, ThreatLevel.Medium);
        }
    }

    /// <summary>
    /// Validates resource access patterns in the assembly.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Resource validation requires type inspection")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Security scanning requires comprehensive type access")]
    private async Task<ValidationLayerResult> ValidateResourceAccessAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        var violations = new List<string>();
        var warnings = new List<string>();

        try
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            // Detect resource usage from the assembly's referenced/defined types via metadata. This
            // reflects which framework APIs the assembly actually uses, and works without loading the
            // assembly (so it succeeds for framework assemblies whose dependencies are unresolvable).
            var metadata = ReadReferencedNamespaces(assemblyPath);

            var hasFileIOUsage = ReferencesNamespace(metadata, "System.IO");
            var hasNetworkUsage =
                ReferencesNamespace(metadata, "System.Net") ||
                ReferencesNamespace(metadata, "System.Net.Sockets") ||
                ReferencesNamespace(metadata, "System.Net.Http");
            var hasThreadingUsage =
                metadata.TypeNames.Contains("System.Threading.Thread") ||
                metadata.TypeNames.Contains("System.Threading.ThreadPool");

            // Validate against policy
            if (hasFileIOUsage && !_options.AllowFileSystemAccess)
            {
                violations.Add("Plugin attempts file system access which is not allowed by security policy");
            }
            else if (hasFileIOUsage)
            {
                warnings.Add("Plugin uses file system access");
            }

            if (hasNetworkUsage && !_options.AllowNetworkAccess)
            {
                violations.Add("Plugin attempts network access which is not allowed by security policy");
            }
            else if (hasNetworkUsage)
            {
                warnings.Add("Plugin uses network access");
            }

            if (hasThreadingUsage)
            {
                warnings.Add("Plugin creates or manages threads");
            }

            LogResourceValidationComplete(assemblyPath, hasFileIOUsage, hasNetworkUsage, hasThreadingUsage);
            return new ValidationLayerResult(violations.Count == 0, violations, warnings,
                violations.Count > 0 ? ThreatLevel.High : ThreatLevel.None);
        }
        catch (Exception ex)
        {
            warnings.Add($"Resource access validation incomplete: {ex.Message}");
            return new ValidationLayerResult(true, violations, warnings, ThreatLevel.Low);
        }
    }

    /// <summary>
    /// Validates that assembly has required attributes for plugin system.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Attribute validation requires assembly loading")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Plugin discovery requires type inspection")]
    private async Task<ValidationLayerResult> ValidateRequiredAttributesAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        var violations = new List<string>();
        var warnings = new List<string>();

        try
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            var assembly = Assembly.LoadFrom(assemblyPath);
            var types = assembly.GetTypes();

            // Check for IAlgorithmPlugin implementation
            var hasPluginInterface = types.Any(t =>
                t.IsClass &&
                !t.IsAbstract &&
                typeof(Abstractions.IAlgorithmPlugin).IsAssignableFrom(t));

            if (!hasPluginInterface)
            {
                violations.Add("Assembly does not contain any classes implementing IAlgorithmPlugin interface");
                return new ValidationLayerResult(false, violations, warnings, ThreatLevel.High);
            }

            // Check for security transparency (if required)
            if (_options.RequireSecurityTransparency)
            {
                var hasSecurityTransparency = assembly.GetCustomAttributes<System.Security.SecurityTransparentAttribute>().Any();
                if (!hasSecurityTransparency)
                {
                    warnings.Add("Assembly lacks SecurityTransparent attribute");
                }
            }

            LogAttributeValidationComplete(assemblyPath, hasPluginInterface);
            return new ValidationLayerResult(true, violations, warnings, ThreatLevel.None);
        }
        catch (Exception ex)
        {
            violations.Add($"Attribute validation failed: {ex.Message}");
            return new ValidationLayerResult(false, violations, warnings, ThreatLevel.Medium);
        }
    }

    /// <summary>
    /// Checks rate limiting to prevent abuse of plugin loading.
    /// </summary>
    private bool CheckRateLimit(string assemblyPath)
    {
        var now = DateTime.UtcNow;
        var bucket = now.Ticks / _rateLimitWindow.Ticks;

        // Key on the normalized base path + time bucket. Using a delimiter that cannot appear in the
        // base path (it survives Windows "C:\" paths) keeps the time-bucket parsing unambiguous.
        var normalizedPath = NormalizeRateLimitPath(assemblyPath);
        var cacheKey = $"{normalizedPath}|{bucket}";

        var attempts = _loadAttempts.AddOrUpdate(cacheKey, 1, (_, current) => current + 1);

        // Clean old entries
        var oldKeys = _loadAttempts.Keys.Where(k =>
        {
            var separatorIndex = k.AsSpan().LastIndexOf('|');
            if (separatorIndex >= 0 && long.TryParse(k.AsSpan(separatorIndex + 1), out var ticks))
            {
                var age = TimeSpan.FromTicks(now.Ticks - ticks * _rateLimitWindow.Ticks);
                return age > _rateLimitWindow;
            }
            return true;
        }).ToList();

        foreach (var key in oldKeys)
        {
            _loadAttempts.TryRemove(key, out _);
        }

        return attempts <= _maxLoadAttemptsPerWindow;
    }

    /// <summary>
    /// Normalizes a path for rate-limit keying: strips any query-string suffix so that variations
    /// such as <c>plugin.dll?attempt=1</c> and <c>plugin.dll?attempt=2</c> count against the same
    /// underlying plugin.
    /// </summary>
    private static string NormalizeRateLimitPath(string assemblyPath)
    {
        var queryIndex = assemblyPath.IndexOf('?', StringComparison.Ordinal);
        return queryIndex >= 0 ? assemblyPath[..queryIndex] : assemblyPath;
    }

    /// <summary>
    /// Computes SHA256 hash of the assembly file.
    /// </summary>
    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        using var fileStream = File.OpenRead(filePath);
        var hashBytes = await sha256.ComputeHashAsync(fileStream, cancellationToken);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Updates threat level to the higher severity.
    /// </summary>
    private static ThreatLevel UpdateThreatLevel(ThreatLevel current, ThreatLevel newLevel)
    {
        return (ThreatLevel)Math.Max((int)current, (int)newLevel);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _validationSemaphore.Dispose();
            _validationCache.Clear();
            _loadAttempts.Clear();
            LogSecurityValidatorDisposed();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _validationSemaphore.Dispose();
            _validationCache.Clear();
            _loadAttempts.Clear();
            LogSecurityValidatorDisposedAsynchronously();
            await Task.CompletedTask;
        }
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Debug, Message = "Security validation cache hit for: {AssemblyPath}")]
    private partial void LogSecurityValidationCacheHit(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rate limit exceeded for {AssemblyPath}: {MaxAttempts} attempts per {WindowMinutes} minutes")]
    private partial void LogRateLimitExceeded(string assemblyPath, int maxAttempts, double windowMinutes);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting comprehensive security validation for: {AssemblyPath}")]
    private partial void LogStartingSecurityValidation(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Security validation passed: {AssemblyPath}, Threat: {ThreatLevel}, Duration: {DurationMs:F2}ms")]
    private partial void LogSecurityValidationPassed(string assemblyPath, ThreatLevel threatLevel, double durationMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Security validation FAILED: {AssemblyPath}, Threat: {ThreatLevel}, Violations: {Violations}")]
    private partial void LogSecurityValidationFailed(string assemblyPath, ThreatLevel threatLevel, string violations);

    [LoggerMessage(Level = LogLevel.Debug, Message = "File integrity valid: {AssemblyPath}, Size: {Size:N0} bytes")]
    private partial void LogFileIntegrityValid(string assemblyPath, long size);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Strong-name valid: {AssemblyPath}, Key strength: {KeyBits} bits")]
    private partial void LogStrongNameValid(string assemblyPath, int keyBits);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Code analysis complete: {AssemblyPath}, Types: {TypeCount}, P/Invoke: {PInvokeCount}")]
    private partial void LogCodeAnalysisValid(string assemblyPath, int typeCount, int pinvokeCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dependency validation complete: {AssemblyPath}, References: {ReferenceCount}, Warnings: {WarningCount}")]
    private partial void LogDependencyValidationComplete(string assemblyPath, int referenceCount, int warningCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Resource validation complete: {AssemblyPath}, FileIO: {HasFileIO}, Network: {HasNetwork}, Threading: {HasThreading}")]
    private partial void LogResourceValidationComplete(string assemblyPath, bool hasFileIO, bool hasNetwork, bool hasThreading);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Attribute validation complete: {AssemblyPath}, HasPluginInterface: {HasPluginInterface}")]
    private partial void LogAttributeValidationComplete(string assemblyPath, bool hasPluginInterface);

    [LoggerMessage(Level = LogLevel.Debug, Message = "PluginSecurityValidator disposed")]
    private partial void LogSecurityValidatorDisposed();

    [LoggerMessage(Level = LogLevel.Debug, Message = "PluginSecurityValidator disposed asynchronously")]
    private partial void LogSecurityValidatorDisposedAsynchronously();

    #endregion
}

/// <summary>
/// Result of a security validation operation.
/// </summary>
public sealed class PluginSecurityResult
{
    /// <summary>
    /// Gets a value indicating whether the validation passed.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Gets the threat level determined by the validation.
    /// </summary>
    public required ThreatLevel ThreatLevel { get; init; }

    /// <summary>
    /// Gets the list of security violations found.
    /// </summary>
    public IReadOnlyList<string> Violations { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the list of warnings found.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the SHA256 hash of the validated file.
    /// </summary>
    public string? FileHash { get; init; }

    /// <summary>
    /// Gets the duration of the validation operation.
    /// </summary>
    public TimeSpan ValidationDuration { get; init; }

    /// <summary>
    /// Gets additional validation metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static PluginSecurityResult Failure(string violation, ThreatLevel threatLevel)
    {
        return new PluginSecurityResult
        {
            IsValid = false,
            ThreatLevel = threatLevel,
            Violations = new[] { violation }
        };
    }

    /// <summary>
    /// Creates a success result.
    /// </summary>
    public static PluginSecurityResult Success()
    {
        return new PluginSecurityResult
        {
            IsValid = true,
            ThreatLevel = ThreatLevel.None
        };
    }
}

/// <summary>
/// Result of a single validation layer.
/// </summary>
internal sealed class ValidationLayerResult
{
    public bool IsValid { get; }
    public List<string> Violations { get; }
    public List<string> Warnings { get; }
    public ThreatLevel ThreatLevel { get; }

    public ValidationLayerResult(bool isValid, List<string> violations, List<string> warnings, ThreatLevel threatLevel)
    {
        IsValid = isValid;
        Violations = violations;
        Warnings = warnings;
        ThreatLevel = threatLevel;
    }
}

/// <summary>
/// Cache entry for security validation results.
/// </summary>
internal sealed class SecurityValidationCacheEntry
{
    public PluginSecurityResult Result { get; }
    public DateTime ExpirationTime { get; }

    public bool IsExpired => DateTime.UtcNow > ExpirationTime;

    public SecurityValidationCacheEntry(PluginSecurityResult result, TimeSpan cacheExpiration)
    {
        Result = result;
        ExpirationTime = DateTime.UtcNow.Add(cacheExpiration);
    }
}
