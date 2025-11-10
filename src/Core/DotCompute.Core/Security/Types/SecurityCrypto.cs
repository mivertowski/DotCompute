// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Security;

namespace DotCompute.Core.Security.Types;

/// <summary>
/// Represents the result of a digital signature operation.
/// </summary>
/// <remarks>
/// <para>
/// Contains signature bytes, algorithm information, and metadata for verification.
/// Implements <see cref="ICryptographicResult"/> for standard result handling.
/// </para>
/// <para>
/// The signature can be verified using <see cref="SignatureVerificationResult"/>.
/// </para>
/// </remarks>
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
    /// <remarks>
    /// Common values: RSA-SHA256, ECDSA-SHA384, EdDSA-Ed25519.
    /// </remarks>
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
    /// <remarks>
    /// May include certificate chain, timestamp authority info, or custom properties.
    /// </remarks>
    public Dictionary<string, object> Metadata { get; init; } = [];
}

/// <summary>
/// Represents the result of a signature verification operation.
/// </summary>
/// <remarks>
/// <para>
/// Verifies digital signatures created by <see cref="SignatureResult"/>.
/// Includes trust level assessment and certificate validation.
/// </para>
/// </remarks>
public sealed class SignatureVerificationResult : ICryptographicResult
{
    /// <summary>
    /// Gets whether the signature verification was successful.
    /// </summary>
    /// <remarks>
    /// True indicates the signature is cryptographically valid and trusted.
    /// </remarks>
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
    /// <remarks>
    /// Common values: Trusted, Untrusted, Unknown, Revoked, Expired.
    /// </remarks>
    public string TrustLevel { get; init; } = "Unknown";
}
