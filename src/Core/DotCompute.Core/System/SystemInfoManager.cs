// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.System;

/// <summary>
/// Production-grade system information manager with platform-specific
/// memory detection, CPU information, and resource monitoring.
/// </summary>
public sealed partial class SystemInfoManager
{
    private readonly ILogger<SystemInfoManager> _logger;
    private readonly Timer _monitoringTimer;
    private SystemInfo? _cachedInfo;
    private volatile bool _isMonitoring;

    public SystemInfoManager(ILogger<SystemInfoManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _monitoringTimer = new Timer(UpdateSystemInfo, null, Timeout.Infinite, Timeout.Infinite);
        
        // Get initial system info
        RefreshSystemInfo();
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
            return;
        
        _isMonitoring = true;
        _monitoringTimer.Change(TimeSpan.Zero, interval);
        _logger.LogInformation("Started system monitoring with {Interval}s interval", interval.TotalSeconds);
    }

    /// <summary>
    /// Stops system monitoring.
    /// </summary>
    public void StopMonitoring()
    {
        if (!_isMonitoring)
            return;
        
        _isMonitoring = false;
        _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _logger.LogInformation("Stopped system monitoring");
    }

    /// <summary>
    /// Refreshes system information.
    /// </summary>
    public SystemInfo RefreshSystemInfo()
    {
        try
        {
            var info = new SystemInfo
            {
                Platform = GetPlatform(),
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                OSDescription = RuntimeInformation.OSDescription,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                ProcessorCount = Environment.ProcessorCount,
                MachineName = Environment.MachineName,
                Is64BitOS = Environment.Is64BitOperatingSystem,
                Is64BitProcess = Environment.Is64BitProcess,
                Timestamp = DateTimeOffset.UtcNow
            };

            // Get memory information
            var memInfo = GetMemoryInfo();
            info.TotalPhysicalMemory = memInfo.Total;
            info.AvailablePhysicalMemory = memInfo.Available;
            info.UsedPhysicalMemory = memInfo.Used;
            info.MemoryUsagePercentage = memInfo.UsagePercentage;
            
            // Get virtual memory info
            var virtMemInfo = GetVirtualMemoryInfo();
            info.TotalVirtualMemory = virtMemInfo.Total;
            info.AvailableVirtualMemory = virtMemInfo.Available;
            
            // Get CPU information
            var cpuInfo = GetCpuInfo();
            info.CpuName = cpuInfo.Name;
            info.CpuFrequencyMHz = cpuInfo.FrequencyMHz;
            info.CpuCores = cpuInfo.PhysicalCores;
            info.CpuThreads = cpuInfo.LogicalCores;
            info.CpuUsagePercentage = cpuInfo.UsagePercentage;
            info.CpuArchitecture = cpuInfo.Architecture;
            info.CpuFeatures = cpuInfo.Features;
            
            // Get process information
            using var process = Process.GetCurrentProcess();
            info.ProcessMemory = process.WorkingSet64;
            info.ProcessVirtualMemory = process.VirtualMemorySize64;
            info.ProcessThreadCount = process.Threads.Count;
            info.ProcessHandleCount = process.HandleCount;
            info.ProcessUptime = DateTimeOffset.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
            
            // Get disk information
            var diskInfo = GetDiskInfo();
            info.DiskSpaceTotal = diskInfo.Total;
            info.DiskSpaceAvailable = diskInfo.Available;
            info.DiskSpaceUsed = diskInfo.Used;
            
            _cachedInfo = info;
            
            LogSystemInfo(info);
            
            return info;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system information");
            return _cachedInfo ?? new SystemInfo();
        }
    }

    /// <summary>
    /// Gets platform type.
    /// </summary>
    private static PlatformType GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return PlatformType.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return PlatformType.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return PlatformType.MacOS;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            return PlatformType.FreeBSD;
        
        return PlatformType.Unknown;
    }

    /// <summary>
    /// Gets memory information based on platform.
    /// </summary>
    private MemoryInfo GetMemoryInfo()
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
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
                using var collection = searcher.Get();
                
                foreach (ManagementObject mo in collection)
                {
                    info.Total = Convert.ToInt64(mo["TotalVisibleMemorySize"]) * 1024;
                    info.Available = Convert.ToInt64(mo["FreePhysicalMemory"]) * 1024;
                    info.Used = info.Total - info.Available;
                    info.UsagePercentage = (double)info.Used / info.Total * 100;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Windows memory info via WMI, using fallback");
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
                return GetFallbackMemoryInfo();
            
            var lines = File.ReadAllLines("/proc/meminfo");
            var memValues = new Dictionary<string, long>();
            
            foreach (var line in lines)
            {
                var match = MemInfoRegex().Match(line);
                if (match.Success)
                {
                    var key = match.Groups[1].Value;
                    var value = long.Parse(match.Groups[2].Value) * 1024; // Convert KB to bytes
                    memValues[key] = value;
                }
            }
            
            if (memValues.TryGetValue("MemTotal", out var total))
                info.Total = total;
            
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
            _logger.LogWarning(ex, "Failed to read /proc/meminfo, using fallback");
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
                if (line.Contains("page size of"))
                {
                    var match = Regex.Match(line, @"(\d+) bytes");
                    if (match.Success)
                        pageSize = long.Parse(match.Groups[1].Value);
                }
                else if (line.Contains("Pages free:"))
                {
                    freePages = ParseMacOSPages(line);
                }
                else if (line.Contains("Pages inactive:"))
                {
                    inactivePages = ParseMacOSPages(line);
                }
                else if (line.Contains("Pages active:"))
                {
                    activePages = ParseMacOSPages(line);
                }
                else if (line.Contains("Pages wired down:"))
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
            _logger.LogWarning(ex, "Failed to get macOS memory info, using fallback");
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
            _logger.LogWarning(ex, "Failed to get virtual memory info");
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
            _logger.LogWarning(ex, "Failed to get detailed CPU info");
        }
        
        return info;
    }

    /// <summary>
    /// Gets Windows CPU information.
    /// </summary>
    private void GetWindowsCpuInfo(CpuInfo info)
    {
        if (!OperatingSystem.IsWindows())
            return;
        
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            using var collection = searcher.Get();
            
            foreach (ManagementObject mo in collection)
            {
                info.Name = mo["Name"]?.ToString() ?? "Unknown";
                info.FrequencyMHz = Convert.ToInt32(mo["MaxClockSpeed"]);
                info.PhysicalCores = Convert.ToInt32(mo["NumberOfCores"]);
                info.Architecture = mo["Architecture"]?.ToString() ?? "Unknown";
                break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Windows CPU info via WMI");
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
                return;
            
            var lines = File.ReadAllLines("/proc/cpuinfo");
            var processors = new HashSet<string>();
            var physicalIds = new HashSet<string>();
            
            foreach (var line in lines)
            {
                if (line.StartsWith("model name"))
                {
                    info.Name = line.Split(':', 2)[1].Trim();
                }
                else if (line.StartsWith("cpu MHz"))
                {
                    if (double.TryParse(line.Split(':', 2)[1].Trim(), out var mhz))
                        info.FrequencyMHz = (int)mhz;
                }
                else if (line.StartsWith("processor"))
                {
                    processors.Add(line.Split(':', 2)[1].Trim());
                }
                else if (line.StartsWith("physical id"))
                {
                    physicalIds.Add(line.Split(':', 2)[1].Trim());
                }
                else if (line.StartsWith("flags") && string.IsNullOrEmpty(info.Features))
                {
                    var flags = line.Split(':', 2)[1].Trim().Split(' ');
                    info.Features = string.Join(", ", flags.Take(10)); // Take first 10 features
                }
            }
            
            info.PhysicalCores = physicalIds.Count > 0 ? physicalIds.Count : processors.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read /proc/cpuinfo");
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
                info.Name = brand.Trim();
            
            var coreCount = ExecuteCommand("sysctl", "-n hw.physicalcpu");
            if (int.TryParse(coreCount.Trim(), out var cores))
                info.PhysicalCores = cores;
            
            var freq = ExecuteCommand("sysctl", "-n hw.cpufrequency_max");
            if (long.TryParse(freq.Trim(), out var frequency))
                info.FrequencyMHz = (int)(frequency / 1_000_000);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get macOS CPU info");
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
            
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1000);
            
            return output;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to execute command: {Command} {Args}", command, arguments);
            return string.Empty;
        }
    }

    /// <summary>
    /// Parses macOS vm_stat page count.
    /// </summary>
    private static long ParseMacOSPages(string line)
    {
        var match = Regex.Match(line, @"(\d+)\.");
        return match.Success ? long.Parse(match.Groups[1].Value) : 0;
    }

    /// <summary>
    /// Updates system information (timer callback).
    /// </summary>
    private void UpdateSystemInfo(object? state)
    {
        try
        {
            RefreshSystemInfo();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating system information");
        }
    }

    /// <summary>
    /// Logs system information.
    /// </summary>
    private void LogSystemInfo(SystemInfo info)
    {
        _logger.LogInformation(
            "System Info - Platform: {Platform}, CPU: {CPU} ({Cores} cores), " +
            "Memory: {Memory:F2}GB ({Usage:F1}% used), Process: {ProcessMem:F2}MB",
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
    /// Memory information.
    /// </summary>
    private struct MemoryInfo
    {
        public long Total;
        public long Available;
        public long Used;
        public double UsagePercentage;
    }

    /// <summary>
    /// Virtual memory information.
    /// </summary>
    private struct VirtualMemoryInfo
    {
        public long Total;
        public long Available;
    }

    /// <summary>
    /// CPU information.
    /// </summary>
    private sealed class CpuInfo
    {
        public string Name { get; set; } = "Unknown";
        public int FrequencyMHz { get; set; }
        public int PhysicalCores { get; set; }
        public int LogicalCores { get; set; }
        public double UsagePercentage { get; set; }
        public string Architecture { get; set; } = "Unknown";
        public string Features { get; set; } = string.Empty;
    }

    /// <summary>
    /// Disk information.
    /// </summary>
    private struct DiskInfo
    {
        public long Total;
        public long Available;
        public long Used;
    }
}

/// <summary>
/// Platform types.
/// </summary>
public enum PlatformType
{
    Unknown,
    Windows,
    Linux,
    MacOS,
    FreeBSD
}

/// <summary>
/// System information snapshot.
/// </summary>
public sealed class SystemInfo
{
    // Platform
    public PlatformType Platform { get; init; }
    public string Architecture { get; init; } = string.Empty;
    public string OSDescription { get; init; } = string.Empty;
    public string FrameworkDescription { get; init; } = string.Empty;
    public int ProcessorCount { get; init; }
    public string MachineName { get; init; } = string.Empty;
    public bool Is64BitOS { get; init; }
    public bool Is64BitProcess { get; init; }
    
    // Memory
    public long TotalPhysicalMemory { get; init; }
    public long AvailablePhysicalMemory { get; init; }
    public long UsedPhysicalMemory { get; init; }
    public double MemoryUsagePercentage { get; init; }
    public long TotalVirtualMemory { get; init; }
    public long AvailableVirtualMemory { get; init; }
    
    // CPU
    public string CpuName { get; init; } = string.Empty;
    public int CpuFrequencyMHz { get; init; }
    public int CpuCores { get; init; }
    public int CpuThreads { get; init; }
    public double CpuUsagePercentage { get; init; }
    public string CpuArchitecture { get; init; } = string.Empty;
    public string CpuFeatures { get; init; } = string.Empty;
    
    // Process
    public long ProcessMemory { get; init; }
    public long ProcessVirtualMemory { get; init; }
    public int ProcessThreadCount { get; init; }
    public int ProcessHandleCount { get; init; }
    public TimeSpan ProcessUptime { get; init; }
    
    // Disk
    public long DiskSpaceTotal { get; init; }
    public long DiskSpaceAvailable { get; init; }
    public long DiskSpaceUsed { get; init; }
    
    // Timestamp
    public DateTimeOffset Timestamp { get; init; }
}