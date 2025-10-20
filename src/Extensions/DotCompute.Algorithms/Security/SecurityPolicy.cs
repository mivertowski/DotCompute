// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using DotCompute.Algorithms.Logging;
using DotCompute.Abstractions.Security;
using SecurityLevel = DotCompute.Abstractions.Security.SecurityLevel;

namespace DotCompute.Algorithms.Security;


/// <summary>
/// Comprehensive security policy configuration system for plugin validation.
/// </summary>
public sealed class SecurityPolicy
{
    private readonly ILogger<SecurityPolicy> _logger;
    private readonly ConcurrentDictionary<string, SecurityRule> _rules = new();
    private readonly HashSet<string> _trustedPublishers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _blockedAssemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SecurityLevel> _directoryPolicies = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityPolicy"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public SecurityPolicy(ILogger<SecurityPolicy> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        InitializeDefaultPolicies();
    }

    /// <summary>
    /// Gets or sets whether digital signatures are required for all assemblies.
    /// </summary>
    public bool RequireDigitalSignature { get; set; } = true;

    /// <summary>
    /// Gets or sets whether strong name signatures are required.
    /// </summary>
    public bool RequireStrongName { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum security level required.
    /// </summary>
    public SecurityLevel MinimumSecurityLevel { get; set; } = SecurityLevel.Medium;

    /// <summary>
    /// Gets or sets whether assembly metadata analysis is enabled.
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
    /// Gets or sets the trusted certificate store location.
    /// </summary>
    public StoreLocation TrustedStoreLocation { get; set; } = StoreLocation.LocalMachine;

    /// <summary>
    /// Gets or sets the trusted certificate store name.
    /// </summary>
    public StoreName TrustedStoreName { get; set; } = StoreName.TrustedPublisher;

    /// <summary>
    /// Gets the collection of trusted publisher certificates.
    /// </summary>
    public HashSet<string> TrustedPublishers => _trustedPublishers;

    /// <summary>
    /// Gets the collection of blocked assembly names or hashes.
    /// </summary>
    public HashSet<string> BlockedAssemblies => _blockedAssemblies;

    /// <summary>
    /// Gets the directory security policies.
    /// </summary>
    public Dictionary<string, SecurityLevel> DirectoryPolicies => _directoryPolicies;

    /// <summary>
    /// Adds a security rule for specific conditions.
    /// </summary>
    /// <param name="ruleName">The name of the rule.</param>
    /// <param name="rule">The security rule.</param>
    public void AddSecurityRule(string ruleName, SecurityRule rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleName);
        ArgumentNullException.ThrowIfNull(rule);

        _ = _rules.AddOrUpdate(ruleName, rule, (_, _) => rule);
        _logger.LogInfoMessage("Added security rule: {ruleName}");
    }

    /// <summary>
    /// Removes a security rule.
    /// </summary>
    /// <param name="ruleName">The name of the rule to remove.</param>
    /// <returns>True if the rule was removed; otherwise, false.</returns>
    public bool RemoveSecurityRule(string ruleName)
    {
        var removed = _rules.TryRemove(ruleName, out _);
        if (removed)
        {
            _logger.LogInfoMessage("Removed security rule: {ruleName}");
        }
        return removed;
    }

    /// <summary>
    /// Evaluates all security rules against the provided context.
    /// </summary>
    /// <param name="context">The security evaluation context.</param>
    /// <returns>The security evaluation result.</returns>
    public SecurityEvaluationResult EvaluateRules(SecurityEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result = new SecurityEvaluationResult
        {
            IsAllowed = true,
            SecurityLevel = SecurityLevel.High
        };

        // Evaluate all registered rules
        foreach (var rule in _rules.Values)
        {
            var ruleResult = rule.Evaluate(context);

            if (!ruleResult.IsAllowed)
            {
                result.IsAllowed = false;
                result.Violations.AddRange(ruleResult.Violations);
            }

            // Take the lowest security level
            if (ruleResult.SecurityLevel < result.SecurityLevel)
            {
                result.SecurityLevel = ruleResult.SecurityLevel;
            }

            result.Warnings.AddRange(ruleResult.Warnings);
        }

        // Check minimum security level
        if (result.SecurityLevel < MinimumSecurityLevel)
        {
            result.IsAllowed = false;
            result.Violations.Add($"Assembly does not meet minimum security level. Required: {MinimumSecurityLevel}, Found: {result.SecurityLevel}");
        }

        return result;
    }

    /// <summary>
    /// Loads security policy from a configuration file.
    /// </summary>
    /// <param name="configPath">The path to the configuration file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LoadPolicyFromFileAsync(string configPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);

        if (!File.Exists(configPath))
        {
            _logger.LogWarningMessage("Security policy file not found: {configPath}");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(configPath, cancellationToken);
            var config = JsonSerializer.Deserialize<SecurityPolicyConfiguration>(json);

            if (config != null)
            {
                ApplyConfiguration(config);
                _logger.LogInfoMessage("Loaded security policy from: {configPath}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to load security policy from: {configPath}");
            throw;
        }
    }

    /// <summary>
    /// Saves the current security policy to a configuration file.
    /// </summary>
    /// <param name="configPath">The path to save the configuration file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SavePolicyToFileAsync(string configPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);

        try
        {
            var config = CreateConfiguration();
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(configPath, json, cancellationToken);

            _logger.LogInfoMessage("Saved security policy to: {configPath}");
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to save security policy to: {configPath}");
            throw;
        }
    }

    /// <summary>
    /// Adds a trusted publisher certificate by thumbprint.
    /// </summary>
    /// <param name="thumbprint">The certificate thumbprint.</param>
    public void AddTrustedPublisher(string thumbprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);

        if (_trustedPublishers.Add(thumbprint.ToUpperInvariant()))
        {
            _logger.LogInfoMessage("Added trusted publisher: {thumbprint}");
        }
    }

    /// <summary>
    /// Removes a trusted publisher certificate.
    /// </summary>
    /// <param name="thumbprint">The certificate thumbprint.</param>
    /// <returns>True if removed; otherwise, false.</returns>
    public bool RemoveTrustedPublisher(string thumbprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);

        var removed = _trustedPublishers.Remove(thumbprint.ToUpperInvariant());
        if (removed)
        {
            _logger.LogInfoMessage("Removed trusted publisher: {thumbprint}");
        }
        return removed;
    }

    /// <summary>
    /// Checks if a publisher certificate is trusted.
    /// </summary>
    /// <param name="thumbprint">The certificate thumbprint.</param>
    /// <returns>True if trusted; otherwise, false.</returns>
    public bool IsTrustedPublisher(string thumbprint)
    {
        if (string.IsNullOrWhiteSpace(thumbprint))
        {
            return false;
        }

        return _trustedPublishers.Contains(thumbprint.ToUpperInvariant());
    }

    private void InitializeDefaultPolicies()
    {
        // Add default security rules
        AddSecurityRule("FileSizeCheck", new FileSizeSecurityRule(MaxAssemblySize));
        AddSecurityRule("SignatureValidation", new DigitalSignatureSecurityRule());
        AddSecurityRule("StrongNameValidation", new StrongNameSecurityRule());
        AddSecurityRule("MetadataAnalysis", new MetadataAnalysisSecurityRule());
        AddSecurityRule("DirectoryPolicy", new DirectoryPolicySecurityRule(_directoryPolicies));
    }

    private void ApplyConfiguration(SecurityPolicyConfiguration config)
    {
        RequireDigitalSignature = config.RequireDigitalSignature;
        RequireStrongName = config.RequireStrongName;
        MinimumSecurityLevel = config.MinimumSecurityLevel;
        EnableMetadataAnalysis = config.EnableMetadataAnalysis;
        EnableMalwareScanning = config.EnableMalwareScanning;
        MaxAssemblySize = config.MaxAssemblySize;

        _trustedPublishers.Clear();
        foreach (var publisher in config.TrustedPublishers)
        {
            _ = _trustedPublishers.Add(publisher.ToUpperInvariant());
        }

        _blockedAssemblies.Clear();
        foreach (var blocked in config.BlockedAssemblies)
        {
            _ = _blockedAssemblies.Add(blocked.ToUpperInvariant());
        }

        _directoryPolicies.Clear();
        foreach (var policy in config.DirectoryPolicies)
        {
            _directoryPolicies[policy.Key] = policy.Value;
        }
    }

    private SecurityPolicyConfiguration CreateConfiguration()
    {
        return new SecurityPolicyConfiguration
        {
            RequireDigitalSignature = RequireDigitalSignature,
            RequireStrongName = RequireStrongName,
            MinimumSecurityLevel = MinimumSecurityLevel,
            EnableMetadataAnalysis = EnableMetadataAnalysis,
            EnableMalwareScanning = EnableMalwareScanning,
            MaxAssemblySize = MaxAssemblySize,
            TrustedPublishers = [.. _trustedPublishers],
            BlockedAssemblies = [.. _blockedAssemblies],
            DirectoryPolicies = new Dictionary<string, SecurityLevel>(_directoryPolicies)
        };
    }
}

/// <summary>
/// Configuration for security policy serialization.
/// </summary>
public sealed class SecurityPolicyConfiguration
{
    /// <summary>
    /// Gets or sets whether digital signatures are required.
    /// </summary>
    public bool RequireDigitalSignature { get; set; } = true;

    /// <summary>
    /// Gets or sets whether strong names are required.
    /// </summary>
    public bool RequireStrongName { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum security level.
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
    /// Gets or sets the maximum assembly size.
    /// </summary>
    public long MaxAssemblySize { get; set; } = 50 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the trusted publishers.
    /// </summary>
    public IList<string> TrustedPublishers { get; } = [];

    /// <summary>
    /// Gets or sets the blocked assemblies.
    /// </summary>
    public IList<string> BlockedAssemblies { get; } = [];

    /// <summary>
    /// Gets or sets the directory policies.
    /// </summary>
    public Dictionary<string, SecurityLevel> DirectoryPolicies { get; } = [];
}

/// <summary>
/// Result of security evaluation.
/// </summary>
public sealed class SecurityEvaluationResult
{
    /// <summary>
    /// Gets or sets whether the assembly is allowed to load.
    /// </summary>
    public bool IsAllowed { get; set; } = true;

    /// <summary>
    /// Gets or sets the determined security level.
    /// </summary>
    public SecurityLevel SecurityLevel { get; set; } = SecurityLevel.High;

    /// <summary>
    /// Gets the security violations found.
    /// </summary>
    public IList<string> Violations { get; init; } = [];

    /// <summary>
    /// Gets the security warnings.
    /// </summary>
    public IList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Gets additional evaluation metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}

/// <summary>
/// Base class for security rules.
/// </summary>
public abstract class SecurityRule
{
    /// <summary>
    /// Evaluates the security rule against the provided context.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The evaluation result.</returns>
    public abstract SecurityEvaluationResult Evaluate(SecurityEvaluationContext context);
}
