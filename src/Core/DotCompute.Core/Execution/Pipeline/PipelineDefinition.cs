// Copyright (c) 2024 DotCompute. All rights reserved.

namespace DotCompute.Core.Execution.Pipeline
{
    /// <summary>
    /// Defines a complete pipeline configuration for streaming execution.
    /// A pipeline consists of multiple stages that can be executed in sequence or parallel,
    /// with configurable input/output specifications and streaming capabilities.
    /// This class provides the blueprint for complex data processing workflows
    /// that can be optimized across different compute devices.
    /// </summary>
    /// <typeparam name="T">The unmanaged data type used in the pipeline computations.</typeparam>
    public class PipelineDefinition<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the pipeline stages.
        /// Each stage represents a distinct computation step with its own kernel and dependencies.
        /// Stages are executed according to their dependency relationships, enabling both
        /// sequential and parallel execution patterns within the same pipeline.
        /// </summary>
        public required List<PipelineStageDefinition> Stages { get; set; }

        /// <summary>
        /// Gets or sets the input specification.
        /// Defines how data enters the pipeline, including tensor descriptions,
        /// streaming configuration, and data flow patterns. This specification
        /// determines the pipeline's data ingestion behavior and performance characteristics.
        /// </summary>
        public required PipelineInputSpec<T> InputSpec { get; set; }

        /// <summary>
        /// Gets or sets the output specification.
        /// Defines how computed results are formatted and delivered from the pipeline.
        /// This includes tensor descriptions and output formats that control
        /// the final data representation and accessibility.
        /// </summary>
        public required PipelineOutputSpec<T> OutputSpec { get; set; }
    }
}