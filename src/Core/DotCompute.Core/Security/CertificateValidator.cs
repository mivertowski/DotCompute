// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DotCompute.Core.Logging;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Security;

/// <summary>
/// Validates digital certificates and certificate chains for cryptographic operations
/// </summary>
public sealed partial class CertificateValidator : IDisposable
{
    #region LoggerMessage Delegates

    [LoggerMessage(EventId = 18801, Level = MsLogLevel.Warning, Message = "Error during certificate validation cache cleanup")]
    private static partial void LogCacheCleanupError(ILogger logger, Exception ex);

    #endregion

    private readonly ILogger _logger;
    private readonly Dictionary<string, CachedCertificateValidation> _validationCache;
    private readonly Timer _cacheCleanupTimer;
    private readonly Lock _cacheLock = new();
    private volatile bool _disposed;
    /// <summary>
    /// Initializes a new instance of the CertificateValidator class.
    /// </summary>
    /// <param name="logger">The logger.</param>

    public CertificateValidator(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _validationCache = [];

        // Set up cache cleanup timer
        _cacheCleanupTimer = new Timer(CleanupCache, null,
            TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));

        _logger.LogDebugMessage("Certificate Validator initialized");
    }

    /// <summary>
    /// Validates a certificate and its chain
    /// </summary>
    public CertificateValidationResult ValidateCertificate(
        X509Certificate2 certificate,
        X509Certificate2Collection? additionalCertificates = null,
        bool checkRevocation = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentNullException.ThrowIfNull(certificate);

        try
        {
            var thumbprint = certificate.Thumbprint;
            var cacheKey = GenerateCacheKey(thumbprint, checkRevocation);

            // Check cache first
            lock (_cacheLock)
            {
                if (_validationCache.TryGetValue(cacheKey, out var cachedResult) &&
                    DateTimeOffset.UtcNow - cachedResult.ValidationTime < TimeSpan.FromMinutes(15))
                {
                    _logger.LogDebugMessage($"Certificate validation result retrieved from cache: {thumbprint}");
                    return cachedResult.Result;
                }
            }

            // Perform validation
            var result = PerformCertificateValidation(certificate, additionalCertificates, checkRevocation);

            // Cache result
            lock (_cacheLock)
            {
                _validationCache[cacheKey] = new CachedCertificateValidation
                {
                    Result = result,
                    ValidationTime = DateTimeOffset.UtcNow
                };
            }

            _logger.LogDebugMessage($"Certificate validation completed: {thumbprint}, Valid={result.IsValid}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Certificate validation failed for certificate: {certificate.Thumbprint}");
            return new CertificateValidationResult
            {
                IsValid = false,
                Certificate = certificate,
                ValidationTime = DateTimeOffset.UtcNow,
                Errors = { $"Validation error: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Validates certificate for specific usage (signing, encryption, etc.)
    /// </summary>
    public CertificateUsageValidationResult ValidateCertificateUsage(
        X509Certificate2 certificate,
        CertificateUsage intendedUsage)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentNullException.ThrowIfNull(certificate);

        try
        {
            var result = new CertificateUsageValidationResult
            {
                Certificate = certificate,
                IntendedUsage = intendedUsage,
                ValidationTime = DateTimeOffset.UtcNow
            };

            // Check basic certificate validity first
            var basicValidation = ValidateCertificate(certificate);
            if (!basicValidation.IsValid)
            {
                result.IsValidForUsage = false;
                foreach (var error in basicValidation.Errors)
                {
                    result.Issues.Add(error);
                }
                return result;
            }

            // Check key usage extensions
            result.IsValidForUsage = ValidateKeyUsage(certificate, intendedUsage, result);

            // Check extended key usage
            if (result.IsValidForUsage)
            {
                result.IsValidForUsage = ValidateExtendedKeyUsage(certificate, intendedUsage, result);
            }

            // Check key strength
            if (result.IsValidForUsage)
            {
                ValidateKeyStrength(certificate, result);
            }

            _logger.LogDebugMessage($"Certificate usage validation completed: {certificate.Thumbprint}, Usage={intendedUsage}, Valid={result.IsValidForUsage}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Certificate usage validation failed: {certificate.Thumbprint}");
            return new CertificateUsageValidationResult
            {
                Certificate = certificate,
                IntendedUsage = intendedUsage,
                ValidationTime = DateTimeOffset.UtcNow,
                IsValidForUsage = false,
                Issues = { $"Usage validation error: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Validates certificate chain up to a trusted root
    /// </summary>
    public CertificateChainValidationResult ValidateCertificateChain(
        X509Certificate2 certificate,
        X509Certificate2Collection? additionalCertificates = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentNullException.ThrowIfNull(certificate);

        try
        {
            using var chain = new X509Chain()
            {
                ChainPolicy =
                {
                    RevocationMode = X509RevocationMode.Online,
                    RevocationFlag = X509RevocationFlag.ExcludeRoot,
                    VerificationFlags = X509VerificationFlags.NoFlag,
                    VerificationTime = DateTime.UtcNow,
                    UrlRetrievalTimeout = TimeSpan.FromSeconds(30)
                }
            };

            // Add additional certificates to extra store
            if (additionalCertificates != null)
            {
                chain.ChainPolicy.ExtraStore.AddRange(additionalCertificates);
            }

            var isValid = chain.Build(certificate);

            var result = new CertificateChainValidationResult
            {
                Certificate = certificate,
                IsValid = isValid,
                ValidationTime = DateTimeOffset.UtcNow
            };

            // Add chain elements to the collection
            foreach (var element in chain.ChainElements.Cast<X509ChainElement>())
            {
                result.ChainElements.Add(new ChainElementInfo
                {
                    Certificate = element.Certificate,
                    Status = element.ChainElementStatus
                });
            }

            // Collect chain status information
            foreach (var status in chain.ChainStatus)
            {
                result.ChainStatus.Add($"{status.Status}: {status.StatusInformation}");
            }

            _logger.LogDebugMessage($"Certificate chain validation completed: {certificate.Thumbprint}, Valid={isValid}, Elements={result.ChainElements.Count}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Certificate chain validation failed: {certificate.Thumbprint}");
            return new CertificateChainValidationResult
            {
                Certificate = certificate,
                IsValid = false,
                ValidationTime = DateTimeOffset.UtcNow,
                ChainStatus = { $"Chain validation error: {ex.Message}" }
            };
        }
    }

    private CertificateValidationResult PerformCertificateValidation(
        X509Certificate2 certificate,
        X509Certificate2Collection? additionalCertificates,
        bool checkRevocation)
    {
        var result = new CertificateValidationResult
        {
            Certificate = certificate,
            ValidationTime = DateTimeOffset.UtcNow
        };

        // Check certificate validity period
        var now = DateTime.UtcNow;
        if (now < certificate.NotBefore || now > certificate.NotAfter)
        {
            result.Errors.Add($"Certificate is not valid for current time. Valid from {certificate.NotBefore} to {certificate.NotAfter}");
        }

        // Check if certificate is self-signed
        if (certificate.Subject == certificate.Issuer)
        {
            result.Warnings.Add("Certificate is self-signed");
        }

        // Validate chain
        var chainValidation = ValidateCertificateChain(certificate, additionalCertificates);
        if (!chainValidation.IsValid)
        {
            foreach (var status in chainValidation.ChainStatus)
            {
                result.Errors.Add(status);
            }
        }
        else
        {
            result.ChainInfo = chainValidation;
        }

        // Check for weak signatures
        if (IsWeakSignatureAlgorithm(certificate.SignatureAlgorithm.FriendlyName))
        {
            result.Warnings.Add($"Certificate uses weak signature algorithm: {certificate.SignatureAlgorithm.FriendlyName}");
        }

        // Check key size
        var publicKey = certificate.GetRSAPublicKey() ?? certificate.GetECDsaPublicKey() as AsymmetricAlgorithm;
        if (publicKey != null)
        {
            var keySize = publicKey.KeySize;
            if (keySize < 2048)
            {
                result.Warnings.Add($"Certificate uses weak key size: {keySize} bits");
            }
        }

        result.IsValid = result.Errors.Count == 0;

        return result;
    }

    private static bool ValidateKeyUsage(
        X509Certificate2 certificate,
        CertificateUsage intendedUsage,
        CertificateUsageValidationResult result)
    {
        if (certificate.Extensions["2.5.29.15"] is not X509KeyUsageExtension keyUsageExtension)
        {
            result.Issues.Add("Certificate does not have Key Usage extension");
            return false;
        }

        var requiredUsages = GetRequiredKeyUsages(intendedUsage);
        var hasAllRequired = requiredUsages.All(usage => keyUsageExtension.KeyUsages.HasFlag(usage));

        if (!hasAllRequired)
        {
            result.Issues.Add($"Certificate key usage does not match intended usage. Required: {requiredUsages}, Actual: {keyUsageExtension.KeyUsages}");
        }

        return hasAllRequired;
    }

    private static bool ValidateExtendedKeyUsage(
        X509Certificate2 certificate,
        CertificateUsage intendedUsage,
        CertificateUsageValidationResult result)
    {
        if (certificate.Extensions["2.5.29.37"] is not X509EnhancedKeyUsageExtension ekuExtension)
        {
            result.Issues.Add("Certificate does not have Extended Key Usage extension");
            return false;
        }

        var requiredEkus = GetRequiredExtendedKeyUsages(intendedUsage);
        var hasRequiredEku = requiredEkus.Any(eku => ekuExtension.EnhancedKeyUsages.Cast<Oid>().Any(x => x.Value == eku));

        if (!hasRequiredEku)
        {
            result.Issues.Add($"Certificate extended key usage does not match intended usage. Required one of: {string.Join(", ", requiredEkus)}");
        }

        return hasRequiredEku;
    }

    private static void ValidateKeyStrength(
        X509Certificate2 certificate,
        CertificateUsageValidationResult result)
    {
        var publicKey = certificate.GetRSAPublicKey() ?? certificate.GetECDsaPublicKey() as AsymmetricAlgorithm;
        if (publicKey != null)
        {
            var keySize = publicKey.KeySize;
            var algorithmName = certificate.PublicKey.Oid.FriendlyName;

            var minimumKeySize = algorithmName?.ToUpperInvariant() switch
            {
                "RSA" => 2048,
                "ECC" => 256,
                "ECDSA" => 256,
                _ => 2048
            };

            if (keySize < minimumKeySize)
            {
                result.Issues.Add($"Certificate key size ({keySize} bits) is below minimum recommended size ({minimumKeySize} bits) for {algorithmName}");
            }
        }
    }

    private static X509KeyUsageFlags[] GetRequiredKeyUsages(CertificateUsage intendedUsage)
    {
        return intendedUsage switch
        {
            CertificateUsage.DigitalSignature => [X509KeyUsageFlags.DigitalSignature],
            CertificateUsage.DataEncryption => [X509KeyUsageFlags.DataEncipherment, X509KeyUsageFlags.KeyEncipherment],
            CertificateUsage.KeyAgreement => [X509KeyUsageFlags.KeyAgreement],
            CertificateUsage.CertificateSigning => [X509KeyUsageFlags.KeyCertSign],
            _ => []
        };
    }

    private static string[] GetRequiredExtendedKeyUsages(CertificateUsage intendedUsage)
    {
        return intendedUsage switch
        {
            CertificateUsage.DigitalSignature => ["1.3.6.1.5.5.7.3.4"], // Email protection
            CertificateUsage.DataEncryption => ["1.3.6.1.5.5.7.3.4"], // Email protection
            CertificateUsage.ServerAuthentication => ["1.3.6.1.5.5.7.3.1"], // Server authentication
            CertificateUsage.ClientAuthentication => ["1.3.6.1.5.5.7.3.2"], // Client authentication
            CertificateUsage.CodeSigning => ["1.3.6.1.5.5.7.3.3"], // Code signing
            _ => []
        };
    }

    private static bool IsWeakSignatureAlgorithm(string? algorithmName)
    {
        if (string.IsNullOrEmpty(algorithmName))
        {
            return true;
        }

        var weakAlgorithms = new[] { "md5", "sha1", "md4", "md2" };
        return weakAlgorithms.Any(weak => algorithmName.Contains(weak, StringComparison.OrdinalIgnoreCase));
    }

    private static string GenerateCacheKey(string thumbprint, bool checkRevocation) => $"{thumbprint}_{checkRevocation}";

    private void CleanupCache(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            lock (_cacheLock)
            {
                var expiredEntries = _validationCache
                    .Where(kvp => DateTimeOffset.UtcNow - kvp.Value.ValidationTime > TimeSpan.FromHours(1))
                    .ToList();

                foreach (var (key, _) in expiredEntries)
                {
                    _ = _validationCache.Remove(key);
                }

                if (expiredEntries.Count > 0)
                {
                    _logger.LogDebugMessage($"Cleaned up {expiredEntries.Count} expired certificate validation cache entries");
                }
            }
        }
        catch (Exception ex)
        {
            LogCacheCleanupError(_logger, ex);
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _cacheCleanupTimer?.Dispose();

            lock (_cacheLock)
            {
                _validationCache.Clear();
            }

            _disposed = true;
            _logger.LogDebugMessage("Certificate Validator disposed");
        }
    }
}
