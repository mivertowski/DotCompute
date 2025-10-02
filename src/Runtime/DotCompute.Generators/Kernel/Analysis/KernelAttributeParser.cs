// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;

namespace DotCompute.Generators.Kernel.Analysis;

/// <summary>
/// Parses kernel attributes to extract configuration.
/// </summary>
public class KernelAttributeParser
{
    public static List<string> GetBackendsFromAttribute(AttributeData attribute)
    {
        var backends = new List<string> { "CPU" }; // CPU is always supported

        if (attribute.NamedArguments.FirstOrDefault(a => a.Key == "Backends").Value.Value is int backendsValue)
        {
            if ((backendsValue & 2) != 0)
            {
                backends.Add("CUDA");
            }
            if ((backendsValue & 4) != 0)
            {
                backends.Add("Metal");
            }
            if ((backendsValue & 8) != 0)
            {
                backends.Add("OpenCL");
            }
        }

        return backends;
    }

    public static int GetVectorSizeFromAttribute(AttributeData attribute)
    {
        if (attribute.NamedArguments.FirstOrDefault(a => a.Key == "VectorSize").Value.Value is int vectorSize)
        {
            return vectorSize;
        }
        return 8; // Default to 256-bit vectors
    }

    public static bool GetIsParallelFromAttribute(AttributeData attribute)
    {
        if (attribute.NamedArguments.FirstOrDefault(a => a.Key == "IsParallel").Value.Value is bool isParallel)
        {
            return isParallel;
        }
        return true; // Default to parallel execution
    }
}