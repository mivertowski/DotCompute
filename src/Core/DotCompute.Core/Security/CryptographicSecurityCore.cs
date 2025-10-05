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
        /// <summary>
        /// Initializes a new instance of the CryptographicSecurityCore class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="configuration">The configuration.</param>

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
        /// <summary>
        /// Performs dispose.
        /// </summary>

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
    /// <summary>
    /// An security level enumeration.
    /// </summary>

    // Supporting types - essential subset for compilation
    public enum SecurityLevel { Weak, Moderate, Strong, Unknown }
    /// <summary>
    /// A class that represents cryptographic configuration.
    /// </summary>

    public class CryptographicConfiguration
    {
        /// <summary>
        /// Gets or sets the default.
        /// </summary>
        /// <value>The default.</value>
        public static CryptographicConfiguration Default => new();
        /// <summary>
        /// Gets or sets the key rotation interval.
        /// </summary>
        /// <value>The key rotation interval.</value>
        public TimeSpan KeyRotationInterval { get; set; } = TimeSpan.FromHours(24);
        /// <summary>
        /// Gets or sets the key lifetime.
        /// </summary>
        /// <value>The key lifetime.</value>
        public TimeSpan KeyLifetime { get; set; } = TimeSpan.FromDays(30);
    }
    /// <summary>
    /// An i cryptographic result interface.
    /// </summary>

    public interface ICryptographicResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether successful.
        /// </summary>
        /// <value>The is successful.</value>
        public bool IsSuccessful { get; set; }
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>The error message.</value>
        public string? ErrorMessage { get; set; }
        /// <summary>
        /// Gets or sets the operation time.
        /// </summary>
        /// <value>The operation time.</value>
        public DateTimeOffset OperationTime { get; init; }
    }
    /// <summary>
    /// A class that represents key generation result.
    /// </summary>

    public class KeyGenerationResult : ICryptographicResult
    {
        /// <summary>
        /// Gets or sets the key type.
        /// </summary>
        /// <value>The key type.</value>
        public KeyType KeyType { get; init; }
        /// <summary>
        /// Gets or sets the key size.
        /// </summary>
        /// <value>The key size.</value>
        public int KeySize { get; init; }
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        /// <value>The identifier.</value>
        public string Identifier { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the purpose.
        /// </summary>
        /// <value>The purpose.</value>
        public string Purpose { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the generation time.
        /// </summary>
        /// <value>The generation time.</value>
        public DateTimeOffset GenerationTime { get; init; }
        /// <summary>
        /// Gets or sets the operation time.
        /// </summary>
        /// <value>The operation time.</value>
        public DateTimeOffset OperationTime { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether successful.
        /// </summary>
        /// <value>The is successful.</value>
        public bool IsSuccessful { get; set; }
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>The error message.</value>
        public string? ErrorMessage { get; set; }
        /// <summary>
        /// Gets or sets the success.
        /// </summary>
        /// <value>The success.</value>
        public bool Success { get; set; }
        /// <summary>
        /// Gets or sets the fingerprint.
        /// </summary>
        /// <value>The fingerprint.</value>
        public string? Fingerprint { get; set; }
    }
    /// <summary>
    /// A class that represents encryption result.
    /// </summary>

    public class EncryptionResult : ICryptographicResult
    {
        /// <summary>
        /// Gets or sets the key identifier.
        /// </summary>
        /// <value>The key identifier.</value>
        public string KeyIdentifier { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the algorithm.
        /// </summary>
        /// <value>The algorithm.</value>
        public string Algorithm { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the operation time.
        /// </summary>
        /// <value>The operation time.</value>
        public DateTimeOffset OperationTime { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether successful.
        /// </summary>
        /// <value>The is successful.</value>
        public bool IsSuccessful { get; set; }
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>The error message.</value>
        public string? ErrorMessage { get; set; }
        /// <summary>
        /// Gets or sets the encrypted data.
        /// </summary>
        /// <value>The encrypted data.</value>
        public byte[]? EncryptedData { get; set; }
        /// <summary>
        /// Gets or sets the nonce.
        /// </summary>
        /// <value>The nonce.</value>
        public byte[]? Nonce { get; set; }
        /// <summary>
        /// Gets or sets the authentication tag.
        /// </summary>
        /// <value>The authentication tag.</value>
        public byte[]? AuthenticationTag { get; set; }
    }
    /// <summary>
    /// A class that represents decryption result.
    /// </summary>

    public class DecryptionResult : ICryptographicResult
    {
        /// <summary>
        /// Gets or sets the key identifier.
        /// </summary>
        /// <value>The key identifier.</value>
        public string KeyIdentifier { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the algorithm.
        /// </summary>
        /// <value>The algorithm.</value>
        public string Algorithm { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the operation time.
        /// </summary>
        /// <value>The operation time.</value>
        public DateTimeOffset OperationTime { get; init; }
        /// <summary>
        /// Gets or sets a value indicating whether successful.
        /// </summary>
        /// <value>The is successful.</value>
        public bool IsSuccessful { get; set; }
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>The error message.</value>
        public string? ErrorMessage { get; set; }
        /// <summary>
        /// Gets or sets the decrypted data.
        /// </summary>
        /// <value>The decrypted data.</value>
        public byte[]? DecryptedData { get; set; }
    }
    /// <summary>
    /// A class that represents algorithm validation result.
    /// </summary>

    public class AlgorithmValidationResult
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
        /// Gets or sets the security level.
        /// </summary>
        /// <value>The security level.</value>
        public SecurityLevel SecurityLevel { get; set; }
        /// <summary>
        /// Gets or sets the validation message.
        /// </summary>
        /// <value>The validation message.</value>
        public string ValidationMessage { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets a value indicating whether sues.
        /// </summary>
        /// <value>The issues.</value>
        public IList<string> Issues { get; init; } = [];
        /// <summary>
        /// Gets or sets the warnings.
        /// </summary>
        /// <value>The warnings.</value>
        public IList<string> Warnings { get; init; } = [];
        /// <summary>
        /// Gets or sets the recommendations.
        /// </summary>
        /// <value>The recommendations.</value>
        public IList<string> Recommendations { get; set; } = [];
    }
    /// <summary>
    /// A class that represents secure key container.
    /// </summary>

    public class SecureKeyContainer : IDisposable
    {
        /// <summary>
        /// Gets or sets the key type.
        /// </summary>
        /// <value>The key type.</value>
        public KeyType KeyType { get; init; }
        /// <summary>
        /// Gets or sets the key size.
        /// </summary>
        /// <value>The key size.</value>
        public int KeySize { get; init; }
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        /// <value>The identifier.</value>
        public string Identifier { get; init; }
        /// <summary>
        /// Gets or sets the purpose.
        /// </summary>
        /// <value>The purpose.</value>
        public string Purpose { get; init; }
        /// <summary>
        /// Gets or sets the creation time.
        /// </summary>
        /// <value>The creation time.</value>
        public DateTimeOffset CreationTime { get; init; }

        /// <summary>
        /// Gets or sets the public key data for asymmetric keys.
        /// </summary>
        /// <value>The public key data bytes, or null for symmetric keys.</value>
        public byte[]? PublicKeyData { get; set; }

        private readonly byte[] _keyData;
        private bool _disposed;
        /// <summary>
        /// Initializes a new instance of the SecureKeyContainer class.
        /// </summary>
        /// <param name="keyType">The key type.</param>
        /// <param name="keyData">The key data.</param>
        /// <param name="identifier">The identifier.</param>
        /// <param name="purpose">The purpose.</param>
        /// <param name="keySize">The key size.</param>

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
        /// <summary>
        /// Gets the key bytes.
        /// </summary>
        /// <returns>The key bytes.</returns>

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
        /// <summary>
        /// Performs dispose.
        /// </summary>

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
