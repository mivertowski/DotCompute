// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;

namespace DotCompute.Core.Security;

/// <summary>
/// Main orchestrator for cryptographic security operations with comprehensive
/// key management, encryption/decryption, and digital signature coordination.
/// </summary>
public sealed class CryptographicSecurityOrchestrator : IDisposable
{
    private readonly ILogger _logger;
    private readonly CryptographicConfiguration _configuration;
    private readonly CertificateValidator _certificateValidator;
    private readonly SignatureVerifier _signatureVerifier;
    private readonly HashCalculator _hashCalculator;
    private readonly EncryptionManager _encryptionManager;
    private readonly SecurityAuditor _securityAuditor;
    private readonly ConcurrentDictionary<string, SecureKeyContainer> _keyStore = new();
    private readonly SemaphoreSlim _operationLock;
    private readonly Timer _keyRotationTimer;
    private volatile bool _disposed;
    /// <summary>
    /// Initializes a new instance of the CryptographicSecurityOrchestrator class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>

    public CryptographicSecurityOrchestrator(
        ILogger<CryptographicSecurityOrchestrator> logger,
        CryptographicConfiguration? configuration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? CryptographicConfiguration.Default;
        _operationLock = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);

        // Initialize component services
        _certificateValidator = new CertificateValidator(logger);
        _signatureVerifier = new SignatureVerifier(logger);
        _hashCalculator = new HashCalculator(logger);
        _encryptionManager = new EncryptionManager(logger, _configuration);
        _securityAuditor = new SecurityAuditor(logger, _configuration);

        // Initialize key rotation timer
        _keyRotationTimer = new Timer(PerformKeyRotation, null,
            _configuration.KeyRotationInterval, _configuration.KeyRotationInterval);

        _logger.LogInfoMessage($"CryptographicSecurityOrchestrator initialized with configuration: {_configuration}");
    }

    /// <summary>
    /// Certificate validator component
    /// </summary>
    public CertificateValidator CertificateValidator => _certificateValidator;

    /// <summary>
    /// Signature verifier component
    /// </summary>
    public SignatureVerifier SignatureVerifier => _signatureVerifier;

    /// <summary>
    /// Hash calculator component
    /// </summary>
    public HashCalculator HashCalculator => _hashCalculator;

    /// <summary>
    /// Encryption manager component
    /// </summary>
    public EncryptionManager EncryptionManager => _encryptionManager;

    /// <summary>
    /// Security auditor component
    /// </summary>
    public SecurityAuditor SecurityAuditor => _securityAuditor;

    /// <summary>
    /// Generates a new cryptographically secure encryption key.
    /// </summary>
    public async Task<KeyGenerationResult> GenerateKeyAsync(
        KeyType keyType,
        int keySize,
        string identifier,
        string purpose)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

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

            // Validate key parameters using security auditor
            if (!_securityAuditor.ValidateKeyParameters(keyType, keySize, result))
            {
                return result;
            }

            // Generate the key using appropriate component
            var keyContainer = await GenerateKeyContainerAsync(keyType, keySize, identifier, purpose);

            // Store the key securely
            _ = _keyStore.AddOrUpdate(identifier, keyContainer, (key, existing) =>
            {
                existing.Dispose(); // Securely dispose old key
                return keyContainer;
            });

            result.IsSuccessful = true;
            result.Fingerprint = await _hashCalculator.CalculateKeyFingerprintAsync(keyContainer);

            _logger.LogInfoMessage($"Cryptographic key generated successfully: Id={identifier}, Fingerprint={result.Fingerprint}");

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
    public async Task<EncryptionResult> EncryptAsync(
        ReadOnlyMemory<byte> data,
        string keyIdentifier,
        string algorithm = "AES-256-GCM",
        ReadOnlyMemory<byte> associatedData = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

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

            // Validate algorithm using security auditor
            if (!_securityAuditor.ValidateAlgorithm(algorithm, result))
            {
                return result;
            }

            // Get the key
            if (!_keyStore.TryGetValue(keyIdentifier, out var keyContainer))
            {
                result.ErrorMessage = $"Key not found: {keyIdentifier}";
                return result;
            }

            // Perform encryption using encryption manager
            result = await _encryptionManager.PerformEncryptionAsync(
                data, keyContainer, algorithm, associatedData, result);

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
    public async Task<DecryptionResult> DecryptAsync(
        ReadOnlyMemory<byte> encryptedData,
        string keyIdentifier,
        string algorithm,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> tag = default,
        ReadOnlyMemory<byte> associatedData = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

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
            if (!_securityAuditor.ValidateAlgorithm(algorithm, result))
            {
                return result;
            }

            // Get the key
            if (!_keyStore.TryGetValue(keyIdentifier, out var keyContainer))
            {
                result.ErrorMessage = $"Key not found: {keyIdentifier}";
                return result;
            }

            // Perform decryption using encryption manager
            result = await _encryptionManager.PerformDecryptionAsync(
                encryptedData, keyContainer, algorithm, nonce, tag, associatedData, result);

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
    public async Task<SignatureResult> SignDataAsync(
        ReadOnlyMemory<byte> data,
        string keyIdentifier,
        string hashAlgorithm = "SHA-256")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentException.ThrowIfNullOrWhiteSpace(keyIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashAlgorithm);

        await _operationLock.WaitAsync();
        try
        {
            _logger.LogDebugMessage($"Signing data: KeyId={keyIdentifier}, HashAlgorithm={hashAlgorithm}, DataSize={data.Length}");

            var result = new SignatureResult
            {
                KeyId = keyIdentifier,
                Algorithm = hashAlgorithm,
                Timestamp = DateTimeOffset.UtcNow
            };

            // Validate hash algorithm
            if (!_securityAuditor.ValidateHashAlgorithm(hashAlgorithm, result))
            {
                return result;
            }

            // Get the key
            if (!_keyStore.TryGetValue(keyIdentifier, out var keyContainer))
            {
                return new SignatureResult
                {
                    KeyId = keyIdentifier,
                    Algorithm = hashAlgorithm,
                    Timestamp = DateTimeOffset.UtcNow,
                    ErrorMessage = $"Signing key not found: {keyIdentifier}"
                };
            }

            if (keyContainer.KeyType is not KeyType.RSA and not KeyType.ECDSA)
            {
                return new SignatureResult
                {
                    KeyId = keyIdentifier,
                    Algorithm = hashAlgorithm,
                    Timestamp = DateTimeOffset.UtcNow,
                    ErrorMessage = $"Key type not suitable for signing: {keyContainer.KeyType}"
                };
            }

            // Perform signing using signature verifier
            result = await _signatureVerifier.PerformSigningAsync(data, keyContainer, hashAlgorithm, result);

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
    public async Task<SignatureVerificationResult> VerifySignatureAsync(
        ReadOnlyMemory<byte> data,
        ReadOnlyMemory<byte> signature,
        string keyIdentifier,
        string hashAlgorithm = "SHA-256")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentException.ThrowIfNullOrWhiteSpace(keyIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashAlgorithm);

        await _operationLock.WaitAsync();
        try
        {
            _logger.LogDebugMessage($"Verifying signature: KeyId={keyIdentifier}, HashAlgorithm={hashAlgorithm}, DataSize={data.Length}");

            var result = new SignatureVerificationResult
            {
                KeyId = keyIdentifier,
                Algorithm = hashAlgorithm,
                Timestamp = DateTimeOffset.UtcNow
            };

            // Validate hash algorithm
            if (!_securityAuditor.ValidateHashAlgorithm(hashAlgorithm, result))
            {
                return result;
            }

            // Get the key
            if (!_keyStore.TryGetValue(keyIdentifier, out var keyContainer))
            {
                return new SignatureVerificationResult
                {
                    KeyId = keyIdentifier,
                    Algorithm = hashAlgorithm,
                    Timestamp = DateTimeOffset.UtcNow,
                    ErrorMessage = $"Verification key not found: {keyIdentifier}"
                };
            }

            // Perform verification using signature verifier
            result = await _signatureVerifier.PerformSignatureVerificationAsync(
                data, signature, keyContainer, hashAlgorithm, result);

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
    public AlgorithmValidationResult ValidateCryptographicAlgorithm(
        string algorithm,
        int keySize,
        string context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        return _securityAuditor.ValidateCryptographicAlgorithm(algorithm, keySize, context);
    }

    /// <summary>
    /// Performs secure key rotation for all stored keys based on policy.
    /// </summary>
    public async Task<KeyRotationResult> RotateKeysAsync(bool forceRotation = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _operationLock.WaitAsync();
        try
        {
            _logger.LogInfoMessage($"Starting key rotation: ForceRotation={forceRotation}");

            var result = new KeyRotationResult
            {
                RotationTime = DateTimeOffset.UtcNow,
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
                    result.RotatedKeys.Add(keyContainer.Identifier);
                }
                catch (Exception ex)
                {
                    _logger.LogErrorMessage(ex, $"Failed to rotate key: {keyContainer.Identifier}");
                    result.FailedRotations.Add($"{keyContainer.Identifier}: {ex.Message}");
                }
            }

            result.IsSuccessful = result.FailedRotations.Count == 0;

            _logger.LogInfoMessage($"Key rotation completed: Processed={keysToRotate.Count}, Successful={result.RotatedKeys.Count}, Failed={result.FailedRotations.Count}");

            return result;
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }

    private async Task<SecureKeyContainer> GenerateKeyContainerAsync(
        KeyType keyType,
        int keySize,
        string identifier,
        string purpose)
    {
        return keyType switch
        {
            KeyType.AES => await _encryptionManager.GenerateAESKeyAsync(keySize, identifier, purpose),
            KeyType.RSA => await _signatureVerifier.GenerateRSAKeyAsync(keySize, identifier, purpose),
            KeyType.ECDSA => await _signatureVerifier.GenerateECDSAKeyAsync(keySize, identifier, purpose),
            KeyType.ChaCha20 => await _encryptionManager.GenerateChaCha20KeyAsync(keySize, identifier, purpose),
            _ => throw new ArgumentException($"Unsupported key type: {keyType}")
        };
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
        return keyAge > _configuration.KeyLifetime;
    }

    private async Task RotateKeyAsync(SecureKeyContainer keyContainer)
    {
        // Generate new key with same parameters
        var newKeyContainer = await GenerateKeyContainerAsync(
            keyContainer.KeyType, keyContainer.KeySize,
            keyContainer.Identifier, keyContainer.Purpose);

        // Replace the old key
        _ = _keyStore.TryUpdate(keyContainer.Identifier, newKeyContainer, keyContainer);

        // Securely dispose old key
        keyContainer.Dispose();

        _logger.LogDebugMessage($"Key rotated successfully: {keyContainer.Identifier}");
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

        _disposed = true;

        _keyRotationTimer?.Dispose();
        _operationLock?.Dispose();

        // Dispose components
        _securityAuditor?.Dispose();
        _encryptionManager?.Dispose();
        _hashCalculator?.Dispose();
        _signatureVerifier?.Dispose();
        _certificateValidator?.Dispose();

        // Securely dispose all keys
        foreach (var keyContainer in _keyStore.Values)
        {
            keyContainer.Dispose();
        }
        _keyStore.Clear();

        _logger.LogInfoMessage("CryptographicSecurityOrchestrator disposed");
    }
}
