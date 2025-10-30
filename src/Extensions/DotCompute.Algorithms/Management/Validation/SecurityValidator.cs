
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using DotCompute.Abstractions.Security;
using DotCompute.Algorithms.Management.Configuration;
using Microsoft.Extensions.Logging;
using MalwareScanningService = DotCompute.Algorithms.Security.MalwareScanningService;
using SecurityPolicy = DotCompute.Algorithms.Security.SecurityPolicy;

namespace DotCompute.Algorithms.Management.Validation;

/// <summary>
/// Service responsible for validating assembly security policies.
/// </summary>
public sealed partial class SecurityValidator : ISecurityValidator, IDisposable
{
    private readonly ILogger<SecurityValidator> _logger;
    private readonly AlgorithmPluginManagerOptions _options;
    private readonly SecurityPolicy _securityPolicy;
    private readonly DotCompute.Algorithms.Security.AuthenticodeValidator _authenticodeValidator;
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

        var authenticodeLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<DotCompute.Algorithms.Security.AuthenticodeValidator>.Instance;
        _authenticodeValidator = new DotCompute.Algorithms.Security.AuthenticodeValidator(authenticodeLogger);

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
            // SecurityPolicy expects the canonical SecurityEvaluationContext type
            var assemblyBytes = await File.ReadAllBytesAsync(assemblyPath);

            // Build the context with all properties in the initializer
            X509Certificate2? certificate = null;
            ImmutableArray<byte> strongNameKey = default;

            // Extract certificate information if required
            if (_options.RequireDigitalSignature)
            {
                // ExtractCertificateInfo is part of the AuthenticodeValidator class (already correct type via using alias)
                var certInfo = _authenticodeValidator.ExtractCertificateInfo(assemblyPath);
                if (certInfo?.Subject != null)
                {
                    // Use X509CertificateLoader instead of obsolete CreateFromSignedFile
                    var cert = X509CertificateLoader.LoadCertificateFromFile(assemblyPath);
                    certificate = cert != null ? new X509Certificate2(cert) : null;
                }
            }

            // Extract strong name key if required
            if (_options.RequireStrongName)
            {
                try
                {
                    var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
                    var publicKey = assemblyName.GetPublicKey();
                    strongNameKey = publicKey != null ? ImmutableArray.Create(publicKey) : default;
                }
                catch
                {
                    // Strong name validation will catch this
                }
            }

            // Create context with all properties initialized
            var context = new SecurityEvaluationContext
            {
                AssemblyPath = assemblyPath,
                AssemblyBytes = ImmutableArray.Create(assemblyBytes),
                Certificate = certificate,
                StrongNameKey = strongNameKey
            };

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
        GC.SuppressFinalize(this);
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting security validation for: {AssemblyPath}")]
    private partial void LogSecurityValidationStarting(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Security validation passed for: {AssemblyPath}, Security Level: {SecurityLevel}")]
    private partial void LogSecurityValidationPassed(string assemblyPath, SecurityLevel securityLevel);

    [LoggerMessage(Level = LogLevel.Error, Message = "Digital signature validation failed for: {AssemblyPath}, Reason: {Reason}")]
    private partial void LogDigitalSignatureValidationFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Digital signature validation passed for: {AssemblyPath}, Signer: {Signer}")]
    private partial void LogDigitalSignatureValidationPassed(string assemblyPath, string signer);

    [LoggerMessage(Level = LogLevel.Error, Message = "Strong name validation failed for: {AssemblyPath}")]
    private partial void LogStrongNameValidationFailed(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Strong name validation passed for: {AssemblyPath}")]
    private partial void LogStrongNameValidationPassed(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Malware scanning failed for: {AssemblyPath}, Threat: {ThreatDescription}, Level: {ThreatLevel}")]
    private partial void LogMalwareScanningFailed(string assemblyPath, string threatDescription, ThreatLevel threatLevel);

    [LoggerMessage(Level = LogLevel.Information, Message = "Malware scanning passed for: {AssemblyPath}")]
    private partial void LogMalwareScanningPassed(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Security policy violation for: {AssemblyPath}, Violations: {Violations}")]
    private partial void LogSecurityPolicyViolation(string assemblyPath, string violations);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Security policy warnings for: {AssemblyPath}, Warnings: {Warnings}")]
    private partial void LogSecurityPolicyWarnings(string assemblyPath, string warnings);

    [LoggerMessage(Level = LogLevel.Error, Message = "Security validation error for {AssemblyPath}: {Reason}")]
    private partial void LogSecurityValidationError(string assemblyPath, string reason);

    #endregion
}
