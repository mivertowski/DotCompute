// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Security.Cryptography.X509Certificates;

namespace DotCompute.Core.Security;

/// <summary>
/// Represents information about a single element in a certificate chain.
/// </summary>
public sealed class ChainElementInfo
{
    /// <summary>
    /// Gets the certificate in the chain element.
    /// </summary>
    /// <value>The certificate.</value>
    public required X509Certificate2 Certificate { get; init; }

    /// <summary>
    /// Gets the status information for this chain element.
    /// </summary>
    /// <value>The status.</value>
    public required IReadOnlyList<X509ChainStatus> Status { get; init; }
}
