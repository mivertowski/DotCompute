// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Types.Security;


/// <summary>
/// Security policy for algorithm plugin loading and execution.
/// </summary>
public class SecurityPolicy
{
private readonly ILogger<SecurityPolicy>? _logger;
private readonly Dictionary<string, ISecurityRule> _securityRules = new();
private readonly HashSet<string> _trustedPublishers = new();

/// <summary>
/// Initializes a new instance of the <see cref="SecurityPolicy"/> class.
/// </summary>
/// <param name="logger">Optional logger for diagnostics.</param>
public SecurityPolicy(ILogger<SecurityPolicy>? logger = null)
{
    _logger = logger;
}

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
public Dictionary<string, SecurityLevel> DirectoryPolicies { get; } = new();

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
    _trustedPublishers.Add(thumbprint.ToUpperInvariant());
    _logger?.LogDebug("Added trusted publisher: {Thumbprint}", thumbprint);
}

/// <summary>
/// Removes a trusted publisher certificate thumbprint.
/// </summary>
/// <param name="thumbprint">The certificate thumbprint.</param>
/// <returns>True if the publisher was removed, false if not found.</returns>
public bool RemoveTrustedPublisher(string thumbprint)
{
    var removed = _trustedPublishers.Remove(thumbprint.ToUpperInvariant());
    if (removed)
    {
        _logger?.LogDebug("Removed trusted publisher: {Thumbprint}", thumbprint);
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
    _logger?.LogDebug("Added security rule: {RuleName}", name);
}

/// <summary>
/// Removes a security rule.
/// </summary>
/// <param name="name">The rule name.</param>
/// <returns>True if the rule was removed, false if not found.</returns>
public bool RemoveSecurityRule(string name)
{
    var removed = _securityRules.Remove(name);
    if (removed)
    {
        _logger?.LogDebug("Removed security rule: {RuleName}", name);
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
        Violations = new List<string>()
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
    if (context.AssemblyBytes != null && context.AssemblyBytes.Length > MaxAssemblySize)
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
                result.Violations.AddRange(ruleResult.Violations.Select(v => $"{name}: {v}"));
            }
            
            // Take the most restrictive security level
            if (ruleResult.SecurityLevel < result.SecurityLevel)
            {
                result.SecurityLevel = ruleResult.SecurityLevel;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error evaluating security rule {RuleName}", name);
            result.IsAllowed = false;
            result.Violations.Add($"Rule evaluation error: {name}");
        }
    }

    _logger?.LogDebug("Security evaluation result: Allowed={IsAllowed}, Level={SecurityLevel}, Violations={ViolationCount}",
        result.IsAllowed, result.SecurityLevel, result.Violations.Count);

    return result;
}

/// <summary>
/// Saves the security policy to a JSON file.
/// </summary>
/// <param name="filePath">The file path to save to.</param>
/// <returns>A task representing the async operation.</returns>
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

    var json = JsonSerializer.Serialize(policyData, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(filePath, json);
    
    _logger?.LogInformation("Security policy saved to {FilePath}", filePath);
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

    if (root.TryGetProperty("RequireDigitalSignature", out var reqDigSig))
        RequireDigitalSignature = reqDigSig.GetBoolean();
    
    if (root.TryGetProperty("RequireStrongName", out var reqStrongName))
        RequireStrongName = reqStrongName.GetBoolean();
    
    if (root.TryGetProperty("MinimumSecurityLevel", out var minLevel))
        MinimumSecurityLevel = Enum.Parse<SecurityLevel>(minLevel.GetString()!);
    
    if (root.TryGetProperty("EnableMetadataAnalysis", out var enableMeta))
        EnableMetadataAnalysis = enableMeta.GetBoolean();
    
    if (root.TryGetProperty("EnableMalwareScanning", out var enableMalware))
        EnableMalwareScanning = enableMalware.GetBoolean();
    
    if (root.TryGetProperty("MaxAssemblySize", out var maxSize))
        MaxAssemblySize = maxSize.GetInt64();

    if (root.TryGetProperty("DirectoryPolicies", out var dirPolicies))
    {
        DirectoryPolicies.Clear();
        foreach (var prop in dirPolicies.EnumerateObject())
        {
            DirectoryPolicies[prop.Name] = Enum.Parse<SecurityLevel>(prop.Value.GetString()!);
        }
    }

    if (root.TryGetProperty("TrustedPublishers", out var publishers))
    {
        _trustedPublishers.Clear();
        foreach (var publisher in publishers.EnumerateArray())
        {
            _trustedPublishers.Add(publisher.GetString()!.ToUpperInvariant());
        }
    }

    _logger?.LogInformation("Security policy loaded from {FilePath}", filePath);
}
}

/// <summary>
/// Security evaluation context for policy decisions.
/// </summary>
public class SecurityEvaluationContext
{
/// <summary>
/// Gets or sets the assembly file path.
/// </summary>
public string? AssemblyPath { get; set; }

/// <summary>
/// Gets or sets the assembly bytes.
/// </summary>
public byte[]? AssemblyBytes { get; set; }

/// <summary>
/// Gets or sets additional context metadata.
/// </summary>
public Dictionary<string, object> Metadata { get; set; } = new();
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
/// Gets or sets the list of security violations.
/// </summary>
public List<string> Violations { get; set; } = new();
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
SecurityEvaluationResult Evaluate(SecurityEvaluationContext context);
}

/// <summary>
/// Security rule that checks file size limits.
/// </summary>
public class FileSizeSecurityRule : ISecurityRule
{
private readonly long _maxSizeBytes;

/// <summary>
/// Initializes a new instance of the <see cref="FileSizeSecurityRule"/> class.
/// </summary>
/// <param name="maxSizeBytes">Maximum allowed file size in bytes.</param>
public FileSizeSecurityRule(long maxSizeBytes)
{
    _maxSizeBytes = maxSizeBytes;
}

/// <inheritdoc/>
public SecurityEvaluationResult Evaluate(SecurityEvaluationContext context)
{
    var result = new SecurityEvaluationResult
    {
        IsAllowed = true,
        SecurityLevel = SecurityLevel.Medium
    };

    if (context.AssemblyBytes != null && context.AssemblyBytes.Length > _maxSizeBytes)
    {
        result.IsAllowed = false;
        result.Violations.Add($"File size {context.AssemblyBytes.Length} exceeds limit {_maxSizeBytes}");
        result.SecurityLevel = SecurityLevel.Low;
    }

    return result;
}
}
