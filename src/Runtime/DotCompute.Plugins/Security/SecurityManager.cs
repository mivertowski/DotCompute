// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using DotCompute.Plugins.Logging;
using System;

namespace DotCompute.Plugins.Security;

/// <summary>
/// Manages security validation and analysis for plugin assemblies.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SecurityManager"/> class.
/// </remarks>
public class SecurityManager(ILogger logger) : IDisposable
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SHA256 _hashAlgorithm = SHA256.Create();
    private bool _disposed;

    /// <summary>
    /// Validates the integrity of an assembly file.
    /// </summary>
    public async Task<bool> ValidateAssemblyIntegrityAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        try
        {
            _logger.LogDebugMessage("Validating assembly integrity: {assemblyPath}");

            // Basic file validation
            if (!File.Exists(assemblyPath))
            {
                _logger.LogWarningMessage("Assembly file not found: {assemblyPath}");
                return false;
            }

            // Validate PE structure
            using var fileStream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(fileStream);

            if (!peReader.HasMetadata)
            {
                _logger.LogWarningMessage("Assembly does not contain valid metadata: {assemblyPath}");
                return false;
            }

            // Validate metadata
            var metadataReader = peReader.GetMetadataReader();
            if (!ValidateMetadataStructure(metadataReader))
            {
                _logger.LogWarningMessage("Assembly metadata structure is invalid: {assemblyPath}");
                return false;
            }

            // Calculate and validate hash
            var hash = await CalculateAssemblyHashAsync(assemblyPath, cancellationToken);
            if (string.IsNullOrEmpty(hash))
            {
                _logger.LogWarningMessage("Failed to calculate assembly hash: {assemblyPath}");
                return false;
            }

            _logger.LogDebugMessage($"Assembly integrity validated successfully: {assemblyPath} (Hash: {hash[..16] + "..."})");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Error validating assembly integrity: {assemblyPath}");
            return false;
        }
    }

    /// <summary>
    /// Analyzes assembly metadata for suspicious patterns.
    /// </summary>
    public async Task<AssemblyMetadataAnalysis> AnalyzeAssemblyMetadataAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        var analysis = new AssemblyMetadataAnalysis();

        try
        {
            _logger.LogDebugMessage("Analyzing assembly metadata: {assemblyPath}");

            using var fileStream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(fileStream);
            var metadataReader = peReader.GetMetadataReader();

            // Analyze types and methods
            await AnalyzeTypesAndMethodsAsync(metadataReader, analysis, cancellationToken);

            // Analyze strings and resources
            AnalyzeStringsAndResources(metadataReader, analysis);

            // Analyze attributes
            AnalyzeAttributes(metadataReader, analysis);

            // Analyze referenced assemblies
            AnalyzeAssemblyReferences(metadataReader, analysis);

            // Determine overall risk level
            analysis.RiskLevel = CalculateRiskLevel(analysis);

            _logger.LogDebugMessage($"Assembly metadata analysis completed: {assemblyPath}, Risk: {analysis.RiskLevel}, Patterns: {analysis.SuspiciousPatterns.Count}");

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Error analyzing assembly metadata: {assemblyPath}");
            analysis.HasError = true;
            analysis.ErrorMessage = ex.Message;
            return analysis;
        }
    }

    /// <summary>
    /// Calculates a SHA-256 hash of the assembly file.
    /// </summary>
    private async Task<string> CalculateAssemblyHashAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        try
        {
            using var fileStream = File.OpenRead(assemblyPath);
            var hashBytes = await _hashAlgorithm.ComputeHashAsync(fileStream, cancellationToken);
            return Convert.ToHexString(hashBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calculating assembly hash: {AssemblyPath}", assemblyPath);
            return string.Empty;
        }
    }

    /// <summary>
    /// Validates the basic structure of assembly metadata.
    /// </summary>
    private static bool ValidateMetadataStructure(MetadataReader metadataReader)
    {
        try
        {
            // Basic validation checks
            if (metadataReader.TypeDefinitions.Count == 0)
            {
                return false; // No types defined
            }

            // Validate that required tables are present and have valid entries
            var assemblyDefinition = metadataReader.GetAssemblyDefinition();
            var assemblyName = metadataReader.GetString(assemblyDefinition.Name);


            if (string.IsNullOrEmpty(assemblyName))
            {
                return false; // Invalid assembly name
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Analyzes types and methods for suspicious patterns.
    /// </summary>
    private static async Task AnalyzeTypesAndMethodsAsync(MetadataReader metadataReader, AssemblyMetadataAnalysis analysis, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            var suspiciousMethodNames = new[]
            {
                "CreateProcess", "LoadLibrary", "GetProcAddress", "VirtualAlloc", "WriteProcessMemory",
                "ReadProcessMemory", "CreateRemoteThread", "SetWindowsHookEx", "RegOpenKeyEx",
                "RegSetValueEx", "CryptAcquireContext", "InternetOpen", "HttpSendRequest"
            };

            var suspiciousTypeNames = new[]
            {
                "ProcessStartInfo", "Registry", "RegistryKey", "PowerShell", "ScriptBlock",
                "Activator", "AppDomain", "Assembly", "Reflection"
            };

            foreach (var typeHandle in metadataReader.TypeDefinitions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var typeDef = metadataReader.GetTypeDefinition(typeHandle);
                var typeName = metadataReader.GetString(typeDef.Name);

                // Check for suspicious type names
                foreach (var suspicious in suspiciousTypeNames)
                {
                    if (typeName.Contains(suspicious, StringComparison.OrdinalIgnoreCase))
                    {
                        analysis.SuspiciousPatterns.Add($"Suspicious type: {typeName}");
                    }
                }

                // Analyze methods
                foreach (var methodHandle in typeDef.GetMethods())
                {
                    var methodDef = metadataReader.GetMethodDefinition(methodHandle);
                    var methodName = metadataReader.GetString(methodDef.Name);

                    // Check for suspicious method names
                    foreach (var suspicious in suspiciousMethodNames)
                    {
                        if (methodName.Contains(suspicious, StringComparison.OrdinalIgnoreCase))
                        {
                            analysis.SuspiciousPatterns.Add($"Suspicious method: {typeName}.{methodName}");
                        }
                    }

                    // Check for unsafe code
                    if ((methodDef.Attributes & MethodAttributes.HasSecurity) != 0)
                    {
                        analysis.SuspiciousPatterns.Add($"Method with security attributes: {typeName}.{methodName}");
                    }
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Analyzes string literals and resources for suspicious content.
    /// </summary>
    private static void AnalyzeStringsAndResources(MetadataReader metadataReader, AssemblyMetadataAnalysis analysis)
    {
        var suspiciousStrings = new[]
        {
            "cmd.exe", "powershell.exe", "rundll32.exe", "regsvr32.exe",
            "hkey_", "software\\microsoft\\windows\\currentversion\\run",
            "createprocess", "shellexecute", "downloadfile"
        };

        // Iterate through user strings in the metadata
        var userStrings = typeof(MetadataReader).GetProperty("UserStrings")?.GetValue(metadataReader);
        if (userStrings is IEnumerable<UserStringHandle> handles)
        {
            foreach (var handle in handles)
            {
                var stringValue = metadataReader.GetUserString(handle).ToLowerInvariant();


                foreach (var suspicious in suspiciousStrings)
                {
                    if (stringValue.Contains(suspicious, StringComparison.OrdinalIgnoreCase))
                    {
                        analysis.SuspiciousPatterns.Add($"Suspicious string: {suspicious}");
                        break; // Avoid duplicate entries
                    }
                }
            }
        }
    }

    /// <summary>
    /// Analyzes custom attributes for security-relevant information.
    /// </summary>
    private static void AnalyzeAttributes(MetadataReader metadataReader, AssemblyMetadataAnalysis analysis)
    {
        var dangerousAttributes = new[]
        {
            "AllowPartiallyTrustedCallersAttribute", "SecurityCriticalAttribute",
            "SuppressUnmanagedCodeSecurityAttribute", "UnverifiableCodeAttribute"
        };

        foreach (var attrHandle in metadataReader.CustomAttributes)
        {
            var attr = metadataReader.GetCustomAttribute(attrHandle);


            if (attr.Constructor.Kind == HandleKind.MemberReference)
            {
                var memberRef = metadataReader.GetMemberReference((MemberReferenceHandle)attr.Constructor);
                var typeRef = metadataReader.GetTypeReference((TypeReferenceHandle)memberRef.Parent);
                var typeName = metadataReader.GetString(typeRef.Name);

                if (dangerousAttributes.Any(typeName.Contains))
                {
                    analysis.SuspiciousPatterns.Add($"Dangerous attribute: {typeName}");
                }
            }
        }
    }

    /// <summary>
    /// Analyzes referenced assemblies for potential security risks.
    /// </summary>
    private static void AnalyzeAssemblyReferences(MetadataReader metadataReader, AssemblyMetadataAnalysis analysis)
    {
        var riskyAssemblies = new[]
        {
            "System.Management", "System.DirectoryServices", "Microsoft.Win32",
            "System.Diagnostics.Process", "System.Net.NetworkInformation",
            "global::System.Security.Principal", "System.ServiceProcess"
        };

        foreach (var asmRefHandle in metadataReader.AssemblyReferences)
        {
            var asmRef = metadataReader.GetAssemblyReference(asmRefHandle);
            var asmName = metadataReader.GetString(asmRef.Name);

            if (riskyAssemblies.Any(risky => asmName.StartsWith(risky, StringComparison.OrdinalIgnoreCase)))
            {
                analysis.SuspiciousPatterns.Add($"Risky assembly reference: {asmName}");
            }
        }
    }

    /// <summary>
    /// Calculates the overall risk level based on analysis results.
    /// </summary>
    private static RiskLevel CalculateRiskLevel(AssemblyMetadataAnalysis analysis)
    {
        var patternCount = analysis.SuspiciousPatterns.Count;


        return patternCount switch
        {
            0 => RiskLevel.Low,
            1 or 2 => RiskLevel.Medium,
            3 or 4 => RiskLevel.High,
            _ => RiskLevel.Critical
        };
    }

    /// <summary>
    /// Disposes the security manager and its resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;
        _hashAlgorithm?.Dispose();
    }
}

/// <summary>
/// Results of assembly metadata analysis.
/// </summary>
public class AssemblyMetadataAnalysis
{
    /// <summary>
    /// Gets the list of suspicious patterns found.
    /// </summary>
    public IList<string> SuspiciousPatterns { get; } = [];

    /// <summary>
    /// Gets whether the analysis found any suspicious patterns.
    /// </summary>
    public bool HasSuspiciousPatterns => SuspiciousPatterns.Count > 0;

    /// <summary>
    /// Gets or sets the calculated risk level.
    /// </summary>
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;

    /// <summary>
    /// Gets or sets whether an error occurred during analysis.
    /// </summary>
    public bool HasError { get; set; }

    /// <summary>
    /// Gets or sets the error message if an error occurred.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
/// <summary>
/// An risk level enumeration.
/// </summary>

/// <summary>
/// Risk levels for assembly analysis.
/// </summary>
public enum RiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}