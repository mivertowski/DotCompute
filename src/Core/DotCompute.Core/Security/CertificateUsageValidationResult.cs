// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Security.Cryptography.X509Certificates;

namespace DotCompute.Core.Security;

/// <summary>
/// Represents the result of certificate usage validation for specific cryptographic purposes.
/// </summary>
public sealed class CertificateUsageValidationResult
{
    /// <summary>
    /// Gets the certificate that was validated.
    /// </summary>
    /// <value>The certificate.</value>
    public required X509Certificate2 Certificate { get; init; }

    /// <summary>
    /// Gets the intended usage for which the certificate was validated.
    /// </summary>
    /// <value>The intended usage.</value>
    public required CertificateUsage IntendedUsage { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the certificate is valid for the intended usage.
    /// </summary>
    /// <value>The is valid for usage.</value>
    public bool IsValidForUsage { get; set; }

    /// <summary>
    /// Gets the time when validation was performed.
    /// </summary>
    /// <value>The validation time.</value>
    public DateTimeOffset ValidationTime { get; init; }

    /// <summary>
    /// Gets the collection of validation issues identified during usage validation.
    /// </summary>
    /// <value>The issues.</value>
    public IList<string> Issues { get; init; } = [];
}
