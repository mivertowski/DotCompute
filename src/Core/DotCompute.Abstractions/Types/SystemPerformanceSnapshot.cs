// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotCompute.Core.Types;

/// <summary>
/// Comprehensive system performance snapshot capturing CPU, memory, and I/O metrics
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct SystemPerformanceSnapshot : IEquatable<SystemPerformanceSnapshot>
{
    /// <summary>
    /// Timestamp when the snapshot was taken
    /// </summary>
    public readonly DateTimeOffset Timestamp;

    /// <summary>
    /// CPU utilization percentage (0.0 to 100.0)
    /// </summary>
    public readonly double CpuUtilizationPercent;

    /// <summary>
    /// Available physical memory in bytes
    /// </summary>
    public readonly long AvailableMemoryBytes;

    /// <summary>
    /// Total physical memory in bytes
    /// </summary>
    public readonly long TotalMemoryBytes;

    /// <summary>
    /// Used memory in bytes
    /// </summary>
    public readonly long UsedMemoryBytes;

    /// <summary>
    /// Memory utilization percentage (0.0 to 100.0)
    /// </summary>
    public readonly double MemoryUtilizationPercent;

    /// <summary>
    /// Working set memory for current process
    /// </summary>
    public readonly long ProcessWorkingSetBytes;

    /// <summary>
    /// Private memory usage for current process
    /// </summary>
    public readonly long ProcessPrivateMemoryBytes;

    /// <summary>
    /// Number of active threads in current process
    /// </summary>
    public readonly int ProcessThreadCount;

    /// <summary>
    /// Number of handles in current process
    /// </summary>
    public readonly int ProcessHandleCount;

    /// <summary>
    /// Total CPU time for current process
    /// </summary>
    public readonly TimeSpan ProcessCpuTime;

    /// <summary>
    /// Disk read operations per second
    /// </summary>
    public readonly double DiskReadsPerSecond;

    /// <summary>
    /// Disk write operations per second
    /// </summary>
    public readonly double DiskWritesPerSecond;

    /// <summary>
    /// Disk read bytes per second
    /// </summary>
    public readonly double DiskReadBytesPerSecond;

    /// <summary>
    /// Disk write bytes per second
    /// </summary>
    public readonly double DiskWriteBytesPerSecond;

    /// <summary>
    /// Network bytes sent per second
    /// </summary>
    public readonly double NetworkBytesSentPerSecond;

    /// <summary>
    /// Network bytes received per second
    /// </summary>
    public readonly double NetworkBytesReceivedPerSecond;

    /// <summary>
    /// Garbage collection generation 0 count
    /// </summary>
    public readonly int GCGen0Collections;

    /// <summary>
    /// Garbage collection generation 1 count
    /// </summary>
    public readonly int GCGen1Collections;

    /// <summary>
    /// Garbage collection generation 2 count
    /// </summary>
    public readonly int GCGen2Collections;

    /// <summary>
    /// Total managed memory allocated
    /// </summary>
    public readonly long GCTotalMemoryBytes;

    /// <summary>
    /// Current system load factor (0.0 to 1.0+)
    /// </summary>
    public readonly double SystemLoadFactor;

    /// <summary>
    /// Number of logical processors
    /// </summary>
    public readonly int LogicalProcessorCount;

    /// <summary>
    /// System uptime
    /// </summary>
    public readonly TimeSpan SystemUptime;

    public SystemPerformanceSnapshot(
        double cpuUtilization = 0.0,
        long availableMemory = 0,
        long totalMemory = 0,
        long processWorkingSet = 0,
        long processPrivateMemory = 0,
        int processThreadCount = 0,
        int processHandleCount = 0,
        TimeSpan processCpuTime = default,
        double diskReadsPerSecond = 0.0,
        double diskWritesPerSecond = 0.0,
        double diskReadBytesPerSecond = 0.0,
        double diskWriteBytesPerSecond = 0.0,
        double networkBytesSent = 0.0,
        double networkBytesReceived = 0.0,
        int gcGen0 = 0,
        int gcGen1 = 0,
        int gcGen2 = 0,
        long gcTotalMemory = 0,
        double systemLoadFactor = 0.0)
    {
        Timestamp = DateTimeOffset.UtcNow;
        CpuUtilizationPercent = Math.Clamp(cpuUtilization, 0.0, 100.0);
        AvailableMemoryBytes = Math.Max(0, availableMemory);
        TotalMemoryBytes = Math.Max(0, totalMemory);
        UsedMemoryBytes = Math.Max(0, totalMemory - availableMemory);
        MemoryUtilizationPercent = totalMemory > 0

            ? Math.Clamp((double)UsedMemoryBytes / totalMemory * 100.0, 0.0, 100.0)

            : 0.0;
        ProcessWorkingSetBytes = Math.Max(0, processWorkingSet);
        ProcessPrivateMemoryBytes = Math.Max(0, processPrivateMemory);
        ProcessThreadCount = Math.Max(0, processThreadCount);
        ProcessHandleCount = Math.Max(0, processHandleCount);
        ProcessCpuTime = processCpuTime;
        DiskReadsPerSecond = Math.Max(0, diskReadsPerSecond);
        DiskWritesPerSecond = Math.Max(0, diskWritesPerSecond);
        DiskReadBytesPerSecond = Math.Max(0, diskReadBytesPerSecond);
        DiskWriteBytesPerSecond = Math.Max(0, diskWriteBytesPerSecond);
        NetworkBytesSentPerSecond = Math.Max(0, networkBytesSent);
        NetworkBytesReceivedPerSecond = Math.Max(0, networkBytesReceived);
        GCGen0Collections = Math.Max(0, gcGen0);
        GCGen1Collections = Math.Max(0, gcGen1);
        GCGen2Collections = Math.Max(0, gcGen2);
        GCTotalMemoryBytes = Math.Max(0, gcTotalMemory);
        SystemLoadFactor = Math.Max(0, systemLoadFactor);
        LogicalProcessorCount = Environment.ProcessorCount;
        SystemUptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
    }

    /// <summary>
    /// Available memory in megabytes
    /// </summary>
    public double AvailableMemoryMB => AvailableMemoryBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Total memory in megabytes
    /// </summary>
    public double TotalMemoryMB => TotalMemoryBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Used memory in megabytes
    /// </summary>
    public double UsedMemoryMB => UsedMemoryBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Process working set in megabytes
    /// </summary>
    public double ProcessWorkingSetMB => ProcessWorkingSetBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Process private memory in megabytes
    /// </summary>
    public double ProcessPrivateMemoryMB => ProcessPrivateMemoryBytes / (1024.0 * 1024.0);

    /// <summary>
    /// GC total memory in megabytes
    /// </summary>
    public double GCTotalMemoryMB => GCTotalMemoryBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Total GC collections across all generations
    /// </summary>
    public int TotalGCCollections => GCGen0Collections + GCGen1Collections + GCGen2Collections;

    /// <summary>
    /// Total disk operations per second
    /// </summary>
    public double TotalDiskOperationsPerSecond => DiskReadsPerSecond + DiskWritesPerSecond;

    /// <summary>
    /// Total disk bytes per second
    /// </summary>
    public double TotalDiskBytesPerSecond => DiskReadBytesPerSecond + DiskWriteBytesPerSecond;

    /// <summary>
    /// Total network bytes per second
    /// </summary>
    public double TotalNetworkBytesPerSecond => NetworkBytesSentPerSecond + NetworkBytesReceivedPerSecond;

    /// <summary>
    /// CPU utilization per logical processor
    /// </summary>
    public double CpuUtilizationPerProcessor => LogicalProcessorCount > 0

        ? CpuUtilizationPercent / LogicalProcessorCount

        : 0.0;

    /// <summary>
    /// Whether the system is under high CPU load (>80%)
    /// </summary>
    public bool IsHighCpuLoad => CpuUtilizationPercent > 80.0;

    /// <summary>
    /// Whether the system is under high memory pressure (>90%)
    /// </summary>
    public bool IsHighMemoryPressure => MemoryUtilizationPercent > 90.0;

    /// <summary>
    /// Whether the system is performing well
    /// </summary>
    public bool IsPerformingWell => !IsHighCpuLoad && !IsHighMemoryPressure && SystemLoadFactor < 0.8;

    /// <summary>
    /// Creates a snapshot from current system state
    /// </summary>
    public static SystemPerformanceSnapshot CreateCurrent()
    {
        try
        {
            using var process = Process.GetCurrentProcess();


            return new SystemPerformanceSnapshot(
                cpuUtilization: GetCpuUsage(),
                availableMemory: GetAvailableMemory(),
                totalMemory: GetTotalMemory(),
                processWorkingSet: process.WorkingSet64,
                processPrivateMemory: process.PrivateMemorySize64,
                processThreadCount: process.Threads.Count,
                processHandleCount: process.HandleCount,
                processCpuTime: process.TotalProcessorTime,
                gcGen0: GC.CollectionCount(0),
                gcGen1: GC.CollectionCount(1),
                gcGen2: GC.CollectionCount(2),
                gcTotalMemory: GC.GetTotalMemory(false),
                systemLoadFactor: GetSystemLoadFactor()
            );
        }
        catch (Exception)
        {
            // Return minimal snapshot on error
            return new SystemPerformanceSnapshot();
        }
    }

    /// <summary>
    /// Creates an empty/default snapshot
    /// </summary>
    public static SystemPerformanceSnapshot Empty => new();

    /// <summary>
    /// Calculates the difference between two snapshots
    /// </summary>
    public SystemPerformanceSnapshot CalculateDelta(SystemPerformanceSnapshot previous)
    {
        var timeDelta = Timestamp - previous.Timestamp;
        var timeDeltaSeconds = timeDelta.TotalSeconds;

        if (timeDeltaSeconds <= 0)
        {
            return Empty;
        }


        return new SystemPerformanceSnapshot(
            cpuUtilization: CpuUtilizationPercent - previous.CpuUtilizationPercent,
            availableMemory: AvailableMemoryBytes - previous.AvailableMemoryBytes,
            totalMemory: TotalMemoryBytes,
            processWorkingSet: ProcessWorkingSetBytes - previous.ProcessWorkingSetBytes,
            processPrivateMemory: ProcessPrivateMemoryBytes - previous.ProcessPrivateMemoryBytes,
            processThreadCount: ProcessThreadCount - previous.ProcessThreadCount,
            processHandleCount: ProcessHandleCount - previous.ProcessHandleCount,
            processCpuTime: ProcessCpuTime - previous.ProcessCpuTime,
            gcGen0: GCGen0Collections - previous.GCGen0Collections,
            gcGen1: GCGen1Collections - previous.GCGen1Collections,
            gcGen2: GCGen2Collections - previous.GCGen2Collections,
            gcTotalMemory: GCTotalMemoryBytes - previous.GCTotalMemoryBytes
        );
    }

    private static double GetCpuUsage()
    {
        try
        {
            // Platform-specific CPU usage calculation would go here
            // For now, return a reasonable default
            return 0.0;
        }
        catch
        {
            return 0.0;
        }
    }

    private static long GetAvailableMemory()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows-specific memory info
                var gcMemoryInfo = GC.GetGCMemoryInfo();
                return gcMemoryInfo.TotalAvailableMemoryBytes;
            }
            else
            {
                // Unix-based systems - would need platform-specific implementation
                return Environment.WorkingSet;
            }
        }
        catch
        {
            return 0;
        }
    }

    private static long GetTotalMemory()
    {
        try
        {
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            return gcMemoryInfo.TotalAvailableMemoryBytes;
        }
        catch
        {
            return Environment.WorkingSet * 2; // Rough estimate
        }
    }

    private static double GetSystemLoadFactor()
    {
        try
        {
            // Platform-specific load average calculation
            // For now, return a reasonable default
            return 0.5;
        }
        catch
        {
            return 0.0;
        }
    }

    public override string ToString()
        => $"CPU: {CpuUtilizationPercent:F1}%, Memory: {MemoryUtilizationPercent:F1}% " +
        $"({UsedMemoryMB:F0}/{TotalMemoryMB:F0}MB), Load: {SystemLoadFactor:F2}";

    public string ToDetailedString()
        => $"System Performance Snapshot @ {Timestamp:yyyy-MM-dd HH:mm:ss} UTC\n" +
        $"CPU: {CpuUtilizationPercent:F1}% ({LogicalProcessorCount} cores)\n" +
        $"Memory: {UsedMemoryMB:F0}/{TotalMemoryMB:F0}MB ({MemoryUtilizationPercent:F1}%)\n" +
        $"Process: WS={ProcessWorkingSetMB:F0}MB, Private={ProcessPrivateMemoryMB:F0}MB\n" +
        $"Threads: {ProcessThreadCount}, Handles: {ProcessHandleCount}\n" +
        $"GC: Gen0={GCGen0Collections}, Gen1={GCGen1Collections}, Gen2={GCGen2Collections}\n" +
        $"GC Memory: {GCTotalMemoryMB:F0}MB\n" +
        $"Load Factor: {SystemLoadFactor:F2}\n" +
        $"Status: {(IsPerformingWell ? "Good" : IsHighCpuLoad ? "High CPU" : "High Memory")}";

    /// <summary>
    /// Determines whether the current instance is equal to another SystemPerformanceSnapshot instance.
    /// </summary>
    /// <param name="other">The SystemPerformanceSnapshot instance to compare with this instance.</param>
    /// <returns>true if the instances are equal; otherwise, false.</returns>
    public bool Equals(SystemPerformanceSnapshot other)
        => Timestamp == other.Timestamp &&
        CpuUtilizationPercent == other.CpuUtilizationPercent &&
        AvailableMemoryBytes == other.AvailableMemoryBytes &&
        TotalMemoryBytes == other.TotalMemoryBytes &&
        UsedMemoryBytes == other.UsedMemoryBytes &&
        MemoryUtilizationPercent == other.MemoryUtilizationPercent &&
        ProcessWorkingSetBytes == other.ProcessWorkingSetBytes &&
        ProcessPrivateMemoryBytes == other.ProcessPrivateMemoryBytes &&
        ProcessThreadCount == other.ProcessThreadCount &&
        ProcessHandleCount == other.ProcessHandleCount &&
        ProcessCpuTime == other.ProcessCpuTime &&
        DiskReadsPerSecond == other.DiskReadsPerSecond &&
        DiskWritesPerSecond == other.DiskWritesPerSecond &&
        DiskReadBytesPerSecond == other.DiskReadBytesPerSecond &&
        DiskWriteBytesPerSecond == other.DiskWriteBytesPerSecond &&
        NetworkBytesSentPerSecond == other.NetworkBytesSentPerSecond &&
        NetworkBytesReceivedPerSecond == other.NetworkBytesReceivedPerSecond &&
        GCGen0Collections == other.GCGen0Collections &&
        GCGen1Collections == other.GCGen1Collections &&
        GCGen2Collections == other.GCGen2Collections &&
        GCTotalMemoryBytes == other.GCTotalMemoryBytes &&
        SystemLoadFactor == other.SystemLoadFactor &&
        LogicalProcessorCount == other.LogicalProcessorCount &&
        SystemUptime == other.SystemUptime;

    /// <summary>
    /// Determines whether the current instance is equal to a specified object.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns>true if obj is a SystemPerformanceSnapshot and is equal to this instance; otherwise, false.</returns>
    public override bool Equals(object? obj)
        => obj is SystemPerformanceSnapshot other && Equals(other);

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>A hash code for the current instance.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Timestamp);
        hash.Add(CpuUtilizationPercent);
        hash.Add(AvailableMemoryBytes);
        hash.Add(TotalMemoryBytes);
        hash.Add(UsedMemoryBytes);
        hash.Add(MemoryUtilizationPercent);
        hash.Add(ProcessWorkingSetBytes);
        hash.Add(ProcessPrivateMemoryBytes);
        hash.Add(ProcessThreadCount);
        hash.Add(ProcessHandleCount);
        hash.Add(ProcessCpuTime);
        hash.Add(DiskReadsPerSecond);
        hash.Add(DiskWritesPerSecond);
        hash.Add(DiskReadBytesPerSecond);
        hash.Add(DiskWriteBytesPerSecond);
        hash.Add(NetworkBytesSentPerSecond);
        hash.Add(NetworkBytesReceivedPerSecond);
        hash.Add(GCGen0Collections);
        hash.Add(GCGen1Collections);
        hash.Add(GCGen2Collections);
        hash.Add(GCTotalMemoryBytes);
        hash.Add(SystemLoadFactor);
        hash.Add(LogicalProcessorCount);
        hash.Add(SystemUptime);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Determines whether two SystemPerformanceSnapshot instances are equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns>true if the instances are equal; otherwise, false.</returns>
    public static bool operator ==(SystemPerformanceSnapshot left, SystemPerformanceSnapshot right)
        => left.Equals(right);

    /// <summary>
    /// Determines whether two SystemPerformanceSnapshot instances are not equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns>true if the instances are not equal; otherwise, false.</returns>
    public static bool operator !=(SystemPerformanceSnapshot left, SystemPerformanceSnapshot right)
        => !left.Equals(right);
}
