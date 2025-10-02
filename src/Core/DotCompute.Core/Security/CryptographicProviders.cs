// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;

namespace DotCompute.Core.Security;

/// <summary>
/// Provides cryptographic algorithm implementations and providers.
/// Handles encryption, decryption, signing, and verification operations.
/// </summary>
internal sealed class CryptographicProviders : IDisposable
{
    private readonly ILogger<CryptographicProviders> _logger;
    private readonly CryptographicConfiguration _configuration;
    private readonly RandomNumberGenerator _randomGenerator;
    private bool _disposed;

    // Approved cryptographic algorithms
    private static readonly HashSet<string> ApprovedCiphers = new(StringComparer.OrdinalIgnoreCase)
    {
        "AES-256-GCM", "AES-256-CBC", "AES-192-GCM", "AES-192-CBC", "ChaCha20-Poly1305"
    };

    private static readonly HashSet<string> ApprovedHashAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        "SHA-256", "SHA-384", "SHA-512", "SHA3-256", "SHA3-384", "SHA3-512", "BLAKE2b"
    };

    private static readonly HashSet<string> WeakAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        "DES", "3DES", "RC4", "MD5", "SHA-1", "RSA-1024"
    };
    /// <summary>
    /// Initializes a new instance of the CryptographicProviders class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>

    public CryptographicProviders(
        ILogger<CryptographicProviders> logger,
        CryptographicConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _randomGenerator = RandomNumberGenerator.Create();

        _logger.LogInfoMessage("CryptographicProviders initialized");
    }

    /// <summary>
    /// Encrypts data using the specified algorithm and key.
    /// </summary>
    public async Task<EncryptionResult> EncryptAsync(
        ReadOnlyMemory<byte> data,
        SecureKeyContainer keyContainer,
        string algorithm,
        ReadOnlyMemory<byte>? associatedData = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(keyContainer);
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);

        var result = new EncryptionResult
        {
            KeyIdentifier = keyContainer.Identifier,
            Algorithm = algorithm,
            OperationTime = DateTimeOffset.UtcNow
        };

        try
        {
            _logger.LogDebugMessage($"Encrypting data: Algorithm={algorithm}, DataSize={data.Length}");

            // Validate algorithm
            if (!ApprovedCiphers.Contains(algorithm))
            {
                result.ErrorMessage = $"Algorithm not approved: {algorithm}";
                return result;
            }

            // Generate random nonce/IV
            var nonce = new byte[GetNonceSize(algorithm)];
            _randomGenerator.GetBytes(nonce);

            // Perform encryption based on algorithm
            result = algorithm.ToUpperInvariant() switch
            {
                "AES-256-GCM" or "AES-192-GCM" => await EncryptAesGcmAsync(data, keyContainer, nonce, associatedData, result),
                "AES-256-CBC" or "AES-192-CBC" => await EncryptAesCbcAsync(data, keyContainer, nonce, result),
                "CHACHA20-POLY1305" => await EncryptChaCha20Poly1305Async(data, keyContainer, nonce, associatedData, result),
                _ => throw new NotSupportedException($"Algorithm not implemented: {algorithm}")
            };

            result.Nonce = nonce;
            result.IsSuccessful = true;

            _logger.LogDebugMessage($"Data encryption completed successfully: Algorithm={algorithm}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Encryption failed: Algorithm={algorithm}");
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Decrypts data using the specified algorithm and key.
    /// </summary>
    public async Task<DecryptionResult> DecryptAsync(
        ReadOnlyMemory<byte> encryptedData,
        SecureKeyContainer keyContainer,
        string algorithm,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte>? tag = null,
        ReadOnlyMemory<byte>? associatedData = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(keyContainer);
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);

        var result = new DecryptionResult
        {
            KeyIdentifier = keyContainer.Identifier,
            Algorithm = algorithm,
            OperationTime = DateTimeOffset.UtcNow
        };

        try
        {
            _logger.LogDebugMessage($"Decrypting data: Algorithm={algorithm}, DataSize={encryptedData.Length}");

            // Validate algorithm
            if (!ApprovedCiphers.Contains(algorithm))
            {
                result.ErrorMessage = $"Algorithm not approved: {algorithm}";
                return result;
            }

            // Perform decryption based on algorithm
            result = algorithm.ToUpperInvariant() switch
            {
                "AES-256-GCM" or "AES-192-GCM" => await DecryptAesGcmAsync(encryptedData, keyContainer, nonce, tag, associatedData, result),
                "AES-256-CBC" or "AES-192-CBC" => await DecryptAesCbcAsync(encryptedData, keyContainer, nonce, result),
                "CHACHA20-POLY1305" => await DecryptChaCha20Poly1305Async(encryptedData, keyContainer, nonce, tag, associatedData, result),
                _ => throw new NotSupportedException($"Algorithm not implemented: {algorithm}")
            };

            result.IsSuccessful = true;

            _logger.LogDebugMessage($"Data decryption completed successfully: Algorithm={algorithm}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Decryption failed: Algorithm={algorithm}");
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Creates a digital signature for the provided data.
    /// </summary>
    public async Task<SignatureResult> SignDataAsync(
        ReadOnlyMemory<byte> data,
        SecureKeyContainer keyContainer,
        string hashAlgorithm = "SHA-256")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(keyContainer);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashAlgorithm);

        var result = new SignatureResult
        {
            KeyIdentifier = keyContainer.Identifier,
            HashAlgorithm = hashAlgorithm,
            OperationTime = DateTimeOffset.UtcNow
        };

        try
        {
            _logger.LogDebugMessage($"Signing data: HashAlgorithm={hashAlgorithm}, DataSize={data.Length}");

            // Validate hash algorithm
            if (!ApprovedHashAlgorithms.Contains(hashAlgorithm))
            {
                result.ErrorMessage = $"Hash algorithm not approved: {hashAlgorithm}";
                return result;
            }

            if (keyContainer.KeyType is not KeyType.RSA and not KeyType.ECDSA)
            {
                result.ErrorMessage = $"Key type not suitable for signing: {keyContainer.KeyType}";
                return result;
            }

            // Perform signing based on key type
            result = keyContainer.KeyType switch
            {
                KeyType.RSA => await SignWithRsaAsync(data, keyContainer, hashAlgorithm, result),
                KeyType.ECDSA => await SignWithEcdsaAsync(data, keyContainer, hashAlgorithm, result),
                _ => throw new NotSupportedException($"Key type not supported for signing: {keyContainer.KeyType}")
            };

            result.IsSuccessful = true;

            _logger.LogDebugMessage($"Data signing completed successfully: HashAlgorithm={hashAlgorithm}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Signing failed: HashAlgorithm={hashAlgorithm}");
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Verifies a digital signature.
    /// </summary>
    public async Task<SignatureVerificationResult> VerifySignatureAsync(
        ReadOnlyMemory<byte> data,
        ReadOnlyMemory<byte> signature,
        SecureKeyContainer keyContainer,
        string hashAlgorithm = "SHA-256")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(keyContainer);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashAlgorithm);

        var result = new SignatureVerificationResult
        {
            KeyIdentifier = keyContainer.Identifier,
            HashAlgorithm = hashAlgorithm,
            OperationTime = DateTimeOffset.UtcNow
        };

        try
        {
            _logger.LogDebugMessage($"Verifying signature: HashAlgorithm={hashAlgorithm}, DataSize={data.Length}");

            // Validate hash algorithm
            if (!ApprovedHashAlgorithms.Contains(hashAlgorithm))
            {
                result.ErrorMessage = $"Hash algorithm not approved: {hashAlgorithm}";
                return result;
            }

            // Perform verification based on key type
            var isValid = keyContainer.KeyType switch
            {
                KeyType.RSA => await VerifyWithRsaAsync(data, signature, keyContainer, hashAlgorithm),
                KeyType.ECDSA => await VerifyWithEcdsaAsync(data, signature, keyContainer, hashAlgorithm),
                _ => throw new NotSupportedException($"Key type not supported for verification: {keyContainer.KeyType}")
            };

            result.IsValid = isValid;
            result.IsSuccessful = true;

            _logger.LogDebugMessage($"Signature verification completed: Valid={isValid}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Signature verification failed: HashAlgorithm={hashAlgorithm}");
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Validates if a cryptographic algorithm is approved for use.
    /// </summary>
    public static AlgorithmValidationResult ValidateAlgorithm(string algorithm, int keySize, string context)
    {
        var result = new AlgorithmValidationResult
        {
            Algorithm = algorithm,
            KeySize = keySize,
            Context = context,
            IsApproved = true
        };

        // Check for weak algorithms
        if (WeakAlgorithms.Contains(algorithm))
        {
            result.IsApproved = false;
            result.Issues.Add($"Algorithm '{algorithm}' is considered weak and deprecated");
        }

        // Validate specific algorithm requirements
        if (algorithm.StartsWith("AES", StringComparison.OrdinalIgnoreCase))
        {
            if (keySize < 128)
            {
                result.IsApproved = false;
                result.Issues.Add($"AES key size {keySize} is too small (minimum 128 bits)");
            }
        }
        else if (algorithm.StartsWith("RSA", StringComparison.OrdinalIgnoreCase))
        {
            if (keySize < 2048)
            {
                result.IsApproved = false;
                result.Issues.Add($"RSA key size {keySize} is too small (minimum 2048 bits)");
            }
        }

        // Check if algorithm is in approved list
        var isInApprovedCiphers = ApprovedCiphers.Contains(algorithm);
        var isInApprovedHashes = ApprovedHashAlgorithms.Contains(algorithm);

        if (!isInApprovedCiphers && !isInApprovedHashes)
        {
            result.IsApproved = false;
            result.Issues.Add($"Algorithm '{algorithm}' is not in the approved algorithms list");
        }

        return result;
    }

    // Private implementation methods

    private async Task<EncryptionResult> EncryptAesGcmAsync(
        ReadOnlyMemory<byte> data,
        SecureKeyContainer keyContainer,
        byte[] nonce,
        ReadOnlyMemory<byte>? associatedData,
        EncryptionResult result)
    {
        using var aes = Aes.Create();
        aes.Key = keyContainer.KeyMaterial.ToArray();
        aes.Mode = CipherMode.GCM;

        var tag = new byte[16]; // GCM tag size
        var ciphertext = new byte[data.Length];

        aes.EncryptGcm(data.Span, ciphertext, tag, nonce, associatedData?.Span ?? ReadOnlySpan<byte>.Empty);

        result.EncryptedData = ciphertext;
        result.Tag = tag;
        return result;
    }

    private static async Task<EncryptionResult> EncryptAesCbcAsync(
        ReadOnlyMemory<byte> data,
        SecureKeyContainer keyContainer,
        byte[] nonce,
        EncryptionResult result)
    {
        using var aes = Aes.Create();
        aes.Key = keyContainer.KeyMaterial.ToArray();
        aes.IV = nonce;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var ciphertext = encryptor.TransformFinalBlock(data.ToArray(), 0, data.Length);

        result.EncryptedData = ciphertext;
        return result;
    }

    private async Task<EncryptionResult> EncryptChaCha20Poly1305Async(
        ReadOnlyMemory<byte> data,
        SecureKeyContainer keyContainer,
        byte[] nonce,
        ReadOnlyMemory<byte>? associatedData,
        EncryptionResult result)
        // ChaCha20Poly1305 implementation would go here
        // For now, throw not implemented as .NET Core doesn't have built-in ChaCha20
        => throw new NotImplementedException("ChaCha20-Poly1305 implementation pending");

    private async Task<DecryptionResult> DecryptAesGcmAsync(
        ReadOnlyMemory<byte> encryptedData,
        SecureKeyContainer keyContainer,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte>? tag,
        ReadOnlyMemory<byte>? associatedData,
        DecryptionResult result)
    {
        if (!tag.HasValue)
        {
            result.ErrorMessage = "Tag is required for AES-GCM decryption";
            return result;
        }

        using var aes = Aes.Create();
        aes.Key = keyContainer.KeyMaterial.ToArray();
        aes.Mode = CipherMode.GCM;

        var plaintext = new byte[encryptedData.Length];

        try
        {
            aes.DecryptGcm(encryptedData.Span, plaintext, tag.Value.Span, nonce.Span, associatedData?.Span ?? ReadOnlySpan<byte>.Empty);
            result.DecryptedData = plaintext;
        }
        catch (CryptographicException ex)
        {
            result.ErrorMessage = $"GCM decryption failed: {ex.Message}";
        }

        return result;
    }

    private static async Task<DecryptionResult> DecryptAesCbcAsync(
        ReadOnlyMemory<byte> encryptedData,
        SecureKeyContainer keyContainer,
        ReadOnlyMemory<byte> nonce,
        DecryptionResult result)
    {
        using var aes = Aes.Create();
        aes.Key = keyContainer.KeyMaterial.ToArray();
        aes.IV = nonce.ToArray();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plaintext = decryptor.TransformFinalBlock(encryptedData.ToArray(), 0, encryptedData.Length);

        result.DecryptedData = plaintext;
        return result;
    }

    private async Task<DecryptionResult> DecryptChaCha20Poly1305Async(
        ReadOnlyMemory<byte> encryptedData,
        SecureKeyContainer keyContainer,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte>? tag,
        ReadOnlyMemory<byte>? associatedData,
        DecryptionResult result)
        // ChaCha20Poly1305 implementation would go here
        => throw new NotImplementedException("ChaCha20-Poly1305 implementation pending");

    private static async Task<SignatureResult> SignWithRsaAsync(
        ReadOnlyMemory<byte> data,
        SecureKeyContainer keyContainer,
        string hashAlgorithm,
        SignatureResult result)
    {
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(keyContainer.KeyMaterial.Span, out _);

        var hashName = GetHashAlgorithmName(hashAlgorithm);
        var signature = rsa.SignData(data.ToArray(), hashName, RSASignaturePadding.Pkcs1);

        result.Signature = signature;
        return result;
    }

    private static async Task<SignatureResult> SignWithEcdsaAsync(
        ReadOnlyMemory<byte> data,
        SecureKeyContainer keyContainer,
        string hashAlgorithm,
        SignatureResult result)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(keyContainer.KeyMaterial.Span, out _);

        var hashName = GetHashAlgorithmName(hashAlgorithm);
        var signature = ecdsa.SignData(data.ToArray(), hashName);

        result.Signature = signature;
        return result;
    }

    private static async Task<bool> VerifyWithRsaAsync(
        ReadOnlyMemory<byte> data,
        ReadOnlyMemory<byte> signature,
        SecureKeyContainer keyContainer,
        string hashAlgorithm)
    {
        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(keyContainer.KeyMaterial.Span, out _);

        var hashName = GetHashAlgorithmName(hashAlgorithm);
        return rsa.VerifyData(data.ToArray(), signature.ToArray(), hashName, RSASignaturePadding.Pkcs1);
    }

    private static async Task<bool> VerifyWithEcdsaAsync(
        ReadOnlyMemory<byte> data,
        ReadOnlyMemory<byte> signature,
        SecureKeyContainer keyContainer,
        string hashAlgorithm)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(keyContainer.KeyMaterial.Span, out _);

        var hashName = GetHashAlgorithmName(hashAlgorithm);
        return ecdsa.VerifyData(data.ToArray(), signature.ToArray(), hashName);
    }

    private static HashAlgorithmName GetHashAlgorithmName(string algorithm)
    {
        return algorithm.ToUpperInvariant() switch
        {
            "SHA-256" or "SHA256" => HashAlgorithmName.SHA256,
            "SHA-384" or "SHA384" => HashAlgorithmName.SHA384,
            "SHA-512" or "SHA512" => HashAlgorithmName.SHA512,
            _ => throw new NotSupportedException($"Hash algorithm not supported: {algorithm}")
        };
    }

    private static int GetNonceSize(string algorithm)
    {
        return algorithm.ToUpperInvariant() switch
        {
            "AES-256-GCM" or "AES-192-GCM" => 12, // 96 bits for GCM
            "AES-256-CBC" or "AES-192-CBC" => 16, // 128 bits for CBC
            "CHACHA20-POLY1305" => 12, // 96 bits for ChaCha20
            _ => 16 // Default
        };
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _randomGenerator?.Dispose();
            _disposed = true;
        }
    }
}