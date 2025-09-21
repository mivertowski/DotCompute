// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
namespace DotCompute.Linq.Pipelines.Models;
/// <summary>
/// Options for streaming pipeline configuration.
/// </summary>
public class StreamingPipelineOptions
{
    /// <summary>
    /// Gets or sets the batch size for micro-batching.
    /// </summary>
    public int BatchSize { get; set; } = 1000;
    /// Gets or sets the batch timeout for micro-batching.
    public TimeSpan BatchTimeout { get; set; } = TimeSpan.FromMilliseconds(100);
    /// Gets or sets the buffer size for the streaming pipeline.
    public int BufferSize { get; set; } = 10000;
    /// Gets or sets whether to enable metrics collection.
    public bool EnableMetrics { get; set; } = true;
}
/// Simple streaming pipeline stage information.
public class StreamingStage
    /// Gets or sets the stage name.
    public string Name { get; set; } = string.Empty;
    /// Gets or sets the stage type.
    public string Type { get; set; } = string.Empty;
    /// Gets or sets whether the stage can be parallelized.
    public bool IsParallelizable { get; set; }
    /// Gets or sets the estimated execution time for this stage.
    public TimeSpan EstimatedTime { get; set; }
