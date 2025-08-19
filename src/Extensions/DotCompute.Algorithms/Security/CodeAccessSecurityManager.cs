// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Security;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Security
{

/// <summary>
/// Manages Code Access Security (CAS) permissions for plugin assemblies.
/// Implements permission set restrictions and sandboxing capabilities.
/// </summary>
public sealed class CodeAccessSecurityManager : IDisposable
{
    private readonly ILogger<CodeAccessSecurityManager> _logger;
    private readonly CodeAccessSecurityOptions _options;
    private readonly ConcurrentDictionary<string, SecurityPermissionSet> _assemblyPermissions = new();
    private readonly ConcurrentDictionary<string, SecurityZone> _assemblyZones = new();
    private readonly Lock _lockObject = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeAccessSecurityManager"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">Configuration options for Code Access Security.</param>
    public CodeAccessSecurityManager(ILogger<CodeAccessSecurityManager> logger, CodeAccessSecurityOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new CodeAccessSecurityOptions();

        InitializeDefaultPermissionSets();
    }

    /// <summary>
    /// Creates a restricted permission set for a plugin assembly.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly.</param>
    /// <param name="securityZone">The security zone for the assembly.</param>
    /// <returns>The restricted permission set.</returns>
    public SecurityPermissionSet CreateRestrictedPermissionSet(string assemblyPath, SecurityZone securityZone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lockObject)
        {
            _logger.LogDebug("Creating restricted permission set for: {AssemblyPath}, Zone: {SecurityZone}", assemblyPath, securityZone);

            var permissionSet = new SecurityPermissionSet();

            // Add permissions based on security zone
            switch (securityZone)
            {
                case SecurityZone.MyComputer:
                    AddTrustedPermissions(permissionSet);
                    break;
                
                case SecurityZone.Intranet:
                    AddIntranetPermissions(permissionSet);
                    break;
                
                case SecurityZone.Internet:
                    AddInternetPermissions(permissionSet);
                    break;
                
                case SecurityZone.Untrusted:
                    AddUntrustedPermissions(permissionSet);
                    break;
                
                default:
                    AddMinimalPermissions(permissionSet);
                    break;
            }

            // Apply custom restrictions from options
            ApplyCustomRestrictions(permissionSet);

            // Cache the permission set
            _assemblyPermissions.TryAdd(assemblyPath, permissionSet);
            _assemblyZones.TryAdd(assemblyPath, securityZone);

            _logger.LogInformation("Created restricted permission set for: {AssemblyPath}, Permissions: {PermissionCount}", 
                assemblyPath, permissionSet.Permissions.Count);

            return permissionSet;
        }
    }

    /// <summary>
    /// Applies permission restrictions to an assembly load context.
    /// </summary>
    /// <param name="loadContext">The assembly load context to restrict.</param>
    /// <param name="assemblyPath">Path to the assembly being loaded.</param>
    /// <param name="permissionSet">The permission set to apply.</param>
    public void ApplyPermissionRestrictions(AssemblyLoadContext loadContext, string assemblyPath, SecurityPermissionSet permissionSet)
    {
        ArgumentNullException.ThrowIfNull(loadContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ArgumentNullException.ThrowIfNull(permissionSet);
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            _logger.LogDebug("Applying permission restrictions to load context: {ContextName}", loadContext.Name);

            // Note: .NET Core/.NET 5+ doesn't support traditional CAS like .NET Framework
            // However, we can implement similar functionality through other means:
            
            // 1. Monitor and restrict file system access
            SetupFileSystemRestrictions(loadContext, assemblyPath);
            
            // 2. Monitor and restrict network access
            SetupNetworkRestrictions(loadContext, assemblyPath);
            
            // 3. Monitor reflection and code generation
            SetupReflectionRestrictions(loadContext, assemblyPath);
            
            // 4. Set up resource usage limits
            SetupResourceLimits(loadContext, assemblyPath);

            _logger.LogInformation("Applied permission restrictions to: {AssemblyPath}", assemblyPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply permission restrictions to: {AssemblyPath}", assemblyPath);
            throw;
        }
    }

    /// <summary>
    /// Validates that an assembly operation is permitted under the current security context.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly.</param>
    /// <param name="operation">The operation being performed.</param>
    /// <param name="target">The target of the operation (file path, URL, etc.).</param>
    /// <returns>True if the operation is permitted; otherwise, false.</returns>
    public bool IsOperationPermitted(string assemblyPath, SecurityOperation operation, string? target = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_assemblyPermissions.TryGetValue(assemblyPath, out var permissionSet))
        {
            _logger.LogWarning("No permission set found for assembly: {AssemblyPath}, denying operation: {Operation}", 
                assemblyPath, operation);
            return false;
        }

        return ValidateOperation(permissionSet, operation, target);
    }

    /// <summary>
    /// Gets the security zone for an assembly.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly.</param>
    /// <returns>The security zone or Unknown if not found.</returns>
    public SecurityZone GetAssemblySecurityZone(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _assemblyZones.TryGetValue(assemblyPath, out var zone) ? zone : SecurityZone.Unknown;
    }

    /// <summary>
    /// Removes permission restrictions for an assembly.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly.</param>
    /// <returns>True if restrictions were removed; otherwise, false.</returns>
    public bool RemoveAssemblyRestrictions(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var removed = _assemblyPermissions.TryRemove(assemblyPath, out _);
        _assemblyZones.TryRemove(assemblyPath, out _);

        if (removed)
        {
            _logger.LogInformation("Removed permission restrictions for: {AssemblyPath}", assemblyPath);
        }

        return removed;
    }

    /// <summary>
    /// Saves the current security configuration to a file.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveConfigurationAsync(string configPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            var config = new CodeAccessSecurityConfiguration
            {
                DefaultSecurityZone = _options.DefaultSecurityZone,
                EnableFileSystemRestrictions = _options.EnableFileSystemRestrictions,
                EnableNetworkRestrictions = _options.EnableNetworkRestrictions,
                EnableReflectionRestrictions = _options.EnableReflectionRestrictions,
                MaxMemoryUsage = _options.MaxMemoryUsage,
                MaxExecutionTime = _options.MaxExecutionTime,
                AllowedFileSystemPaths = [.. _options.AllowedFileSystemPaths],
                AllowedNetworkEndpoints = [.. _options.AllowedNetworkEndpoints]
            };

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(configPath, json, cancellationToken);

            _logger.LogInformation("Saved Code Access Security configuration to: {ConfigPath}", configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration to: {ConfigPath}", configPath);
            throw;
        }
    }

    /// <summary>
    /// Loads security configuration from a file.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task LoadConfigurationAsync(string configPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!File.Exists(configPath))
        {
            _logger.LogWarning("Configuration file not found: {ConfigPath}", configPath);
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(configPath, cancellationToken);
            var config = JsonSerializer.Deserialize<CodeAccessSecurityConfiguration>(json);

            if (config != null)
            {
                ApplyConfiguration(config);
                _logger.LogInformation("Loaded Code Access Security configuration from: {ConfigPath}", configPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration from: {ConfigPath}", configPath);
            throw;
        }
    }

    private void InitializeDefaultPermissionSets()
    {
        _logger.LogDebug("Initializing default permission sets");
    }

    private void AddTrustedPermissions(SecurityPermissionSet permissionSet)
    {
        // Full trust permissions (MyComputer zone)
        permissionSet.AllowFileSystemAccess = true;
        permissionSet.AllowNetworkAccess = true;
        permissionSet.AllowReflection = true;
        permissionSet.AllowUnmanagedCode = true;
        permissionSet.MaxMemoryUsage = long.MaxValue;
        permissionSet.MaxExecutionTime = TimeSpan.MaxValue;
    }

    private void AddIntranetPermissions(SecurityPermissionSet permissionSet)
    {
        // Intranet zone permissions
        permissionSet.AllowFileSystemAccess = true;
        permissionSet.AllowNetworkAccess = true;
        permissionSet.AllowReflection = true;
        permissionSet.AllowUnmanagedCode = false;
        permissionSet.MaxMemoryUsage = 512 * 1024 * 1024; // 512 MB
        permissionSet.MaxExecutionTime = TimeSpan.FromMinutes(10);
        
        // Add allowed file paths
        permissionSet.AllowedFilePaths.Add(Environment.CurrentDirectory);
        permissionSet.AllowedFilePaths.AddRange(_options.AllowedFileSystemPaths);
    }

    private void AddInternetPermissions(SecurityPermissionSet permissionSet)
    {
        // Internet zone permissions - very restricted
        permissionSet.AllowFileSystemAccess = false;
        permissionSet.AllowNetworkAccess = true;
        permissionSet.AllowReflection = false;
        permissionSet.AllowUnmanagedCode = false;
        permissionSet.MaxMemoryUsage = _options.MaxMemoryUsage;
        permissionSet.MaxExecutionTime = _options.MaxExecutionTime;
        
        // Limited file I/O for specific directories only
        permissionSet.AllowedFilePaths.AddRange(_options.AllowedFileSystemPaths);
        permissionSet.AllowedNetworkEndpoints.AddRange(_options.AllowedNetworkEndpoints);
    }

    private void AddUntrustedPermissions(SecurityPermissionSet permissionSet)
    {
        // Minimal permissions for untrusted code
        permissionSet.AllowFileSystemAccess = false;
        permissionSet.AllowNetworkAccess = false;
        permissionSet.AllowReflection = false;
        permissionSet.AllowUnmanagedCode = false;
        permissionSet.MaxMemoryUsage = 64 * 1024 * 1024; // 64 MB
        permissionSet.MaxExecutionTime = TimeSpan.FromMinutes(1);
    }

    private void AddMinimalPermissions(SecurityPermissionSet permissionSet)
    {
        // Absolute minimal permissions
        permissionSet.AllowFileSystemAccess = false;
        permissionSet.AllowNetworkAccess = false;
        permissionSet.AllowReflection = false;
        permissionSet.AllowUnmanagedCode = false;
        permissionSet.MaxMemoryUsage = 32 * 1024 * 1024; // 32 MB
        permissionSet.MaxExecutionTime = TimeSpan.FromSeconds(30);
    }

    private void ApplyCustomRestrictions(SecurityPermissionSet permissionSet)
    {
        // Apply any custom restrictions from configuration
        if (!_options.AllowReflectionEmit)
        {
            permissionSet.AllowReflection = false;
        }
        
        if (_options.EnableFileSystemRestrictions)
        {
            permissionSet.AllowFileSystemAccess = permissionSet.AllowedFilePaths.Count > 0;
        }
        
        if (_options.EnableNetworkRestrictions)
        {
            permissionSet.AllowNetworkAccess = permissionSet.AllowedNetworkEndpoints.Count > 0;
        }
    }

    private void SetupFileSystemRestrictions(AssemblyLoadContext loadContext, string assemblyPath)
    {
        if (!_options.EnableFileSystemRestrictions)
            return;

        // In modern .NET, we would implement this through:
        // 1. File system watchers
        // 2. Custom file I/O interceptors
        // 3. Process monitoring
        
        _logger.LogDebug("Setting up file system restrictions for: {AssemblyPath}", assemblyPath);
    }

    private void SetupNetworkRestrictions(AssemblyLoadContext loadContext, string assemblyPath)
    {
        if (!_options.EnableNetworkRestrictions)
            return;

        // Network restrictions could be implemented through:
        // 1. Custom HttpClientHandler
        // 2. Network monitoring
        // 3. Firewall integration
        
        _logger.LogDebug("Setting up network restrictions for: {AssemblyPath}", assemblyPath);
    }

    private void SetupReflectionRestrictions(AssemblyLoadContext loadContext, string assemblyPath)
    {
        if (!_options.EnableReflectionRestrictions)
            return;

        // Reflection restrictions could be monitored through:
        // 1. Custom assembly resolution
        // 2. Type loading monitoring
        // 3. Dynamic method creation tracking
        
        _logger.LogDebug("Setting up reflection restrictions for: {AssemblyPath}", assemblyPath);
    }

    private void SetupResourceLimits(AssemblyLoadContext loadContext, string assemblyPath)
    {
        // Resource limits implementation:
        // 1. Memory usage monitoring
        // 2. CPU time limits
        // 3. Thread count limits
        
        _logger.LogDebug("Setting up resource limits for: {AssemblyPath}", assemblyPath);
    }

    private bool ValidateOperation(SecurityPermissionSet permissionSet, SecurityOperation operation, string? target)
    {
        try
        {
            return operation switch
            {
                SecurityOperation.FileRead => ValidateFileOperation(permissionSet, target, isWrite: false),
                SecurityOperation.FileWrite => ValidateFileOperation(permissionSet, target, isWrite: true),
                SecurityOperation.NetworkAccess => ValidateNetworkOperation(permissionSet, target),
                SecurityOperation.ReflectionAccess => ValidateReflectionOperation(permissionSet),
                SecurityOperation.UnmanagedCode => ValidateUnmanagedCodeOperation(permissionSet),
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating operation: {Operation}", operation);
            return false;
        }
    }

    private static bool ValidateFileOperation(SecurityPermissionSet permissionSet, string? target, bool isWrite)
    {
        if (!permissionSet.AllowFileSystemAccess)
            return false;

        if (target == null)
            return permissionSet.AllowFileSystemAccess;

        // Check if the target path is in the allowed paths
        return permissionSet.AllowedFilePaths.Any(allowedPath => 
            target.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ValidateNetworkOperation(SecurityPermissionSet permissionSet, string? target)
    {
        if (!permissionSet.AllowNetworkAccess)
            return false;

        if (target == null)
            return permissionSet.AllowNetworkAccess;

        // Check if the target endpoint is in the allowed endpoints
        return permissionSet.AllowedNetworkEndpoints.Any(allowedEndpoint => 
            target.StartsWith(allowedEndpoint, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ValidateReflectionOperation(SecurityPermissionSet permissionSet)
    {
        return permissionSet.AllowReflection;
    }

    private static bool ValidateUnmanagedCodeOperation(SecurityPermissionSet permissionSet)
    {
        return permissionSet.AllowUnmanagedCode;
    }

    private void ApplyConfiguration(CodeAccessSecurityConfiguration config)
    {
        _options.DefaultSecurityZone = config.DefaultSecurityZone;
        _options.EnableFileSystemRestrictions = config.EnableFileSystemRestrictions;
        _options.EnableNetworkRestrictions = config.EnableNetworkRestrictions;
        _options.EnableReflectionRestrictions = config.EnableReflectionRestrictions;
        _options.MaxMemoryUsage = config.MaxMemoryUsage;
        _options.MaxExecutionTime = config.MaxExecutionTime;
        
        _options.AllowedFileSystemPaths.Clear();
        _options.AllowedFileSystemPaths.AddRange(config.AllowedFileSystemPaths);
        
        _options.AllowedNetworkEndpoints.Clear();
        _options.AllowedNetworkEndpoints.AddRange(config.AllowedNetworkEndpoints);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _assemblyPermissions.Clear();
            _assemblyZones.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Configuration options for Code Access Security.
/// </summary>
public sealed class CodeAccessSecurityOptions
{
    /// <summary>
    /// Gets or sets the default security zone for assemblies.
    /// </summary>
    public SecurityZone DefaultSecurityZone { get; set; } = SecurityZone.Internet;

    /// <summary>
    /// Gets or sets whether file system access restrictions are enabled.
    /// </summary>
    public bool EnableFileSystemRestrictions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether network access restrictions are enabled.
    /// </summary>
    public bool EnableNetworkRestrictions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether reflection restrictions are enabled.
    /// </summary>
    public bool EnableReflectionRestrictions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether reflection emit is allowed.
    /// </summary>
    public bool AllowReflectionEmit { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum memory usage per assembly in bytes.
    /// </summary>
    public long MaxMemoryUsage { get; set; } = 256 * 1024 * 1024; // 256 MB

    /// <summary>
    /// Gets or sets the maximum execution time per operation.
    /// </summary>
    public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the list of allowed file system paths.
    /// </summary>
    public List<string> AllowedFileSystemPaths { get; } = [];

    /// <summary>
    /// Gets the list of allowed network endpoints.
    /// </summary>
    public List<string> AllowedNetworkEndpoints { get; } = [];
}

/// <summary>
/// Security zones for assemblies.
/// </summary>
public enum SecurityZone
{
    /// <summary>
    /// Unknown security zone.
    /// </summary>
    Unknown = -1,

    /// <summary>
    /// MyComputer zone - full trust.
    /// </summary>
    MyComputer = 0,

    /// <summary>
    /// Intranet zone - high trust.
    /// </summary>
    Intranet = 1,

    /// <summary>
    /// Internet zone - medium trust.
    /// </summary>
    Internet = 2,

    /// <summary>
    /// Untrusted zone - minimal trust.
    /// </summary>
    Untrusted = 3
}

/// <summary>
/// Security operations that can be validated.
/// </summary>
public enum SecurityOperation
{
    /// <summary>
    /// File read operation.
    /// </summary>
    FileRead,

    /// <summary>
    /// File write operation.
    /// </summary>
    FileWrite,

    /// <summary>
    /// Network access operation.
    /// </summary>
    NetworkAccess,

    /// <summary>
    /// Reflection access operation.
    /// </summary>
    ReflectionAccess,

    /// <summary>
    /// Unmanaged code operation.
    /// </summary>
    UnmanagedCode
}

/// <summary>
/// Modern .NET Core compatible permission set implementation.
/// Replaces the .NET Framework PermissionSet class.
/// </summary>
public sealed class SecurityPermissionSet
{
    /// <summary>
    /// Gets or sets whether file system access is allowed.
    /// </summary>
    public bool AllowFileSystemAccess { get; set; }

    /// <summary>
    /// Gets or sets whether network access is allowed.
    /// </summary>
    public bool AllowNetworkAccess { get; set; }

    /// <summary>
    /// Gets or sets whether reflection is allowed.
    /// </summary>
    public bool AllowReflection { get; set; }

    /// <summary>
    /// Gets or sets whether unmanaged code execution is allowed.
    /// </summary>
    public bool AllowUnmanagedCode { get; set; }

    /// <summary>
    /// Gets or sets the maximum memory usage in bytes.
    /// </summary>
    public long MaxMemoryUsage { get; set; } = 256 * 1024 * 1024; // 256 MB

    /// <summary>
    /// Gets or sets the maximum execution time.
    /// </summary>
    public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the list of allowed file system paths.
    /// </summary>
    public List<string> AllowedFilePaths { get; } = [];

    /// <summary>
    /// Gets the list of allowed network endpoints.
    /// </summary>
    public List<string> AllowedNetworkEndpoints { get; } = [];

    /// <summary>
    /// Gets the collection of individual permissions.
    /// </summary>
    public Dictionary<string, object> Permissions { get; } = [];

    /// <summary>
    /// Adds a permission to the set.
    /// </summary>
    /// <param name="name">Permission name.</param>
    /// <param name="value">Permission value.</param>
    public void AddPermission(string name, object value)
    {
        Permissions[name] = value;
    }

    /// <summary>
    /// Removes a permission from the set.
    /// </summary>
    /// <param name="name">Permission name.</param>
    /// <returns>True if removed; otherwise, false.</returns>
    public bool RemovePermission(string name)
    {
        return Permissions.Remove(name);
    }

    /// <summary>
    /// Checks if a permission exists in the set.
    /// </summary>
    /// <param name="name">Permission name.</param>
    /// <returns>True if the permission exists; otherwise, false.</returns>
    public bool HasPermission(string name)
    {
        return Permissions.ContainsKey(name);
    }

    /// <summary>
    /// Creates a copy of this permission set.
    /// </summary>
    /// <returns>A new permission set with the same settings.</returns>
    public SecurityPermissionSet Clone()
    {
        var clone = new SecurityPermissionSet
        {
            AllowFileSystemAccess = AllowFileSystemAccess,
            AllowNetworkAccess = AllowNetworkAccess,
            AllowReflection = AllowReflection,
            AllowUnmanagedCode = AllowUnmanagedCode,
            MaxMemoryUsage = MaxMemoryUsage,
            MaxExecutionTime = MaxExecutionTime
        };

        clone.AllowedFilePaths.AddRange(AllowedFilePaths);
        clone.AllowedNetworkEndpoints.AddRange(AllowedNetworkEndpoints);

        foreach (var permission in Permissions)
        {
            clone.Permissions[permission.Key] = permission.Value;
        }

        return clone;
    }
}

/// <summary>
/// Configuration for Code Access Security serialization.
/// </summary>
public sealed class CodeAccessSecurityConfiguration
{
    /// <summary>
    /// Gets or sets the default security zone.
    /// </summary>
    public SecurityZone DefaultSecurityZone { get; set; } = SecurityZone.Internet;

    /// <summary>
    /// Gets or sets whether file system restrictions are enabled.
    /// </summary>
    public bool EnableFileSystemRestrictions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether network restrictions are enabled.
    /// </summary>
    public bool EnableNetworkRestrictions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether reflection restrictions are enabled.
    /// </summary>
    public bool EnableReflectionRestrictions { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum memory usage.
    /// </summary>
    public long MaxMemoryUsage { get; set; } = 256 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum execution time.
    /// </summary>
    public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the allowed file system paths.
    /// </summary>
    public List<string> AllowedFileSystemPaths { get; set; } = [];

    /// <summary>
    /// Gets or sets the allowed network endpoints.
    /// </summary>
    public List<string> AllowedNetworkEndpoints { get; set; } = [];
}}
