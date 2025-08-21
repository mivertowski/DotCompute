// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Accelerators;


/// <summary>
/// Configuration options for the CPU accelerator.
/// </summary>
public sealed class CpuAcceleratorOptions
{
    /// <summary>
    /// Gets or sets the maximum work group size.
    /// If null, uses the system's processor count.
    /// </summary>
    public int? MaxWorkGroupSize { get; set; }

    /// <summary>
    /// Gets or sets the maximum memory allocation size in bytes.
    /// </summary>
    public long MaxMemoryAllocation { get; set; } = 2L * 1024 * 1024 * 1024; // 2GB default

    /// <summary>
    /// Gets or sets whether to enable automatic vectorization.
    /// </summary>
    public bool EnableAutoVectorization { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable NUMA-aware memory allocation.
    /// </summary>
    public bool EnableNumaAwareAllocation { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum work size for vectorization to be considered beneficial.
    /// </summary>
    public int MinVectorizationWorkSize { get; set; } = 256;

    /// <summary>
    /// Gets or sets whether to enable loop unrolling optimizations.
    /// </summary>
    public bool EnableLoopUnrolling { get; set; } = true;

    /// <summary>
    /// Gets or sets the target vector width in bits.
    /// If 0, the system will automatically detect the best width.
    /// </summary>
    public int TargetVectorWidth { get; set; }


    /// <summary>
    /// Gets or sets whether to enable performance profiling.
    /// </summary>
    public bool EnableProfiling { get; set; } = true;

    /// <summary>
    /// Gets or sets the profiling sampling interval in milliseconds.
    /// </summary>
    public int ProfilingSamplingIntervalMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to enable kernel caching.
    /// </summary>
    public bool EnableKernelCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of cached kernels.
    /// </summary>
    public int MaxCachedKernels { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether to enable memory prefetching.
    /// </summary>
    public bool EnableMemoryPrefetching { get; set; } = true;

    /// <summary>
    /// Gets or sets the memory prefetch distance in cache lines.
    /// </summary>
    public int MemoryPrefetchDistance { get; set; } = 8;

    /// <summary>
    /// Gets or sets whether to enable CPU frequency scaling detection.
    /// </summary>
    public bool EnableFrequencyScalingDetection { get; set; }


    /// <summary>
    /// Gets or sets whether to prefer performance over power efficiency.
    /// </summary>
    public bool PreferPerformanceOverPower { get; set; } = true;

    /// <summary>
    /// Gets or sets the thread priority for compute threads.
    /// </summary>
    public ThreadPriority ComputeThreadPriority { get; set; } = ThreadPriority.Normal;

    /// <summary>
    /// Gets or sets custom instruction set preferences.
    /// Empty list means use all available instruction sets.
    /// </summary>
    public IList<string> PreferredInstructionSets { get; set; } = [];

    /// <summary>
    /// Gets or sets instruction sets to avoid.
    /// </summary>
    public IList<string> DisabledInstructionSets { get; set; } = [];

    /// <summary>
    /// Gets or sets whether to enable hardware performance counters.
    /// </summary>
    public bool EnableHardwareCounters { get; set; }


    /// <summary>
    /// Gets or sets the memory alignment requirement in bytes.
    /// </summary>
    public int MemoryAlignment { get; set; } = 64; // Cache line aligned by default

    /// <summary>
    /// Gets or sets whether to use huge pages when available.
    /// </summary>
    public bool UseHugePages { get; set; }


    /// <summary>
    /// Validates the options and returns any validation errors.
    /// </summary>
    public IList<string> Validate()
    {
        var errors = new List<string>();

        if (MaxWorkGroupSize.HasValue && MaxWorkGroupSize.Value <= 0)
        {
            errors.Add("MaxWorkGroupSize must be positive when specified");
        }

        if (MaxMemoryAllocation <= 0)
        {
            errors.Add("MaxMemoryAllocation must be positive");
        }

        if (MinVectorizationWorkSize <= 0)
        {
            errors.Add("MinVectorizationWorkSize must be positive");
        }

        if (TargetVectorWidth < 0)
        {
            errors.Add("TargetVectorWidth must be non-negative");
        }

        if (TargetVectorWidth > 0 && !IsValidVectorWidth(TargetVectorWidth))
        {
            errors.Add("TargetVectorWidth must be a valid vector width (128, 256, 512)");
        }

        if (ProfilingSamplingIntervalMs <= 0)
        {
            errors.Add("ProfilingSamplingIntervalMs must be positive");
        }

        if (MaxCachedKernels <= 0)
        {
            errors.Add("MaxCachedKernels must be positive");
        }

        if (MemoryPrefetchDistance <= 0)
        {
            errors.Add("MemoryPrefetchDistance must be positive");
        }

        if (MemoryAlignment <= 0 || !IsPowerOfTwo(MemoryAlignment))
        {
            errors.Add("MemoryAlignment must be a positive power of two");
        }

        return errors;
    }

    /// <summary>
    /// Gets the effective work group size based on configuration and system capabilities.
    /// </summary>
    public int GetEffectiveWorkGroupSize()
    {
        if (MaxWorkGroupSize.HasValue)
        {
            return Math.Min(MaxWorkGroupSize.Value, Environment.ProcessorCount * 64);
        }

        // Default to processor count * optimal multiplier
        return Math.Min(Environment.ProcessorCount * 32, 1024);
    }

    /// <summary>
    /// Gets the effective vector width based on configuration and hardware capabilities.
    /// </summary>
    public int GetEffectiveVectorWidth()
    {
        if (TargetVectorWidth > 0)
        {
            return TargetVectorWidth;
        }

        // Auto-detect based on hardware capabilities
        return Intrinsics.SimdCapabilities.PreferredVectorWidth;
    }

    /// <summary>
    /// Determines if the specified instruction set should be used.
    /// </summary>
    public bool ShouldUseInstructionSet(string instructionSet)
    {
        ArgumentNullException.ThrowIfNull(instructionSet);

        // Check if explicitly disabled
        if (DisabledInstructionSets.Contains(instructionSet, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        // If preferred sets are specified, only use those
        if (PreferredInstructionSets.Count > 0)
        {
            return PreferredInstructionSets.Contains(instructionSet, StringComparer.OrdinalIgnoreCase);
        }

        // Otherwise, use all available instruction sets
        return true;
    }

    private static bool IsValidVectorWidth(int width) => width is 128 or 256 or 512;

    private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
}

/// <summary>
/// Extension methods for CpuAcceleratorOptions.
/// </summary>
public static class CpuAcceleratorOptionsExtensions
{
    /// <summary>
    /// Configures the options for maximum performance.
    /// </summary>
    public static CpuAcceleratorOptions ConfigureForMaxPerformance(this CpuAcceleratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.EnableAutoVectorization = true;
        options.EnableNumaAwareAllocation = true;
        options.EnableLoopUnrolling = true;
        options.EnableMemoryPrefetching = true;
        options.PreferPerformanceOverPower = true;
        options.ComputeThreadPriority = ThreadPriority.AboveNormal;
        options.UseHugePages = true;
        options.EnableHardwareCounters = true;
        options.MemoryAlignment = 64;

        return options;
    }

    /// <summary>
    /// Configures the options for minimum memory usage.
    /// </summary>
    public static CpuAcceleratorOptions ConfigureForMinMemory(this CpuAcceleratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.EnableKernelCaching = false;
        options.MaxCachedKernels = 10;
        options.EnableProfiling = false;
        options.UseHugePages = false;
        options.EnableMemoryPrefetching = false;
        options.MemoryPrefetchDistance = 1;

        return options;
    }

    /// <summary>
    /// Configures the options for balanced performance and efficiency.
    /// </summary>
    public static CpuAcceleratorOptions ConfigureForBalanced(this CpuAcceleratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.EnableAutoVectorization = true;
        options.EnableNumaAwareAllocation = true;
        options.EnableLoopUnrolling = true;
        options.EnableMemoryPrefetching = true;
        options.PreferPerformanceOverPower = false;
        options.ComputeThreadPriority = ThreadPriority.Normal;
        options.UseHugePages = false;
        options.EnableHardwareCounters = false;
        options.EnableProfiling = true;

        return options;
    }

    /// <summary>
    /// Configures the options for debugging and development.
    /// </summary>
    public static CpuAcceleratorOptions ConfigureForDebugging(this CpuAcceleratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.EnableProfiling = true;
        options.ProfilingSamplingIntervalMs = 10; // More frequent sampling
        options.EnableHardwareCounters = true;
        options.EnableKernelCaching = false; // Disable caching for consistent behavior
        options.ComputeThreadPriority = ThreadPriority.Normal;
        options.MaxWorkGroupSize = Environment.ProcessorCount; // Simpler work distribution

        return options;
    }
}
