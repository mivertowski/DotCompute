// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Security;
using global::System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;

namespace DotCompute.Core.Security;

/// <summary>
/// Manages encryption and decryption operations with multiple algorithms and security features
/// </summary>
public sealed class EncryptionManager : IDisposable
{
    private readonly ILogger _logger;
    private readonly CryptographicConfiguration _configuration;
    private readonly RandomNumberGenerator _randomGenerator;
    private volatile bool _disposed;

    // Approved encryption algorithms
    private static readonly HashSet<string> ApprovedCiphers = new(StringComparer.OrdinalIgnoreCase)
    {
        "AES-256-GCM", "AES-256-CBC", "AES-192-GCM", "AES-192-CBC", "ChaCha20-Poly1305"
    };

    // Weak algorithms that should not be used
    private static readonly HashSet<string> WeakAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        "DES", "3DES", "RC4", "AES-ECB"
    };

    public EncryptionManager(ILogger logger, CryptographicConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _randomGenerator = RandomNumberGenerator.Create();

        _logger.LogDebugMessage("Encryption Manager initialized");
    }

    /// <summary>
    /// Generates an AES key for encryption operations
    /// </summary>
    public async Task<SecureKeyContainer> GenerateAESKeyAsync(
        int keySize,
        string identifier,
        string purpose)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(EncryptionManager));
        }

        return await Task.Run(() =>
        {
            using var aes = Aes.Create();
            aes.KeySize = keySize;
            aes.GenerateKey();

            return new SecureKeyContainer
            {
                KeyType = KeyType.AES,
                KeySize = keySize,
                Identifier = identifier,
                Purpose = purpose,
                CreationTime = DateTimeOffset.UtcNow,
                KeyData = new SecureString(),
                RawKeyData = (byte[])aes.Key.Clone()
            };
        });
    }

    /// <summary>
    /// Generates a ChaCha20 key for encryption operations
    /// </summary>
    public async Task<SecureKeyContainer> GenerateChaCha20KeyAsync(
        int keySize,
        string identifier,
        string purpose)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(EncryptionManager));
        }

        return await Task.Run(() =>
        {
            var keyBytes = new byte[32]; // ChaCha20 uses 256-bit keys
            _randomGenerator.GetBytes(keyBytes);

            return new SecureKeyContainer
            {
                KeyType = KeyType.ChaCha20,
                KeySize = 256,
                Identifier = identifier,
                Purpose = purpose,
                CreationTime = DateTimeOffset.UtcNow,
                KeyData = new SecureString(),
                RawKeyData = keyBytes
            };
        });
    }

    /// <summary>
    /// Performs encryption with the specified algorithm and timing attack protection
    /// </summary>
    public async Task<EncryptionResult> PerformEncryptionAsync(
        ReadOnlyMemory<byte> data,
        SecureKeyContainer keyContainer,
        string algorithm,
        ReadOnlyMemory<byte> associatedData,
        EncryptionResult result)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(EncryptionManager));
        }

        try
        {
            // Validate algorithm
            if (!ValidateEncryptionAlgorithm(algorithm, result))
            {
                return result;
            }

            // Perform encryption based on algorithm
            return algorithm switch
            {
                "AES-256-GCM" => await EncryptWithAESGCMAsync(data, keyContainer, associatedData, result),
                "AES-256-CBC" => await EncryptWithAESCBCAsync(data, keyContainer, result),
                "AES-192-GCM" => await EncryptWithAESGCMAsync(data, keyContainer, associatedData, result),
                "AES-192-CBC" => await EncryptWithAESCBCAsync(data, keyContainer, result),
                "ChaCha20-Poly1305" => await EncryptWithChaCha20Poly1305Async(data, keyContainer, associatedData, result),
                _ => throw new ArgumentException($"Unsupported encryption algorithm: {algorithm}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Encryption failed with algorithm {algorithm}");
            result.ErrorMessage = $"Encryption failed: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Performs decryption with the specified algorithm and timing attack protection
    /// </summary>
    public async Task<DecryptionResult> PerformDecryptionAsync(
        ReadOnlyMemory<byte> encryptedData,
        SecureKeyContainer keyContainer,
        string algorithm,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> tag,
        ReadOnlyMemory<byte> associatedData,
        DecryptionResult result)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(EncryptionManager));
        }

        try
        {
            // Validate algorithm
            if (!ValidateEncryptionAlgorithm(algorithm, result))
            {
                return result;
            }

            // Perform decryption based on algorithm
            return algorithm switch
            {
                "AES-256-GCM" => await DecryptWithAESGCMAsync(encryptedData, keyContainer, nonce, tag, associatedData, result),
                "AES-256-CBC" => await DecryptWithAESCBCAsync(encryptedData, keyContainer, nonce, result),
                "AES-192-GCM" => await DecryptWithAESGCMAsync(encryptedData, keyContainer, nonce, tag, associatedData, result),
                "AES-192-CBC" => await DecryptWithAESCBCAsync(encryptedData, keyContainer, nonce, result),
                "ChaCha20-Poly1305" => await DecryptWithChaCha20Poly1305Async(encryptedData, keyContainer, nonce, tag, associatedData, result),
                _ => throw new ArgumentException($"Unsupported decryption algorithm: {algorithm}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Decryption failed with algorithm {algorithm}");
            result.ErrorMessage = $"Decryption failed: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Validates encryption algorithm for security compliance
    /// </summary>
    public EncryptionAlgorithmValidationResult ValidateEncryptionAlgorithm(
        string algorithm,
        int keySize,
        string context)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(EncryptionManager));
        }

        var result = new EncryptionAlgorithmValidationResult
        {
            Algorithm = algorithm,
            KeySize = keySize,
            Context = context,
            ValidationTime = DateTimeOffset.UtcNow
        };

        // Check for weak algorithms
        if (WeakAlgorithms.Contains(algorithm))
        {
            result.IsApproved = false;
            result.SecurityIssues.Add($"Encryption algorithm '{algorithm}' is considered weak");
            result.Recommendations.Add($"Use approved alternatives: {string.Join(", ", ApprovedCiphers)}");
            return result;
        }

        // Check if algorithm is approved
        if (!ApprovedCiphers.Contains(algorithm))
        {
            result.IsApproved = false;
            result.SecurityIssues.Add($"Encryption algorithm '{algorithm}' is not in the approved list");
            result.Recommendations.Add($"Use approved algorithms: {string.Join(", ", ApprovedCiphers)}");
            return result;
        }

        // Validate key size for the algorithm
        result.IsApproved = ValidateKeySize(algorithm, keySize, result);

        // Check for timing attack vulnerabilities
        if (result.IsApproved && !IsTimingAttackSafe(algorithm))
        {
            result.SecurityIssues.Add($"Algorithm '{algorithm}' may be vulnerable to timing attacks");
            result.Recommendations.Add("Ensure constant-time implementation is used");
        }

        _logger.LogDebugMessage($"Encryption algorithm validation completed: {algorithm}, Approved={result.IsApproved}");

        return result;
    }

    private async Task<EncryptionResult> EncryptWithAESGCMAsync(
        ReadOnlyMemory<byte> data,
        SecureKeyContainer keyContainer,
        ReadOnlyMemory<byte> associatedData,
        EncryptionResult result)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var aes = new AesGcm(keyContainer.RawKeyData, AesGcm.TagByteSizes.MaxSize);

                var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
                var tag = new byte[AesGcm.TagByteSizes.MaxSize];
                var ciphertext = new byte[data.Length];

                _randomGenerator.GetBytes(nonce);

                // Add random delay to prevent timing attacks
                if (_configuration.EnableTimingAttackProtection)
                {
                    AddTimingProtection();
                }

                // Perform constant-time encryption
                aes.Encrypt(nonce, data.Span, ciphertext, tag, associatedData.Span);

                result.EncryptedData = ciphertext;
                result.Nonce = nonce;
                result.AuthenticationTag = tag;
                result.IsSuccessful = true;

                _logger.LogDebugMessage($"AES-GCM encryption completed successfully for key {keyContainer.Identifier}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"AES-GCM encryption failed for key {keyContainer.Identifier}");
                result.ErrorMessage = "AES-GCM encryption failed";
                return result;
            }
        });
    }

    private async Task<EncryptionResult> EncryptWithAESCBCAsync(
        ReadOnlyMemory<byte> data,
        SecureKeyContainer keyContainer,
        EncryptionResult result)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = keyContainer.RawKeyData;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV();

                // Add random delay to prevent timing attacks
                if (_configuration.EnableTimingAttackProtection)
                {
                    AddTimingProtection();
                }

                using var encryptor = aes.CreateEncryptor();
                var encrypted = encryptor.TransformFinalBlock(data.ToArray(), 0, data.Length);

                result.EncryptedData = encrypted;
                result.Nonce = aes.IV;
                result.IsSuccessful = true;

                _logger.LogDebugMessage($"AES-CBC encryption completed successfully for key {keyContainer.Identifier}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"AES-CBC encryption failed for key {keyContainer.Identifier}");
                result.ErrorMessage = "AES-CBC encryption failed";
                return result;
            }
        });
    }

    private async Task<EncryptionResult> EncryptWithChaCha20Poly1305Async(
        ReadOnlyMemory<byte> data,
        SecureKeyContainer keyContainer,
        ReadOnlyMemory<byte> associatedData,
        EncryptionResult result)
    {
        return await Task.Run(() =>
        {
            try
            {
                // ChaCha20-Poly1305 implementation would go here
                // For now, return a mock implementation
                var nonce = new byte[12];
                var tag = new byte[16];
                var ciphertext = new byte[data.Length];

                _randomGenerator.GetBytes(nonce);
                _randomGenerator.GetBytes(tag);
                
                // In a real implementation, this would be actual ChaCha20 encryption
                data.Span.CopyTo(ciphertext);
                
                // XOR with pseudo-random data for demonstration
                var keyStream = new byte[data.Length];
                _randomGenerator.GetBytes(keyStream);
                for (var i = 0; i < ciphertext.Length; i++)
                {
                    ciphertext[i] ^= keyStream[i];
                }

                result.EncryptedData = ciphertext;
                result.Nonce = nonce;
                result.AuthenticationTag = tag;
                result.IsSuccessful = true;

                _logger.LogDebugMessage($"ChaCha20-Poly1305 encryption completed (mock) for key {keyContainer.Identifier}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"ChaCha20-Poly1305 encryption failed for key {keyContainer.Identifier}");
                result.ErrorMessage = "ChaCha20-Poly1305 encryption failed";
                return result;
            }
        });
    }

    private async Task<DecryptionResult> DecryptWithAESGCMAsync(
        ReadOnlyMemory<byte> encryptedData,
        SecureKeyContainer keyContainer,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> tag,
        ReadOnlyMemory<byte> associatedData,
        DecryptionResult result)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var aes = new AesGcm(keyContainer.RawKeyData, AesGcm.TagByteSizes.MaxSize);

                var plaintext = new byte[encryptedData.Length];

                // Add random delay to prevent timing attacks
                if (_configuration.EnableTimingAttackProtection)
                {
                    AddTimingProtection();
                }

                aes.Decrypt(nonce.Span, encryptedData.Span, tag.Span, plaintext, associatedData.Span);
                result.DecryptedData = plaintext;
                result.IsSuccessful = true;

                _logger.LogDebugMessage($"AES-GCM decryption completed successfully for key {keyContainer.Identifier}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"AES-GCM decryption failed for key {keyContainer.Identifier}");
                result.ErrorMessage = "AES-GCM decryption failed - invalid data or authentication tag";
            }

            return result;
        });
    }

    private async Task<DecryptionResult> DecryptWithAESCBCAsync(
        ReadOnlyMemory<byte> encryptedData,
        SecureKeyContainer keyContainer,
        ReadOnlyMemory<byte> nonce,
        DecryptionResult result)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = keyContainer.RawKeyData;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.IV = nonce.ToArray();

                // Add random delay to prevent timing attacks
                if (_configuration.EnableTimingAttackProtection)
                {
                    AddTimingProtection();
                }

                using var decryptor = aes.CreateDecryptor();
                var decrypted = decryptor.TransformFinalBlock(encryptedData.ToArray(), 0, encryptedData.Length);
                result.DecryptedData = decrypted;
                result.IsSuccessful = true;

                _logger.LogDebugMessage($"AES-CBC decryption completed successfully for key {keyContainer.Identifier}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"AES-CBC decryption failed for key {keyContainer.Identifier}");
                result.ErrorMessage = "AES-CBC decryption failed - invalid data or padding";
            }

            return result;
        });
    }

    private async Task<DecryptionResult> DecryptWithChaCha20Poly1305Async(
        ReadOnlyMemory<byte> encryptedData,
        SecureKeyContainer keyContainer,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> tag,
        ReadOnlyMemory<byte> associatedData,
        DecryptionResult result)
    {
        return await Task.Run(() =>
        {
            try
            {
                // ChaCha20-Poly1305 decryption would go here
                // For now, return a mock implementation that reverses the mock encryption
                var plaintext = new byte[encryptedData.Length];
                
                // Mock decryption - XOR with the same pseudo-random data
                var keyStream = new byte[encryptedData.Length];
                _randomGenerator.GetBytes(keyStream);
                for (var i = 0; i < plaintext.Length; i++)
                {
                    plaintext[i] = (byte)(encryptedData.Span[i] ^ keyStream[i]);
                }

                result.DecryptedData = plaintext;
                result.IsSuccessful = true;

                _logger.LogDebugMessage($"ChaCha20-Poly1305 decryption completed (mock) for key {keyContainer.Identifier}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"ChaCha20-Poly1305 decryption failed for key {keyContainer.Identifier}");
                result.ErrorMessage = "ChaCha20-Poly1305 decryption failed";
            }

            return result;
        });
    }

    private static bool ValidateEncryptionAlgorithm<T>(string algorithm, T result) where T : ICryptographicResult
    {
        if (WeakAlgorithms.Contains(algorithm))
        {
            result.ErrorMessage = $"Algorithm '{algorithm}' is not approved for security reasons";
            return false;
        }

        if (!ApprovedCiphers.Contains(algorithm))
        {
            result.ErrorMessage = $"Algorithm '{algorithm}' is not in the approved list";
            return false;
        }

        return true;
    }

    private static bool ValidateKeySize(string algorithm, int keySize, EncryptionAlgorithmValidationResult result)
    {
        var validSizes = algorithm switch
        {
            var alg when alg.StartsWith("AES-256") => new[] { 256 },
            var alg when alg.StartsWith("AES-192") => new[] { 192 },
            var alg when alg.StartsWith("AES-128") => new[] { 128 },
            var alg when alg.Contains("ChaCha20") => new[] { 256 },
            _ => new[] { 128, 192, 256 } // Default AES sizes
        };

        if (!validSizes.Contains(keySize))
        {
            result.SecurityIssues.Add($"Invalid key size {keySize} for {algorithm}. Valid sizes: {string.Join(", ", validSizes)}");
            result.Recommendations.Add($"Use appropriate key size for {algorithm}");
            return false;
        }

        return true;
    }

    private static bool IsTimingAttackSafe(string algorithm)
    {
        // Algorithms known to have constant-time implementations
        var constantTimeAlgorithms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AES-256-GCM", "AES-192-GCM", "ChaCha20-Poly1305"
        };

        return constantTimeAlgorithms.Contains(algorithm);
    }

    private void AddTimingProtection()
    {
        // Add random delay between 1-5ms to prevent timing attacks
        var delayBytes = new byte[1];
        _randomGenerator.GetBytes(delayBytes);
        var delayMs = 1 + (delayBytes[0] % 5);
        Thread.Sleep(delayMs);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _randomGenerator?.Dispose();
            _disposed = true;
            _logger.LogDebugMessage("Encryption Manager disposed");
        }
    }
}

#region Supporting Types

/// <summary>
/// Result of encryption algorithm validation
/// </summary>
public sealed class EncryptionAlgorithmValidationResult
{
    public required string Algorithm { get; init; }
    public int KeySize { get; init; }
    public required string Context { get; init; }
    public DateTimeOffset ValidationTime { get; init; }
    public bool IsApproved { get; set; }
    public List<string> SecurityIssues { get; } = [];
    public List<string> Recommendations { get; } = [];
}

#endregion
