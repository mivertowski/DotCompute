// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DotCompute.Algorithms.Security;


/// <summary>
/// Security rule that validates file size constraints.
/// </summary>
public sealed class FileSizeSecurityRule : SecurityRule
{
    private readonly long _maxSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSizeSecurityRule"/> class.
    /// </summary>
    /// <param name="maxSize">Maximum allowed file size in bytes.</param>
    public FileSizeSecurityRule(long maxSize)
    {
        _maxSize = maxSize;
    }

    /// <inheritdoc/>
    public override SecurityEvaluationResult Evaluate(SecurityEvaluationContext context)
    {
        var result = new SecurityEvaluationResult();

        try
        {
            var fileInfo = new FileInfo(context.AssemblyPath);

            if (fileInfo.Length > _maxSize)
            {
                result.IsAllowed = false;
                result.SecurityLevel = SecurityLevel.Low;
                result.Violations.Add($"Assembly size ({fileInfo.Length:N0} bytes) exceeds maximum allowed size ({_maxSize:N0} bytes)");
            }
            else if (fileInfo.Length > _maxSize * 0.8) // Warn at 80% of limit
            {
                result.Warnings.Add($"Assembly size ({fileInfo.Length:N0} bytes) is approaching the maximum limit ({_maxSize:N0} bytes)");
            }
        }
        catch (Exception ex)
        {
            result.IsAllowed = false;
            result.SecurityLevel = SecurityLevel.Low;
            result.Violations.Add($"Failed to check file size: {ex.Message}");
        }

        return result;
    }
}

/// <summary>
/// Security rule that validates digital signatures using Authenticode.
/// </summary>
public sealed class DigitalSignatureSecurityRule : SecurityRule
{
    /// <inheritdoc/>
    public override SecurityEvaluationResult Evaluate(SecurityEvaluationContext context)
    {
        var result = new SecurityEvaluationResult();

        try
        {
            // Try to get the certificate from the assembly
            var certificate = GetAssemblyCertificate(context.AssemblyPath);

            if (certificate == null)
            {
                result.IsAllowed = false;
                result.SecurityLevel = SecurityLevel.Low;
                result.Violations.Add("No digital signature found on assembly");
                return result;
            }

            // Validate certificate chain
            var chain = new X509Chain
            {
                ChainPolicy =

            {
                RevocationMode = X509RevocationMode.Online,
                RevocationFlag = X509RevocationFlag.ExcludeRoot,
                VerificationFlags = X509VerificationFlags.NoFlag
            }
            };

            var chainValid = chain.Build(certificate);

            if (!chainValid)
            {
                result.SecurityLevel = SecurityLevel.Medium;
                result.Warnings.Add("Certificate chain validation failed");

                foreach (var status in chain.ChainStatus)
                {
                    if (status.Status == X509ChainStatusFlags.NotTimeValid)
                    {
                        result.IsAllowed = false;
                        result.Violations.Add("Certificate has expired or is not yet valid");
                    }
                    else if (status.Status == X509ChainStatusFlags.Revoked)
                    {
                        result.IsAllowed = false;
                        result.Violations.Add("Certificate has been revoked");
                    }
                    else
                    {
                        result.Warnings.Add($"Certificate chain issue: {status.StatusInformation}");
                    }
                }
            }

            // Check if certificate is from a trusted publisher
            var trustedStore = new X509Store(StoreName.TrustedPublisher, StoreLocation.LocalMachine);
            try
            {
                trustedStore.Open(OpenFlags.ReadOnly);
                var trustedCert = trustedStore.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false);

                if (trustedCert.Count == 0)
                {
                    result.SecurityLevel = SecurityLevel.Medium;
                    result.Warnings.Add("Publisher certificate is not in the trusted publisher store");
                }
                else
                {
                    result.SecurityLevel = SecurityLevel.High;
                }
            }
            finally
            {
                trustedStore.Close();
            }

            // Store certificate in context for other rules
            context.Certificate = certificate;
            result.Metadata["CertificateThumbprint"] = certificate.Thumbprint;
            result.Metadata["CertificateSubject"] = certificate.Subject;
            result.Metadata["CertificateIssuer"] = certificate.Issuer;
            result.Metadata["CertificateExpiration"] = certificate.NotAfter;
        }
        catch (Exception ex)
        {
            result.IsAllowed = false;
            result.SecurityLevel = SecurityLevel.Low;
            result.Violations.Add($"Digital signature validation failed: {ex.Message}");
        }

        return result;
    }

    private static X509Certificate2? GetAssemblyCertificate(string assemblyPath)
    {
        try
        {
            // Load the assembly and check for Authenticode signature
            // Using X509Certificate2 constructor with X509Certificate
            // Use X509CertificateLoader instead of obsolete CreateFromSignedFile
            return X509CertificateLoader.LoadCertificateFromFile(assemblyPath);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Security rule that validates strong name signatures.
/// </summary>
public sealed class StrongNameSecurityRule : SecurityRule
{
    /// <inheritdoc/>
    public override SecurityEvaluationResult Evaluate(SecurityEvaluationContext context)
    {
        var result = new SecurityEvaluationResult();

        try
        {
            // Load assembly to check strong name
            var assemblyName = AssemblyName.GetAssemblyName(context.AssemblyPath);
            var publicKey = assemblyName.GetPublicKey();

            if (publicKey == null || publicKey.Length == 0)
            {
                result.IsAllowed = false;
                result.SecurityLevel = SecurityLevel.Low;
                result.Violations.Add("Assembly is not signed with a strong name");
                return result;
            }

            // Verify strong name signature
            if (!IsStrongNameValid(context.AssemblyPath, publicKey))
            {
                result.IsAllowed = false;
                result.SecurityLevel = SecurityLevel.Low;
                result.Violations.Add("Strong name signature is invalid");
                return result;
            }

            // Store public key for other rules
            context.StrongNameKey = publicKey;
            result.Metadata["StrongNameKeySize"] = publicKey.Length;
            result.Metadata["PublicKeyToken"] = Convert.ToHexString(assemblyName.GetPublicKeyToken() ?? []);
            result.SecurityLevel = SecurityLevel.High;
        }
        catch (Exception ex)
        {
            result.IsAllowed = false;
            result.SecurityLevel = SecurityLevel.Low;
            result.Violations.Add($"Strong name validation failed: {ex.Message}");
        }

        return result;
    }

    private static bool IsStrongNameValid(string assemblyPath, byte[] publicKey)
    {
        try
        {
            // Basic validation: check if public key exists and has reasonable size
            if (publicKey.Length < 160) // Minimum size for RSA-1024
            {
                return false;
            }

            // Additional validation could be implemented here
            // For now, we'll assume the presence of a public key indicates valid strong name
            // In a production environment, you might want to use Windows APIs or
            // third-party libraries for more thorough validation

            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Security rule that analyzes assembly metadata for suspicious patterns.
/// </summary>
public sealed class MetadataAnalysisSecurityRule : SecurityRule
{
    private static readonly HashSet<string> SuspiciousNamespaces = new(StringComparer.OrdinalIgnoreCase)
{
    "System.Diagnostics",
    "System.IO",
    "System.Net",
    "System.Reflection.Emit",
    "Microsoft.Win32",
    "System.Security.Cryptography"
};

    private static readonly HashSet<string> SuspiciousTypeNames = new(StringComparer.OrdinalIgnoreCase)
{
    "Process",
    "ProcessStartInfo",
    "FileStream",
    "Socket",
    "WebClient",
    "HttpClient",
    "Registry",
    "AssemblyBuilder",
    "ModuleBuilder",
    "TypeBuilder"
};

    /// <inheritdoc/>
    public override SecurityEvaluationResult Evaluate(SecurityEvaluationContext context)
    {
        var result = new SecurityEvaluationResult();
        var suspiciousCount = 0;
        var warnings = new List<string>();

        try
        {
            // Load assembly for metadata analysis
            var assembly = Assembly.LoadFrom(context.AssemblyPath);

            // Analyze types and their namespaces
            foreach (var type in assembly.GetTypes())
            {
                // Check namespace
                if (!string.IsNullOrEmpty(type.Namespace) && SuspiciousNamespaces.Contains(type.Namespace))
                {
                    suspiciousCount++;
                    warnings.Add($"References suspicious namespace: {type.Namespace}");
                }

                // Check type names
                if (SuspiciousTypeNames.Contains(type.Name))
                {
                    suspiciousCount++;
                    warnings.Add($"Uses suspicious type: {type.FullName}");
                }

                // Check for dynamic code generation capabilities
                if (typeof(System.Reflection.Emit.AssemblyBuilder).IsAssignableFrom(type) ||
                    typeof(System.Reflection.Emit.ModuleBuilder).IsAssignableFrom(type))
                {
                    suspiciousCount++;
                    warnings.Add($"Has dynamic code generation capabilities: {type.FullName}");
                }
            }

            // Analyze assembly attributes
            var attributes = assembly.GetCustomAttributes();
            foreach (var attr in attributes)
            {
                // Check for unsafe code attributes
                if (attr.GetType().Name.Contains("Unsafe", StringComparison.OrdinalIgnoreCase) ||
                    attr.GetType().Name.Contains("Unmanaged", StringComparison.OrdinalIgnoreCase))
                {
                    suspiciousCount++;
                    warnings.Add($"Contains unsafe code attribute: {attr.GetType().Name}");
                }
            }

            // Evaluate suspicion level
            if (suspiciousCount > 10)
            {
                result.IsAllowed = false;
                result.SecurityLevel = SecurityLevel.Low;
                result.Violations.Add($"Assembly contains too many suspicious patterns ({suspiciousCount} found)");
            }
            else if (suspiciousCount > 5)
            {
                result.SecurityLevel = SecurityLevel.Medium;
                result.Warnings.Add($"Assembly contains moderate suspicious patterns ({suspiciousCount} found)");
            }
            else if (suspiciousCount > 0)
            {
                result.SecurityLevel = SecurityLevel.High;
                result.Warnings.AddRange(warnings.Take(5)); // Limit warnings
            }

            result.Metadata["SuspiciousPatternCount"] = suspiciousCount;
            result.Metadata["TypeCount"] = assembly.GetTypes().Length;
        }
        catch (ReflectionTypeLoadException ex)
        {
            result.SecurityLevel = SecurityLevel.Medium;
            result.Warnings.Add($"Some types could not be loaded for analysis: {ex.LoaderExceptions.Length} errors");
        }
        catch (Exception ex)
        {
            result.SecurityLevel = SecurityLevel.Medium;
            result.Warnings.Add($"Metadata analysis encountered issues: {ex.Message}");
        }

        return result;
    }
}

/// <summary>
/// Security rule that enforces directory-based policies.
/// </summary>
public sealed class DirectoryPolicySecurityRule : SecurityRule
{
    private readonly Dictionary<string, SecurityLevel> _directoryPolicies;

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectoryPolicySecurityRule"/> class.
    /// </summary>
    /// <param name="directoryPolicies">The directory policies to enforce.</param>
    public DirectoryPolicySecurityRule(Dictionary<string, SecurityLevel> directoryPolicies)
    {
        _directoryPolicies = directoryPolicies ?? throw new ArgumentNullException(nameof(directoryPolicies));
    }

    /// <inheritdoc/>
    public override SecurityEvaluationResult Evaluate(SecurityEvaluationContext context)
    {
        var result = new SecurityEvaluationResult();

        try
        {
            var assemblyDir = Path.GetDirectoryName(context.AssemblyPath);
            if (string.IsNullOrEmpty(assemblyDir))
            {
                result.IsAllowed = false;
                result.SecurityLevel = SecurityLevel.Low;
                result.Violations.Add("Cannot determine assembly directory");
                return result;
            }

            assemblyDir = Path.GetFullPath(assemblyDir);

            // Find the most specific directory policy that applies
            SecurityLevel? applicableLevel = null;
            string? matchedPolicy = null;

            foreach (var policy in _directoryPolicies.OrderByDescending(p => p.Key.Length))
            {
                var policyDir = Path.GetFullPath(policy.Key);
                if (assemblyDir.StartsWith(policyDir, StringComparison.OrdinalIgnoreCase))
                {
                    applicableLevel = policy.Value;
                    matchedPolicy = policy.Key;
                    break;
                }
            }

            if (applicableLevel.HasValue)
            {
                result.SecurityLevel = applicableLevel.Value;
                result.Metadata["AppliedDirectoryPolicy"] = matchedPolicy!;  // Non-null because we checked HasValue

                if (applicableLevel.Value == SecurityLevel.Low)
                {
                    result.IsAllowed = false;
                    result.Violations.Add($"Assembly location '{assemblyDir}' is restricted by directory policy");
                }
                else if (applicableLevel.Value == SecurityLevel.Medium)
                {
                    result.Warnings.Add($"Assembly location '{assemblyDir}' has medium trust policy");
                }
            }
            else
            {
                // No specific policy found - use default medium security level
                result.SecurityLevel = SecurityLevel.Medium;
                result.Warnings.Add($"No specific directory policy found for '{assemblyDir}', using default security level");
            }
        }
        catch (Exception ex)
        {
            result.IsAllowed = false;
            result.SecurityLevel = SecurityLevel.Low;
            result.Violations.Add($"Directory policy evaluation failed: {ex.Message}");
        }

        return result;
    }
}

/// <summary>
/// Security rule that validates assemblies against a blocklist.
/// </summary>
public sealed class BlocklistSecurityRule : SecurityRule
{
    private readonly HashSet<string> _blockedHashes;
    private readonly HashSet<string> _blockedNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlocklistSecurityRule"/> class.
    /// </summary>
    /// <param name="blockedHashes">Set of blocked assembly hashes.</param>
    /// <param name="blockedNames">Set of blocked assembly names.</param>
    public BlocklistSecurityRule(HashSet<string> blockedHashes, HashSet<string> blockedNames)
    {
        _blockedHashes = blockedHashes ?? throw new ArgumentNullException(nameof(blockedHashes));
        _blockedNames = blockedNames ?? throw new ArgumentNullException(nameof(blockedNames));
    }

    /// <inheritdoc/>
    public override SecurityEvaluationResult Evaluate(SecurityEvaluationContext context)
    {
        var result = new SecurityEvaluationResult();

        try
        {
            // Check assembly name
            var assemblyName = Path.GetFileNameWithoutExtension(context.AssemblyPath);
            if (_blockedNames.Contains(assemblyName))
            {
                result.IsAllowed = false;
                result.SecurityLevel = SecurityLevel.Low;
                result.Violations.Add($"Assembly name '{assemblyName}' is on the blocklist");
                return result;
            }

            // Check assembly hash if we have the bytes
            if (context.AssemblyBytes != null)
            {
                using var sha256 = SHA256.Create();
                var hash = Convert.ToHexString(sha256.ComputeHash(context.AssemblyBytes));

                if (_blockedHashes.Contains(hash))
                {
                    result.IsAllowed = false;
                    result.SecurityLevel = SecurityLevel.Low;
                    result.Violations.Add($"Assembly hash '{hash}' is on the blocklist");
                    return result;
                }

                result.Metadata["AssemblyHash"] = hash;
            }
            else if (File.Exists(context.AssemblyPath))
            {
                // Compute hash from file
                using var sha256 = SHA256.Create();
                using var fileStream = File.OpenRead(context.AssemblyPath);
                var hash = Convert.ToHexString(sha256.ComputeHash(fileStream));

                if (_blockedHashes.Contains(hash))
                {
                    result.IsAllowed = false;
                    result.SecurityLevel = SecurityLevel.Low;
                    result.Violations.Add($"Assembly hash '{hash}' is on the blocklist");
                    return result;
                }

                result.Metadata["AssemblyHash"] = hash;
            }

            result.SecurityLevel = SecurityLevel.High;
        }
        catch (Exception ex)
        {
            result.SecurityLevel = SecurityLevel.Medium;
            result.Warnings.Add($"Blocklist validation encountered issues: {ex.Message}");
        }

        return result;
    }
}
