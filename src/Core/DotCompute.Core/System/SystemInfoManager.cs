// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using DotCompute.Core.Logging;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.System;

/// <summary>
/// Production-grade system information manager with platform-specific
/// memory detection, CPU information, and resource monitoring.
/// </summary>
public sealed partial class SystemInfoManager : IDisposable
{
    private readonly ILogger<SystemInfoManager> _logger;
    private readonly Timer _monitoringTimer;
    private SystemInfo? _cachedInfo;
    private volatile bool _isMonitoring;

    [GeneratedRegex(@"(\d+) bytes")]
    private static partial Regex PageSizeBytesRegex();

    [GeneratedRegex(@"(\d+)\.")]
    private static partial Regex MacOSPagesRegex();

    // LoggerMessage delegates for high-performance logging (Event ID range: 17000-17099)
    private static readonly Action<ILogger, Exception?> _logWindowsMemoryWarning =
        LoggerMessage.Define(
            MsLogLevel.Warning,
            new EventId(17000, "WindowsMemoryWarning"),
            "Failed to get Windows memory info via WMI, using fallback");

    private static readonly Action<ILogger, Exception?> _logLinuxMemoryWarning =
        LoggerMessage.Define(
            MsLogLevel.Warning,
            new EventId(17001, "LinuxMemoryWarning"),
            "Failed to read /proc/meminfo, using fallback");

    private static readonly Action<ILogger, Exception?> _logMacOSMemoryWarning =
        LoggerMessage.Define(
            MsLogLevel.Warning,
            new EventId(17002, "MacOSMemoryWarning"),
            "Failed to get macOS memory info, using fallback");

    private static readonly Action<ILogger, Exception?> _logVirtualMemoryWarning =
        LoggerMessage.Define(
            MsLogLevel.Warning,
            new EventId(17003, "VirtualMemoryWarning"),
            "Failed to get virtual memory info");

    private static readonly Action<ILogger, Exception?> _logCpuInfoWarning =
        LoggerMessage.Define(
            MsLogLevel.Warning,
            new EventId(17004, "CpuInfoWarning"),
            "Failed to get detailed CPU info");

    private static readonly Action<ILogger, Exception?> _logWindowsCpuWarning =
        LoggerMessage.Define(
            MsLogLevel.Warning,
            new EventId(17005, "WindowsCpuWarning"),
            "Failed to get Windows CPU info via WMI");

    private static readonly Action<ILogger, Exception?> _logLinuxCpuWarning =
        LoggerMessage.Define(
            MsLogLevel.Warning,
            new EventId(17006, "LinuxCpuWarning"),
            "Failed to read /proc/cpuinfo");

    private static readonly Action<ILogger, Exception?> _logMacOSCpuWarning =
        LoggerMessage.Define(
            MsLogLevel.Warning,
            new EventId(17007, "MacOSCpuWarning"),
            "Failed to get macOS CPU info");

    private static readonly Action<ILogger, string, string, Exception?> _logCommandExecutionDebug =
        LoggerMessage.Define<string, string>(
            MsLogLevel.Debug,
            new EventId(17008, "CommandExecutionDebug"),
            "Failed to execute command: {Command} {Args}");

    private static readonly Action<ILogger, PlatformType, string, int, double, double, double, Exception?> _logSystemInfo =
        LoggerMessage.Define<PlatformType, string, int, double, double, double>(
            MsLogLevel.Information,
            new EventId(17009, nameof(SystemInfo)),
            "System Info - Platform: {Platform}, CPU: {CPU} ({Cores} cores), Memory: {Memory:F2}GB ({Usage:F1}% used), Process: {ProcessMem:F2}MB");

    // Wrapper methods for the delegates
    private static void LogWindowsMemoryWarning(ILogger logger, Exception ex)
        => _logWindowsMemoryWarning(logger, ex);

    private static void LogLinuxMemoryWarning(ILogger logger, Exception ex)
        => _logLinuxMemoryWarning(logger, ex);

    private static void LogMacOSMemoryWarning(ILogger logger, Exception ex)
        => _logMacOSMemoryWarning(logger, ex);

    private static void LogVirtualMemoryWarning(ILogger logger, Exception ex)
        => _logVirtualMemoryWarning(logger, ex);

    private static void LogCpuInfoWarning(ILogger logger, Exception ex)
        => _logCpuInfoWarning(logger, ex);

    private static void LogWindowsCpuWarning(ILogger logger, Exception ex)
        => _logWindowsCpuWarning(logger, ex);

    private static void LogLinuxCpuWarning(ILogger logger, Exception ex)
        => _logLinuxCpuWarning(logger, ex);

    private static void LogMacOSCpuWarning(ILogger logger, Exception ex)
        => _logMacOSCpuWarning(logger, ex);

    private static void LogCommandExecutionDebug(ILogger logger, string command, string args, Exception ex)
        => _logCommandExecutionDebug(logger, command, args, ex);

    private static void LogSystemInfoMessage(ILogger logger, PlatformType platform, string cpu, int cores, double memory, double usage, double processMem)
        => _logSystemInfo(logger, platform, cpu, cores, memory, usage, processMem, null);

    /// <summary>
    /// Initializes a new instance of the SystemInfoManager class.
    /// </summary>
    /// <param name="logger">The logger.</param>

    public SystemInfoManager(ILogger<SystemInfoManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _monitoringTimer = new Timer(UpdateSystemInfo, null, Timeout.Infinite, Timeout.Infinite);

        // Get initial system info
        _ = RefreshSystemInfo();
    }

    /// <summary>
    /// Gets current system information.
    /// </summary>
    public SystemInfo CurrentInfo => _cachedInfo ?? RefreshSystemInfo();

    /// <summary>
    /// Starts continuous monitoring of system resources.
    /// </summary>
    public void StartMonitoring(TimeSpan interval)
    {
        if (_isMonitoring)
        {
            return;
        }


        _isMonitoring = true;
        _ = _monitoringTimer.Change(TimeSpan.Zero, interval);
        _logger.LogInfoMessage("Started system monitoring with {interval.TotalSeconds}s interval");
    }

    /// <summary>
    /// Stops system monitoring.
    /// </summary>
    public void StopMonitoring()
    {
        if (!_isMonitoring)
        {
            return;
        }


        _isMonitoring = false;
        _ = _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _logger.LogInfoMessage("Stopped system monitoring");
    }

    /// <summary>
    /// Refreshes system information.
    /// </summary>
    public SystemInfo RefreshSystemInfo()
    {
        try
        {
            // Get all information first
            var memInfo = GetPhysicalMemoryInfo();
            var virtMemInfo = GetVirtualMemoryInfo();
            var cpuInfo = GetCpuInfo();
            var diskInfo = GetDiskInfo();

            // Get process information

            using var process = Process.GetCurrentProcess();

            // Create SystemInfo with all properties initialized

            var info = new SystemInfo
            {
                // Platform
                Platform = GetPlatform(),
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                OSDescription = RuntimeInformation.OSDescription,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                ProcessorCount = Environment.ProcessorCount,
                MachineName = Environment.MachineName,
                Is64BitOS = Environment.Is64BitOperatingSystem,
                Is64BitProcess = Environment.Is64BitProcess,
                Timestamp = DateTimeOffset.UtcNow,

                // Memory

                TotalPhysicalMemory = memInfo.Total,
                AvailablePhysicalMemory = memInfo.Available,
                UsedPhysicalMemory = memInfo.Used,
                MemoryUsagePercentage = memInfo.UsagePercentage,

                // Virtual Memory

                TotalVirtualMemory = virtMemInfo.Total,
                AvailableVirtualMemory = virtMemInfo.Available,

                // CPU

                CpuName = cpuInfo.Name,
                CpuFrequencyMHz = cpuInfo.FrequencyMHz,
                CpuCores = cpuInfo.PhysicalCores,
                CpuThreads = cpuInfo.LogicalCores,
                CpuUsagePercentage = cpuInfo.UsagePercentage,
                CpuArchitecture = cpuInfo.Architecture,
                CpuFeatures = cpuInfo.Features,

                // Process

                ProcessMemory = process.WorkingSet64,
                ProcessVirtualMemory = process.VirtualMemorySize64,
                ProcessThreadCount = process.Threads.Count,
                ProcessHandleCount = process.HandleCount,
                ProcessUptime = DateTimeOffset.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime(),

                // Disk

                DiskSpaceTotal = diskInfo.Total,
                DiskSpaceAvailable = diskInfo.Available,
                DiskSpaceUsed = diskInfo.Used
            };


            _cachedInfo = info;


            LogSystemInfo(info);


            return info;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to get system information");
            return _cachedInfo ?? new SystemInfo();
        }
    }

    /// <summary>
    /// Gets platform type.
    /// </summary>
    private static PlatformType GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {

            return PlatformType.Windows;
        }


        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {

            return PlatformType.Linux;
        }


        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {

            return PlatformType.MacOS;
        }


        if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
        {

            return PlatformType.FreeBSD;
        }


        return PlatformType.Unknown;
    }

    /// <summary>
    /// Gets detailed memory information for the current system.
    /// </summary>
    /// <returns>A <see cref="SystemMemoryInfo"/> object containing detailed memory statistics.</returns>
    /// <remarks>
    /// This method provides comprehensive memory information including physical memory,
    /// virtual memory, and memory usage statistics. The information is gathered
    /// using platform-specific methods for accuracy.
    /// </remarks>
    public SystemMemoryInfo GetMemoryInfo()
    {
        var physicalMemory = GetPhysicalMemoryInfo();
        var virtualMemory = GetVirtualMemoryInfo();


        return new SystemMemoryInfo
        {
            // Physical memory
            TotalPhysicalMemory = physicalMemory.Total,
            AvailablePhysicalMemory = physicalMemory.Available,
            UsedPhysicalMemory = physicalMemory.Used,
            PhysicalMemoryUsagePercentage = physicalMemory.UsagePercentage,

            // Virtual memory

            TotalVirtualMemory = virtualMemory.Total,
            AvailableVirtualMemory = virtualMemory.Available,

            // Process memory

            ProcessMemoryUsage = Process.GetCurrentProcess().WorkingSet64,
            ProcessVirtualMemoryUsage = Process.GetCurrentProcess().VirtualMemorySize64,

            // GC information

            GCTotalMemory = GC.GetTotalMemory(false),
            GCGen0Collections = GC.CollectionCount(0),
            GCGen1Collections = GC.CollectionCount(1),
            GCGen2Collections = GC.CollectionCount(2),

            // Timestamp

            Timestamp = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Gets physical memory information based on platform.
    /// </summary>
    private MemoryInfo GetPhysicalMemoryInfo()
    {
        var platform = GetPlatform();


        return platform switch
        {
            PlatformType.Windows => GetWindowsMemoryInfo(),
            PlatformType.Linux => GetLinuxMemoryInfo(),
            PlatformType.MacOS => GetMacOSMemoryInfo(),
            _ => GetFallbackMemoryInfo()
        };
    }

    /// <summary>
    /// Gets Windows memory information using WMI.
    /// </summary>
    private MemoryInfo GetWindowsMemoryInfo()
    {
        var info = new MemoryInfo();


        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Use dynamic loading for Windows Management Instrumentation
                // This avoids compile-time dependency on System.Management
                var wmiType = Type.GetType("System.Management.ManagementObjectSearcher, System.Management, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                if (wmiType != null)
                {
                    dynamic? searcher = Activator.CreateInstance(wmiType, "SELECT * FROM Win32_OperatingSystem");
                    dynamic? collection = searcher?.Get();
                    if (collection == null)
                    {
                        return info;
                    }


                    foreach (var mo in collection)
                    {
                        info.Total = Convert.ToInt64(CultureInfo.InvariantCulture, mo["TotalVisibleMemorySize"]) * 1024;
                        info.Available = Convert.ToInt64(CultureInfo.InvariantCulture, mo["FreePhysicalMemory"]) * 1024;
                        info.Used = info.Total - info.Available;
                        info.UsagePercentage = (double)info.Used / info.Total * 100;
                        break;
                    }


                    collection?.Dispose();
                    searcher?.Dispose();
                }
                else
                {
                    _logger.LogWarningMessage("System.Management not available, using fallback");
                    return GetFallbackMemoryInfo();
                }
            }
        }
        catch (Exception ex)
        {
            LogWindowsMemoryWarning(_logger, ex);
            return GetFallbackMemoryInfo();
        }


        return info;
    }

    /// <summary>
    /// Gets Linux memory information from /proc/meminfo.
    /// </summary>
    private MemoryInfo GetLinuxMemoryInfo()
    {
        var info = new MemoryInfo();


        try
        {
            if (!File.Exists("/proc/meminfo"))
            {

                return GetFallbackMemoryInfo();
            }


            var lines = File.ReadAllLines("/proc/meminfo");
            var memValues = new Dictionary<string, long>();


            foreach (var line in lines)
            {
                var match = MemInfoRegex().Match(line);
                if (match.Success)
                {
                    var key = match.Groups[1].Value;
                    var value = long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 1024; // Convert KB to bytes
                    memValues[key] = value;
                }
            }


            if (memValues.TryGetValue("MemTotal", out var total))
            {
                info.Total = total;
            }


            if (memValues.TryGetValue("MemAvailable", out var available))
            {
                info.Available = available;
            }
            else if (memValues.TryGetValue("MemFree", out var free) &&
                     memValues.TryGetValue("Buffers", out var buffers) &&
                     memValues.TryGetValue("Cached", out var cached))
            {
                info.Available = free + buffers + cached;
            }


            info.Used = info.Total - info.Available;
            info.UsagePercentage = info.Total > 0 ? (double)info.Used / info.Total * 100 : 0;
        }
        catch (Exception ex)
        {
            LogLinuxMemoryWarning(_logger, ex);
            return GetFallbackMemoryInfo();
        }


        return info;
    }

    /// <summary>
    /// Gets macOS memory information using vm_stat.
    /// </summary>
    private MemoryInfo GetMacOSMemoryInfo()
    {
        var info = new MemoryInfo();


        try
        {
            // Get total memory using sysctl
            var totalOutput = ExecuteCommand("sysctl", "-n hw.memsize");
            if (long.TryParse(totalOutput.Trim(), out var total))
            {
                info.Total = total;
            }

            // Get memory usage using vm_stat

            var vmStatOutput = ExecuteCommand("vm_stat", "");
            var lines = vmStatOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);


            long pageSize = 4096; // Default page size
            long freePages = 0;
            long inactivePages = 0;
            long activePages = 0;
            long wiredPages = 0;


            foreach (var line in lines)
            {
                if (line.Contains("page size of", StringComparison.OrdinalIgnoreCase))
                {
                    var match = PageSizeBytesRegex().Match(line);
                    if (match.Success)
                    {
                        pageSize = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    }
                }
                else if (line.Contains("Pages free:", StringComparison.OrdinalIgnoreCase))
                {
                    freePages = ParseMacOSPages(line);
                }
                else if (line.Contains("Pages inactive:", StringComparison.Ordinal))
                {
                    inactivePages = ParseMacOSPages(line);
                }
                else if (line.Contains("Pages active:", StringComparison.Ordinal))
                {
                    activePages = ParseMacOSPages(line);
                }
                else if (line.Contains("Pages wired down:", StringComparison.Ordinal))
                {
                    wiredPages = ParseMacOSPages(line);
                }
            }


            info.Available = (freePages + inactivePages) * pageSize;
            info.Used = (activePages + wiredPages) * pageSize;


            if (info.Total == 0 && info.Available > 0 && info.Used > 0)
            {
                info.Total = info.Available + info.Used;
            }


            info.UsagePercentage = info.Total > 0 ? (double)info.Used / info.Total * 100 : 0;
        }
        catch (Exception ex)
        {
            LogMacOSMemoryWarning(_logger, ex);
            return GetFallbackMemoryInfo();
        }


        return info;
    }

    /// <summary>
    /// Gets fallback memory information using GC.
    /// </summary>
    private static MemoryInfo GetFallbackMemoryInfo()
    {
        var info = new MemoryInfo();

        // Use GC memory info as fallback

        var gcMemInfo = GC.GetGCMemoryInfo();
        info.Total = gcMemInfo.TotalAvailableMemoryBytes;
        info.Available = gcMemInfo.TotalAvailableMemoryBytes - gcMemInfo.HeapSizeBytes;
        info.Used = gcMemInfo.HeapSizeBytes;
        info.UsagePercentage = info.Total > 0 ? (double)info.Used / info.Total * 100 : 0;


        return info;
    }

    /// <summary>
    /// Gets virtual memory information.
    /// </summary>
    private VirtualMemoryInfo GetVirtualMemoryInfo()
    {
        var info = new VirtualMemoryInfo();


        try
        {
            using var process = Process.GetCurrentProcess();
            info.Total = process.MaxWorkingSet.ToInt64();
            info.Available = info.Total - process.WorkingSet64;
        }
        catch (Exception ex)
        {
            LogVirtualMemoryWarning(_logger, ex);
        }


        return info;
    }

    /// <summary>
    /// Gets CPU information.
    /// </summary>
    private CpuInfo GetCpuInfo()
    {
        var info = new CpuInfo
        {
            LogicalCores = Environment.ProcessorCount
        };


        var platform = GetPlatform();


        try
        {
            switch (platform)
            {
                case PlatformType.Windows:
                    GetWindowsCpuInfo(info);
                    break;
                case PlatformType.Linux:
                    GetLinuxCpuInfo(info);
                    break;
                case PlatformType.MacOS:
                    GetMacOSCpuInfo(info);
                    break;
            }

            // Get CPU usage

            info.UsagePercentage = GetCpuUsage();
        }
        catch (Exception ex)
        {
            LogCpuInfoWarning(_logger, ex);
        }


        return info;
    }

    /// <summary>
    /// Gets Windows CPU information.
    /// </summary>
    private void GetWindowsCpuInfo(CpuInfo info)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }


        try
        {
            // Use dynamic loading for Windows Management Instrumentation
            var wmiType = Type.GetType("System.Management.ManagementObjectSearcher, System.Management, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            if (wmiType != null)
            {
                dynamic? searcher = Activator.CreateInstance(wmiType, "SELECT * FROM Win32_Processor");
                dynamic? collection = searcher?.Get();
                if (collection == null)
                {
                    return;
                }


                foreach (var mo in collection)
                {
                    info.Name = mo["Name"]?.ToString() ?? "Unknown";
                    info.FrequencyMHz = Convert.ToInt32(mo["MaxClockSpeed"]);
                    info.PhysicalCores = Convert.ToInt32(mo["NumberOfCores"]);
                    info.Architecture = mo["Architecture"]?.ToString() ?? "Unknown";
                    break;
                }


                collection?.Dispose();
                searcher?.Dispose();
            }
            else
            {
                _logger.LogWarningMessage("System.Management not available for CPU info");
            }
        }
        catch (Exception ex)
        {
            LogWindowsCpuWarning(_logger, ex);
        }
    }

    /// <summary>
    /// Gets Linux CPU information from /proc/cpuinfo.
    /// </summary>
    private void GetLinuxCpuInfo(CpuInfo info)
    {
        try
        {
            if (!File.Exists("/proc/cpuinfo"))
            {
                return;
            }


            var lines = File.ReadAllLines("/proc/cpuinfo");
            var processors = new HashSet<string>();
            var physicalIds = new HashSet<string>();


            foreach (var line in lines)
            {
                if (line.StartsWith("model name", StringComparison.Ordinal))
                {
                    info.Name = line.Split(':', 2)[1].Trim();
                }
                else if (line.StartsWith("cpu MHz", StringComparison.Ordinal))
                {
                    if (double.TryParse(line.Split(':', 2)[1].Trim(), out var mhz))
                    {
                        info.FrequencyMHz = (int)mhz;
                    }
                }
                else if (line.StartsWith("processor", StringComparison.Ordinal))
                {
                    _ = processors.Add(line.Split(':', 2)[1].Trim());
                }
                else if (line.StartsWith("physical id", StringComparison.Ordinal))
                {
                    _ = physicalIds.Add(line.Split(':', 2)[1].Trim());
                }
                else if (line.StartsWith("flags", StringComparison.CurrentCulture) && string.IsNullOrEmpty(info.Features))
                {
                    var flags = line.Split(':', 2)[1].Trim().Split(' ');
                    info.Features = string.Join(", ", flags.Take(10)); // Take first 10 features
                }
            }


            info.PhysicalCores = physicalIds.Count > 0 ? physicalIds.Count : processors.Count;
        }
        catch (Exception ex)
        {
            LogLinuxCpuWarning(_logger, ex);
        }
    }

    /// <summary>
    /// Gets macOS CPU information.
    /// </summary>
    private void GetMacOSCpuInfo(CpuInfo info)
    {
        try
        {
            var brand = ExecuteCommand("sysctl", "-n machdep.cpu.brand_string");
            if (!string.IsNullOrWhiteSpace(brand))
            {
                info.Name = brand.Trim();
            }

            var coreCount = ExecuteCommand("sysctl", "-n hw.physicalcpu");
            if (int.TryParse(coreCount.Trim(), out var cores))
            {
                info.PhysicalCores = cores;
            }

            var freq = ExecuteCommand("sysctl", "-n hw.cpufrequency_max");
            if (long.TryParse(freq.Trim(), out var frequency))
            {
                info.FrequencyMHz = (int)(frequency / 1_000_000);
            }
        }
        catch (Exception ex)
        {
            LogMacOSCpuWarning(_logger, ex);
        }
    }

    /// <summary>
    /// Gets current CPU usage percentage.
    /// </summary>
    private static double GetCpuUsage()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var startTime = DateTimeOffset.UtcNow;
            var startCpuUsage = process.TotalProcessorTime;


            Thread.Sleep(100);


            var endTime = DateTimeOffset.UtcNow;
            var endCpuUsage = process.TotalProcessorTime;


            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);


            return Math.Min(100, cpuUsageTotal * 100);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets disk space information.
    /// </summary>
    private static DiskInfo GetDiskInfo()
    {
        var info = new DiskInfo();


        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed);


            foreach (var drive in drives)
            {
                info.Total += drive.TotalSize;
                info.Available += drive.AvailableFreeSpace;
            }


            info.Used = info.Total - info.Available;
        }
        catch (Exception)
        {
            // Ignore disk info errors
        }


        return info;
    }

    /// <summary>
    /// Executes a shell command and returns output.
    /// </summary>
    private string ExecuteCommand(string command, string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _ = process.Start();
            var output = process.StandardOutput.ReadToEnd();
            _ = process.WaitForExit(1000);


            return output;
        }
        catch (Exception ex)
        {
            LogCommandExecutionDebug(_logger, command, arguments, ex);
            return string.Empty;
        }
    }

    /// <summary>
    /// Parses macOS vm_stat page count.
    /// </summary>
    private static long ParseMacOSPages(string line)
    {
        var match = MacOSPagesRegex().Match(line);
        return match.Success ? long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }

    /// <summary>
    /// Updates system information (timer callback).
    /// </summary>
    private void UpdateSystemInfo(object? state)
    {
        try
        {
            _ = RefreshSystemInfo();
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error updating system information");
        }
    }

    /// <summary>
    /// Logs system information.
    /// </summary>
    private void LogSystemInfo(SystemInfo info)
    {
        LogSystemInfoMessage(
            _logger,
            info.Platform,
            info.CpuName,
            info.CpuCores,
            info.TotalPhysicalMemory / (1024.0 * 1024 * 1024),
            info.MemoryUsagePercentage,
            info.ProcessMemory / (1024.0 * 1024));
    }

    [GeneratedRegex(@"^(\w+):\s+(\d+)")]
    private static partial Regex MemInfoRegex();

    /// <summary>
    /// Disposes the SystemInfoManager and stops monitoring.
    /// </summary>
    public void Dispose()
    {
        StopMonitoring();
        _monitoringTimer?.Dispose();
    }

    /// <summary>
    /// Memory information.
    /// </summary>
    /// <summary>
    /// Physical memory information structure.
    /// </summary>
    private struct MemoryInfo
    {
        /// <summary>
        /// Total physical memory available in bytes.
        /// </summary>
        public long Total;
        /// <summary>
        /// Available physical memory in bytes that can be allocated.
        /// </summary>
        public long Available;
        /// <summary>
        /// Used physical memory in bytes (Total - Available).
        /// </summary>
        public long Used;
        /// <summary>
        /// Physical memory usage as a percentage (0-100).
        /// </summary>
        public double UsagePercentage;
    }

    /// <summary>
    /// Virtual memory information structure.
    /// </summary>
    private struct VirtualMemoryInfo
    {
        /// <summary>
        /// Total virtual memory available in bytes (physical + page file).
        /// </summary>
        public long Total;
        /// <summary>
        /// Available virtual memory in bytes that can be allocated.
        /// </summary>
        public long Available;
    }

    /// <summary>
    /// CPU information.
    /// </summary>
    private sealed class CpuInfo
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; set; } = "Unknown";
        /// <summary>
        /// Gets or sets the frequency m hz.
        /// </summary>
        /// <value>The frequency m hz.</value>
        public int FrequencyMHz { get; set; }
        /// <summary>
        /// Gets or sets the physical cores.
        /// </summary>
        /// <value>The physical cores.</value>
        public int PhysicalCores { get; set; }
        /// <summary>
        /// Gets or sets the logical cores.
        /// </summary>
        /// <value>The logical cores.</value>
        public int LogicalCores { get; set; }
        /// <summary>
        /// Gets or sets the usage percentage.
        /// </summary>
        /// <value>The usage percentage.</value>
        public double UsagePercentage { get; set; }
        /// <summary>
        /// Gets or sets the architecture.
        /// </summary>
        /// <value>The architecture.</value>
        public string Architecture { get; set; } = "Unknown";
        /// <summary>
        /// Gets or sets the features.
        /// </summary>
        /// <value>The features.</value>
        public string Features { get; set; } = string.Empty;
    }

    /// <summary>
    /// Disk information.
    /// </summary>
    /// <summary>
    /// Disk storage information structure.
    /// </summary>
    private struct DiskInfo
    {
        /// <summary>
        /// Total disk storage capacity in bytes.
        /// </summary>
        public long Total;
        /// <summary>
        /// Available disk storage space in bytes that can be used.
        /// </summary>
        public long Available;
        /// <summary>
        /// Used disk storage space in bytes (Total - Available).
        /// </summary>
        public long Used;
    }
}
