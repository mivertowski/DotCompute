// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Security.Types;

/// <summary>
/// Sanitization strategy types for different input contexts.
/// </summary>
/// <remarks>
/// Each type applies context-specific validation rules and sanitization patterns
/// optimized for common attack vectors in that domain.
/// </remarks>
public enum SanitizationType
{
    /// <summary>General purpose sanitization for uncategorized input.</summary>
    General,

    /// <summary>HTML content sanitization preventing XSS attacks.</summary>
    Html,

    /// <summary>SQL query parameter sanitization preventing SQL injection.</summary>
    Sql,

    /// <summary>File path sanitization preventing path traversal attacks.</summary>
    FilePath,

    /// <summary>URL sanitization ensuring valid and safe URLs.</summary>
    Url,

    /// <summary>Email address sanitization with format validation.</summary>
    Email,

    /// <summary>Alphanumeric character sanitization (letters and numbers only).</summary>
    AlphaNumeric,

    /// <summary>Numeric value sanitization (digits and decimal points only).</summary>
    Numeric,

    /// <summary>Kernel parameter sanitization for GPU compute operations.</summary>
    /// <remarks>
    /// Validates work group sizes, memory ranges, and execution parameters.
    /// </remarks>
    KernelParameter
}

/// <summary>
/// Types of security threats detected during input validation.
/// </summary>
/// <remarks>
/// <para>
/// Comprehensive classification of common injection attacks and malicious patterns.
/// Used by <see cref="InputSanitizer"/> to categorize detected threats.
/// </para>
/// </remarks>
public enum ThreatType
{
    /// <summary>SQL injection attack attempt detected.</summary>
    SqlInjection,

    /// <summary>Cross-site scripting (XSS) injection detected.</summary>
    XssInjection,

    /// <summary>Command injection attack attempt detected.</summary>
    CommandInjection,

    /// <summary>Path traversal attack attempt (e.g., "../../../") detected.</summary>
    PathTraversal,

    /// <summary>LDAP injection attack attempt detected.</summary>
    LdapInjection,

    /// <summary>Code injection attack attempt detected.</summary>
    CodeInjection,

    /// <summary>XML injection attack attempt detected.</summary>
    XmlInjection,

    /// <summary>NoSQL injection attack attempt detected.</summary>
    NoSqlInjection,

    /// <summary>Buffer overflow attack attempt detected.</summary>
    BufferOverflow,

    /// <summary>Null byte injection attempt detected.</summary>
    NullByteInjection,

    /// <summary>Control character injection detected.</summary>
    ControlCharacters,

    /// <summary>Excessive input length exceeding limits.</summary>
    ExcessiveLength,

    /// <summary>Invalid or disallowed file type detected.</summary>
    InvalidFileType,

    /// <summary>Suspicious file name pattern detected.</summary>
    SuspiciousFileName,

    /// <summary>Sanitization process failed to complete safely.</summary>
    SanitizationFailure,

    /// <summary>Processing error during validation.</summary>
    ProcessingError,

    /// <summary>General malicious input pattern detected.</summary>
    GeneralMalicious
}

/// <summary>
/// Severity levels for detected security threats.
/// </summary>
/// <remarks>
/// <para>
/// Numeric values allow threshold comparisons and severity-based filtering.
/// Higher values indicate more severe threats requiring immediate action.
/// </para>
/// </remarks>
public enum ThreatSeverity
{
    /// <summary>No threat detected - input is safe.</summary>
    None = 0,

    /// <summary>Low severity - minor policy violation or suspicious pattern.</summary>
    /// <remarks>
    /// May log for monitoring but typically allows processing to continue.
    /// </remarks>
    Low = 1,

    /// <summary>Medium severity - potential security concern requiring attention.</summary>
    /// <remarks>
    /// Should be logged and may trigger additional validation or user notification.
    /// </remarks>
    Medium = 2,

    /// <summary>High severity - likely attack attempt requiring defensive action.</summary>
    /// <remarks>
    /// Should block the operation and alert security monitoring systems.
    /// </remarks>
    High = 3,

    /// <summary>Critical severity - definite attack requiring immediate response.</summary>
    /// <remarks>
    /// Must block operation, log with full context, and trigger incident response.
    /// </remarks>
    Critical = 4
}
