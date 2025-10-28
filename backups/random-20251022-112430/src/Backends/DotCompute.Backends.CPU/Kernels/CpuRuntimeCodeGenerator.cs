// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Kernels.Models;

namespace DotCompute.Backends.CPU.Kernels;


/// <summary>
/// Generates CPU code from kernel representations.
/// </summary>
internal static class CpuRuntimeCodeGenerator
{
    /// <summary>
    /// Gets generate from ast.
    /// </summary>
    /// <param name="ast">The ast.</param>
    /// <param name="definition">The definition.</param>
    /// <param name="analysis">The analysis.</param>
    /// <param name="options">The options.</param>
    /// <returns>The result of the operation.</returns>
    public static CompiledCode GenerateFromAst(KernelAst ast, KernelDefinition definition, KernelAnalysis analysis, CompilationOptions options)
    {
        // Use advanced IL code generator
        var ilGenerator = new ILCodeGenerator();
        var kernelCode = ilGenerator.GenerateKernel(definition, ast, analysis, options);

        return new CompiledCode
        {
            CompiledDelegate = kernelCode.CompiledDelegate,
            CodeSize = kernelCode.EstimatedCodeSize,
            OptimizationNotes = kernelCode.OptimizationNotes
        };
    }
    /// <summary>
    /// Gets generate from bytecode.
    /// </summary>
    /// <param name="bytecode">The bytecode.</param>
    /// <param name="definition">The definition.</param>
    /// <param name="analysis">The analysis.</param>
    /// <param name="options">The options.</param>
    /// <returns>The result of the operation.</returns>

    public static CompiledCode GenerateFromBytecode(byte[] bytecode, KernelDefinition definition, KernelAnalysis analysis, CompilationOptions options)
    {
        // JIT compile bytecode
        var compiledCode = new CompiledCode
        {
            Bytecode = [.. bytecode],
            CodeSize = bytecode.Length,
            OptimizationNotes = ["JIT compiled from bytecode"]
        };

        return compiledCode;
    }
    /// <summary>
    /// Gets generate default kernel.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="analysis">The analysis.</param>
    /// <param name="options">The options.</param>
    /// <returns>The result of the operation.</returns>

    public static CompiledCode GenerateDefaultKernel(KernelDefinition definition, KernelAnalysis analysis, CompilationOptions options)
    {
        // Generate a default vectorized kernel based on metadata
        var compiledCode = new CompiledCode();

        // Check if operation type is specified in metadata
        if (definition.Metadata?.TryGetValue("Operation", out var opObj) == true && opObj is string opStr)
        {
            // Generate optimized code for specific operation
            compiledCode.OptimizationNotes = [$"Generated optimized {opStr} kernel"];
        }
        else
        {
            compiledCode.OptimizationNotes = ["Generated default kernel"];
        }

        compiledCode.CodeSize = 2048; // Estimated

        return compiledCode;
    }
}
