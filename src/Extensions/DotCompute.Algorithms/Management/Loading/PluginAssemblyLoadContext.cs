
// <copyright file="PluginAssemblyLoadContext.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;

namespace DotCompute.Algorithms.Management.Loading;

/// <summary>
/// Enhanced plugin assembly load context with isolation support.
/// Provides isolated execution environments for algorithm plugins to prevent conflicts and enhance security.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PluginAssemblyLoadContext"/> class.
/// </remarks>
/// <param name="name">The name of the load context for identification.</param>
/// <param name="pluginPath">The file system path to the plugin assembly.</param>
/// <param name="enableIsolation">Whether to enable assembly isolation from the default context.</param>
public sealed class PluginAssemblyLoadContext(string name, string pluginPath, bool enableIsolation) : AssemblyLoadContext(name, isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);
    private readonly bool _enableIsolation = enableIsolation;
    private WeakReference? _weakReference;

    /// <summary>
    /// Gets a value indicating whether the load context is still alive.
    /// This property tracks whether the context has been garbage collected after unloading.
    /// </summary>
    public bool IsAlive => _weakReference?.IsAlive ?? true;

    /// <summary>
    /// Loads an assembly given its name.
    /// Implements custom resolution logic for plugin dependencies with optional isolation.
    /// </summary>
    /// <param name="assemblyName">The name of the assembly to load.</param>
    /// <returns>The loaded assembly, or null if resolution should be deferred to the default context.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Plugin assembly loading requires dynamic loading")]
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // For isolation, only load plugin dependencies from plugin directory
        if (_enableIsolation)
        {
            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            // Don't load system assemblies in isolated context
            if (IsSystemAssembly(assemblyName))
            {
                return null; // Let default context handle it
            }
        }
        else
        {
            // Non-isolated: try to resolve dependencies first
            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }
        }

        return null;
    }

    /// <summary>
    /// Loads an unmanaged DLL given its name.
    /// Resolves native dependencies required by the plugin.
    /// </summary>
    /// <param name="unmanagedDllName">The name of the unmanaged DLL to load.</param>
    /// <returns>A handle to the loaded library, or IntPtr.Zero if not found.</returns>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Determines if an assembly is a system assembly.
    /// System assemblies are shared across all contexts and should not be isolated.
    /// </summary>
    /// <param name="assemblyName">The assembly name to check.</param>
    /// <returns>True if the assembly is a system assembly, false otherwise.</returns>
    private static bool IsSystemAssembly(AssemblyName assemblyName)
    {
        var name = assemblyName.Name?.ToUpperInvariant();
        return name != null && (
            name.StartsWith("system.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("microsoft.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Sets up weak reference tracking for the context to enable IsAlive checks.
    /// </summary>
    public void InitializeWeakReference() => _weakReference = new WeakReference(this);
}