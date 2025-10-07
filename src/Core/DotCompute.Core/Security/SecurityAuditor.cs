// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using System;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Security;

/// <summary>
/// Provides comprehensive security auditing and validation for cryptographic operations
/// </summary>
public sealed partial class SecurityAuditor : IDisposable
{
    private readonly ILogger _logger;
    private readonly CryptographicConfiguration _configuration;
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

    // Weak algorithms that should not be used
    private static readonly HashSet<string> _weakAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        "DES", "3DES", "RC4", "MD5", "SHA-1", "RSA-1024", "AES-ECB"
    };

    #region LoggerMessage Delegates (Event IDs: 18100-18199)

    private static readonly Action<ILogger, Exception?> _logSecurityAuditorInitialized =
        LoggerMessage.Define(
            MsLogLevel.Debug,
            new EventId(18100, nameof(SecurityAuditorInitialized)),
            "Security Auditor initialized");

    private static readonly Action<ILogger, int, string, Exception?> _logKeyValidationFailedInvalidSize =
        LoggerMessage.Define<int, string>(
            MsLogLevel.Warning,
            new EventId(18101, nameof(KeyValidationFailedInvalidSize)),
            "Key validation failed: Invalid key size {KeySize} for {KeyType}");

    private static readonly Action<ILogger, int, Exception?> _logKeyValidationFailedBelowThreshold =
        LoggerMessage.Define<int>(
            MsLogLevel.Warning,
            new EventId(18102, nameof(KeyValidationFailedBelowThreshold)),
            "Key validation failed: RSA key size {KeySize} below security threshold");

    private static readonly Action<ILogger, string, int, Exception?> _logKeyParametersValidated =
        LoggerMessage.Define<string, int>(
            MsLogLevel.Debug,
            new EventId(18103, nameof(KeyParametersValidated)),
            "Key parameters validated successfully: {KeyType}, {KeySize} bits");

    private static readonly Action<ILogger, string, Exception?> _logAlgorithmValidationFailedWeak =
        LoggerMessage.Define<string>(
            MsLogLevel.Warning,
            new EventId(18104, nameof(AlgorithmValidationFailedWeak)),
            "Algorithm validation failed: {Algorithm} is considered weak");

    private static readonly Action<ILogger, string, Exception?> _logAlgorithmValidationFailedNotApproved =
        LoggerMessage.Define<string>(
            MsLogLevel.Warning,
            new EventId(18105, nameof(AlgorithmValidationFailedNotApproved)),
            "Algorithm validation failed: {Algorithm} not in approved list");

    private static readonly Action<ILogger, string, Exception?> _logAlgorithmValidationFailedRequiresApproval =
        LoggerMessage.Define<string>(
            MsLogLevel.Warning,
            new EventId(18106, nameof(AlgorithmValidationFailedRequiresApproval)),
            "Algorithm validation failed: {Algorithm} requires additional approval");

    private static readonly Action<ILogger, string, Exception?> _logAlgorithmValidatedSuccessfully =
        LoggerMessage.Define<string>(
            MsLogLevel.Debug,
            new EventId(18107, nameof(AlgorithmValidatedSuccessfully)),
            "Algorithm validated successfully: {Algorithm}");

    private static readonly Action<ILogger, string, Exception?> _logHashAlgorithmValidationFailedWeak =
        LoggerMessage.Define<string>(
            MsLogLevel.Warning,
            new EventId(18108, nameof(HashAlgorithmValidationFailedWeak)),
            "Hash algorithm validation failed: {HashAlgorithm} is considered weak");

    private static readonly Action<ILogger, string, Exception?> _logHashAlgorithmValidationFailedNotApproved =
        LoggerMessage.Define<string>(
            MsLogLevel.Warning,
            new EventId(18109, nameof(HashAlgorithmValidationFailedNotApproved)),
            "Hash algorithm validation failed: {HashAlgorithm} not in approved list");

    private static readonly Action<ILogger, Exception?> _logHashAlgorithmValidationFailedSha1Broken =
        LoggerMessage.Define(
            MsLogLevel.Warning,
            new EventId(18110, nameof(HashAlgorithmValidationFailedSha1Broken)),
            "Hash algorithm validation failed: SHA-1 is cryptographically broken");

    private static readonly Action<ILogger, string, Exception?> _logHashAlgorithmValidatedSuccessfully =
        LoggerMessage.Define<string>(
            MsLogLevel.Debug,
            new EventId(18111, nameof(HashAlgorithmValidatedSuccessfully)),
            "Hash algorithm validated successfully: {HashAlgorithm}");

    private static readonly Action<ILogger, string, bool, int, Exception?> _logCryptographicAlgorithmValidationCompleted =
        LoggerMessage.Define<string, bool, int>(
            MsLogLevel.Debug,
            new EventId(18112, nameof(CryptographicAlgorithmValidationCompleted)),
            "Cryptographic algorithm validation completed: Algorithm={Algorithm}, Approved={IsApproved}, Issues={IssueCount}");

    private static readonly Action<ILogger, Exception?> _logSecurityAuditorDisposed =
        LoggerMessage.Define(
            MsLogLevel.Debug,
            new EventId(18113, nameof(SecurityAuditorDisposed)),
            "Security Auditor disposed");

    private static void SecurityAuditorInitialized(ILogger logger)
        => _logSecurityAuditorInitialized(logger, null);

    private static void KeyValidationFailedInvalidSize(ILogger logger, int keySize, string keyType)
        => _logKeyValidationFailedInvalidSize(logger, keySize, keyType, null);

    private static void KeyValidationFailedBelowThreshold(ILogger logger, int keySize)
        => _logKeyValidationFailedBelowThreshold(logger, keySize, null);

    private static void KeyParametersValidated(ILogger logger, string keyType, int keySize)
        => _logKeyParametersValidated(logger, keyType, keySize, null);

    private static void AlgorithmValidationFailedWeak(ILogger logger, string algorithm)
        => _logAlgorithmValidationFailedWeak(logger, algorithm, null);

    private static void AlgorithmValidationFailedNotApproved(ILogger logger, string algorithm)
        => _logAlgorithmValidationFailedNotApproved(logger, algorithm, null);

    private static void AlgorithmValidationFailedRequiresApproval(ILogger logger, string algorithm)
        => _logAlgorithmValidationFailedRequiresApproval(logger, algorithm, null);

    private static void AlgorithmValidatedSuccessfully(ILogger logger, string algorithm)
        => _logAlgorithmValidatedSuccessfully(logger, algorithm, null);

    private static void HashAlgorithmValidationFailedWeak(ILogger logger, string hashAlgorithm)
        => _logHashAlgorithmValidationFailedWeak(logger, hashAlgorithm, null);

    private static void HashAlgorithmValidationFailedNotApproved(ILogger logger, string hashAlgorithm)
        => _logHashAlgorithmValidationFailedNotApproved(logger, hashAlgorithm, null);

    private static void HashAlgorithmValidationFailedSha1Broken(ILogger logger)
        => _logHashAlgorithmValidationFailedSha1Broken(logger, null);

    private static void HashAlgorithmValidatedSuccessfully(ILogger logger, string hashAlgorithm)
        => _logHashAlgorithmValidatedSuccessfully(logger, hashAlgorithm, null);

    private static void CryptographicAlgorithmValidationCompleted(ILogger logger, string algorithm, bool isApproved, int issueCount)
        => _logCryptographicAlgorithmValidationCompleted(logger, algorithm, isApproved, issueCount, null);

    private static void SecurityAuditorDisposed(ILogger logger)
        => _logSecurityAuditorDisposed(logger, null);

    #endregion

    /// <summary>
    /// Initializes a new instance of the SecurityAuditor class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>

    public SecurityAuditor(ILogger logger, CryptographicConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        SecurityAuditorInitialized(_logger);
    }

    /// <summary>
    /// Validates key generation parameters for security compliance
    /// </summary>
    /// <param name="keyType">Type of key to validate</param>
    /// <param name="keySize">Size of the key in bits</param>
    /// <param name="result">Result object to populate with validation outcome</param>
    /// <returns>True if parameters are valid, false otherwise</returns>
    public bool ValidateKeyParameters(KeyType keyType, int keySize, KeyGenerationResult result)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SecurityAuditor));
        }

        var validSizes = keyType switch
        {
            KeyType.AES => [128, 192, 256],
            KeyType.RSA => [2048, 3072, 4096],
            KeyType.ECDSA => [256, 384, 521],
            KeyType.ChaCha20 => [256],
            _ => Array.Empty<int>()
        };

        if (!validSizes.Contains(keySize))
        {
            result.ErrorMessage = $"Invalid key size {keySize} for {keyType}. Valid sizes: {string.Join(", ", validSizes)}";
            KeyValidationFailedInvalidSize(_logger, keySize, keyType.ToString());
            return false;
        }

        // Additional security checks
        if (keyType == KeyType.RSA && keySize < 2048)
        {
            result.ErrorMessage = $"RSA key size {keySize} is below minimum secure size of 2048 bits";
            KeyValidationFailedBelowThreshold(_logger, keySize);
            return false;
        }

        KeyParametersValidated(_logger, keyType.ToString(), keySize);
        return true;
    }

    /// <summary>
    /// Validates encryption/decryption algorithm for security compliance
    /// </summary>
    /// <param name="algorithm">Algorithm to validate</param>
    /// <param name="result">Result object to populate with validation outcome</param>
    /// <returns>True if algorithm is approved, false otherwise</returns>
    public bool ValidateAlgorithm<T>(string algorithm, T result) where T : ICryptographicResult
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SecurityAuditor));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);

        // Check for weak algorithms
        if (_weakAlgorithms.Contains(algorithm))
        {
            result.ErrorMessage = $"Algorithm '{algorithm}' is not approved for security reasons";
            AlgorithmValidationFailedWeak(_logger, algorithm);
            return false;
        }

        // Check if algorithm is in approved list
        if (!_approvedCiphers.Contains(algorithm) && !_approvedHashAlgorithms.Contains(algorithm))
        {
            result.ErrorMessage = $"Algorithm '{algorithm}' is not in the approved list";
            AlgorithmValidationFailedNotApproved(_logger, algorithm);
            return false;
        }

        // Additional checks for specific algorithms - always require fully approved algorithms for security
        if (!IsAlgorithmFullyApproved(algorithm))
        {
            result.ErrorMessage = $"Algorithm '{algorithm}' requires additional approval for secure usage";
            AlgorithmValidationFailedRequiresApproval(_logger, algorithm);
            return false;
        }

        AlgorithmValidatedSuccessfully(_logger, algorithm);
        return true;
    }

    /// <summary>
    /// Validates hash algorithm for security compliance
    /// </summary>
    /// <param name="hashAlgorithm">Hash algorithm to validate</param>
    /// <param name="result">Result object to populate with validation outcome</param>
    /// <returns>True if hash algorithm is approved, false otherwise</returns>
    public bool ValidateHashAlgorithm<T>(string hashAlgorithm, T result) where T : ICryptographicResult
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SecurityAuditor));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(hashAlgorithm);

        // Check for weak hash algorithms
        if (_weakAlgorithms.Contains(hashAlgorithm))
        {
            result.ErrorMessage = $"Hash algorithm '{hashAlgorithm}' is not approved for security reasons";
            HashAlgorithmValidationFailedWeak(_logger, hashAlgorithm);
            return false;
        }

        // Check if hash algorithm is approved
        if (!_approvedHashAlgorithms.Contains(hashAlgorithm))
        {
            result.ErrorMessage = $"Hash algorithm '{hashAlgorithm}' is not in the approved list";
            HashAlgorithmValidationFailedNotApproved(_logger, hashAlgorithm);
            return false;
        }

        // Special check for SHA-1
        if (hashAlgorithm.Equals("SHA-1", StringComparison.OrdinalIgnoreCase))
        {
            result.ErrorMessage = "SHA-1 is cryptographically broken and should not be used";
            HashAlgorithmValidationFailedSha1Broken(_logger);
            return false;
        }

        HashAlgorithmValidatedSuccessfully(_logger, hashAlgorithm);
        return true;
    }

    /// <summary>
    /// Validates cryptographic algorithms and configurations for comprehensive security compliance
    /// </summary>
    /// <param name="algorithm">Algorithm to validate</param>
    /// <param name="keySize">Key size to validate</param>
    /// <param name="context">Context of usage</param>
    /// <returns>Detailed algorithm validation result</returns>
    public AlgorithmValidationResult ValidateCryptographicAlgorithm(string algorithm, int keySize, string context)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SecurityAuditor));
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
        if (_weakAlgorithms.Contains(algorithm))
        {
            result.IsApproved = false;
            result.Issues.Add($"Algorithm '{algorithm}' is considered cryptographically weak");
            result.Recommendations.Add($"Consider using approved alternatives: {string.Join(", ", _approvedCiphers)}");
            return result;
        }

        // Validate specific algorithm requirements
        result.IsApproved = algorithm switch
        {
            var alg when alg.StartsWith("AES", StringComparison.OrdinalIgnoreCase) => ValidateAESConfiguration(alg, keySize, result),
            var alg when alg.StartsWith("RSA", StringComparison.OrdinalIgnoreCase) => ValidateRSAConfiguration(keySize, result),
            var alg when alg.StartsWith("ECDSA", StringComparison.Ordinal) => ValidateECDSAConfiguration(keySize, result),
            var alg when alg.Contains("SHA", StringComparison.Ordinal) => ValidateSHAConfiguration(alg, result),
            var alg when alg.Contains("ChaCha20", StringComparison.Ordinal) => ValidateChaCha20Configuration(keySize, result),
            _ => ValidateGenericAlgorithm(algorithm, keySize, result)
        };

        // Check for timing attack vulnerabilities
        if (result.IsApproved && !IsTimingAttackSafe(algorithm))
        {
            result.Issues.Add($"Algorithm '{algorithm}' may be vulnerable to timing attacks");
            result.Recommendations.Add("Ensure constant-time implementation is used");
        }

        // Context-specific validations
        ValidateAlgorithmContext(algorithm, context, result);

        CryptographicAlgorithmValidationCompleted(_logger, algorithm, result.IsApproved, result.Issues.Count);

        return result;
    }

    private static bool ValidateAESConfiguration(string algorithm, int keySize, AlgorithmValidationResult result)
    {
        var validKeySizes = new[] { 128, 192, 256 };
        if (!validKeySizes.Contains(keySize))
        {
            result.Issues.Add($"Invalid AES key size: {keySize}. Valid sizes: 128, 192, 256");
            result.Recommendations.Add("Use a standard AES key size");
            return false;
        }

        if (algorithm.Contains("ECB", StringComparison.Ordinal))
        {
            result.Issues.Add("AES-ECB mode is not secure for most applications");
            result.Recommendations.Add("Use AES-GCM or AES-CBC with proper IV");
            return false;
        }

        return true;
    }

    private static bool ValidateRSAConfiguration(int keySize, AlgorithmValidationResult result)
    {
        if (keySize < 2048)
        {
            result.Issues.Add($"RSA key size {keySize} is below minimum secure size of 2048 bits");
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
            result.Issues.Add($"Invalid ECDSA key size: {keySize}. Valid sizes: 256, 384, 521");
            result.Recommendations.Add("Use P-256, P-384, or P-521 curves");
            return false;
        }

        return true;
    }

    private static bool ValidateSHAConfiguration(string algorithm, AlgorithmValidationResult result)
    {
        if (algorithm.Equals("SHA-1", StringComparison.OrdinalIgnoreCase))
        {
            result.Issues.Add("SHA-1 is cryptographically broken and should not be used");
            result.Recommendations.Add("Use SHA-256, SHA-384, or SHA-512");
            return false;
        }

        return true;
    }

    private static bool ValidateChaCha20Configuration(int keySize, AlgorithmValidationResult result)
    {
        if (keySize != 256)
        {
            result.Issues.Add($"ChaCha20 requires 256-bit keys, got {keySize}");
            result.Recommendations.Add("Use 256-bit keys for ChaCha20");
            return false;
        }

        return true;
    }

    private static bool ValidateGenericAlgorithm(string algorithm, int keySize, AlgorithmValidationResult result)
    {
        // Add basic validation for other algorithms
        if (string.IsNullOrWhiteSpace(algorithm))
        {
            result.Issues.Add("Algorithm name cannot be empty");
            return false;
        }

        // Add to approved list if it passes basic checks
        result.Recommendations.Add("Verify this algorithm meets your security requirements");
        return true;
    }

    private static void ValidateAlgorithmContext(string algorithm, string context, AlgorithmValidationResult result)
    {
        // Context-specific validations
        switch (context.ToLowerInvariant())
        {
            case "storage":
                if (!algorithm.Contains("GCM", StringComparison.Ordinal) && !algorithm.Contains("Poly1305", StringComparison.Ordinal))
                {
                    result.Recommendations.Add("Consider using authenticated encryption for data storage");
                }
                break;
            case "transmission":
                if (!algorithm.Contains("GCM", StringComparison.Ordinal) && !algorithm.Contains("Poly1305", StringComparison.Ordinal))
                {
                    result.Issues.Add("Non-authenticated encryption may be vulnerable to tampering during transmission");
                    result.Recommendations.Add("Use authenticated encryption modes like AES-GCM or ChaCha20-Poly1305");
                }
                break;
            case "signing":
                if (!algorithm.StartsWith("RSA", StringComparison.Ordinal) && !algorithm.StartsWith("ECDSA", StringComparison.Ordinal))
                {
                    result.Issues.Add("Algorithm not suitable for digital signatures");
                    result.Recommendations.Add("Use RSA or ECDSA for digital signatures");
                }
                break;
        }
    }

    private static bool IsAlgorithmFullyApproved(string algorithm)
    {
        // Additional approval checks beyond basic approved list
        return algorithm switch
        {
            "AES-256-GCM" => true,
            "ChaCha20-Poly1305" => true,
            "SHA-256" => true,
            "SHA-384" => true,
            "SHA-512" => true,
            _ => false
        };
    }

    private static bool IsTimingAttackSafe(string algorithm)
    {
        // Algorithms known to have constant-time implementations
        var constantTimeAlgorithms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AES-256-GCM", "AES-192-GCM", "ChaCha20-Poly1305", "SHA-256", "SHA-384", "SHA-512"
        };

        return constantTimeAlgorithms.Contains(algorithm);
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            SecurityAuditorDisposed(_logger);
        }
    }
}
