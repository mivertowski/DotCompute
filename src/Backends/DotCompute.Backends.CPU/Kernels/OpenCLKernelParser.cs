// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text.RegularExpressions;
using DotCompute.Backends.CPU.Kernels.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels;


/// <summary>
/// OpenCL kernel parser that identifies kernel types and extracts parameters.
/// </summary>
internal partial class OpenCLKernelParser(ILogger logger)
{
    private readonly ILogger _logger = logger;

    private static readonly Dictionary<string, KernelType> _kernelPatterns = new()
{
    { @"result\[i\]\s*=\s*a\[i\]\s*\+\s*b\[i\]", KernelType.VectorAdd },
    { @"result\[i\]\s*=\s*a\[i\]\s*\*\s*b\[i\]", KernelType.VectorMultiply },
    { @"(result\[i\]\s*=\s*input\[i\]\s*\*\s*scale)|(vector_scale)", KernelType.VectorScale },
    { @"matrix_mul|c\[row.*col\].*sum", KernelType.MatrixMultiply },
    { @"reduce_sum|sum\s*\+=.*input\[", KernelType.Reduction },
    { @"memory_intensive|multiple.*input\[i\]", KernelType.MemoryIntensive },
    { @"compute_intensive|sin\(|cos\(|sqrt\(", KernelType.ComputeIntensive }
};
    /// <summary>
    /// Gets parse kernel.
    /// </summary>
    /// <param name="kernelSource">The kernel source.</param>
    /// <param name="entryPoint">The entry point.</param>
    /// <returns>The result of the operation.</returns>

    public KernelInfo ParseKernel(string kernelSource, string entryPoint)
    {
        _logger.LogDebug("Parsing kernel: {EntryPoint}", entryPoint);

        var kernelType = DetectKernelType(kernelSource);
        var parameters = ExtractParameters(kernelSource);

        var kernelInfo = new KernelInfo
        {
            Name = entryPoint,
            Type = kernelType,
            Source = kernelSource
        };

        foreach (var param in parameters)
        {
            kernelInfo.Parameters.Add(param);
        }

        return kernelInfo;
    }

    private static KernelType DetectKernelType(string kernelSource)
    {
        foreach (var pattern in _kernelPatterns)
        {
            if (Regex.IsMatch(kernelSource, pattern.Key, RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                return pattern.Value;
            }
        }
        return KernelType.Generic;
    }

    private static List<KernelParameter> ExtractParameters(string kernelSource)
    {
        var parameters = new List<KernelParameter>();

        // Extract function signature parameters
        var signatureMatch = MyRegex().Match(kernelSource);
        if (signatureMatch.Success)
        {
            var paramString = signatureMatch.Groups[1].Value;
            var paramMatches = MyRegex1().Matches(paramString);

            foreach (Match match in paramMatches)
            {
                if (match.Groups[2].Success) // Global memory parameter
                {
                    parameters.Add(new KernelParameter
                    {
                        Name = match.Groups[3].Value,
                        Type = match.Groups[2].Value,
                        IsGlobal = true
                    });
                }
                else if (match.Groups[5].Success) // Regular parameter
                {
                    parameters.Add(new KernelParameter
                    {
                        Name = match.Groups[5].Value,
                        Type = match.Groups[4].Value,
                        IsGlobal = false
                    });
                }
            }
        }

        return parameters;
    }

    [GeneratedRegex(@"__kernel\s+void\s+\w+\s*\(([^)]+)\)", RegexOptions.IgnoreCase, "")]
    private static partial Regex MyRegex();
    [GeneratedRegex(@"(__global\s+(?:const\s+)?(\w+\*?)\s+(\w+))|(\w+\s+(\w+))", RegexOptions.IgnoreCase, "")]
    private static partial Regex MyRegex1();
}
