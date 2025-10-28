// Copyright (c) 2024 DotCompute. All rights reserved.

namespace DotCompute.Core.Execution.Types
{
    /// <summary>
    /// Defines types of computational workloads for parallel execution strategies.
    /// These workload types characterize different parallelization approaches
    /// and help determine the most suitable execution strategy for specific computational tasks.
    /// </summary>
    public enum WorkloadType
    {
        /// <summary>
        /// Data parallel workload.
        /// The same computation is applied to different portions of data simultaneously.
        /// Ideal for operations that can be applied independently to data elements,
        /// such as element-wise operations, convolutions, and matrix operations.
        /// </summary>
        DataParallel,

        /// <summary>
        /// Model parallel workload.
        /// Different parts of a computational model are executed on different devices.
        /// Used when models are too large to fit on a single device or when
        /// different model components can be optimized for different hardware.
        /// </summary>
        ModelParallel,

        /// <summary>
        /// Pipeline parallel workload.
        /// Computation is organized as a pipeline with data flowing through sequential stages.
        /// Different stages can be processed simultaneously on different devices,
        /// improving throughput for sequential computations with data dependencies.
        /// </summary>
        Pipeline,

        /// <summary>
        /// Work stealing workload.
        /// Dynamic load balancing where idle processing units steal work from busy ones.
        /// Effective for irregular workloads with unpredictable computation times
        /// or when static load balancing is insufficient.
        /// </summary>
        WorkStealing,

        /// <summary>
        /// Heterogeneous workload.
        /// Computation involves different types of processing units or algorithms.
        /// Combines multiple parallelization strategies or utilizes specialized
        /// hardware for different parts of the computation.
        /// </summary>
        Heterogeneous
    }
}