// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Plugins.Loaders.Internal;

/// <summary>
/// Result of Authenticode signature validation.
/// </summary>
internal class AuthenticodeResult
{
    /// <summary>
    /// Gets or sets a value indicating whether signed.
    /// </summary>
    /// <value>The is signed.</value>
    public bool IsSigned { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; set; }
    /// <summary>
    /// Gets or sets the publisher.
    /// </summary>
    /// <value>The publisher.</value>
    public string? Publisher { get; set; }
    /// <summary>
    /// Gets or sets the certificate thumbprint.
    /// </summary>
    /// <value>The certificate thumbprint.</value>
    public string? CertificateThumbprint { get; set; }
    /// <summary>
    /// Gets or sets the signing algorithm.
    /// </summary>
    /// <value>The signing algorithm.</value>
    public string? SigningAlgorithm { get; set; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; set; }
}