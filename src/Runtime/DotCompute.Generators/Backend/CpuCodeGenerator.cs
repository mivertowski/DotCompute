// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Generators.Models;
using DotCompute.Generators.Utils;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Backend;

/// <summary>
/// Generates optimized CPU code with SIMD support for kernel execution.
/// This generator creates multiple implementations (scalar, SIMD, AVX2, AVX-512) 
/// and selects the best one based on hardware capabilities at runtime.
/// </summary>
public class CpuCodeGenerator
{
    #region Constants
    
    // Array size thresholds for optimization selection
    private const int SmallArrayThreshold = 32;
    private const int MinVectorSize = 32;
    private const int MinAvx512Size = 64;
    private const int DefaultChunkSize = 1024;
    
    // SIMD vector sizes (elements per vector)
    private const int Avx2VectorSize = 8;  // 256-bit / 32-bit float
    private const int Avx512VectorSize = 16; // 512-bit / 32-bit float
    
    // Code generation constants
    private const int BaseIndentLevel = 2;
    private const int MethodBodyIndentLevel = 3;
    private const int LoopBodyIndentLevel = 4;
    
    #endregion
    
    #region Fields
    
    private readonly string _methodName;
    private readonly IReadOnlyList<KernelParameter> _parameters;
    private readonly MethodDeclarationSyntax _methodSyntax;
    private readonly VectorizationInfo _vectorizationInfo;
    
    #endregion
    
    #region Constructor
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CpuCodeGenerator"/> class.
    /// </summary>
    /// <param name="methodName">The name of the kernel method to generate.</param>
    /// <param name="parameters">The list of kernel parameters.</param>
    /// <param name="methodSyntax">The method syntax tree for analysis.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public CpuCodeGenerator(
        string methodName,
        IReadOnlyList<KernelParameter> parameters,
        MethodDeclarationSyntax methodSyntax)
    {
        _methodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _methodSyntax = methodSyntax ?? throw new ArgumentNullException(nameof(methodSyntax));
        _vectorizationInfo = VectorizationAnalyzer.AnalyzeVectorization(_methodSyntax);
        
        // Validate all parameters
        foreach (var parameter in _parameters)
        {
            parameter.Validate();
        }
    }
    
    #endregion

    #region Public Methods
    
    /// <summary>
    /// Generates the complete CPU implementation with multiple optimization paths.
    /// </summary>
    /// <returns>The generated C# code as a string.</returns>
    public string Generate()
    {
        var sb = new StringBuilder();

        GenerateFileHeader(sb);
        GenerateNamespaceAndClass(sb);
        GenerateImplementations(sb);
        GenerateClassClosing(sb);

        return sb.ToString();
    }
    
    #endregion
    
    #region Private Methods - Main Structure Generation
    
    /// <summary>
    /// Generates the file header with required using statements.
    /// </summary>
    private static void GenerateFileHeader(StringBuilder sb)
    {
        _ = sb.Append(CodeFormatter.GenerateHeader(
            "System",
            "global::System.Runtime.CompilerServices",
            "global::System.Runtime.Intrinsics",
            "global::System.Runtime.Intrinsics.X86",
            "global::System.Runtime.Intrinsics.Arm",
            "System.Threading.Tasks",
            "System.Numerics"
        ));
    }
    
    /// <summary>
    /// Generates the namespace declaration and class opening.
    /// </summary>
    private void GenerateNamespaceAndClass(StringBuilder sb)
    {
        _ = sb.AppendLine($"namespace DotCompute.Generated.Cpu");
        _ = sb.AppendLine("{");
        _ = sb.AppendLine($"    /// <summary>");
        _ = sb.AppendLine($"    /// CPU implementation for {_methodName} kernel.");
        _ = sb.AppendLine($"    /// Provides multiple optimization paths including scalar, SIMD, AVX2, and AVX-512.");
        _ = sb.AppendLine($"    /// </summary>");
        _ = sb.AppendLine($"    public static unsafe class {_methodName}Cpu");
        _ = sb.AppendLine("    {");
    }
    
    /// <summary>
    /// Generates all implementation methods.
    /// </summary>
    private void GenerateImplementations(StringBuilder sb)
    {
        // Generate scalar implementation (always required)
        GenerateScalarImplementation(sb);

        // Generate SIMD implementations if vectorizable
        if (_vectorizationInfo.IsVectorizable)
        {
            GenerateSimdImplementation(sb);
            GenerateAvx2Implementation(sb);
            GenerateAvx512Implementation(sb);
        }

        // Generate parallel implementation
        GenerateParallelImplementation(sb);

        // Generate main execute method
        GenerateExecuteMethod(sb);
    }
    
    /// <summary>
    /// Generates the closing braces for class and namespace.
    /// </summary>
    private static void GenerateClassClosing(StringBuilder sb)
    {
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("}");
    }
    
    #endregion
    
    #region Private Methods - Scalar Implementation

    /// <summary>
    /// Generates scalar implementation for non-vectorizable code and remainder handling.
    /// </summary>
    /// <param name="sb">The StringBuilder to append the generated code to.</param>
    private void GenerateScalarImplementation(StringBuilder sb)
    {
        GenerateMethodDocumentation(sb, 
            "Scalar implementation for compatibility and remainder handling.",
            "This implementation is used for small arrays and processors without SIMD support.");
        
        GenerateMethodSignature(sb, "ExecuteScalar", true, includeRange: true);
        GenerateMethodBody(sb, () => GenerateScalarMethodContent(sb));
    }
    
    /// <summary>
    /// Generates the content of the scalar implementation method.
    /// </summary>
    private void GenerateScalarMethodContent(StringBuilder sb)
    {
        // Generate parameter validation
        var validation = ParameterValidator.GenerateParameterValidation(_parameters);
        if (!string.IsNullOrEmpty(validation))
        {
            _ = sb.Append(CodeFormatter.Indent(validation, MethodBodyIndentLevel));
        }

        // Generate scalar processing loop
        var methodBody = MethodBodyExtractor.ExtractMethodBody(_methodSyntax);
        if (!string.IsNullOrEmpty(methodBody))
        {
            GenerateTransformedScalarLoop(sb, methodBody!);
        }
        else
        {
            GenerateDefaultScalarLoop(sb);
        }
    }
    
    /// <summary>
    /// Generates a transformed scalar processing loop based on the original method body.
    /// </summary>
    private static void GenerateTransformedScalarLoop(StringBuilder sb, string methodBody)
    {
        _ = sb.AppendLine("            // Transformed scalar implementation:");
        var transformedBody = TransformMethodBodyForScalar(methodBody);
        _ = sb.AppendLine(transformedBody);
    }
    
    /// <summary>
    /// Generates a default scalar processing loop based on detected operation type.
    /// </summary>
    private void GenerateDefaultScalarLoop(StringBuilder sb)
    {
        _ = sb.AppendLine("            // Default scalar implementation based on operation type");
        _ = sb.AppendLine("            for (int i = start; i < end; i++)");
        _ = sb.AppendLine("            {");
        
        if (_vectorizationInfo.IsArithmetic)
        {
            _ = sb.AppendLine("                // Perform arithmetic operation on elements");
            _ = sb.AppendLine("                output[i] = ProcessArithmetic(input1[i], input2[i]);");
        }
        else if (_vectorizationInfo.IsMemoryOperation)
        {
            _ = sb.AppendLine("                // Perform memory operation");
            _ = sb.AppendLine("                output[i] = input[i];");
        }
        else
        {
            _ = sb.AppendLine("                // Generic element processing");
            _ = sb.AppendLine("                ProcessElement(i);");
        }
        
        _ = sb.AppendLine("            }");
    }
    
    #endregion

    #region Private Methods - SIMD Implementation

    /// <summary>
    /// Generates SIMD implementation using platform-agnostic global::System.Numerics.Vector.
    /// </summary>
    /// <param name="sb">The StringBuilder to append the generated code to.</param>
    private void GenerateSimdImplementation(StringBuilder sb)
    {
        GenerateMethodDocumentation(sb,
            "SIMD implementation using platform-agnostic vectors.",
            "Works on any platform with hardware vector support.");
        
        GenerateMethodSignature(sb, "ExecuteSimd", true, includeRange: true);
        GenerateMethodBody(sb, () => GenerateSimdMethodContent(sb));
    }
    
    /// <summary>
    /// Generates the content of the SIMD implementation method.
    /// </summary>
    private void GenerateSimdMethodContent(StringBuilder sb)
    {
        _ = sb.AppendLine($"            int vectorSize = Vector<float>.Count;");
        _ = sb.AppendLine("            int alignedEnd = start + ((end - start) / vectorSize) * vectorSize;");
        _ = sb.AppendLine();
        
        GenerateSimdProcessingLoop(sb);
        GenerateRemainderHandling(sb, "ExecuteScalar");
    }
    
    /// <summary>
    /// Generates the SIMD processing loop.
    /// </summary>
    private void GenerateSimdProcessingLoop(StringBuilder sb)
    {
        _ = sb.AppendLine("            // Process vectors");
        _ = sb.AppendLine("            for (int i = start; i < alignedEnd; i += vectorSize)");
        _ = sb.AppendLine("            {");
        GenerateSimdOperations(sb);
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine();
    }

    #endregion

    #region Private Methods - AVX2 Implementation

    /// <summary>
    /// Generates AVX2 optimized implementation for x86/x64 processors.
    /// </summary>
    private void GenerateAvx2Implementation(StringBuilder sb)
    {
        GenerateMethodDocumentation(sb,
            "AVX2 optimized implementation for x86/x64 processors.",
            "Uses 256-bit vector operations for improved performance.");
        
        GenerateMethodSignature(sb, "ExecuteAvx2", true, includeRange: true);
        GenerateMethodBody(sb, () => GenerateAvx2MethodContent(sb));
    }
    
    /// <summary>
    /// Generates the content of the AVX2 implementation method.
    /// </summary>
    private void GenerateAvx2MethodContent(StringBuilder sb)
    {
        _ = sb.AppendLine($"            const int vectorSize = {Avx2VectorSize}; // 256-bit / 32-bit");
        _ = sb.AppendLine("            int alignedEnd = start + ((end - start) / vectorSize) * vectorSize;");
        _ = sb.AppendLine();
        
        GenerateAvx2ProcessingLoop(sb);
        GenerateRemainderHandling(sb, "ExecuteScalar");
    }
    
    /// <summary>
    /// Generates the AVX2 processing loop.
    /// </summary>
    private void GenerateAvx2ProcessingLoop(StringBuilder sb)
    {
        _ = sb.AppendLine("            // Process AVX2 vectors");
        _ = sb.AppendLine("            for (int i = start; i < alignedEnd; i += vectorSize)");
        _ = sb.AppendLine("            {");
        GenerateAvx2Operations(sb);
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine();
    }

    #endregion

    #region Private Methods - AVX-512 Implementation

    /// <summary>
    /// Generates AVX-512 optimized implementation for latest x86/x64 processors.
    /// </summary>
    private void GenerateAvx512Implementation(StringBuilder sb)
    {
        GenerateMethodDocumentation(sb,
            "AVX-512 optimized implementation for latest x86/x64 processors.",
            "Uses 512-bit vector operations for maximum throughput.");
        
        GenerateMethodSignature(sb, "ExecuteAvx512", true, includeRange: true);
        GenerateMethodBody(sb, () => GenerateAvx512MethodContent(sb));
    }
    
    /// <summary>
    /// Generates the content of the AVX-512 implementation method.
    /// </summary>
    private void GenerateAvx512MethodContent(StringBuilder sb)
    {
        _ = sb.AppendLine($"            const int vectorSize = {Avx512VectorSize}; // 512-bit / 32-bit");
        _ = sb.AppendLine("            int alignedEnd = start + ((end - start) / vectorSize) * vectorSize;");
        _ = sb.AppendLine();
        
        GenerateAvx512ProcessingLoop(sb);
        GenerateRemainderHandling(sb, "ExecuteAvx2");
    }
    
    /// <summary>
    /// Generates the AVX-512 processing loop.
    /// </summary>
    private void GenerateAvx512ProcessingLoop(StringBuilder sb)
    {
        _ = sb.AppendLine("            // Process AVX-512 vectors");
        _ = sb.AppendLine("            for (int i = start; i < alignedEnd; i += vectorSize)");
        _ = sb.AppendLine("            {");
        GenerateAvx512Operations(sb);
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine();
    }

    #endregion

    #region Private Methods - Parallel Implementation

    /// <summary>
    /// Generates parallel implementation using task parallelism.
    /// </summary>
    private void GenerateParallelImplementation(StringBuilder sb)
    {
        GenerateMethodDocumentation(sb,
            "Parallel implementation using task parallelism.",
            "Automatically partitions work across available CPU cores.");
        
        GenerateMethodSignature(sb, "ExecuteParallel", false, includeRange: false, includeLength: true);
        GenerateMethodBody(sb, () => GenerateParallelMethodContent(sb));
    }
    
    /// <summary>
    /// Generates the content of the parallel implementation method.
    /// </summary>
    private void GenerateParallelMethodContent(StringBuilder sb)
    {
        _ = sb.AppendLine("            int processorCount = Environment.ProcessorCount;");
        _ = sb.AppendLine($"            int chunkSize = Math.Max({DefaultChunkSize}, length / processorCount);");
        _ = sb.AppendLine();
        _ = sb.AppendLine("            Parallel.ForEach(Partitioner.Create(0, length, chunkSize), range =>");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                Execute(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => p.Name)));
        _ = sb.AppendLine(", range.Item1, range.Item2);");
        _ = sb.AppendLine("            });");
    }

    #endregion

    #region Private Methods - Main Execute Method

    /// <summary>
    /// Generates the main execute method that selects the best implementation.
    /// </summary>
    private void GenerateExecuteMethod(StringBuilder sb)
    {
        GenerateMainExecuteMethod(sb);
        GenerateConvenienceOverload(sb);
    }
    
    /// <summary>
    /// Generates the main execute method with hardware detection.
    /// </summary>
    private void GenerateMainExecuteMethod(StringBuilder sb)
    {
        GenerateMethodDocumentation(sb,
            "Main execution method that selects the best implementation.",
            "Automatically chooses between scalar, SIMD, AVX2, and AVX-512 based on hardware support and data size.");
        
        _ = sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        _ = sb.Append($"        public static void Execute(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => p.GetDeclaration())));
        _ = sb.AppendLine(", int start, int end)");
        _ = sb.AppendLine("        {");
        
        GenerateImplementationSelection(sb);
        
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine();
    }
    
    /// <summary>
    /// Generates the implementation selection logic.
    /// </summary>
    private void GenerateImplementationSelection(StringBuilder sb)
    {
        _ = sb.AppendLine("            int length = end - start;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("            // Select best implementation based on hardware support and data size");
        
        // Small arrays - use scalar
        _ = sb.AppendLine($"            if (length < {SmallArrayThreshold})");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                // Small arrays - use scalar for better cache efficiency");
        GenerateMethodCall(sb, "ExecuteScalar", includeRange: true);
        _ = sb.AppendLine("            }");
        
        // AVX-512 path
        _ = sb.AppendLine($"            else if (Avx512F.IsSupported && length >= {MinAvx512Size})");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                // Use AVX-512 for maximum throughput on supported hardware");
        GenerateMethodCall(sb, "ExecuteAvx512", includeRange: true);
        _ = sb.AppendLine("            }");
        
        // AVX2 path
        _ = sb.AppendLine($"            else if (Avx2.IsSupported && length >= {MinVectorSize})");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                // Use AVX2 for good performance on modern x86/x64");
        GenerateMethodCall(sb, "ExecuteAvx2", includeRange: true);
        _ = sb.AppendLine("            }");
        
        // Generic SIMD path
        _ = sb.AppendLine("            else if (Vector.IsHardwareAccelerated)");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                // Use platform-agnostic SIMD");
        GenerateMethodCall(sb, "ExecuteSimd", includeRange: true);
        _ = sb.AppendLine("            }");
        
        // Fallback to scalar
        _ = sb.AppendLine("            else");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                // Fallback to scalar implementation");
        GenerateMethodCall(sb, "ExecuteScalar", includeRange: true);
        _ = sb.AppendLine("            }");
    }
    
    /// <summary>
    /// Generates a convenience overload for full array processing.
    /// </summary>
    private void GenerateConvenienceOverload(StringBuilder sb)
    {
        GenerateMethodDocumentation(sb,
            "Convenience overload for full array processing.",
            "Processes the entire array from index 0 to length.");
        
        _ = sb.Append($"        public static void Execute(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => p.GetDeclaration())));
        _ = sb.AppendLine(", int length)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine("            Execute(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => p.Name)));
        _ = sb.AppendLine(", 0, length);");
        _ = sb.AppendLine("        }");
    }

    #endregion
    
    #region Private Methods - Vector Operations Generation

    /// <summary>
    /// Generates optimized SIMD operations using global::System.Numerics.Vector.
    /// </summary>
    private void GenerateSimdOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("                // Optimized SIMD vector processing");
        
        if (_vectorizationInfo.IsArithmetic)
        {
            GenerateSimdArithmeticOperations(sb);
        }
        else if (_vectorizationInfo.IsMemoryOperation)
        {
            GenerateSimdMemoryOperations(sb);
        }
        else
        {
            GenerateGenericSimdOperations(sb);
        }
    }
    
    /// <summary>
    /// Generates SIMD arithmetic operations.
    /// </summary>
    private static void GenerateSimdArithmeticOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("                // Load vectors for arithmetic operation");
        _ = sb.AppendLine("                var vec1 = new Vector<float>(input1, i);");
        _ = sb.AppendLine("                var vec2 = new Vector<float>(input2, i);");
        _ = sb.AppendLine("                var result = Vector.Add(vec1, vec2);");
        _ = sb.AppendLine("                result.CopyTo(output, i);");
    }
    
    /// <summary>
    /// Generates SIMD memory operations.
    /// </summary>
    private static void GenerateSimdMemoryOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("                // Vectorized memory copy");
        _ = sb.AppendLine("                var vec = new Vector<float>(input, i);");
        _ = sb.AppendLine("                vec.CopyTo(output, i);");
    }
    
    /// <summary>
    /// Generates generic SIMD operations.
    /// </summary>
    private static void GenerateGenericSimdOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("                // Generic vector processing");
        _ = sb.AppendLine("                var vec = new Vector<float>(data, i);");
        _ = sb.AppendLine("                var processed = ProcessVector(vec);");
        _ = sb.AppendLine("                processed.CopyTo(output, i);");
    }

    /// <summary>
    /// Generates AVX2 intrinsic operations for x86/x64 processors.
    /// </summary>
    private void GenerateAvx2Operations(StringBuilder sb)
    {
        _ = sb.AppendLine("                // AVX2 256-bit vector operations");
        _ = sb.AppendLine("                unsafe");
        _ = sb.AppendLine("                {");
        
        if (_vectorizationInfo.IsArithmetic)
        {
            GenerateAvx2ArithmeticOperations(sb);
        }
        else
        {
            GenerateAvx2MemoryOperations(sb);
        }
        
        _ = sb.AppendLine("                }");
    }
    
    /// <summary>
    /// Generates AVX2 arithmetic operations.
    /// </summary>
    private static void GenerateAvx2ArithmeticOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("                    // AVX2 arithmetic operations");
        _ = sb.AppendLine("                    fixed (float* pInput1 = &input1[i], pInput2 = &input2[i], pOutput = &output[i])");
        _ = sb.AppendLine("                    {");
        _ = sb.AppendLine("                        var vec1 = Avx.LoadVector256(pInput1);");
        _ = sb.AppendLine("                        var vec2 = Avx.LoadVector256(pInput2);");
        _ = sb.AppendLine("                        var result = Avx.Add(vec1, vec2);");
        _ = sb.AppendLine("                        Avx.Store(pOutput, result);");
        _ = sb.AppendLine("                    }");
    }
    
    /// <summary>
    /// Generates AVX2 memory operations.
    /// </summary>
    private static void GenerateAvx2MemoryOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("                    // AVX2 memory operations");
        _ = sb.AppendLine("                    fixed (float* pInput = &input[i], pOutput = &output[i])");
        _ = sb.AppendLine("                    {");
        _ = sb.AppendLine("                        var vec = Avx.LoadVector256(pInput);");
        _ = sb.AppendLine("                        Avx.Store(pOutput, vec);");
        _ = sb.AppendLine("                    }");
    }

    /// <summary>
    /// Generates AVX-512 intrinsic operations for latest x86/x64 processors.
    /// </summary>
    private void GenerateAvx512Operations(StringBuilder sb)
    {
        _ = sb.AppendLine("                // AVX-512 512-bit vector operations");
        _ = sb.AppendLine("                unsafe");
        _ = sb.AppendLine("                {");
        
        if (_vectorizationInfo.IsArithmetic)
        {
            GenerateAvx512ArithmeticOperations(sb);
        }
        else
        {
            GenerateAvx512MemoryOperations(sb);
        }
        
        _ = sb.AppendLine("                }");
    }
    
    /// <summary>
    /// Generates AVX-512 arithmetic operations.
    /// </summary>
    private static void GenerateAvx512ArithmeticOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("                    // AVX-512 arithmetic operations");
        _ = sb.AppendLine("                    fixed (float* pInput1 = &input1[i], pInput2 = &input2[i], pOutput = &output[i])");
        _ = sb.AppendLine("                    {");
        _ = sb.AppendLine("                        var vec1 = Avx512F.LoadVector512(pInput1);");
        _ = sb.AppendLine("                        var vec2 = Avx512F.LoadVector512(pInput2);");
        _ = sb.AppendLine("                        var result = Avx512F.Add(vec1, vec2);");
        _ = sb.AppendLine("                        Avx512F.Store(pOutput, result);");
        _ = sb.AppendLine("                    }");
    }
    
    /// <summary>
    /// Generates AVX-512 memory operations.
    /// </summary>
    private static void GenerateAvx512MemoryOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("                    // AVX-512 memory operations");
        _ = sb.AppendLine("                    fixed (float* pInput = &input[i], pOutput = &output[i])");
        _ = sb.AppendLine("                    {");
        _ = sb.AppendLine("                        var vec = Avx512F.LoadVector512(pInput);");
        _ = sb.AppendLine("                        Avx512F.Store(pOutput, vec);");
        _ = sb.AppendLine("                    }");
    }
    
    #endregion
    
    #region Private Methods - Helper Functions
    
    /// <summary>
    /// Generates method documentation comments.
    /// </summary>
    private static void GenerateMethodDocumentation(StringBuilder sb, string summary, string? remarks = null)
    {
        _ = sb.AppendLine("        /// <summary>");
        _ = sb.AppendLine($"        /// {summary}");
        _ = sb.AppendLine("        /// </summary>");
        
        if (!string.IsNullOrEmpty(remarks))
        {
            _ = sb.AppendLine("        /// <remarks>");
            _ = sb.AppendLine($"        /// {remarks}");
            _ = sb.AppendLine("        /// </remarks>");
        }
    }
    
    /// <summary>
    /// Generates a method signature.
    /// </summary>
    private void GenerateMethodSignature(StringBuilder sb, string methodName, bool isPrivate, 
        bool includeRange = false, bool includeLength = false)
    {
        _ = sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        _ = sb.Append($"        {(isPrivate ? "private" : "public")} static void {methodName}(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => p.GetDeclaration())));
        
        if (includeRange)
        {
            _ = sb.Append(", int start, int end");
        }
        else if (includeLength)
        {
            _ = sb.Append(", int length");
        }
        
        _ = sb.AppendLine(")");
    }
    
    /// <summary>
    /// Generates a method body with the provided content generator.
    /// </summary>
    private static void GenerateMethodBody(StringBuilder sb, Action contentGenerator)
    {
        _ = sb.AppendLine("        {");
        contentGenerator();
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine();
    }
    
    /// <summary>
    /// Generates remainder handling code.
    /// </summary>
    private void GenerateRemainderHandling(StringBuilder sb, string fallbackMethod)
    {
        _ = sb.AppendLine("            // Process remainder");
        _ = sb.AppendLine("            if (alignedEnd < end)");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine($"                {fallbackMethod}(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => p.Name)));
        _ = sb.AppendLine(", alignedEnd, end);");
        _ = sb.AppendLine("            }");
    }
    
    /// <summary>
    /// Generates a method call with the specified parameters.
    /// </summary>
    private void GenerateMethodCall(StringBuilder sb, string methodName, bool includeRange)
    {
        _ = sb.AppendLine($"                {methodName}(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => p.Name)));
        
        if (includeRange)
        {
            _ = sb.AppendLine(", start, end);");
        }
        else
        {
            _ = sb.AppendLine(");");
        }
    }
    
    /// <summary>
    /// Transforms the original method body for scalar execution within a specified range.
    /// </summary>
    /// <param name="methodBody">The original method body to transform.</param>
    /// <returns>The transformed method body suitable for scalar execution.</returns>
    private static string TransformMethodBodyForScalar(string methodBody)
    {
        if (string.IsNullOrEmpty(methodBody))
        {
            return string.Empty;
        }

        var transformedBody = methodBody
            .Replace("{", "")
            .Replace("}", "")
            .Trim();

        // Handle common patterns
        if (transformedBody.Contains("for") && transformedBody.Contains("++"))
        {
            // Already has loop structure - adapt for range
            return $"            for (int i = start; i < end; i++)\n            {{\n                {transformedBody.Replace("i++", "").Trim()}\n            }}";
        }
        else if (transformedBody.Contains("[") && transformedBody.Contains("]"))
        {
            // Array access pattern - adapt for indexed operation
            return $"            for (int i = start; i < end; i++)\n            {{\n                // Process element at index i\n                {transformedBody}\n            }}";
        }
        else
        {
            // Generic operation
            return $"            for (int i = start; i < end; i++)\n            {{\n                {transformedBody}\n            }}";
        }
    }
    
    #endregion
}