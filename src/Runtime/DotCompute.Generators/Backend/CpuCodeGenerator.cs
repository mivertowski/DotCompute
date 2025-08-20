// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Generators.Utils;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Backend;

/// <summary>
/// Generates optimized CPU code with SIMD support.
/// </summary>
public class CpuCodeGenerator(
    string methodName,
    IReadOnlyList<(string name, string type, bool isBuffer)> parameters,
    MethodDeclarationSyntax methodSyntax)
{
    private readonly string _methodName = methodName;
    private readonly IReadOnlyList<(string name, string type, bool isBuffer)> _parameters = parameters;
    private readonly MethodDeclarationSyntax _methodSyntax = methodSyntax;
    private readonly VectorizationInfo _vectorizationInfo = SourceGeneratorHelpers.AnalyzeVectorization(methodSyntax);

    /// <summary>
    /// Generates the complete CPU implementation.
    /// </summary>
    public string Generate()
    {
        var sb = new StringBuilder();

        // Generate header
        _ = sb.Append(SourceGeneratorHelpers.GenerateHeader(
            "System",
            "System.Runtime.CompilerServices",
            "System.Runtime.Intrinsics",
            "System.Runtime.Intrinsics.X86",
            "System.Runtime.Intrinsics.Arm",
            "System.Threading.Tasks",
            "System.Numerics"
        ));

        // Generate class
        _ = sb.AppendLine($"namespace DotCompute.Generated.Cpu");
        _ = sb.AppendLine("{");
        _ = sb.AppendLine($"    /// <summary>");
        _ = sb.AppendLine($"    /// CPU implementation for {_methodName} kernel.");
        _ = sb.AppendLine($"    /// </summary>");
        _ = sb.AppendLine($"    public static unsafe class {_methodName}Cpu");
        _ = sb.AppendLine("    {");

        // Generate scalar implementation
        GenerateScalarImplementation(sb);

        // Generate SIMD implementation if vectorizable
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

        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("}");

        return sb.ToString();
    }

    private void GenerateScalarImplementation(StringBuilder sb)
    {
        _ = sb.AppendLine("        /// <summary>");
        _ = sb.AppendLine("        /// Scalar implementation for compatibility and remainder handling.");
        _ = sb.AppendLine("        /// </summary>");
        _ = sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        _ = sb.Append($"        private static void ExecuteScalar(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => $"{p.type} {p.name}")));
        _ = sb.AppendLine(", int start, int end)");
        _ = sb.AppendLine("        {");

        // Generate parameter validation
        var validation = SourceGeneratorHelpers.GenerateParameterValidation(_parameters);
        if (!string.IsNullOrEmpty(validation))
        {
            _ = sb.Append(SourceGeneratorHelpers.Indent(validation, 3));
        }

        // Extract and transform method body for scalar execution
        var methodBody = SourceGeneratorHelpers.ExtractMethodBody(_methodSyntax);
        if (!string.IsNullOrEmpty(methodBody))
        {
            // Transform the method body for scalar execution
            var transformedBody = TransformMethodBodyForScalar(methodBody!);
            _ = sb.AppendLine("            // Transformed scalar implementation:");
            _ = sb.AppendLine(transformedBody);
        }
        else
        {
            // Generate default scalar implementation
            GenerateDefaultScalarImplementation(sb);
        }
        _ = sb.AppendLine("            for (int i = start; i < end; i++)");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                // Process element i");
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine();
    }

    private void GenerateSimdImplementation(StringBuilder sb)
    {
        _ = sb.AppendLine("        /// <summary>");
        _ = sb.AppendLine("        /// SIMD implementation using platform-agnostic vectors.");
        _ = sb.AppendLine("        /// </summary>");
        _ = sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        _ = sb.Append($"        private static void ExecuteSimd(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => $"{p.type} {p.name}")));
        _ = sb.AppendLine(", int start, int end)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine($"            int vectorSize = Vector<float>.Count;");
        _ = sb.AppendLine("            int alignedEnd = start + ((end - start) / vectorSize) * vectorSize;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("            // Process vectors");
        _ = sb.AppendLine("            for (int i = start; i < alignedEnd; i += vectorSize)");
        _ = sb.AppendLine("            {");
        // Generate optimized SIMD operations
        GenerateSimdOperations(sb);
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine();
        _ = sb.AppendLine("            // Process remainder");
        _ = sb.AppendLine("            if (alignedEnd < end)");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                ExecuteScalar(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
        _ = sb.AppendLine(", alignedEnd, end);");
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine();
    }

    private void GenerateAvx2Implementation(StringBuilder sb)
    {
        _ = sb.AppendLine("        /// <summary>");
        _ = sb.AppendLine("        /// AVX2 optimized implementation for x86/x64 processors.");
        _ = sb.AppendLine("        /// </summary>");
        _ = sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        _ = sb.Append($"        private static void ExecuteAvx2(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => $"{p.type} {p.name}")));
        _ = sb.AppendLine(", int start, int end)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine("            const int vectorSize = 8; // 256-bit / 32-bit");
        _ = sb.AppendLine("            int alignedEnd = start + ((end - start) / vectorSize) * vectorSize;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("            // Process AVX2 vectors");
        _ = sb.AppendLine("            for (int i = start; i < alignedEnd; i += vectorSize)");
        _ = sb.AppendLine("            {");
        // Generate AVX2 intrinsic operations
        GenerateAvx2Operations(sb);
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine();
        _ = sb.AppendLine("            // Process remainder");
        _ = sb.AppendLine("            if (alignedEnd < end)");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                ExecuteScalar(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
        _ = sb.AppendLine(", alignedEnd, end);");
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine();
    }

    private void GenerateAvx512Implementation(StringBuilder sb)
    {
        _ = sb.AppendLine("        /// <summary>");
        _ = sb.AppendLine("        /// AVX-512 optimized implementation for latest x86/x64 processors.");
        _ = sb.AppendLine("        /// </summary>");
        _ = sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        _ = sb.Append($"        private static void ExecuteAvx512(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => $"{p.type} {p.name}")));
        _ = sb.AppendLine(", int start, int end)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine("            const int vectorSize = 16; // 512-bit / 32-bit");
        _ = sb.AppendLine("            int alignedEnd = start + ((end - start) / vectorSize) * vectorSize;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("            // Process AVX-512 vectors");
        _ = sb.AppendLine("            for (int i = start; i < alignedEnd; i += vectorSize)");
        _ = sb.AppendLine("            {");
        // Generate AVX-512 intrinsic operations
        GenerateAvx512Operations(sb);
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine();
        _ = sb.AppendLine("            // Process remainder");
        _ = sb.AppendLine("            if (alignedEnd < end)");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                ExecuteAvx2(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
        _ = sb.AppendLine(", alignedEnd, end);");
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine();
    }

    private void GenerateParallelImplementation(StringBuilder sb)
    {
        _ = sb.AppendLine("        /// <summary>");
        _ = sb.AppendLine("        /// Parallel implementation using task parallelism.");
        _ = sb.AppendLine("        /// </summary>");
        _ = sb.Append($"        public static void ExecuteParallel(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => $"{p.type} {p.name}")));
        _ = sb.AppendLine(", int length)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine("            int processorCount = Environment.ProcessorCount;");
        _ = sb.AppendLine("            int chunkSize = Math.Max(1024, length / processorCount);");
        _ = sb.AppendLine();
        _ = sb.AppendLine("            Parallel.ForEach(Partitioner.Create(0, length, chunkSize), range =>");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                Execute(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
        _ = sb.AppendLine(", range.Item1, range.Item2);");
        _ = sb.AppendLine("            });");
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine();
    }

    private void GenerateExecuteMethod(StringBuilder sb)
    {
        _ = sb.AppendLine("        /// <summary>");
        _ = sb.AppendLine("        /// Main execution method that selects the best implementation.");
        _ = sb.AppendLine("        /// </summary>");
        _ = sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        _ = sb.Append($"        public static void Execute(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => $"{p.type} {p.name}")));
        _ = sb.AppendLine(", int start, int end)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine("            int length = end - start;");
        _ = sb.AppendLine();
        _ = sb.AppendLine("            // Select best implementation based on hardware support");
        _ = sb.AppendLine("            if (length < 32)");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                // Small arrays - use scalar");
        _ = sb.AppendLine("                ExecuteScalar(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
        _ = sb.AppendLine(", start, end);");
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine("            else if (Avx512F.IsSupported && length >= 64)");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                ExecuteAvx512(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
        _ = sb.AppendLine(", start, end);");
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine("            else if (Avx2.IsSupported && length >= 32)");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                ExecuteAvx2(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
        _ = sb.AppendLine(", start, end);");
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine("            else if (Vector.IsHardwareAccelerated)");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                ExecuteSimd(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
        _ = sb.AppendLine(", start, end);");
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine("            else");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                ExecuteScalar(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
        _ = sb.AppendLine(", start, end);");
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine();
        _ = sb.AppendLine("        /// <summary>");
        _ = sb.AppendLine("        /// Convenience overload for full array processing.");
        _ = sb.AppendLine("        /// </summary>");
        _ = sb.Append($"        public static void Execute(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => $"{p.type} {p.name}")));
        _ = sb.AppendLine(", int length)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine("            Execute(");
        _ = sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
        _ = sb.AppendLine(", 0, length);");
        _ = sb.AppendLine("        }");
    }

    /// <summary>
    /// Transforms method body for scalar execution.
    /// </summary>
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

    /// <summary>
    /// Generates default scalar implementation.
    /// </summary>
    private void GenerateDefaultScalarImplementation(StringBuilder sb)
    {
        _ = sb.AppendLine("            // Default scalar implementation based on operation type");

        if (_vectorizationInfo.IsArithmetic)
        {
            _ = sb.AppendLine("            for (int i = start; i < end; i++)");
            _ = sb.AppendLine("            {");
            _ = sb.AppendLine("                // Perform arithmetic operation on elements");
            _ = sb.AppendLine("                output[i] = ProcessArithmetic(input1[i], input2[i]);");
            _ = sb.AppendLine("            }");
        }
        else if (_vectorizationInfo.IsMemoryOperation)
        {
            _ = sb.AppendLine("            for (int i = start; i < end; i++)");
            _ = sb.AppendLine("            {");
            _ = sb.AppendLine("                // Perform memory operation");
            _ = sb.AppendLine("                output[i] = input[i];");
            _ = sb.AppendLine("            }");
        }
        else
        {
            _ = sb.AppendLine("            for (int i = start; i < end; i++)");
            _ = sb.AppendLine("            {");
            _ = sb.AppendLine("                // Generic element processing");
            _ = sb.AppendLine("                ProcessElement(i);");
            _ = sb.AppendLine("            }");
        }
    }

    /// <summary>
    /// Generates optimized SIMD operations.
    /// </summary>
    private void GenerateSimdOperations(StringBuilder sb)
    {
        _ = sb.AppendLine("            // Optimized SIMD vector processing");
        _ = sb.AppendLine("            unsafe");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                for (int i = start; i < alignedEnd; i += vectorSize)");
        _ = sb.AppendLine("                {");

        if (_vectorizationInfo.IsArithmetic)
        {
            _ = sb.AppendLine("                    // Load vectors for arithmetic operation");
            _ = sb.AppendLine("                    var vec1 = new Vector<float>(input1, i);");
            _ = sb.AppendLine("                    var vec2 = new Vector<float>(input2, i);");
            _ = sb.AppendLine("                    var result = Vector.Add(vec1, vec2);");
            _ = sb.AppendLine("                    result.CopyTo(output, i);");
        }
        else if (_vectorizationInfo.IsMemoryOperation)
        {
            _ = sb.AppendLine("                    // Vectorized memory copy");
            _ = sb.AppendLine("                    var vec = new Vector<float>(input, i);");
            _ = sb.AppendLine("                    vec.CopyTo(output, i);");
        }
        else
        {
            _ = sb.AppendLine("                    // Generic vector processing");
            _ = sb.AppendLine("                    var vec = new Vector<float>(data, i);");
            _ = sb.AppendLine("                    var processed = ProcessVector(vec);");
            _ = sb.AppendLine("                    processed.CopyTo(output, i);");
        }

        _ = sb.AppendLine("                }");
        _ = sb.AppendLine("            }");
    }

    /// <summary>
    /// Generates AVX2 intrinsic operations.
    /// </summary>
    private void GenerateAvx2Operations(StringBuilder sb)
    {
        _ = sb.AppendLine("            // AVX2 256-bit vector operations");
        _ = sb.AppendLine("            unsafe");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                for (int i = start; i < alignedEnd; i += vectorSize)");
        _ = sb.AppendLine("                {");

        if (_vectorizationInfo.IsArithmetic)
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
        else
        {
            _ = sb.AppendLine("                    // AVX2 memory operations");
            _ = sb.AppendLine("                    fixed (float* pInput = &input[i], pOutput = &output[i])");
            _ = sb.AppendLine("                    {");
            _ = sb.AppendLine("                        var vec = Avx.LoadVector256(pInput);");
            _ = sb.AppendLine("                        Avx.Store(pOutput, vec);");
            _ = sb.AppendLine("                    }");
        }

        _ = sb.AppendLine("                }");
        _ = sb.AppendLine("            }");
    }

    /// <summary>
    /// Generates AVX-512 intrinsic operations.
    /// </summary>
    private void GenerateAvx512Operations(StringBuilder sb)
    {
        _ = sb.AppendLine("            // AVX-512 512-bit vector operations");
        _ = sb.AppendLine("            unsafe");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine("                for (int i = start; i < alignedEnd; i += vectorSize)");
        _ = sb.AppendLine("                {");

        if (_vectorizationInfo.IsArithmetic)
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
        else
        {
            _ = sb.AppendLine("                    // AVX-512 memory operations");
            _ = sb.AppendLine("                    fixed (float* pInput = &input[i], pOutput = &output[i])");
            _ = sb.AppendLine("                    {");
            _ = sb.AppendLine("                        var vec = Avx512F.LoadVector512(pInput);");
            _ = sb.AppendLine("                        Avx512F.Store(pOutput, vec);");
            _ = sb.AppendLine("                    }");
        }

        _ = sb.AppendLine("                }");
        _ = sb.AppendLine("            }");
    }
}
