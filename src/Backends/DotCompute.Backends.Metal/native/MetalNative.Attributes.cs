// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.InteropServices;

namespace DotCompute.Backends.Metal.Native;


/// <summary>
/// Attribute for specifying DLL import search paths for Metal native library.
/// </summary>
internal static class MetalLibraryImportAttribute
{
    public const DllImportSearchPath SearchPath = DllImportSearchPath.SafeDirectories | DllImportSearchPath.AssemblyDirectory;
}
