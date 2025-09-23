// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using DotCompute.Abstractions.Security;

namespace DotCompute.Core.Security;

/// <summary>
/// Configuration for security logging operations.
/// </summary>
public sealed class SecurityLoggingConfiguration
{
    /// <summary>
    /// Gets or sets whether audit logging is enabled.
    /// </summary>
    public bool AuditEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of log entries to keep in memory.
    /// </summary>
    public int MaxLogEntries { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the log file path for security events.
    /// </summary>
    public string LogFilePath { get; set; } = "./logs/security.log";

    /// <summary>
    /// Gets or sets whether to include sensitive data in logs.
    /// </summary>
    public bool IncludeSensitiveData { get; set; } = false;

    /// <summary>
    /// Gets or sets the log retention period.
    /// </summary>
    public TimeSpan LogRetentionPeriod { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Gets or sets the minimum security level to log.
    /// </summary>
    public SecurityLevel MinimumLogLevel { get; set; } = SecurityLevel.Medium;
}

/// <summary>
/// Represents a security log entry with comprehensive audit information.
/// </summary>
public sealed class SecurityLogEntry
{
    /// <summary>
    /// Gets the unique sequence number for this log entry.
    /// </summary>
    public long SequenceNumber { get; init; }

    /// <summary>
    /// Gets the timestamp when this entry was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the type of security event.
    /// </summary>
    public SecurityEventType EventType { get; init; }

    /// <summary>
    /// Gets the security level of this event.
    /// </summary>
    public SecurityLevel Level { get; init; }

    /// <summary>
    /// Gets the event message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user ID associated with this event, if any.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Gets the resource ID associated with this event, if any.
    /// </summary>
    public string? ResourceId { get; init; }

    /// <summary>
    /// Gets the correlation ID for tracing related events.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets additional contextual data for this event.
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; init; } = [];

    /// <summary>
    /// Gets the caller member name where this event was logged.
    /// </summary>
    public string CallerName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the source file where this event was logged.
    /// </summary>
    public string SourceFile { get; init; } = string.Empty;

    /// <summary>
    /// Gets the line number where this event was logged.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// Gets the thread ID where this event was logged.
    /// </summary>
    public int ThreadId { get; init; } = Environment.CurrentManagedThreadId;
}

/// <summary>
/// Represents the result of an audit export operation.
/// </summary>
public sealed class AuditExportResult
{
    /// <summary>
    /// Gets whether the export operation was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the export file path if successful.
    /// </summary>
    public string? ExportFilePath { get; init; }

    /// <summary>
    /// Gets the number of entries exported.
    /// </summary>
    public int EntriesExported { get; init; }

    /// <summary>
    /// Gets the export timestamp.
    /// </summary>
    public DateTimeOffset ExportTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets any error message if the export failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the export format used.
    /// </summary>
    public string Format { get; init; } = "JSON";

    /// <summary>
    /// Gets the size of the exported file in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }
}

/// <summary>
/// Represents correlation context for related security events.
/// </summary>
public sealed class CorrelationContext
{
    /// <summary>
    /// Gets the correlation ID.
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Gets when this correlation context was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the user ID associated with this correlation.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Gets the operation name that started this correlation.
    /// </summary>
    public string? OperationName { get; init; }

    /// <summary>
    /// Gets additional context data.
    /// </summary>
    public Dictionary<string, object> Data { get; init; } = [];
}

/// <summary>
/// Types of cryptographic keys supported by the system.
/// </summary>
public enum KeyType
{
    /// <summary>
    /// Advanced Encryption Standard symmetric key.
    /// </summary>
    AES,

    /// <summary>
    /// RSA asymmetric key pair.
    /// </summary>
    RSA,

    /// <summary>
    /// Elliptic Curve Digital Signature Algorithm key.
    /// </summary>
    ECDSA,

    /// <summary>
    /// ChaCha20 symmetric key.
    /// </summary>
    ChaCha20
}

/// <summary>
/// Types of security events that can be logged.
/// </summary>
public enum SecurityEventType
{
    /// <summary>
    /// Authentication related events.
    /// </summary>
    Authentication,

    /// <summary>
    /// Authorization related events.
    /// </summary>
    Authorization,

    /// <summary>
    /// Encryption or decryption events.
    /// </summary>
    Cryptographic,

    /// <summary>
    /// Key management events.
    /// </summary>
    KeyManagement,

    /// <summary>
    /// Access control events.
    /// </summary>
    AccessControl,

    /// <summary>
    /// Security policy violations.
    /// </summary>
    PolicyViolation,

    /// <summary>
    /// Audit events.
    /// </summary>
    Audit,

    /// <summary>
    /// Configuration changes.
    /// </summary>
    Configuration,

    /// <summary>
    /// Security threats or attacks.
    /// </summary>
    Threat,

    /// <summary>
    /// General security information.
    /// </summary>
    Information
}

/// <summary>
/// Represents the result of a digital signature operation.
/// </summary>
public sealed class SignatureResult
{
    /// <summary>
    /// Gets whether the signature operation was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the generated signature bytes.
    /// </summary>
    public byte[] Signature { get; init; } = [];

    /// <summary>
    /// Gets the algorithm used for signing.
    /// </summary>
    public string Algorithm { get; init; } = string.Empty;

    /// <summary>
    /// Gets the key identifier used for signing.
    /// </summary>
    public string KeyId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the signature was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets any error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets additional metadata about the signature.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}

/// <summary>
/// Represents the result of a signature verification operation.
/// </summary>
public sealed class SignatureVerificationResult
{
    /// <summary>
    /// Gets whether the signature verification was successful.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets whether the verification operation completed without errors.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the algorithm used for verification.
    /// </summary>
    public string Algorithm { get; init; } = string.Empty;

    /// <summary>
    /// Gets the key identifier used for verification.
    /// </summary>
    public string KeyId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the verification was performed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets any error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets additional metadata about the verification.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];

    /// <summary>
    /// Gets the trust level of the signature.
    /// </summary>
    public string TrustLevel { get; init; } = "Unknown";
}

/// <summary>
/// Types of security policy violations.
/// </summary>
public enum SecurityViolationType
{
    /// <summary>
    /// Unauthorized access attempt.
    /// </summary>
    UnauthorizedAccess,

    /// <summary>
    /// Weak cryptographic algorithm usage.
    /// </summary>
    WeakCryptography,

    /// <summary>
    /// Excessive failed authentication attempts.
    /// </summary>
    BruteForceAttempt,

    /// <summary>
    /// Data integrity violation.
    /// </summary>
    DataIntegrityViolation,

    /// <summary>
    /// Policy configuration violation.
    /// </summary>
    PolicyViolation,

    /// <summary>
    /// Key management violation.
    /// </summary>
    KeyManagementViolation,

    /// <summary>
    /// Audit trail tampering attempt.
    /// </summary>
    AuditTampering
}

/// <summary>
/// Represents the result of an access control check.
/// </summary>
public sealed class AccessResult
{
    /// <summary>
    /// Gets whether access was granted.
    /// </summary>
    public bool Granted { get; init; }

    /// <summary>
    /// Gets the reason for the access decision.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user ID that requested access.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Gets the resource ID that was accessed.
    /// </summary>
    public string? ResourceId { get; init; }

    /// <summary>
    /// Gets the permission that was checked.
    /// </summary>
    public string Permission { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp of the access check.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Types of data operations for security logging.
/// </summary>
public enum DataOperation
{
    /// <summary>
    /// Data read operation.
    /// </summary>
    Read,

    /// <summary>
    /// Data write operation.
    /// </summary>
    Write,

    /// <summary>
    /// Data update operation.
    /// </summary>
    Update,

    /// <summary>
    /// Data delete operation.
    /// </summary>
    Delete,

    /// <summary>
    /// Data export operation.
    /// </summary>
    Export,

    /// <summary>
    /// Data import operation.
    /// </summary>
    Import,

    /// <summary>
    /// Data backup operation.
    /// </summary>
    Backup,

    /// <summary>
    /// Data restore operation.
    /// </summary>
    Restore
}