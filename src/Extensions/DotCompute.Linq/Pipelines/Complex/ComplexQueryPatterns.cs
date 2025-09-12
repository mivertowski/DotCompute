// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// This file has been REPLACED by CorePipelineComplexExtensions
// All complex query patterns are now implemented as extensions to DotCompute.Core.Pipelines.IKernelPipeline

using DotCompute.Linq.Pipelines.Extensions;

namespace DotCompute.Linq.Pipelines.Complex;

/// <summary>
/// This class has been replaced by CorePipelineComplexExtensions.
/// Use the extension methods in DotCompute.Linq.Pipelines.Extensions.CorePipelineComplexExtensions instead.
/// </summary>
/// <remarks>
/// All complex LINQ query patterns (GroupBy, Join, Aggregate, Window functions) are now available
/// as extension methods on DotCompute.Core.Pipelines.IKernelPipeline.
/// 
/// Usage:
/// <code>
/// using DotCompute.Linq.Pipelines.Extensions;
/// 
/// var pipeline = coreBuilder.Build()
///     .GroupByGpu(x => x.CategoryId)
///     .JoinGpu(otherData, x => x.Id, y => y.ForeignId, (x, y) => new { x, y })
///     .ReduceGpu((a, b) => a + b);
/// </code>
/// </remarks>
public static class ComplexQueryPatterns
{
    // This class is now empty - all functionality moved to CorePipelineComplexExtensions
    // Keep this file for backward compatibility but point users to the new location


    /// <summary>
    /// This method has been moved to CorePipelineComplexExtensions.GroupByGpu.
    /// Please update your code to use the extension method on IKernelPipeline directly.
    /// </summary>
    [System.Obsolete("Use CorePipelineComplexExtensions.GroupByGpu extension method instead", true)]
    public static void GroupByGpu_UseExtensionInstead()
    {
        throw new InvalidOperationException("This method has been replaced by CorePipelineComplexExtensions.GroupByGpu extension method");
    }

    /// <summary>
    /// This method has been moved to CorePipelineComplexExtensions.JoinGpu.
    /// Please update your code to use the extension method on IKernelPipeline directly.
    /// </summary>
    [System.Obsolete("Use CorePipelineComplexExtensions.JoinGpu extension method instead", true)]
    public static void JoinGpu_UseExtensionInstead()
    {
        throw new InvalidOperationException("This method has been replaced by CorePipelineComplexExtensions.JoinGpu extension method");
    }
}