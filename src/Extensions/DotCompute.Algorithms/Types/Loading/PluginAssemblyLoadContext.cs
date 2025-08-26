// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using System.Runtime.Loader;

namespace DotCompute.Algorithms.Types.Loading;

/// <summary>
/// Represents an assembly load context for plugin isolation.
/// </summary>
public sealed class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginAssemblyLoadContext"/> class.
    /// </summary>
    /// <param name="name">The name of the load context.</param>
    /// <param name="pluginPath">The path to the main plugin assembly.</param>
    /// <param name="isCollectible">A value that indicates whether the assembly load context is collectible.</param>
    public PluginAssemblyLoadContext(string name, string pluginPath, bool isCollectible = true)
        : base(name, isCollectible)
    {
        _pluginPath = pluginPath ?? throw new ArgumentNullException(nameof(pluginPath));
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    /// <summary>
    /// Gets the path to the main plugin assembly.
    /// </summary>
    public string PluginPath => _pluginPath;

    /// <inheritdoc/>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Check if this is a core framework assembly or shared dependency
        // Let the default load context handle these
        if (IsSharedAssembly(assemblyName))
        {
            return null; // Use default load context
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    /// <inheritdoc/>
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
    /// Determines if an assembly should be loaded in the shared context.
    /// </summary>
    /// <param name="assemblyName">The assembly name to check.</param>
    /// <returns>True if the assembly should use the shared context; otherwise, false.</returns>
    private static bool IsSharedAssembly(AssemblyName assemblyName)
    {
        var name = assemblyName.Name ?? string.Empty;
        
        // Core .NET assemblies
        if (name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Microsoft.Extensions.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Microsoft.NET.", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("netstandard", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // DotCompute shared assemblies
        if (name.StartsWith("DotCompute.Core", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("DotCompute.Abstractions", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("DotCompute.Memory", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}