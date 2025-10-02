// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;

namespace DotCompute.Core.Security
{
    /// <summary>
    /// Core cryptographic security service providing key management, encryption/decryption,
    /// and digital signatures. This is a streamlined version maintaining essential functionality.
    /// </summary>
    public sealed class CryptographicSecurityCore : IDisposable
    {
        private readonly ILogger _logger;
        private readonly CryptographicConfiguration _configuration;
        private readonly ConcurrentDictionary<string, SecureKeyContainer> _keyStore = new();
        private readonly SemaphoreSlim _operationLock = new(Environment.ProcessorCount, Environment.ProcessorCount);
        private readonly Timer _keyRotationTimer;
        private readonly RandomNumberGenerator _randomGenerator;
        private volatile bool _disposed;

        // Approved cryptographic algorithms
        private static readonly HashSet<string> _approvedCiphers = new(StringComparer.OrdinalIgnoreCase)
        {
            "AES-256-GCM", "AES-256-CBC", "AES-192-GCM", "AES-192-CBC", "ChaCha20-Poly1305"
        };

        private static readonly HashSet<string> _approvedHashAlgorithms = new(StringComparer.OrdinalIgnoreCase)
        {
            "SHA-256", "SHA-384", "SHA-512", "SHA3-256", "SHA3-384", "SHA3-512", "BLAKE2b"
        };

        private static readonly HashSet<string> _weakAlgorithms = new(StringComparer.OrdinalIgnoreCase)
        {
            "DES", "3DES", "RC4", "MD5", "SHA-1", "RSA-1024"
        };

        public CryptographicSecurityCore(ILogger<CryptographicSecurityCore> logger, CryptographicConfiguration? configuration = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? CryptographicConfiguration.Default;
            _randomGenerator = RandomNumberGenerator.Create();

            // Initialize key rotation timer
            _keyRotationTimer = new Timer(PerformKeyRotation, null,
                _configuration.KeyRotationInterval, _configuration.KeyRotationInterval);

            _logger.LogInfoMessage($"CryptographicSecurity initialized with configuration: {_configuration}");
        }

        /// <summary>
        /// Generates a new cryptographically secure encryption key.
        /// </summary>
        public async Task<KeyGenerationResult> GenerateKeyAsync(KeyType keyType, int keySize,
            string identifier, string purpose)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CryptographicSecurityCore));
            }


            ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
            ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

            await _operationLock.WaitAsync();
            try
            {
                var result = new KeyGenerationResult
                {
                    KeyType = keyType,
                    KeySize = keySize,
                    Identifier = identifier,
                    Purpose = purpose,
                    GenerationTime = DateTimeOffset.UtcNow
                };

                // Generate key based on type
                var keyContainer = await GenerateKeyContainerAsync(keyType, keySize, identifier, purpose);
                if (keyContainer != null && _keyStore.TryAdd(identifier, keyContainer))
                {
                    result.Success = true;
                    result.Fingerprint = await CalculateKeyFingerprintAsync(keyContainer);
                }
                else
                {
                    result.ErrorMessage = "Failed to generate or store key";
                }

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
        public async Task<EncryptionResult> EncryptAsync(ReadOnlyMemory<byte> data, string keyIdentifier,
            string algorithm, ReadOnlyMemory<byte> associatedData = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CryptographicSecurityCore));
            }


            var result = new EncryptionResult
            {
                KeyIdentifier = keyIdentifier,
                Algorithm = algorithm,
                OperationTime = DateTimeOffset.UtcNow
            };

            if (!_keyStore.TryGetValue(keyIdentifier, out var keyContainer))
            {
                result.ErrorMessage = $"Key '{keyIdentifier}' not found";
                return result;
            }

            return await PerformEncryptionAsync(data, keyContainer, algorithm, associatedData, result);
        }

        /// <summary>
        /// Decrypts data using the specified key and algorithm.
        /// </summary>
        public async Task<DecryptionResult> DecryptAsync(ReadOnlyMemory<byte> encryptedData, string keyIdentifier,
            string algorithm, ReadOnlyMemory<byte> nonce = default, ReadOnlyMemory<byte> tag = default,
            ReadOnlyMemory<byte> associatedData = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CryptographicSecurityCore));
            }

            var result = new DecryptionResult
            {
                KeyIdentifier = keyIdentifier,
                Algorithm = algorithm,
                OperationTime = DateTimeOffset.UtcNow
            };

            if (!_keyStore.TryGetValue(keyIdentifier, out var keyContainer))
            {
                result.ErrorMessage = $"Key '{keyIdentifier}' not found";
                return result;
            }

            // Simplified decryption - in practice would handle different algorithms
            try
            {
                using var aes = Aes.Create();
                aes.Key = keyContainer.GetKeyBytes();

                if (!nonce.IsEmpty)
                {
                    aes.IV = nonce.ToArray();
                }
                else
                {
                    aes.GenerateIV();
                }

                using var decryptor = aes.CreateDecryptor();
                var decrypted = decryptor.TransformFinalBlock(encryptedData.ToArray(), 0, encryptedData.Length);

                result.DecryptedData = decrypted;
                result.IsSuccessful = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Decryption failed: {ex.Message}";
            }

            await Task.CompletedTask;

            return result;
        }

        /// <summary>
        /// Validates a cryptographic algorithm for security compliance.
        /// </summary>
        public static AlgorithmValidationResult ValidateCryptographicAlgorithm(string algorithm, int keySize, string context)
        {
            var result = new AlgorithmValidationResult
            {
                Algorithm = algorithm,
                KeySize = keySize,
                Context = context,
                ValidationTime = DateTimeOffset.UtcNow
            };

            if (_weakAlgorithms.Contains(algorithm))
            {
                result.IsApproved = false;
                result.SecurityLevel = SecurityLevel.Weak;
                result.ValidationMessage = $"Algorithm '{algorithm}' is considered weak and should not be used";
                return result;
            }

            if (_approvedCiphers.Contains(algorithm) || _approvedHashAlgorithms.Contains(algorithm))
            {
                result.IsApproved = true;
                result.SecurityLevel = SecurityLevel.Strong;
                result.ValidationMessage = $"Algorithm '{algorithm}' is approved for use";
            }
            else
            {
                result.IsApproved = false;
                result.SecurityLevel = SecurityLevel.Unknown;
                result.ValidationMessage = $"Algorithm '{algorithm}' is not in the approved list";
            }

            return result;
        }

        #region Private Methods

        private static async Task<SecureKeyContainer> GenerateKeyContainerAsync(KeyType keyType, int keySize,
            string identifier, string purpose)
        {
            return keyType switch
            {
                KeyType.AES => await GenerateAESKeyAsync(keySize, identifier, purpose),
                KeyType.RSA => await GenerateRSAKeyAsync(keySize, identifier, purpose),
                _ => throw new NotSupportedException($"Key type {keyType} is not supported")
            };
        }

        private static async Task<SecureKeyContainer> GenerateAESKeyAsync(int keySize, string identifier, string purpose)
        {
            using var aes = Aes.Create();
            aes.KeySize = keySize;
            aes.GenerateKey();

            var keyData = new byte[aes.Key.Length];
            aes.Key.CopyTo(keyData, 0);

            await Task.CompletedTask;
            return new SecureKeyContainer(KeyType.AES, keyData, identifier, purpose, keySize);
        }

        private static async Task<SecureKeyContainer> GenerateRSAKeyAsync(int keySize, string identifier, string purpose)
        {
            using var rsa = RSA.Create(keySize);
            var keyData = rsa.ExportRSAPrivateKey();

            await Task.CompletedTask;
            return new SecureKeyContainer(KeyType.RSA, keyData, identifier, purpose, keySize);
        }

        private static async Task<EncryptionResult> PerformEncryptionAsync(ReadOnlyMemory<byte> data,
            SecureKeyContainer keyContainer, string algorithm, ReadOnlyMemory<byte> associatedData, EncryptionResult result)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = keyContainer.GetKeyBytes();
                aes.GenerateIV();

                using var encryptor = aes.CreateEncryptor();
                var encrypted = encryptor.TransformFinalBlock(data.ToArray(), 0, data.Length);

                result.EncryptedData = encrypted;
                result.Nonce = aes.IV;
                result.IsSuccessful = true;

                await Task.CompletedTask;
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Encryption failed: {ex.Message}";
                return result;
            }
        }

        private static async Task<string> CalculateKeyFingerprintAsync(SecureKeyContainer keyContainer)
        {
            var hash = SHA256.HashData(keyContainer.GetKeyBytes());
            await Task.CompletedTask;
            return Convert.ToHexString(hash);
        }

        private void PerformKeyRotation(object? state)
        {
            // Simplified key rotation placeholder
            try
            {
                _logger.LogInfoMessage("Performing automatic key rotation check");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error during key rotation");
            }
        }

        #endregion

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }


            _keyRotationTimer?.Dispose();

            foreach (var keyContainer in _keyStore.Values)
            {
                keyContainer.Dispose();
            }

            _keyStore.Clear();
            _operationLock?.Dispose();
            _randomGenerator?.Dispose();

            _disposed = true;
        }
    }

    // Supporting types - essential subset for compilation
    public enum SecurityLevel { Weak, Moderate, Strong, Unknown }

    public class CryptographicConfiguration
    {
        public static CryptographicConfiguration Default => new();
        public TimeSpan KeyRotationInterval { get; set; } = TimeSpan.FromHours(24);
        public TimeSpan KeyLifetime { get; set; } = TimeSpan.FromDays(30);
    }

    public interface ICryptographicResult
    {
        public bool IsSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTimeOffset OperationTime { get; init; }
    }

    public class KeyGenerationResult : ICryptographicResult
    {
        public KeyType KeyType { get; init; }
        public int KeySize { get; init; }
        public string Identifier { get; init; } = string.Empty;
        public string Purpose { get; init; } = string.Empty;
        public DateTimeOffset GenerationTime { get; init; }
        public DateTimeOffset OperationTime { get; init; }
        public bool IsSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
        public bool Success { get; set; }
        public string? Fingerprint { get; set; }
    }

    public class EncryptionResult : ICryptographicResult
    {
        public string KeyIdentifier { get; init; } = string.Empty;
        public string Algorithm { get; init; } = string.Empty;
        public DateTimeOffset OperationTime { get; init; }
        public bool IsSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
        public byte[]? EncryptedData { get; set; }
        public byte[]? Nonce { get; set; }
        public byte[]? AuthenticationTag { get; set; }
    }

    public class DecryptionResult : ICryptographicResult
    {
        public string KeyIdentifier { get; init; } = string.Empty;
        public string Algorithm { get; init; } = string.Empty;
        public DateTimeOffset OperationTime { get; init; }
        public bool IsSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
        public byte[]? DecryptedData { get; set; }
    }

    public class AlgorithmValidationResult
    {
        public required string Algorithm { get; init; }
        public int KeySize { get; init; }
        public required string Context { get; init; }
        public DateTimeOffset ValidationTime { get; init; }
        public bool IsApproved { get; set; }
        public SecurityLevel SecurityLevel { get; set; }
        public string ValidationMessage { get; set; } = string.Empty;
        public List<string> Issues { get; set; } = [];
        public List<string> Warnings { get; set; } = [];
        public List<string> Recommendations { get; set; } = [];
    }

    public class SecureKeyContainer : IDisposable
    {
        public KeyType KeyType { get; init; }
        public int KeySize { get; init; }
        public string Identifier { get; init; }
        public string Purpose { get; init; }
        public DateTimeOffset CreationTime { get; init; }
        private readonly byte[] _keyData;
        private bool _disposed;

        public SecureKeyContainer(KeyType keyType, byte[] keyData, string identifier, string purpose, int keySize)
        {
            KeyType = keyType;
            KeySize = keySize;
            Identifier = identifier;
            Purpose = purpose;
            CreationTime = DateTimeOffset.UtcNow;
            _keyData = new byte[keyData.Length];
            Array.Copy(keyData, _keyData, keyData.Length);
        }

        public byte[] GetKeyBytes()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SecureKeyContainer));
            }


            var copy = new byte[_keyData.Length];
            Array.Copy(_keyData, copy, _keyData.Length);
            return copy;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }


            Array.Clear(_keyData, 0, _keyData.Length);
            _disposed = true;
        }
    }
}
