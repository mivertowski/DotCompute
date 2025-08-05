// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Generators.Utils;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Backend
{
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
            sb.Append(SourceGeneratorHelpers.GenerateHeader(
                "System",
                "System.Runtime.CompilerServices",
                "System.Runtime.Intrinsics",
                "System.Runtime.Intrinsics.X86",
                "System.Runtime.Intrinsics.Arm",
                "System.Threading.Tasks",
                "System.Numerics"
            ));

            // Generate class
            sb.AppendLine($"namespace DotCompute.Generated.Cpu");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// CPU implementation for {_methodName} kernel.");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public static unsafe class {_methodName}Cpu");
            sb.AppendLine("    {");

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

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private void GenerateScalarImplementation(StringBuilder sb)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Scalar implementation for compatibility and remainder handling.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.Append($"        private static void ExecuteScalar(");
            sb.Append(string.Join(", ", _parameters.Select(p => $"{p.type} {p.name}")));
            sb.AppendLine(", int start, int end)");
            sb.AppendLine("        {");

            // Generate parameter validation
            var validation = SourceGeneratorHelpers.GenerateParameterValidation(_parameters);
            if (!string.IsNullOrEmpty(validation))
            {
                sb.Append(SourceGeneratorHelpers.Indent(validation, 3));
            }

            // Extract and transform method body for scalar execution
            var methodBody = SourceGeneratorHelpers.ExtractMethodBody(_methodSyntax);
            if (!string.IsNullOrEmpty(methodBody))
            {
                // Transform the method body for scalar execution
                var transformedBody = TransformMethodBodyForScalar(methodBody!);
                sb.AppendLine("            // Transformed scalar implementation:");
                sb.AppendLine(transformedBody);
            }
            else
            {
                // Generate default scalar implementation
                GenerateDefaultScalarImplementation(sb);
            }
            sb.AppendLine("            for (int i = start; i < end; i++)");
            sb.AppendLine("            {");
            sb.AppendLine("                // Process element i");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private void GenerateSimdImplementation(StringBuilder sb)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// SIMD implementation using platform-agnostic vectors.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.Append($"        private static void ExecuteSimd(");
            sb.Append(string.Join(", ", _parameters.Select(p => $"{p.type} {p.name}")));
            sb.AppendLine(", int start, int end)");
            sb.AppendLine("        {");
            sb.AppendLine($"            int vectorSize = Vector<float>.Count;");
            sb.AppendLine("            int alignedEnd = start + ((end - start) / vectorSize) * vectorSize;");
            sb.AppendLine();
            sb.AppendLine("            // Process vectors");
            sb.AppendLine("            for (int i = start; i < alignedEnd; i += vectorSize)");
            sb.AppendLine("            {");
            // Generate optimized SIMD operations
            GenerateSimdOperations(sb);
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            // Process remainder");
            sb.AppendLine("            if (alignedEnd < end)");
            sb.AppendLine("            {");
            sb.AppendLine("                ExecuteScalar(");
            sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
            sb.AppendLine(", alignedEnd, end);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private void GenerateAvx2Implementation(StringBuilder sb)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// AVX2 optimized implementation for x86/x64 processors.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.Append($"        private static void ExecuteAvx2(");
            sb.Append(string.Join(", ", _parameters.Select(p => $"{p.type} {p.name}")));
            sb.AppendLine(", int start, int end)");
            sb.AppendLine("        {");
            sb.AppendLine("            const int vectorSize = 8; // 256-bit / 32-bit");
            sb.AppendLine("            int alignedEnd = start + ((end - start) / vectorSize) * vectorSize;");
            sb.AppendLine();
            sb.AppendLine("            // Process AVX2 vectors");
            sb.AppendLine("            for (int i = start; i < alignedEnd; i += vectorSize)");
            sb.AppendLine("            {");
            // Generate AVX2 intrinsic operations
            GenerateAvx2Operations(sb);
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            // Process remainder");
            sb.AppendLine("            if (alignedEnd < end)");
            sb.AppendLine("            {");
            sb.AppendLine("                ExecuteScalar(");
            sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
            sb.AppendLine(", alignedEnd, end);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private void GenerateAvx512Implementation(StringBuilder sb)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// AVX-512 optimized implementation for latest x86/x64 processors.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.Append($"        private static void ExecuteAvx512(");
            sb.Append(string.Join(", ", _parameters.Select(p => $"{p.type} {p.name}")));
            sb.AppendLine(", int start, int end)");
            sb.AppendLine("        {");
            sb.AppendLine("            const int vectorSize = 16; // 512-bit / 32-bit");
            sb.AppendLine("            int alignedEnd = start + ((end - start) / vectorSize) * vectorSize;");
            sb.AppendLine();
            sb.AppendLine("            // Process AVX-512 vectors");
            sb.AppendLine("            for (int i = start; i < alignedEnd; i += vectorSize)");
            sb.AppendLine("            {");
            // Generate AVX-512 intrinsic operations
            GenerateAvx512Operations(sb);
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            // Process remainder");
            sb.AppendLine("            if (alignedEnd < end)");
            sb.AppendLine("            {");
            sb.AppendLine("                ExecuteAvx2(");
            sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
            sb.AppendLine(", alignedEnd, end);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private void GenerateParallelImplementation(StringBuilder sb)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Parallel implementation using task parallelism.");
            sb.AppendLine("        /// </summary>");
            sb.Append($"        public static void ExecuteParallel(");
            sb.Append(string.Join(", ", _parameters.Select(p => $"{p.type} {p.name}")));
            sb.AppendLine(", int length)");
            sb.AppendLine("        {");
            sb.AppendLine("            int processorCount = Environment.ProcessorCount;");
            sb.AppendLine("            int chunkSize = Math.Max(1024, length / processorCount);");
            sb.AppendLine();
            sb.AppendLine("            Parallel.ForEach(Partitioner.Create(0, length, chunkSize), range =>");
            sb.AppendLine("            {");
            sb.AppendLine("                Execute(");
            sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
            sb.AppendLine(", range.Item1, range.Item2);");
            sb.AppendLine("            });");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private void GenerateExecuteMethod(StringBuilder sb)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Main execution method that selects the best implementation.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.Append($"        public static void Execute(");
            sb.Append(string.Join(", ", _parameters.Select(p => $"{p.type} {p.name}")));
            sb.AppendLine(", int start, int end)");
            sb.AppendLine("        {");
            sb.AppendLine("            int length = end - start;");
            sb.AppendLine();
            sb.AppendLine("            // Select best implementation based on hardware support");
            sb.AppendLine("            if (length < 32)");
            sb.AppendLine("            {");
            sb.AppendLine("                // Small arrays - use scalar");
            sb.AppendLine("                ExecuteScalar(");
            sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
            sb.AppendLine(", start, end);");
            sb.AppendLine("            }");
            sb.AppendLine("            else if (Avx512F.IsSupported && length >= 64)");
            sb.AppendLine("            {");
            sb.AppendLine("                ExecuteAvx512(");
            sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
            sb.AppendLine(", start, end);");
            sb.AppendLine("            }");
            sb.AppendLine("            else if (Avx2.IsSupported && length >= 32)");
            sb.AppendLine("            {");
            sb.AppendLine("                ExecuteAvx2(");
            sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
            sb.AppendLine(", start, end);");
            sb.AppendLine("            }");
            sb.AppendLine("            else if (Vector.IsHardwareAccelerated)");
            sb.AppendLine("            {");
            sb.AppendLine("                ExecuteSimd(");
            sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
            sb.AppendLine(", start, end);");
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            sb.AppendLine("                ExecuteScalar(");
            sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
            sb.AppendLine(", start, end);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Convenience overload for full array processing.");
            sb.AppendLine("        /// </summary>");
            sb.Append($"        public static void Execute(");
            sb.Append(string.Join(", ", _parameters.Select(p => $"{p.type} {p.name}")));
            sb.AppendLine(", int length)");
            sb.AppendLine("        {");
            sb.AppendLine("            Execute(");
            sb.Append(string.Join(", ", _parameters.Select(p => p.name)));
            sb.AppendLine(", 0, length);");
            sb.AppendLine("        }");
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
            sb.AppendLine("            // Default scalar implementation based on operation type");

            if (_vectorizationInfo.IsArithmetic)
            {
                sb.AppendLine("            for (int i = start; i < end; i++)");
                sb.AppendLine("            {");
                sb.AppendLine("                // Perform arithmetic operation on elements");
                sb.AppendLine("                output[i] = ProcessArithmetic(input1[i], input2[i]);");
                sb.AppendLine("            }");
            }
            else if (_vectorizationInfo.IsMemoryOperation)
            {
                sb.AppendLine("            for (int i = start; i < end; i++)");
                sb.AppendLine("            {");
                sb.AppendLine("                // Perform memory operation");
                sb.AppendLine("                output[i] = input[i];");
                sb.AppendLine("            }");
            }
            else
            {
                sb.AppendLine("            for (int i = start; i < end; i++)");
                sb.AppendLine("            {");
                sb.AppendLine("                // Generic element processing");
                sb.AppendLine("                ProcessElement(i);");
                sb.AppendLine("            }");
            }
        }

        /// <summary>
        /// Generates optimized SIMD operations.
        /// </summary>
        private void GenerateSimdOperations(StringBuilder sb)
        {
            sb.AppendLine("            // Optimized SIMD vector processing");
            sb.AppendLine("            unsafe");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int i = start; i < alignedEnd; i += vectorSize)");
            sb.AppendLine("                {");

            if (_vectorizationInfo.IsArithmetic)
            {
                sb.AppendLine("                    // Load vectors for arithmetic operation");
                sb.AppendLine("                    var vec1 = new Vector<float>(input1, i);");
                sb.AppendLine("                    var vec2 = new Vector<float>(input2, i);");
                sb.AppendLine("                    var result = Vector.Add(vec1, vec2);");
                sb.AppendLine("                    result.CopyTo(output, i);");
            }
            else if (_vectorizationInfo.IsMemoryOperation)
            {
                sb.AppendLine("                    // Vectorized memory copy");
                sb.AppendLine("                    var vec = new Vector<float>(input, i);");
                sb.AppendLine("                    vec.CopyTo(output, i);");
            }
            else
            {
                sb.AppendLine("                    // Generic vector processing");
                sb.AppendLine("                    var vec = new Vector<float>(data, i);");
                sb.AppendLine("                    var processed = ProcessVector(vec);");
                sb.AppendLine("                    processed.CopyTo(output, i);");
            }

            sb.AppendLine("                }");
            sb.AppendLine("            }");
        }

        /// <summary>
        /// Generates AVX2 intrinsic operations.
        /// </summary>
        private void GenerateAvx2Operations(StringBuilder sb)
        {
            sb.AppendLine("            // AVX2 256-bit vector operations");
            sb.AppendLine("            unsafe");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int i = start; i < alignedEnd; i += vectorSize)");
            sb.AppendLine("                {");

            if (_vectorizationInfo.IsArithmetic)
            {
                sb.AppendLine("                    // AVX2 arithmetic operations");
                sb.AppendLine("                    fixed (float* pInput1 = &input1[i], pInput2 = &input2[i], pOutput = &output[i])");
                sb.AppendLine("                    {");
                sb.AppendLine("                        var vec1 = Avx.LoadVector256(pInput1);");
                sb.AppendLine("                        var vec2 = Avx.LoadVector256(pInput2);");
                sb.AppendLine("                        var result = Avx.Add(vec1, vec2);");
                sb.AppendLine("                        Avx.Store(pOutput, result);");
                sb.AppendLine("                    }");
            }
            else
            {
                sb.AppendLine("                    // AVX2 memory operations");
                sb.AppendLine("                    fixed (float* pInput = &input[i], pOutput = &output[i])");
                sb.AppendLine("                    {");
                sb.AppendLine("                        var vec = Avx.LoadVector256(pInput);");
                sb.AppendLine("                        Avx.Store(pOutput, vec);");
                sb.AppendLine("                    }");
            }

            sb.AppendLine("                }");
            sb.AppendLine("            }");
        }

        /// <summary>
        /// Generates AVX-512 intrinsic operations.
        /// </summary>
        private void GenerateAvx512Operations(StringBuilder sb)
        {
            sb.AppendLine("            // AVX-512 512-bit vector operations");
            sb.AppendLine("            unsafe");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int i = start; i < alignedEnd; i += vectorSize)");
            sb.AppendLine("                {");

            if (_vectorizationInfo.IsArithmetic)
            {
                sb.AppendLine("                    // AVX-512 arithmetic operations");
                sb.AppendLine("                    fixed (float* pInput1 = &input1[i], pInput2 = &input2[i], pOutput = &output[i])");
                sb.AppendLine("                    {");
                sb.AppendLine("                        var vec1 = Avx512F.LoadVector512(pInput1);");
                sb.AppendLine("                        var vec2 = Avx512F.LoadVector512(pInput2);");
                sb.AppendLine("                        var result = Avx512F.Add(vec1, vec2);");
                sb.AppendLine("                        Avx512F.Store(pOutput, result);");
                sb.AppendLine("                    }");
            }
            else
            {
                sb.AppendLine("                    // AVX-512 memory operations");
                sb.AppendLine("                    fixed (float* pInput = &input[i], pOutput = &output[i])");
                sb.AppendLine("                    {");
                sb.AppendLine("                        var vec = Avx512F.LoadVector512(pInput);");
                sb.AppendLine("                        Avx512F.Store(pOutput, vec);");
                sb.AppendLine("                    }");
            }

            sb.AppendLine("                }");
            sb.AppendLine("            }");
        }
    }
}
