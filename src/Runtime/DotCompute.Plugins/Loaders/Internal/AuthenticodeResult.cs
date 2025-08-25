// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Plugins.Loaders.Internal;

/// <summary>
/// Result of Authenticode signature validation.
/// </summary>
internal class AuthenticodeResult
{
    public bool IsSigned { get; set; }
    public bool IsValid { get; set; }
    public string? Publisher { get; set; }
    public string? CertificateThumbprint { get; set; }
    public string? SigningAlgorithm { get; set; }
    public string? ErrorMessage { get; set; }
}