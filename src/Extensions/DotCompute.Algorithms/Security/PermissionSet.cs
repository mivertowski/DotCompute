
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Frozen;
using DotCompute.Abstractions.Security;

namespace DotCompute.Algorithms.Security;

/// <summary>
/// Modern replacement for .NET Framework's Code Access Security PermissionSet.
/// Represents a collection of security permissions using contemporary .NET security patterns.
/// </summary>
public sealed class PermissionSet
{
    private readonly HashSet<string> _grantedPermissions = [];
    private readonly Dictionary<string, object> _permissionData = [];

    /// <summary>
    /// Gets the security zone associated with this permission set.
    /// </summary>
    public SecurityZone Zone { get; init; } = SecurityZone.Internet;

    /// <summary>
    /// Gets the security level for this permission set.
    /// </summary>
    public SecurityLevel Level { get; init; } = SecurityLevel.Low;

    /// <summary>
    /// Gets whether this is an unrestricted permission set.
    /// </summary>
    public bool IsUnrestricted { get; init; }

    /// <summary>
    /// Gets the read-only collection of granted permission names.
    /// </summary>
    public FrozenSet<string> GrantedPermissions => _grantedPermissions.ToFrozenSet();

    /// <summary>
    /// Adds a permission to this set.
    /// </summary>
    /// <param name="permissionName">Name of the permission (e.g., "FileIO.Read").</param>
    /// <param name="permissionData">Optional permission-specific data.</param>
    public void AddPermission(string permissionName, object? permissionData = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);
        _ = _grantedPermissions.Add(permissionName);
        if (permissionData != null)
        {
            _permissionData[permissionName] = permissionData;
        }
    }

    /// <summary>
    /// Checks if a specific permission is granted.
    /// </summary>
    /// <param name="permissionName">Name of the permission to check.</param>
    /// <returns>True if granted, false otherwise.</returns>
    public bool HasPermission(string permissionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);
        return IsUnrestricted || _grantedPermissions.Contains(permissionName);
    }

    /// <summary>
    /// Gets permission-specific data if available.
    /// </summary>
    /// <typeparam name="T">Type of permission data.</typeparam>
    /// <param name="permissionName">Name of the permission.</param>
    /// <returns>Permission data if found, default(T) otherwise.</returns>
    public T? GetPermissionData<T>(string permissionName)
    {
        return _permissionData.TryGetValue(permissionName, out var data) && data is T typedData
            ? typedData
            : default;
    }

    /// <summary>
    /// Creates an unrestricted permission set (highest trust level).
    /// </summary>
    public static PermissionSet CreateUnrestricted() => new()
    {
        IsUnrestricted = true,
        Level = SecurityLevel.Maximum,
        Zone = SecurityZone.LocalMachine  // Using the canonical SecurityZone from Abstractions
    };

    /// <summary>
    /// Creates a permission set for the LocalMachine zone.
    /// </summary>
    public static PermissionSet CreateLocalMachine() => new()
    {
        Level = SecurityLevel.High,
        Zone = SecurityZone.LocalMachine  // Using the canonical SecurityZone from Abstractions
    };

    /// <summary>
    /// Creates a permission set for the LocalIntranet zone.
    /// </summary>
    public static PermissionSet CreateLocalIntranet()
    {
        var permSet = new PermissionSet
        {
            Level = SecurityLevel.Medium,
            Zone = SecurityZone.LocalIntranet  // Using the canonical SecurityZone from Abstractions
        };
        permSet.AddPermission("FileIO.Read");
        permSet.AddPermission("FileIO.Write");
        permSet.AddPermission("FileIO.PathDiscovery");
        permSet.AddPermission("Network.Connect");
        return permSet;
    }

    /// <summary>
    /// Creates a permission set for the Internet zone.
    /// </summary>
    public static PermissionSet CreateInternet()
    {
        var permSet = new PermissionSet
        {
            Level = SecurityLevel.Low,
            Zone = SecurityZone.Internet
        };
        permSet.AddPermission("UI.SafeTopLevelWindows");
        permSet.AddPermission("FileDialog.Open");
        permSet.AddPermission("IsolatedStorage.AssemblyIsolationByUser");
        permSet.AddPermission("Network.Connect");
        return permSet;
    }

    /// <summary>
    /// Creates a restrictive permission set for untrusted code.
    /// </summary>
    public static PermissionSet CreateRestrictedSites()
    {
        var permSet = new PermissionSet
        {
            Level = SecurityLevel.Low,
            Zone = SecurityZone.Internet
        };
        permSet.AddPermission("Execution");
        return permSet;
    }

    /// <summary>
    /// Creates a minimal permission set (execution only).
    /// </summary>
    public static PermissionSet CreateMinimal()
    {
        var permSet = new PermissionSet
        {
            Level = SecurityLevel.Basic,
            Zone = SecurityZone.Internet
        };
        permSet.AddPermission("Execution");
        return permSet;
    }
}
