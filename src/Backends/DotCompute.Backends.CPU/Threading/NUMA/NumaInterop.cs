// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CPU.Threading.NUMA;

/// <summary>
/// Platform interop declarations for NUMA operations.
/// </summary>
internal static class NumaInterop
{
    #region Windows Native API Declarations

    [StructLayout(LayoutKind.Sequential)]
    internal struct GROUP_AFFINITY
    {
        /// <summary>
        /// The mask.
        /// </summary>
        public ulong Mask;
        /// <summary>
        /// The group.
        /// </summary>
        public ushort Group;
        /// <summary>
        /// The reserved1.
        /// </summary>
        public ushort Reserved1;
        /// <summary>
        /// The reserved2.
        /// </summary>
        public ushort Reserved2;
        /// <summary>
        /// The reserved3.
        /// </summary>
        public ushort Reserved3;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_INFO
    {
        /// <summary>
        /// The processor architecture.
        /// </summary>
        public ushort ProcessorArchitecture;
        /// <summary>
        /// The reserved.
        /// </summary>
        public ushort Reserved;
        /// <summary>
        /// The page size.
        /// </summary>
        public uint PageSize;
        /// <summary>
        /// The minimum application address.
        /// </summary>
        public IntPtr MinimumApplicationAddress;
        /// <summary>
        /// The maximum application address.
        /// </summary>
        public IntPtr MaximumApplicationAddress;
        /// <summary>
        /// The active processor mask.
        /// </summary>
        public IntPtr ActiveProcessorMask;
        /// <summary>
        /// The number of processors.
        /// </summary>
        public uint NumberOfProcessors;
        /// <summary>
        /// The processor type.
        /// </summary>
        public uint ProcessorType;
        /// <summary>
        /// The allocation granularity.
        /// </summary>
        public uint AllocationGranularity;
        /// <summary>
        /// The processor level.
        /// </summary>
        public ushort ProcessorLevel;
        /// <summary>
        /// The processor revision.
        /// </summary>
        public ushort ProcessorRevision;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern bool GetNumaNodeProcessorMaskEx(ushort Node, out GROUP_AFFINITY ProcessorMask);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern bool GetNumaHighestNodeNumber(out uint HighestNodeNumber);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern bool GetNumaNodeProcessorMask(byte Node, out ulong ProcessorMask);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern bool GetNumaAvailableMemoryNodeEx(ushort Node, out ulong AvailableBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern bool GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern bool SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern bool SetProcessAffinityMask(IntPtr hProcess, UIntPtr dwProcessAffinityMask);

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern IntPtr GetCurrentProcess();

    #endregion

    #region Linux libnuma Integration

    private static bool _libnumaLoaded;
    private static bool _libnumaAvailable;

    internal static bool TryLoadLibnuma()
    {
        if (_libnumaLoaded)
        {
            return _libnumaAvailable;
        }

        try
        {
            // Try to load libnuma
            _libnumaAvailable = numa_available() != -1;
        }
        catch
        {
            _libnumaAvailable = false;
        }
        finally
        {
            _libnumaLoaded = true;
        }

        return _libnumaAvailable;
    }

    [DllImport("numa", EntryPoint = "numa_available")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int numa_available();

    [DllImport("numa", EntryPoint = "numa_max_node")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int numa_max_node();

    [DllImport("numa", EntryPoint = "numa_distance")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int numa_distance(int from, int to);

    [DllImport("numa", EntryPoint = "numa_node_size")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern long numa_node_size(int node, out long freep);

    [DllImport("numa", EntryPoint = "numa_set_bind_policy")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void numa_set_bind_policy(int strict);

    [DllImport("numa", EntryPoint = "numa_bind")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void numa_bind(IntPtr nodemask);

    [DllImport("numa", EntryPoint = "numa_run_on_node")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int numa_run_on_node(int node);

    [DllImport("numa", EntryPoint = "numa_run_on_node_mask")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int numa_run_on_node_mask(IntPtr nodemask);

    [DllImport("numa", EntryPoint = "numa_alloc_onnode")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr numa_alloc_onnode(ulong size, int node);

    [DllImport("numa", EntryPoint = "numa_free")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void numa_free(IntPtr ptr, ulong size);

    [DllImport("numa", EntryPoint = "numa_set_preferred")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void numa_set_preferred(int node);

    #endregion

    #region Cross-Platform Helpers

    /// <summary>
    /// Checks if NUMA library is available on the current platform.
    /// </summary>
    internal static bool IsNumaLibraryAvailable()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        try
        {
            return numa_available() != -1;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the highest NUMA node number on Windows.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    internal static int GetNumaHighestNodeNumber()
        => GetNumaHighestNodeNumber(out var highest) ? (int)highest : 0;

    /// <summary>
    /// Gets NUMA node memory size on Windows.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    internal static long GetNumaNodeMemorySize(int nodeId)
    {
        if (GetNumaAvailableMemoryNodeEx((ushort)nodeId, out var availableBytes))
        {
            return (long)availableBytes;
        }
        return 0;
    }

    /// <summary>
    /// Gets page size from system info.
    /// </summary>
    internal static int GetPageSize()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (GetSystemInfo(out var sysInfo))
            {
                return (int)sysInfo.PageSize;
            }
        }
        return NumaConstants.Sizes.PageSize; // Default
    }

    /// <summary>
    /// Sets thread affinity to specific processors.
    /// </summary>
    internal static bool SetThreadAffinity(IntPtr threadHandle, ulong affinityMask)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return SetThreadAffinityMask(threadHandle, new UIntPtr(affinityMask));
        }

        // Linux implementation would use sched_setaffinity
        return false;
    }

    /// <summary>
    /// Sets process affinity to specific processors.
    /// </summary>
    internal static bool SetProcessAffinity(IntPtr processHandle, ulong affinityMask)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return SetProcessAffinityMask(processHandle, new UIntPtr(affinityMask));
        }

        // Linux implementation would use sched_setaffinity
        return false;
    }

    /// <summary>
    /// Runs the current thread on a specific NUMA node.
    /// </summary>
    internal static bool RunOnNode(int nodeId)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && IsNumaLibraryAvailable())
        {
            return numa_run_on_node(nodeId) == 0;
        }

        return false;
    }

    /// <summary>
    /// Allocates memory on a specific NUMA node.
    /// </summary>
    internal static IntPtr AllocateOnNode(nuint size, int nodeId)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && IsNumaLibraryAvailable())
        {
            return numa_alloc_onnode(size, nodeId);
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Frees NUMA-allocated memory.
    /// </summary>
    internal static void FreeNumaMemory(IntPtr ptr, nuint size)
    {
        if (ptr != IntPtr.Zero && RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && IsNumaLibraryAvailable())
        {
            numa_free(ptr, size);
        }
    }

    /// <summary>
    /// Sets the preferred NUMA node for memory allocations.
    /// </summary>
    internal static void SetPreferredNode(int nodeId)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && IsNumaLibraryAvailable())
        {
            numa_set_preferred(nodeId);
        }
    }

    #endregion
}