using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotCompute.Generators.Utils;

namespace DotCompute.Generators.Backend
{
    /// <summary>
    /// Generates optimized CPU code with SIMD support.
    /// </summary>
    internal class CpuCodeGenerator
    {
        private readonly string _methodName;
        private readonly List<(string name, string type, bool isBuffer)> _parameters;
        private readonly MethodDeclarationSyntax _methodSyntax;
        private readonly VectorizationInfo _vectorizationInfo;
        private readonly int _vectorSize;

        public CpuCodeGenerator(
            string methodName,
            List<(string name, string type, bool isBuffer)> parameters,
            MethodDeclarationSyntax methodSyntax,
            int vectorSize = 8)
        {
            _methodName = methodName;
            _parameters = parameters;
            _methodSyntax = methodSyntax;
            _vectorizationInfo = SourceGeneratorHelpers.AnalyzeVectorization(methodSyntax);
            _vectorSize = vectorSize;
        }

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

            // Extract and adapt method body
            var methodBody = SourceGeneratorHelpers.ExtractMethodBody(_methodSyntax);
            if (!string.IsNullOrEmpty(methodBody))
            {
                // TODO: Transform method body for scalar execution
                sb.AppendLine("            // Original method body (needs transformation):");
                sb.AppendLine($"            // {methodBody}");
            }

            sb.AppendLine("            // TODO: Implement scalar logic");
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
            sb.AppendLine("                // TODO: Load vectors and perform operations");
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
            sb.AppendLine("                // TODO: Use AVX2 intrinsics");
            sb.AppendLine("                // var vec = Avx.LoadVector256(...);");
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
            sb.AppendLine("                // TODO: Use AVX-512 intrinsics");
            sb.AppendLine("                // var vec = Avx512F.LoadVector512(...);");
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
    }
}