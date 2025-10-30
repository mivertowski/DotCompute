// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using System.Security.Cryptography.X509Certificates;

namespace DotCompute.Abstractions.Security;

/// <summary>
/// Canonical security evaluation context for policy decisions across all security validations.
/// This is the single source of truth for security context used throughout DotCompute.
/// </summary>
/// <remarks>
/// This context consolidates security evaluation requirements from:
/// - Plugin validation and loading
/// - Assembly digital signature verification
/// - Malware scanning and threat detection
/// - Security policy enforcement
/// </remarks>
public sealed class SecurityEvaluationContext
{
    /// <summary>
    /// Gets or initializes the assembly file path being evaluated.
    /// </summary>
    /// <value>The absolute path to the assembly file.</value>
    public required string AssemblyPath { get; init; }

    /// <summary>
    /// Gets or initializes the assembly bytes for in-memory analysis.
    /// </summary>
    /// <value>The raw assembly bytes as immutable array, or default if not loaded into memory.</value>
    /// <remarks>CA1819: Using ImmutableArray to avoid exposing mutable array reference.</remarks>
    public ImmutableArray<byte> AssemblyBytes { get; init; }

    /// <summary>
    /// Gets or initializes the X.509 certificate extracted from the assembly's digital signature.
    /// </summary>
    /// <value>The certificate, or null if the assembly is not signed.</value>
    public X509Certificate2? Certificate { get; init; }

    /// <summary>
    /// Gets or initializes the strong name public key extracted from the assembly.
    /// </summary>
    /// <value>The strong name public key bytes as immutable array, or default if not strong-named.</value>
    /// <remarks>CA1819: Using ImmutableArray to avoid exposing mutable array reference.</remarks>
    public ImmutableArray<byte> StrongNameKey { get; init; }

    /// <summary>
    /// Gets additional metadata for custom security rule evaluation.
    /// </summary>
    /// <value>A dictionary of key-value pairs for extensibility.</value>
    /// <remarks>
    /// This allows security rules to store and retrieve custom evaluation data
    /// without modifying the core context structure.
    /// </remarks>
    public Dictionary<string, object> Metadata { get; init; } = [];
}
