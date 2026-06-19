// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using DotCompute.Plugins.Logging;
using Microsoft.Extensions.Logging;

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
    private bool _disposed;

    /// <summary>
    /// Validates the integrity of an assembly file.
    /// </summary>
    public async Task<bool> ValidateAssemblyIntegrityAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        // Honor cancellation requested before (or during) the operation. This is checked
        // before the early file-existence exit so that a pre-cancelled token is always observed.
        cancellationToken.ThrowIfCancellationRequested();

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
        catch (OperationCanceledException)
        {
            // Cancellation must propagate to the caller rather than being treated as a validation failure.
            throw;
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

            // Analyze P/Invoke declarations for dangerous native API imports
            await AnalyzeTypesAndMethodsAsync(metadataReader, analysis, cancellationToken);

            // Analyze type references for use of dangerous managed APIs
            AnalyzeTypeReferences(metadataReader, analysis);

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
        catch (OperationCanceledException)
        {
            // Cancellation must propagate to the caller rather than being recorded as an analysis error.
            throw;
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
    /// <remarks>
    /// Uses the stateless static <see cref="SHA256.HashDataAsync(Stream, CancellationToken)"/> API rather
    /// than a shared <see cref="SHA256"/> instance. A single hash-algorithm instance carries mutable
    /// internal state and is not safe for concurrent use, which caused concurrent validations to corrupt
    /// each other and spuriously fail. The static API allocates no shared state and is fully thread-safe.
    /// </remarks>
    private async Task<string> CalculateAssemblyHashAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        try
        {
            using var fileStream = File.OpenRead(assemblyPath);
            var hashBytes = await SHA256.HashDataAsync(fileStream, cancellationToken);
            return Convert.ToHexString(hashBytes);
        }
        catch (Exception ex)
        {
            // Mid-operation cancellation is treated as a (non-fatal) hash failure here and surfaces as a
            // failed-integrity result rather than a thrown exception; an already-cancelled token is rejected
            // up front in ValidateAssemblyIntegrityAsync before any hashing work begins.
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
    /// Dangerous native API entry points that are a strong signal of malicious or
    /// privilege-escalating behaviour when imported via P/Invoke.
    /// </summary>
    private static readonly string[] DangerousNativeApis =
    [
        "CreateProcess", "LoadLibrary", "GetProcAddress", "VirtualAlloc", "WriteProcessMemory",
        "ReadProcessMemory", "CreateRemoteThread", "SetWindowsHookEx", "RegOpenKeyEx",
        "RegSetValueEx", "CryptAcquireContext", "InternetOpen", "HttpSendRequest"
    ];

    /// <summary>
    /// Analyzes the assembly's methods for genuinely suspicious patterns.
    /// </summary>
    /// <remarks>
    /// Only <em>P/Invoke declarations</em> (methods with the <see cref="MethodAttributes.PinvokeImpl"/>
    /// flag) that import a known-dangerous native API are flagged. The mere presence of a managed method
    /// whose name happens to contain a substring such as "LoadLibrary" is not a security signal — for
    /// example <c>System.Private.CoreLib</c> defines managed helpers named <c>LoadLibraryByName</c> that
    /// are entirely benign. Flagging type or method <em>definitions</em> by loose substring match produced
    /// large numbers of false positives (the entire BCL was classified as Critical), so detection is now
    /// scoped to the imports/references the assembly actually uses.
    /// </remarks>
    private static async Task AnalyzeTypesAndMethodsAsync(MetadataReader metadataReader, AssemblyMetadataAnalysis analysis, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            foreach (var typeHandle in metadataReader.TypeDefinitions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var typeDef = metadataReader.GetTypeDefinition(typeHandle);
                var typeName = metadataReader.GetString(typeDef.Name);

                foreach (var methodHandle in typeDef.GetMethods())
                {
                    var methodDef = metadataReader.GetMethodDefinition(methodHandle);

                    // Only P/Invoke declarations expose native APIs; managed methods are not native imports.
                    if ((methodDef.Attributes & MethodAttributes.PinvokeImpl) == 0)
                    {
                        continue;
                    }

                    // Resolve the actual imported native entry-point name (the DllImport EntryPoint),
                    // falling back to the managed method name when no explicit entry point is given.
                    var importName = ResolvePInvokeEntryPointName(metadataReader, methodDef);

                    foreach (var dangerous in DangerousNativeApis)
                    {
                        // Native API names are matched as whole entry-point names (allowing the common
                        // A/W Win32 suffixes), not loose substrings, to avoid false positives.
                        if (string.Equals(importName, dangerous, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(importName, dangerous + "A", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(importName, dangerous + "W", StringComparison.OrdinalIgnoreCase))
                        {
                            var methodName = metadataReader.GetString(methodDef.Name);
                            analysis.SuspiciousPatterns.Add($"Suspicious method: {typeName}.{methodName} (P/Invoke {importName})");
                            break;
                        }
                    }
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Resolves the native entry-point name imported by a P/Invoke method, preferring the explicit
    /// <c>DllImport.EntryPoint</c> recorded in the method's <see cref="MethodImport"/> when present.
    /// </summary>
    private static string ResolvePInvokeEntryPointName(MetadataReader metadataReader, MethodDefinition methodDef)
    {
        try
        {
            var import = methodDef.GetImport();
            if (!import.Name.IsNil)
            {
                return metadataReader.GetString(import.Name);
            }
        }
        catch
        {
            // Fall through to the managed method name if import metadata is unavailable.
        }

        return metadataReader.GetString(methodDef.Name);
    }

    /// <summary>
    /// Analyzes the external type references used by the assembly for dangerous managed APIs.
    /// </summary>
    /// <remarks>
    /// This scans <see cref="MetadataReader.TypeReferences"/> — i.e. types the assembly <em>consumes</em>
    /// from other assemblies — rather than the types it defines. Matching is by exact type name so that
    /// referencing, for example, <c>System.Diagnostics.ProcessStartInfo</c> or
    /// <c>Microsoft.Win32.RegistryKey</c> is flagged, while a plugin that merely defines its own type whose
    /// name contains "Assembly" or "Reflection" is not.
    /// </remarks>
    private static void AnalyzeTypeReferences(MetadataReader metadataReader, AssemblyMetadataAnalysis analysis)
    {
        var dangerousReferencedTypes = new[]
        {
            "ProcessStartInfo", "Registry", "RegistryKey", "PowerShell", "ScriptBlock"
        };

        foreach (var typeRefHandle in metadataReader.TypeReferences)
        {
            var typeRef = metadataReader.GetTypeReference(typeRefHandle);
            var typeName = metadataReader.GetString(typeRef.Name);

            foreach (var dangerous in dangerousReferencedTypes)
            {
                if (string.Equals(typeName, dangerous, StringComparison.Ordinal))
                {
                    analysis.SuspiciousPatterns.Add($"Suspicious type reference: {typeName}");
                    break;
                }
            }
        }
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
        GC.SuppressFinalize(this);
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
