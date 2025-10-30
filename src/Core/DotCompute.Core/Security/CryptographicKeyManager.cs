// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Security;

/// <summary>
/// Manages cryptographic keys including generation, rotation, and secure storage.
/// Provides secure key lifecycle management with automatic rotation capabilities.
/// </summary>
internal sealed partial class CryptographicKeyManager : IDisposable
{
    // LoggerMessage delegates - Event ID range 18400-18499 for CryptographicKeyManager (Security module)
    private static readonly Action<ILogger, Exception?> _logManagerInitialized =
        LoggerMessage.Define(
            MsLogLevel.Information,
            new EventId(18400, nameof(LogManagerInitialized)),
            "CryptographicKeyManager initialized with automatic key rotation");

    private static readonly Action<ILogger, string, int, string, Exception?> _logKeyGeneration =
        LoggerMessage.Define<string, int, string>(
            MsLogLevel.Information,
            new EventId(18401, nameof(LogKeyGeneration)),
            "Generating new cryptographic key: Type={KeyType}, Size={KeySize}, Id={Identifier}");

    private static readonly Action<ILogger, string, string, Exception?> _logKeyGenerationSuccess =
        LoggerMessage.Define<string, string>(
            MsLogLevel.Information,
            new EventId(18402, nameof(LogKeyGenerationSuccess)),
            "Key generated successfully: Id={Identifier}, Fingerprint={Fingerprint}");

    private static readonly Action<ILogger, string, string, Exception?> _logKeyGenerationFailed =
        LoggerMessage.Define<string, string>(
            MsLogLevel.Error,
            new EventId(18403, nameof(LogKeyGenerationFailed)),
            "Key generation failed: Type={KeyType}, Id={Identifier}");

    private static readonly Action<ILogger, string, Exception?> _logKeyRetrieved =
        LoggerMessage.Define<string>(
            MsLogLevel.Debug,
            new EventId(18404, nameof(LogKeyRetrieved)),
            "Key retrieved: Id={Identifier}");

    private static readonly Action<ILogger, bool, Exception?> _logKeyRotationStarted =
        LoggerMessage.Define<bool>(
            MsLogLevel.Information,
            new EventId(18405, nameof(LogKeyRotationStarted)),
            "Starting key rotation: Force={ForceRotation}");

    private static readonly Action<ILogger, string, Exception?> _logKeyRotationError =
        LoggerMessage.Define<string>(
            MsLogLevel.Error,
            new EventId(18406, nameof(LogKeyRotationError)),
            "Failed to rotate key: {KeyId}");

    private static readonly Action<ILogger, int, int, Exception?> _logKeyRotationCompleted =
        LoggerMessage.Define<int, int>(
            MsLogLevel.Information,
            new EventId(18407, nameof(LogKeyRotationCompleted)),
            "Key rotation completed: Rotated={RotatedCount}, Failed={FailedCount}");

    private static readonly Action<ILogger, string, Exception?> _logKeyDeleted =
        LoggerMessage.Define<string>(
            MsLogLevel.Information,
            new EventId(18408, nameof(LogKeyDeleted)),
            "Key deleted successfully: Id={Identifier}");

    private static readonly Action<ILogger, string, string, Exception?> _logKeyExported =
        LoggerMessage.Define<string, string>(
            MsLogLevel.Information,
            new EventId(18409, nameof(LogKeyExported)),
            "Key exported successfully: Id={Identifier}, Format={Format}");

    private static readonly Action<ILogger, string, Exception?> _logKeyExportFailed =
        LoggerMessage.Define<string>(
            MsLogLevel.Error,
            new EventId(18410, nameof(LogKeyExportFailed)),
            "Key export failed: Id={Identifier}");

    private static readonly Action<ILogger, Exception?> _logAutoRotationFailed =
        LoggerMessage.Define(
            MsLogLevel.Error,
            new EventId(18411, nameof(LogAutoRotationFailed)),
            "Automatic key rotation failed");

    [LoggerMessage(EventId = 18412, Level = MsLogLevel.Warning, Message = "Attempted to access expired key: {Identifier}")]
    private static partial void LogExpiredKeyAccess(ILogger logger, string identifier);

    [LoggerMessage(EventId = 18413, Level = MsLogLevel.Warning, Message = "Key not found: {Identifier}")]
    private static partial void LogKeyNotFound(ILogger logger, string identifier);

    [LoggerMessage(EventId = 18414, Level = MsLogLevel.Warning, Message = "Attempted to delete non-existent key: {Identifier}")]
    private static partial void LogNonExistentKeyDelete(ILogger logger, string identifier);

    // Wrapper methods
    private static void LogManagerInitialized(ILogger logger)
        => _logManagerInitialized(logger, null);

    private static void LogKeyGeneration(ILogger logger, string keyType, int keySize, string identifier)
        => _logKeyGeneration(logger, keyType, keySize, identifier, null);

    private static void LogKeyGenerationSuccess(ILogger logger, string identifier, string fingerprint)
        => _logKeyGenerationSuccess(logger, identifier, fingerprint, null);

    private static void LogKeyGenerationFailed(ILogger logger, Exception ex, string keyType, string identifier)
        => _logKeyGenerationFailed(logger, keyType, identifier, ex);

    private static void LogKeyRetrieved(ILogger logger, string identifier)
        => _logKeyRetrieved(logger, identifier, null);

    private static void LogKeyRotationStarted(ILogger logger, bool forceRotation)
        => _logKeyRotationStarted(logger, forceRotation, null);

    private static void LogKeyRotationError(ILogger logger, Exception ex, string keyId)
        => _logKeyRotationError(logger, keyId, ex);

    private static void LogKeyRotationCompleted(ILogger logger, int rotatedCount, int failedCount)
        => _logKeyRotationCompleted(logger, rotatedCount, failedCount, null);

    private static void LogKeyDeleted(ILogger logger, string identifier)
        => _logKeyDeleted(logger, identifier, null);

    private static void LogKeyExported(ILogger logger, string identifier, string format)
        => _logKeyExported(logger, identifier, format, null);

    private static void LogKeyExportFailed(ILogger logger, Exception ex, string identifier)
        => _logKeyExportFailed(logger, identifier, ex);

    private static void LogAutoRotationFailed(ILogger logger, Exception ex)
        => _logAutoRotationFailed(logger, ex);

    private readonly ILogger<CryptographicKeyManager> _logger;
    private readonly CryptographicConfiguration _configuration;
    private readonly ConcurrentDictionary<string, SecureKeyContainer> _keyStore;
    private readonly RandomNumberGenerator _randomGenerator;
    private readonly Timer _keyRotationTimer;
    private readonly SemaphoreSlim _operationLock;
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the CryptographicKeyManager class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>

    public CryptographicKeyManager(
        ILogger<CryptographicKeyManager> logger,
        CryptographicConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _keyStore = new ConcurrentDictionary<string, SecureKeyContainer>();
        _randomGenerator = RandomNumberGenerator.Create();
        _operationLock = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);

        // Initialize key rotation timer
        _keyRotationTimer = new Timer(PerformKeyRotation, null,
            _configuration.KeyRotationInterval, _configuration.KeyRotationInterval);

        LogManagerInitialized(_logger);
    }

    /// <summary>
    /// Generates a new cryptographically secure key.
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
            LogKeyGeneration(_logger, keyType.ToString(), keySize, identifier);

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

            // Check if key already exists
            if (_keyStore.ContainsKey(identifier))
            {
                result.ErrorMessage = $"Key with identifier '{identifier}' already exists";
                return result;
            }

            // Generate the key based on type
            var keyContainer = keyType switch
            {
                KeyType.AES => GenerateAesKey(keySize, identifier, purpose),
                KeyType.RSA => GenerateRsaKey(keySize, identifier, purpose),
                KeyType.ECDSA => GenerateEcdsaKey(keySize, identifier, purpose),
                KeyType.ChaCha20 => GenerateChaCha20Key(keySize, identifier, purpose),
                _ => throw new NotSupportedException($"Key type not supported: {keyType}")
            };

            // Store the key securely
            if (_keyStore.TryAdd(identifier, keyContainer))
            {
                result.IsSuccessful = true;
                result.Fingerprint = ComputeKeyFingerprint(keyContainer);

                LogKeyGenerationSuccess(_logger, identifier, result.Fingerprint);
            }
            else
            {
                result.ErrorMessage = "Failed to store the generated key";
            }

            return result;
        }
        catch (Exception ex)
        {
            LogKeyGenerationFailed(_logger, ex, keyType.ToString(), identifier);
            return new KeyGenerationResult
            {
                KeyType = keyType,
                KeySize = keySize,
                Identifier = identifier,
                Purpose = purpose,
                GenerationTime = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }

    /// <summary>
    /// Retrieves a key from secure storage.
    /// </summary>
    public async Task<SecureKeyContainer?> GetKeyAsync(string identifier)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        await _operationLock.WaitAsync();
        try
        {
            if (_keyStore.TryGetValue(identifier, out var keyContainer))
            {
                // Check if key is expired
                if (IsKeyExpired(keyContainer))
                {
                    LogExpiredKeyAccess(_logger, identifier);
                    return null;
                }

                LogKeyRetrieved(_logger, identifier);
                return keyContainer;
            }

            LogKeyNotFound(_logger, identifier);
            return null;
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }

    /// <summary>
    /// Rotates cryptographic keys based on age and policy.
    /// </summary>
    public async Task<KeyRotationResult> RotateKeysAsync(bool forceRotation = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _operationLock.WaitAsync();
        try
        {
            LogKeyRotationStarted(_logger, forceRotation);

            var result = new KeyRotationResult
            {
                RotationTime = DateTimeOffset.UtcNow,
                ForceRotation = forceRotation
            };

            var keysToRotate = new List<string>();
            var rotationThreshold = DateTimeOffset.UtcNow - _configuration.KeyRotationInterval;

            // Identify keys that need rotation
            foreach (var kvp in _keyStore)
            {
                var key = kvp.Value;
                var shouldRotate = forceRotation ||
                                 key.CreationTime < rotationThreshold ||
                                 IsKeyExpired(key);

                if (shouldRotate)
                {
                    keysToRotate.Add(kvp.Key);
                }
            }

            // Rotate identified keys
            foreach (var keyId in keysToRotate)
            {
                try
                {
                    var rotationResult = await RotateSingleKeyAsync(keyId);
                    if (rotationResult.IsSuccessful)
                    {
                        result.RotatedKeys.Add(keyId);
                    }
                    else
                    {
                        result.FailedRotations.Add($"{keyId}: {rotationResult.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    LogKeyRotationError(_logger, ex, keyId);
                    result.FailedRotations.Add($"{keyId}: {ex.Message}");
                }
            }

            result.IsSuccessful = result.FailedRotations.Count == 0;

            LogKeyRotationCompleted(_logger, result.RotatedKeys.Count, result.FailedRotations.Count);

            return result;
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }

    /// <summary>
    /// Deletes a key from secure storage.
    /// </summary>
    public async Task<bool> DeleteKeyAsync(string identifier)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        await _operationLock.WaitAsync();
        try
        {
            if (_keyStore.TryRemove(identifier, out var removedKey))
            {
                // Securely dispose of key material
                removedKey.Dispose();
                LogKeyDeleted(_logger, identifier);
                return true;
            }

            LogNonExistentKeyDelete(_logger, identifier);
            return false;
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }

    /// <summary>
    /// Lists all stored key identifiers with metadata.
    /// </summary>
    public async Task<IEnumerable<KeyMetadata>> ListKeysAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _operationLock.WaitAsync();
        try
        {
            var keyMetadata = new List<KeyMetadata>();

            foreach (var kvp in _keyStore)
            {
                var key = kvp.Value;
                var metadata = new KeyMetadata
                {
                    Identifier = key.Identifier,
                    KeyType = key.KeyType,
                    KeySize = key.KeySize,
                    Purpose = key.Purpose,
                    CreationTime = key.CreationTime,
                    IsExpired = IsKeyExpired(key),
                    Fingerprint = ComputeKeyFingerprint(key)
                };

                keyMetadata.Add(metadata);
            }

            return keyMetadata;
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }

    /// <summary>
    /// Exports a key in the specified format for backup or transfer.
    /// </summary>
    public async Task<KeyExportResult> ExportKeyAsync(string identifier, KeyExportFormat format, string? passphrase = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        await _operationLock.WaitAsync();
        try
        {
            var result = new KeyExportResult
            {
                Identifier = identifier,
                Format = format,
                ExportTime = DateTimeOffset.UtcNow
            };

            if (!_keyStore.TryGetValue(identifier, out var keyContainer))
            {
                result.ErrorMessage = $"Key not found: {identifier}";
                return result;
            }

            // Export key based on format
            result = format switch
            {
                KeyExportFormat.Pkcs8 => ExportAsPkcs8(keyContainer, passphrase, result),
                KeyExportFormat.Pkcs12 => ExportAsPkcs12(keyContainer, passphrase, result),
                KeyExportFormat.Jwk => ExportAsJwk(keyContainer, result),
                _ => throw new NotSupportedException($"Export format not supported: {format}")
            };

            result.IsSuccessful = true;
            LogKeyExported(_logger, identifier, format.ToString());

            return result;
        }
        catch (Exception ex)
        {
            LogKeyExportFailed(_logger, ex, identifier);
            return new KeyExportResult
            {
                Identifier = identifier,
                Format = format,
                ExportTime = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            _ = _operationLock.Release();
        }
    }

    // Private implementation methods

    private static bool ValidateKeyParameters(KeyType keyType, int keySize, KeyGenerationResult result)
    {
        var isValid = keyType switch
        {
            KeyType.AES => keySize is 128 or 192 or 256,
            KeyType.RSA => keySize >= 2048 && keySize <= 4096 && keySize % 1024 == 0,
            KeyType.ECDSA => keySize is 256 or 384 or 521,
            KeyType.ChaCha20 => keySize == 256,
            _ => false
        };

        if (!isValid)
        {
            result.ErrorMessage = $"Invalid key size {keySize} for key type {keyType}";
        }

        return isValid;
    }

    private SecureKeyContainer GenerateAesKey(int keySize, string identifier, string purpose)
    {
        var keyBytes = new byte[keySize / 8];
        _randomGenerator.GetBytes(keyBytes);

        return new SecureKeyContainer(KeyType.AES, keyBytes, identifier, purpose, keySize);
    }

    private static SecureKeyContainer GenerateRsaKey(int keySize, string identifier, string purpose)
    {
        using var rsa = RSA.Create(keySize);
        var privateKey = rsa.ExportPkcs8PrivateKey();

        return new SecureKeyContainer(KeyType.RSA, privateKey, identifier, purpose, keySize);
    }

    private static SecureKeyContainer GenerateEcdsaKey(int keySize, string identifier, string purpose)
    {
        var curve = keySize switch
        {
            256 => ECCurve.NamedCurves.nistP256,
            384 => ECCurve.NamedCurves.nistP384,
            521 => ECCurve.NamedCurves.nistP521,
            _ => throw new ArgumentException($"Unsupported ECDSA key size: {keySize}")
        };

        using var ecdsa = ECDsa.Create(curve);
        var privateKey = ecdsa.ExportPkcs8PrivateKey();

        return new SecureKeyContainer(KeyType.ECDSA, privateKey, identifier, purpose, keySize);
    }

    private SecureKeyContainer GenerateChaCha20Key(int keySize, string identifier, string purpose)
    {
        var keyBytes = new byte[32]; // ChaCha20 uses 256-bit keys
        _randomGenerator.GetBytes(keyBytes);

        return new SecureKeyContainer(KeyType.ChaCha20, keyBytes, identifier, purpose, keySize);
    }

    private bool IsKeyExpired(SecureKeyContainer key)
    {
        var maxAge = _configuration.KeyLifetime;
        return DateTimeOffset.UtcNow - key.CreationTime > maxAge;
    }

    private static string ComputeKeyFingerprint(SecureKeyContainer key)
    {
        var hash = SHA256.HashData(key.GetKeyBytes());
        return Convert.ToHexString(hash)[..16]; // First 16 chars for readability
    }

    private async Task<KeyRotationResult> RotateSingleKeyAsync(string keyId)
    {
        if (!_keyStore.TryGetValue(keyId, out var oldKey))
        {
            return new KeyRotationResult
            {
                RotationTime = DateTimeOffset.UtcNow,
                ErrorMessage = $"Key not found: {keyId}"
            };
        }

        // Generate new key with same parameters
        var newKeyId = $"{keyId}_rotated_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var generationResult = await GenerateKeyAsync(oldKey.KeyType, oldKey.KeySize, newKeyId, oldKey.Purpose);

        if (!generationResult.IsSuccessful)
        {
            return new KeyRotationResult
            {
                RotationTime = DateTimeOffset.UtcNow,
                ErrorMessage = $"Failed to generate rotated key: {generationResult.ErrorMessage}"
            };
        }

        // Mark old key as rotated but keep it for a grace period
        oldKey.IsRotated = true;
        oldKey.RotationTime = DateTimeOffset.UtcNow;

        return new KeyRotationResult
        {
            RotationTime = DateTimeOffset.UtcNow,
            IsSuccessful = true,
            RotatedKeys = [keyId],
            NewKeyIdentifier = newKeyId
        };
    }

    private static KeyExportResult ExportAsPkcs8(SecureKeyContainer key, string? passphrase, KeyExportResult result)
    {
        result.ExportedData = key.GetKeyBytes();
        result.ExportFormat = "PKCS#8";
        return result;
    }

    private KeyExportResult ExportAsPkcs12(SecureKeyContainer key, string? passphrase, KeyExportResult result)
        // PKCS#12 export implementation would go here

        => throw new NotImplementedException("PKCS#12 export not yet implemented");

    private KeyExportResult ExportAsJwk(SecureKeyContainer key, KeyExportResult result)
        // JWK export implementation would go here

        => throw new NotImplementedException("JWK export not yet implemented");

    private void PerformKeyRotation(object? state)
    {
        try
        {
            _ = Task.Run(async () =>
            {
                _ = await RotateKeysAsync();
            });
        }
        catch (Exception ex)
        {
            LogAutoRotationFailed(_logger, ex);
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _keyRotationTimer?.Dispose();
            _operationLock?.Dispose();
            _randomGenerator?.Dispose();

            // Securely dispose all keys
            foreach (var key in _keyStore.Values)
            {
                key?.Dispose();
            }
            _keyStore.Clear();

            _disposed = true;
        }
    }
}

// Supporting classes and enums
/// <summary>
/// Defines cryptographic key export formats.
/// </summary>
public enum KeyExportFormat
{
    /// <summary>
    /// PKCS#8 format for private key export.
    /// </summary>
    Pkcs8,

    /// <summary>
    /// PKCS#12 format for key and certificate bundle export.
    /// </summary>
    Pkcs12,

    /// <summary>
    /// JSON Web Key (JWK) format for web-based key exchange.
    /// </summary>
    Jwk
}
/// <summary>
/// A class that represents key metadata.
/// </summary>

public class KeyMetadata
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    /// <value>The identifier.</value>
    public required string Identifier { get; set; }
    /// <summary>
    /// Gets or sets the key type.
    /// </summary>
    /// <value>The key type.</value>
    public KeyType KeyType { get; set; }
    /// <summary>
    /// Gets or sets the key size.
    /// </summary>
    /// <value>The key size.</value>
    public int KeySize { get; set; }
    /// <summary>
    /// Gets or sets the purpose.
    /// </summary>
    /// <value>The purpose.</value>
    public required string Purpose { get; set; }
    /// <summary>
    /// Gets or sets the creation time.
    /// </summary>
    /// <value>The creation time.</value>
    public DateTimeOffset CreationTime { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether expired.
    /// </summary>
    /// <value>The is expired.</value>
    public bool IsExpired { get; set; }
    /// <summary>
    /// Gets or sets the fingerprint.
    /// </summary>
    /// <value>The fingerprint.</value>
    public required string Fingerprint { get; set; }
}
/// <summary>
/// A class that represents key export result.
/// </summary>

public class KeyExportResult
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    /// <value>The identifier.</value>
    public required string Identifier { get; set; }
    /// <summary>
    /// Gets or sets the format.
    /// </summary>
    /// <value>The format.</value>
    public KeyExportFormat Format { get; set; }
    /// <summary>
    /// Gets or sets the export time.
    /// </summary>
    /// <value>The export time.</value>
    public DateTimeOffset ExportTime { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether successful.
    /// </summary>
    /// <value>The is successful.</value>
    public bool IsSuccessful { get; set; }
    /// <summary>
    /// Gets or sets the exported data.
    /// </summary>
    /// <value>The exported data.</value>
    public IReadOnlyList<byte>? ExportedData { get; set; }
    /// <summary>
    /// Gets or sets the export format.
    /// </summary>
    /// <value>The export format.</value>
    public string? ExportFormat { get; set; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; set; }
}
/// <summary>
/// A class that represents key rotation result.
/// </summary>

public class KeyRotationResult
{
    /// <summary>
    /// Gets or sets the rotation time.
    /// </summary>
    /// <value>The rotation time.</value>
    public DateTimeOffset RotationTime { get; set; }
    /// <summary>
    /// Gets or sets the force rotation.
    /// </summary>
    /// <value>The force rotation.</value>
    public bool ForceRotation { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether successful.
    /// </summary>
    /// <value>The is successful.</value>
    public bool IsSuccessful { get; set; }
    /// <summary>
    /// Gets or sets the rotated keys.
    /// </summary>
    /// <value>The rotated keys.</value>
    public IList<string> RotatedKeys { get; init; } = [];
    /// <summary>
    /// Gets or sets the failed rotations.
    /// </summary>
    /// <value>The failed rotations.</value>
    public IList<string> FailedRotations { get; init; } = [];
    /// <summary>
    /// Gets or sets the new key identifier.
    /// </summary>
    /// <value>The new key identifier.</value>
    public string? NewKeyIdentifier { get; set; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; set; }
}
