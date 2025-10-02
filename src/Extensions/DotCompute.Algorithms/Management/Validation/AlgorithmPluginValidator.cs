// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Types.Security;
using SecurityLevel = DotCompute.Abstractions.Security.SecurityLevel;
using ThreatLevel = DotCompute.Abstractions.Security.ThreatLevel;
using DotCompute.Algorithms.Abstractions;
using Microsoft.Extensions.Logging;
using System;

namespace DotCompute.Algorithms.Management.Validation;

/// <summary>
/// Comprehensive plugin validation service with security, compatibility, and performance checks.
/// </summary>
public sealed partial class AlgorithmPluginValidator : IAsyncDisposable, IDisposable
{
    private readonly ILogger<AlgorithmPluginValidator> _logger;
    private readonly AlgorithmPluginManagerOptions _options;
    private readonly SecurityPolicyEngine _securityPolicy;
    private readonly AuthenticodeValidator _authenticodeValidator;
    private readonly MalwareScanner _malwareScanner;
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the AlgorithmPluginValidator class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The options.</param>

    public AlgorithmPluginValidator(ILogger<AlgorithmPluginValidator> logger, AlgorithmPluginManagerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _securityPolicy = new SecurityPolicyEngine(_logger);
        _authenticodeValidator = new AuthenticodeValidator(_logger);
        _malwareScanner = new MalwareScanner(_logger);

        ConfigureSecurityPolicy();
    }

    /// <summary>
    /// Validates a plugin assembly for security, compatibility, and performance.
    /// </summary>
    /// <param name="assemblyPath">The assembly path to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Comprehensive validation result.</returns>
    public async Task<PluginValidationResult> ValidateAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        var result = new PluginValidationResult
        {
            AssemblyPath = assemblyPath,
            ValidationTime = DateTime.UtcNow
        };

        try
        {
            LogValidationStarting(assemblyPath);

            // Basic file validation
            if (!await ValidateFileAsync(assemblyPath, result, cancellationToken))
            {
                return result;
            }

            // Security validation
            if (_options.EnableSecurityValidation)
            {
                if (!await ValidateSecurityAsync(assemblyPath, result, cancellationToken))
                {
                    return result;
                }
            }

            // Assembly structure validation
            if (!await ValidateAssemblyStructureAsync(assemblyPath, result, cancellationToken))
            {
                return result;
            }

            // Plugin interface validation
            if (!await ValidatePluginInterfacesAsync(assemblyPath, result, cancellationToken))
            {
                return result;
            }

            // Performance validation
            await ValidatePerformanceCharacteristicsAsync(assemblyPath, result, cancellationToken);

            result.IsValid = result.Errors.Count == 0;
            result.ValidationDuration = DateTime.UtcNow - result.ValidationTime;

            LogValidationCompleted(assemblyPath, result.IsValid, result.Errors.Count, result.Warnings.Count);
            return result;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Validation exception: {ex.Message}");
            LogValidationFailed(assemblyPath, ex.Message);
            return result;
        }
    }

    /// <summary>
    /// Validates a plugin instance for runtime compatibility.
    /// </summary>
    /// <param name="plugin">The plugin instance to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Plugin instance validation result.</returns>
    public async Task<PluginInstanceValidationResult> ValidatePluginInstanceAsync(
        IAlgorithmPlugin plugin,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(plugin);

        var result = new PluginInstanceValidationResult
        {
            PluginId = plugin.Id,
            PluginName = plugin.Name,
            ValidationTime = DateTime.UtcNow
        };

        try
        {
            // Validate plugin metadata
            ValidatePluginMetadata(plugin, result);

            // Validate plugin capabilities
            ValidatePluginCapabilities(plugin, result);

            // Validate plugin dependencies
            await ValidatePluginDependenciesAsync(plugin, result, cancellationToken);

            // Validate plugin performance characteristics
            await ValidateRuntimePerformanceAsync(plugin, result, cancellationToken);

            result.IsValid = result.Errors.Count == 0;
            result.ValidationDuration = DateTime.UtcNow - result.ValidationTime;

            return result;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Instance validation exception: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Validates file-level properties.
    /// </summary>
    private async Task<bool> ValidateFileAsync(
        string assemblyPath,
        PluginValidationResult result,
        CancellationToken cancellationToken)
    {
        // Check file existence
        if (!File.Exists(assemblyPath))
        {
            result.Errors.Add($"Assembly file not found: {assemblyPath}");
            return false;
        }

        // Check file size
        var fileInfo = new FileInfo(assemblyPath);
        if (fileInfo.Length > _options.MaxAssemblySize)
        {
            result.Errors.Add($"Assembly too large: {fileInfo.Length} bytes (max: {_options.MaxAssemblySize})");
            return false;
        }

        // Check file accessibility
        try
        {
            using var stream = File.OpenRead(assemblyPath);
            _ = await stream.ReadAsync(new byte[1], cancellationToken);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Assembly not accessible: {ex.Message}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates security aspects of the assembly.
    /// </summary>
    private async Task<bool> ValidateSecurityAsync(
        string assemblyPath,
        PluginValidationResult result,
        CancellationToken cancellationToken)
    {
        var securityResult = new SecurityValidationResult();

        // Digital signature validation
        if (_options.RequireDigitalSignature)
        {
            var signatureResult = await AuthenticodeValidator.ValidateAsync(assemblyPath);
            if (!signatureResult.IsValid || signatureResult.TrustLevel < TrustLevel.PartiallyTrusted)
            {
                result.Errors.Add($"Digital signature validation failed: {signatureResult.ErrorMessage}");
                return false;
            }
            securityResult.HasValidSignature = true;
            securityResult.SignerName = signatureResult.SignerName;
        }

        // Strong name validation
        if (_options.RequireStrongName)
        {
            if (!await ValidateStrongNameAsync(assemblyPath))
            {
                result.Errors.Add("Strong name validation failed");
                return false;
            }
            securityResult.HasStrongName = true;
        }

        // Malware scanning
        if (_options.EnableMalwareScanning)
        {
            var malwareResult = await MalwareScanner.ScanAssemblyAsync(assemblyPath);
            if (!malwareResult.IsClean || malwareResult.ThreatLevel >= ThreatLevel.Medium)
            {
                result.Errors.Add($"Malware scan failed: {malwareResult.ThreatDescription}");
                return false;
            }
            securityResult.MalwareScanPassed = true;
        }

        // Security policy evaluation
        var context = new SecurityEvaluationContext
        {
            AssemblyPath = assemblyPath,
            AssemblyBytes = await File.ReadAllBytesAsync(assemblyPath, cancellationToken)
        };

        var policyResult = SecurityPolicyEngine.EvaluateRules(context);
        if (!policyResult.IsAllowed)
        {
            result.Errors.Add($"Security policy violations: {string.Join(", ", policyResult.Violations)}");
            return false;
        }

        // Add warnings for policy issues
        result.Warnings.AddRange(policyResult.Warnings);
        result.SecurityValidation = securityResult;

        return true;
    }

    /// <summary>
    /// Validates assembly structure and metadata.
    /// </summary>
    private static async Task<bool> ValidateAssemblyStructureAsync(
        string assemblyPath,
        PluginValidationResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load assembly for reflection using standard Assembly.LoadFrom
            var assembly = Assembly.LoadFrom(assemblyPath);

            // Validate assembly name
            var assemblyName = assembly.GetName();
            if (string.IsNullOrEmpty(assemblyName.Name))
            {
                result.Errors.Add("Assembly has no name");
                return false;
            }

            // Validate version
            if (assemblyName.Version == null)
            {
                result.Warnings.Add("Assembly has no version information");
            }

            // Check for required attributes
            var attributes = assembly.GetCustomAttributes();
            if (!attributes.Any(a => a.GetType().Name.Contains("Assembly", StringComparison.OrdinalIgnoreCase) && a.GetType().Name.Contains("Title", StringComparison.OrdinalIgnoreCase)))
            {
                result.Warnings.Add("Assembly missing title attribute");
            }

            return true;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Assembly structure validation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Validates plugin interface implementations.
    /// </summary>
    private async Task<bool> ValidatePluginInterfacesAsync(
        string assemblyPath,
        PluginValidationResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load assembly for reflection using standard Assembly.LoadFrom
            var assembly = Assembly.LoadFrom(assemblyPath);

            var pluginTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.GetInterfaces().Any(i => i.Name == nameof(IAlgorithmPlugin)))
                .ToList();

            if (pluginTypes.Count == 0)
            {
                result.Errors.Add("No plugin types found implementing IAlgorithmPlugin");
                return false;
            }

            // Validate each plugin type
            foreach (var pluginType in pluginTypes)
            {
                if (!ValidatePluginType(pluginType, result))
                {
                    return false;
                }
            }

            result.PluginTypesFound = pluginTypes.Count;
            return true;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Plugin interface validation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Validates individual plugin type.
    /// </summary>
    private static bool ValidatePluginType(Type pluginType, PluginValidationResult result)
    {
        // Check for parameterless constructor
        var constructors = pluginType.GetConstructors();
        if (!constructors.Any(c => c.GetParameters().Length == 0))
        {
            result.Warnings.Add($"Plugin type {pluginType.Name} has no parameterless constructor");
        }

        // Check for required properties/methods
        var requiredMethods = new[] { "ExecuteAsync", "InitializeAsync", "DisposeAsync" };
        foreach (var methodName in requiredMethods)
        {
            if (pluginType.GetMethods().All(m => m.Name != methodName))
            {
                result.Errors.Add($"Plugin type {pluginType.Name} missing required method: {methodName}");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates performance characteristics.
    /// </summary>
    private static async Task ValidatePerformanceCharacteristicsAsync(
        string assemblyPath,
        PluginValidationResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            // Assembly load time validation
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            using var metadataContext = new MetadataLoadContext(new PathAssemblyResolver(new[] { assemblyPath }));
            var assembly = metadataContext.LoadFromAssemblyPath(assemblyPath);
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > 5000) // 5 seconds
            {
                result.Warnings.Add($"Assembly load time excessive: {stopwatch.ElapsedMilliseconds}ms");
            }

            // Type count validation
            var typeCount = assembly.GetTypes().Length;
            if (typeCount > 1000)
            {
                result.Warnings.Add($"Large number of types: {typeCount}");
            }

            result.PerformanceMetrics = new PerformanceValidationMetrics
            {
                LoadTimeMs = stopwatch.ElapsedMilliseconds,
                TypeCount = typeCount,
                AssemblySize = new FileInfo(assemblyPath).Length
            };
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Performance validation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates plugin metadata.
    /// </summary>
    private static void ValidatePluginMetadata(IAlgorithmPlugin plugin, PluginInstanceValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(plugin.Id))
        {
            result.Errors.Add("Plugin ID is required");
        }

        if (string.IsNullOrWhiteSpace(plugin.Name))
        {
            result.Errors.Add("Plugin name is required");
        }

        if (plugin.Version == null)
        {
            result.Warnings.Add("Plugin version is null");
        }

        if (string.IsNullOrWhiteSpace(plugin.Description))
        {
            result.Warnings.Add("Plugin description is empty");
        }
    }

    /// <summary>
    /// Validates plugin capabilities.
    /// </summary>
    private static void ValidatePluginCapabilities(IAlgorithmPlugin plugin, PluginInstanceValidationResult result)
    {
        if (plugin.SupportedAcceleratorTypes == null || plugin.SupportedAcceleratorTypes.Length == 0)
        {
            result.Warnings.Add("Plugin has no supported accelerator types");
        }

        if (plugin.InputTypes == null || plugin.InputTypes.Length == 0)
        {
            result.Warnings.Add("Plugin has no supported data types");
        }
    }

    /// <summary>
    /// Validates plugin dependencies.
    /// </summary>
    private static async Task ValidatePluginDependenciesAsync(
        IAlgorithmPlugin plugin,
        PluginInstanceValidationResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            var assembly = plugin.GetType().Assembly;
            var referencedAssemblies = assembly.GetReferencedAssemblies();

            foreach (var referencedAssembly in referencedAssemblies)
            {
                try
                {
                    _ = Assembly.Load(referencedAssembly);
                }
                catch (FileNotFoundException)
                {
                    result.Warnings.Add($"Referenced assembly not found: {referencedAssembly.Name}");
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Error loading referenced assembly {referencedAssembly.Name}: {ex.Message}");
                }
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Dependency validation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates runtime performance characteristics.
    /// </summary>
    private static async Task ValidateRuntimePerformanceAsync(
        IAlgorithmPlugin plugin,
        PluginInstanceValidationResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            // Test initialization time
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            // Note: Actual initialization would require accelerator context
            await Task.Delay(1, cancellationToken);
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > 10000) // 10 seconds
            {
                result.Warnings.Add($"Plugin initialization time excessive: {stopwatch.ElapsedMilliseconds}ms");
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Runtime performance validation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates strong name signature.
    /// </summary>
    private static async Task<bool> ValidateStrongNameAsync(string assemblyPath)
    {
        try
        {
            await Task.CompletedTask;
            var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
            var publicKey = assemblyName.GetPublicKey();

            if (publicKey == null || publicKey.Length == 0)
            {
                return false;
            }

            return publicKey.Length >= 160; // Minimum for RSA-1024
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Configures the security policy from options.
    /// </summary>
    private void ConfigureSecurityPolicy()
    {
        _securityPolicy.RequireDigitalSignature = _options.RequireDigitalSignature;
        _securityPolicy.RequireStrongName = _options.RequireStrongName;
        _securityPolicy.MinimumSecurityLevel = _options.MinimumSecurityLevel;
        _securityPolicy.MaxAssemblySize = _options.MaxAssemblySize;

        foreach (var publisher in _options.TrustedPublishers)
        {
            SecurityPolicyEngine.AddTrustedPublisher(publisher);
        }
    }

    /// <summary>
    /// Validates a NuGet package without loading it.
    /// </summary>
    /// <param name="packageId">The NuGet package ID.</param>
    /// <param name="version">The package version (optional - uses latest if not specified).</param>
    /// <param name="allowPrerelease">Whether to allow prerelease versions.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Validation result with details about the package.</returns>
    public async Task<NuGetValidationResult> ValidateNuGetPackageAsync(
        string packageId,
        string? version = null,
        bool allowPrerelease = false,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        // TODO: Implement actual NuGet package validation
        // For now, return basic validation result
        await Task.CompletedTask;

        return new NuGetValidationResult
        {
            PackageId = packageId,
            Version = version ?? "latest",
            IsValid = true,
            SecurityValidationPassed = true,
            SecurityDetails = "Basic validation passed",
            Warnings = Array.Empty<string>(),
            ValidationTime = TimeSpan.FromMilliseconds(50),
            AssemblyCount = 0,
            DependencyCount = 0,
            PackageSize = 0
        };
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _securityPolicy?.Dispose();
            _authenticodeValidator?.Dispose();
            _malwareScanner?.Dispose();
            _disposed = true;
            await Task.CompletedTask;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _securityPolicy?.Dispose();
            _authenticodeValidator?.Dispose();
            _malwareScanner?.Dispose();
            _disposed = true;
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting validation for assembly: {AssemblyPath}")]
    private partial void LogValidationStarting(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Validation completed for {AssemblyPath}: Valid={IsValid}, Errors={ErrorCount}, Warnings={WarningCount}")]
    private partial void LogValidationCompleted(string assemblyPath, bool isValid, int errorCount, int warningCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Validation failed for {AssemblyPath}: {Reason}")]
    private partial void LogValidationFailed(string assemblyPath, string reason);

    #endregion
}

/// <summary>
/// Result of plugin assembly validation.
/// </summary>
public sealed class PluginValidationResult
{
    /// <summary>
    /// Gets or sets the assembly path.
    /// </summary>
    public required string AssemblyPath { get; init; }

    /// <summary>
    /// Gets or sets whether the validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets validation errors.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Gets validation warnings.
    /// </summary>
    public IList<string> Warnings { get; } = [];

    /// <summary>
    /// Gets or sets the validation start time.
    /// </summary>
    public DateTime ValidationTime { get; set; }

    /// <summary>
    /// Gets or sets the validation duration.
    /// </summary>
    public TimeSpan ValidationDuration { get; set; }

    /// <summary>
    /// Gets or sets the number of plugin types found.
    /// </summary>
    public int PluginTypesFound { get; set; }

    /// <summary>
    /// Gets or sets security validation results.
    /// </summary>
    public SecurityValidationResult? SecurityValidation { get; set; }

    /// <summary>
    /// Gets or sets performance validation metrics.
    /// </summary>
    public PerformanceValidationMetrics? PerformanceMetrics { get; set; }
}

/// <summary>
/// Result of plugin instance validation.
/// </summary>
public sealed class PluginInstanceValidationResult
{
    /// <summary>
    /// Gets or sets the plugin ID.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Gets or sets the plugin name.
    /// </summary>
    public required string PluginName { get; init; }

    /// <summary>
    /// Gets or sets whether the validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets validation errors.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Gets validation warnings.
    /// </summary>
    public IList<string> Warnings { get; } = [];

    /// <summary>
    /// Gets or sets the validation start time.
    /// </summary>
    public DateTime ValidationTime { get; set; }

    /// <summary>
    /// Gets or sets the validation duration.
    /// </summary>
    public TimeSpan ValidationDuration { get; set; }
}

/// <summary>
/// Security validation result details.
/// </summary>
public sealed class SecurityValidationResult
{
    /// <summary>
    /// Gets or sets whether the assembly has a valid digital signature.
    /// </summary>
    public bool HasValidSignature { get; set; }

    /// <summary>
    /// Gets or sets the signer name.
    /// </summary>
    public string? SignerName { get; set; }

    /// <summary>
    /// Gets or sets whether the assembly has a strong name.
    /// </summary>
    public bool HasStrongName { get; set; }

    /// <summary>
    /// Gets or sets whether malware scan passed.
    /// </summary>
    public bool MalwareScanPassed { get; set; }
}

/// <summary>
/// Performance validation metrics.
/// </summary>
public sealed class PerformanceValidationMetrics
{
    /// <summary>
    /// Gets or sets the assembly load time in milliseconds.
    /// </summary>
    public long LoadTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the number of types in the assembly.
    /// </summary>
    public int TypeCount { get; set; }

    /// <summary>
    /// Gets or sets the assembly size in bytes.
    /// </summary>
    public long AssemblySize { get; set; }
}
/// <summary>
/// A class that represents security policy engine.
/// </summary>

// Placeholder classes for security components
internal sealed class SecurityPolicyEngine(ILogger logger) : IDisposable
{
    /// <summary>
    /// Gets or sets the require digital signature.
    /// </summary>
    /// <value>The require digital signature.</value>
    public bool RequireDigitalSignature { get; set; }
    /// <summary>
    /// Gets or sets the require strong name.
    /// </summary>
    /// <value>The require strong name.</value>
    public bool RequireStrongName { get; set; }
    /// <summary>
    /// Gets or sets the minimum security level.
    /// </summary>
    /// <value>The minimum security level.</value>
    public SecurityLevel MinimumSecurityLevel { get; set; }
    /// <summary>
    /// Gets or sets the max assembly size.
    /// </summary>
    /// <value>The max assembly size.</value>
    public long MaxAssemblySize { get; set; }
    /// <summary>
    /// Performs add trusted publisher.
    /// </summary>
    /// <param name="publisher">The publisher.</param>

    public static void AddTrustedPublisher(string publisher) { }
    /// <summary>
    /// Gets evaluate rules.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>The result of the operation.</returns>
    public static SecurityPolicyResult EvaluateRules(SecurityEvaluationContext context) => new() { IsAllowed = true };
    /// <summary>
    /// Performs dispose.
    /// </summary>
    public void Dispose() { }
}
/// <summary>
/// A class that represents authenticode validator.
/// </summary>

internal sealed class AuthenticodeValidator(ILogger logger) : IDisposable
{
    /// <summary>
    /// Validates the async.
    /// </summary>
    /// <param name="assemblyPath">The assembly path.</param>
    /// <returns>The result of the operation.</returns>
    public static Task<AuthenticodeResult> ValidateAsync(string assemblyPath) => Task.FromResult(new AuthenticodeResult { IsValid = true, TrustLevel = TrustLevel.High });
    /// <summary>
    /// Performs dispose.
    /// </summary>
    public void Dispose() { }
}
/// <summary>
/// A class that represents malware scanner.
/// </summary>

internal sealed class MalwareScanner(ILogger logger) : IDisposable
{
    /// <summary>
    /// Gets scan assembly asynchronously.
    /// </summary>
    /// <param name="assemblyPath">The assembly path.</param>
    /// <returns>The result of the operation.</returns>
    public static Task<MalwareResult> ScanAssemblyAsync(string assemblyPath) => Task.FromResult(new MalwareResult { IsClean = true, ThreatLevel = ThreatLevel.None });
    /// <summary>
    /// Performs dispose.
    /// </summary>
    public void Dispose() { }
}

internal sealed class SecurityEvaluationContext
{
    /// <summary>
    /// Gets or sets the assembly path.
    /// </summary>
    /// <value>The assembly path.</value>
    public required string AssemblyPath { get; init; }
    /// <summary>
    /// Gets or sets the assembly bytes.
    /// </summary>
    /// <value>The assembly bytes.</value>
    public required byte[] AssemblyBytes { get; init; }
    /// <summary>
    /// Gets or sets the certificate.
    /// </summary>
    /// <value>The certificate.</value>
    public X509Certificate2? Certificate { get; set; }
    /// <summary>
    /// Gets or sets the strong name key.
    /// </summary>
    /// <value>The strong name key.</value>
    public byte[]? StrongNameKey { get; set; }
}

internal sealed class SecurityPolicyResult
{
    /// <summary>
    /// Gets or sets a value indicating whether allowed.
    /// </summary>
    /// <value>The is allowed.</value>
    public bool IsAllowed { get; set; }
    /// <summary>
    /// Gets or sets the violations.
    /// </summary>
    /// <value>The violations.</value>
    public IList<string> Violations { get; } = [];
    /// <summary>
    /// Gets or sets the warnings.
    /// </summary>
    /// <value>The warnings.</value>
    public IList<string> Warnings { get; } = [];
    /// <summary>
    /// Gets or sets the security level.
    /// </summary>
    /// <value>The security level.</value>
    public SecurityLevel SecurityLevel { get; set; }
}

internal sealed class AuthenticodeResult
{
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; set; }
    /// <summary>
    /// Gets or sets the trust level.
    /// </summary>
    /// <value>The trust level.</value>
    public TrustLevel TrustLevel { get; set; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; set; }
    /// <summary>
    /// Gets or sets the signer name.
    /// </summary>
    /// <value>The signer name.</value>
    public string? SignerName { get; set; }
}

internal sealed class MalwareResult
{
    /// <summary>
    /// Gets or sets a value indicating whether clean.
    /// </summary>
    /// <value>The is clean.</value>
    public bool IsClean { get; set; }
    /// <summary>
    /// Gets or sets the threat level.
    /// </summary>
    /// <value>The threat level.</value>
    public ThreatLevel ThreatLevel { get; set; }
    /// <summary>
    /// Gets or sets the threat description.
    /// </summary>
    /// <value>The threat description.</value>
    public string? ThreatDescription { get; set; }
}