// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using DotCompute.Generators.Kernel.Generation;
using DotCompute.Generators.Models.Kernel;

namespace DotCompute.Generators.Kernel;

/// <summary>
/// Incremental source generator for DotCompute kernels.
/// Generates backend-specific implementations for kernel methods using a modular architecture.
/// </summary>
/// <remarks>
/// This generator has been refactored into focused components for better maintainability:
/// - KernelSyntaxReceiver: Syntax analysis and filtering
/// - KernelMethodAnalyzer: Semantic analysis of kernel methods
/// - KernelParameterAnalyzer: Parameter validation and analysis
/// - KernelCodeBuilder: Orchestrates code generation across backends
/// - KernelWrapperEmitter: Generates unified wrapper classes
/// - KernelValidator: Validates kernel compliance
/// </remarks>
[Generator]
public class KernelSourceGenerator : IIncrementalGenerator
{
    private readonly KernelMethodAnalyzer _methodAnalyzer;
    private readonly KernelCodeBuilder _codeBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelSourceGenerator"/> class.
    /// </summary>
    public KernelSourceGenerator()
    {
        _methodAnalyzer = new KernelMethodAnalyzer();
        _codeBuilder = new KernelCodeBuilder();
    }

    /// <summary>
    /// Initializes the incremental source generator.
    /// </summary>
    /// <param name="context">The incremental generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Create syntax providers using the new modular architecture
        var kernelMethods = KernelSyntaxReceiver.CreateKernelMethodProvider(
            context,
            _methodAnalyzer.AnalyzeKernelMethod);

        var kernelClasses = KernelSyntaxReceiver.CreateKernelClassProvider(
            context,
            KernelMethodAnalyzer.AnalyzeKernelClass);

        // Combine all sources for generation
        var kernelsToGenerate = kernelMethods
            .Collect()
            .Combine(kernelClasses.Collect())
            .Combine(context.CompilationProvider);

        // Register the source output handler
        context.RegisterSourceOutput(kernelsToGenerate,
            ExecuteGeneration);
    }

    /// <summary>
    /// Executes the kernel code generation process.
    /// </summary>
    /// <param name="context">The source production context.</param>
    /// <param name="sources">The combined source data containing kernel methods, classes, and compilation.</param>
    private void ExecuteGeneration(
        SourceProductionContext context,
        ((ImmutableArray<KernelMethodInfo> Left, ImmutableArray<KernelClassInfo> Right) Left, Compilation Right) sources)
    {
        var methods = sources.Left.Left;
        var classes = sources.Left.Right;
        var compilation = sources.Right;

        // Early exit if no kernels found
        if (methods.IsDefaultOrEmpty && classes.IsDefaultOrEmpty)
        {
            return;
        }

        try
        {
            // Use the code builder to orchestrate all generation
            _codeBuilder.BuildKernelSources(methods, classes, compilation, context);
        }
        catch (System.Exception ex)
        {
            // Report generation errors as diagnostics
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "DC_KG000",
                    "Kernel Generation Failed",
                    "Failed to generate kernel code: {0}",
                    "KernelGeneration",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                ex.Message));
        }
    }
}