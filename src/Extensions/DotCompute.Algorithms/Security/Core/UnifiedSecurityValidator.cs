
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using DotCompute.Abstractions.Security;
using Microsoft.Extensions.Logging;
using SecurityEvaluationContext = DotCompute.Abstractions.Security.SecurityEvaluationContext;
using SecurityLevel = DotCompute.Abstractions.Security.SecurityLevel;

namespace DotCompute.Algorithms.Security.Core;

/// <summary>
/// Unified security validator that consolidates all security validation concerns
/// into a single, cohesive service with comprehensive validation capabilities.
/// </summary>
public sealed partial class UnifiedSecurityValidator : IUnifiedSecurityValidator
{
    private readonly ILogger<UnifiedSecurityValidator> _logger;
    private readonly SecurityConfiguration _configuration;
    private readonly AuthenticodeValidator _authenticodeValidator;
    private readonly MalwareScanningService _malwareScanner;
    private readonly SecurityPolicy _securityPolicy;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnifiedSecurityValidator"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="configuration">Security configuration.</param>
    public UnifiedSecurityValidator(ILogger<UnifiedSecurityValidator> logger, SecurityConfiguration? configuration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? new SecurityConfiguration();

        // Initialize security components
        var policyLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<SecurityPolicy>.Instance;
        _securityPolicy = new SecurityPolicy(policyLogger);

        var authenticodeLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthenticodeValidator>.Instance;
        _authenticodeValidator = new AuthenticodeValidator(authenticodeLogger);

        var malwareLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MalwareScanningService>.Instance;
        _malwareScanner = new MalwareScanningService(malwareLogger, new MalwareScanningOptions
        {
            EnableWindowsDefender = _configuration.EnableMalwareScanning,
            MaxConcurrentScans = 2,
            ScanTimeout = _configuration.ScanTimeout
        });

        ConfigureSecurityPolicy();
    }

    /// <inheritdoc/>
    public SecurityConfiguration Configuration => _configuration;

    /// <inheritdoc/>
    public async Task<UnifiedSecurityResult> ValidateAssemblyAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(assemblyPath);

        if (!_configuration.EnableSecurityValidation)
        {
            LogSecurityValidationDisabled(assemblyPath);
            return CreatePassingResult();
        }

        var startTime = DateTimeOffset.UtcNow;
        LogSecurityValidationStarting(assemblyPath);

        try
        {
            var violations = new List<string>();
            var warnings = new List<string>();
            var metadata = new Dictionary<string, object>();

            // 1. Digital signature validation
            DigitalSignatureResult? digitalSignature = null;
            if (_configuration.RequireDigitalSignature)
            {
                digitalSignature = await ValidateDigitalSignatureAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
                if (!digitalSignature.IsValid)
                {
                    violations.Add($"Digital signature validation failed: {digitalSignature.ErrorMessage}");
                }
            }

            // 2. Strong name validation
            StrongNameResult? strongName = null;
            if (_configuration.RequireStrongName)
            {
                strongName = await ValidateStrongNameAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
                if (!strongName.IsValid)
                {
                    violations.Add($"Strong name validation failed: {strongName.ErrorMessage}");
                }
            }

            // 3. Malware scanning
            MalwareScanResult? malwareScan = null;
            if (_configuration.EnableMalwareScanning)
            {
                malwareScan = await ScanForMalwareAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
                if (!malwareScan.IsClean)
                {
                    violations.Add($"Malware detected: {malwareScan.ThreatDescription}");
                }
            }

            // 4. Security policy evaluation
            var context = await CreateSecurityEvaluationContextAsync(assemblyPath, digitalSignature, strongName).ConfigureAwait(false);
            var policyResult = await EvaluateSecurityPolicyAsync(assemblyPath, context, cancellationToken).ConfigureAwait(false);

            if (!policyResult.IsAllowed)
            {
                violations.AddRange(policyResult.Violations);
            }
            warnings.AddRange(policyResult.Warnings);

            var duration = DateTimeOffset.UtcNow - startTime;
            var isValid = violations.Count == 0;
            var securityLevel = DetermineSecurityLevel(digitalSignature, strongName, malwareScan, policyResult);

            metadata["ValidationSteps"] = new[] { "DigitalSignature", "StrongName", "MalwareScan", "PolicyEvaluation" };
            metadata["AssemblySize"] = new FileInfo(assemblyPath).Length;

            var result = new UnifiedSecurityResult
            {
                IsValid = isValid,
                SecurityLevel = securityLevel,
                DigitalSignature = digitalSignature,
                StrongName = strongName,
                MalwareScan = malwareScan,
                PolicyEvaluation = policyResult,
                Violations = violations.AsReadOnly(),
                Warnings = warnings.AsReadOnly(),
                ValidationDuration = duration,
                Metadata = metadata.AsReadOnly()
            };

            if (isValid)
            {
                LogSecurityValidationPassed(assemblyPath, securityLevel, duration.TotalMilliseconds);
            }
            else
            {
                LogSecurityValidationFailed(assemblyPath, string.Join(", ", violations));
            }

            return result;
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            LogSecurityValidationError(assemblyPath, ex.Message);

            return new UnifiedSecurityResult
            {
                IsValid = false,
                SecurityLevel = SecurityLevel.Basic,
                Violations = [$"Validation error: {ex.Message}"],
                ValidationDuration = duration
            };
        }
    }

    /// <inheritdoc/>
    public async Task<DigitalSignatureResult> ValidateDigitalSignatureAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            var result = await _authenticodeValidator.ValidateAsync(assemblyPath, cancellationToken).ConfigureAwait(false);

            return new DigitalSignatureResult
            {
                IsValid = result.IsValid,
                TrustLevel = result.TrustLevel,
                SignerName = result.SignerName,
                CertificateThumbprint = result.Certificate?.Thumbprint,
                ErrorMessage = result.ErrorMessage
            };
        }
        catch (Exception ex)
        {
            return new DigitalSignatureResult
            {
                IsValid = false,
                TrustLevel = TrustLevel.None,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public async Task<StrongNameResult> ValidateStrongNameAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            await Task.CompletedTask; // Make async for consistency
            var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
            var publicKey = assemblyName.GetPublicKey();

            if (publicKey == null || publicKey.Length == 0)
            {
                return new StrongNameResult
                {
                    IsValid = false,
                    ErrorMessage = "Assembly does not have a strong name"
                };
            }

            // Basic validation: ensure key is of reasonable size
            if (publicKey.Length < 160) // Minimum for RSA-1024
            {
                return new StrongNameResult
                {
                    IsValid = false,
                    ErrorMessage = "Strong name key is too small (minimum 1024-bit RSA required)"
                };
            }

            return new StrongNameResult
            {
                IsValid = true,
                PublicKey = publicKey
            };
        }
        catch (Exception ex)
        {
            return new StrongNameResult
            {
                IsValid = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public async Task<MalwareScanResult> ScanForMalwareAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            return await _malwareScanner.ScanAssemblyAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new MalwareScanResult
            {
                IsClean = false,
                ThreatLevel = ThreatLevel.None,
                ThreatDescription = $"Scan error: {ex.Message}",
                ScanDuration = TimeSpan.Zero
            };
        }
    }

    /// <inheritdoc/>
    public async Task<SecurityPolicyResult> EvaluateSecurityPolicyAsync(string assemblyPath, SecurityEvaluationContext context, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            await Task.CompletedTask; // Make async for consistency
            var result = _securityPolicy.EvaluateRules(context);

            return new SecurityPolicyResult
            {
                IsAllowed = result.IsAllowed,
                SecurityLevel = result.SecurityLevel,
                Violations = result.Violations.AsReadOnly(),
                Warnings = result.Warnings.AsReadOnly()
            };
        }
        catch (Exception ex)
        {
            return new SecurityPolicyResult
            {
                IsAllowed = false,
                SecurityLevel = SecurityLevel.Basic,
                Violations = [$"Policy evaluation error: {ex.Message}"]
            };
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

    private static async Task<SecurityEvaluationContext> CreateSecurityEvaluationContextAsync(
        string assemblyPath,
        DigitalSignatureResult? digitalSignature,
        StrongNameResult? strongName)
    {
        var assemblyBytes = await File.ReadAllBytesAsync(assemblyPath).ConfigureAwait(false);

        X509Certificate2? certificate = null;
        // Add certificate information if available
        if (digitalSignature?.IsValid == true && digitalSignature.CertificateThumbprint != null)
        {
            try
            {
                certificate = GetCertificateFromStore(digitalSignature.CertificateThumbprint);
            }
            catch
            {
                // Certificate not found in store
            }
        }

        byte[]? strongNameKey = null;
        // Add strong name key if available
        if (strongName?.IsValid == true && strongName.PublicKey != null)
        {
            strongNameKey = strongName.PublicKey.ToArray();
        }

        var context = new SecurityEvaluationContext
        {
            AssemblyPath = assemblyPath,
            AssemblyBytes = System.Collections.Immutable.ImmutableArray.Create(assemblyBytes),
            Certificate = certificate,
            StrongNameKey = strongNameKey != null ? System.Collections.Immutable.ImmutableArray.Create(strongNameKey) : []
        };

        return context;
    }

    private static X509Certificate2? GetCertificateFromStore(string thumbprint)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
        return certificates.Count > 0 ? certificates[0] : null;
    }

    private static SecurityLevel DetermineSecurityLevel(
        DigitalSignatureResult? digitalSignature,
        StrongNameResult? strongName,
        MalwareScanResult? malwareScan,
        SecurityPolicyResult policyResult)
    {
        var level = SecurityLevel.Low;

        if (digitalSignature?.IsValid == true && digitalSignature.TrustLevel >= TrustLevel.Medium)
        {
            level = SecurityLevel.Medium;
        }

        if (strongName?.IsValid == true)
        {
            level = level == SecurityLevel.Medium ? SecurityLevel.High : SecurityLevel.Medium;
        }

        if (malwareScan?.IsClean == true && malwareScan.ThreatLevel == ThreatLevel.None)
        {
            level = level switch
            {
                DotCompute.Abstractions.Security.SecurityLevel.Low => DotCompute.Abstractions.Security.SecurityLevel.Medium,
                DotCompute.Abstractions.Security.SecurityLevel.Medium => DotCompute.Abstractions.Security.SecurityLevel.High,
                DotCompute.Abstractions.Security.SecurityLevel.High => DotCompute.Abstractions.Security.SecurityLevel.Maximum,
                _ => level
            };
        }

        // Policy result takes precedence if it's more restrictive
        if (policyResult.SecurityLevel < level)
        {
            level = policyResult.SecurityLevel;
        }

        return level;
    }

    private static UnifiedSecurityResult CreatePassingResult()
    {
        return new UnifiedSecurityResult
        {
            IsValid = true,
            SecurityLevel = SecurityLevel.Medium,
            ValidationDuration = TimeSpan.Zero
        };
    }

    private void ConfigureSecurityPolicy()
    {
        _securityPolicy.RequireDigitalSignature = _configuration.RequireDigitalSignature;
        _securityPolicy.RequireStrongName = _configuration.RequireStrongName;
        _securityPolicy.MinimumSecurityLevel = _configuration.MinimumSecurityLevel;
        _securityPolicy.MaxAssemblySize = _configuration.MaxAssemblySize;
        _securityPolicy.EnableMalwareScanning = _configuration.EnableMalwareScanning;
        _securityPolicy.EnableMetadataAnalysis = true;

        foreach (var publisher in _configuration.TrustedPublishers)
        {
            _securityPolicy.AddTrustedPublisher(publisher);
        }

        foreach (var directory in _configuration.AllowedPluginDirectories)
        {
            _securityPolicy.DirectoryPolicies[directory] = SecurityLevel.High;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            try
            {
                _authenticodeValidator?.Dispose();
            }
            catch (Exception ex)
            {
                LogAuthenticodeValidatorDisposeFailed(ex);
            }

            try
            {
                _malwareScanner?.Dispose();
            }
            catch (Exception ex)
            {
                LogMalwareScannerDisposeFailed(ex);
            }

            LogSecurityValidatorDisposed();
        }
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Security validation disabled for: {AssemblyPath}")]
    private partial void LogSecurityValidationDisabled(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting comprehensive security validation for: {AssemblyPath}")]
    private partial void LogSecurityValidationStarting(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Security validation passed for: {AssemblyPath}, Level: {SecurityLevel}, Duration: {DurationMs:F2}ms")]
    partial void LogSecurityValidationPassed(string assemblyPath, SecurityLevel securityLevel, double durationMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Security validation failed for: {AssemblyPath}, Violations: {Violations}")]
    private partial void LogSecurityValidationFailed(string assemblyPath, string violations);

    [LoggerMessage(Level = LogLevel.Error, Message = "Security validation error for: {AssemblyPath}, Error: {ErrorMessage}")]
    private partial void LogSecurityValidationError(string assemblyPath, string errorMessage);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Unified security validator disposed")]
    private partial void LogSecurityValidatorDisposed();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error disposing authenticode validator")]
    private partial void LogAuthenticodeValidatorDisposeFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error disposing malware scanner")]
    private partial void LogMalwareScannerDisposeFailed(Exception ex);

    #endregion
}
