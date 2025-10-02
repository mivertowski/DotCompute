// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using DotCompute.Generators.Models.Kernel;

namespace DotCompute.Generators.Kernel.Generation;

/// <summary>
/// Coordinates the generation of kernel source code across different backends.
/// Orchestrates the compilation process and manages the integration of various
/// code generation components.
/// </summary>
/// <remarks>
/// This class serves as the main orchestrator for kernel code generation,
/// coordinating between different emitters and ensuring consistent output
/// across all supported backends. It manages the overall generation workflow
/// and handles cross-cutting concerns like dependency injection integration.
/// </remarks>
public sealed class KernelCodeBuilder
{
    private readonly KernelWrapperEmitter _wrapperEmitter;
    private readonly KernelRegistrationEmitter _registrationEmitter;
    private readonly KernelMetadataEmitter _metadataEmitter;
    private readonly KernelValidator _validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelCodeBuilder"/> class.
    /// </summary>
    public KernelCodeBuilder()
    {
        _wrapperEmitter = new KernelWrapperEmitter();
        _registrationEmitter = new KernelRegistrationEmitter();
        _metadataEmitter = new KernelMetadataEmitter();
        _validator = new KernelValidator();
    }

    /// <summary>
    /// Builds all kernel source code files for the given kernel methods and classes.
    /// </summary>
    /// <param name="kernelMethods">The kernel methods to generate code for.</param>
    /// <param name="kernelClasses">The kernel classes to generate code for.</param>
    /// <param name="compilation">The compilation context.</param>
    /// <param name="context">The source production context.</param>
    /// <remarks>
    /// This method coordinates the entire code generation process:
    /// 1. Validates all kernel methods and classes
    /// 2. Generates kernel registry and metadata
    /// 3. Generates backend-specific implementations
    /// 4. Generates unified wrapper classes
    /// 5. Generates invoker classes for dynamic execution
    /// </remarks>
    public static void BuildKernelSources(
        IEnumerable<KernelMethodInfo> kernelMethods,
        IEnumerable<KernelClassInfo> kernelClasses,
        Compilation compilation,
        SourceProductionContext context)
    {
        var methodList = kernelMethods.ToList();
        var classList = kernelClasses.ToList();

        if (methodList.Count == 0 && classList.Count == 0)
        {
            return;
        }

        // Validate all kernels before generation
        var validationResults = ValidateKernels(methodList, classList);
        if (validationResults.HasErrors)
        {
            ReportValidationErrors(validationResults, context);
            return;
        }

        // Generate kernel registry and metadata
        GenerateKernelRegistry(methodList, classList, context);

        // Generate backend-specific implementations for each method
        foreach (var method in methodList)
        {
            GenerateKernelImplementations(method, compilation, context);
        }

        // Generate unified wrapper classes
        foreach (var method in methodList)
        {
            GenerateUnifiedWrapper(method, compilation, context);
        }

        // Generate kernel invokers for dynamic execution
        foreach (var kernelClass in classList)
        {
            GenerateKernelInvoker(kernelClass, methodList, context);
        }
    }

    /// <summary>
    /// Validates all kernel methods and classes for compatibility.
    /// </summary>
    /// <param name="methods">The kernel methods to validate.</param>
    /// <param name="classes">The kernel classes to validate.</param>
    /// <returns>A validation result containing any errors or warnings.</returns>
    private static KernelValidationResult ValidateKernels(
        List<KernelMethodInfo> methods,
        List<KernelClassInfo> classes)
    {
        var result = new KernelValidationResult();

        // Validate each method
        foreach (var method in methods)
        {
            var methodValidation = KernelValidator.ValidateKernelMethod(method);
            result.Merge(methodValidation);
        }

        // Validate each class
        foreach (var kernelClass in classes)
        {
            var classValidation = KernelValidator.ValidateKernelClass(kernelClass, methods);
            result.Merge(classValidation);
        }

        return result;
    }

    /// <summary>
    /// Reports validation errors to the compilation context.
    /// </summary>
    /// <param name="validationResults">The validation results to report.</param>
    /// <param name="context">The source production context.</param>
    private static void ReportValidationErrors(
        KernelValidationResult validationResults,
        SourceProductionContext context)
    {
        foreach (var error in validationResults.Errors)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "DC_KG001",
                    "Kernel Generation Error",
                    error,
                    "KernelGeneration",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None));
        }

        foreach (var warning in validationResults.Warnings)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "DC_KG002",
                    "Kernel Generation Warning",
                    warning,
                    "KernelGeneration",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true),
                Location.None));
        }
    }

    /// <summary>
    /// Generates the kernel registry containing all kernel metadata.
    /// </summary>
    /// <param name="kernelMethods">The kernel methods to include in the registry.</param>
    /// <param name="kernelClasses">The kernel classes to include in the registry.</param>
    /// <param name="context">The source production context.</param>
    private static void GenerateKernelRegistry(
        List<KernelMethodInfo> kernelMethods,
        List<KernelClassInfo> kernelClasses,
        SourceProductionContext context)
    {
        var registrySource = KernelRegistrationEmitter.GenerateKernelRegistry(kernelMethods, kernelClasses);
        context.AddSource("KernelRegistry.g.cs", SourceText.From(registrySource, Encoding.UTF8));

        // Generate additional metadata files
        var metadataSource = KernelMetadataEmitter.GenerateKernelMetadata(kernelMethods);
        context.AddSource("KernelMetadata.g.cs", SourceText.From(metadataSource, Encoding.UTF8));
    }

    /// <summary>
    /// Generates backend-specific implementations for a kernel method.
    /// </summary>
    /// <param name="method">The kernel method to generate implementations for.</param>
    /// <param name="compilation">The compilation context.</param>
    /// <param name="context">The source production context.</param>
    private static void GenerateKernelImplementations(
        KernelMethodInfo method,
        Compilation compilation,
        SourceProductionContext context)
    {
        foreach (var backend in method.Backends)
        {
            var implementationSource = GenerateBackendImplementation(method, backend, compilation);
            if (!string.IsNullOrEmpty(implementationSource))
            {
                var fileName = $"{SanitizeFileName(method.ContainingType)}_{method.Name}_{backend}.g.cs";
                context.AddSource(fileName, SourceText.From(implementationSource!, Encoding.UTF8));
            }
        }
    }

    /// <summary>
    /// Generates a unified wrapper class for cross-backend execution.
    /// </summary>
    /// <param name="method">The kernel method to generate a wrapper for.</param>
    /// <param name="compilation">The compilation context.</param>
    /// <param name="context">The source production context.</param>
    private static void GenerateUnifiedWrapper(
        KernelMethodInfo method,
        Compilation compilation,
        SourceProductionContext context)
    {
        var wrapperSource = KernelWrapperEmitter.GenerateUnifiedWrapper(method, compilation);
        var fileName = $"{SanitizeFileName(method.ContainingType)}_{method.Name}_Unified.g.cs";
        context.AddSource(fileName, SourceText.From(wrapperSource, Encoding.UTF8));
    }

    /// <summary>
    /// Generates a kernel invoker class for dynamic execution.
    /// </summary>
    /// <param name="kernelClass">The kernel class to generate an invoker for.</param>
    /// <param name="allMethods">All kernel methods for lookup.</param>
    /// <param name="context">The source production context.</param>
    private static void GenerateKernelInvoker(
        KernelClassInfo kernelClass,
        List<KernelMethodInfo> allMethods,
        SourceProductionContext context)
    {
        var invokerSource = KernelWrapperEmitter.GenerateKernelInvoker(kernelClass, allMethods);
        var fileName = $"{kernelClass.Name}Invoker.g.cs";
        context.AddSource(fileName, SourceText.From(invokerSource, Encoding.UTF8));
    }

    /// <summary>
    /// Generates backend-specific implementation code.
    /// </summary>
    /// <param name="method">The kernel method to generate code for.</param>
    /// <param name="backend">The target backend.</param>
    /// <param name="compilation">The compilation context.</param>
    /// <returns>The generated source code, or null if generation is not supported.</returns>
    private static string? GenerateBackendImplementation(
        KernelMethodInfo method,
        string backend,
        Compilation compilation)
    {
        return backend switch
        {
            "CPU" => GenerateCpuImplementation(method),
            "CUDA" => GenerateCudaImplementation(method, compilation),
            "Metal" => GenerateMetalImplementation(method, compilation),
            "OpenCL" => GenerateOpenCLImplementation(method),
            _ => null
        };
    }

    /// <summary>
    /// Generates CPU-specific implementation with SIMD optimization.
    /// </summary>
    /// <param name="method">The kernel method to generate code for.</param>
    /// <returns>The generated C# source code for CPU execution.</returns>
    private static string GenerateCpuImplementation(KernelMethodInfo method)
    {
        var source = new StringBuilder();
        _ = source.AppendLine("// <auto-generated/>");
        _ = source.AppendLine("using System;");
        _ = source.AppendLine("using System.Runtime.CompilerServices;");
        _ = source.AppendLine("using System.Runtime.Intrinsics;");
        _ = source.AppendLine("using System.Runtime.Intrinsics.X86;");
        _ = source.AppendLine("using System.Threading.Tasks;");
        _ = source.AppendLine();

        var namespaceName = $"{method.Namespace}.Generated.CPU";
        _ = source.AppendLine($"namespace {namespaceName}");
        _ = source.AppendLine("{");
        _ = source.AppendLine($"    /// <summary>");
        _ = source.AppendLine($"    /// CPU implementation of {method.Name} kernel with SIMD optimization.");
        _ = source.AppendLine($"    /// </summary>");
        _ = source.AppendLine($"    public static unsafe class {method.Name}CpuKernel");
        _ = source.AppendLine("    {");

        // Generate SIMD execution method
        GenerateCpuSIMDMethod(source, method);

        // Generate scalar fallback method
        GenerateCpuScalarMethod(source, method);

        // Generate parallel execution method if enabled
        if (method.IsParallel)
        {
            GenerateCpuParallelMethod(source, method);
        }

        _ = source.AppendLine("    }");
        _ = source.AppendLine("}");

        return source.ToString();
    }

    /// <summary>
    /// Generates CUDA-specific implementation.
    /// </summary>
    /// <param name="method">The kernel method to generate code for.</param>
    /// <param name="compilation">The compilation context.</param>
    /// <returns>The generated CUDA C source code.</returns>
    private static string GenerateCudaImplementation(KernelMethodInfo method, Compilation compilation)
    {
        // This would integrate with existing CUDA generation logic
        var source = new StringBuilder();
        _ = source.AppendLine("// <auto-generated/>");
        _ = source.AppendLine("// CUDA kernel implementation");
        _ = source.AppendLine("#include <cuda_runtime.h>");
        _ = source.AppendLine("#include <device_launch_parameters.h>");
        _ = source.AppendLine();

        // Generate CUDA kernel function
        _ = source.AppendLine($"extern \"C\" __global__ void {method.Name}_cuda_kernel(");

        // Add parameters
        for (var i = 0; i < method.Parameters.Count; i++)
        {
            var param = method.Parameters[i];
            var cudaType = ConvertToCudaType(param);
            _ = source.Append($"    {cudaType} {param.Name}");
            if (i < method.Parameters.Count - 1)
            {
                _ = source.Append(",");
            }
            _ = source.AppendLine();
        }

        _ = source.AppendLine("    , const uint32_t length)");
        _ = source.AppendLine("{");
        _ = source.AppendLine("    const uint32_t idx = blockIdx.x * blockDim.x + threadIdx.x;");
        _ = source.AppendLine("    if (idx < length) {");
        _ = source.AppendLine("        // Kernel operation placeholder");
        _ = source.AppendLine("    }");
        _ = source.AppendLine("}");

        return source.ToString();
    }

    /// <summary>
    /// Generates Metal-specific implementation.
    /// </summary>
    /// <param name="method">The kernel method to generate code for.</param>
    /// <param name="compilation">The compilation context.</param>
    /// <returns>The generated Metal shader source code.</returns>
    private static string GenerateMetalImplementation(KernelMethodInfo method, Compilation compilation)
    {
        var source = new StringBuilder();
        _ = source.AppendLine("// <auto-generated/>");
        _ = source.AppendLine("// Metal compute shader implementation");
        _ = source.AppendLine("#include <metal_stdlib>");
        _ = source.AppendLine("using namespace metal;");
        _ = source.AppendLine();

        _ = source.AppendLine($"kernel void {method.Name}_metal_kernel(");

        // Add Metal-specific parameters
        for (var i = 0; i < method.Parameters.Count; i++)
        {
            var param = method.Parameters[i];
            var metalType = ConvertToMetalType(param);
            var addressSpace = param.IsBuffer ? "device " : "constant ";
            _ = source.Append($"    {addressSpace}{metalType} {param.Name} [[buffer({i})]]");
            if (i < method.Parameters.Count - 1)
            {
                _ = source.Append(",");
            }
            _ = source.AppendLine();
        }

        _ = source.AppendLine("    , uint2 gid [[thread_position_in_grid]])");
        _ = source.AppendLine("{");
        _ = source.AppendLine("    // Metal kernel operation placeholder");
        _ = source.AppendLine("}");

        return source.ToString();
    }

    /// <summary>
    /// Generates OpenCL-specific implementation.
    /// </summary>
    /// <param name="method">The kernel method to generate code for.</param>
    /// <returns>The generated OpenCL kernel source code.</returns>
    private static string GenerateOpenCLImplementation(KernelMethodInfo method)
    {
        var source = new StringBuilder();
        _ = source.AppendLine("// <auto-generated/>");
        _ = source.AppendLine("// OpenCL kernel implementation");
        _ = source.AppendLine();

        _ = source.AppendLine($"__kernel void {method.Name}_opencl_kernel(");

        for (var i = 0; i < method.Parameters.Count; i++)
        {
            var param = method.Parameters[i];
            var openclType = ConvertToOpenCLType(param);
            var addressSpace = param.IsBuffer ? "__global " : "__constant ";
            _ = source.Append($"    {addressSpace}{openclType} {param.Name}");
            if (i < method.Parameters.Count - 1)
            {
                _ = source.Append(",");
            }
            _ = source.AppendLine();
        }

        _ = source.AppendLine("    , int length)");
        _ = source.AppendLine("{");
        _ = source.AppendLine("    int idx = get_global_id(0);");
        _ = source.AppendLine("    if (idx < length) {");
        _ = source.AppendLine("        // OpenCL kernel operation placeholder");
        _ = source.AppendLine("    }");
        _ = source.AppendLine("}");

        return source.ToString();
    }

    // Helper methods for CPU code generation
    private static void GenerateCpuSIMDMethod(StringBuilder source, KernelMethodInfo method)
    {
        _ = source.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        _ = source.AppendLine($"        public static void ExecuteSIMD(/* parameters */)");
        _ = source.AppendLine("        {");
        _ = source.AppendLine("            // SIMD implementation placeholder");
        _ = source.AppendLine("        }");
        _ = source.AppendLine();
    }

    private static void GenerateCpuScalarMethod(StringBuilder source, KernelMethodInfo method)
    {
        _ = source.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        _ = source.AppendLine($"        public static void ExecuteScalar(/* parameters */)");
        _ = source.AppendLine("        {");
        _ = source.AppendLine("            // Scalar implementation placeholder");
        _ = source.AppendLine("        }");
        _ = source.AppendLine();
    }

    private static void GenerateCpuParallelMethod(StringBuilder source, KernelMethodInfo method)
    {
        _ = source.AppendLine($"        public static void ExecuteParallel(/* parameters */)");
        _ = source.AppendLine("        {");
        _ = source.AppendLine("            // Parallel implementation placeholder");
        _ = source.AppendLine("        }");
        _ = source.AppendLine();
    }

    // Type conversion helper methods
    private static string ConvertToCudaType(ParameterInfo param)
    {
        // Simplified type conversion for demonstration
        var baseType = ExtractBaseType(param.Type);
        var cudaType = baseType switch
        {
            "float" => "float",
            "int" => "int32_t",
            "double" => "double",
            _ => "float"
        };

        return param.IsBuffer ? $"{cudaType}*" : cudaType;
    }

    private static string ConvertToMetalType(ParameterInfo param)
    {
        var baseType = ExtractBaseType(param.Type);
        var metalType = baseType switch
        {
            "float" => "float",
            "int" => "int",
            "double" => "double",
            _ => "float"
        };

        return param.IsBuffer ? $"{metalType}*" : metalType;
    }

    private static string ConvertToOpenCLType(ParameterInfo param)
    {
        var baseType = ExtractBaseType(param.Type);
        var openclType = baseType switch
        {
            "float" => "float",
            "int" => "int",
            "double" => "double",
            _ => "float"
        };

        return param.IsBuffer ? $"{openclType}*" : openclType;
    }

    private static string ExtractBaseType(string typeString)
    {
        // Simple type extraction - would be more sophisticated in real implementation
        if (typeString.Contains('<') && typeString.Contains('>'))
        {
            var start = typeString.IndexOf('<') + 1;
            var end = typeString.LastIndexOf('>');
            if (start > 0 && end > start)
            {
                return typeString.Substring(start, end - start);
            }
        }

        if (typeString.EndsWith("[]", StringComparison.Ordinal))
        {
            return typeString.Substring(0, typeString.Length - 2);
        }

        return typeString;
    }

    private static string SanitizeFileName(string input) => input.Replace(".", "_").Replace("<", "_").Replace(">", "_");
}

/// <summary>
/// Contains the results of kernel validation.
/// </summary>
public sealed class KernelValidationResult
{
    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets the list of validation warnings.
    /// </summary>
    public List<string> Warnings { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the validation result contains errors.
    /// </summary>
    public bool HasErrors => Errors.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the validation result contains warnings.
    /// </summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>
    /// Merges another validation result into this one.
    /// </summary>
    /// <param name="other">The validation result to merge.</param>
    public void Merge(KernelValidationResult other)
    {
        Errors.AddRange(other.Errors);
        Warnings.AddRange(other.Warnings);
    }
}