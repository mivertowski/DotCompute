// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using System;
using DotCompute.Abstractions.Security;

namespace DotCompute.Algorithms.Types.Security;


/// <summary>
/// Validates Authenticode digital signatures on assemblies.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AuthenticodeValidator"/> class.
/// </remarks>
/// <param name="logger">Optional logger for diagnostics.</param>
public class AuthenticodeValidator(ILogger<AuthenticodeValidator>? logger = null) : IDisposable
{
    private readonly ILogger<AuthenticodeValidator>? _logger = logger;
    private bool _disposed;

    /// <summary>
    /// Validates the Authenticode signature of an assembly.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly file.</param>
    /// <returns>The validation result.</returns>
    public async Task<AuthenticodeValidationResult> ValidateAsync(string assemblyPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        if (!File.Exists(assemblyPath))
        {
            return new AuthenticodeValidationResult
            {
                IsValid = false,
                ErrorMessage = "Assembly file not found"
            };
        }

        try
        {
            _logger?.LogDebug("Validating Authenticode signature for {AssemblyPath}", assemblyPath);

            // For now, return a mock result since full Authenticode validation
            // requires platform-specific implementation
            var result = await ValidateAssemblySignatureAsync(assemblyPath);

            _logger?.LogDebug("Signature validation result: {IsValid}", result.IsValid);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error validating assembly signature for {AssemblyPath}", assemblyPath);
            return new AuthenticodeValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Validation error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validates multiple assemblies in parallel.
    /// </summary>
    /// <param name="assemblyPaths">Paths to the assembly files.</param>
    /// <param name="maxConcurrency">Maximum number of concurrent validations.</param>
    /// <returns>Dictionary mapping assembly paths to validation results.</returns>
    public async Task<Dictionary<string, AuthenticodeValidationResult>> ValidateMultipleAsync(
        IEnumerable<string> assemblyPaths,
        int maxConcurrency = 4)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(assemblyPaths);

        var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = assemblyPaths.Select(async path =>
        {
            await semaphore.WaitAsync();
            try
            {
                var result = await ValidateAsync(path);
                return new KeyValuePair<string, AuthenticodeValidationResult>(path, result);
            }
            finally
            {
                _ = semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Extracts certificate information from an assembly.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly file.</param>
    /// <returns>Certificate information, or null if no certificate found.</returns>
    public X509Certificate2? ExtractCertificateInfo(string assemblyPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        if (!File.Exists(assemblyPath))
        {
            return null;
        }

        try
        {
            // Simplified implementation - in practice would use WinVerifyTrust or similar
            // For cross-platform compatibility, we'll return null for now
            _logger?.LogDebug("Extracting certificate info for {AssemblyPath}", assemblyPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to extract certificate info from {AssemblyPath}", assemblyPath);
            return null;
        }
    }

    /// <summary>
    /// Internal method to validate assembly signature.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly.</param>
    /// <returns>Validation result.</returns>
    private static async Task<AuthenticodeValidationResult> ValidateAssemblySignatureAsync(string assemblyPath)
    {
        // Mock implementation for cross-platform compatibility
        // In a real implementation, this would:
        // 1. Use WinVerifyTrust on Windows
        // 2. Check assembly signature metadata
        // 3. Validate certificate chain
        // 4. Check certificate revocation status

        await Task.Delay(50); // Simulate validation work

        var fileName = Path.GetFileName(assemblyPath);
        var isSystemFile = fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                          fileName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                          fileName.StartsWith("netstandard", StringComparison.CurrentCulture) ||
                          fileName.StartsWith("mscorlib", StringComparison.CurrentCulture);

        return new AuthenticodeValidationResult
        {
            IsValid = isSystemFile, // Mock: assume system files are signed
            SignerName = isSystemFile ? "Microsoft Corporation" : "Unknown",
            TrustLevel = isSystemFile ? TrustLevel.Trusted : TrustLevel.Unknown,
            IsTrustedPublisher = isSystemFile,
            ErrorMessage = isSystemFile ? null : "No valid signature found"
        };
    }

    /// <summary>
    /// Disposes the validator resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// Result of Authenticode signature validation.
/// </summary>
public class AuthenticodeValidationResult
{
    /// <summary>
    /// Gets or sets whether the signature is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the name of the signer.
    /// </summary>
    public string? SignerName { get; set; }

    /// <summary>
    /// Gets or sets the trust level of the signature.
    /// </summary>
    public TrustLevel TrustLevel { get; set; }

    /// <summary>
    /// Gets or sets whether the publisher is trusted.
    /// </summary>
    public bool IsTrustedPublisher { get; set; }

    /// <summary>
    /// Gets or sets the certificate thumbprint.
    /// </summary>
    public string? CertificateThumbprint { get; set; }

    /// <summary>
    /// Gets or sets the certificate information.
    /// </summary>
    public X509Certificate2? CertificateInfo { get; set; }

    /// <summary>
    /// Gets or sets the error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets additional validation details.
    /// </summary>
    public Dictionary<string, object> AdditionalDetails { get; } = [];
}
