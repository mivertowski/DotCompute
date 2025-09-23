// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;
using DotCompute.Generators.Models.Kernel;

namespace DotCompute.Generators.Kernel.Generation;

/// <summary>
/// Validates kernel methods and classes for compatibility with code generation.
/// </summary>
public sealed class KernelValidator
{
    /// <summary>
    /// Validates a kernel method for code generation compatibility.
    /// </summary>
    /// <param name="method">The kernel method to validate.</param>
    /// <returns>A validation result.</returns>
    public static KernelValidationResult ValidateKernelMethod(KernelMethodInfo method)
    {
        var result = new KernelValidationResult();

        if (string.IsNullOrEmpty(method.Name))
        {
            result.Errors.Add("Kernel method name cannot be empty");
        }

        if (method.Parameters.Count == 0)
        {
            result.Warnings.Add("Kernel method has no parameters");
        }

        return result;
    }

    /// <summary>
    /// Validates a kernel class for code generation compatibility.
    /// </summary>
    /// <param name="kernelClass">The kernel class to validate.</param>
    /// <param name="allMethods">All kernel methods for reference.</param>
    /// <returns>A validation result.</returns>
    public static KernelValidationResult ValidateKernelClass(KernelClassInfo kernelClass, List<KernelMethodInfo> allMethods)
    {
        var result = new KernelValidationResult();

        if (string.IsNullOrEmpty(kernelClass.Name))
        {
            result.Errors.Add("Kernel class name cannot be empty");
        }

        return result;
    }
}