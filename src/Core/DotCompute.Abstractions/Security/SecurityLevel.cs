// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Security;

/// <summary>
/// Security levels for algorithm plugins, operations, and compute environments.
/// This is the canonical SecurityLevel enum used across all projects.
/// </summary>
public enum SecurityLevel
{
    /// <summary>
    /// No security restrictions - minimal validation.
    /// Use only in trusted environments.
    /// </summary>
    None = 0,

    /// <summary>
    /// Basic security level - minimal restrictions and validation.
    /// Suitable for development and testing environments.
    /// </summary>
    Basic = 1,

    /// <summary>
    /// Low security level - light restrictions.
    /// Basic input validation and simple checks.
    /// </summary>
    Low = 2,

    /// <summary>
    /// Standard security level - moderate restrictions.
    /// Standard validation, authentication checks, and resource limits.
    /// Recommended for most production scenarios.
    /// </summary>
    Standard = 3,

    /// <summary>
    /// Medium security level - enhanced restrictions.
    /// Stricter validation and additional security checks.
    /// </summary>
    Medium = 4,

    /// <summary>
    /// High security level - strict restrictions.
    /// Comprehensive validation, audit logging, and enhanced monitoring.
    /// </summary>
    High = 5,

    /// <summary>
    /// Maximum security level - maximum restrictions.
    /// Highest level of validation, monitoring, and access control.
    /// Use for sensitive or critical operations.
    /// </summary>
    Maximum = 6,

    /// <summary>
    /// Critical security level - ultra-strict restrictions.
    /// Most restrictive security mode with comprehensive auditing.
    /// </summary>
    Critical = 7
}

/// <summary>
/// Threat levels for security scanning and monitoring.
/// </summary>
public enum ThreatLevel
{
    /// <summary>
    /// No threat detected.
    /// </summary>
    None = 0,

    /// <summary>
    /// Low threat level - minor concerns.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Medium threat level - moderate concerns.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// High threat level - significant concerns.
    /// </summary>
    High = 3,

    /// <summary>
    /// Critical threat level - severe security risk.
    /// Immediate action required.
    /// </summary>
    Critical = 4
}

/// <summary>
/// Security zones for code access security and trust levels.
/// </summary>
public enum SecurityZone
{
    /// <summary>
    /// Unknown or undefined zone.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Local machine zone - highest trust.
    /// Code running from the local machine.
    /// </summary>
    LocalMachine = 1,

    /// <summary>
    /// Local intranet zone - high trust.
    /// Code from the local network or intranet.
    /// </summary>
    LocalIntranet = 2,

    /// <summary>
    /// Trusted sites zone - medium trust.
    /// Code from explicitly trusted remote locations.
    /// </summary>
    TrustedSites = 3,

    /// <summary>
    /// Internet zone - low trust.
    /// Code from unknown internet locations.
    /// </summary>
    Internet = 4,

    /// <summary>
    /// Restricted sites zone - minimal trust.
    /// Code from explicitly restricted locations.
    /// </summary>
    RestrictedSites = 5
}

/// <summary>
/// Security operations that can be permitted or denied.
/// </summary>
public enum SecurityOperation
{
    /// <summary>
    /// File system read operations.
    /// </summary>
    FileRead,

    /// <summary>
    /// File system write operations.
    /// </summary>
    FileWrite,

    /// <summary>
    /// Network access operations.
    /// </summary>
    NetworkAccess,

    /// <summary>
    /// Reflection access operations.
    /// </summary>
    ReflectionAccess,

    /// <summary>
    /// Unmanaged code execution.
    /// </summary>
    UnmanagedCode,

    /// <summary>
    /// Registry access operations.
    /// </summary>
    RegistryAccess,

    /// <summary>
    /// Environment variable access.
    /// </summary>
    EnvironmentAccess,

    /// <summary>
    /// User interface operations.
    /// </summary>
    UserInterface,

    /// <summary>
    /// GPU kernel execution.
    /// </summary>
    GpuKernelExecution,

    /// <summary>
    /// Memory allocation and management.
    /// </summary>
    MemoryManagement,

    /// <summary>
    /// System configuration changes.
    /// </summary>
    SystemConfiguration
}

/// <summary>
/// Extension methods for SecurityLevel.
/// </summary>
public static class SecurityLevelExtensions
{
    /// <summary>
    /// Gets a human-readable description of the security level.
    /// </summary>
    /// <param name="level">The security level.</param>
    /// <returns>A descriptive string.</returns>
    public static string GetDescription(this SecurityLevel level)
    {
        return level switch
        {
            SecurityLevel.None => "No security restrictions",
            SecurityLevel.Basic => "Basic security - minimal validation",
            SecurityLevel.Low => "Low security - light restrictions",
            SecurityLevel.Standard => "Standard security - moderate restrictions",
            SecurityLevel.Medium => "Medium security - enhanced restrictions",
            SecurityLevel.High => "High security - strict restrictions",
            SecurityLevel.Maximum => "Maximum security - comprehensive protection",
            SecurityLevel.Critical => "Critical security - ultra-strict restrictions",
            _ => "Unknown security level"
        };
    }

    /// <summary>
    /// Gets the numeric weight for security enforcement.
    /// Higher values indicate stricter security.
    /// </summary>
    /// <param name="level">The security level.</param>
    /// <returns>The numeric weight (0-100).</returns>
    public static int GetWeight(this SecurityLevel level)
    {
        return level switch
        {
            SecurityLevel.None => 0,
            SecurityLevel.Basic => 15,
            SecurityLevel.Low => 25,
            SecurityLevel.Standard => 50,
            SecurityLevel.Medium => 65,
            SecurityLevel.High => 80,
            SecurityLevel.Maximum => 95,
            SecurityLevel.Critical => 100,
            _ => 50 // Default to standard
        };
    }

    /// <summary>
    /// Determines if this security level is higher than another level.
    /// </summary>
    /// <param name="level">The current security level.</param>
    /// <param name="other">The other security level to compare against.</param>
    /// <returns>True if this level is higher than the other.</returns>
    public static bool IsHigherThan(this SecurityLevel level, SecurityLevel other)
    {
        return level > other;
    }

    /// <summary>
    /// Determines if this security level requires auditing.
    /// </summary>
    /// <param name="level">The security level.</param>
    /// <returns>True if auditing is required.</returns>
    public static bool RequiresAuditing(this SecurityLevel level)
    {
        return level >= SecurityLevel.High;
    }

    /// <summary>
    /// Determines if this security level allows the specified operation.
    /// </summary>
    /// <param name="level">The security level.</param>
    /// <param name="operation">The operation to check.</param>
    /// <returns>True if the operation is allowed.</returns>
    public static bool AllowsOperation(this SecurityLevel level, SecurityOperation operation)
    {
        return level switch
        {
            SecurityLevel.None => true,
            SecurityLevel.Basic => operation != SecurityOperation.UnmanagedCode,
            SecurityLevel.Low => operation is not (SecurityOperation.UnmanagedCode or SecurityOperation.SystemConfiguration),
            SecurityLevel.Standard => operation is not (SecurityOperation.UnmanagedCode or SecurityOperation.SystemConfiguration or SecurityOperation.RegistryAccess),
            SecurityLevel.Medium => operation is SecurityOperation.FileRead or SecurityOperation.MemoryManagement or SecurityOperation.GpuKernelExecution,
            SecurityLevel.High => operation is SecurityOperation.MemoryManagement or SecurityOperation.GpuKernelExecution,
            SecurityLevel.Maximum => operation is SecurityOperation.GpuKernelExecution,
            SecurityLevel.Critical => false,
            _ => false
        };
    }
}