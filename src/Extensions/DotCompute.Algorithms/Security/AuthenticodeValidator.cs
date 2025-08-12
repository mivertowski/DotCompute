// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Security;

/// <summary>
/// Validates Authenticode digital signatures on assemblies using Windows APIs.
/// </summary>
public sealed class AuthenticodeValidator : IDisposable
{
    private readonly ILogger<AuthenticodeValidator> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticodeValidator"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public AuthenticodeValidator(ILogger<AuthenticodeValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates the Authenticode signature of an assembly.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    public async Task<AuthenticodeValidationResult> ValidateAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!File.Exists(assemblyPath))
        {
            return new AuthenticodeValidationResult
            {
                IsValid = false,
                ErrorMessage = "Assembly file not found",
                TrustLevel = TrustLevel.None
            };
        }

        _logger.LogDebug("Validating Authenticode signature for: {AssemblyPath}", assemblyPath);

        try
        {
            var result = await Task.Run(() => ValidateSignature(assemblyPath), cancellationToken);
            
            if (result.IsValid)
            {
                _logger.LogInformation("Authenticode validation passed for: {AssemblyPath}", assemblyPath);
            }
            else
            {
                _logger.LogWarning("Authenticode validation failed for: {AssemblyPath}, Reason: {ErrorMessage}", 
                    assemblyPath, result.ErrorMessage);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during Authenticode validation for: {AssemblyPath}", assemblyPath);
            return new AuthenticodeValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Validation exception: {ex.Message}",
                TrustLevel = TrustLevel.None
            };
        }
    }

    /// <summary>
    /// Validates multiple assemblies concurrently.
    /// </summary>
    /// <param name="assemblyPaths">The paths to the assemblies to validate.</param>
    /// <param name="maxConcurrency">Maximum number of concurrent validations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of validation results keyed by assembly path.</returns>
    public async Task<Dictionary<string, AuthenticodeValidationResult>> ValidateMultipleAsync(
        IEnumerable<string> assemblyPaths, 
        int maxConcurrency = 5, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assemblyPaths);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = assemblyPaths.Select(async path =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var result = await ValidateAsync(path, cancellationToken);
                return new KeyValuePair<string, AuthenticodeValidationResult>(path, result);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private AuthenticodeValidationResult ValidateSignature(string filePath)
    {
        var result = new AuthenticodeValidationResult();

        try
        {
            // Try to create certificate from signed file
            X509Certificate2? certificate = null;
            try
            {
                // Use X509CertificateLoader instead of obsolete CreateFromSignedFile
                certificate = X509CertificateLoader.LoadCertificateFromFile(filePath);
                result.Certificate = certificate;
                if (certificate != null)
                {
                    result.SignerName = certificate.GetNameInfo(X509NameType.SimpleName, false);
                    result.IssuerName = certificate.GetNameInfo(X509NameType.SimpleName, true);
                    result.Thumbprint = certificate.Thumbprint;
                    result.NotAfter = certificate.NotAfter;
                    result.NotBefore = certificate.NotBefore;
                }
            }
            catch (Exception)
            {
                result.IsValid = false;
                result.ErrorMessage = "No valid Authenticode signature found";
                result.TrustLevel = TrustLevel.None;
                return result;
            }

            if (certificate == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "Unable to extract certificate from signature";
                result.TrustLevel = TrustLevel.None;
                return result;
            }

            // Validate certificate chain
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

            var chainValid = chain.Build(certificate);
            result.ChainStatus = [.. chain.ChainStatus];

            if (!chainValid)
            {
                var criticalErrors = chain.ChainStatus.Where(status => 
                    status.Status == X509ChainStatusFlags.NotTimeValid ||
                    status.Status == X509ChainStatusFlags.Revoked ||
                    status.Status == X509ChainStatusFlags.NotSignatureValid);

                if (criticalErrors.Any())
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"Critical certificate issues: {string.Join(", ", criticalErrors.Select(e => e.StatusInformation))}";
                    result.TrustLevel = TrustLevel.None;
                    return result;
                }
                
                // Non-critical issues - still consider valid but with lower trust
                result.TrustLevel = TrustLevel.Low;
                result.Warnings.AddRange(chain.ChainStatus.Select(s => s.StatusInformation));
            }

            // Check if certificate is in trusted publisher store
            result.IsTrustedPublisher = IsCertificateInTrustedStore(certificate);
            
            // Determine overall trust level
            if (result.IsTrustedPublisher && chainValid)
            {
                result.TrustLevel = TrustLevel.High;
            }
            else if (chainValid)
            {
                result.TrustLevel = TrustLevel.Medium;
            }
            else if (result.TrustLevel == TrustLevel.Unknown)
            {
                result.TrustLevel = TrustLevel.Low;
            }

            // Check certificate validity period
            var now = DateTime.Now;
            if (now < certificate.NotBefore || now > certificate.NotAfter)
            {
                result.IsValid = false;
                result.ErrorMessage = "Certificate is not currently valid (expired or not yet valid)";
                result.TrustLevel = TrustLevel.None;
                return result;
            }

            // If we get here, the signature is valid
            result.IsValid = true;
            
            _logger.LogDebug("Authenticode validation successful - Signer: {Signer}, Trust Level: {TrustLevel}", 
                result.SignerName, result.TrustLevel);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Authenticode signature for: {FilePath}", filePath);
            result.IsValid = false;
            result.ErrorMessage = $"Validation error: {ex.Message}";
            result.TrustLevel = TrustLevel.None;
            return result;
        }
    }

    private static bool IsCertificateInTrustedStore(X509Certificate2 certificate)
    {
        try
        {
            using var store = new X509Store(StoreName.TrustedPublisher, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            var found = store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false);
            return found.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts certificate information from a signed assembly without full validation.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly.</param>
    /// <returns>Basic certificate information or null if no signature.</returns>
    public CertificateInfo? ExtractCertificateInfo(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // Use X509CertificateLoader instead of obsolete CreateFromSignedFile
            using var certificate = X509CertificateLoader.LoadCertificateFromFile(assemblyPath);
            if (certificate == null) throw new InvalidOperationException("Failed to extract certificate");

            return new CertificateInfo
            {
                Subject = certificate.Subject,
                Issuer = certificate.Issuer,
                Thumbprint = certificate.Thumbprint,
                NotBefore = certificate.NotBefore,
                NotAfter = certificate.NotAfter,
                SignerName = certificate.GetNameInfo(X509NameType.SimpleName, false),
                IssuerName = certificate.GetNameInfo(X509NameType.SimpleName, true),
                SerialNumber = certificate.SerialNumber
            };
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
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
public sealed class AuthenticodeValidationResult
{
    /// <summary>
    /// Gets or sets whether the signature is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the trust level of the signature.
    /// </summary>
    public TrustLevel TrustLevel { get; set; } = TrustLevel.Unknown;

    /// <summary>
    /// Gets or sets the error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the signer certificate.
    /// </summary>
    public X509Certificate2? Certificate { get; set; }

    /// <summary>
    /// Gets or sets the signer name.
    /// </summary>
    public string? SignerName { get; set; }

    /// <summary>
    /// Gets or sets the issuer name.
    /// </summary>
    public string? IssuerName { get; set; }

    /// <summary>
    /// Gets or sets the certificate thumbprint.
    /// </summary>
    public string? Thumbprint { get; set; }

    /// <summary>
    /// Gets or sets the certificate validity start date.
    /// </summary>
    public DateTime NotBefore { get; set; }

    /// <summary>
    /// Gets or sets the certificate validity end date.
    /// </summary>
    public DateTime NotAfter { get; set; }

    /// <summary>
    /// Gets or sets whether the signer is a trusted publisher.
    /// </summary>
    public bool IsTrustedPublisher { get; set; }

    /// <summary>
    /// Gets the certificate chain status.
    /// </summary>
    public X509ChainStatus[] ChainStatus { get; set; } = [];

    /// <summary>
    /// Gets validation warnings.
    /// </summary>
    public List<string> Warnings { get; } = [];

    /// <summary>
    /// Gets additional validation metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = [];
}

/// <summary>
/// Trust levels for signed assemblies.
/// </summary>
public enum TrustLevel
{
    /// <summary>
    /// No trust - unsigned or invalid signature.
    /// </summary>
    None = 0,

    /// <summary>
    /// Unknown trust level.
    /// </summary>
    Unknown = 1,

    /// <summary>
    /// Low trust - signed but with issues.
    /// </summary>
    Low = 2,

    /// <summary>
    /// Medium trust - signed with valid certificate.
    /// </summary>
    Medium = 3,

    /// <summary>
    /// High trust - signed by trusted publisher.
    /// </summary>
    High = 4
}

/// <summary>
/// Basic certificate information.
/// </summary>
public sealed class CertificateInfo
{
    /// <summary>
    /// Gets or sets the certificate subject.
    /// </summary>
    public required string Subject { get; set; }

    /// <summary>
    /// Gets or sets the certificate issuer.
    /// </summary>
    public required string Issuer { get; set; }

    /// <summary>
    /// Gets or sets the certificate thumbprint.
    /// </summary>
    public required string Thumbprint { get; set; }

    /// <summary>
    /// Gets or sets the validity start date.
    /// </summary>
    public DateTime NotBefore { get; set; }

    /// <summary>
    /// Gets or sets the validity end date.
    /// </summary>
    public DateTime NotAfter { get; set; }

    /// <summary>
    /// Gets or sets the signer name.
    /// </summary>
    public string? SignerName { get; set; }

    /// <summary>
    /// Gets or sets the issuer name.
    /// </summary>
    public string? IssuerName { get; set; }

    /// <summary>
    /// Gets or sets the certificate serial number.
    /// </summary>
    public string? SerialNumber { get; set; }
}