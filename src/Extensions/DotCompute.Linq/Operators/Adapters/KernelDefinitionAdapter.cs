// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Linq.Operators.Types;
using DotCompute.Abstractions.Types;
using CoreKernelDefinition = DotCompute.Abstractions.Kernels.KernelDefinition;
using LinqKernelDefinition = DotCompute.Linq.Operators.Types.KernelDefinition;

namespace DotCompute.Linq.Operators.Adapters;

/// <summary>
/// Adapter for converting between LINQ and Core kernel definitions.
/// </summary>
public static class KernelDefinitionAdapter
{
    /// <summary>
    /// Converts a LINQ KernelDefinition to a Core KernelDefinition.
    /// </summary>
    /// <param name="linqDefinition">The LINQ kernel definition to convert.</param>
    /// <returns>The converted core kernel definition.</returns>
    public static CoreKernelDefinition ConvertToCoreDefinition(LinqKernelDefinition linqDefinition)
    {
        var coreDefinition = new CoreKernelDefinition
        {
            Name = linqDefinition.Name,
            Source = linqDefinition.CompiledSource ?? string.Empty,
        };
        
        // Store parameters in metadata for later retrieval
        if (linqDefinition.Parameters != null && linqDefinition.Parameters.Count > 0)
        {
            coreDefinition.Metadata["Parameters"] = linqDefinition.Parameters.ToArray();
        }
        
        // Store language in metadata
        coreDefinition.Metadata["Language"] = linqDefinition.Language;
        
        return coreDefinition;
    }
    
    /// <summary>
    /// Converts a Core KernelDefinition to a LINQ KernelDefinition.
    /// </summary>
    /// <param name="coreDefinition">The core kernel definition to convert.</param>
    /// <returns>The converted LINQ kernel definition.</returns>
    public static LinqKernelDefinition ConvertToLinqDefinition(CoreKernelDefinition coreDefinition)
    {
        var linqDefinition = new LinqKernelDefinition
        {
            Name = coreDefinition.Name,
            CompiledSource = coreDefinition.Source
        };
        
        // Retrieve parameters from metadata
        if (coreDefinition.Metadata.TryGetValue("Parameters", out var paramsObj) && 
            paramsObj is Parameters.KernelParameter[] parameters)
        {
            linqDefinition.Parameters.AddRange(parameters);
        }
        
        // Retrieve language from metadata
        if (coreDefinition.Metadata.TryGetValue("Language", out var langObj) && 
            langObj is KernelLanguage language)
        {
            linqDefinition.Language = language;
        }
        
        return linqDefinition;
    }
    
    /// <summary>
    /// Extracts parameters from a Core KernelDefinition.
    /// </summary>
    public static Parameters.KernelParameter[] ExtractParameters(CoreKernelDefinition definition)
    {
        if (definition.Metadata.TryGetValue("Parameters", out var paramsObj) && 
            paramsObj is Parameters.KernelParameter[] parameters)
        {
            return parameters;
        }
        return Array.Empty<Parameters.KernelParameter>();
    }
    
    /// <summary>
    /// Extracts language from a Core KernelDefinition.
    /// </summary>
    public static KernelLanguage ExtractLanguage(CoreKernelDefinition definition)
    {
        if (definition.Metadata.TryGetValue("Language", out var langObj) && 
            langObj is KernelLanguage language)
        {
            return language;
        }
        return KernelLanguage.CSharpIL;
    }
}