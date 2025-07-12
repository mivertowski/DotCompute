using System.Runtime.InteropServices;

namespace DotCompute.Backends.Metal.Native;

/// <summary>
/// Attribute for specifying DLL import search paths for Metal native library.
/// </summary>
internal static class MetalLibraryImportAttribute
{
    public const DllImportSearchPath SearchPath = DllImportSearchPath.SafeDirectories | DllImportSearchPath.AssemblyDirectory;
}