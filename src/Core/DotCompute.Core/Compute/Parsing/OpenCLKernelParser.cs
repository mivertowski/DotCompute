// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text.RegularExpressions;
using DotCompute.Core.Compute.Types;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;

namespace DotCompute.Core.Compute.Parsing
{
    /// <summary>
    /// OpenCL kernel parser that identifies kernel types and extracts parameters.
    /// Analyzes OpenCL kernel source code to determine optimization strategies and parameter information.
    /// </summary>
    internal class OpenCLKernelParser(ILogger logger)
    {
        private readonly ILogger _logger = logger;

        /// <summary>
        /// Patterns for detecting different kernel types based on source code analysis.
        /// </summary>
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

        /// <summary>
        /// Parses an OpenCL kernel to extract type information and parameters.
        /// </summary>
        /// <param name="kernelSource">OpenCL kernel source code.</param>
        /// <param name="entryPoint">Entry point function name.</param>
        /// <returns>Parsed kernel information including type and parameters.</returns>
        public KernelInfo ParseKernel(string kernelSource, string entryPoint)
        {
            _logger.LogDebugMessage("Parsing kernel: {entryPoint}");

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

        /// <summary>
        /// Detects the kernel type by analyzing the source code patterns.
        /// </summary>
        /// <param name="kernelSource">OpenCL kernel source code.</param>
        /// <returns>Detected kernel type for optimization selection.</returns>
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

        /// <summary>
        /// Extracts parameter information from the kernel function signature.
        /// </summary>
        /// <param name="kernelSource">OpenCL kernel source code.</param>
        /// <returns>List of parsed kernel parameters with type information.</returns>
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
    }
}
