// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Plugins.Loaders.Internal;

/// <summary>
/// Result of strong name validation.
/// </summary>
internal class StrongNameResult
{
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; set; }
    /// <summary>
    /// Gets or sets the public key token.
    /// </summary>
    /// <value>The public key token.</value>
    public string? PublicKeyToken { get; set; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; set; }
}
