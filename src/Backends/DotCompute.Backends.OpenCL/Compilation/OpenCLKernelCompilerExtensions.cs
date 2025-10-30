// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Compilation;

/// <summary>
/// Extensions for OpenCLKernelCompiler to support C# to OpenCL C translation.
/// </summary>
public static class OpenCLKernelCompilerExtensions
{
    /// <summary>
    /// Compiles a C# kernel definition by first translating to OpenCL C, then compiling to binary.
    /// </summary>
    /// <param name="compiler">The OpenCL kernel compiler.</param>
    /// <param name="definition">The C# kernel definition.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A compiled kernel ready for execution.</returns>
    public static async Task<CompiledKernel> CompileFromCSharpAsync(
        this OpenCLKernelCompiler compiler,
        KernelDefinition definition,
        CompilationOptions options,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        // Step 1: Translate C# to OpenCL C
        var translator = new CSharpToOpenCLTranslator(logger);
        var openclSource = await translator.TranslateAsync(definition, cancellationToken)
            .ConfigureAwait(false);

        // Step 2: Compile OpenCL C to binary
        var kernelName = definition.EntryPoint ?? definition.Name;
        var compiledKernel = await compiler.CompileAsync(
            openclSource,
            kernelName,
            options,
            cancellationToken)
            .ConfigureAwait(false);

        return compiledKernel;
    }

    /// <summary>
    /// Determines if a kernel definition contains C# source that needs translation.
    /// </summary>
    /// <param name="definition">The kernel definition to check.</param>
    /// <returns>True if the kernel is C# and needs translation; false if it's already OpenCL C.</returns>
    public static bool IsCSharpKernel(this KernelDefinition definition)
    {
        if (definition?.Source == null)
        {
            return false;
        }

        // Check for C# keywords and patterns
        var source = definition.Source;

        // Look for C# specific syntax
        if (source.Contains("Kernel.ThreadId", StringComparison.Ordinal) ||
            source.Contains("Span<", StringComparison.Ordinal) ||
            source.Contains("ReadOnlySpan<", StringComparison.Ordinal) ||
            source.Contains("public static", StringComparison.Ordinal))
        {
            return true;
        }

        // Check metadata for language indicator
        if (definition.Metadata?.TryGetValue("Language", out var langObj) == true &&
            langObj is string lang)
        {
            return lang.Equals("C#", StringComparison.OrdinalIgnoreCase) ||
                   lang.Equals("CSharp", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
