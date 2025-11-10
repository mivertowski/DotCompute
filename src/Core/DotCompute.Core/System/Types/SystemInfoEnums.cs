// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.System;

/// <summary>
/// Operating system platform types supported by DotCompute.
/// </summary>
/// <remarks>
/// <para>
/// Identifies the underlying operating system platform for platform-specific
/// system information gathering and resource management. Used by
/// <see cref="SystemInfoManager"/> to select appropriate platform APIs.
/// </para>
/// <list type="bullet">
/// <item><b>Windows</b>: WMI (Windows Management Instrumentation) APIs</item>
/// <item><b>Linux</b>: /proc filesystem, sysctl, sysconf</item>
/// <item><b>macOS</b>: sysctl, vm_stat, system_profiler</item>
/// <item><b>FreeBSD</b>: sysctl, swapinfo</item>
/// </list>
/// </remarks>
public enum PlatformType
{
    /// <summary>
    /// Unknown or unsupported platform.
    /// </summary>
    /// <remarks>
    /// Returned when platform detection fails or when running on an unsupported OS.
    /// System information collection will use basic .NET APIs only.
    /// </remarks>
    Unknown,

    /// <summary>
    /// Microsoft Windows operating system.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Includes all Windows versions: 10, 11, Server 2016+, etc.
    /// System information collected via WMI queries when available.
    /// </para>
    /// <para><b>APIs</b>: GlobalMemoryStatusEx, GetSystemInfo, WMI queries</para>
    /// </remarks>
    Windows,

    /// <summary>
    /// Linux operating system.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Includes all Linux distributions (Ubuntu, CentOS, Debian, etc.).
    /// System information read from /proc filesystem.
    /// </para>
    /// <para><b>Sources</b>: /proc/meminfo, /proc/cpuinfo, /proc/stat, sysconf</para>
    /// </remarks>
    Linux,

    /// <summary>
    /// Apple macOS operating system.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Includes macOS 10.15+ (Catalina and later), both Intel and Apple Silicon.
    /// System information collected via sysctl and vm_stat commands.
    /// </para>
    /// <para><b>Commands</b>: sysctl, vm_stat, system_profiler</para>
    /// </remarks>
    MacOS,

    /// <summary>
    /// FreeBSD operating system.
    /// </summary>
    /// <remarks>
    /// <para>
    /// FreeBSD Unix-like operating system. System information collected
    /// via sysctl and swapinfo.
    /// </para>
    /// <para><b>Commands</b>: sysctl, swapinfo</para>
    /// </remarks>
    FreeBSD
}
