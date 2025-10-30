// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

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
    public bool IncludeSensitiveData { get; set; }


    /// <summary>
    /// Gets or sets the log retention period.
    /// </summary>
    public TimeSpan LogRetentionPeriod { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Gets or sets the minimum security level to log.
    /// </summary>
    public SecurityLevel MinimumLogLevel { get; set; } = SecurityLevel.Medium;

    /// <summary>
    /// Gets or sets whether critical security event alerts are enabled.
    /// </summary>
    public bool EnableCriticalEventAlerts { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include stack traces in log entries.
    /// </summary>
    public bool IncludeStackTraces { get; set; }


    /// <summary>
    /// Gets or sets whether correlation tracking is enabled.
    /// </summary>
    public bool EnableCorrelationTracking { get; set; } = true;
}

/// <summary>
/// Represents a security log entry with comprehensive audit information.
/// </summary>
public sealed class SecurityLogEntry
{
    /// <summary>
    /// Gets the unique identifier for this log entry.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

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
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets the export format used.
    /// </summary>
    public string Format { get; init; } = "JSON";

    /// <summary>
    /// Gets the size of the exported file in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }
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
    /// Gets the start time for this correlation context.
    /// </summary>
    public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the user ID associated with this correlation.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Gets the operation name that started this correlation.
    /// </summary>
    public string? OperationName { get; init; }

    /// <summary>
    /// Gets the number of events in this correlation.
    /// </summary>
    public int EventCount { get; set; }

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
/// Represents intended usage types for certificate validation operations.
/// </summary>
public enum CertificateUsage
{
    /// <summary>
    /// Certificate is intended for digital signature operations.
    /// </summary>
    DigitalSignature,

    /// <summary>
    /// Certificate is intended for data encryption operations.
    /// </summary>
    DataEncryption,

    /// <summary>
    /// Certificate is intended for key agreement operations.
    /// </summary>
    KeyAgreement,

    /// <summary>
    /// Certificate is intended for signing other certificates (CA usage).
    /// </summary>
    CertificateSigning,

    /// <summary>
    /// Certificate is intended for server authentication (TLS/SSL server).
    /// </summary>
    ServerAuthentication,

    /// <summary>
    /// Certificate is intended for client authentication (TLS/SSL client).
    /// </summary>
    ClientAuthentication,

    /// <summary>
    /// Certificate is intended for code signing operations.
    /// </summary>
    CodeSigning
}

/// <summary>
/// Represents a cached certificate validation result with timestamp for cache management.
/// </summary>
internal readonly struct CachedCertificateValidation
{
    /// <summary>
    /// Gets the cached validation result.
    /// </summary>
    public required CertificateValidationResult Result { get; init; }

    /// <summary>
    /// Gets the timestamp when the validation was performed and cached.
    /// </summary>
    public required DateTimeOffset ValidationTime { get; init; }
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
    Information,

    /// <summary>
    /// Security violation events - breaches of security policies or rules.
    /// </summary>
    SecurityViolation,

    /// <summary>
    /// Successful authentication events.
    /// </summary>
    AuthenticationSuccess,

    /// <summary>
    /// Failed authentication events.
    /// </summary>
    AuthenticationFailure,

    /// <summary>
    /// Access granted events.
    /// </summary>
    AccessGranted,

    /// <summary>
    /// Access denied events.
    /// </summary>
    AccessDenied,

    /// <summary>
    /// Data access events (read operations).
    /// </summary>
    DataAccess,

    /// <summary>
    /// Data modification events (create/update operations).
    /// </summary>
    DataModification,

    /// <summary>
    /// Data deletion events.
    /// </summary>
    DataDeletion
}

/// <summary>
/// Represents the result of a digital signature operation.
/// </summary>
public sealed class SignatureResult : ICryptographicResult
{
    /// <summary>
    /// Gets whether the signature operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets whether the operation was successful (ICryptographicResult implementation).
    /// </summary>
    public bool IsSuccessful
    {
        get => Success;
        set => throw new NotSupportedException("Use Success property instead");
    }

    /// <summary>
    /// Gets the generated signature bytes.
    /// </summary>
    public IReadOnlyList<byte> Signature { get; set; } = [];

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
    /// Gets the operation time (ICryptographicResult implementation).
    /// </summary>
    public DateTimeOffset OperationTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets any error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the error message (ICryptographicResult implementation).
    /// </summary>
    string? ICryptographicResult.ErrorMessage
    {
        get => ErrorMessage;
        set => throw new NotSupportedException("Use ErrorMessage property instead");
    }

    /// <summary>
    /// Gets additional metadata about the signature.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}

/// <summary>
/// Represents the result of a signature verification operation.
/// </summary>
public sealed class SignatureVerificationResult : ICryptographicResult
{
    /// <summary>
    /// Gets whether the signature verification was successful.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets whether the verification operation completed without errors.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets whether the verification operation completed without errors (alias for Success).
    /// </summary>
    public bool IsSuccessful
    {
        get => Success;
        set => throw new NotSupportedException("Use Success property instead");
    }

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
    /// Gets the operation time (ICryptographicResult implementation).
    /// </summary>
    public DateTimeOffset OperationTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets any error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the error message (ICryptographicResult implementation).
    /// </summary>
    string? ICryptographicResult.ErrorMessage
    {
        get => ErrorMessage;
        set => ErrorMessage = value;
    }

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
    /// Input validation failure.
    /// </summary>
    InputValidation,

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
    AuditTampering,

    /// <summary>
    /// Authentication bypass attempt.
    /// </summary>
    AuthenticationBypass,

    /// <summary>
    /// Privilege escalation attempt.
    /// </summary>
    PrivilegeEscalation,

    /// <summary>
    /// SQL injection attempt.
    /// </summary>
    SqlInjection,

    /// <summary>
    /// Cross-site scripting attempt.
    /// </summary>
    XssAttempt,

    /// <summary>
    /// Path traversal attack attempt.
    /// </summary>
    PathTraversal,

    /// <summary>
    /// Command injection attempt.
    /// </summary>
    CommandInjection,

    /// <summary>
    /// Data exfiltration attempt.
    /// </summary>
    DataExfiltration,

    /// <summary>
    /// Malicious code detected.
    /// </summary>
    MaliciousCode,

    /// <summary>
    /// Cryptographic operation failure.
    /// </summary>
    CryptographicFailure,

    /// <summary>
    /// Data integrity violation.
    /// </summary>
    IntegrityViolation,

    /// <summary>
    /// Buffer overflow attack attempt.
    /// </summary>
    BufferOverflow
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
    /// Gets a static instance representing granted access.
    /// </summary>
    public static AccessResult GrantedResult { get; } = new() { Granted = true, Reason = "Access granted" };

    /// <summary>
    /// Gets a static instance representing denied access.
    /// </summary>
    public static AccessResult DeniedResult { get; } = new() { Granted = false, Reason = "Access denied" };

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
    /// Data create operation.
    /// </summary>
    Create,

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
