// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;

namespace DotCompute.Core.Security;

/// <summary>
/// Provides comprehensive security auditing and validation for cryptographic operations
/// </summary>
public sealed class SecurityAuditor : IDisposable
{
    private readonly ILogger _logger;
    private readonly CryptographicConfiguration _configuration;
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

    // Weak algorithms that should not be used
    private static readonly HashSet<string> WeakAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        "DES", "3DES", "RC4", "MD5", "SHA-1", "RSA-1024", "AES-ECB"
    };

    public SecurityAuditor(ILogger logger, CryptographicConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        _logger.LogDebugMessage("Security Auditor initialized");
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
            KeyType.AES => new[] { 128, 192, 256 },
            KeyType.RSA => new[] { 2048, 3072, 4096 },
            KeyType.ECDSA => new[] { 256, 384, 521 },
            KeyType.ChaCha20 => new[] { 256 },
            _ => Array.Empty<int>()
        };

        if (!validSizes.Contains(keySize))
        {
            result.ErrorMessage = $"Invalid key size {keySize} for {keyType}. Valid sizes: {string.Join(", ", validSizes)}";
            _logger.LogWarning($"Key validation failed: Invalid key size {keySize} for {keyType}");
            return false;
        }

        // Additional security checks
        if (keyType == KeyType.RSA && keySize < 2048)
        {
            result.ErrorMessage = $"RSA key size {keySize} is below minimum secure size of 2048 bits";
            _logger.LogWarning($"Key validation failed: RSA key size {keySize} below security threshold");
            return false;
        }

        _logger.LogDebugMessage($"Key parameters validated successfully: {keyType}, {keySize} bits");
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
        if (WeakAlgorithms.Contains(algorithm))
        {
            result.ErrorMessage = $"Algorithm '{algorithm}' is not approved for security reasons";
            _logger.LogWarning($"Algorithm validation failed: {algorithm} is considered weak");
            return false;
        }

        // Check if algorithm is in approved list
        if (!ApprovedCiphers.Contains(algorithm) && !ApprovedHashAlgorithms.Contains(algorithm))
        {
            result.ErrorMessage = $"Algorithm '{algorithm}' is not in the approved list";
            _logger.LogWarning($"Algorithm validation failed: {algorithm} not in approved list");
            return false;
        }

        // Additional checks for specific algorithms
        if (_configuration.RequireApprovedAlgorithms && !IsAlgorithmFullyApproved(algorithm))
        {
            result.ErrorMessage = $"Algorithm '{algorithm}' requires additional approval for this configuration";
            _logger.LogWarning($"Algorithm validation failed: {algorithm} requires additional approval");
            return false;
        }

        _logger.LogDebugMessage($"Algorithm validated successfully: {algorithm}");
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
        if (WeakAlgorithms.Contains(hashAlgorithm))
        {
            result.ErrorMessage = $"Hash algorithm '{hashAlgorithm}' is not approved for security reasons";
            _logger.LogWarning($"Hash algorithm validation failed: {hashAlgorithm} is considered weak");
            return false;
        }

        // Check if hash algorithm is approved
        if (!ApprovedHashAlgorithms.Contains(hashAlgorithm))
        {
            result.ErrorMessage = $"Hash algorithm '{hashAlgorithm}' is not in the approved list";
            _logger.LogWarning($"Hash algorithm validation failed: {hashAlgorithm} not in approved list");
            return false;
        }

        // Special check for SHA-1
        if (hashAlgorithm.Equals("SHA-1", StringComparison.OrdinalIgnoreCase))
        {
            result.ErrorMessage = "SHA-1 is cryptographically broken and should not be used";
            _logger.LogWarning("Hash algorithm validation failed: SHA-1 is cryptographically broken");
            return false;
        }

        _logger.LogDebugMessage($"Hash algorithm validated successfully: {hashAlgorithm}");
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
        if (WeakAlgorithms.Contains(algorithm))
        {
            result.IsApproved = false;
            result.Issues.Add($"Algorithm '{algorithm}' is considered cryptographically weak");
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
            var alg when alg.Contains("ChaCha20") => ValidateChaCha20Configuration(keySize, result),
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

        _logger.LogDebugMessage($"Cryptographic algorithm validation completed: Algorithm={algorithm}, Approved={result.IsApproved}, Issues={result.SecurityIssues.Count}");

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

        if (algorithm.Contains("ECB"))
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
                if (!algorithm.Contains("GCM") && !algorithm.Contains("Poly1305"))
                {
                    result.Recommendations.Add("Consider using authenticated encryption for data storage");
                }
                break;
            case "transmission":
                if (!algorithm.Contains("GCM") && !algorithm.Contains("Poly1305"))
                {
                    result.Issues.Add("Non-authenticated encryption may be vulnerable to tampering during transmission");
                    result.Recommendations.Add("Use authenticated encryption modes like AES-GCM or ChaCha20-Poly1305");
                }
                break;
            case "signing":
                if (!algorithm.StartsWith("RSA") && !algorithm.StartsWith("ECDSA"))
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

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _logger.LogDebugMessage("Security Auditor disposed");
        }
    }
}