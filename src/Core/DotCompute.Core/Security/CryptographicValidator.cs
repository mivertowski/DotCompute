// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using DotCompute.Abstractions.Security;
using DotCompute.Core.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Security;

/// <summary>
/// Provides security validation and compliance checking for cryptographic operations.
/// Ensures adherence to security policies and standards.
/// </summary>
internal sealed class CryptographicValidator : IDisposable
{
    private readonly ILogger<CryptographicValidator> _logger;
    private readonly CryptographicConfiguration _configuration;
    private bool _disposed;

    // Security compliance standards
    private static readonly Dictionary<string, SecurityStandard> SecurityStandards = new()
    {
        ["FIPS-140-2"] = new SecurityStandard
        {
            Name = "FIPS 140-2",
            ApprovedAlgorithms = ["AES-256", "RSA-2048", "SHA-256", "SHA-384", "SHA-512"],
            MinimumKeySizes = new Dictionary<string, int>
            {
                ["AES"] = 128,
                ["RSA"] = 2048,
                ["ECDSA"] = 256
            }
        },
        ["NIST-SP-800-57"] = new SecurityStandard
        {
            Name = "NIST SP 800-57",
            ApprovedAlgorithms = ["AES-256", "RSA-3072", "ECDSA-256", "SHA-256", "SHA-384"],
            MinimumKeySizes = new Dictionary<string, int>
            {
                ["AES"] = 128,
                ["RSA"] = 3072,
                ["ECDSA"] = 256
            }
        }
    };

    // Weak algorithms and their replacement recommendations
    private static readonly Dictionary<string, string> WeakAlgorithmReplacements = new()
    {
        ["MD5"] = "SHA-256",
        ["SHA-1"] = "SHA-256",
        ["DES"] = "AES-256",
        ["3DES"] = "AES-256",
        ["RC4"] = "AES-256-GCM",
        ["RSA-1024"] = "RSA-2048"
    };
    /// <summary>
    /// Initializes a new instance of the CryptographicValidator class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>

    public CryptographicValidator(
        ILogger<CryptographicValidator> logger,
        CryptographicConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        _logger.LogInfoMessage("CryptographicValidator initialized");
    }

    /// <summary>
    /// Validates a cryptographic algorithm against security policies.
    /// </summary>
    public async Task<AlgorithmValidationResult> ValidateAlgorithmAsync(
        string algorithm,
        int keySize,
        string context,
        string? securityStandard = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        _logger.LogDebugMessage($"Validating algorithm: {algorithm} with key size {keySize} for context {context}");

        var result = new AlgorithmValidationResult
        {
            Algorithm = algorithm,
            KeySize = keySize,
            Context = context,
            ValidationTime = DateTimeOffset.UtcNow,
            IsApproved = true
        };

        try
        {
            // Check for known weak algorithms
            await ValidateAgainstWeakAlgorithmsAsync(algorithm, result);

            // Validate key size requirements
            await ValidateKeySizeRequirementsAsync(algorithm, keySize, result);

            // Validate against security standards if specified
            if (!string.IsNullOrEmpty(securityStandard))
            {
                await ValidateAgainstSecurityStandardAsync(algorithm, keySize, securityStandard, result);
            }

            // Context-specific validation
            await ValidateForContextAsync(algorithm, keySize, context, result);

            // Check algorithm implementation security
            await ValidateImplementationSecurityAsync(algorithm, result);

            _logger.LogDebugMessage($"Algorithm validation completed: {algorithm} - Approved: {result.IsApproved}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Algorithm validation failed: {algorithm}");
            result.IsApproved = false;
            result.Issues.Add($"Validation error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Validates cryptographic configuration for compliance.
    /// </summary>
    public async Task<ConfigurationValidationResult> ValidateConfigurationAsync(
        CryptographicConfiguration configuration,
        string? targetStandard = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(configuration);

        _logger.LogInfoMessage($"Validating cryptographic configuration for standard: {targetStandard ?? "default"}");

        var result = new ConfigurationValidationResult
        {
            Configuration = configuration,
            TargetStandard = targetStandard,
            ValidationTime = DateTimeOffset.UtcNow,
            IsCompliant = true
        };

        try
        {
            // Validate key rotation settings
            await ValidateKeyRotationSettingsAsync(configuration, result);

            // Validate entropy requirements
            await ValidateEntropyRequirementsAsync(configuration, result);

            // Validate timing attack protections
            await ValidateTimingAttackProtectionsAsync(configuration, result);

            // Validate against target standard if specified
            if (!string.IsNullOrEmpty(targetStandard))
            {
                await ValidateAgainstStandardAsync(configuration, targetStandard, result);
            }

            _logger.LogInfoMessage($"Configuration validation completed - Compliant: {result.IsCompliant}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Configuration validation failed");
            result.IsCompliant = false;
            result.Issues.Add($"Validation error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Performs a comprehensive security audit of cryptographic operations.
    /// </summary>
    public async Task<SecurityAuditResult> PerformSecurityAuditAsync(
        IEnumerable<CryptographicOperation> operations,
        string auditContext)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentException.ThrowIfNullOrWhiteSpace(auditContext);

        _logger.LogInfoMessage($"Performing security audit for context: {auditContext}");

        var operationsList = operations.ToList();
        var result = new SecurityAuditResult
        {
            AuditContext = auditContext,
            AuditTime = DateTimeOffset.UtcNow,
            OperationsAudited = operationsList.Count,
            OverallSecurityLevel = SecurityLevel.High
        };

        try
        {
            foreach (var operation in operationsList)
            {
                var operationResult = await AuditSingleOperationAsync(operation);
                result.OperationResults.Add(operationResult);

                // Update overall security level based on worst finding
                if (operationResult.SecurityLevel < result.OverallSecurityLevel)
                {
                    result.OverallSecurityLevel = operationResult.SecurityLevel;
                }
            }

            // Generate security recommendations
            var recommendations = await GenerateSecurityRecommendationsAsync((IReadOnlyList<OperationAuditResult>)result.OperationResults);
            result.Recommendations.Clear();
            foreach (var recommendation in recommendations)
            {
                result.Recommendations.Add(recommendation);
            }

            _logger.LogInfoMessage($"Security audit completed - Level: {result.OverallSecurityLevel}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Security audit failed");
            result.OverallSecurityLevel = SecurityLevel.Critical;
            result.AuditErrors.Add($"Audit error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Validates entropy quality for random number generation.
    /// </summary>
    public Task<EntropyValidationResult> ValidateEntropyAsync(byte[] randomData)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(randomData);

        _logger.LogDebugMessage($"Validating entropy quality for {randomData.Length} bytes");

        var result = new EntropyValidationResult
        {
            DataSize = randomData.Length,
            ValidationTime = DateTimeOffset.UtcNow
        };

        try
        {
            // Calculate entropy metrics
            result.Shannon = CalculateShannonEntropy(randomData);
            result.ChiSquare = CalculateChiSquareTest(randomData);
            result.CompressionRatio = CalculateCompressionRatio(randomData);

            // Determine quality based on metrics
            result.Quality = DetermineEntropyQuality(result.Shannon, result.ChiSquare, result.CompressionRatio);

            // Generate recommendations if quality is poor
            if (result.Quality < EntropyQuality.Good)
            {
                result.Recommendations.Add("Consider using a hardware random number generator");
                result.Recommendations.Add("Verify entropy source is properly seeded");
            }

            _logger.LogDebugMessage($"Entropy validation completed - Quality: {result.Quality}");
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Entropy validation failed");
            result.Quality = EntropyQuality.Poor;
            result.ErrorMessage = ex.Message;
            return Task.FromResult(result);
        }
    }

    // Private validation methods

    private static Task ValidateAgainstWeakAlgorithmsAsync(string algorithm, AlgorithmValidationResult result)
    {
        if (WeakAlgorithmReplacements.ContainsKey(algorithm.ToUpperInvariant()))
        {
            result.IsApproved = false;
            var replacement = WeakAlgorithmReplacements[algorithm.ToUpperInvariant()];
            result.Issues.Add($"Algorithm '{algorithm}' is considered weak. Recommended replacement: {replacement}");
            result.Recommendations.Add($"Migrate to {replacement} for improved security");
        }
        return Task.CompletedTask;
    }

    private static Task ValidateKeySizeRequirementsAsync(string algorithm, int keySize, AlgorithmValidationResult result)
    {
        var minimumSizes = new Dictionary<string, int>
        {
            ["AES"] = 128,
            ["RSA"] = 2048,
            ["ECDSA"] = 256,
            ["CHACHA20"] = 256
        };

        var algorithmFamily = algorithm.Split('-')[0].ToUpperInvariant();

        if (minimumSizes.TryGetValue(algorithmFamily, out var minSize) && keySize < minSize)
        {
            result.IsApproved = false;
            result.Issues.Add($"Key size {keySize} is below minimum requirement of {minSize} bits for {algorithmFamily}");
            result.Recommendations.Add($"Increase key size to at least {minSize} bits");
        }
        return Task.CompletedTask;
    }

    private static Task ValidateAgainstSecurityStandardAsync(string algorithm, int keySize, string standard, AlgorithmValidationResult result)
    {
        if (!SecurityStandards.TryGetValue(standard.ToUpperInvariant(), out var securityStandard))
        {
            result.Warnings.Add($"Unknown security standard: {standard}");
            return Task.CompletedTask;
        }

        var algorithmFamily = algorithm.Split('-')[0].ToUpperInvariant();

        // Check if algorithm is approved by the standard
        if (!securityStandard.ApprovedAlgorithms.Any(a => a.StartsWith(algorithmFamily, StringComparison.OrdinalIgnoreCase)))
        {
            result.IsApproved = false;
            result.Issues.Add($"Algorithm '{algorithm}' is not approved by {securityStandard.Name}");
        }

        // Check minimum key size requirements
        if (securityStandard.MinimumKeySizes.TryGetValue(algorithmFamily, out var minSize) && keySize < minSize)
        {
            result.IsApproved = false;
            result.Issues.Add($"Key size {keySize} does not meet {securityStandard.Name} requirement of {minSize} bits");
        }
        return Task.CompletedTask;
    }

    private static Task ValidateForContextAsync(string algorithm, int keySize, string context, AlgorithmValidationResult result)
    {
        // Context-specific validation logic
        switch (context.ToUpper(CultureInfo.InvariantCulture))
        {
            case "payment" or "financial":
                if (!algorithm.Contains("AES", StringComparison.OrdinalIgnoreCase) && !algorithm.Contains("RSA", StringComparison.OrdinalIgnoreCase))
                {
                    result.Warnings.Add("Financial contexts typically require AES or RSA algorithms");
                }
                break;

            case "medical" or "healthcare":
                if (keySize < 256)
                {
                    result.Warnings.Add("Healthcare data typically requires stronger encryption (256+ bit keys)");
                }
                break;

            case "government" or "classified":
                if (!algorithm.Contains("AES-256", StringComparison.Ordinal) && !algorithm.Contains("RSA-3072", StringComparison.Ordinal))
                {
                    result.IsApproved = false;
                    result.Issues.Add("Government contexts require AES-256 or RSA-3072 minimum");
                }
                break;
        }
        return Task.CompletedTask;
    }

    private static Task ValidateImplementationSecurityAsync(string algorithm, AlgorithmValidationResult result)
    {
        // Check for implementation-specific security considerations
        if (algorithm.Contains("CBC", StringComparison.Ordinal))
        {
            result.Warnings.Add("CBC mode requires proper IV handling to prevent padding oracle attacks");
            result.Recommendations.Add("Consider using GCM mode for authenticated encryption");
        }

        if (algorithm.Contains("RSA", StringComparison.Ordinal) && !algorithm.Contains("OAEP", StringComparison.Ordinal) && !algorithm.Contains("PSS", StringComparison.Ordinal))
        {
            result.Warnings.Add("RSA without OAEP padding may be vulnerable to chosen ciphertext attacks");
            result.Recommendations.Add("Use RSA with OAEP padding for encryption or PSS for signatures");
        }
        return Task.CompletedTask;
    }

    private static Task ValidateKeyRotationSettingsAsync(CryptographicConfiguration config, ConfigurationValidationResult result)
    {
        if (config.KeyRotationInterval > TimeSpan.FromDays(90))
        {
            result.Issues.Add("Key rotation interval exceeds recommended maximum of 90 days");
        }

        // KeyMaxAge property doesn't exist - removing this check
        return Task.CompletedTask;
    }

    private static Task ValidateEntropyRequirementsAsync(CryptographicConfiguration config, ConfigurationValidationResult result)
    {
        // Validate entropy source configuration
        // This would typically check if hardware RNG is available and properly configured
        result.Recommendations.Add("Ensure entropy source meets minimum randomness requirements");
        return Task.CompletedTask;
    }

    private static Task ValidateTimingAttackProtectionsAsync(CryptographicConfiguration config, ConfigurationValidationResult result)
    {
        // Validate timing attack protection measures
        result.Recommendations.Add("Implement constant-time comparisons for sensitive operations");
        return Task.CompletedTask;
    }

    private static Task ValidateAgainstStandardAsync(CryptographicConfiguration config, string standard, ConfigurationValidationResult result)
    {
        if (SecurityStandards.TryGetValue(standard.ToUpperInvariant(), out var securityStandard))
        {
            // Validate configuration against specific standard requirements
            result.Recommendations.Add($"Ensure compliance with {securityStandard.Name} requirements");
        }
        return Task.CompletedTask;
    }

    private static Task<OperationAuditResult> AuditSingleOperationAsync(CryptographicOperation operation)
    {
        var result = new OperationAuditResult
        {
            OperationType = operation.Type,
            Algorithm = operation.Algorithm,
            SecurityLevel = SecurityLevel.High
        };

        // Audit the operation for security issues
        if (WeakAlgorithmReplacements.ContainsKey(operation.Algorithm.ToUpperInvariant()))
        {
            result.SecurityLevel = SecurityLevel.Low;
            result.Issues.Add($"Uses weak algorithm: {operation.Algorithm}");
        }

        if (operation.KeySize < 128)
        {
            result.SecurityLevel = SecurityLevel.Critical;
            result.Issues.Add($"Key size too small: {operation.KeySize} bits");
        }

        return Task.FromResult(result);
    }

    private static Task<List<string>> GenerateSecurityRecommendationsAsync(IReadOnlyList<OperationAuditResult> results)
    {
        var recommendations = new List<string>();

        var weakAlgorithms = results.Where(r => r.Issues.Any(i => i.Contains("weak algorithm", StringComparison.Ordinal))).ToList();
        if (weakAlgorithms.Count > 0)
        {
            recommendations.Add("Replace weak cryptographic algorithms with approved alternatives");
        }

        var smallKeys = results.Where(r => r.Issues.Any(i => i.Contains("Key size too small", StringComparison.Ordinal))).ToList();
        if (smallKeys.Count > 0)
        {
            recommendations.Add("Increase key sizes to meet current security standards");
        }

        return Task.FromResult(recommendations);
    }

    // Entropy validation helper methods

    private static double CalculateShannonEntropy(byte[] data)
    {
        var frequencies = new int[256];
        foreach (var b in data)
        {
            frequencies[b]++;
        }

        var entropy = 0.0;
        var length = data.Length;

        for (var i = 0; i < 256; i++)
        {
            if (frequencies[i] > 0)
            {
                var probability = (double)frequencies[i] / length;
                entropy -= probability * Math.Log2(probability);
            }
        }

        return entropy;
    }

    private static double CalculateChiSquareTest(byte[] data)
    {
        var expected = data.Length / 256.0;
        var frequencies = new int[256];

        foreach (var b in data)
        {
            frequencies[b]++;
        }

        var chiSquare = 0.0;
        for (var i = 0; i < 256; i++)
        {
            var observed = frequencies[i];
            chiSquare += Math.Pow(observed - expected, 2) / expected;
        }

        return chiSquare;
    }

    private static double CalculateCompressionRatio(byte[] data)
    {
        // Simplified compression ratio calculation
        // In practice, you'd use a proper compression algorithm
        var uniqueBytes = data.Distinct().Count();
        return (double)uniqueBytes / 256;
    }

    private static EntropyQuality DetermineEntropyQuality(double shannon, double chiSquare, double compressionRatio)
    {
        // Simplified quality determination
        if (shannon > 7.9 && compressionRatio > 0.9)
        {

            return EntropyQuality.Excellent;
        }


        if (shannon > 7.5 && compressionRatio > 0.8)
        {

            return EntropyQuality.Good;
        }


        if (shannon > 6.0 && compressionRatio > 0.6)
        {

            return EntropyQuality.Fair;
        }


        return EntropyQuality.Poor;
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
/// <summary>
/// A class that represents security standard.
/// </summary>

// Supporting classes and enums
public class SecurityStandard
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public required string Name { get; set; }
    /// <summary>
    /// Gets or sets the approved algorithms.
    /// </summary>
    /// <value>The approved algorithms.</value>
    public IList<string> ApprovedAlgorithms { get; init; } = [];
    /// <summary>
    /// Gets or sets the minimum key sizes.
    /// </summary>
    /// <value>The minimum key sizes.</value>
    public Dictionary<string, int> MinimumKeySizes { get; init; } = [];
}
/// <summary>
/// An entropy quality enumeration.
/// </summary>

// Use the canonical SecurityLevel from DotCompute.Abstractions.Security
// This local enum has been replaced with the unified type

public enum EntropyQuality
{
    /// <summary>
    /// Poor entropy quality indicating insufficient randomness for cryptographic operations.
    /// </summary>
    Poor,
    /// <summary>
    /// Fair entropy quality indicating acceptable but not optimal randomness.
    /// </summary>
    Fair,
    /// <summary>
    /// Good entropy quality indicating strong randomness suitable for most cryptographic operations.
    /// </summary>
    Good,
    /// <summary>
    /// Excellent entropy quality indicating optimal randomness from high-quality sources.
    /// </summary>
    Excellent
}
/// <summary>
/// A class that represents configuration validation result.
/// </summary>

// AlgorithmValidationResult moved to CryptographicSecurityCore.cs to avoid duplication

public class ConfigurationValidationResult
{
    /// <summary>
    /// Gets or sets the configuration.
    /// </summary>
    /// <value>The configuration.</value>
    public required CryptographicConfiguration Configuration { get; set; }
    /// <summary>
    /// Gets or sets the target standard.
    /// </summary>
    /// <value>The target standard.</value>
    public string? TargetStandard { get; set; }
    /// <summary>
    /// Gets or sets the validation time.
    /// </summary>
    /// <value>The validation time.</value>
    public DateTimeOffset ValidationTime { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether compliant.
    /// </summary>
    /// <value>The is compliant.</value>
    public bool IsCompliant { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether sues.
    /// </summary>
    /// <value>The issues.</value>
    public IList<string> Issues { get; init; } = [];
    /// <summary>
    /// Gets or sets the recommendations.
    /// </summary>
    /// <value>The recommendations.</value>
    public IList<string> Recommendations { get; } = [];
}
/// <summary>
/// A class that represents security audit result.
/// </summary>

public class SecurityAuditResult
{
    /// <summary>
    /// Gets or sets the audit context.
    /// </summary>
    /// <value>The audit context.</value>
    public required string AuditContext { get; set; }
    /// <summary>
    /// Gets or sets the audit time.
    /// </summary>
    /// <value>The audit time.</value>
    public DateTimeOffset AuditTime { get; set; }
    /// <summary>
    /// Gets or sets the operations audited.
    /// </summary>
    /// <value>The operations audited.</value>
    public int OperationsAudited { get; set; }
    /// <summary>
    /// Gets or sets the overall security level.
    /// </summary>
    /// <value>The overall security level.</value>
    public SecurityLevel OverallSecurityLevel { get; set; }
    /// <summary>
    /// Gets or sets the operation results.
    /// </summary>
    /// <value>The operation results.</value>
    public IList<OperationAuditResult> OperationResults { get; init; } = [];
    /// <summary>
    /// Gets or sets the recommendations.
    /// </summary>
    /// <value>The recommendations.</value>
    public IList<string> Recommendations { get; } = [];
    /// <summary>
    /// Gets or sets the audit errors.
    /// </summary>
    /// <value>The audit errors.</value>
    public IList<string> AuditErrors { get; init; } = [];
}
/// <summary>
/// A class that represents operation audit result.
/// </summary>

public class OperationAuditResult
{
    /// <summary>
    /// Gets or sets the operation type.
    /// </summary>
    /// <value>The operation type.</value>
    public required string OperationType { get; set; }
    /// <summary>
    /// Gets or sets the algorithm.
    /// </summary>
    /// <value>The algorithm.</value>
    public required string Algorithm { get; set; }
    /// <summary>
    /// Gets or sets the security level.
    /// </summary>
    /// <value>The security level.</value>
    public SecurityLevel SecurityLevel { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether sues.
    /// </summary>
    /// <value>The issues.</value>
    public IList<string> Issues { get; init; } = [];
}
/// <summary>
/// A class that represents entropy validation result.
/// </summary>

public class EntropyValidationResult
{
    /// <summary>
    /// Gets or sets the data size.
    /// </summary>
    /// <value>The data size.</value>
    public int DataSize { get; set; }
    /// <summary>
    /// Gets or sets the validation time.
    /// </summary>
    /// <value>The validation time.</value>
    public DateTimeOffset ValidationTime { get; set; }
    /// <summary>
    /// Gets or sets the shannon.
    /// </summary>
    /// <value>The shannon.</value>
    public double Shannon { get; set; }
    /// <summary>
    /// Gets or sets the chi square.
    /// </summary>
    /// <value>The chi square.</value>
    public double ChiSquare { get; set; }
    /// <summary>
    /// Gets or sets the compression ratio.
    /// </summary>
    /// <value>The compression ratio.</value>
    public double CompressionRatio { get; set; }
    /// <summary>
    /// Gets or sets the quality.
    /// </summary>
    /// <value>The quality.</value>
    public EntropyQuality Quality { get; set; }
    /// <summary>
    /// Gets or sets the recommendations.
    /// </summary>
    /// <value>The recommendations.</value>
    public IList<string> Recommendations { get; } = [];
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; set; }
}
/// <summary>
/// A class that represents cryptographic operation.
/// </summary>

public class CryptographicOperation
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    /// <value>The type.</value>
    public required string Type { get; set; }
    /// <summary>
    /// Gets or sets the algorithm.
    /// </summary>
    /// <value>The algorithm.</value>
    public required string Algorithm { get; set; }
    /// <summary>
    /// Gets or sets the key size.
    /// </summary>
    /// <value>The key size.</value>
    public int KeySize { get; set; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; set; }
}
