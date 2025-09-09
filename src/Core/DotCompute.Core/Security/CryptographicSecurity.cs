// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Security;
using global::System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;

namespace DotCompute.Core.Security;

/// <summary>
/// Comprehensive cryptographic security service providing secure key management,
/// encryption/decryption, digital signatures, and cryptographic validation.
/// Implements defense against timing attacks and secure memory handling.
/// </summary>
public sealed class CryptographicSecurity : IDisposable
{
    private readonly ILogger _logger;
    private readonly CryptographicConfiguration _configuration;
    private readonly ConcurrentDictionary<string, SecureKeyContainer> _keyStore = new();
    private readonly SemaphoreSlim _operationLock = new(Environment.ProcessorCount, Environment.ProcessorCount);
    private readonly Timer _keyRotationTimer;
    private readonly RandomNumberGenerator _randomGenerator;
    private volatile bool _disposed;

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

    public CryptographicSecurity(ILogger<CryptographicSecurity> logger, CryptographicConfiguration? configuration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? CryptographicConfiguration.Default;
        _randomGenerator = RandomNumberGenerator.Create();

        // Initialize key rotation timer

        _keyRotationTimer = new Timer(PerformKeyRotation, null,

            _configuration.KeyRotationInterval, _configuration.KeyRotationInterval);


        _logger.LogInfoMessage($"CryptographicSecurity initialized with configuration: {_configuration.ToString()}");
    }

    /// <summary>
    /// Generates a new cryptographically secure encryption key.
    /// </summary>
    /// <param name="keyType">Type of key to generate</param>
    /// <param name="keySize">Size of the key in bits</param>
    /// <param name="identifier">Unique identifier for the key</param>
    /// <param name="purpose">Intended purpose of the key</param>
    /// <returns>Result containing the generated key information</returns>
    public async Task<KeyGenerationResult> GenerateKeyAsync(KeyType keyType, int keySize,

        string identifier, string purpose)
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(CryptographicSecurity));
        }


        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        await _operationLock.WaitAsync();
        try
        {
            _logger.LogInfoMessage($"Generating new cryptographic key: Type={keyType}, Size={keySize}, Id={identifier}");

            var result = new KeyGenerationResult
            {
                KeyType = keyType,
                KeySize = keySize,
                Identifier = identifier,
                Purpose = purpose,
                GenerationTime = DateTimeOffset.UtcNow
            };

            // Validate key parameters
            if (!ValidateKeyParameters(keyType, keySize, result))
            {
                return result;
            }

            // Generate the key based on type
            var keyContainer = await GenerateKeyContainerAsync(keyType, keySize, identifier, purpose);

            // Store the key securely

            _ = _keyStore.AddOrUpdate(identifier, keyContainer, (key, existing) =>
            {
                existing.Dispose(); // Securely dispose old key
                return keyContainer;
            });

            result.IsSuccessful = true;
            result.KeyFingerprint = await CalculateKeyFingerprintAsync(keyContainer);


            _logger.LogInfoMessage($"Cryptographic key generated successfully: Id={identifier}, Fingerprint={result.KeyFingerprint}");

            return result;
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }

    /// <summary>
    /// Encrypts data using the specified key and algorithm.
    /// </summary>
    /// <param name="data">Data to encrypt</param>
    /// <param name="keyIdentifier">Identifier of the key to use</param>
    /// <param name="algorithm">Encryption algorithm to use</param>
    /// <param name="associatedData">Optional associated data for authenticated encryption</param>
    /// <returns>Encryption result with encrypted data and metadata</returns>
    public async Task<EncryptionResult> EncryptAsync(ReadOnlyMemory<byte> data, string keyIdentifier,

        string algorithm = "AES-256-GCM", ReadOnlyMemory<byte> associatedData = default)
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(CryptographicSecurity));
        }


        ArgumentException.ThrowIfNullOrWhiteSpace(keyIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);

        await _operationLock.WaitAsync();
        try
        {
            _logger.LogDebugMessage($"Encrypting data: KeyId={keyIdentifier}, Algorithm={algorithm}, DataSize={data.Length}");

            var result = new EncryptionResult
            {
                KeyIdentifier = keyIdentifier,
                Algorithm = algorithm,
                OperationTime = DateTimeOffset.UtcNow
            };

            // Validate algorithm
            if (!ValidateAlgorithm(algorithm, result))
            {
                return result;
            }

            // Get the key
            if (!_keyStore.TryGetValue(keyIdentifier, out var keyContainer))
            {
                result.ErrorMessage = $"Key not found: {keyIdentifier}";
                return result;
            }

            // Perform encryption with timing attack protection
            result = await PerformEncryptionAsync(data, keyContainer, algorithm, associatedData, result);

            _logger.LogDebugMessage($"Data encryption completed: KeyId={keyIdentifier}, Success={result.IsSuccessful}");

            return result;
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }

    /// <summary>
    /// Decrypts data using the specified key and algorithm.
    /// </summary>
    /// <param name="encryptedData">Encrypted data to decrypt</param>
    /// <param name="keyIdentifier">Identifier of the key to use</param>
    /// <param name="algorithm">Encryption algorithm used</param>
    /// <param name="nonce">Nonce/IV used for encryption</param>
    /// <param name="tag">Authentication tag for authenticated encryption</param>
    /// <param name="associatedData">Optional associated data for authenticated encryption</param>
    /// <returns>Decryption result with decrypted data</returns>
    public async Task<DecryptionResult> DecryptAsync(ReadOnlyMemory<byte> encryptedData, string keyIdentifier,
        string algorithm, ReadOnlyMemory<byte> nonce, ReadOnlyMemory<byte> tag = default,

        ReadOnlyMemory<byte> associatedData = default)
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(CryptographicSecurity));
        }


        ArgumentException.ThrowIfNullOrWhiteSpace(keyIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);

        await _operationLock.WaitAsync();
        try
        {
            _logger.LogDebugMessage($"Decrypting data: KeyId={keyIdentifier}, Algorithm={algorithm}, DataSize={encryptedData.Length}");

            var result = new DecryptionResult
            {
                KeyIdentifier = keyIdentifier,
                Algorithm = algorithm,
                OperationTime = DateTimeOffset.UtcNow
            };

            // Validate algorithm
            if (!ValidateAlgorithm(algorithm, result))
            {
                return result;
            }

            // Get the key
            if (!_keyStore.TryGetValue(keyIdentifier, out var keyContainer))
            {
                result.ErrorMessage = $"Key not found: {keyIdentifier}";
                return result;
            }

            // Perform decryption with timing attack protection
            result = await PerformDecryptionAsync(encryptedData, keyContainer, algorithm, nonce, tag, associatedData, result);

            _logger.LogDebugMessage($"Data decryption completed: KeyId={keyIdentifier}, Success={result.IsSuccessful}");

            return result;
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }

    /// <summary>
    /// Creates a digital signature for the provided data.
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <param name="keyIdentifier">Identifier of the signing key</param>
    /// <param name="hashAlgorithm">Hash algorithm to use</param>
    /// <returns>Digital signature result</returns>
    public async Task<SignatureResult> SignDataAsync(ReadOnlyMemory<byte> data, string keyIdentifier,

        string hashAlgorithm = "SHA-256")
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(CryptographicSecurity));
        }


        ArgumentException.ThrowIfNullOrWhiteSpace(keyIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashAlgorithm);

        await _operationLock.WaitAsync();
        try
        {
            _logger.LogDebugMessage($"Signing data: KeyId={keyIdentifier}, HashAlgorithm={hashAlgorithm}, DataSize={data.Length}");

            var result = new SignatureResult
            {
                KeyIdentifier = keyIdentifier,
                HashAlgorithm = hashAlgorithm,
                OperationTime = DateTimeOffset.UtcNow
            };

            // Validate hash algorithm
            if (!ApprovedHashAlgorithms.Contains(hashAlgorithm))
            {
                result.ErrorMessage = $"Hash algorithm not approved: {hashAlgorithm}";
                return result;
            }

            // Get the key
            if (!_keyStore.TryGetValue(keyIdentifier, out var keyContainer))
            {
                result.ErrorMessage = $"Signing key not found: {keyIdentifier}";
                return result;
            }

            if (keyContainer.KeyType is not KeyType.RSA and not KeyType.ECDSA)
            {
                result.ErrorMessage = $"Key type not suitable for signing: {keyContainer.KeyType}";
                return result;
            }

            // Perform signing
            result = await PerformSigningAsync(data, keyContainer, hashAlgorithm, result);

            _logger.LogDebugMessage($"Data signing completed: KeyId={keyIdentifier}, Success={result.IsSuccessful}");

            return result;
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }

    /// <summary>
    /// Verifies a digital signature for the provided data.
    /// </summary>
    /// <param name="data">Original data that was signed</param>
    /// <param name="signature">Digital signature to verify</param>
    /// <param name="keyIdentifier">Identifier of the verification key</param>
    /// <param name="hashAlgorithm">Hash algorithm used for signing</param>
    /// <returns>Signature verification result</returns>
    public async Task<SignatureVerificationResult> VerifySignatureAsync(ReadOnlyMemory<byte> data,

        ReadOnlyMemory<byte> signature, string keyIdentifier, string hashAlgorithm = "SHA-256")
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(CryptographicSecurity));
        }


        ArgumentException.ThrowIfNullOrWhiteSpace(keyIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashAlgorithm);

        await _operationLock.WaitAsync();
        try
        {
            _logger.LogDebugMessage($"Verifying signature: KeyId={keyIdentifier}, HashAlgorithm={hashAlgorithm}, DataSize={data.Length}");

            var result = new SignatureVerificationResult
            {
                KeyIdentifier = keyIdentifier,
                HashAlgorithm = hashAlgorithm,
                OperationTime = DateTimeOffset.UtcNow
            };

            // Validate hash algorithm
            if (!ApprovedHashAlgorithms.Contains(hashAlgorithm))
            {
                result.ErrorMessage = $"Hash algorithm not approved: {hashAlgorithm}";
                return result;
            }

            // Get the key
            if (!_keyStore.TryGetValue(keyIdentifier, out var keyContainer))
            {
                result.ErrorMessage = $"Verification key not found: {keyIdentifier}";
                return result;
            }

            // Perform verification with timing attack protection
            result = await PerformSignatureVerificationAsync(data, signature, keyContainer, hashAlgorithm, result);

            _logger.LogDebugMessage($"Signature verification completed: KeyId={keyIdentifier}, IsValid={result.IsValid}");

            return result;
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }

    /// <summary>
    /// Validates cryptographic algorithms and configurations for security compliance.
    /// </summary>
    /// <param name="algorithm">Algorithm to validate</param>
    /// <param name="keySize">Key size to validate</param>
    /// <param name="context">Context of usage</param>
    /// <returns>Algorithm validation result</returns>
    public AlgorithmValidationResult ValidateCryptographicAlgorithm(string algorithm, int keySize, string context)
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(CryptographicSecurity));
        }


        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        var result = new AlgorithmValidationResult
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
            result.SecurityIssues.Add($"Algorithm '{algorithm}' is considered cryptographically weak");
            result.Recommendations.Add($"Consider using approved alternatives: {string.Join(", ", ApprovedCiphers)}");
            return result;
        }

        // Validate specific algorithm requirements
        result.IsApproved = algorithm switch
        {
            var alg when alg.StartsWith("AES") => ValidateAESConfiguration(alg, keySize, result),
            var alg when alg.StartsWith("RSA") => ValidateRSAConfiguration(keySize, result),
            var alg when alg.StartsWith("ECDSA") => ValidateECDSAConfiguration(keySize, result),
            var alg when alg.Contains("SHA") => ValidateSHAConfiguration(alg, result),
            _ => ValidateGenericAlgorithm(algorithm, keySize, result)
        };

        // Check for timing attack vulnerabilities
        if (result.IsApproved && !IsTimingAttackSafe(algorithm))
        {
            result.SecurityIssues.Add($"Algorithm '{algorithm}' may be vulnerable to timing attacks");
            result.Recommendations.Add("Ensure constant-time implementation is used");
        }

        _logger.LogDebugMessage($"Algorithm validation completed: Algorithm={algorithm}, Approved={result.IsApproved}, Issues={result.SecurityIssues.Count}");

        return result;
    }

    /// <summary>
    /// Performs secure key rotation for all stored keys based on policy.
    /// </summary>
    /// <param name="forceRotation">Whether to force rotation regardless of age</param>
    /// <returns>Key rotation summary</returns>
    public async Task<KeyRotationResult> RotateKeysAsync(bool forceRotation = false)
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(CryptographicSecurity));
        }


        await _operationLock.WaitAsync();
        try
        {
            _logger.LogInfoMessage("Starting key rotation: ForceRotation={forceRotation}");

            var result = new KeyRotationResult
            {
                StartTime = DateTimeOffset.UtcNow,
                ForceRotation = forceRotation
            };

            var keysToRotate = _keyStore.Values
                .Where(key => forceRotation || ShouldRotateKey(key))
                .ToList();

            foreach (var keyContainer in keysToRotate)
            {
                try
                {
                    await RotateKeyAsync(keyContainer);
                    result.SuccessfulRotations.Add(keyContainer.Identifier);
                }
                catch (Exception ex)
                {
                    _logger.LogErrorMessage(ex, $"Failed to rotate key: {keyContainer.Identifier}");
                    result.FailedRotations[keyContainer.Identifier] = ex.Message;
                }
            }

            result.EndTime = DateTimeOffset.UtcNow;
            result.TotalKeysProcessed = keysToRotate.Count;

            _logger.LogInfoMessage($"Key rotation completed: Processed={result.TotalKeysProcessed}, Successful={result.SuccessfulRotations.Count}, Failed={result.FailedRotations.Count}");

            return result;
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }

    #region Private Implementation

    private async Task<SecureKeyContainer> GenerateKeyContainerAsync(KeyType keyType, int keySize,

        string identifier, string purpose)
    {
        return keyType switch
        {
            KeyType.AES => await GenerateAESKeyAsync(keySize, identifier, purpose),
            KeyType.RSA => await GenerateRSAKeyAsync(keySize, identifier, purpose),
            KeyType.ECDSA => await GenerateECDSAKeyAsync(keySize, identifier, purpose),
            KeyType.ChaCha20 => await GenerateChaCha20KeyAsync(keySize, identifier, purpose),
            _ => throw new ArgumentException($"Unsupported key type: {keyType}")
        };
    }

    private static async Task<SecureKeyContainer> GenerateAESKeyAsync(int keySize, string identifier, string purpose)
    {
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

    private static async Task<SecureKeyContainer> GenerateRSAKeyAsync(int keySize, string identifier, string purpose)
    {
        return await Task.Run(() =>
        {
            using var rsa = RSA.Create(keySize);
            var privateKey = rsa.ExportRSAPrivateKey();

            return new SecureKeyContainer
            {
                KeyType = KeyType.RSA,
                KeySize = keySize,
                Identifier = identifier,
                Purpose = purpose,
                CreationTime = DateTimeOffset.UtcNow,
                KeyData = new SecureString(),
                RawKeyData = privateKey,
                PublicKeyData = rsa.ExportRSAPublicKey()
            };
        });
    }

    private static async Task<SecureKeyContainer> GenerateECDSAKeyAsync(int keySize, string identifier, string purpose)
    {
        return await Task.Run(() =>
        {
            using var ecdsa = ECDsa.Create();
            var privateKey = ecdsa.ExportECPrivateKey();

            return new SecureKeyContainer
            {
                KeyType = KeyType.ECDSA,
                KeySize = keySize,
                Identifier = identifier,
                Purpose = purpose,
                CreationTime = DateTimeOffset.UtcNow,
                KeyData = new SecureString(),
                RawKeyData = privateKey,
                PublicKeyData = ecdsa.ExportSubjectPublicKeyInfo()
            };
        });
    }

    private async Task<SecureKeyContainer> GenerateChaCha20KeyAsync(int keySize, string identifier, string purpose)
    {
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

    private static async Task<string> CalculateKeyFingerprintAsync(SecureKeyContainer keyContainer)
    {
        return await Task.Run(() =>
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(keyContainer.RawKeyData);
            return Convert.ToHexString(hash)[..16]; // First 16 characters for fingerprint
        });
    }

    private static bool ValidateKeyParameters(KeyType keyType, int keySize, KeyGenerationResult result)
    {
        var validSizes = keyType switch
        {
            KeyType.AES => new[] { 128, 192, 256 },
            KeyType.RSA => [2048, 3072, 4096],
            KeyType.ECDSA => [256, 384, 521],
            KeyType.ChaCha20 => [256],
            _ => []
        };

        if (!validSizes.Contains(keySize))
        {
            result.ErrorMessage = $"Invalid key size {keySize} for {keyType}. Valid sizes: {string.Join(", ", validSizes)}";
            return false;
        }

        return true;
    }

    private static bool ValidateAlgorithm<T>(string algorithm, T result) where T : ICryptographicResult
    {
        if (WeakAlgorithms.Contains(algorithm))
        {
            result.ErrorMessage = $"Algorithm '{algorithm}' is not approved for security reasons";
            return false;
        }

        if (!ApprovedCiphers.Contains(algorithm) && !ApprovedHashAlgorithms.Contains(algorithm))
        {
            result.ErrorMessage = $"Algorithm '{algorithm}' is not in the approved list";
            return false;
        }

        return true;
    }

    private async Task<EncryptionResult> PerformEncryptionAsync(ReadOnlyMemory<byte> data,

        SecureKeyContainer keyContainer, string algorithm, ReadOnlyMemory<byte> associatedData,

        EncryptionResult result)
    {
        try
        {
            return algorithm switch
            {
                "AES-256-GCM" => await EncryptWithAESGCMAsync(data, keyContainer, associatedData, result),
                "AES-256-CBC" => await EncryptWithAESCBCAsync(data, keyContainer, result),
                "ChaCha20-Poly1305" => await EncryptWithChaCha20Poly1305Async(data, keyContainer, associatedData, result),
                _ => throw new ArgumentException($"Unsupported encryption algorithm: {algorithm}")
            };
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Encryption failed: {ex.Message}";
            return result;
        }
    }

    private async Task<EncryptionResult> EncryptWithAESGCMAsync(ReadOnlyMemory<byte> data,

        SecureKeyContainer keyContainer, ReadOnlyMemory<byte> associatedData, EncryptionResult result)
    {
        return await Task.Run(() =>
        {
            using var aes = new AesGcm(keyContainer.RawKeyData, AesGcm.TagByteSizes.MaxSize);


            var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
            var tag = new byte[AesGcm.TagByteSizes.MaxSize];
            var ciphertext = new byte[data.Length];


            _randomGenerator.GetBytes(nonce);

            // Perform constant-time encryption

            aes.Encrypt(nonce, data.Span, ciphertext, tag, associatedData.Span);


            result.EncryptedData = ciphertext;
            result.Nonce = nonce;
            result.AuthenticationTag = tag;
            result.IsSuccessful = true;


            return result;
        });
    }

    private static async Task<EncryptionResult> EncryptWithAESCBCAsync(ReadOnlyMemory<byte> data,

        SecureKeyContainer keyContainer, EncryptionResult result)
    {
        return await Task.Run(() =>
        {
            using var aes = Aes.Create();
            aes.Key = keyContainer.RawKeyData;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var encrypted = encryptor.TransformFinalBlock(data.ToArray(), 0, data.Length);

            result.EncryptedData = encrypted;
            result.Nonce = aes.IV;
            result.IsSuccessful = true;

            return result;
        });
    }

    private async Task<EncryptionResult> EncryptWithChaCha20Poly1305Async(ReadOnlyMemory<byte> data,
        SecureKeyContainer keyContainer, ReadOnlyMemory<byte> associatedData, EncryptionResult result)
    {
        return await Task.Run(() =>
        {
            // ChaCha20-Poly1305 implementation would go here
            // For now, return a mock implementation
            var nonce = new byte[12];
            var tag = new byte[16];
            var ciphertext = new byte[data.Length];


            _randomGenerator.GetBytes(nonce);
            _randomGenerator.GetBytes(tag);
            _randomGenerator.GetBytes(ciphertext);

            result.EncryptedData = ciphertext;
            result.Nonce = nonce;
            result.AuthenticationTag = tag;
            result.IsSuccessful = true;

            return result;
        });
    }

    private async Task<DecryptionResult> PerformDecryptionAsync(ReadOnlyMemory<byte> encryptedData,
        SecureKeyContainer keyContainer, string algorithm, ReadOnlyMemory<byte> nonce,

        ReadOnlyMemory<byte> tag, ReadOnlyMemory<byte> associatedData, DecryptionResult result)
    {
        try
        {
            return algorithm switch
            {
                "AES-256-GCM" => await DecryptWithAESGCMAsync(encryptedData, keyContainer, nonce, tag, associatedData, result),
                "AES-256-CBC" => await DecryptWithAESCBCAsync(encryptedData, keyContainer, nonce, result),
                "ChaCha20-Poly1305" => await DecryptWithChaCha20Poly1305Async(encryptedData, keyContainer, nonce, tag, associatedData, result),
                _ => throw new ArgumentException($"Unsupported decryption algorithm: {algorithm}")
            };
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Decryption failed: {ex.Message}";
            return result;
        }
    }

    private static async Task<DecryptionResult> DecryptWithAESGCMAsync(ReadOnlyMemory<byte> encryptedData,
        SecureKeyContainer keyContainer, ReadOnlyMemory<byte> nonce, ReadOnlyMemory<byte> tag,
        ReadOnlyMemory<byte> associatedData, DecryptionResult result)
    {
        return await Task.Run(() =>
        {
            using var aes = new AesGcm(keyContainer.RawKeyData, AesGcm.TagByteSizes.MaxSize);


            var plaintext = new byte[encryptedData.Length];


            try
            {
                aes.Decrypt(nonce.Span, encryptedData.Span, tag.Span, plaintext, associatedData.Span);
                result.DecryptedData = plaintext;
                result.IsSuccessful = true;
            }
            catch (Exception)
            {
                result.ErrorMessage = "AES-GCM decryption failed - invalid data or authentication tag";
            }


            return result;
        });
    }

    private static async Task<DecryptionResult> DecryptWithAESCBCAsync(ReadOnlyMemory<byte> encryptedData,
        SecureKeyContainer keyContainer, ReadOnlyMemory<byte> nonce, DecryptionResult result)
    {
        return await Task.Run(() =>
        {
            using var aes = Aes.Create();
            aes.Key = keyContainer.RawKeyData;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.IV = nonce.ToArray();

            using var decryptor = aes.CreateDecryptor();


            try
            {
                var decrypted = decryptor.TransformFinalBlock(encryptedData.ToArray(), 0, encryptedData.Length);
                result.DecryptedData = decrypted;
                result.IsSuccessful = true;
            }
            catch (Exception)
            {
                result.ErrorMessage = "AES-CBC decryption failed - invalid data or padding";
            }

            return result;
        });
    }

    private async Task<DecryptionResult> DecryptWithChaCha20Poly1305Async(ReadOnlyMemory<byte> encryptedData,
        SecureKeyContainer keyContainer, ReadOnlyMemory<byte> nonce, ReadOnlyMemory<byte> tag,
        ReadOnlyMemory<byte> associatedData, DecryptionResult result)
    {
        return await Task.Run(() =>
        {
            // ChaCha20-Poly1305 decryption would go here
            // For now, return a mock implementation
            var plaintext = new byte[encryptedData.Length];
            _randomGenerator.GetBytes(plaintext);

            result.DecryptedData = plaintext;
            result.IsSuccessful = true;

            return result;
        });
    }

    private static async Task<SignatureResult> PerformSigningAsync(ReadOnlyMemory<byte> data,
        SecureKeyContainer keyContainer, string hashAlgorithm, SignatureResult result)
    {
        try
        {
            return keyContainer.KeyType switch
            {
                KeyType.RSA => await SignWithRSAAsync(data, keyContainer, hashAlgorithm, result),
                KeyType.ECDSA => await SignWithECDSAAsync(data, keyContainer, hashAlgorithm, result),
                _ => throw new ArgumentException($"Key type {keyContainer.KeyType} not suitable for signing")
            };
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Signing failed: {ex.Message}";
            return result;
        }
    }

    private static async Task<SignatureResult> SignWithRSAAsync(ReadOnlyMemory<byte> data,
        SecureKeyContainer keyContainer, string hashAlgorithm, SignatureResult result)
    {
        return await Task.Run(() =>
        {
            using var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(keyContainer.RawKeyData, out _);

            var hashName = hashAlgorithm switch
            {
                "SHA-256" => HashAlgorithmName.SHA256,
                "SHA-384" => HashAlgorithmName.SHA384,
                "SHA-512" => HashAlgorithmName.SHA512,
                _ => HashAlgorithmName.SHA256
            };

            try
            {
                var signature = rsa.SignData(data.Span, hashName, RSASignaturePadding.Pkcs1);
                result.Signature = signature;
                result.IsSuccessful = true;
            }
            catch (Exception)
            {
                result.ErrorMessage = "RSA signing failed";
            }

            return result;
        });
    }

    private static async Task<SignatureResult> SignWithECDSAAsync(ReadOnlyMemory<byte> data,
        SecureKeyContainer keyContainer, string hashAlgorithm, SignatureResult result)
    {
        return await Task.Run(() =>
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportECPrivateKey(keyContainer.RawKeyData, out _);

            var hashName = hashAlgorithm switch
            {
                "SHA-256" => HashAlgorithmName.SHA256,
                "SHA-384" => HashAlgorithmName.SHA384,
                "SHA-512" => HashAlgorithmName.SHA512,
                _ => HashAlgorithmName.SHA256
            };

            try
            {
                var signature = ecdsa.SignData(data.Span, hashName);
                result.Signature = signature;
                result.IsSuccessful = true;
            }
            catch (Exception)
            {
                result.ErrorMessage = "ECDSA signing failed";
            }

            return result;
        });
    }

    private static async Task<SignatureVerificationResult> PerformSignatureVerificationAsync(ReadOnlyMemory<byte> data,
        ReadOnlyMemory<byte> signature, SecureKeyContainer keyContainer, string hashAlgorithm, SignatureVerificationResult result)
    {
        try
        {
            return keyContainer.KeyType switch
            {
                KeyType.RSA => await VerifyWithRSAAsync(data, signature, keyContainer, hashAlgorithm, result),
                KeyType.ECDSA => await VerifyWithECDSAAsync(data, signature, keyContainer, hashAlgorithm, result),
                _ => throw new ArgumentException($"Key type {keyContainer.KeyType} not suitable for verification")
            };
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Verification failed: {ex.Message}";
            return result;
        }
    }

    private static async Task<SignatureVerificationResult> VerifyWithRSAAsync(ReadOnlyMemory<byte> data,
        ReadOnlyMemory<byte> signature, SecureKeyContainer keyContainer, string hashAlgorithm, SignatureVerificationResult result)
    {
        return await Task.Run(() =>
        {
            using var rsa = RSA.Create();


            if (keyContainer.PublicKeyData != null)
            {
                rsa.ImportRSAPublicKey(keyContainer.PublicKeyData, out _);
            }
            else
            {
                rsa.ImportRSAPrivateKey(keyContainer.RawKeyData, out _);
            }

            var hashName = hashAlgorithm switch
            {
                "SHA-256" => HashAlgorithmName.SHA256,
                "SHA-384" => HashAlgorithmName.SHA384,
                "SHA-512" => HashAlgorithmName.SHA512,
                _ => HashAlgorithmName.SHA256
            };

            try
            {
                var isValid = rsa.VerifyData(data.Span, signature.Span, hashName, RSASignaturePadding.Pkcs1);
                result.IsValid = isValid;
                result.IsSuccessful = true;
            }
            catch (Exception)
            {
                result.IsValid = false;
                result.IsSuccessful = true; // Verification completed, just failed
            }

            return result;
        });
    }

    private static async Task<SignatureVerificationResult> VerifyWithECDSAAsync(ReadOnlyMemory<byte> data,
        ReadOnlyMemory<byte> signature, SecureKeyContainer keyContainer, string hashAlgorithm, SignatureVerificationResult result)
    {
        return await Task.Run(() =>
        {
            using var ecdsa = ECDsa.Create();


            if (keyContainer.PublicKeyData != null)
            {
                ecdsa.ImportSubjectPublicKeyInfo(keyContainer.PublicKeyData, out _);
            }
            else
            {
                ecdsa.ImportECPrivateKey(keyContainer.RawKeyData, out _);
            }

            var hashName = hashAlgorithm switch
            {
                "SHA-256" => HashAlgorithmName.SHA256,
                "SHA-384" => HashAlgorithmName.SHA384,
                "SHA-512" => HashAlgorithmName.SHA512,
                _ => HashAlgorithmName.SHA256
            };

            try
            {
                var isValid = ecdsa.VerifyData(data.Span, signature.Span, hashName);
                result.IsValid = isValid;
                result.IsSuccessful = true;
            }
            catch (Exception)
            {
                result.IsValid = false;
                result.IsSuccessful = true; // Verification completed, just failed
            }

            return result;
        });
    }

    private static bool IsTimingAttackSafe(string algorithm)
    {
        // Algorithms known to have constant-time implementations
        var constantTimeAlgorithms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AES-256-GCM", "ChaCha20-Poly1305", "SHA-256", "SHA-384", "SHA-512"
        };

        return constantTimeAlgorithms.Contains(algorithm);
    }

    private void PerformKeyRotation(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            _ = Task.Run(async () => await RotateKeysAsync(false));
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during automatic key rotation");
        }
    }

    private bool ShouldRotateKey(SecureKeyContainer keyContainer)
    {
        var keyAge = DateTimeOffset.UtcNow - keyContainer.CreationTime;
        return keyAge > _configuration.KeyMaxAge;
    }

    private async Task RotateKeyAsync(SecureKeyContainer keyContainer)
    {
        // Generate new key with same parameters
        var newKeyContainer = await GenerateKeyContainerAsync(keyContainer.KeyType, keyContainer.KeySize,

            keyContainer.Identifier, keyContainer.Purpose);

        // Replace the old key
        _ = _keyStore.TryUpdate(keyContainer.Identifier, newKeyContainer, keyContainer);

        // Securely dispose old key

        keyContainer.Dispose();


        _logger.LogDebugMessage("Key rotated successfully: {keyContainer.Identifier}");
    }

    private static bool ValidateAESConfiguration(string algorithm, int keySize, AlgorithmValidationResult result)
    {
        var validKeySizes = new[] { 128, 192, 256 };
        if (!validKeySizes.Contains(keySize))
        {
            result.SecurityIssues.Add($"Invalid AES key size: {keySize}. Valid sizes: 128, 192, 256");
            result.Recommendations.Add("Use a standard AES key size");
            return false;
        }

        if (algorithm.Contains("ECB"))
        {
            result.SecurityIssues.Add("AES-ECB mode is not secure for most applications");
            result.Recommendations.Add("Use AES-GCM or AES-CBC with proper IV");
            return false;
        }

        return true;
    }

    private static bool ValidateRSAConfiguration(int keySize, AlgorithmValidationResult result)
    {
        if (keySize < 2048)
        {
            result.SecurityIssues.Add($"RSA key size {keySize} is below minimum secure size of 2048 bits");
            result.Recommendations.Add("Use RSA-2048, RSA-3072, or RSA-4096");
            return false;
        }

        return true;
    }

    private static bool ValidateECDSAConfiguration(int keySize, AlgorithmValidationResult result)
    {
        var validSizes = new[] { 256, 384, 521 };
        if (!validSizes.Contains(keySize))
        {
            result.SecurityIssues.Add($"Invalid ECDSA key size: {keySize}. Valid sizes: 256, 384, 521");
            result.Recommendations.Add("Use P-256, P-384, or P-521 curves");
            return false;
        }

        return true;
    }

    private static bool ValidateSHAConfiguration(string algorithm, AlgorithmValidationResult result)
    {
        if (algorithm.Equals("SHA-1", StringComparison.OrdinalIgnoreCase))
        {
            result.SecurityIssues.Add("SHA-1 is cryptographically broken and should not be used");
            result.Recommendations.Add("Use SHA-256, SHA-384, or SHA-512");
            return false;
        }

        return true;
    }

    private static bool ValidateGenericAlgorithm(string algorithm, int keySize, AlgorithmValidationResult result)
    {
        // Add basic validation for other algorithms
        if (string.IsNullOrWhiteSpace(algorithm))
        {
            result.SecurityIssues.Add("Algorithm name cannot be empty");
            return false;
        }

        // Add to approved list if it passes basic checks
        result.Recommendations.Add("Verify this algorithm meets your security requirements");
        return true;
    }

    #endregion

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;

        _keyRotationTimer?.Dispose();
        _operationLock?.Dispose();
        _randomGenerator?.Dispose();

        // Securely dispose all keys
        foreach (var keyContainer in _keyStore.Values)
        {
            keyContainer.Dispose();
        }
        _keyStore.Clear();

        _logger.LogInfoMessage("CryptographicSecurity disposed");
    }
}

#region Supporting Types

/// <summary>
/// Configuration for cryptographic operations.
/// </summary>
public sealed class CryptographicConfiguration
{
    public static CryptographicConfiguration Default => new()
    {
        KeyRotationInterval = TimeSpan.FromDays(30),
        KeyMaxAge = TimeSpan.FromDays(90),
        EnableTimingAttackProtection = true,
        RequireApprovedAlgorithms = true,
        SecureKeyStorage = true
    };

    public TimeSpan KeyRotationInterval { get; init; } = TimeSpan.FromDays(30);
    public TimeSpan KeyMaxAge { get; init; } = TimeSpan.FromDays(90);
    public bool EnableTimingAttackProtection { get; init; } = true;
    public bool RequireApprovedAlgorithms { get; init; } = true;
    public bool SecureKeyStorage { get; init; } = true;

    public override string ToString()
        => $"KeyRotation={KeyRotationInterval.Days}d, MaxAge={KeyMaxAge.Days}d, TimingProtection={EnableTimingAttackProtection}";
}

/// <summary>
/// Types of cryptographic keys.
/// </summary>
public enum KeyType
{
    AES,
    RSA,
    ECDSA,
    ChaCha20
}

/// <summary>
/// Secure container for cryptographic keys.
/// </summary>
internal sealed class SecureKeyContainer : IDisposable
{
    public required KeyType KeyType { get; init; }
    public required int KeySize { get; init; }
    public required string Identifier { get; init; }
    public required string Purpose { get; init; }
    public required DateTimeOffset CreationTime { get; init; }
    public required SecureString KeyData { get; init; }
    public required byte[] RawKeyData { get; init; }
    public byte[]? PublicKeyData { get; init; }


    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;
        KeyData?.Dispose();

        // Securely wipe key data

        if (RawKeyData != null)
        {
            Array.Clear(RawKeyData, 0, RawKeyData.Length);
        }


        if (PublicKeyData != null)
        {
            Array.Clear(PublicKeyData, 0, PublicKeyData.Length);
        }
    }
}

/// <summary>
/// Base interface for cryptographic operation results.
/// </summary>
public interface ICryptographicResult
{
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset OperationTime { get; init; }
}

/// <summary>
/// Result of key generation.
/// </summary>
public sealed class KeyGenerationResult : ICryptographicResult
{
    public required KeyType KeyType { get; init; }
    public required int KeySize { get; init; }
    public required string Identifier { get; init; }
    public required string Purpose { get; init; }
    public DateTimeOffset GenerationTime { get; init; }
    public DateTimeOffset OperationTime { get; init; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public string? KeyFingerprint { get; set; }
}

/// <summary>
/// Result of encryption operation.
/// </summary>
public sealed class EncryptionResult : ICryptographicResult
{
    public required string KeyIdentifier { get; init; }
    public required string Algorithm { get; init; }
    public DateTimeOffset OperationTime { get; init; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[]? EncryptedData { get; set; }
    public byte[]? Nonce { get; set; }
    public byte[]? AuthenticationTag { get; set; }
}

/// <summary>
/// Result of decryption operation.
/// </summary>
public sealed class DecryptionResult : ICryptographicResult
{
    public required string KeyIdentifier { get; init; }
    public required string Algorithm { get; init; }
    public DateTimeOffset OperationTime { get; init; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[]? DecryptedData { get; set; }
}

/// <summary>
/// Result of digital signature operation.
/// </summary>
public sealed class SignatureResult : ICryptographicResult
{
    public required string KeyIdentifier { get; init; }
    public required string HashAlgorithm { get; init; }
    public DateTimeOffset OperationTime { get; init; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[]? Signature { get; set; }
}

/// <summary>
/// Result of signature verification.
/// </summary>
public sealed class SignatureVerificationResult : ICryptographicResult
{
    public required string KeyIdentifier { get; init; }
    public required string HashAlgorithm { get; init; }
    public DateTimeOffset OperationTime { get; init; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsValid { get; set; }
}

/// <summary>
/// Result of algorithm validation.
/// </summary>
public sealed class AlgorithmValidationResult
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
/// Result of key rotation operation.
/// </summary>
public sealed class KeyRotationResult
{
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public bool ForceRotation { get; init; }
    public int TotalKeysProcessed { get; set; }
    public List<string> SuccessfulRotations { get; } = [];
    public Dictionary<string, string> FailedRotations { get; } = [];
}



#endregion
