// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Backends.CUDA.Types
{

/// <summary>
/// Extended memory statistics for CUDA memory management
/// </summary>
public sealed class CudaMemoryStatisticsExtended : MemoryStatistics
{
    public long PinnedMemoryBytes { get; set; }
    public long UnifiedMemoryBytes { get; set; }
    public long PooledMemoryBytes { get; set; }
    public int PoolHits { get; set; }
    public int PoolMisses { get; set; }
    public double PoolEfficiency => PoolHits + PoolMisses > 0 ? 
        (double)PoolHits / (PoolHits + PoolMisses) : 0.0;
    public long HostToDeviceTransfers { get; set; }
    public long DeviceToHostTransfers { get; set; }
    public long TotalTransferredBytes { get; set; }
    public double AverageBandwidth { get; set; } // MB/s
    public double MemoryFragmentation { get; set; }
    public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// CUDA memory allocation flags and options
/// </summary>
[Flags]
public enum CudaMemoryFlags : uint
{
    None = 0,
    DeviceLocal = 1,
    HostVisible = 2,
    HostCoherent = 4,
    HostCached = 8,
    LazilyAllocated = 16,
    Protected = 32,
    Unified = 64,
    Pinned = 128
}

/// <summary>
/// CUDA memory type enumeration
/// </summary>
public enum CudaMemoryType
{
    Device,
    Host,
    Unified,
    Pinned,
    Mapped
}

/// <summary>
/// CUDA memory alignment requirements
/// </summary>
public static class CudaMemoryAlignment
{
    public const int MinAlignment = 256; // 256 bytes minimum for coalesced access
    public const int OptimalAlignment = 512; // 512 bytes optimal for RTX 2000 series
    public const int TextureAlignment = 512; // Texture memory alignment
    public const int SurfaceAlignment = 512; // Surface memory alignment
    
    public static long AlignUp(long size, int alignment = OptimalAlignment)
    {
        return ((size + alignment - 1) / alignment) * alignment;
    }
    
    public static bool IsAligned(long size, int alignment = OptimalAlignment)
    {
        return size % alignment == 0;
    }
}

/// <summary>
/// Kernel arguments for CUDA execution
/// </summary>
public sealed class CudaKernelArguments
{
    private readonly List<object> _arguments = new();
    
    public CudaKernelArguments() { }
    
    public CudaKernelArguments(params object[] arguments)
    {
        _arguments.AddRange(arguments);
    }
    
    public void Add<T>(T argument) where T : unmanaged
    {
        _arguments.Add(argument);
    }
    
    public void AddBuffer(IntPtr devicePointer)
    {
        _arguments.Add(devicePointer);
    }
    
    public object[] ToArray() => _arguments.ToArray();
    
    public int Count => _arguments.Count;
}


/// <summary>
/// Validation result for CUDA operations
/// </summary>
public sealed class ValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public List<string> Warnings { get; init; } = new();
    
    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Success(string message) => new() { IsValid = true, ErrorMessage = message };
    public static ValidationResult Error(string message) => new() { IsValid = false, ErrorMessage = message };
    public static ValidationResult Failure(string message) => new() { IsValid = false, ErrorMessage = message };
    public static ValidationResult SuccessWithWarnings(List<string> warnings) => new() { IsValid = true, Warnings = warnings };
    public static ValidationResult SuccessWithWarnings(string[] warnings) => new() { IsValid = true, Warnings = new List<string>(warnings) };
}

/// <summary>
/// Cache statistics for CUDA operations
/// </summary>
public sealed class CacheStatistics
{
    public int HitCount { get; set; }
    public int MissCount { get; set; }
    public int TotalRequests => HitCount + MissCount;
    public int TotalEntries { get; set; }
    public long TotalSizeBytes { get; set; }
    public double HitRate { get; set; } = 0.0;
    public double AverageAccessCount { get; set; }
    public DateTime? OldestEntryTime { get; set; } = DateTime.UtcNow;
    public DateTime? NewestEntryTime { get; set; } = DateTime.UtcNow;
    public long CacheSizeBytes { get; set; }
    public DateTime LastAccess { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Exception thrown during kernel compilation
/// </summary>
public sealed class KernelCompilationException : Exception
{
    public string? CompilerOutput { get; }
    public string? SourceCode { get; }
    public int? ErrorCode { get; }
    
    public KernelCompilationException() : base() { }
    
    public KernelCompilationException(string message) : base(message) { }
    
    public KernelCompilationException(string message, Exception innerException) : base(message, innerException) { }
    
    public KernelCompilationException(string message, string? compilerOutput, string? sourceCode = null, int? errorCode = null) 
        : base(message)
    {
        CompilerOutput = compilerOutput;
        SourceCode = sourceCode;
        ErrorCode = errorCode;
    }
}}
