using DotCompute.Abstractions.Types;

namespace DotCompute.Core.Pipelines.Optimization.Utilities;

/// <summary>
/// Intelligent calculator for optimal buffer sizes based on workload characteristics
/// </summary>
public static class IntelligentBufferSizeCalculator
{
    /// <summary>
    /// Calculates the optimal buffer size for a given workload
    /// </summary>
    /// <param name="elementCount">Number of elements to process</param>
    /// <param name="elementSize">Size of each element in bytes</param>
    /// <param name="workloadType">Type of workload</param>
    /// <param name="availableMemory">Available memory in bytes</param>
    /// <returns>Optimal buffer size in bytes</returns>
    public static long CalculateOptimalSize(
        long elementCount,
        int elementSize,
        WorkloadType workloadType = WorkloadType.Compute,
        long availableMemory = 0)
    {
        if (elementCount <= 0)
        {

            throw new ArgumentOutOfRangeException(nameof(elementCount), "Element count must be positive");
        }


        if (elementSize <= 0)
        {

            throw new ArgumentOutOfRangeException(nameof(elementSize), "Element size must be positive");
        }

        // Calculate total data size

        var totalDataSize = elementCount * elementSize;

        // If no memory limit specified, use reasonable defaults
        if (availableMemory <= 0)
        {
            availableMemory = workloadType switch
            {
                WorkloadType.Compute => 1024L * 1024 * 1024, // 1GB default for compute
                WorkloadType.Memory => 512L * 1024 * 1024,   // 512MB default for memory-bound
                WorkloadType.IO => 256L * 1024 * 1024,       // 256MB default for I/O
                _ => 512L * 1024 * 1024                      // 512MB default
            };
        }

        // Calculate buffer size based on workload characteristics
        var bufferSize = workloadType switch
        {
            WorkloadType.Compute => CalculateComputeOptimalSize(totalDataSize, availableMemory),
            WorkloadType.Memory => CalculateMemoryOptimalSize(totalDataSize, availableMemory),
            WorkloadType.IO => CalculateIOOptimalSize(totalDataSize, availableMemory),
            _ => CalculateDefaultOptimalSize(totalDataSize, availableMemory)
        };

        // Ensure buffer size is aligned to cache line boundaries (64 bytes)
        bufferSize = AlignToCacheLine(bufferSize);

        // Ensure minimum viable size
        bufferSize = Math.Max(bufferSize, 4096); // 4KB minimum

        // Ensure we don't exceed total data size
        bufferSize = Math.Min(bufferSize, totalDataSize);

        return bufferSize;
    }

    /// <summary>
    /// Calculates optimal buffer size for compute-intensive workloads
    /// </summary>
    private static long CalculateComputeOptimalSize(long totalDataSize, long availableMemory)
        // For compute workloads, prioritize fitting working set in cache
        // Use 25% of available memory to leave room for intermediate results

        => Math.Min(totalDataSize, availableMemory / 4);

    /// <summary>
    /// Calculates optimal buffer size for memory-intensive workloads
    /// </summary>
    private static long CalculateMemoryOptimalSize(long totalDataSize, long availableMemory)
        // For memory workloads, use larger buffers to improve throughput
        // Use 60% of available memory

        => Math.Min(totalDataSize, (availableMemory * 6) / 10);

    /// <summary>
    /// Calculates optimal buffer size for I/O-intensive workloads
    /// </summary>
    private static long CalculateIOOptimalSize(long totalDataSize, long availableMemory)
        // For I/O workloads, use smaller buffers for better responsiveness
        // Use 20% of available memory

        => Math.Min(totalDataSize, availableMemory / 5);

    /// <summary>
    /// Calculates default optimal buffer size
    /// </summary>
    private static long CalculateDefaultOptimalSize(long totalDataSize, long availableMemory)
        // Default strategy - balanced approach
        // Use 33% of available memory

        => Math.Min(totalDataSize, availableMemory / 3);

    /// <summary>
    /// Aligns size to cache line boundary (64 bytes)
    /// </summary>
    private static long AlignToCacheLine(long size)
    {
        const int cacheLineSize = 64;
        return ((size + cacheLineSize - 1) / cacheLineSize) * cacheLineSize;
    }

    /// <summary>
    /// Calculates the number of buffers needed for the given total size and buffer size
    /// </summary>
    /// <param name="totalSize">Total data size</param>
    /// <param name="bufferSize">Size of each buffer</param>
    /// <returns>Number of buffers needed</returns>
    public static int CalculateBufferCount(long totalSize, long bufferSize)
    {
        if (bufferSize <= 0)
        {

            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be positive");
        }


        return (int)Math.Ceiling((double)totalSize / bufferSize);
    }

    /// <summary>
    /// Validates if a buffer size is suitable for the given constraints
    /// </summary>
    /// <param name="bufferSize">Buffer size to validate</param>
    /// <param name="availableMemory">Available memory</param>
    /// <param name="maxBufferSize">Maximum allowed buffer size</param>
    /// <returns>True if the buffer size is valid</returns>
    public static bool ValidateBufferSize(long bufferSize, long availableMemory, long maxBufferSize = long.MaxValue)
    {
        return bufferSize > 0 &&
               bufferSize <= availableMemory &&
               bufferSize <= maxBufferSize &&
               bufferSize >= 4096; // Minimum 4KB
    }

    /// <summary>
    /// Suggests an optimal number of buffers for parallel processing
    /// </summary>
    /// <param name="totalSize">Total data size</param>
    /// <param name="availableMemory">Available memory</param>
    /// <param name="processorCount">Number of available processors</param>
    /// <returns>Suggested number of buffers</returns>
    public static int SuggestBufferCount(long totalSize, long availableMemory, int processorCount = 0)
    {
        if (processorCount <= 0)
        {
            processorCount = Environment.ProcessorCount;
        }

        // Start with processor count as base

        var baseCount = processorCount * 2; // 2x for better utilization

        // Calculate buffer size for this count
        var bufferSize = totalSize / baseCount;

        // Ensure buffer size is reasonable
        const long minBufferSize = 64 * 1024; // 64KB minimum
        const long maxBufferSize = 128 * 1024 * 1024; // 128MB maximum

        if (bufferSize < minBufferSize)
        {
            // Too many small buffers, reduce count
            baseCount = (int)(totalSize / minBufferSize);
        }
        else if (bufferSize > maxBufferSize)
        {
            // Buffers too large, increase count
            baseCount = (int)(totalSize / maxBufferSize);
        }

        // Ensure we don't exceed memory constraints
        var totalBufferMemory = baseCount * (totalSize / baseCount);
        if (totalBufferMemory > availableMemory)
        {
            baseCount = (int)(availableMemory / (totalSize / baseCount));
        }

        return Math.Max(1, baseCount);
    }
}