// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines.Enums;

/// <summary>
/// Defines the different types of stages that can exist in a pipeline.
/// Each type has specific characteristics and optimization strategies.
/// </summary>
public enum PipelineStageType
{
    /// <summary>
    /// Standard computation stage that executes a single kernel.
    /// </summary>
    Computation,

    /// <summary>
    /// Data transformation stage that converts data between formats or layouts.
    /// </summary>
    DataTransformation,

    /// <summary>
    /// Memory transfer stage that moves data between devices or memory spaces.
    /// </summary>
    MemoryTransfer,

    /// <summary>
    /// Synchronization stage that coordinates execution between parallel operations.
    /// </summary>
    Synchronization,

    /// <summary>
    /// Conditional branching stage that selects execution paths based on runtime conditions.
    /// </summary>
    ConditionalBranch,

    /// <summary>
    /// Branch stage (alias for ConditionalBranch).
    /// </summary>
    Branch = ConditionalBranch,

    /// <summary>
    /// Parallel execution stage that runs multiple operations concurrently.
    /// </summary>
    ParallelExecution,

    /// <summary>
    /// Parallel stage (alias for ParallelExecution).
    /// </summary>
    Parallel = ParallelExecution,

    /// <summary>
    /// Loop stage that repeats operations until a condition is met.
    /// </summary>
    Loop,

    /// <summary>
    /// Aggregation stage that combines results from multiple parallel operations.
    /// </summary>
    Aggregation,

    /// <summary>
    /// Reduction stage that reduces a dataset to a smaller representation.
    /// </summary>
    Reduction,

    /// <summary>
    /// Map stage that applies a function to each element in a dataset.
    /// </summary>
    Map,

    /// <summary>
    /// Filter stage that selects elements from a dataset based on criteria.
    /// </summary>
    Filter,

    /// <summary>
    /// Sort stage that orders elements in a dataset.
    /// </summary>
    Sort,

    /// <summary>
    /// Join stage that combines multiple datasets based on keys or conditions.
    /// </summary>
    Join,

    /// <summary>
    /// Group stage that partitions data into groups based on keys.
    /// </summary>
    Group,

    /// <summary>
    /// Scan stage that computes prefix sums or similar cumulative operations.
    /// </summary>
    Scan,

    /// <summary>
    /// Input/Output stage that reads from or writes to external sources.
    /// </summary>
    InputOutput,

    /// <summary>
    /// Validation stage that checks data integrity and correctness.
    /// </summary>
    Validation,

    /// <summary>
    /// Optimization stage that applies performance improvements to data or operations.
    /// </summary>
    Optimization,

    /// <summary>
    /// Debugging stage that collects diagnostic information during execution.
    /// </summary>
    Debugging,

    /// <summary>
    /// Profiling stage that measures performance characteristics.
    /// </summary>
    Profiling,

    /// <summary>
    /// Caching stage that stores and retrieves intermediate results.
    /// </summary>
    Caching,

    /// <summary>
    /// Compression stage that reduces data size for storage or transmission.
    /// </summary>
    Compression,

    /// <summary>
    /// Decompression stage that expands compressed data.
    /// </summary>
    Decompression,

    /// <summary>
    /// Serialization stage that converts data to a storable format.
    /// </summary>
    Serialization,

    /// <summary>
    /// Deserialization stage that converts stored data back to usable format.
    /// </summary>
    Deserialization,

    /// <summary>
    /// Error handling stage that manages and recovers from errors.
    /// </summary>
    ErrorHandling,

    /// <summary>
    /// Resource allocation stage that manages compute and memory resources.
    /// </summary>
    ResourceAllocation,

    /// <summary>
    /// Barrier stage that ensures all parallel operations reach a synchronization point.
    /// </summary>
    Barrier,

    /// <summary>
    /// Broadcast stage that sends data to multiple destinations.
    /// </summary>
    Broadcast,

    /// <summary>
    /// Scatter stage that distributes data across multiple workers.
    /// </summary>
    Scatter,

    /// <summary>
    /// Gather stage that collects data from multiple workers.
    /// </summary>
    Gather,

    /// <summary>
    /// Custom user-defined stage type for specialized operations.
    /// </summary>
    Custom
}