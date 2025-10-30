// <copyright file="PipelineOptimizationException.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions.Pipelines.Enums;

namespace DotCompute.Core.Pipelines.Exceptions;

/// <summary>
/// Exception thrown when pipeline optimization fails.
/// Provides information about which optimization failed and why.
/// </summary>
public sealed class PipelineOptimizationException : PipelineException
{
    /// <summary>
    /// Gets the type of optimization that failed.
    /// Identifies the specific optimization technique that caused the error.
    /// </summary>
    public OptimizationType FailedOptimization { get; }

    /// <summary>
    /// Gets the detailed reason for the optimization failure.
    /// Explains why the optimization could not be applied.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Initializes a new instance of the PipelineOptimizationException class.
    /// </summary>
    public PipelineOptimizationException()
        : base("Pipeline optimization failed", "PIPELINE_OPTIMIZATION_FAILED")
    {
        FailedOptimization = OptimizationType.KernelFusion;
        Reason = "Unknown reason";
    }

    /// <summary>
    /// Initializes a new instance of the PipelineOptimizationException class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PipelineOptimizationException(string message)

        : base(message, "PIPELINE_OPTIMIZATION_FAILED")
    {
        FailedOptimization = OptimizationType.KernelFusion;
        Reason = "Unknown reason";
    }

    /// <summary>
    /// Initializes a new instance of the PipelineOptimizationException class with message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PipelineOptimizationException(string message, Exception innerException)

        : base(message, "PIPELINE_OPTIMIZATION_FAILED", innerException)
    {
        FailedOptimization = OptimizationType.KernelFusion;
        Reason = "Unknown reason";
    }

    /// <summary>
    /// Initializes a new instance of the PipelineOptimizationException class with full context.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="failedOptimization">The optimization that failed.</param>
    /// <param name="reason">The reason for failure.</param>
    /// <param name="pipelineId">The pipeline identifier.</param>
    /// <param name="innerException">The inner exception.</param>
    public PipelineOptimizationException(
        string message,
        OptimizationType failedOptimization,
        string reason,
        string? pipelineId = null,
        Exception? innerException = null)
        : base(message, "PIPELINE_OPTIMIZATION_FAILED", pipelineId, null, null, innerException)
    {
        FailedOptimization = failedOptimization;
        Reason = reason;
    }
}
