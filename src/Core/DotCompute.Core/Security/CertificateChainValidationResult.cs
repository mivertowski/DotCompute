// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Security.Cryptography.X509Certificates;

namespace DotCompute.Core.Security;

/// <summary>
/// Represents the result of certificate chain validation up to a trusted root.
/// </summary>
public sealed class CertificateChainValidationResult
{
    /// <summary>
    /// Gets the certificate whose chain was validated.
    /// </summary>
    /// <value>The certificate.</value>
    public required X509Certificate2 Certificate { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the certificate chain is valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets the time when validation was performed.
    /// </summary>
    /// <value>The validation time.</value>
    public DateTimeOffset ValidationTime { get; init; }

    /// <summary>
    /// Gets the collection of chain status messages.
    /// </summary>
    /// <value>The chain status.</value>
    public IList<string> ChainStatus { get; init; } = [];

    /// <summary>
    /// Gets the collection of certificate chain elements, from leaf to root.
    /// </summary>
    /// <value>The chain elements.</value>
    public IList<ChainElementInfo> ChainElements { get; init; } = [];
}
