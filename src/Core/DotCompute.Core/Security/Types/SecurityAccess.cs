// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Security.Types;

/// <summary>
/// Represents the result of an access control decision.
/// </summary>
/// <remarks>
/// <para>
/// Contains access decision (granted/denied), reason, and audit information.
/// Used by access control systems to record authorization decisions.
/// </para>
/// <para>
/// Implements immutable value semantics with init-only properties for security auditing.
/// </para>
/// </remarks>
public sealed class AccessResult
{
    /// <summary>
    /// Gets whether access was granted.
    /// </summary>
    public bool Granted { get; init; }

    /// <summary>
    /// Gets a static instance representing granted access.
    /// </summary>
    /// <remarks>
    /// Use this singleton for common granted access scenarios to reduce allocations.
    /// </remarks>
    public static AccessResult GrantedResult { get; } = new() { Granted = true, Reason = "Access granted" };

    /// <summary>
    /// Gets a static instance representing denied access.
    /// </summary>
    /// <remarks>
    /// Use this singleton for common denied access scenarios to reduce allocations.
    /// </remarks>
    public static AccessResult DeniedResult { get; } = new() { Granted = false, Reason = "Access denied" };

    /// <summary>
    /// Gets the reason for the access decision.
    /// </summary>
    /// <remarks>
    /// Provides human-readable explanation for audit trails and user feedback.
    /// Examples: "Access granted", "Insufficient permissions", "Resource not found".
    /// </remarks>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user ID that requested access.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Gets the resource ID that was accessed.
    /// </summary>
    public string? ResourceId { get; init; }

    /// <summary>
    /// Gets the permission that was checked.
    /// </summary>
    /// <remarks>
    /// Examples: "read", "write", "execute", "admin".
    /// </remarks>
    public string Permission { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp of the access check.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
