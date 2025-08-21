// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Execution.Plans;

namespace DotCompute.Core.Execution.Workload
{
    /// <summary>
    /// Model parallel workload specification for machine learning models
    /// that are partitioned across multiple devices.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type for the workload data</typeparam>
    public class ModelParallelWorkload<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the model layers that define the neural network structure.
        /// </summary>
        public required List<ModelLayer<T>> ModelLayers { get; set; }

        /// <summary>
        /// Gets or sets the input tensors for the model.
        /// </summary>
        public required TensorDescription<T>[] InputTensors { get; set; }

        /// <summary>
        /// Gets or sets the output tensors for the model.
        /// </summary>
        public required TensorDescription<T>[] OutputTensors { get; set; }

        /// <summary>
        /// Gets or sets the total memory requirement in bytes for the entire model.
        /// This includes weights, activations, and intermediate results.
        /// </summary>
        public long TotalMemoryRequirementBytes { get; set; }

        /// <summary>
        /// Gets or sets the total compute requirement in floating-point operations per second (FLOPS).
        /// This represents the computational complexity of the model.
        /// </summary>
        public long TotalComputeRequirementFLOPS { get; set; }
    }
}