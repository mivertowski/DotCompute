// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using System.Runtime.Loader;
using System.Security;
using Microsoft.Extensions.Logging;
using DotCompute.Plugins.Logging;

namespace DotCompute.Plugins.Security;

/// <summary>
/// Isolated assembly load context for secure plugin loading with restricted access.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="IsolatedPluginLoadContext"/> class.
/// </remarks>
public class IsolatedPluginLoadContext(
    string name,
    string pluginAssemblyPath,
    SandboxPermissions permissions,
    ILogger logger) : AssemblyLoadContext(name, isCollectible: true)
{
    private readonly SandboxPermissions _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly AssemblyDependencyResolver _resolver = new(pluginAssemblyPath);
    private readonly HashSet<string> _loadedAssemblies = [];
    private readonly object _loadLock = new();

    /// <summary>
    /// Loads an assembly with security validation.
    /// </summary>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        lock (_loadLock)
        {
            _logger.LogDebugMessage("Loading assembly: {assemblyName}");

            // Check if this assembly is allowed to be loaded
            if (!IsAssemblyLoadAllowed(assemblyName))
            {
                _logger.LogWarningMessage("Assembly load denied: {assemblyName}");
                throw new SecurityException($"Assembly load denied: {assemblyName}");
            }

            // Track loaded assemblies
            var assemblyKey = assemblyName.FullName;
            if (_loadedAssemblies.Contains(assemblyKey))
            {
                _logger.LogDebugMessage("Assembly already loaded: {assemblyName}");
                return null; // Already loaded
            }

            // Try to resolve assembly path
            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                // Validate assembly before loading
                if (!ValidateAssemblyBeforeLoad(assemblyPath))
                {
                    throw new SecurityException($"Assembly security validation failed: {assemblyName}");
                }

                _ = _loadedAssemblies.Add(assemblyKey);
                var assembly = LoadFromAssemblyPath(assemblyPath);


                _logger.LogDebugMessage($"Successfully loaded assembly: {assemblyName} from {assemblyPath}");


                return assembly;
            }

            // Check if it's a system assembly that should be loaded from default context
            if (IsSystemAssembly(assemblyName))
            {
                _logger.LogDebugMessage("Loading system assembly from default context: {assemblyName}");
                return null; // Load from default context
            }

            _logger.LogWarningMessage("Could not resolve assembly: {assemblyName}");
            return null;
        }
    }

    /// <summary>
    /// Loads an unmanaged DLL with security restrictions.
    /// </summary>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        _logger.LogDebugMessage("Loading unmanaged DLL: {unmanagedDllName}");

        // Check if unmanaged DLL loading is allowed
        if (!_permissions.AllowedPermissions.Contains("LoadNativeDll"))
        {
            _logger.LogWarningMessage("Unmanaged DLL load denied: {unmanagedDllName}");
            throw new SecurityException($"Unmanaged DLL load denied: {unmanagedDllName}");
        }

        // Validate DLL path for security
        if (!IsUnmanagedDllSafe(unmanagedDllName))
        {
            throw new SecurityException($"Unsafe unmanaged DLL path: {unmanagedDllName}");
        }

        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            _logger.LogDebugMessage("Loading unmanaged DLL from: {libraryPath}");
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        _logger.LogWarningMessage("Could not resolve unmanaged DLL: {unmanagedDllName}");
        return IntPtr.Zero;
    }

    /// <summary>
    /// Determines if an assembly is allowed to be loaded based on security policy.
    /// </summary>
    private bool IsAssemblyLoadAllowed(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        // Check denied assemblies list
        if (_permissions.DeniedPermissions.Contains($"Assembly:{name}"))
        {
            return false;
        }

        // Check for dangerous assemblies that should be restricted
        var dangerousAssemblies = new[]
        {
            "System.Reflection.Emit",
            "System.CodeDom.Compiler",
            "Microsoft.CSharp",
            "System.Management"
        };

        if (dangerousAssemblies.Any(dangerous => name.StartsWith(dangerous, StringComparison.OrdinalIgnoreCase)))
        {
            // Only allow if explicitly permitted
            return _permissions.AllowedPermissions.Contains($"Assembly:{name}") ||
                   _permissions.AllowedPermissions.Contains("LoadDangerousAssemblies");
        }

        // Allow system assemblies and explicitly allowed assemblies
        return IsSystemAssembly(assemblyName) ||

               _permissions.AllowedPermissions.Contains($"Assembly:{name}") ||
               _permissions.AllowedPermissions.Contains("LoadAllAssemblies");
    }

    /// <summary>
    /// Determines if an assembly is a system assembly.
    /// </summary>
    private static bool IsSystemAssembly(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        return name.StartsWith("System.") ||
               name.StartsWith("Microsoft.") ||
               name.StartsWith("netstandard") ||
               name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("System.Private.CoreLib", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates an assembly before loading it.
    /// </summary>
    private bool ValidateAssemblyBeforeLoad(string assemblyPath)
    {
        try
        {
            // Basic file validation
            if (!File.Exists(assemblyPath))
            {
                return false;
            }

            // Check file size limits
            var fileInfo = new FileInfo(assemblyPath);
            if (fileInfo.Length > _permissions.ResourceLimits.MaxMemoryMB * 1024 * 1024)
            {
                _logger.LogWarningMessage($"Assembly exceeds size limit: {assemblyPath} ({fileInfo.Length} bytes)");
                return false;
            }

            // Check if path is safe (prevent path traversal)
            var fullPath = Path.GetFullPath(assemblyPath);
            var fileName = Path.GetFileName(fullPath);


            if (fileName.Contains("..") || fileName.Contains("~") || fileName.StartsWith("."))
            {
                _logger.LogWarningMessage("Unsafe assembly path detected: {assemblyPath}");
                return false;
            }

            // Additional security checks could be added here:
            // - Hash verification
            // - Signature validation
            // - Metadata analysis

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Error validating assembly: {assemblyPath}");
            return false;
        }
    }

    /// <summary>
    /// Determines if an unmanaged DLL path is safe.
    /// </summary>
    private static bool IsUnmanagedDllSafe(string dllName)
    {
        // Check for path traversal attacks
        if (dllName.Contains("..") || dllName.Contains("~") ||

            dllName.Contains(":") || dllName.StartsWith("."))
        {
            return false;
        }

        // Check for dangerous DLLs
        var dangerousDlls = new[]
        {
            "kernel32", "ntdll", "user32", "advapi32", "shell32"
        };

        var baseName = Path.GetFileNameWithoutExtension(dllName).ToLowerInvariant();
        if (dangerousDlls.Contains(baseName))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets information about loaded assemblies in this context.
    /// </summary>
    public IReadOnlyList<string> GetLoadedAssemblies()
    {
        lock (_loadLock)
        {
            return _loadedAssemblies.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Gets the number of assemblies loaded in this context.
    /// </summary>
    public int LoadedAssemblyCount
    {
        get
        {
            lock (_loadLock)
            {
                return _loadedAssemblies.Count;
            }
        }
    }
}