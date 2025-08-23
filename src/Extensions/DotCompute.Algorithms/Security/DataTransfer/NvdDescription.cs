// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Security.DataTransfer;

/// <summary>
/// Data transfer object representing a description entry from the National Vulnerability Database.
/// Contains the textual description of a vulnerability in a specific language.
/// </summary>
internal sealed class NvdDescription
{
    /// <summary>
    /// Gets or sets the description text for the vulnerability.
    /// Contains the detailed explanation of the security issue, its impact, and potential exploitation methods.
    /// </summary>
    /// <value>The description text as a string, or null if not available.</value>
    public string? Value { get; set; }
}