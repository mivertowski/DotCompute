// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Security;
using global::System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;

namespace DotCompute.Core.Security;

/// <summary>
/// Handles digital signature creation and verification with timing attack protection
/// </summary>
public sealed class SignatureVerifier : IDisposable
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

    public SignatureVerifier(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _randomGenerator = RandomNumberGenerator.Create();
        _verificationCache = new Dictionary<string, CachedSignatureValidation>();

        // Set up cache cleanup timer
        _cacheCleanupTimer = new Timer(CleanupCache, null,
            TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));

        _logger.LogDebugMessage("Signature Verifier initialized");
    }

    /// <summary>
    /// Generates an RSA key pair for signing operations
    /// </summary>
    public async Task<SecureKeyContainer> GenerateRSAKeyAsync(
        int keySize,
        string identifier,
        string purpose)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SignatureVerifier));
        }

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
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SignatureVerifier));
        }

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
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SignatureVerifier));
        }

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
            _logger.LogErrorMessage(ex, $"Signing failed for key {keyContainer.Identifier}");
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
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SignatureVerifier));
        }

        try
        {
            // Check cache first for performance
            var cacheKey = GenerateCacheKey(data, signature, keyContainer.Identifier, hashAlgorithm);
            lock (_cacheLock)
            {
                if (_verificationCache.TryGetValue(cacheKey, out var cachedResult) &&
                    DateTimeOffset.UtcNow - cachedResult.ValidationTime < TimeSpan.FromMinutes(5))
                {
                    _logger.LogDebugMessage($"Signature verification result retrieved from cache: {keyContainer.Identifier}");
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
            _logger.LogErrorMessage(ex, $"Signature verification failed for key {keyContainer.Identifier}");
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
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SignatureVerifier));
        }

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

        _logger.LogDebugMessage($"Signature algorithm validation completed: {algorithm}, Approved={result.IsApproved}");

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
                _logger.LogDebugMessage($"RSA signature created successfully for key {keyContainer.Identifier}");
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
                _logger.LogErrorMessage(ex, $"RSA signing failed for key {keyContainer.Identifier}");
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
                _logger.LogDebugMessage($"ECDSA signature created successfully for key {keyContainer.Identifier}");
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
                _logger.LogErrorMessage(ex, $"ECDSA signing failed for key {keyContainer.Identifier}");
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
                rsa.ImportRSAPublicKey(keyContainer.PublicKeyData, out _);
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
                _logger.LogDebugMessage($"RSA signature verification completed for key {keyContainer.Identifier}: Valid={isValid}");
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
                _logger.LogWarning(ex, $"RSA signature verification failed for key {keyContainer.Identifier}");
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
                ecdsa.ImportSubjectPublicKeyInfo(keyContainer.PublicKeyData, out _);
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
                _logger.LogDebugMessage($"ECDSA signature verification completed for key {keyContainer.Identifier}: Valid={isValid}");
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
                _logger.LogWarning(ex, $"ECDSA signature verification failed for key {keyContainer.Identifier}");
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
                    _verificationCache.Remove(key);
                }

                if (expiredEntries.Count > 0)
                {
                    _logger.LogDebugMessage($"Cleaned up {expiredEntries.Count} expired signature verification cache entries");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during signature verification cache cleanup");
        }
    }

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
            _logger.LogDebugMessage("Signature Verifier disposed");
        }
    }
}

#region Supporting Types

/// <summary>
/// Result of signature algorithm validation
/// </summary>
public sealed class SignatureAlgorithmValidationResult
{
    public required string Algorithm { get; init; }
    public int KeySize { get; init; }
    public required string Context { get; init; }
    public DateTimeOffset ValidationTime { get; init; }
    public bool IsApproved { get; set; }
    public List<string> SecurityIssues { get; } = [];
    public List<string> Recommendations { get; } = [];
}

/// <summary>
/// Cached signature validation result
/// </summary>
internal sealed class CachedSignatureValidation
{
    public bool IsValid { get; init; }
    public DateTimeOffset ValidationTime { get; init; }
}

#endregion
