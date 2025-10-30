// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Interfaces.Pipelines;

/// <summary>
/// Simple pipeline interface for test purposes, based on IKernelPipeline.
/// </summary>
public interface IPipeline
{
    /// <summary>
    /// Gets the pipeline name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the pipeline stages.
    /// </summary>
    public IReadOnlyList<IPipelineStage> Stages { get; }
}
