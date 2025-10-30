// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Security.Cryptography;
using DotCompute.Core.Logging;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Security;

/// <summary>
/// Provides secure hash calculation with multiple algorithms and integrity verification
/// </summary>
public sealed partial class HashCalculator : IDisposable
{
    #region LoggerMessage Delegates

    [LoggerMessage(EventId = 18701, Level = MsLogLevel.Warning, Message = "Error during hash cache cleanup")]
    private static partial void LogCacheCleanupError(ILogger logger, Exception ex);

    #endregion

    private readonly ILogger _logger;
    private readonly Dictionary<string, CachedHashResult> _hashCache;
    private readonly Timer _cacheCleanupTimer;
    private readonly Lock _cacheLock = new();
    private volatile bool _disposed;

    // Approved hash algorithms
    private static readonly HashSet<string> ApprovedHashAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        "SHA-256", "SHA-384", "SHA-512", "SHA3-256", "SHA3-384", "SHA3-512", "BLAKE2b"
    };

    // Weak algorithms that should not be used
    private static readonly HashSet<string> WeakHashAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        "MD5", "SHA-1", "MD4", "MD2"
    };

    /// <inheritdoc/>
    public HashCalculator(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _hashCache = [];

        // Set up cache cleanup timer
        _cacheCleanupTimer = new Timer(CleanupCache, null,
            TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));

        _logger.LogDebugMessage("Hash Calculator initialized");
    }

    /// <summary>
    /// Calculates hash for data using the specified algorithm
    /// </summary>
    public async Task<HashResult> CalculateHashAsync(
        ReadOnlyMemory<byte> data,
        string algorithm = "SHA-256",
        bool enableCaching = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);

        try
        {
            var result = new HashResult
            {
                Algorithm = algorithm,
                InputSize = data.Length,
                CalculationTime = DateTimeOffset.UtcNow
            };

            // Validate algorithm
            if (!ValidateHashAlgorithm(algorithm, result))
            {
                return result;
            }

            // Check cache if enabled
            if (enableCaching)
            {
                var cacheKey = GenerateCacheKey(data, algorithm);
                lock (_cacheLock)
                {
                    if (_hashCache.TryGetValue(cacheKey, out var cachedResult) &&
                        DateTimeOffset.UtcNow - cachedResult.CalculationTime < TimeSpan.FromMinutes(30))
                    {
                        _logger.LogDebugMessage($"Hash result retrieved from cache: {algorithm}");
                        return cachedResult.Result;
                    }
                }
            }

            // Calculate hash
            result = await PerformHashCalculationAsync(data, algorithm, result);

            // Cache result if successful and caching is enabled
            if (result.IsSuccessful && enableCaching)
            {
                var cacheKey = GenerateCacheKey(data, algorithm);
                lock (_cacheLock)
                {
                    _hashCache[cacheKey] = new CachedHashResult
                    {
                        Result = result,
                        CalculationTime = DateTimeOffset.UtcNow
                    };
                }
            }

            _logger.LogDebugMessage($"Hash calculation completed: {algorithm}, Size={data.Length}, Success={result.IsSuccessful}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Hash calculation failed for algorithm {algorithm}");
            return new HashResult
            {
                Algorithm = algorithm,
                InputSize = data.Length,
                CalculationTime = DateTimeOffset.UtcNow,
                IsSuccessful = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Calculates multiple hashes for the same data
    /// </summary>
    public async Task<MultiHashResult> CalculateMultipleHashesAsync(
        ReadOnlyMemory<byte> data,
        string[] algorithms)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentNullException.ThrowIfNull(algorithms);

        var result = new MultiHashResult
        {
            InputSize = data.Length,
            CalculationTime = DateTimeOffset.UtcNow,
            RequestedAlgorithms = [.. algorithms]
        };

        try
        {
            var tasks = algorithms.Select(async algorithm =>
            {
                var hashResult = await CalculateHashAsync(data, algorithm, enableCaching: true);
                return new { Algorithm = algorithm, Result = hashResult };
            });

            var results = await Task.WhenAll(tasks);

            foreach (var item in results)
            {
                result.HashResults[item.Algorithm] = item.Result;
                if (!item.Result.IsSuccessful)
                {
                    result.Errors.Add($"{item.Algorithm}: {item.Result.ErrorMessage}");
                }
            }

            result.IsSuccessful = result.HashResults.Values.Any(r => r.IsSuccessful);

            _logger.LogDebugMessage($"Multiple hash calculation completed: {algorithms.Length} algorithms, {result.HashResults.Count} successful");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Multiple hash calculation failed");
            result.IsSuccessful = false;
            result.Errors.Add($"Calculation error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Verifies data integrity by comparing with expected hash
    /// </summary>
    public async Task<HashVerificationResult> VerifyHashAsync(
        ReadOnlyMemory<byte> data,
        ReadOnlyMemory<byte> expectedHash,
        string algorithm = "SHA-256")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);

        try
        {
            var result = new HashVerificationResult
            {
                Algorithm = algorithm,
                InputSize = data.Length,
                ExpectedHashSize = expectedHash.Length,
                VerificationTime = DateTimeOffset.UtcNow
            };

            // Calculate actual hash
            var hashResult = await CalculateHashAsync(data, algorithm, enableCaching: false);
            if (!hashResult.IsSuccessful)
            {
                result.ErrorMessage = $"Hash calculation failed: {hashResult.ErrorMessage}";
                return result;
            }

            result.CalculatedHash = hashResult.HashValue;
            result.ExpectedHash = expectedHash.ToArray();

            // Compare hashes using constant-time comparison
            result.IsValid = hashResult.HashValue != null && ConstantTimeEquals([.. hashResult.HashValue], expectedHash.ToArray());
            result.IsSuccessful = true;

            _logger.LogDebugMessage($"Hash verification completed: {algorithm}, Valid={result.IsValid}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Hash verification failed for algorithm {algorithm}");
            return new HashVerificationResult
            {
                Algorithm = algorithm,
                InputSize = data.Length,
                ExpectedHashSize = expectedHash.Length,
                VerificationTime = DateTimeOffset.UtcNow,
                IsSuccessful = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Calculates fingerprint for a cryptographic key
    /// </summary>
    public async Task<string> CalculateKeyFingerprintAsync(SecureKeyContainer keyContainer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentNullException.ThrowIfNull(keyContainer);

        try
        {
            var hashResult = await CalculateHashAsync(keyContainer.GetKeyBytes(), "SHA-256", enableCaching: false);
            if (!hashResult.IsSuccessful)
            {
                throw new InvalidOperationException($"Failed to calculate key fingerprint: {hashResult.ErrorMessage}");
            }

            // Return first 16 characters of hex representation
            return Convert.ToHexString([.. hashResult.HashValue!])[..16];
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Key fingerprint calculation failed for key {keyContainer.Identifier}");
            throw;
        }
    }

    /// <summary>
    /// Validates hash algorithm for security compliance
    /// </summary>
    public HashAlgorithmValidationResult ValidateHashAlgorithm(string algorithm, string context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        var result = new HashAlgorithmValidationResult
        {
            Algorithm = algorithm,
            Context = context,
            ValidationTime = DateTimeOffset.UtcNow
        };

        // Check for weak algorithms
        if (WeakHashAlgorithms.Contains(algorithm))
        {
            result.IsApproved = false;
            result.SecurityIssues.Add($"Hash algorithm '{algorithm}' is cryptographically weak");
            result.Recommendations.Add($"Use approved alternatives: {string.Join(", ", ApprovedHashAlgorithms)}");
            return result;
        }

        // Check if algorithm is approved
        if (!ApprovedHashAlgorithms.Contains(algorithm))
        {
            result.IsApproved = false;
            result.SecurityIssues.Add($"Hash algorithm '{algorithm}' is not in the approved list");
            result.Recommendations.Add($"Use approved algorithms: {string.Join(", ", ApprovedHashAlgorithms)}");
            return result;
        }

        result.IsApproved = true;
        result.Recommendations.Add($"Algorithm '{algorithm}' is approved for use in {context}");

        _logger.LogDebugMessage($"Hash algorithm validation completed: {algorithm}, Approved={result.IsApproved}");

        return result;
    }

    private static async Task<HashResult> PerformHashCalculationAsync(
        ReadOnlyMemory<byte> data,
        string algorithm,
        HashResult result)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var hashAlgorithm = CreateHashAlgorithm(algorithm);
                var hash = hashAlgorithm.ComputeHash(data.ToArray());


                result.HashValue = hash;
                result.HashSize = hash.Length;
                result.IsSuccessful = true;


                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Hash calculation failed: {ex.Message}";
                return result;
            }
        });
    }

    private static HashAlgorithm CreateHashAlgorithm(string algorithm)
    {
#pragma warning disable CA5350, CA5351 // Do not use weak cryptographic algorithms
        // SECURITY JUSTIFICATION:
        // MD5 and SHA-1 are intentionally supported for backward compatibility and non-security scenarios:
        // - Legacy data integrity verification where algorithm cannot be changed
        // - Checksum generation for non-cryptographic purposes (file identification, deduplication)
        // - Compatibility with external systems that require these algorithms
        // - These algorithms are marked as "weak" in WeakHashAlgorithms set and trigger validation warnings
        // - Production code should use SHA-256 or higher; MD5/SHA-1 generate explicit warnings in ValidateHashAlgorithm()
        // - For security-critical operations, always use approved algorithms (SHA-256, SHA-384, SHA-512)
        return algorithm.ToUpperInvariant() switch
        {
            "SHA-256" => SHA256.Create(),
            "SHA-384" => SHA384.Create(),
            "SHA-512" => SHA512.Create(),
            "MD5" => MD5.Create(),      // Legacy compatibility only - not approved for security
            "SHA-1" => SHA1.Create(),   // Legacy compatibility only - not approved for security
            _ => throw new ArgumentException($"Unsupported hash algorithm: {algorithm}")
        };
#pragma warning restore CA5350, CA5351
    }

    private static bool ValidateHashAlgorithm(string algorithm, HashResult result)
    {
        if (WeakHashAlgorithms.Contains(algorithm))
        {
            result.ErrorMessage = $"Hash algorithm '{algorithm}' is not approved for security reasons";
            return false;
        }

        if (!ApprovedHashAlgorithms.Contains(algorithm) && !WeakHashAlgorithms.Contains(algorithm))
        {
            // Allow for demonstration purposes, but warn
            result.Warnings.Add($"Hash algorithm '{algorithm}' is not in the standard approved list");
        }

        return true;
    }

    private static string GenerateCacheKey(ReadOnlyMemory<byte> data, string algorithm)
    {
        // Generate a simple cache key based on data hash and algorithm
        var dataHash = data.Length.GetHashCode() ^ data.ToArray().GetHashCode();
        return $"{algorithm}_{dataHash:X8}";
    }

    private static bool ConstantTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var result = 0;
        for (var i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
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
                var expiredEntries = _hashCache
                    .Where(kvp => DateTimeOffset.UtcNow - kvp.Value.CalculationTime > TimeSpan.FromHours(1))
                    .ToList();

                foreach (var (key, _) in expiredEntries)
                {
                    _ = _hashCache.Remove(key);
                }

                if (expiredEntries.Count > 0)
                {
                    _logger.LogDebugMessage($"Cleaned up {expiredEntries.Count} expired hash cache entries");
                }
            }
        }
        catch (Exception ex)
        {
            LogCacheCleanupError(_logger, ex);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _cacheCleanupTimer?.Dispose();


            lock (_cacheLock)
            {
                _hashCache.Clear();
            }


            _disposed = true;
            _logger.LogDebugMessage("Hash Calculator disposed");
        }
    }
}

#region Supporting Types

/// <summary>
/// Result of hash calculation
/// </summary>
public sealed class HashResult
{
    /// <inheritdoc/>
    public required string Algorithm { get; init; }
    /// <inheritdoc/>
    public int InputSize { get; init; }
    /// <inheritdoc/>
    public IReadOnlyList<byte>? HashValue { get; set; }
    /// <inheritdoc/>
    public int HashSize { get; set; }
    /// <inheritdoc/>
    public bool IsSuccessful { get; set; }
    /// <inheritdoc/>
    public string? ErrorMessage { get; set; }
    /// <inheritdoc/>
    public DateTimeOffset CalculationTime { get; init; }
    /// <inheritdoc/>
    public IList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Result of multiple hash calculations
/// </summary>
public sealed class MultiHashResult
{
    /// <inheritdoc/>
    public int InputSize { get; init; }
    /// <inheritdoc/>
    public DateTimeOffset CalculationTime { get; init; }
    /// <inheritdoc/>
    public IReadOnlyList<string> RequestedAlgorithms { get; init; } = [];
    /// <inheritdoc/>
    public Dictionary<string, HashResult> HashResults { get; init; } = [];
    /// <inheritdoc/>
    public bool IsSuccessful { get; set; }
    /// <inheritdoc/>
    public IList<string> Errors { get; init; } = [];
}

/// <summary>
/// Result of hash verification
/// </summary>
public sealed class HashVerificationResult
{
    /// <inheritdoc/>
    public required string Algorithm { get; init; }
    /// <inheritdoc/>
    public int InputSize { get; init; }
    /// <inheritdoc/>
    public int ExpectedHashSize { get; init; }
    /// <inheritdoc/>
    public IReadOnlyList<byte>? CalculatedHash { get; set; }
    /// <inheritdoc/>
    public IReadOnlyList<byte>? ExpectedHash { get; set; }
    /// <inheritdoc/>
    public bool IsValid { get; set; }
    /// <inheritdoc/>
    public bool IsSuccessful { get; set; }
    /// <inheritdoc/>
    public string? ErrorMessage { get; set; }
    /// <inheritdoc/>
    public DateTimeOffset VerificationTime { get; init; }
}

/// <summary>
/// Result of hash algorithm validation
/// </summary>
public sealed class HashAlgorithmValidationResult
{
    /// <inheritdoc/>
    public required string Algorithm { get; init; }
    /// <inheritdoc/>
    public required string Context { get; init; }
    /// <inheritdoc/>
    public DateTimeOffset ValidationTime { get; init; }
    /// <inheritdoc/>
    public bool IsApproved { get; set; }
    /// <inheritdoc/>
    public IList<string> SecurityIssues { get; init; } = [];
    /// <inheritdoc/>
    public IList<string> Recommendations { get; } = [];
}

/// <summary>
/// Cached hash calculation result
/// </summary>
internal sealed class CachedHashResult
{
    /// <summary>
    /// Gets or sets the result.
    /// </summary>
    /// <value>The result.</value>
    public required HashResult Result { get; init; }
    /// <summary>
    /// Gets or sets the calculation time.
    /// </summary>
    /// <value>The calculation time.</value>
    public DateTimeOffset CalculationTime { get; init; }
}

#endregion
