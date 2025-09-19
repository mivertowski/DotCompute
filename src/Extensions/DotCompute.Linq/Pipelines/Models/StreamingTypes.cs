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

    /// <summary>
    /// Gets or sets the batch timeout for micro-batching.
    /// </summary>
    public TimeSpan BatchTimeout { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets the buffer size for the streaming pipeline.
    /// </summary>
    public int BufferSize { get; set; } = 10000;

    /// <summary>
    /// Gets or sets whether to enable metrics collection.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;
}

/// <summary>
/// Simple streaming pipeline stage information.
/// </summary>
public class StreamingStage
{
    /// <summary>
    /// Gets or sets the stage name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stage type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the stage can be parallelized.
    /// </summary>
    public bool IsParallelizable { get; set; }

    /// <summary>
    /// Gets or sets the estimated execution time for this stage.
    /// </summary>
    public TimeSpan EstimatedTime { get; set; }
}