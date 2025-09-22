using System;
using System.Linq;
using System.Linq.Expressions;
using DotCompute.Linq.Types;
using DotCompute.Linq.Compilation.Analysis;
using DotCompute.Linq.KernelGeneration;

namespace DotCompute.Linq.Compilation.Stages.Utilities;
/// <summary>
/// Estimates resource usage for generated kernels.
/// </summary>
internal class ResourceUsageEstimator
{
    public ResourceUsageEstimate Estimate(BackendType backend, ExpressionAnalysisResult analysisResult)
    {
        return backend switch
        {
            BackendType.CPU => EstimateCpuUsage(analysisResult),
            BackendType.CUDA => EstimateGpuUsage(analysisResult),
            _ => new ResourceUsageEstimate(0, 0, 0, 0)
        };
    }
    private ResourceUsageEstimate EstimateCpuUsage(ExpressionAnalysisResult analysisResult)
        var operatorCount = analysisResult.OperatorChain.Count;
        var memoryMB = operatorCount * 10; // Rough estimate
        var threadsNeeded = Environment.ProcessorCount;
        return new ResourceUsageEstimate(memoryMB, threadsNeeded, 0, 0);
    private ResourceUsageEstimate EstimateGpuUsage(ExpressionAnalysisResult analysisResult)
        var memoryMB = operatorCount * 20; // GPU kernels typically use more memory
        var registersPerThread = operatorCount * 4;
        var sharedMemoryKB = Math.Min(operatorCount * 2, 48); // Max 48KB per block
        return new ResourceUsageEstimate(memoryMB, 0, registersPerThread, sharedMemoryKB);
    /// <summary>
    /// Converts pipeline operator info to analysis operator info.
    /// </summary>
    private static DotCompute.Linq.Analysis.OperatorInfo ConvertToOperatorInfo(PipelineOperatorInfo pipelineInfo)
        return new DotCompute.Linq.Analysis.OperatorInfo
            OperatorType = ExpressionType.Call,
            ResultType = pipelineInfo.OutputType ?? typeof(object),
            OperandTypes = pipelineInfo.InputTypes?.ToArray() ?? Array.Empty<Type>()
}
