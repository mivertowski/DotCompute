// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// Import organized security types from subdirectories
global using DotCompute.Core.Security.Types;

using DotCompute.Abstractions.Security;

namespace DotCompute.Core.Security;

/// <summary>
/// Backward compatibility facade for security types.
/// </summary>
/// <remarks>
/// <para>
/// This file provides a single namespace for all security types. Types have been
/// organized into focused files for maintainability:
/// </para>
/// <list type="bullet">
/// <item><see cref="Types.SecurityEnums"/> - All security-related enumerations</item>
/// <item><see cref="Types.SecurityLogging"/> - Logging, audit, and correlation types</item>
/// <item><see cref="Types.SecurityCrypto"/> - Cryptographic operation result types</item>
/// <item><see cref="Types.SecurityAccess"/> - Access control result types</item>
/// </list>
/// <para>
/// The global using directive ensures all types remain accessible in the
/// DotCompute.Core.Security namespace without breaking existing code.
/// </para>
/// </remarks>

/// <summary>
/// Represents a cached certificate validation result with timestamp for cache management.
/// </summary>
/// <remarks>
/// Internal type for certificate validation caching. Stored in concurrent dictionary
/// with thumbprint as key for O(1) lookup performance.
/// </remarks>
internal readonly struct CachedCertificateValidation
{
    /// <summary>
    /// Gets the cached validation result.
    /// </summary>
    public required CertificateValidationResult Result { get; init; }

    /// <summary>
    /// Gets the timestamp when the validation was performed and cached.
    /// </summary>
    /// <remarks>
    /// Used for cache invalidation based on TTL (typically 5-60 minutes).
    /// </remarks>
    public required DateTimeOffset ValidationTime { get; init; }
}
