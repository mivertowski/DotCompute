// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using DotCompute.Generators.Kernel.Generation;
using DotCompute.Generators.Models.Kernel;
using Microsoft.CodeAnalysis;

namespace DotCompute.Generators.Kernel;

/// <summary>
/// Incremental source generator for DotCompute kernels.
/// Generates backend-specific implementations for kernel methods using a modular architecture.
/// Supports both standard [Kernel] and persistent [RingKernel] attributes.
/// </summary>
/// <remarks>
/// This generator has been refactored into focused components for better maintainability:
/// - KernelSyntaxReceiver: Syntax analysis and filtering (catches both [Kernel] and [RingKernel])
/// - KernelMethodAnalyzer: Semantic analysis of standard kernel methods
/// - RingKernelMethodAnalyzer: Semantic analysis of Ring Kernel methods
/// - KernelParameterAnalyzer: Parameter validation and analysis
/// - KernelCodeBuilder: Orchestrates code generation for standard kernels
/// - RingKernelCodeBuilder: Orchestrates code generation for Ring Kernels
/// - KernelWrapperEmitter: Generates unified wrapper classes
/// - KernelValidator: Validates kernel compliance
///
/// Supported kernel types:
/// - [Kernel]: Standard compute kernels for data-parallel operations
/// - [RingKernel]: Persistent kernels with message-passing for streaming workloads
/// </remarks>
[Generator]
public class KernelSourceGenerator : IIncrementalGenerator
{
    private readonly KernelMethodAnalyzer _methodAnalyzer;
    private readonly RingKernelMethodAnalyzer _ringKernelMethodAnalyzer;
    private readonly KernelCodeBuilder _codeBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelSourceGenerator"/> class.
    /// </summary>
    public KernelSourceGenerator()
    {
        _methodAnalyzer = new KernelMethodAnalyzer();
        _ringKernelMethodAnalyzer = new RingKernelMethodAnalyzer();
        _codeBuilder = new KernelCodeBuilder();
    }

    /// <summary>
    /// Initializes the incremental source generator.
    /// </summary>
    /// <param name="context">The incremental generator initialization context.</param>
    /// <remarks>
    /// This method sets up incremental providers for:
    /// - Standard [Kernel] methods: data-parallel compute kernels
    /// - [RingKernel] methods: persistent kernels with message passing
    /// - Kernel classes: classes containing kernel methods
    ///
    /// The syntax receiver catches both attribute types (uses Contains("Kernel")),
    /// then we use semantic analysis to distinguish between them.
    /// </remarks>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Create syntax providers for standard kernels
        var kernelMethods = KernelSyntaxReceiver.CreateKernelMethodProvider(
            context,
            _methodAnalyzer.AnalyzeKernelMethod);

        // Create syntax providers for Ring Kernels
        var ringKernelMethods = KernelSyntaxReceiver.CreateKernelMethodProvider(
            context,
            _ringKernelMethodAnalyzer.AnalyzeRingKernelMethod);

        // Create syntax providers for kernel classes
        var kernelClasses = KernelSyntaxReceiver.CreateKernelClassProvider(
            context,
            KernelMethodAnalyzer.AnalyzeKernelClass);

        // Combine all sources for generation
        var allKernelsToGenerate = kernelMethods
            .Collect()
            .Combine(ringKernelMethods.Collect())
            .Combine(kernelClasses.Collect())
            .Combine(context.CompilationProvider);

        // Register the source output handler
        context.RegisterSourceOutput(allKernelsToGenerate,
            ExecuteGeneration);
    }

    /// <summary>
    /// Executes the kernel code generation process for both standard kernels and Ring Kernels.
    /// </summary>
    /// <param name="context">The source production context.</param>
    /// <param name="sources">The combined source data containing kernel methods, Ring Kernel methods, classes, and compilation.</param>
    /// <remarks>
    /// This method handles code generation for:
    /// - Standard [Kernel] methods: via KernelCodeBuilder
    /// - [RingKernel] methods: via RingKernelCodeBuilder (placeholder for now)
    /// - Kernel classes: unified wrapper generation
    /// </remarks>
    private void ExecuteGeneration(
        SourceProductionContext context,
        (((ImmutableArray<KernelMethodInfo> Left, ImmutableArray<RingKernelMethodInfo> Right) Left, ImmutableArray<KernelClassInfo> Right) Left, Compilation Right) sources)
    {
        var kernelMethods = sources.Left.Left.Left;
        var ringKernelMethods = sources.Left.Left.Right;
        var classes = sources.Left.Right;
        var compilation = sources.Right;

        // Early exit if no kernels found
        if (kernelMethods.IsDefaultOrEmpty && ringKernelMethods.IsDefaultOrEmpty && classes.IsDefaultOrEmpty)
        {
            return;
        }

        try
        {
            // Generate standard kernels
            if (!kernelMethods.IsDefaultOrEmpty)
            {
                KernelCodeBuilder.BuildKernelSources(kernelMethods, classes, compilation, context);
            }

            // Generate Ring Kernels
            if (!ringKernelMethods.IsDefaultOrEmpty)
            {
                RingKernelCodeBuilder.BuildRingKernelSources(ringKernelMethods, compilation, context);
            }
        }
        catch (Exception ex)
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
