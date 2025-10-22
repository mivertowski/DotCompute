// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Security;

namespace DotCompute.Algorithms.Security.Core;

/// <summary>
/// Unified interface for comprehensive security validation of algorithm plugins.
/// Consolidates all security concerns into a single, cohesive service.
/// </summary>
public interface IUnifiedSecurityValidator : IDisposable
{
    /// <summary>
    /// Performs comprehensive security validation of an assembly.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Comprehensive security validation result.</returns>
    public Task<UnifiedSecurityResult> ValidateAssemblyAsync(string assemblyPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates digital signature and certificate trust.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Digital signature validation result.</returns>
    public Task<DigitalSignatureResult> ValidateDigitalSignatureAsync(string assemblyPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates strong name signature.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Strong name validation result.</returns>
    public Task<StrongNameResult> ValidateStrongNameAsync(string assemblyPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs malware scanning using available security providers.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly to scan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Malware scan result.</returns>
    public Task<MalwareScanResult> ScanForMalwareAsync(string assemblyPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates security policy compliance.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly to evaluate.</param>
    /// <param name="context">Security evaluation context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Security policy evaluation result.</returns>
    public Task<SecurityPolicyResult> EvaluateSecurityPolicyAsync(string assemblyPath, SecurityEvaluationContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks framework version compatibility.
    /// </summary>
    /// <param name="requiredVersion">Required framework version.</param>
    /// <returns>True if compatible; otherwise, false.</returns>
    public bool IsVersionCompatible(Version? requiredVersion);

    /// <summary>
    /// Gets the current security configuration.
    /// </summary>
    public SecurityConfiguration Configuration { get; }
}

/// <summary>
/// Comprehensive security validation result.
/// </summary>
public sealed class UnifiedSecurityResult
{
    /// <summary>
    /// Gets whether the assembly passed all security validations.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Gets the overall security level assessed.
    /// </summary>
    public required SecurityLevel SecurityLevel { get; init; }

    /// <summary>
    /// Gets the digital signature validation result.
    /// </summary>
    public DigitalSignatureResult? DigitalSignature { get; init; }

    /// <summary>
    /// Gets the strong name validation result.
    /// </summary>
    public StrongNameResult? StrongName { get; init; }

    /// <summary>
    /// Gets the malware scan result.
    /// </summary>
    public MalwareScanResult? MalwareScan { get; init; }

    /// <summary>
    /// Gets the security policy evaluation result.
    /// </summary>
    public SecurityPolicyResult? PolicyEvaluation { get; init; }

    /// <summary>
    /// Gets any security violations found.
    /// </summary>
    public IReadOnlyList<string> Violations { get; init; } = [];

    /// <summary>
    /// Gets any security warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Gets the validation duration.
    /// </summary>
    public TimeSpan ValidationDuration { get; init; }

    /// <summary>
    /// Gets additional validation metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Digital signature validation result.
/// </summary>
public sealed class DigitalSignatureResult
{
    /// <summary>
    /// Gets whether the digital signature is valid.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Gets the trust level of the signature.
    /// </summary>
    public required TrustLevel TrustLevel { get; init; }

    /// <summary>
    /// Gets the signer name.
    /// </summary>
    public string? SignerName { get; init; }

    /// <summary>
    /// Gets the certificate thumbprint.
    /// </summary>
    public string? CertificateThumbprint { get; init; }

    /// <summary>
    /// Gets any error message.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Strong name validation result.
/// </summary>
public sealed class StrongNameResult
{
    /// <summary>
    /// Gets whether the strong name is valid.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Gets the public key information.
    /// </summary>
    public IReadOnlyList<byte>? PublicKey { get; init; }

    /// <summary>
    /// Gets any error message.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Security policy evaluation result.
/// </summary>
public sealed class SecurityPolicyResult
{
    /// <summary>
    /// Gets whether the policy evaluation passed.
    /// </summary>
    public required bool IsAllowed { get; init; }

    /// <summary>
    /// Gets the assessed security level.
    /// </summary>
    public required SecurityLevel SecurityLevel { get; init; }

    /// <summary>
    /// Gets any policy violations.
    /// </summary>
    public IReadOnlyList<string> Violations { get; init; } = [];

    /// <summary>
    /// Gets any policy warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Security configuration for the validator.
/// </summary>
public sealed class SecurityConfiguration
{
    /// <summary>
    /// Gets or sets whether security validation is enabled.
    /// </summary>
    public bool EnableSecurityValidation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether digital signature validation is required.
    /// </summary>
    public bool RequireDigitalSignature { get; set; }

    /// <summary>
    /// Gets or sets whether strong name validation is required.
    /// </summary>
    public bool RequireStrongName { get; set; }

    /// <summary>
    /// Gets or sets whether malware scanning is enabled.
    /// </summary>
    public bool EnableMalwareScanning { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum required security level.
    /// </summary>
    public SecurityLevel MinimumSecurityLevel { get; set; } = SecurityLevel.Medium;

    /// <summary>
    /// Gets or sets the maximum allowed assembly size in bytes.
    /// </summary>
    public long MaxAssemblySize { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// Gets trusted publishers.
    /// </summary>
    public ISet<string> TrustedPublishers { get; init; } = new HashSet<string>();

    /// <summary>
    /// Gets allowed plugin directories.
    /// </summary>
    public ISet<string> AllowedPluginDirectories { get; init; } = new HashSet<string>();

    /// <summary>
    /// Gets or sets scan timeout duration.
    /// </summary>
    public TimeSpan ScanTimeout { get; set; } = TimeSpan.FromMinutes(2);
}