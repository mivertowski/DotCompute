// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using global::System.Runtime.Loader;
using System.Security;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Security;

/// <summary>
/// Isolated assembly load context for secure plugin loading with restricted access.
/// </summary>
public class IsolatedPluginLoadContext : AssemblyLoadContext
{
    private readonly string _pluginAssemblyPath;
    private readonly SandboxPermissions _permissions;
    private readonly ILogger _logger;
    private readonly AssemblyDependencyResolver _resolver;
    private readonly HashSet<string> _loadedAssemblies = [];
    private readonly object _loadLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="IsolatedPluginLoadContext"/> class.
    /// </summary>
    public IsolatedPluginLoadContext(
        string name,
        string pluginAssemblyPath,
        SandboxPermissions permissions,
        ILogger logger)

        : base(name, isCollectible: true)
    {
        _pluginAssemblyPath = pluginAssemblyPath ?? throw new ArgumentNullException(nameof(pluginAssemblyPath));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    /// <summary>
    /// Loads an assembly with security validation.
    /// </summary>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        lock (_loadLock)
        {
            _logger.LogDebug("Loading assembly: {AssemblyName}", assemblyName);

            // Check if this assembly is allowed to be loaded
            if (!IsAssemblyLoadAllowed(assemblyName))
            {
                _logger.LogWarning("Assembly load denied: {AssemblyName}", assemblyName);
                throw new SecurityException($"Assembly load denied: {assemblyName}");
            }

            // Track loaded assemblies
            var assemblyKey = assemblyName.FullName;
            if (_loadedAssemblies.Contains(assemblyKey))
            {
                _logger.LogDebug("Assembly already loaded: {AssemblyName}", assemblyName);
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


                _logger.LogDebug("Successfully loaded assembly: {AssemblyName} from {Path}",

                    assemblyName, assemblyPath);


                return assembly;
            }

            // Check if it's a system assembly that should be loaded from default context
            if (IsSystemAssembly(assemblyName))
            {
                _logger.LogDebug("Loading system assembly from default context: {AssemblyName}", assemblyName);
                return null; // Load from default context
            }

            _logger.LogWarning("Could not resolve assembly: {AssemblyName}", assemblyName);
            return null;
        }
    }

    /// <summary>
    /// Loads an unmanaged DLL with security restrictions.
    /// </summary>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        _logger.LogDebug("Loading unmanaged DLL: {DllName}", unmanagedDllName);

        // Check if unmanaged DLL loading is allowed
        if (!_permissions.AllowedPermissions.Contains("LoadNativeDll"))
        {
            _logger.LogWarning("Unmanaged DLL load denied: {DllName}", unmanagedDllName);
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
            _logger.LogDebug("Loading unmanaged DLL from: {Path}", libraryPath);
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        _logger.LogWarning("Could not resolve unmanaged DLL: {DllName}", unmanagedDllName);
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
                _logger.LogWarning("Assembly exceeds size limit: {Path} ({Size} bytes)",

                    assemblyPath, fileInfo.Length);
                return false;
            }

            // Check if path is safe (prevent path traversal)
            var fullPath = Path.GetFullPath(assemblyPath);
            var fileName = Path.GetFileName(fullPath);


            if (fileName.Contains("..") || fileName.Contains("~") || fileName.StartsWith("."))
            {
                _logger.LogWarning("Unsafe assembly path detected: {Path}", assemblyPath);
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
            _logger.LogError(ex, "Error validating assembly: {Path}", assemblyPath);
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