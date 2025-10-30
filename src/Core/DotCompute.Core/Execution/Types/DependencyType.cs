// Copyright (c) 2024 DotCompute. All rights reserved.

namespace DotCompute.Core.Execution.Types
{
    /// <summary>
    /// Defines types of dependencies that can exist between computational operations.
    /// These dependency types are crucial for correct scheduling and execution ordering
    /// in parallel and distributed computing environments.
    /// </summary>
    public enum DependencyType
    {
        /// <summary>
        /// Data hazard (read-after-write, write-after-read).
        /// Occurs when operations have conflicting access patterns to the same data.
        /// Includes scenarios where one operation must complete writing before another
        /// can read, or vice versa. Critical for maintaining data consistency.
        /// </summary>
        DataHazard,

        /// <summary>
        /// Control flow dependency.
        /// Represents dependencies arising from conditional execution paths
        /// where the outcome of one operation determines which subsequent
        /// operations should be executed. Essential for maintaining program correctness.
        /// </summary>
        Control,

        /// <summary>
        /// Structural dependency defined in model/pipeline.
        /// Dependencies that are inherent to the computational structure,
        /// such as layer-to-layer connections in neural networks or
        /// stage-to-stage dependencies in processing pipelines.
        /// </summary>
        Structural,

        /// <summary>
        /// Data flow between layers/stages.
        /// Represents the flow of intermediate results between different
        /// computational stages or layers. The output of one stage serves
        /// as input to subsequent stages, creating natural ordering constraints.
        /// </summary>
        DataFlow,

        /// <summary>
        /// Sequential execution requirement.
        /// Operations that must be executed in a specific order due to
        /// algorithmic requirements or side effects. Cannot be parallelized
        /// or reordered without affecting correctness.
        /// </summary>
        Sequential,

        /// <summary>
        /// Resource contention.
        /// Dependencies arising from shared resource usage where operations
        /// compete for limited resources such as memory, compute units,
        /// or I/O bandwidth. Requires careful scheduling to avoid conflicts.
        /// </summary>
        Resource
    }
}
