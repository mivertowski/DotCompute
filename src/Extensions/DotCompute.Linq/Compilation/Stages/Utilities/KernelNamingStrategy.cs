using System;
using System.Linq;
using DotCompute.Linq.Types;
using DotCompute.Linq.Compilation.Analysis;

namespace DotCompute.Linq.Compilation.Stages.Utilities;
{
/// <summary>
/// Generates unique kernel names based on analysis results.
/// </summary>
internal class KernelNamingStrategy
{
    }
    public string GenerateKernelName(ExpressionAnalysisResult analysisResult, BackendType backend)
    {
        var operatorChain = string.Join("_", analysisResult.OperatorChain.Take(3).Select(op => op.OperatorType));
        var hash = Math.Abs(analysisResult.OperationSignature.GetHashCode()) % 10000;
        return $"Kernel_{operatorChain}_{backend}_{hash:D4}";
    }
}
