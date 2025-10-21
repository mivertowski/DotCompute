// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// using global::System.Security.Permissions; // Commented out for .NET compatibility
using System.Diagnostics.CodeAnalysis;
using System.Security.Permissions;
using System.Text.Json;
using DotCompute.Abstractions.Security;
using Microsoft.Extensions.Logging;
using PermissionSet = DotCompute.Algorithms.Security.PermissionSet;

namespace DotCompute.Algorithms.Types.Security
{

    /// <summary>
    /// Manages Code Access Security (CAS) for plugin execution.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CodeAccessSecurityManager"/> class.
    /// </remarks>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <param name="options">CAS options.</param>
    public sealed partial class CodeAccessSecurityManager(
    ILogger<CodeAccessSecurityManager>? logger = null,
    CodeAccessSecurityOptions? options = null) : IDisposable
    {
        private readonly ILogger<CodeAccessSecurityManager>? _logger = logger;
        private readonly CodeAccessSecurityOptions _options = options ?? new CodeAccessSecurityOptions();
        private readonly Dictionary<string, PermissionSet> _permissionSets = [];
        private bool _disposed;

        /// <summary>
        /// Creates a restricted permission set for an assembly.
        /// </summary>
        /// <param name="assemblyPath">Path to the assembly.</param>
        /// <param name="securityZone">Security zone for the assembly.</param>
        /// <returns>The created permission set.</returns>
        public PermissionSet CreateRestrictedPermissionSet(string assemblyPath, SecurityZone securityZone)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

            LogCreatingPermissionSet(assemblyPath, securityZone);

            // Create a permission set based on security zone
            var permissionSet = new PermissionSet
            {
                Zone = securityZone,
                Level = securityZone switch
                {
                    SecurityZone.LocalMachine => DotCompute.Abstractions.Security.SecurityLevel.Maximum,
                    SecurityZone.LocalIntranet => DotCompute.Abstractions.Security.SecurityLevel.High,
                    SecurityZone.TrustedSites => DotCompute.Abstractions.Security.SecurityLevel.Medium,
                    SecurityZone.Internet => DotCompute.Abstractions.Security.SecurityLevel.Low,
                    SecurityZone.RestrictedSites => DotCompute.Abstractions.Security.SecurityLevel.Basic,
                    _ => DotCompute.Abstractions.Security.SecurityLevel.None
                }
            };

            // Add permissions based on security zone
            switch (securityZone)
            {
                case SecurityZone.LocalMachine:
                    AddLocalMachinePermissions(permissionSet);
                    break;
                case SecurityZone.LocalIntranet:
                    AddLocalIntranetPermissions(permissionSet);
                    break;
                case SecurityZone.TrustedSites:
                    AddTrustedSitesPermissions(permissionSet);
                    break;
                case SecurityZone.Internet:
                    AddInternetPermissions(permissionSet);
                    break;
                case SecurityZone.RestrictedSites:
                    AddRestrictedSitesPermissions(permissionSet);
                    break;
                default:
                    AddMinimalPermissions(permissionSet);
                    break;
            }

            // Store the permission set
            _permissionSets[assemblyPath] = permissionSet;

            LogCreatedPermissionSet(permissionSet.GrantedPermissions.Count, assemblyPath);

            return permissionSet;
        }

        /// <summary>
        /// Checks if a specific operation is permitted for an assembly.
        /// </summary>
        /// <param name="assemblyPath">Path to the assembly.</param>
        /// <param name="operation">The operation to check.</param>
        /// <param name="target">Optional target for the operation.</param>
        /// <returns>True if the operation is permitted.</returns>
        public bool IsOperationPermitted(string assemblyPath, SecurityOperation operation, string? target = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

            if (!_permissionSets.TryGetValue(assemblyPath, out var permissionSet))
            {
                LogNoPermissionSetFound(assemblyPath);
                return false;
            }

            // Check permission based on operation type
            return operation switch
            {
                SecurityOperation.FileRead => CheckFileReadPermission(permissionSet, target),
                SecurityOperation.FileWrite => CheckFileWritePermission(permissionSet, target),
                SecurityOperation.NetworkAccess => CheckNetworkPermission(permissionSet, target),
                SecurityOperation.ReflectionAccess => CheckReflectionPermission(permissionSet),
                SecurityOperation.UnmanagedCode => CheckUnmanagedCodePermission(permissionSet),
                SecurityOperation.RegistryAccess => CheckRegistryPermission(permissionSet),
                SecurityOperation.EnvironmentAccess => CheckEnvironmentPermission(permissionSet),
                SecurityOperation.UserInterface => CheckUIPermission(permissionSet),
                _ => false
            };
        }

        /// <summary>
        /// Saves the CAS configuration to a file.
        /// </summary>
        /// <param name="filePath">Path to save the configuration.</param>
        /// <returns>A task representing the async operation.</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute",
            Justification = "JSON serialization used for configuration only, types are preserved")]
        [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCodeAttribute",
            Justification = "JSON serialization used for configuration only")]
        public async Task SaveConfigurationAsync(string filePath)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            var config = new
            {
                DefaultSecurityZone = _options.DefaultSecurityZone,
                EnableFileSystemRestrictions = _options.EnableFileSystemRestrictions,
                EnableNetworkRestrictions = _options.EnableNetworkRestrictions,
                EnableReflectionRestrictions = _options.EnableReflectionRestrictions,
                AllowReflectionEmit = _options.AllowReflectionEmit,
                MaxMemoryUsage = _options.MaxMemoryUsage,
                MaxExecutionTime = _options.MaxExecutionTime,
                AllowedFileSystemPaths = _options.AllowedFileSystemPaths,
                AllowedNetworkEndpoints = _options.AllowedNetworkEndpoints
            };

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);

            LogConfigurationSaved(filePath);
        }

        /// <summary>
        /// Loads the CAS configuration from a file.
        /// </summary>
        /// <param name="filePath">Path to load the configuration from.</param>
        /// <returns>A task representing the async operation.</returns>
        public async Task LoadConfigurationAsync(string filePath)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"CAS configuration file not found: {filePath}");
            }

            var json = await File.ReadAllTextAsync(filePath);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            // Load configuration properties
            if (root.TryGetProperty("DefaultSecurityZone", out var zone))
            {
                _options.DefaultSecurityZone = Enum.Parse<SecurityZone>(zone.GetString()!);
            }

            if (root.TryGetProperty("EnableFileSystemRestrictions", out var fileRestrictions))
            {
                _options.EnableFileSystemRestrictions = fileRestrictions.GetBoolean();
            }

            if (root.TryGetProperty("EnableNetworkRestrictions", out var netRestrictions))
            {
                _options.EnableNetworkRestrictions = netRestrictions.GetBoolean();
            }

            if (root.TryGetProperty("EnableReflectionRestrictions", out var refRestrictions))
            {
                _options.EnableReflectionRestrictions = refRestrictions.GetBoolean();
            }

            if (root.TryGetProperty("AllowReflectionEmit", out var allowRefEmit))
            {
                _options.AllowReflectionEmit = allowRefEmit.GetBoolean();
            }

            if (root.TryGetProperty("MaxMemoryUsage", out var maxMem))
            {
                _options.MaxMemoryUsage = maxMem.GetInt64();
            }

            if (root.TryGetProperty("MaxExecutionTime", out var maxTime))
            {
                _options.MaxExecutionTime = TimeSpan.FromMilliseconds(maxTime.GetDouble());
            }

            // Load allowed paths and endpoints

            if (root.TryGetProperty("AllowedFileSystemPaths", out var paths))
            {
                _options.AllowedFileSystemPaths.Clear();
                foreach (var path in paths.EnumerateArray())
                {
                    _options.AllowedFileSystemPaths.Add(path.GetString()!);
                }
            }

            if (root.TryGetProperty("AllowedNetworkEndpoints", out var endpoints))
            {
                _options.AllowedNetworkEndpoints.Clear();
                foreach (var endpoint in endpoints.EnumerateArray())
                {
                    _options.AllowedNetworkEndpoints.Add(endpoint.GetString()!);
                }
            }

            LogConfigurationLoaded(filePath);
        }

        #region Permission Set Creation Methods

        private static void AddLocalMachinePermissions(PermissionSet permissionSet)
        {
            // Full trust for local machine zone
            permissionSet.SetPermission(new FileIOPermission(PermissionState.Unrestricted));
            permissionSet.SetPermission(new ReflectionPermission(PermissionState.Unrestricted));
            permissionSet.SetPermission(new SecurityPermission(SecurityPermissionFlag.AllFlags));
        }

        private void AddLocalIntranetPermissions(PermissionSet permissionSet)
        {
            // High trust for local intranet
            permissionSet.SetPermission(new FileIOPermission(FileIOPermissionAccess.Read | FileIOPermissionAccess.PathDiscovery, _options.AllowedFileSystemPaths.ToArray()));
            permissionSet.SetPermission(new ReflectionPermission(ReflectionPermissionFlag.RestrictedMemberAccess));
            permissionSet.SetPermission(new SecurityPermission(SecurityPermissionFlag.Execution));
        }

        private void AddTrustedSitesPermissions(PermissionSet permissionSet)
        {
            // Medium trust for trusted sites
            if (_options.AllowedFileSystemPaths.Count > 0)
            {
                permissionSet.SetPermission(new FileIOPermission(FileIOPermissionAccess.Read, _options.AllowedFileSystemPaths.ToArray()));
            }
            permissionSet.SetPermission(new SecurityPermission(SecurityPermissionFlag.Execution));
        }

        private static void AddInternetPermissions(PermissionSet permissionSet)
            // Low trust for internet zone

            => permissionSet.SetPermission(new SecurityPermission(SecurityPermissionFlag.Execution));

        private static void AddRestrictedSitesPermissions(PermissionSet permissionSet)
            // Minimal trust for restricted sites

            => permissionSet.SetPermission(new SecurityPermission(SecurityPermissionFlag.Execution));

        private static void AddMinimalPermissions(PermissionSet permissionSet)
            // Bare minimum permissions

            => permissionSet.SetPermission(new SecurityPermission(SecurityPermissionFlag.Execution));

        #endregion

        #region Permission Check Methods

        private bool CheckFileReadPermission(PermissionSet permissionSet, string? target)
        {
            if (!_options.EnableFileSystemRestrictions)
            {

                return true;
            }


            try
            {
                var filePermission = permissionSet.GetPermission(typeof(FileIOPermission)) as FileIOPermission;
                return filePermission != null && (filePermission.IsUnrestricted() ||
                       string.IsNullOrEmpty(target) ||
                       _options.AllowedFileSystemPaths.Any(path => target.StartsWith(path, StringComparison.OrdinalIgnoreCase)));
            }
            catch
            {
                return false;
            }
        }

        private bool CheckFileWritePermission(PermissionSet permissionSet, string? target)
        {
            if (!_options.EnableFileSystemRestrictions)
            {

                return true;
            }


            try
            {
                var filePermission = permissionSet.GetPermission(typeof(FileIOPermission)) as FileIOPermission;
                return filePermission != null && filePermission.IsUnrestricted();
            }
            catch
            {
                return false;
            }
        }

        private bool CheckNetworkPermission(PermissionSet permissionSet, string? target)
        {
            if (!_options.EnableNetworkRestrictions)
            {

                return true;
            }


            return string.IsNullOrEmpty(target) ||
                   _options.AllowedNetworkEndpoints.Any(endpoint =>
                       target.StartsWith(endpoint, StringComparison.OrdinalIgnoreCase));
        }

        private bool CheckReflectionPermission(PermissionSet permissionSet)
        {
            if (!_options.EnableReflectionRestrictions)
            {

                return true;
            }


            try
            {
                var reflectionPermission = permissionSet.GetPermission(typeof(ReflectionPermission)) as ReflectionPermission;
                return reflectionPermission != null;
            }
            catch
            {
                return false;
            }
        }

        private bool CheckUnmanagedCodePermission(PermissionSet permissionSet)
        {
            try
            {
                var securityPermission = permissionSet.GetPermission(typeof(SecurityPermission)) as SecurityPermission;
                return securityPermission != null &&
                       securityPermission.Flags.HasFlag(SecurityPermissionFlag.UnmanagedCode);
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckRegistryPermission(PermissionSet permissionSet)
            // Simplified registry permission check

            => false; // Restrict registry access by default

        private static bool CheckEnvironmentPermission(PermissionSet permissionSet)
            // Simplified environment permission check

            => false; // Restrict environment access by default

        private static bool CheckUIPermission(PermissionSet permissionSet)
            // Simplified UI permission check

            => true; // Allow basic UI operations

        #endregion

        /// <summary>
        /// Disposes the CAS manager resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _permissionSets.Clear();
                _disposed = true;
            }
        }

        #region LoggerMessage Delegates

        [LoggerMessage(Level = LogLevel.Debug, Message = "Creating permission set for {AssemblyPath} in zone {SecurityZone}")]
        private partial void LogCreatingPermissionSet(string assemblyPath, SecurityZone securityZone);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Created permission set with {PermissionCount} permissions for {AssemblyPath}")]
        private partial void LogCreatedPermissionSet(int permissionCount, string assemblyPath);

        [LoggerMessage(Level = LogLevel.Warning, Message = "No permission set found for {AssemblyPath}")]
        private partial void LogNoPermissionSetFound(string assemblyPath);

        [LoggerMessage(Level = LogLevel.Information, Message = "CAS configuration saved to {FilePath}")]
        private partial void LogConfigurationSaved(string filePath);

        [LoggerMessage(Level = LogLevel.Information, Message = "CAS configuration loaded from {FilePath}")]
        private partial void LogConfigurationLoaded(string filePath);

        #endregion
    }

    /// <summary>
    /// Options for Code Access Security management.
    /// </summary>
    public class CodeAccessSecurityOptions
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
        /// Gets or sets whether reflection emit is allowed.
        /// </summary>
        public bool AllowReflectionEmit { get; set; }

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
        public IList<string> AllowedFileSystemPaths { get; } = [];

        /// <summary>
        /// Gets the list of allowed network endpoints.
        /// </summary>
        public IList<string> AllowedNetworkEndpoints { get; } = [];
    }
}
