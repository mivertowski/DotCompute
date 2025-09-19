// Copyright (c) 2024 DotCompute. All rights reserved.

namespace DotCompute.Core.Execution.Pipeline
{
    /// <summary>
    /// Defines a single stage within a pipeline execution workflow.
    /// A stage represents an atomic computation unit with its own kernel,
    /// dependencies, and execution characteristics. Stages can be chained together
    /// to form complex processing pipelines with controlled data flow and
    /// dependency management for optimal resource utilization.
    /// </summary>
    public class PipelineStageDefinition
    {
        /// <summary>
        /// Gets or sets the stage name.
        /// A unique identifier for this stage within the pipeline.
        /// Used for dependency resolution, debugging, and performance monitoring.
        /// Stage names should be descriptive and follow consistent naming conventions.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the kernel name for this stage.
        /// Specifies which compiled kernel should be executed for this stage.
        /// The kernel name must correspond to a pre-compiled and available
        /// kernel in the execution context's kernel cache.
        /// </summary>
        public required string KernelName { get; set; }

        /// <summary>
        /// Gets or sets the dependencies on other stages.
        /// Lists the names of other stages that must complete before this stage can execute.
        /// Dependencies define the execution order and enable the pipeline scheduler
        /// to optimize parallel execution while maintaining correctness.
        /// Empty list indicates this stage has no dependencies and can execute immediately.
        /// </summary>
        public List<string> Dependencies { get; set; } = [];

        /// <summary>
        /// Gets or sets the input specifications for this stage.
        /// Defines the expected input data types, shapes, and access patterns.
        /// Used for memory optimization and data transfer planning.
        /// </summary>
        public List<DataSpecification> InputSpecs { get; set; } = [];

        /// <summary>
        /// Gets or sets the output specifications for this stage.
        /// Defines the output data types, shapes, and memory requirements.
        /// Used for memory allocation and data flow optimization.
        /// </summary>
        public List<DataSpecification> OutputSpecs { get; set; } = [];
    }

    /// <summary>
    /// Specification for data input or output in a pipeline stage.
    /// </summary>
    public class DataSpecification
    {
        /// <summary>
        /// Gets or sets the name of the data specification.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the data type.
        /// </summary>
        public required Type DataType { get; set; }

        /// <summary>
        /// Gets or sets the data shape or dimensions.
        /// </summary>
        public int[] Shape { get; set; } = [];

        /// <summary>
        /// Gets or sets the total number of elements in the data specification.
        /// </summary>
        public long ElementCount { get; set; }

        /// <summary>
        /// Gets or sets the memory access pattern.
        /// </summary>
        public string AccessPattern { get; set; } = "ReadWrite";

        /// <summary>
        /// Gets or sets whether the data is optional.
        /// </summary>
        public bool IsOptional { get; set; } = false;
    }
}