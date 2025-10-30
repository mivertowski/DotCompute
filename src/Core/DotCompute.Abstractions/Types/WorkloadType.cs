namespace DotCompute.Abstractions.Types;

/// <summary>
/// Defines the type of workload being processed
/// </summary>
public enum WorkloadType
{
    /// <summary>
    /// General compute workload
    /// </summary>
    Compute = 0,

    /// <summary>
    /// Memory-intensive workload
    /// </summary>
    Memory = 1,

    /// <summary>
    /// I/O-intensive workload
    /// </summary>
    IO = 2,

    /// <summary>
    /// Mixed workload with compute and memory operations
    /// </summary>
    Mixed = 3,

    /// <summary>
    /// Graphics/rendering workload
    /// </summary>
    Graphics = 4,

    /// <summary>
    /// Machine learning/AI workload
    /// </summary>
    MachineLearning = 5,

    /// <summary>
    /// Scientific computing workload
    /// </summary>
    Scientific = 6
}
