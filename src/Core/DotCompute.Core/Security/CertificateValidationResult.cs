// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Security.Cryptography.X509Certificates;

namespace DotCompute.Core.Security;

/// <summary>
/// Represents the result of certificate validation, including validation status, errors, and warnings.
/// </summary>
public sealed class CertificateValidationResult
{
    /// <summary>
    /// Gets the certificate that was validated.
    /// </summary>
    /// <value>The certificate.</value>
    public required X509Certificate2 Certificate { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the certificate is valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets the time when validation was performed.
    /// </summary>
    /// <value>The validation time.</value>
    public DateTimeOffset ValidationTime { get; init; }

    /// <summary>
    /// Gets the collection of validation errors.
    /// </summary>
    /// <value>The errors.</value>
    public IList<string> Errors { get; init; } = [];

    /// <summary>
    /// Gets the collection of validation warnings.
    /// </summary>
    /// <value>The warnings.</value>
    public IList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Gets or sets the certificate chain validation information.
    /// </summary>
    /// <value>The chain info.</value>
    public CertificateChainValidationResult? ChainInfo { get; set; }
}
