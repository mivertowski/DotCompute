// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Abstractions.Security;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Validation;

/// <summary>
/// Service responsible for validating assembly security policies.
/// </summary>
public sealed partial class SecurityValidator : ISecurityValidator
{
    private readonly SecurityPolicy _securityPolicy;
    private readonly AuthenticodeValidator _authenticodeValidator;
    private readonly MalwareScanningService _malwareScanner;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityValidator"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">Configuration options.</param>
    public SecurityValidator(ILogger<SecurityValidator> logger, AlgorithmPluginManagerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Initialize security components
        var securityLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<SecurityPolicy>.Instance;
        _securityPolicy = new SecurityPolicy(securityLogger);

        var authenticodeLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthenticodeValidator>.Instance;
        _authenticodeValidator = new AuthenticodeValidator(authenticodeLogger);

        var malwareLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MalwareScanningService>.Instance;
        _malwareScanner = new MalwareScanningService(malwareLogger, new MalwareScanningOptions
        {
            EnableWindowsDefender = _options.EnableWindowsDefenderScanning,
            MaxConcurrentScans = 2,
            ScanTimeout = TimeSpan.FromMinutes(1)
        });

        ConfigureSecurityPolicy();
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateAssemblySecurityAsync(string assemblyPath)
    {
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
                var signatureResult = await AuthenticodeValidator.ValidateAsync(assemblyPath);
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

            // Step 3: Malware scanning
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

            // Step 4: Security policy evaluation
            var context = new SecurityEvaluationContext
            {
                AssemblyPath = assemblyPath,
                AssemblyBytes = await File.ReadAllBytesAsync(assemblyPath)
            };

            // Add certificate information if available
            if (_options.RequireDigitalSignature)
            {
                if (_authenticodeValidator.ExtractCertificateInfo(assemblyPath)?.Subject != null)
                {
                    // Use X509CertificateLoader instead of obsolete CreateFromSignedFile
                    var cert = X509CertificateLoader.LoadCertificateFromFile(assemblyPath);
                    context.Certificate = cert != null ? new X509Certificate2(cert) : null;
                }
                else
                {
                    context.Certificate = null;
                }
            }

            // Add strong name key if available
            if (_options.RequireStrongName)
            {
                try
                {
                    var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
                    context.StrongNameKey = assemblyName.GetPublicKey();
                }
                catch
                {
                    // Strong name validation will catch this
                }
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

    /// <inheritdoc/>
    public async Task<bool> ValidateStrongNameAsync(string assemblyPath)
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

    /// <inheritdoc/>
    public bool IsVersionCompatible(Version? requiredVersion)
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
    /// Disposes of managed resources.
    /// </summary>
    public void Dispose()
    {
        _authenticodeValidator?.Dispose();
        _malwareScanner?.Dispose();
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting security validation for: {AssemblyPath}")]
    private static partial void LogSecurityValidationStarting(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Security validation passed for: {AssemblyPath}, Security Level: {SecurityLevel}")]
    private static partial void LogSecurityValidationPassed(string assemblyPath, SecurityLevel securityLevel);

    [LoggerMessage(Level = LogLevel.Error, Message = "Digital signature validation failed for: {AssemblyPath}, Reason: {Reason}")]
    private static partial void LogDigitalSignatureValidationFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Digital signature validation passed for: {AssemblyPath}, Signer: {Signer}")]
    private static partial void LogDigitalSignatureValidationPassed(string assemblyPath, string signer);

    [LoggerMessage(Level = LogLevel.Error, Message = "Strong name validation failed for: {AssemblyPath}")]
    private static partial void LogStrongNameValidationFailed(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Strong name validation passed for: {AssemblyPath}")]
    private static partial void LogStrongNameValidationPassed(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Malware scanning failed for: {AssemblyPath}, Threat: {ThreatDescription}, Level: {ThreatLevel}")]
    private static partial void LogMalwareScanningFailed(string assemblyPath, string threatDescription, ThreatLevel threatLevel);

    [LoggerMessage(Level = LogLevel.Information, Message = "Malware scanning passed for: {AssemblyPath}")]
    private static partial void LogMalwareScanningPassed(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Security policy violation for: {AssemblyPath}, Violations: {Violations}")]
    private static partial void LogSecurityPolicyViolation(string assemblyPath, string violations);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Security policy warnings for: {AssemblyPath}, Warnings: {Warnings}")]
    private static partial void LogSecurityPolicyWarnings(string assemblyPath, string warnings);

    [LoggerMessage(Level = LogLevel.Error, Message = "Security validation error for {AssemblyPath}: {Reason}")]
    private static partial void LogSecurityValidationError(string assemblyPath, string reason);

    #endregion
}