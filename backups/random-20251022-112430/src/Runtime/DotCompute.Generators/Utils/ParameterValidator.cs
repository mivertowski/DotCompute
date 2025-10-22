// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Generators.Models;

namespace DotCompute.Generators.Utils;

/// <summary>
/// Provides parameter validation code generation for kernel methods.
/// </summary>
public static class ParameterValidator
{
    /// <summary>
    /// Generates parameter validation code for kernel parameters.
    /// </summary>
    /// <param name="parameters">The kernel parameters to validate.</param>
    /// <returns>Generated validation code.</returns>
    public static string GenerateParameterValidation(IEnumerable<KernelParameter> parameters)
    {
        ArgumentValidation.ThrowIfNull(parameters);


        var sb = new StringBuilder();

        foreach (var param in parameters)
        {
            var validation = GenerateValidationForParameter(param);
            if (!string.IsNullOrEmpty(validation))
            {
                _ = sb.AppendLine(validation);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates validation code for a buffer parameter.
    /// </summary>
    /// <param name="parameter">The buffer parameter to validate.</param>
    /// <returns>Generated validation code for buffer parameter.</returns>
    public static string ValidateBufferParameter(KernelParameter parameter)
    {
        ArgumentValidation.ThrowIfNull(parameter);


        if (!parameter.IsBuffer)
        {
            throw new ArgumentException("Parameter is not a buffer type", nameof(parameter));
        }

        return $"ArgumentValidation.ThrowIfNull({parameter.Name}, nameof({parameter.Name}));";
    }

    /// <summary>
    /// Generates validation code for a Span or Memory parameter.
    /// </summary>
    /// <param name="parameter">The span/memory parameter to validate.</param>
    /// <returns>Generated validation code for span/memory parameter.</returns>
    public static string ValidateSpanParameter(KernelParameter parameter)
    {
        ArgumentValidation.ThrowIfNull(parameter);


        if (!IsSpanOrMemoryType(parameter.Type))
        {
            throw new ArgumentException("Parameter is not a Span or Memory type", nameof(parameter));
        }

        return $"if ({parameter.Name}.IsEmpty) throw new ArgumentException(\"Buffer cannot be empty\", nameof({parameter.Name}));";
    }

    /// <summary>
    /// Generates validation code for a single parameter based on its type.
    /// </summary>
    private static string GenerateValidationForParameter(KernelParameter param)
    {
        if (param.IsBuffer)
        {
            return ValidateBufferParameter(param);
        }


        if (IsSpanOrMemoryType(param.Type))
        {
            return ValidateSpanParameter(param);
        }

        // Add more validation types as needed
        return string.Empty;
    }

    /// <summary>
    /// Determines if a type is a Span or Memory type.
    /// </summary>
    private static bool IsSpanOrMemoryType(string type)
    {
        return type.IndexOf("Span", StringComparison.OrdinalIgnoreCase) >= 0 ||

               type.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Generates range validation for numeric parameters.
    /// </summary>
    /// <param name="parameter">The parameter to validate.</param>
    /// <param name="min">Minimum allowed value.</param>
    /// <param name="max">Maximum allowed value.</param>
    /// <returns>Generated range validation code.</returns>
    public static string ValidateNumericRange(KernelParameter parameter, double? min = null, double? max = null)
    {
        ArgumentValidation.ThrowIfNull(parameter);


        var sb = new StringBuilder();


        if (min.HasValue)
        {
            _ = sb.AppendLine($"if ({parameter.Name} < {min.Value}) throw new ArgumentOutOfRangeException(nameof({parameter.Name}), \"{parameter.Name} must be >= {min.Value}\");");
        }


        if (max.HasValue)
        {
            _ = sb.AppendLine($"if ({parameter.Name} > {max.Value}) throw new ArgumentOutOfRangeException(nameof({parameter.Name}), \"{parameter.Name} must be <= {max.Value}\");");
        }


        return sb.ToString();
    }

    /// <summary>
    /// Generates dimension compatibility validation for array parameters.
    /// </summary>
    /// <param name="parameters">Parameters that should have compatible dimensions.</param>
    /// <returns>Generated dimension validation code.</returns>
    public static string ValidateDimensionCompatibility(IEnumerable<KernelParameter> parameters)
    {
        ArgumentValidation.ThrowIfNull(parameters);


        var bufferParams = parameters.Where(p => p.IsBuffer || IsSpanOrMemoryType(p.Type)).ToList();


        if (bufferParams.Count < 2)
        {
            return string.Empty;
        }


        var sb = new StringBuilder();
        var firstParam = bufferParams[0];


        for (var i = 1; i < bufferParams.Count; i++)
        {
            var param = bufferParams[i];
            _ = sb.AppendLine($"if ({firstParam.Name}.Length != {param.Name}.Length) throw new ArgumentException(\"Buffer dimensions must match\", nameof({param.Name}));");
        }


        return sb.ToString();
    }
}
