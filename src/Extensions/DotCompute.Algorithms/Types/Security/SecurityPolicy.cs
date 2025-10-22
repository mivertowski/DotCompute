// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Security;
using SecurityLevel = DotCompute.Abstractions.Security.SecurityLevel;

namespace DotCompute.Algorithms.Types.Security;


/// <summary>
/// Security policy for algorithm plugin loading and execution.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SecurityPolicy"/> class.
/// </remarks>
/// <param name="logger">Optional logger for diagnostics.</param>
public partial class SecurityPolicy(ILogger<SecurityPolicy>? logger = null)
{
    private readonly ILogger<SecurityPolicy>? _logger = logger;
    private readonly Dictionary<string, ISecurityRule> _securityRules = [];
    private readonly HashSet<string> _trustedPublishers = [];

    /// <summary>
    /// Cached JSON serializer options for policy serialization/deserialization.
    /// </summary>
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new() { WriteIndented = true };

    /// <summary>
    /// Gets or sets whether digital signatures are required.
    /// </summary>
    public bool RequireDigitalSignature { get; set; } = true;

    /// <summary>
    /// Gets or sets whether strong names are required.
    /// </summary>
    public bool RequireStrongName { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum security level required.
    /// </summary>
    public SecurityLevel MinimumSecurityLevel { get; set; } = SecurityLevel.Medium;

    /// <summary>
    /// Gets or sets whether metadata analysis is enabled.
    /// </summary>
    public bool EnableMetadataAnalysis { get; set; } = true;

    /// <summary>
    /// Gets or sets whether malware scanning is enabled.
    /// </summary>
    public bool EnableMalwareScanning { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum allowed assembly size in bytes.
    /// </summary>
    public long MaxAssemblySize { get; set; } = 50 * 1024 * 1024; // 50 MB

    /// <summary>
    /// Gets the directory-based security policies.
    /// </summary>
    public Dictionary<string, SecurityLevel> DirectoryPolicies { get; } = [];

    /// <summary>
    /// Gets the set of trusted publisher certificate thumbprints.
    /// </summary>
    public IReadOnlySet<string> TrustedPublishers => _trustedPublishers;

    /// <summary>
    /// Adds a trusted publisher certificate thumbprint.
    /// </summary>
    /// <param name="thumbprint">The certificate thumbprint.</param>
    public void AddTrustedPublisher(string thumbprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);
        _ = _trustedPublishers.Add(thumbprint.ToUpperInvariant());
        if (_logger is not null)
        {
            LogAddedTrustedPublisher(thumbprint);
        }
    }

    /// <summary>
    /// Removes a trusted publisher certificate thumbprint.
    /// </summary>
    /// <param name="thumbprint">The certificate thumbprint.</param>
    /// <returns>True if the publisher was removed, false if not found.</returns>
    public bool RemoveTrustedPublisher(string thumbprint)
    {
        var removed = _trustedPublishers.Remove(thumbprint.ToUpperInvariant());
        if (removed && _logger is not null)
        {
            LogRemovedTrustedPublisher(thumbprint);
        }
        return removed;
    }

    /// <summary>
    /// Adds a security rule.
    /// </summary>
    /// <param name="name">The rule name.</param>
    /// <param name="rule">The security rule implementation.</param>
    public void AddSecurityRule(string name, ISecurityRule rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rule);

        _securityRules[name] = rule;
        if (_logger is not null)
        {
            LogAddedSecurityRule(name);
        }
    }

    /// <summary>
    /// Removes a security rule.
    /// </summary>
    /// <param name="name">The rule name.</param>
    /// <returns>True if the rule was removed, false if not found.</returns>
    public bool RemoveSecurityRule(string name)
    {
        var removed = _securityRules.Remove(name);
        if (removed && _logger is not null)
        {
            LogRemovedSecurityRule(name);
        }
        return removed;
    }

    /// <summary>
    /// Evaluates all security rules against the given context.
    /// </summary>
    /// <param name="context">The security evaluation context.</param>
    /// <returns>The evaluation result.</returns>
    public SecurityEvaluationResult EvaluateRules(SecurityEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result = new SecurityEvaluationResult
        {
            IsAllowed = true,
            SecurityLevel = SecurityLevel.High,
            Violations = [],
            Warnings = []
        };

        // Check directory-based policies
        if (!string.IsNullOrEmpty(context.AssemblyPath))
        {
            foreach (var (directory, level) in DirectoryPolicies)
            {
                if (context.AssemblyPath.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
                {
                    if (level < MinimumSecurityLevel)
                    {
                        result.IsAllowed = false;
                        result.Violations.Add($"Directory security level {level} is below minimum {MinimumSecurityLevel}");
                    }
                    result.SecurityLevel = level;
                    break;
                }
            }
        }

        // Check assembly size
        if (!context.AssemblyBytes.IsDefault && context.AssemblyBytes.Length > MaxAssemblySize)
        {
            result.IsAllowed = false;
            result.Violations.Add($"Assembly size {context.AssemblyBytes.Length} exceeds maximum {MaxAssemblySize}");
        }

        // Evaluate custom security rules
        foreach (var (name, rule) in _securityRules)
        {
            try
            {
                var ruleResult = rule.Evaluate(context);
                if (!ruleResult.IsAllowed)
                {
                    result.IsAllowed = false;
                    foreach (var violation in ruleResult.Violations.Select(v => $"{name}: {v}"))
                    {
                        result.Violations.Add(violation);
                    }
                }

                // Take the most restrictive security level
                if (ruleResult.SecurityLevel < result.SecurityLevel)
                {
                    result.SecurityLevel = ruleResult.SecurityLevel;
                }
            }
            catch (Exception ex)
            {
                if (_logger is not null)
                {
                    LogErrorEvaluatingSecurityRule(ex, name);
                }


                result.IsAllowed = false;
                result.Violations.Add($"Rule evaluation error: {name}");
            }
        }

        if (_logger is not null)
        {
            LogSecurityEvaluationResult(result.IsAllowed, result.SecurityLevel, result.Violations.Count);
        }


        return result;
    }

    /// <summary>
    /// Saves the security policy to a JSON file.
    /// </summary>
    /// <param name="filePath">The file path to save to.</param>
    /// <returns>A task representing the async operation.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute",
        Justification = "JSON serialization used for configuration only, types are preserved")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCodeAttribute",
        Justification = "JSON serialization used for configuration only")]
    public async Task SavePolicyToFileAsync(string filePath)
    {
        var policyData = new
        {
            RequireDigitalSignature,
            RequireStrongName,
            MinimumSecurityLevel,
            EnableMetadataAnalysis,
            EnableMalwareScanning,
            MaxAssemblySize,
            DirectoryPolicies,
            TrustedPublishers = _trustedPublishers.ToList()
        };

        var json = JsonSerializer.Serialize(policyData, s_jsonSerializerOptions);
        await File.WriteAllTextAsync(filePath, json);

        if (_logger is not null)
        {
            LogSecurityPolicySaved(filePath);
        }
    }

    /// <summary>
    /// Loads the security policy from a JSON file.
    /// </summary>
    /// <param name="filePath">The file path to load from.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task LoadPolicyFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Security policy file not found: {filePath}");
        }

        var json = await File.ReadAllTextAsync(filePath);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty(nameof(RequireDigitalSignature), out var reqDigSig))
        {
            RequireDigitalSignature = reqDigSig.GetBoolean();
        }

        if (root.TryGetProperty(nameof(RequireStrongName), out var reqStrongName))
        {
            RequireStrongName = reqStrongName.GetBoolean();
        }

        if (root.TryGetProperty(nameof(MinimumSecurityLevel), out var minLevel))
        {
            MinimumSecurityLevel = Enum.Parse<SecurityLevel>(minLevel.GetString()!);
        }

        if (root.TryGetProperty(nameof(EnableMetadataAnalysis), out var enableMeta))
        {
            EnableMetadataAnalysis = enableMeta.GetBoolean();
        }

        if (root.TryGetProperty(nameof(EnableMalwareScanning), out var enableMalware))
        {
            EnableMalwareScanning = enableMalware.GetBoolean();
        }

        if (root.TryGetProperty(nameof(MaxAssemblySize), out var maxSize))
        {
            MaxAssemblySize = maxSize.GetInt64();
        }

        if (root.TryGetProperty(nameof(DirectoryPolicies), out var dirPolicies))
        {
            DirectoryPolicies.Clear();
            foreach (var prop in dirPolicies.EnumerateObject())
            {
                DirectoryPolicies[prop.Name] = Enum.Parse<SecurityLevel>(prop.Value.GetString()!);
            }
        }

        if (root.TryGetProperty(nameof(TrustedPublishers), out var publishers))
        {
            _trustedPublishers.Clear();
            foreach (var publisher in publishers.EnumerateArray())
            {
                _ = _trustedPublishers.Add(publisher.GetString()!.ToUpperInvariant());
            }
        }

        if (_logger is not null)
        {

            LogSecurityPolicyLoaded(filePath);
        }
    }

    #region LoggerMessage Delegates

    [LoggerMessage(Level = LogLevel.Debug, Message = "Added trusted publisher: {Thumbprint}")]
    private partial void LogAddedTrustedPublisher(string thumbprint);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removed trusted publisher: {Thumbprint}")]
    private partial void LogRemovedTrustedPublisher(string thumbprint);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Added security rule: {RuleName}")]
    private partial void LogAddedSecurityRule(string ruleName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removed security rule: {RuleName}")]
    private partial void LogRemovedSecurityRule(string ruleName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error evaluating security rule {RuleName}")]
    private partial void LogErrorEvaluatingSecurityRule(Exception ex, string ruleName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Security evaluation result: Allowed={IsAllowed}, Level={SecurityLevel}, Violations={ViolationCount}")]
    private partial void LogSecurityEvaluationResult(bool isAllowed, SecurityLevel securityLevel, int violationCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Security policy saved to {FilePath}")]
    private partial void LogSecurityPolicySaved(string filePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Security policy loaded from {FilePath}")]
    private partial void LogSecurityPolicyLoaded(string filePath);

    #endregion
}

/// <summary>
/// Result of a security evaluation.
/// </summary>
public class SecurityEvaluationResult
{
    /// <summary>
    /// Gets or sets whether the operation is allowed.
    /// </summary>
    public bool IsAllowed { get; set; }

    /// <summary>
    /// Gets or sets the determined security level.
    /// </summary>
    public SecurityLevel SecurityLevel { get; set; }

    /// <summary>
    /// Gets the list of security violations.
    /// </summary>
    public IList<string> Violations { get; init; } = [];

    /// <summary>
    /// Gets the list of security warnings.
    /// </summary>
    public IList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Interface for custom security rules.
/// </summary>
public interface ISecurityRule
{
    /// <summary>
    /// Evaluates the security rule against the given context.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The evaluation result.</returns>
    public SecurityEvaluationResult Evaluate(SecurityEvaluationContext context);
}

/// <summary>
/// Security rule that checks file size limits.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="FileSizeSecurityRule"/> class.
/// </remarks>
/// <param name="maxSizeBytes">Maximum allowed file size in bytes.</param>
public class FileSizeSecurityRule(long maxSizeBytes) : ISecurityRule
{
    private readonly long _maxSizeBytes = maxSizeBytes;

    /// <inheritdoc/>
    public SecurityEvaluationResult Evaluate(SecurityEvaluationContext context)
    {
        var result = new SecurityEvaluationResult
        {
            IsAllowed = true,
            SecurityLevel = SecurityLevel.Medium,
            Violations = [],
            Warnings = []
        };

        if (!context.AssemblyBytes.IsDefault && context.AssemblyBytes.Length > _maxSizeBytes)
        {
            result.IsAllowed = false;
            result.Violations.Add($"File size {context.AssemblyBytes.Length} exceeds limit {_maxSizeBytes}");
            result.SecurityLevel = SecurityLevel.Low;
        }

        return result;
    }
}
