// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Security.Types;

/// <summary>
/// Type of cryptographic key.
/// </summary>
/// <remarks>
/// Identifies key purpose and algorithm for cryptographic operations.
/// </remarks>
public enum KeyType
{
    /// <summary>Symmetric encryption key (AES, ChaCha20, etc.).</summary>
    Symmetric,

    /// <summary>AES encryption key.</summary>
    AES,

    /// <summary>ChaCha20 encryption key.</summary>
    ChaCha20,

    /// <summary>RSA public/private key pair.</summary>
    RSA,

    /// <summary>Elliptic Curve Cryptography key.</summary>
    ECC,

    /// <summary>ECDSA digital signature key.</summary>
    ECDSA,

    /// <summary>Diffie-Hellman key exchange key.</summary>
    DiffieHellman,

    /// <summary>Digital signature key (DSA, ECDSA).</summary>
    Signature,

    /// <summary>Message Authentication Code key (HMAC).</summary>
    MAC
}

/// <summary>
/// Certificate usage scenarios.
/// </summary>
/// <remarks>
/// Defines the intended purpose of a certificate to prevent misuse.
/// </remarks>
public enum CertificateUsage
{
    /// <summary>Code signing certificate.</summary>
    CodeSigning,

    /// <summary>TLS/SSL certificate for secure communication.</summary>
    TLS,

    /// <summary>Email encryption certificate (S/MIME).</summary>
    Email,

    /// <summary>Document signing certificate.</summary>
    DocumentSigning,

    /// <summary>Client authentication certificate.</summary>
    ClientAuth,

    /// <summary>Server authentication certificate.</summary>
    ServerAuth,

    /// <summary>Certificate authority certificate.</summary>
    CA,

    /// <summary>Timestamping certificate.</summary>
    Timestamping,

    /// <summary>Digital signature certificate.</summary>
    DigitalSignature,

    /// <summary>Data encryption certificate.</summary>
    DataEncryption,

    /// <summary>Key agreement certificate.</summary>
    KeyAgreement,

    /// <summary>Certificate signing certificate.</summary>
    CertificateSigning,

    /// <summary>Server authentication certificate.</summary>
    ServerAuthentication,

    /// <summary>Client authentication certificate.</summary>
    ClientAuthentication
}

/// <summary>
/// Types of security events that can be logged.
/// </summary>
/// <remarks>
/// Comprehensive taxonomy of security-relevant events for audit trails.
/// </remarks>
public enum SecurityEventType
{
    /// <summary>Authentication attempt (success or failure).</summary>
    Authentication,

    /// <summary>Successful authentication.</summary>
    AuthenticationSuccess,

    /// <summary>Failed authentication attempt.</summary>
    AuthenticationFailure,

    /// <summary>Authorization decision.</summary>
    Authorization,

    /// <summary>Access granted to resource.</summary>
    AccessGranted,

    /// <summary>Access denied to resource.</summary>
    AccessDenied,

    /// <summary>Access to sensitive resource.</summary>
    ResourceAccess,

    /// <summary>Data access operation.</summary>
    DataAccess,

    /// <summary>Data modification operation.</summary>
    DataModification,

    /// <summary>Data deletion operation.</summary>
    DataDeletion,

    /// <summary>Data encryption operation.</summary>
    Encryption,

    /// <summary>Data decryption operation.</summary>
    Decryption,

    /// <summary>Digital signature creation.</summary>
    Signing,

    /// <summary>Signature verification.</summary>
    Verification,

    /// <summary>Key generation.</summary>
    KeyGeneration,

    /// <summary>Key import or loading.</summary>
    KeyImport,

    /// <summary>Key export or extraction.</summary>
    KeyExport,

    /// <summary>Key rotation or renewal.</summary>
    KeyRotation,

    /// <summary>Key destruction or deletion.</summary>
    KeyDestruction,

    /// <summary>Certificate issuance.</summary>
    CertificateIssued,

    /// <summary>Certificate revocation.</summary>
    CertificateRevoked,

    /// <summary>Certificate validation.</summary>
    CertificateValidation,

    /// <summary>Security policy change.</summary>
    PolicyChange,

    /// <summary>Security configuration change.</summary>
    ConfigurationChange,

    /// <summary>Security violation or attempted breach.</summary>
    SecurityViolation,

    /// <summary>Audit log access.</summary>
    AuditAccess,

    /// <summary>Security scan or assessment.</summary>
    SecurityScan
}

/// <summary>
/// Types of security violations.
/// </summary>
/// <remarks>
/// Classifies security violations for appropriate response and reporting.
/// </remarks>
public enum SecurityViolationType
{
    /// <summary>Invalid authentication credentials.</summary>
    InvalidCredentials,

    /// <summary>Expired authentication token.</summary>
    ExpiredToken,

    /// <summary>Insufficient permissions for operation.</summary>
    InsufficientPermissions,

    /// <summary>Access to prohibited resource.</summary>
    UnauthorizedAccess,

    /// <summary>Invalid signature.</summary>
    InvalidSignature,

    /// <summary>Expired certificate.</summary>
    ExpiredCertificate,

    /// <summary>Revoked certificate.</summary>
    RevokedCertificate,

    /// <summary>Untrusted certificate chain.</summary>
    UntrustedCertificate,

    /// <summary>Cryptographic operation failure.</summary>
    CryptographicFailure,

    /// <summary>Rate limit exceeded.</summary>
    RateLimitExceeded,

    /// <summary>Suspicious activity detected.</summary>
    SuspiciousActivity,

    /// <summary>Security policy violation.</summary>
    PolicyViolation,

    /// <summary>Data integrity check failed.</summary>
    IntegrityViolation,

    /// <summary>Malformed security data.</summary>
    MalformedData,

    /// <summary>Input validation failure.</summary>
    InputValidation,

    /// <summary>Authentication bypass attempt.</summary>
    AuthenticationBypass,

    /// <summary>Privilege escalation attempt.</summary>
    PrivilegeEscalation,

    /// <summary>SQL injection attack.</summary>
    SqlInjection,

    /// <summary>Cross-site scripting (XSS) attack.</summary>
    XssAttempt,

    /// <summary>Path traversal attack.</summary>
    PathTraversal,

    /// <summary>Command injection attack.</summary>
    CommandInjection,

    /// <summary>Data exfiltration attempt.</summary>
    DataExfiltration,

    /// <summary>Malicious code execution attempt.</summary>
    MaliciousCode,

    /// <summary>Buffer overflow attempt.</summary>
    BufferOverflow
}

/// <summary>
/// Data operations for access control.
/// </summary>
/// <remarks>
/// Granular operations that can be controlled via access policies.
/// </remarks>
public enum DataOperation
{
    /// <summary>Read data.</summary>
    Read,

    /// <summary>Write or create data.</summary>
    Write,

    /// <summary>Create new data.</summary>
    Create,

    /// <summary>Update existing data.</summary>
    Update,

    /// <summary>Delete data.</summary>
    Delete,

    /// <summary>Execute code or operation.</summary>
    Execute,

    /// <summary>List or enumerate data.</summary>
    List,

    /// <summary>Search or query data.</summary>
    Search,

    /// <summary>Export data.</summary>
    Export,

    /// <summary>Import data.</summary>
    Import,

    /// <summary>Share or grant access to data.</summary>
    Share
}
