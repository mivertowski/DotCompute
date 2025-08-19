// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels
{

/// <summary>
/// OpenCL kernel parser that identifies kernel types and extracts parameters.
/// </summary>
internal class OpenCLKernelParser(ILogger logger)
{
    private readonly ILogger _logger = logger;
    
    private static readonly Dictionary<string, KernelType> KernelPatterns = new()
    {
        { @"result\[i\]\s*=\s*a\[i\]\s*\+\s*b\[i\]", KernelType.VectorAdd },
        { @"result\[i\]\s*=\s*a\[i\]\s*\*\s*b\[i\]", KernelType.VectorMultiply },
        { @"(result\[i\]\s*=\s*input\[i\]\s*\*\s*scale)|(vector_scale)", KernelType.VectorScale },
        { @"matrix_mul|c\[row.*col\].*sum", KernelType.MatrixMultiply },
        { @"reduce_sum|sum\s*\+=.*input\[", KernelType.Reduction },
        { @"memory_intensive|multiple.*input\[i\]", KernelType.MemoryIntensive },
        { @"compute_intensive|sin\(|cos\(|sqrt\(", KernelType.ComputeIntensive }
    };

    public KernelInfo ParseKernel(string kernelSource, string entryPoint)
    {
        _logger.LogDebug("Parsing kernel: {EntryPoint}", entryPoint);

        var kernelType = DetectKernelType(kernelSource);
        var parameters = ExtractParameters(kernelSource);

        return new KernelInfo
        {
            Name = entryPoint,
            Type = kernelType,
            Source = kernelSource,
            Parameters = parameters
        };
    }

    private static KernelType DetectKernelType(string kernelSource)
    {
        foreach (var pattern in KernelPatterns)
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
        var signatureMatch = Regex.Match(kernelSource, @"__kernel\s+void\s+\w+\s*\(([^)]+)\)", RegexOptions.IgnoreCase);
        if (signatureMatch.Success)
        {
            var paramString = signatureMatch.Groups[1].Value;
            var paramMatches = Regex.Matches(paramString, @"(__global\s+(?:const\s+)?(\w+\*?)\s+(\w+))|(\w+\s+(\w+))", RegexOptions.IgnoreCase);
            
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
}}
