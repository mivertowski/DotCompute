// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using System.Runtime.Loader;

namespace DotCompute.Plugins.Loaders.NuGet;

/// <summary>
/// Custom AssemblyLoadContext for NuGet-based plugins with proper isolation
/// </summary>
public sealed class NuGetPluginLoadContext(string pluginPath, string contextName, bool isCollectible = true) : AssemblyLoadContext(contextName, isCollectible)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name == null)
            return null;

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);

        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
