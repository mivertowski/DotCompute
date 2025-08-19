// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Types.Security
{

/// <summary>
/// Security levels for algorithm plugins and operations.
/// </summary>
public enum SecurityLevel
{
    /// <summary>
    /// Lowest security level - minimal restrictions.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium security level - standard restrictions.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High security level - strict restrictions.
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical security level - maximum restrictions.
    /// </summary>
    Critical = 3
}

/// <summary>
/// Threat levels for security scanning.
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
    /// </summary>
    Critical = 4
}

/// <summary>
/// Security zones for code access security.
/// </summary>
public enum SecurityZone
{
    /// <summary>
    /// Unknown or undefined zone.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Local machine zone - highest trust.
    /// </summary>
    LocalMachine = 1,

    /// <summary>
    /// Local intranet zone - high trust.
    /// </summary>
    LocalIntranet = 2,

    /// <summary>
    /// Trusted sites zone - medium trust.
    /// </summary>
    TrustedSites = 3,

    /// <summary>
    /// Internet zone - low trust.
    /// </summary>
    Internet = 4,

    /// <summary>
    /// Restricted sites zone - minimal trust.
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
    /// UI operations.
    /// </summary>
    UserInterface
}}
