// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Security;

/// <summary>
/// Handles digital signature creation and verification with timing attack protection
/// </summary>
public sealed partial class SignatureVerifier : IDisposable
{
    private readonly ILogger _logger;
    private readonly RandomNumberGenerator _randomGenerator;
    private readonly Dictionary<string, CachedSignatureValidation> _verificationCache;
    private readonly Timer _cacheCleanupTimer;
    private readonly object _cacheLock = new();
    private volatile bool _disposed;

    // Approved hash algorithms for signatures
    private static readonly HashSet<string> ApprovedHashAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        "SHA-256", "SHA-384", "SHA-512", "SHA3-256", "SHA3-384", "SHA3-512"
    };
    /// <summary>
    /// Initializes a new instance of the SignatureVerifier class.
    /// </summary>
    /// <param name="logger">The logger.</param>

    public SignatureVerifier(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _randomGenerator = RandomNumberGenerator.Create();
        _verificationCache = [];

        // Set up cache cleanup timer
        _cacheCleanupTimer = new Timer(CleanupCache, null,
            TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));

        LogVerifierInitialized(_logger);
    }

    // LoggerMessage delegates - Event ID range 18700-18711 for SignatureVerifier
    private static readonly Action<ILogger, Exception?> _logVerifierInitialized =
        LoggerMessage.Define(
            MsLogLevel.Debug,
            new EventId(18700, nameof(LogVerifierInitialized)),
            "Signature Verifier initialized");

    private static void LogVerifierInitialized(ILogger logger)
        => _logVerifierInitialized(logger, null);

    private static readonly Action<ILogger, string, Exception?> _logSigningFailed =
        LoggerMessage.Define<string>(
            MsLogLevel.Error,
            new EventId(18701, nameof(LogSigningFailed)),
            "Signing failed for key {KeyId}");

    private static void LogSigningFailed(ILogger logger, string keyId, Exception exception)
        => _logSigningFailed(logger, keyId, exception);

    private static readonly Action<ILogger, string, Exception?> _logCacheResult =
        LoggerMessage.Define<string>(
            MsLogLevel.Debug,
            new EventId(18702, nameof(LogCacheResult)),
            "Signature verification result retrieved from cache: {KeyId}");

    private static void LogCacheResult(ILogger logger, string keyId)
        => _logCacheResult(logger, keyId, null);

    private static readonly Action<ILogger, string, Exception?> _logVerificationFailed =
        LoggerMessage.Define<string>(
            MsLogLevel.Error,
            new EventId(18703, nameof(LogVerificationFailed)),
            "Signature verification failed for key {KeyId}");

    private static void LogVerificationFailed(ILogger logger, string keyId, Exception exception)
        => _logVerificationFailed(logger, keyId, exception);

    private static readonly Action<ILogger, string, bool, Exception?> _logAlgorithmValidated =
        LoggerMessage.Define<string, bool>(
            MsLogLevel.Debug,
            new EventId(18704, nameof(LogAlgorithmValidated)),
            "Signature algorithm validation completed: {Algorithm}, Approved={Approved}");

    private static void LogAlgorithmValidated(ILogger logger, string algorithm, bool approved)
        => _logAlgorithmValidated(logger, algorithm, approved, null);

    private static readonly Action<ILogger, string, Exception?> _logRsaSignatureCreated =
        LoggerMessage.Define<string>(
            MsLogLevel.Debug,
            new EventId(18705, nameof(LogRsaSignatureCreated)),
            "RSA signature created successfully for key {KeyId}");

    private static void LogRsaSignatureCreated(ILogger logger, string keyId)
        => _logRsaSignatureCreated(logger, keyId, null);

    private static readonly Action<ILogger, string, Exception?> _logRsaSigningFailed =
        LoggerMessage.Define<string>(
            MsLogLevel.Error,
            new EventId(18706, nameof(LogRsaSigningFailed)),
            "RSA signing failed for key {KeyId}");

    private static void LogRsaSigningFailed(ILogger logger, string keyId, Exception exception)
        => _logRsaSigningFailed(logger, keyId, exception);

    private static readonly Action<ILogger, string, Exception?> _logEcdsaSignatureCreated =
        LoggerMessage.Define<string>(
            MsLogLevel.Debug,
            new EventId(18707, nameof(LogEcdsaSignatureCreated)),
            "ECDSA signature created successfully for key {KeyId}");

    private static void LogEcdsaSignatureCreated(ILogger logger, string keyId)
        => _logEcdsaSignatureCreated(logger, keyId, null);

    private static readonly Action<ILogger, string, Exception?> _logEcdsaSigningFailed =
        LoggerMessage.Define<string>(
            MsLogLevel.Error,
            new EventId(18708, nameof(LogEcdsaSigningFailed)),
            "ECDSA signing failed for key {KeyId}");

    private static void LogEcdsaSigningFailed(ILogger logger, string keyId, Exception exception)
        => _logEcdsaSigningFailed(logger, keyId, exception);

    private static readonly Action<ILogger, string, bool, Exception?> _logRsaVerificationCompleted =
        LoggerMessage.Define<string, bool>(
            MsLogLevel.Debug,
            new EventId(18709, nameof(LogRsaVerificationCompleted)),
            "RSA signature verification completed for key {KeyId}: Valid={Valid}");

    private static void LogRsaVerificationCompleted(ILogger logger, string keyId, bool valid)
        => _logRsaVerificationCompleted(logger, keyId, valid, null);

    private static readonly Action<ILogger, string, bool, Exception?> _logEcdsaVerificationCompleted =
        LoggerMessage.Define<string, bool>(
            MsLogLevel.Debug,
            new EventId(18710, nameof(LogEcdsaVerificationCompleted)),
            "ECDSA signature verification completed for key {KeyId}: Valid={Valid}");

    private static void LogEcdsaVerificationCompleted(ILogger logger, string keyId, bool valid)
        => _logEcdsaVerificationCompleted(logger, keyId, valid, null);

    private static readonly Action<ILogger, int, Exception?> _logCacheCleanup =
        LoggerMessage.Define<int>(
            MsLogLevel.Debug,
            new EventId(18711, nameof(LogCacheCleanup)),
            "Cleaned up {Count} expired signature verification cache entries");

    private static void LogCacheCleanup(ILogger logger, int count)
        => _logCacheCleanup(logger, count, null);

    private static readonly Action<ILogger, Exception?> _logVerifierDisposed =
        LoggerMessage.Define(
            MsLogLevel.Debug,
            new EventId(18712, nameof(LogVerifierDisposed)),
            "Signature Verifier disposed");

    private static void LogVerifierDisposed(ILogger logger)
        => _logVerifierDisposed(logger, null);

    private static readonly Action<ILogger, string, Exception?> _logRsaVerificationWarning =
        LoggerMessage.Define<string>(
            MsLogLevel.Warning,
            new EventId(18713, nameof(LogRsaVerificationWarning)),
            "RSA signature verification failed for key {KeyId}");

    private static void LogRsaVerificationWarning(ILogger logger, string keyId, Exception exception)
        => _logRsaVerificationWarning(logger, keyId, exception);

    private static readonly Action<ILogger, string, Exception?> _logEcdsaVerificationWarning =
        LoggerMessage.Define<string>(
            MsLogLevel.Warning,
            new EventId(18714, nameof(LogEcdsaVerificationWarning)),
            "ECDSA signature verification failed for key {KeyId}");

    private static void LogEcdsaVerificationWarning(ILogger logger, string keyId, Exception exception)
        => _logEcdsaVerificationWarning(logger, keyId, exception);

    private static readonly Action<ILogger, Exception?> _logCacheCleanupError =
        LoggerMessage.Define(
            MsLogLevel.Warning,
            new EventId(18715, nameof(LogCacheCleanupError)),
            "Error during signature verification cache cleanup");

    private static void LogCacheCleanupError(ILogger logger, Exception exception)
        => _logCacheCleanupError(logger, exception);

    /// <summary>
    /// Generates an RSA key pair for signing operations
    /// </summary>
    public async Task<SecureKeyContainer> GenerateRSAKeyAsync(
        int keySize,
        string identifier,
        string purpose)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await Task.Run(() =>
        {
            using var rsa = RSA.Create(keySize);
            var privateKey = rsa.ExportRSAPrivateKey();

            return new SecureKeyContainer(KeyType.RSA, privateKey, identifier, purpose, keySize)
            {
                PublicKeyData = rsa.ExportRSAPublicKey()
            };
        });
    }

    /// <summary>
    /// Generates an ECDSA key pair for signing operations
    /// </summary>
    public async Task<SecureKeyContainer> GenerateECDSAKeyAsync(
        int keySize,
        string identifier,
        string purpose)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await Task.Run(() =>
        {
            using var ecdsa = ECDsa.Create();
            var privateKey = ecdsa.ExportECPrivateKey();

            return new SecureKeyContainer(KeyType.ECDSA, privateKey, identifier, purpose, keySize)
            {
                PublicKeyData = ecdsa.ExportSubjectPublicKeyInfo()
            };
        });
    }

    /// <summary>
    /// Creates a digital signature for data using timing attack protection
    /// </summary>
    public async Task<SignatureResult> PerformSigningAsync(
        ReadOnlyMemory<byte> data,
        SecureKeyContainer keyContainer,
        string hashAlgorithm,
        SignatureResult result)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // Validate hash algorithm
            if (!ApprovedHashAlgorithms.Contains(hashAlgorithm))
            {
                return new SignatureResult
                {
                    Success = false,
                    ErrorMessage = $"Hash algorithm not approved: {hashAlgorithm}",
                    Algorithm = hashAlgorithm
                };
            }

            // Perform signing based on key type
            return keyContainer.KeyType switch
            {
                KeyType.RSA => await SignWithRSAAsync(data, keyContainer, hashAlgorithm, result),
                KeyType.ECDSA => await SignWithECDSAAsync(data, keyContainer, hashAlgorithm, result),
                _ => throw new ArgumentException($"Key type {keyContainer.KeyType} not suitable for signing")
            };
        }
        catch (Exception ex)
        {
            LogSigningFailed(_logger, keyContainer.Identifier, ex);
            return new SignatureResult
            {
                Success = false,
                ErrorMessage = $"Signing failed: {ex.Message}",
                Algorithm = hashAlgorithm,
                KeyId = keyContainer.Identifier
            };
        }
    }

    /// <summary>
    /// Verifies a digital signature with timing attack protection
    /// </summary>
    public async Task<SignatureVerificationResult> PerformSignatureVerificationAsync(
        ReadOnlyMemory<byte> data,
        ReadOnlyMemory<byte> signature,
        SecureKeyContainer keyContainer,
        string hashAlgorithm,
        SignatureVerificationResult result)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // Check cache first for performance
            var cacheKey = GenerateCacheKey(data, signature, keyContainer.Identifier, hashAlgorithm);
            lock (_cacheLock)
            {
                if (_verificationCache.TryGetValue(cacheKey, out var cachedResult) &&
                    DateTimeOffset.UtcNow - cachedResult.ValidationTime < TimeSpan.FromMinutes(5))
                {
                    LogCacheResult(_logger, keyContainer.Identifier);
                    return new SignatureVerificationResult
                    {
                        IsValid = cachedResult.IsValid,
                        Success = true,
                        Algorithm = hashAlgorithm,
                        KeyId = keyContainer.Identifier
                    };
                }
            }

            // Validate hash algorithm
            if (!ApprovedHashAlgorithms.Contains(hashAlgorithm))
            {
                return new SignatureVerificationResult
                {
                    IsValid = false,
                    Success = false,
                    ErrorMessage = $"Hash algorithm not approved: {hashAlgorithm}",
                    Algorithm = hashAlgorithm,
                    KeyId = keyContainer.Identifier
                };
            }

            // Perform verification based on key type with timing protection
            var verificationResult = keyContainer.KeyType switch
            {
                KeyType.RSA => await VerifyWithRSAAsync(data, signature, keyContainer, hashAlgorithm, result),
                KeyType.ECDSA => await VerifyWithECDSAAsync(data, signature, keyContainer, hashAlgorithm, result),
                _ => throw new ArgumentException($"Key type {keyContainer.KeyType} not suitable for verification")
            };

            // Cache the result
            lock (_cacheLock)
            {
                _verificationCache[cacheKey] = new CachedSignatureValidation
                {
                    IsValid = verificationResult.IsValid,
                    ValidationTime = DateTimeOffset.UtcNow
                };
            }

            return verificationResult;
        }
        catch (Exception ex)
        {
            LogVerificationFailed(_logger, keyContainer.Identifier, ex);
            return new SignatureVerificationResult
            {
                IsValid = false,
                Success = false,
                ErrorMessage = $"Verification failed: {ex.Message}",
                Algorithm = hashAlgorithm,
                KeyId = keyContainer.Identifier
            };
        }
    }

    /// <summary>
    /// Validates signature algorithm parameters for security compliance
    /// </summary>
    public SignatureAlgorithmValidationResult ValidateSignatureAlgorithm(
        string algorithm,
        int keySize,
        string context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = new SignatureAlgorithmValidationResult
        {
            Algorithm = algorithm,
            KeySize = keySize,
            Context = context,
            ValidationTime = DateTimeOffset.UtcNow
        };

        // Check if algorithm is approved
        if (!ApprovedHashAlgorithms.Contains(algorithm))
        {
            result.IsApproved = false;
            result.SecurityIssues.Add($"Hash algorithm '{algorithm}' is not approved for signature operations");
            result.Recommendations.Add($"Use approved algorithms: {string.Join(", ", ApprovedHashAlgorithms)}");
            return result;
        }

        // Validate key size based on algorithm type
        result.IsApproved = ValidateKeySize(algorithm, keySize, result);

        // Check for timing attack vulnerabilities
        if (result.IsApproved && !IsTimingAttackSafe(algorithm))
        {
            result.SecurityIssues.Add($"Algorithm '{algorithm}' may be vulnerable to timing attacks");
            result.Recommendations.Add("Ensure constant-time implementation is used");
        }

        LogAlgorithmValidated(_logger, algorithm, result.IsApproved);

        return result;
    }

    private async Task<SignatureResult> SignWithRSAAsync(
        ReadOnlyMemory<byte> data,
        SecureKeyContainer keyContainer,
        string hashAlgorithm,
        SignatureResult result)
    {
        return await Task.Run(() =>
        {
            using var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(keyContainer.GetKeyBytes(), out _);

            var hashName = GetHashAlgorithmName(hashAlgorithm);

            try
            {
                // Add random delay to prevent timing attacks
                AddTimingProtection();

                var signature = rsa.SignData(data.Span, hashName, RSASignaturePadding.Pkcs1);
                LogRsaSignatureCreated(_logger, keyContainer.Identifier);
                return new SignatureResult
                {
                    Success = true,
                    Signature = signature,
                    Algorithm = hashAlgorithm,
                    KeyId = keyContainer.Identifier
                };
            }
            catch (Exception ex)
            {
                LogRsaSigningFailed(_logger, keyContainer.Identifier, ex);
                return new SignatureResult
                {
                    Success = false,
                    ErrorMessage = "RSA signing failed",
                    Algorithm = hashAlgorithm,
                    KeyId = keyContainer.Identifier
                };
            }
        });
    }

    private async Task<SignatureResult> SignWithECDSAAsync(
        ReadOnlyMemory<byte> data,
        SecureKeyContainer keyContainer,
        string hashAlgorithm,
        SignatureResult result)
    {
        return await Task.Run(() =>
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportECPrivateKey(keyContainer.GetKeyBytes(), out _);

            var hashName = GetHashAlgorithmName(hashAlgorithm);

            try
            {
                // Add random delay to prevent timing attacks
                AddTimingProtection();

                var signature = ecdsa.SignData(data.Span, hashName);
                LogEcdsaSignatureCreated(_logger, keyContainer.Identifier);
                return new SignatureResult
                {
                    Success = true,
                    Signature = signature,
                    Algorithm = hashAlgorithm,
                    KeyId = keyContainer.Identifier
                };
            }
            catch (Exception ex)
            {
                LogEcdsaSigningFailed(_logger, keyContainer.Identifier, ex);
                return new SignatureResult
                {
                    Success = false,
                    ErrorMessage = "ECDSA signing failed",
                    Algorithm = hashAlgorithm,
                    KeyId = keyContainer.Identifier
                };
            }
        });
    }

    private async Task<SignatureVerificationResult> VerifyWithRSAAsync(
        ReadOnlyMemory<byte> data,
        ReadOnlyMemory<byte> signature,
        SecureKeyContainer keyContainer,
        string hashAlgorithm,
        SignatureVerificationResult result)
    {
        return await Task.Run(() =>
        {
            using var rsa = RSA.Create();

            // Use public key if available, otherwise private key
            if (keyContainer.PublicKeyData != null)
            {
                rsa.ImportRSAPublicKey([.. keyContainer.PublicKeyData], out _);
            }
            else
            {
                rsa.ImportRSAPrivateKey(keyContainer.GetKeyBytes(), out _);
            }

            var hashName = GetHashAlgorithmName(hashAlgorithm);

            try
            {
                // Add random delay to prevent timing attacks
                AddTimingProtection();

                var isValid = rsa.VerifyData(data.Span, signature.Span, hashName, RSASignaturePadding.Pkcs1);
                LogRsaVerificationCompleted(_logger, keyContainer.Identifier, isValid);
                return new SignatureVerificationResult
                {
                    IsValid = isValid,
                    Success = true,
                    Algorithm = hashAlgorithm,
                    KeyId = keyContainer.Identifier
                };
            }
            catch (Exception ex)
            {
                LogRsaVerificationWarning(_logger, keyContainer.Identifier, ex);
                return new SignatureVerificationResult
                {
                    IsValid = false,
                    Success = true, // Verification completed, just failed
                    Algorithm = hashAlgorithm,
                    KeyId = keyContainer.Identifier,
                    ErrorMessage = ex.Message
                };
            }
        });
    }

    private async Task<SignatureVerificationResult> VerifyWithECDSAAsync(
        ReadOnlyMemory<byte> data,
        ReadOnlyMemory<byte> signature,
        SecureKeyContainer keyContainer,
        string hashAlgorithm,
        SignatureVerificationResult result)
    {
        return await Task.Run(() =>
        {
            using var ecdsa = ECDsa.Create();

            // Use public key if available, otherwise private key
            if (keyContainer.PublicKeyData != null)
            {
                ecdsa.ImportSubjectPublicKeyInfo([.. keyContainer.PublicKeyData], out _);
            }
            else
            {
                ecdsa.ImportECPrivateKey(keyContainer.GetKeyBytes(), out _);
            }

            var hashName = GetHashAlgorithmName(hashAlgorithm);

            try
            {
                // Add random delay to prevent timing attacks
                AddTimingProtection();

                var isValid = ecdsa.VerifyData(data.Span, signature.Span, hashName);
                LogEcdsaVerificationCompleted(_logger, keyContainer.Identifier, isValid);
                return new SignatureVerificationResult
                {
                    IsValid = isValid,
                    Success = true,
                    Algorithm = hashAlgorithm,
                    KeyId = keyContainer.Identifier
                };
            }
            catch (Exception ex)
            {
                LogEcdsaVerificationWarning(_logger, keyContainer.Identifier, ex);
                return new SignatureVerificationResult
                {
                    IsValid = false,
                    Success = true, // Verification completed, just failed
                    Algorithm = hashAlgorithm,
                    KeyId = keyContainer.Identifier,
                    ErrorMessage = ex.Message
                };
            }
        });
    }

    private static HashAlgorithmName GetHashAlgorithmName(string hashAlgorithm)
    {
        return hashAlgorithm switch
        {
            "SHA-256" => HashAlgorithmName.SHA256,
            "SHA-384" => HashAlgorithmName.SHA384,
            "SHA-512" => HashAlgorithmName.SHA512,
            _ => HashAlgorithmName.SHA256
        };
    }

    private static bool ValidateKeySize(string algorithm, int keySize, SignatureAlgorithmValidationResult result)
    {
        // For signature verification, we care about the signing algorithm's key size requirements
        var minimumKeySize = algorithm switch
        {
            var alg when alg.Contains("RSA", StringComparison.OrdinalIgnoreCase) => 2048,
            var alg when alg.Contains("ECDSA", StringComparison.OrdinalIgnoreCase) => 256,
            _ => 2048 // Conservative default
        };

        if (keySize < minimumKeySize)
        {
            result.SecurityIssues.Add($"Key size {keySize} is below minimum secure size of {minimumKeySize} bits");
            result.Recommendations.Add($"Use key size of at least {minimumKeySize} bits");
            return false;
        }

        return true;
    }

    private static bool IsTimingAttackSafe(string algorithm)
    {
        // Algorithms known to have constant-time implementations in .NET
        var constantTimeAlgorithms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SHA-256", "SHA-384", "SHA-512"
        };

        return constantTimeAlgorithms.Contains(algorithm);
    }

    private void AddTimingProtection()
    {
        // Add random delay between 1-10ms to prevent timing attacks
        var delayBytes = new byte[1];
        _randomGenerator.GetBytes(delayBytes);
        var delayMs = 1 + (delayBytes[0] % 10);
        Thread.Sleep(delayMs);
    }

    private static string GenerateCacheKey(
        ReadOnlyMemory<byte> data,
        ReadOnlyMemory<byte> signature,
        string keyIdentifier,
        string hashAlgorithm)
    {
        // Generate a cache key based on data hash, signature, key, and algorithm
        using var sha256 = SHA256.Create();
        var dataHash = sha256.ComputeHash(data.ToArray());
        var signatureHash = sha256.ComputeHash(signature.ToArray());


        return $"{Convert.ToHexString(dataHash)[..16]}_{Convert.ToHexString(signatureHash)[..16]}_{keyIdentifier}_{hashAlgorithm}";
    }

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
                var expiredEntries = _verificationCache
                    .Where(kvp => DateTimeOffset.UtcNow - kvp.Value.ValidationTime > TimeSpan.FromMinutes(30))
                    .ToList();

                foreach (var (key, _) in expiredEntries)
                {
                    _ = _verificationCache.Remove(key);
                }

                if (expiredEntries.Count > 0)
                {
                    LogCacheCleanup(_logger, expiredEntries.Count);
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
            _randomGenerator?.Dispose();


            lock (_cacheLock)
            {
                _verificationCache.Clear();
            }


            _disposed = true;
            LogVerifierDisposed(_logger);
        }
    }
}

#region Supporting Types

/// <summary>
/// Result of signature algorithm validation
/// </summary>
public sealed class SignatureAlgorithmValidationResult
{
    /// <summary>
    /// Gets or sets the algorithm.
    /// </summary>
    /// <value>The algorithm.</value>
    public required string Algorithm { get; init; }
    /// <summary>
    /// Gets or sets the key size.
    /// </summary>
    /// <value>The key size.</value>
    public int KeySize { get; init; }
    /// <summary>
    /// Gets or sets the context.
    /// </summary>
    /// <value>The context.</value>
    public required string Context { get; init; }
    /// <summary>
    /// Gets or sets the validation time.
    /// </summary>
    /// <value>The validation time.</value>
    public DateTimeOffset ValidationTime { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether approved.
    /// </summary>
    /// <value>The is approved.</value>
    public bool IsApproved { get; set; }
    /// <summary>
    /// Gets or sets the security issues.
    /// </summary>
    /// <value>The security issues.</value>
    public IList<string> SecurityIssues { get; init; } = [];
    /// <summary>
    /// Gets or sets the recommendations.
    /// </summary>
    /// <value>The recommendations.</value>
    public IList<string> Recommendations { get; } = [];
}

/// <summary>
/// Cached signature validation result
/// </summary>
internal sealed class CachedSignatureValidation
{
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; init; }
    /// <summary>
    /// Gets or sets the validation time.
    /// </summary>
    /// <value>The validation time.</value>
    public DateTimeOffset ValidationTime { get; init; }
}

#endregion
