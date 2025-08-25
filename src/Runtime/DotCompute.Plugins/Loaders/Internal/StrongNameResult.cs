// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Plugins.Loaders.Internal;

/// <summary>
/// Result of strong name validation.
/// </summary>
internal class StrongNameResult
{
    public bool IsValid { get; set; }
    public string? PublicKeyToken { get; set; }
    public string? ErrorMessage { get; set; }
}