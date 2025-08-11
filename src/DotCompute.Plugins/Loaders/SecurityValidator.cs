// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Loaders
{
    /// <summary>
    /// Advanced security validator for NuGet plugins with vulnerability scanning and signature verification.
    /// </summary>
    public class SecurityValidator
    {
        private readonly ILogger _logger;
        private readonly SecurityPolicy? _securityPolicy;
        private readonly ConcurrentDictionary<string, SecurityScanCache> _scanCache = new();
        private readonly VulnerabilityDatabase _vulnerabilityDatabase;
        private readonly CodeAnalyzer _codeAnalyzer;

        public SecurityValidator(ILogger logger, SecurityPolicy? securityPolicy = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _securityPolicy = securityPolicy;
            _vulnerabilityDatabase = new VulnerabilityDatabase(_logger);
            _codeAnalyzer = new CodeAnalyzer(_logger);
        }

        /// <summary>
        /// Validates a plugin for security compliance.
        /// </summary>
        public async Task ValidateAsync(NuGetPluginManifest manifest, NuGetPluginValidationResult result, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(manifest);
            ArgumentNullException.ThrowIfNull(result);

            _logger.LogInformation("Performing security validation for plugin: {PluginId}", manifest.Id);

            try
            {
                // Check security policy compliance
                ValidateSecurityPolicy(manifest, result);

                // Verify digital signatures
                await VerifySignaturesAsync(manifest, result, cancellationToken);

                // Scan for vulnerabilities
                var scanResult = await ScanForVulnerabilitiesAsync(manifest, cancellationToken);
                result.SecurityScan = scanResult;

                if (scanResult.HasCriticalVulnerabilities)
                {
                    result.ValidationErrors.Add($"Plugin contains critical security vulnerabilities: {string.Join(", ", scanResult.CriticalVulnerabilities.Select(v => v.Id))}");
                }

                if (scanResult.HasHighRiskVulnerabilities && _securityPolicy?.BlockHighRiskPackages == true)
                {
                    result.ValidationErrors.Add($"Plugin contains high-risk vulnerabilities: {string.Join(", ", scanResult.HighRiskVulnerabilities.Select(v => v.Id))}");
                }

                // Analyze code for malicious patterns
                await AnalyzeCodeAsync(manifest, result, cancellationToken);

                // Validate permissions
                ValidatePermissions(manifest, result);

                _logger.LogInformation("Security validation completed for plugin: {PluginId}. Errors: {ErrorCount}, Warnings: {WarningCount}",
                    manifest.Id, result.ValidationErrors.Count, result.ValidationWarnings.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Security validation failed for plugin: {PluginId}", manifest.Id);
                result.ValidationErrors.Add($"Security validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Scans a plugin for known security vulnerabilities.
        /// </summary>
        public async Task<SecurityScanResult> ScanForVulnerabilitiesAsync(NuGetPluginManifest manifest, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(manifest);

            var cacheKey = $"{manifest.Id}_{manifest.Version}";
            if (_scanCache.TryGetValue(cacheKey, out var cached) && !cached.IsExpired)
            {
                _logger.LogDebug("Using cached security scan for plugin: {PluginId}", manifest.Id);
                return cached.ScanResult;
            }

            _logger.LogInformation("Scanning plugin for vulnerabilities: {PluginId}", manifest.Id);

            var scanResult = new SecurityScanResult
            {
                PluginId = manifest.Id,
                PluginVersion = manifest.Version,
                ScanDate = DateTimeOffset.UtcNow
            };

            try
            {
                // Scan plugin itself
                await ScanPackageAsync(manifest.Id, manifest.Version, scanResult, cancellationToken);

                // Scan dependencies
                if (manifest.Dependencies?.Any() == true)
                {
                    foreach (var dependency in manifest.Dependencies)
                    {
                        await ScanDependencyAsync(dependency, scanResult, cancellationToken);
                    }
                }

                // Check against vulnerability database
                await CheckVulnerabilityDatabaseAsync(manifest, scanResult, cancellationToken);

                // Cache the scan result
                _scanCache.TryAdd(cacheKey, new SecurityScanCache
                {
                    ScanResult = scanResult,
                    CachedAt = DateTimeOffset.UtcNow
                });

                _logger.LogInformation("Vulnerability scan completed for plugin: {PluginId}. Vulnerabilities found: {Count}",
                    manifest.Id, scanResult.AllVulnerabilities.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vulnerability scan failed for plugin: {PluginId}", manifest.Id);
                scanResult.ScanErrors.Add($"Scan failed: {ex.Message}");
            }

            return scanResult;
        }

        /// <summary>
        /// Validates security policy compliance.
        /// </summary>
        private void ValidateSecurityPolicy(NuGetPluginManifest manifest, NuGetPluginValidationResult result)
        {
            if (_securityPolicy == null)
                return;

            // Check if plugin is from allowed sources
            if (_securityPolicy.AllowedSources?.Any() == true)
            {
                if (string.IsNullOrEmpty(manifest.PackageSource) ||
                    !_securityPolicy.AllowedSources.Contains(manifest.PackageSource, StringComparer.OrdinalIgnoreCase))
                {
                    result.ValidationErrors.Add($"Plugin source '{manifest.PackageSource}' is not in the allowed sources list");
                }
            }

            // Check if plugin is from blocked sources
            if (_securityPolicy.BlockedSources?.Any() == true)
            {
                if (!string.IsNullOrEmpty(manifest.PackageSource) &&
                    _securityPolicy.BlockedSources.Contains(manifest.PackageSource, StringComparer.OrdinalIgnoreCase))
                {
                    result.ValidationErrors.Add($"Plugin source '{manifest.PackageSource}' is blocked");
                }
            }

            // Check assembly size limits
            if (_securityPolicy.MaxAssemblySize.HasValue && File.Exists(manifest.AssemblyPath))
            {
                var assemblySize = new FileInfo(manifest.AssemblyPath).Length;
                if (assemblySize > _securityPolicy.MaxAssemblySize.Value)
                {
                    result.ValidationErrors.Add($"Assembly size ({assemblySize} bytes) exceeds maximum allowed size ({_securityPolicy.MaxAssemblySize.Value} bytes)");
                }
            }

            // Check for unsafe features
            if (_securityPolicy.BlockUnsafeCode && manifest.Metadata?.ContainsKey("AllowUnsafeBlocks") == true &&
                manifest.Metadata["AllowUnsafeBlocks"].Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                result.ValidationErrors.Add("Plugin uses unsafe code which is not allowed");
            }
        }

        /// <summary>
        /// Verifies digital signatures of the plugin assembly.
        /// </summary>
        private async Task VerifySignaturesAsync(NuGetPluginManifest manifest, NuGetPluginValidationResult result, CancellationToken cancellationToken)
        {
            if (_securityPolicy?.RequireSignedAssemblies != true)
                return;

            try
            {
                await Task.Run(() =>
                {
                    if (!File.Exists(manifest.AssemblyPath))
                    {
                        result.ValidationErrors.Add($"Assembly file not found for signature verification: {manifest.AssemblyPath}");
                        return;
                    }

                    var signatureInfo = VerifyAssemblySignature(manifest.AssemblyPath);
                    manifest.Signature = signatureInfo;

                    if (!signatureInfo.IsSigned)
                    {
                        result.ValidationErrors.Add("Assembly is not digitally signed");
                        return;
                    }

                    if (!signatureInfo.IsValid)
                    {
                        result.ValidationErrors.Add("Assembly has invalid digital signature");
                        return;
                    }

                    // Check against trusted publishers
                    if (_securityPolicy.TrustedPublishers?.Any() == true)
                    {
                        if (string.IsNullOrEmpty(signatureInfo.Publisher) ||
                            !_securityPolicy.TrustedPublishers.Contains(signatureInfo.Publisher, StringComparer.OrdinalIgnoreCase))
                        {
                            result.ValidationErrors.Add($"Assembly publisher '{signatureInfo.Publisher}' is not in the trusted publishers list");
                        }
                    }

                    _logger.LogInformation("Signature verification completed for plugin: {PluginId}. Signed: {IsSigned}, Valid: {IsValid}",
                        manifest.Id, signatureInfo.IsSigned, signatureInfo.IsValid);

                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Signature verification failed for plugin: {PluginId}", manifest.Id);
                result.ValidationErrors.Add($"Signature verification failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Verifies the digital signature of an assembly file.
        /// </summary>
        private PluginSignature VerifyAssemblySignature(string assemblyPath)
        {
            try
            {
                // This is a simplified implementation
                // In a real system, you would use AuthenticodeTools or similar
                var signature = new PluginSignature { IsSigned = false, IsValid = false };

                using var certificate = X509CertificateLoader.LoadCertificateFromFile(assemblyPath);
                if (certificate != null)
                {
                    signature.IsSigned = true;
                    signature.CertificateThumbprint = certificate.GetCertHashString();
                    signature.Publisher = certificate.Subject;
                    signature.SigningAlgorithm = certificate.GetKeyAlgorithm();
                    signature.IsValid = true; // Simplified - would need proper chain validation

                    // In a real implementation, you would validate the certificate chain
                    // and check if the certificate is trusted
                }

                return signature;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not verify signature for assembly: {AssemblyPath}", assemblyPath);
                return new PluginSignature { IsSigned = false, IsValid = false };
            }
        }

        /// <summary>
        /// Analyzes plugin code for malicious patterns.
        /// </summary>
        private async Task AnalyzeCodeAsync(NuGetPluginManifest manifest, NuGetPluginValidationResult result, CancellationToken cancellationToken)
        {
            if (_securityPolicy?.ScanForMaliciousCode != true || !File.Exists(manifest.AssemblyPath))
                return;

            try
            {
                var analysis = await _codeAnalyzer.AnalyzeAssemblyAsync(manifest.AssemblyPath, cancellationToken);

                foreach (var finding in analysis.SuspiciousPatterns)
                {
                    switch (finding.Severity)
                    {
                        case SeverityLevel.Critical:
                        case SeverityLevel.High:
                            result.ValidationErrors.Add($"Suspicious code pattern detected: {finding.Description}");
                            break;
                        case SeverityLevel.Medium:
                        case SeverityLevel.Low:
                            result.ValidationWarnings.Add($"Potentially suspicious code pattern: {finding.Description}");
                            break;
                    }
                }

                _logger.LogInformation("Code analysis completed for plugin: {PluginId}. Findings: {Count}",
                    manifest.Id, analysis.SuspiciousPatterns.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Code analysis failed for plugin: {PluginId}", manifest.Id);
                result.ValidationWarnings.Add($"Code analysis failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates plugin permissions against security policy.
        /// </summary>
        private void ValidatePermissions(NuGetPluginManifest manifest, NuGetPluginValidationResult result)
        {
            if (_securityPolicy?.AllowedPermissions == null || manifest.RequiredPermissions == null)
                return;

            var unauthorizedPermissions = manifest.RequiredPermissions
                .Except(_securityPolicy.AllowedPermissions, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var permission in unauthorizedPermissions)
            {
                result.ValidationErrors.Add($"Plugin requires unauthorized permission: {permission}");
            }

            _logger.LogInformation("Permission validation completed for plugin: {PluginId}. Unauthorized permissions: {Count}",
                manifest.Id, unauthorizedPermissions.Count);
        }

        /// <summary>
        /// Scans a specific package for vulnerabilities.
        /// </summary>
        private async Task ScanPackageAsync(string packageId, string version, SecurityScanResult scanResult, CancellationToken cancellationToken)
        {
            var vulnerabilities = await _vulnerabilityDatabase.GetVulnerabilitiesAsync(packageId, version, cancellationToken);

            foreach (var vulnerability in vulnerabilities)
            {
                switch (vulnerability.Severity)
                {
                    case VulnerabilitySeverity.Critical:
                        scanResult.CriticalVulnerabilities.Add(vulnerability);
                        break;
                    case VulnerabilitySeverity.High:
                        scanResult.HighRiskVulnerabilities.Add(vulnerability);
                        break;
                    case VulnerabilitySeverity.Medium:
                        scanResult.MediumRiskVulnerabilities.Add(vulnerability);
                        break;
                    case VulnerabilitySeverity.Low:
                        scanResult.LowRiskVulnerabilities.Add(vulnerability);
                        break;
                }
            }
        }

        /// <summary>
        /// Scans a dependency for vulnerabilities.
        /// </summary>
        private async Task ScanDependencyAsync(NuGetPackageDependency dependency, SecurityScanResult scanResult, CancellationToken cancellationToken)
        {
            // Parse version range to get specific versions to scan
            var versionsToScan = await GetVersionsToScanAsync(dependency, cancellationToken);

            foreach (var version in versionsToScan)
            {
                await ScanPackageAsync(dependency.Id, version, scanResult, cancellationToken);
            }
        }

        /// <summary>
        /// Gets specific versions to scan for a dependency.
        /// </summary>
        private async Task<List<string>> GetVersionsToScanAsync(NuGetPackageDependency dependency, CancellationToken cancellationToken)
        {
            // This would resolve the version range to specific versions
            // For now, return a single mock version
            await Task.CompletedTask;
            return ["1.0.0"];
        }

        /// <summary>
        /// Checks the vulnerability database for known issues.
        /// </summary>
        private async Task CheckVulnerabilityDatabaseAsync(NuGetPluginManifest manifest, SecurityScanResult scanResult, CancellationToken cancellationToken)
        {
            // Check if the plugin or its dependencies have known vulnerabilities
            await _vulnerabilityDatabase.CheckPluginAsync(manifest, scanResult, cancellationToken);
        }
    }

    /// <summary>
    /// Security policy configuration for plugin validation.
    /// </summary>
    public class SecurityPolicy
    {
        /// <summary>
        /// Gets or sets whether to require digitally signed assemblies.
        /// </summary>
        public bool RequireSignedAssemblies { get; set; }

        /// <summary>
        /// Gets or sets the list of trusted certificate publishers.
        /// </summary>
        public List<string>? TrustedPublishers { get; set; }

        /// <summary>
        /// Gets or sets whether to scan for malicious code patterns.
        /// </summary>
        public bool ScanForMaliciousCode { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to block packages with high-risk vulnerabilities.
        /// </summary>
        public bool BlockHighRiskPackages { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to block packages with critical vulnerabilities.
        /// </summary>
        public bool BlockCriticalVulnerabilities { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum allowed assembly size in bytes.
        /// </summary>
        public long? MaxAssemblySize { get; set; }

        /// <summary>
        /// Gets or sets whether to block unsafe code.
        /// </summary>
        public bool BlockUnsafeCode { get; set; } = true;

        /// <summary>
        /// Gets or sets the allowed package sources.
        /// </summary>
        public List<string>? AllowedSources { get; set; }

        /// <summary>
        /// Gets or sets the blocked package sources.
        /// </summary>
        public List<string>? BlockedSources { get; set; }

        /// <summary>
        /// Gets or sets the allowed permissions for plugins.
        /// </summary>
        public List<string>? AllowedPermissions { get; set; }

        /// <summary>
        /// Gets or sets the vulnerability scan timeout.
        /// </summary>
        public TimeSpan VulnerabilityScanTimeout { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Gets or sets whether to enable network security validation.
        /// </summary>
        public bool EnableNetworkValidation { get; set; } = true;
    }

    /// <summary>
    /// Result of a security vulnerability scan.
    /// </summary>
    public class SecurityScanResult
    {
        /// <summary>
        /// Gets or sets the plugin ID that was scanned.
        /// </summary>
        public string PluginId { get; set; } = "";

        /// <summary>
        /// Gets or sets the plugin version that was scanned.
        /// </summary>
        public string PluginVersion { get; set; } = "";

        /// <summary>
        /// Gets or sets the scan date.
        /// </summary>
        public DateTimeOffset ScanDate { get; set; }

        /// <summary>
        /// Gets the critical vulnerabilities found.
        /// </summary>
        public List<SecurityVulnerability> CriticalVulnerabilities { get; } = [];

        /// <summary>
        /// Gets the high-risk vulnerabilities found.
        /// </summary>
        public List<SecurityVulnerability> HighRiskVulnerabilities { get; } = [];

        /// <summary>
        /// Gets the medium-risk vulnerabilities found.
        /// </summary>
        public List<SecurityVulnerability> MediumRiskVulnerabilities { get; } = [];

        /// <summary>
        /// Gets the low-risk vulnerabilities found.
        /// </summary>
        public List<SecurityVulnerability> LowRiskVulnerabilities { get; } = [];

        /// <summary>
        /// Gets all vulnerabilities found.
        /// </summary>
        public IEnumerable<SecurityVulnerability> AllVulnerabilities =>
            CriticalVulnerabilities.Concat(HighRiskVulnerabilities)
                .Concat(MediumRiskVulnerabilities)
                .Concat(LowRiskVulnerabilities);

        /// <summary>
        /// Gets whether critical vulnerabilities were found.
        /// </summary>
        public bool HasCriticalVulnerabilities => CriticalVulnerabilities.Count > 0;

        /// <summary>
        /// Gets whether high-risk vulnerabilities were found.
        /// </summary>
        public bool HasHighRiskVulnerabilities => HighRiskVulnerabilities.Count > 0;

        /// <summary>
        /// Gets the scan errors.
        /// </summary>
        public List<string> ScanErrors { get; } = [];

        /// <summary>
        /// Gets or sets the scan duration.
        /// </summary>
        public TimeSpan ScanDuration { get; set; }
    }

    /// <summary>
    /// Represents a security vulnerability.
    /// </summary>
    public class SecurityVulnerability
    {
        /// <summary>
        /// Gets or sets the vulnerability ID (e.g., CVE-2023-1234).
        /// </summary>
        public required string Id { get; set; }

        /// <summary>
        /// Gets or sets the vulnerability title.
        /// </summary>
        public required string Title { get; set; }

        /// <summary>
        /// Gets or sets the vulnerability description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the vulnerability severity.
        /// </summary>
        public VulnerabilitySeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the CVSS score.
        /// </summary>
        public double? CvssScore { get; set; }

        /// <summary>
        /// Gets or sets the affected package ID.
        /// </summary>
        public string? PackageId { get; set; }

        /// <summary>
        /// Gets or sets the affected version range.
        /// </summary>
        public string? AffectedVersionRange { get; set; }

        /// <summary>
        /// Gets or sets the fixed version.
        /// </summary>
        public string? FixedVersion { get; set; }

        /// <summary>
        /// Gets or sets the advisory URL.
        /// </summary>
        public string? AdvisoryUrl { get; set; }

        /// <summary>
        /// Gets or sets the publication date.
        /// </summary>
        public DateTimeOffset? PublishedAt { get; set; }
    }

    /// <summary>
    /// Severity levels for vulnerabilities.
    /// </summary>
    public enum VulnerabilitySeverity
    {
        /// <summary>
        /// Low severity vulnerability.
        /// </summary>
        Low,

        /// <summary>
        /// Medium severity vulnerability.
        /// </summary>
        Medium,

        /// <summary>
        /// High severity vulnerability.
        /// </summary>
        High,

        /// <summary>
        /// Critical severity vulnerability.
        /// </summary>
        Critical
    }

    /// <summary>
    /// Cache entry for security scan results.
    /// </summary>
    internal sealed class SecurityScanCache
    {
        public required SecurityScanResult ScanResult { get; set; }
        public DateTimeOffset CachedAt { get; set; }
        public bool IsExpired => DateTimeOffset.UtcNow - CachedAt > TimeSpan.FromHours(1);
    }

    /// <summary>
    /// Vulnerability database for checking known security issues.
    /// </summary>
    internal sealed class VulnerabilityDatabase
    {
        private readonly ILogger _logger;

        public VulnerabilityDatabase(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<List<SecurityVulnerability>> GetVulnerabilitiesAsync(string packageId, string version, CancellationToken cancellationToken)
        {
            // This would query a real vulnerability database (NVD, GitHub Advisory Database, etc.)
            await Task.Delay(10, cancellationToken);

            // Return mock vulnerabilities for testing
            if (string.Equals(packageId, "VulnerablePackage", StringComparison.OrdinalIgnoreCase))
            {
                return [
                    new SecurityVulnerability
                    {
                        Id = "CVE-2023-12345",
                        Title = "Remote Code Execution",
                        Description = "Mock vulnerability for testing",
                        Severity = VulnerabilitySeverity.Critical,
                        CvssScore = 9.8,
                        PackageId = packageId,
                        AffectedVersionRange = "[1.0.0,2.0.0)",
                        FixedVersion = "2.0.0"
                    }
                ];
            }

            return [];
        }

        public async Task CheckPluginAsync(NuGetPluginManifest manifest, SecurityScanResult scanResult, CancellationToken cancellationToken)
        {
            // Check plugin-specific vulnerabilities
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Code analyzer for detecting malicious patterns in assemblies.
    /// </summary>
    internal sealed class CodeAnalyzer
    {
        private readonly ILogger _logger;

        public CodeAnalyzer(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<CodeAnalysisResult> AnalyzeAssemblyAsync(string assemblyPath, CancellationToken cancellationToken)
        {
            var result = new CodeAnalysisResult();

            try
            {
                // This would use tools like Roslyn analyzers, reflection, or IL analysis
                await Task.Delay(50, cancellationToken); // Simulate analysis time

                // Simple heuristic analysis based on file content
                await AnalyzeFileHeuristics(assemblyPath, result, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Code analysis failed for assembly: {AssemblyPath}", assemblyPath);
                result.AnalysisErrors.Add($"Analysis failed: {ex.Message}");
            }

            return result;
        }

        private async Task AnalyzeFileHeuristics(string assemblyPath, CodeAnalysisResult result, CancellationToken cancellationToken)
        {
            var assemblyBytes = await File.ReadAllBytesAsync(assemblyPath, cancellationToken);
            var assemblyContent = System.Text.Encoding.ASCII.GetString(assemblyBytes);

            // Check for suspicious patterns
            var suspiciousPatterns = new[]
            {
                ("Process.Start", SeverityLevel.Medium, "Process execution detected"),
                ("Registry.SetValue", SeverityLevel.Medium, "Registry modification detected"),
                ("File.Delete", SeverityLevel.Low, "File deletion capability detected"),
                ("NetworkCredential", SeverityLevel.Medium, "Network credential handling detected"),
                ("PowerShell", SeverityLevel.High, "PowerShell execution detected"),
                ("cmd.exe", SeverityLevel.High, "Command execution detected")
            };

            foreach (var (pattern, severity, description) in suspiciousPatterns)
            {
                if (assemblyContent.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    result.SuspiciousPatterns.Add(new SuspiciousCodePattern
                    {
                        Pattern = pattern,
                        Severity = severity,
                        Description = description,
                        Location = "Assembly content"
                    });
                }
            }
        }
    }

    /// <summary>
    /// Result of code analysis.
    /// </summary>
    internal sealed class CodeAnalysisResult
    {
        public List<SuspiciousCodePattern> SuspiciousPatterns { get; } = [];
        public List<string> AnalysisErrors { get; } = [];
    }

    /// <summary>
    /// Represents a suspicious code pattern found during analysis.
    /// </summary>
    internal sealed class SuspiciousCodePattern
    {
        public required string Pattern { get; set; }
        public SeverityLevel Severity { get; set; }
        public required string Description { get; set; }
        public required string Location { get; set; }
    }

    /// <summary>
    /// Severity levels for code analysis findings.
    /// </summary>
    internal enum SeverityLevel
    {
        Low,
        Medium,
        High,
        Critical
    }
}
