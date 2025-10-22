#nullable enable

// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using DotCompute.Abstractions.Security;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Types.Security;
using Microsoft.Extensions.Logging;
using SecurityLevel = DotCompute.Algorithms.Types.Security.SecurityLevel;

namespace DotCompute.Algorithms.Management.Core;

/// <summary>
/// Provides comprehensive security validation for algorithm plugins.
/// Handles digital signatures, strong names, malware scanning, and security policy evaluation.
/// </summary>
public sealed partial class AlgorithmPluginValidator : IDisposable
{
    private readonly ILogger<AlgorithmPluginValidator> _logger;
    private readonly AlgorithmPluginManagerOptions _options;
    private readonly AuthenticodeValidator _authenticodeValidator;
    private readonly MalwareScanner _malwareScanner;
    private readonly SecurityPolicy _securityPolicy;
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the AlgorithmPluginValidator class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The options.</param>

    public AlgorithmPluginValidator(
        ILogger<AlgorithmPluginValidator> logger,
        AlgorithmPluginManagerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Initialize security components
        _authenticodeValidator = new AuthenticodeValidator(_logger);
        _malwareScanner = new MalwareScanner(_logger, options);
        _securityPolicy = new SecurityPolicy(_logger);

        ConfigureSecurityPolicy();
    }

    /// <summary>
    /// Validates assembly security using comprehensive security policies and scanning.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly to validate.</param>
    /// <returns>True if the assembly passes all security validations; otherwise, false.</returns>
    public async Task<bool> ValidateAssemblySecurityAsync(string assemblyPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        if (!_options.EnableSecurityValidation)
        {
            return true;
        }

        try
        {
            LogSecurityValidationStarting(assemblyPath);

            // Step 1: Digital signature validation (Authenticode)
            if (_options.RequireDigitalSignature)
            {
                var signatureResult = await _authenticodeValidator.ValidateAsync(assemblyPath);
                if (!signatureResult.IsValid || signatureResult.TrustLevel < TrustLevel.Medium)
                {
                    LogDigitalSignatureValidationFailed(assemblyPath, signatureResult.ErrorMessage ?? "Unknown error");
                    return false;
                }
                LogDigitalSignatureValidationPassed(assemblyPath, signatureResult.SignerName ?? "Unknown");
            }

            // Step 2: Strong name validation
            if (_options.RequireStrongName)
            {
                if (!await ValidateStrongNameAsync(assemblyPath))
                {
                    LogStrongNameValidationFailed(assemblyPath);
                    return false;
                }
                LogStrongNameValidationPassed(assemblyPath);
            }

            // Step 3: Assembly size validation
            var fileInfo = new FileInfo(assemblyPath);
            if (fileInfo.Length > _options.MaxAssemblySize)
            {
                LogAssemblyTooLarge(assemblyPath, fileInfo.Length, _options.MaxAssemblySize);
                return false;
            }

            // Step 4: Malware scanning
            if (_options.EnableMalwareScanning)
            {
                var malwareResult = await _malwareScanner.ScanAssemblyAsync(assemblyPath);
                if (!malwareResult.IsClean || malwareResult.ThreatLevel >= ThreatLevel.Medium)
                {
                    LogMalwareScanningFailed(assemblyPath, malwareResult.ThreatDescription ?? "Unknown threat", malwareResult.ThreatLevel);
                    return false;
                }
                LogMalwareScanningPassed(assemblyPath);
            }

            // Step 5: Security policy evaluation
            var context = new SecurityEvaluationContext
            {
                AssemblyPath = assemblyPath,
                AssemblyBytes = await File.ReadAllBytesAsync(assemblyPath)
            };

            // Add certificate information if available
            if (_options.RequireDigitalSignature)
            {
                context.Certificate = ExtractCertificate(assemblyPath);
            }

            // Add strong name key if available
            if (_options.RequireStrongName)
            {
                context.StrongNameKey = ExtractStrongNameKey(assemblyPath);
            }

            var policyResult = _securityPolicy.EvaluateRules(context);
            if (!policyResult.IsAllowed)
            {
                LogSecurityPolicyViolation(assemblyPath, string.Join(", ", policyResult.Violations));
                return false;
            }

            // Log any warnings
            if (policyResult.Warnings.Count > 0)
            {
                LogSecurityPolicyWarnings(assemblyPath, string.Join(", ", policyResult.Warnings));
            }

            LogSecurityValidationPassed(assemblyPath, policyResult.SecurityLevel);
            return true;
        }
        catch (Exception ex)
        {
            LogSecurityValidationError(assemblyPath, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Validates the framework version compatibility of an assembly.
    /// </summary>
    /// <param name="requiredVersion">The required framework version.</param>
    /// <returns>True if compatible; otherwise, false.</returns>
    public bool IsVersionCompatible(string? requiredVersion)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrEmpty(requiredVersion))
        {
            return true;
        }

        try
        {
            var required = Version.Parse(requiredVersion);
            var current = Environment.Version;
            return current >= required;
        }
        catch
        {
            return true; // If we can't parse, assume compatible
        }
    }

    /// <summary>
    /// Determines if an assembly is a system assembly that should be ignored.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly.</param>
    /// <returns>True if it's a system assembly; otherwise, false.</returns>
    public bool IsSystemAssembly(string assemblyPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        var fileName = Path.GetFileName(assemblyPath).ToUpperInvariant();
        return fileName.StartsWith("system.", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("microsoft.", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates the digital signature of a plugin assembly.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly.</param>
    /// <returns>The validation result.</returns>
    public async Task<AuthenticodeValidationResult> ValidateDigitalSignatureAsync(string assemblyPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _authenticodeValidator.ValidateAsync(assemblyPath);
    }

    /// <summary>
    /// Scans an assembly for malware.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly.</param>
    /// <returns>The scan result.</returns>
    public async Task<MalwareScanResult> ScanForMalwareAsync(string assemblyPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _malwareScanner.ScanAssemblyAsync(assemblyPath);
    }

    /// <summary>
    /// Validates strong name signature of an assembly.
    /// </summary>
    private static async Task<bool> ValidateStrongNameAsync(string assemblyPath)
    {
        try
        {
            await Task.CompletedTask; // Make it async for consistency
            var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
            var publicKey = assemblyName.GetPublicKey();

            // Check if assembly has a public key (strong name)
            if (publicKey == null || publicKey.Length == 0)
            {
                return false;
            }

            // Basic validation: ensure key is of reasonable size
            if (publicKey.Length < 160) // Minimum for RSA-1024
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
    /// Extracts certificate information from an assembly.
    /// </summary>
    private X509Certificate2? ExtractCertificate(string assemblyPath)
    {
        try
        {
            var certInfo = _authenticodeValidator.ExtractCertificateInfo(assemblyPath);
            if (certInfo?.Subject != null)
            {
                // Use X509CertificateLoader instead of obsolete CreateFromSignedFile
                var cert = X509CertificateLoader.LoadCertificateFromFile(assemblyPath);
                return cert != null ? new X509Certificate2(cert) : null;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts strong name key from an assembly.
    /// </summary>
    private static byte[]? ExtractStrongNameKey(string assemblyPath)
    {
        try
        {
            var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
            return assemblyName.GetPublicKey();
        }
        catch
        {
            return null;
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
        _securityPolicy.EnableMalwareScanning = _options.EnableMalwareScanning;
        _securityPolicy.EnableMetadataAnalysis = _options.EnableMetadataAnalysis;

        // Add trusted publishers
        foreach (var publisher in _options.TrustedPublishers)
        {
            _securityPolicy.AddTrustedPublisher(publisher);
        }

        // Configure directory policies
        foreach (var directory in _options.AllowedPluginDirectories)
        {
            _securityPolicy.DirectoryPolicies[directory] = SecurityLevel.High;
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
            _authenticodeValidator?.Dispose();
            _malwareScanner?.Dispose();
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting security validation for: {AssemblyPath}")]
    partial void LogSecurityValidationStarting(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Security validation passed for: {AssemblyPath}, Security Level: {SecurityLevel}")]
    partial void LogSecurityValidationPassed(string assemblyPath, SecurityLevel securityLevel);

    [LoggerMessage(Level = LogLevel.Error, Message = "Digital signature validation failed for: {AssemblyPath}, Reason: {Reason}")]
    partial void LogDigitalSignatureValidationFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Digital signature validation passed for: {AssemblyPath}, Signer: {Signer}")]
    partial void LogDigitalSignatureValidationPassed(string assemblyPath, string signer);

    [LoggerMessage(Level = LogLevel.Error, Message = "Strong name validation failed for: {AssemblyPath}")]
    partial void LogStrongNameValidationFailed(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Strong name validation passed for: {AssemblyPath}")]
    partial void LogStrongNameValidationPassed(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Assembly too large: {AssemblyPath}, size: {ActualSize}, max: {MaxSize}")]
    partial void LogAssemblyTooLarge(string assemblyPath, long actualSize, long maxSize);

    [LoggerMessage(Level = LogLevel.Error, Message = "Malware scanning failed for: {AssemblyPath}, Threat: {ThreatDescription}, Level: {ThreatLevel}")]
    partial void LogMalwareScanningFailed(string assemblyPath, string threatDescription, ThreatLevel threatLevel);

    [LoggerMessage(Level = LogLevel.Information, Message = "Malware scanning passed for: {AssemblyPath}")]
    partial void LogMalwareScanningPassed(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Security policy violation for: {AssemblyPath}, Violations: {Violations}")]
    partial void LogSecurityPolicyViolation(string assemblyPath, string violations);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Security policy warnings for: {AssemblyPath}, Warnings: {Warnings}")]
    partial void LogSecurityPolicyWarnings(string assemblyPath, string warnings);

    [LoggerMessage(Level = LogLevel.Error, Message = "Security validation error for {AssemblyPath}: {Reason}")]
    partial void LogSecurityValidationError(string assemblyPath, string reason);

    #endregion
}