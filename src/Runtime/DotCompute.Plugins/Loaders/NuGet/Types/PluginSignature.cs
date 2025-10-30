// <copyright file="PluginSignature.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Loaders.NuGet.Types;

/// <summary>
/// Digital signature information for a plugin.
/// Provides security verification data for trusted plugin loading.
/// </summary>
public class PluginSignature
{
    /// <summary>
    /// Gets or sets whether the plugin is signed.
    /// Indicates if the plugin has a digital signature.
    /// </summary>
    public bool IsSigned { get; set; }

    /// <summary>
    /// Gets or sets the signer certificate thumbprint.
    /// Unique identifier of the signing certificate.
    /// </summary>
    public string? CertificateThumbprint { get; set; }

    /// <summary>
    /// Gets or sets the signing algorithm used.
    /// Cryptographic algorithm used for signature generation.
    /// </summary>
    public string? SigningAlgorithm { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of signing.
    /// When the plugin was digitally signed.
    /// </summary>
    public DateTimeOffset? SignedAt { get; set; }

    /// <summary>
    /// Gets or sets the trusted publisher information.
    /// Name of the organization or individual who signed the plugin.
    /// </summary>
    public string? Publisher { get; set; }

    /// <summary>
    /// Gets or sets whether the signature is valid.
    /// Result of signature verification.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets whether the plugin has a strong name.
    /// Indicates if the assembly is strong-named.
    /// </summary>
    public bool HasStrongName { get; set; }

    /// <summary>
    /// Gets or sets validation errors.
    /// Any errors encountered during signature validation.
    /// </summary>
    public IList<string> ValidationErrors { get; init; } = [];
}
