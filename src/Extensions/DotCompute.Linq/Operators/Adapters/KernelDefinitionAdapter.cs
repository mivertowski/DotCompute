// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Linq.Operators.Parameters;
using DotCompute.Linq.Operators.Types;
using CoreKernelDefinition = DotCompute.Abstractions.Kernels.KernelDefinition;
using LinqKernelDefinition = DotCompute.Linq.Operators.Types.KernelDefinition;

namespace DotCompute.Linq.Operators.Adapters;

/// <summary>
/// Adapter for converting between different KernelDefinition types following clean architecture principles.
/// </summary>
/// <remarks>
/// This adapter provides a clean separation between the core abstractions and LINQ-specific extensions
/// by handling the conversion between DotCompute.Abstractions.Kernels.KernelDefinition and 
/// DotCompute.Linq.Operators.Types.KernelDefinition.
/// </remarks>
internal static class KernelDefinitionAdapter
{
    /// <summary>
    /// Converts a LINQ KernelDefinition to a Core KernelDefinition.
    /// </summary>
    /// <param name="linqDefinition">The LINQ kernel definition to convert.</param>
    /// <returns>A core kernel definition with LINQ-specific data stored in metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when linqDefinition is null.</exception>
    public static CoreKernelDefinition ToCore(LinqKernelDefinition linqDefinition)
    {
        ArgumentNullException.ThrowIfNull(linqDefinition);

        var coreDefinition = new CoreKernelDefinition(linqDefinition.Name, linqDefinition.CompiledSource ?? string.Empty)
        {
            Metadata = new Dictionary<string, object>
            {
                ["Language"] = linqDefinition.Language,
                ["Parameters"] = linqDefinition.Parameters,
                ["Expression"] = linqDefinition.Expression
            }
        };

        return coreDefinition;
    }

    /// <summary>
    /// Converts a Core KernelDefinition to a LINQ KernelDefinition.
    /// </summary>
    /// <param name="coreDefinition">The core kernel definition to convert.</param>
    /// <returns>A LINQ kernel definition with data extracted from metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when coreDefinition is null.</exception>
    public static LinqKernelDefinition ToLinq(CoreKernelDefinition coreDefinition)
    {
        ArgumentNullException.ThrowIfNull(coreDefinition);

        var linqDefinition = new LinqKernelDefinition
        {
            Name = coreDefinition.Name,
            CompiledSource = coreDefinition.Source
        };

        // Extract LINQ-specific data from metadata
        if (coreDefinition.Metadata.TryGetValue("Language", out var langValue) && langValue is KernelLanguage language)
        {
            linqDefinition.Language = language;
        }

        if (coreDefinition.Metadata.TryGetValue("Parameters", out var paramValue) && paramValue is List<KernelParameter> parameters)
        {
            linqDefinition.Parameters = parameters;
        }

        if (coreDefinition.Metadata.TryGetValue("Expression", out var exprValue) && exprValue is System.Linq.Expressions.Expression expression)
        {
            linqDefinition.Expression = expression;
        }

        return linqDefinition;
    }

    /// <summary>
    /// Safely extracts the kernel language from core definition metadata.
    /// </summary>
    /// <param name="coreDefinition">The core kernel definition.</param>
    /// <param name="defaultLanguage">The default language if none is specified.</param>
    /// <returns>The kernel language.</returns>
    public static KernelLanguage ExtractLanguage(CoreKernelDefinition coreDefinition, KernelLanguage defaultLanguage = KernelLanguage.CSharp)
    {
        ArgumentNullException.ThrowIfNull(coreDefinition);

        return coreDefinition.Metadata.TryGetValue("Language", out var langValue) && langValue is KernelLanguage language
            ? language
            : defaultLanguage;
    }

    /// <summary>
    /// Safely extracts kernel parameters from core definition metadata.
    /// </summary>
    /// <param name="coreDefinition">The core kernel definition.</param>
    /// <returns>The kernel parameters or an empty list if none are available.</returns>
    public static List<KernelParameter> ExtractParameters(CoreKernelDefinition coreDefinition)
    {
        ArgumentNullException.ThrowIfNull(coreDefinition);

        return coreDefinition.Metadata.TryGetValue("Parameters", out var paramValue) && paramValue is List<KernelParameter> parameters
            ? parameters
            : new List<KernelParameter>();
    }
}