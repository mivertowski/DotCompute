// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Security;

/// <summary>
/// Canonical trust levels for digital signature validation and code signing.
/// This is the single source of truth for trust level evaluation across DotCompute.
/// </summary>
/// <remarks>
/// Trust levels are assigned based on digital signature validation, certificate chain verification,
/// and publisher trust configuration. Used primarily by Authenticode validation and assembly loading.
/// Trust levels are comparable using standard comparison operators (&lt;, &gt;, &lt;=, &gt;=).
/// </remarks>
public enum TrustLevel : int
{
    /// <summary>
    /// No trust established - unsigned assembly or invalid signature.
    /// </summary>
    /// <remarks>
    /// Indicates the assembly has no digital signature, the signature is invalid,
    /// or the certificate chain could not be verified.
    /// </remarks>
    None = 0,

    /// <summary>
    /// Unknown trust level - signature status could not be determined.
    /// </summary>
    /// <remarks>
    /// Returned when signature validation encounters an error or cannot complete.
    /// Treat as untrusted for security purposes.
    /// </remarks>
    Unknown = 1,

    /// <summary>
    /// Untrusted - signed but certificate is explicitly untrusted.
    /// </summary>
    /// <remarks>
    /// The assembly is signed, but the signing certificate is explicitly marked as untrusted
    /// in system or application trust stores.
    /// </remarks>
    Untrusted = 2,

    /// <summary>
    /// Low trust - signed but with certificate validation issues.
    /// </summary>
    /// <remarks>
    /// The assembly has a valid signature, but the certificate has issues such as:
    /// - Certificate is expired or not yet valid
    /// - Certificate chain is incomplete
    /// - Certificate is self-signed
    /// </remarks>
    Low = 3,

    /// <summary>
    /// Partially trusted - signed with valid certificate from unknown publisher.
    /// </summary>
    /// <remarks>
    /// The assembly is properly signed with a valid certificate chain,
    /// but the publisher is not in the trusted publisher list.
    /// </remarks>
    PartiallyTrusted = 4,

    /// <summary>
    /// Medium trust - signed with valid certificate and complete chain.
    /// </summary>
    /// <remarks>
    /// The assembly has a valid signature with a complete, verified certificate chain.
    /// The certificate is issued by a recognized certificate authority.
    /// </remarks>
    Medium = 5,

    /// <summary>
    /// High trust - signed by explicitly trusted publisher.
    /// </summary>
    /// <remarks>
    /// The assembly is signed by a publisher whose certificate is in the system
    /// or application trusted publisher store. This is the highest trust level.
    /// </remarks>
    High = 6
}

/// <summary>
/// Extension methods for TrustLevel.
/// </summary>
public static class TrustLevelExtensions
{
    /// <summary>
    /// Gets a human-readable description of the trust level.
    /// </summary>
    /// <param name="level">The trust level.</param>
    /// <returns>A descriptive string.</returns>
    public static string GetDescription(this TrustLevel level)
    {
        return level switch
        {
            TrustLevel.None => "No trust - unsigned or invalid signature",
            TrustLevel.Unknown => "Unknown trust level",
            TrustLevel.Untrusted => "Explicitly untrusted",
            TrustLevel.Low => "Low trust - certificate issues",
            TrustLevel.PartiallyTrusted => "Partially trusted - unknown publisher",
            TrustLevel.Medium => "Medium trust - valid signature",
            TrustLevel.High => "High trust - trusted publisher",
            _ => "Unknown trust level"
        };
    }

    /// <summary>
    /// Determines if this trust level is sufficient for the specified minimum level.
    /// </summary>
    /// <param name="level">The current trust level.</param>
    /// <param name="minimumRequired">The minimum required trust level.</param>
    /// <returns>True if this level meets or exceeds the minimum.</returns>
    public static bool MeetsMinimum(this TrustLevel level, TrustLevel minimumRequired)
        => level >= minimumRequired;

    /// <summary>
    /// Determines if this trust level should block assembly loading.
    /// </summary>
    /// <param name="level">The trust level.</param>
    /// <returns>True if the assembly should be blocked from loading.</returns>
    public static bool ShouldBlockLoading(this TrustLevel level)
        => level is TrustLevel.None or TrustLevel.Untrusted;

    /// <summary>
    /// Determines if this trust level requires user confirmation before loading.
    /// </summary>
    /// <param name="level">The trust level.</param>
    /// <returns>True if user confirmation should be requested.</returns>
    public static bool RequiresUserConfirmation(this TrustLevel level)
        => level is TrustLevel.Unknown or TrustLevel.Low or TrustLevel.PartiallyTrusted;

    /// <summary>
    /// Gets the numeric weight for trust enforcement.
    /// Higher values indicate greater trust.
    /// </summary>
    /// <param name="level">The trust level.</param>
    /// <returns>The numeric weight (0-100).</returns>
    public static int GetWeight(this TrustLevel level)
    {
        return level switch
        {
            TrustLevel.None => 0,
            TrustLevel.Unknown => 10,
            TrustLevel.Untrusted => 15,
            TrustLevel.Low => 30,
            TrustLevel.PartiallyTrusted => 50,
            TrustLevel.Medium => 75,
            TrustLevel.High => 100,
            _ => 0
        };
    }

    /// <summary>
    /// Compares two trust levels for ordering.
    /// </summary>
    /// <param name="left">The first trust level to compare.</param>
    /// <param name="right">The second trust level to compare.</param>
    /// <returns>True if left is less than right.</returns>
    /// <remarks>
    /// Since TrustLevel is an enum with sequential integer values, direct enum comparison is supported.
    /// Use standard comparison operators: trustLevel1 &lt; trustLevel2, trustLevel1 &gt; trustLevel2, etc.
    /// </remarks>
    public static bool IsLessThan(this TrustLevel left, TrustLevel right) => (int)left < (int)right;

    /// <summary>
    /// Compares two trust levels for ordering.
    /// </summary>
    /// <param name="left">The first trust level to compare.</param>
    /// <param name="right">The second trust level to compare.</param>
    /// <returns>True if left is greater than right.</returns>
    public static bool IsGreaterThan(this TrustLevel left, TrustLevel right) => (int)left > (int)right;

    /// <summary>
    /// Compares two trust levels for ordering.
    /// </summary>
    /// <param name="left">The first trust level to compare.</param>
    /// <param name="right">The second trust level to compare.</param>
    /// <returns>True if left is less than or equal to right.</returns>
    public static bool IsLessThanOrEqual(this TrustLevel left, TrustLevel right) => (int)left <= (int)right;

    /// <summary>
    /// Compares two trust levels for ordering.
    /// </summary>
    /// <param name="left">The first trust level to compare.</param>
    /// <param name="right">The second trust level to compare.</param>
    /// <returns>True if left is greater than or equal to right.</returns>
    public static bool IsGreaterThanOrEqual(this TrustLevel left, TrustLevel right) => (int)left >= (int)right;
}
