// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Generators.Utils;

namespace DotCompute.Generators.Utils;

/// <summary>
/// Provides loop optimization and unrolling functionality for code generation.
/// </summary>
public static class LoopOptimizer
{
    /// <summary>
    /// Configuration options for loop optimization.
    /// </summary>
    public class LoopOptimizationOptions
    {
        public bool EnableUnrolling { get; set; }
        public int UnrollFactor { get; set; } = 4;
        public bool EnablePrefetching { get; set; }
        public bool EnableVectorization { get; set; }
        public bool ParallelizeIfPossible { get; set; }
    }

    /// <summary>
    /// Context for loop generation.
    /// </summary>
    public class LoopContext
    {
        public string IndexVariable { get; set; } = string.Empty;
        public string LimitVariable { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public int StartValue { get; set; }

        public int Increment { get; set; } = 1;
        public LoopOptimizationOptions Options { get; set; } = new();
    }

    /// <summary>
    /// Generates optimized loop code based on provided options.
    /// </summary>
    /// <param name="indexVar">The loop index variable name.</param>
    /// <param name="limitVar">The loop limit variable name.</param>
    /// <param name="body">The loop body code.</param>
    /// <param name="options">Optimization options for the loop.</param>
    /// <returns>Generated optimized loop code.</returns>
    public static string GenerateOptimizedLoop(string indexVar, string limitVar, string body, LoopOptimizationOptions? options = null)
    {
        ArgumentValidation.ThrowIfNullOrEmpty(indexVar);
        ArgumentValidation.ThrowIfNullOrEmpty(limitVar);
        ArgumentValidation.ThrowIfNullOrEmpty(body);


        options ??= new LoopOptimizationOptions();


        var context = new LoopContext
        {
            IndexVariable = indexVar,
            LimitVariable = limitVar,
            Body = body,
            Options = options
        };

        if (options.EnableUnrolling)
        {
            return GenerateUnrolledLoop(context);
        }

        return GenerateRegularLoop(context);
    }

    /// <summary>
    /// Generates an unrolled loop for improved performance.
    /// </summary>
    /// <param name="context">The loop generation context.</param>
    /// <returns>Generated unrolled loop code.</returns>
    public static string GenerateUnrolledLoop(LoopContext context)
    {
        ArgumentValidation.ThrowIfNull(context);


        var sb = new StringBuilder();
        var unrollFactor = context.Options.UnrollFactor;

        // Generate unrolled loop

        _ = sb.AppendLine($"var remainder = {context.LimitVariable} % {unrollFactor};");
        _ = sb.AppendLine($"var limit = {context.LimitVariable} - remainder;");
        _ = sb.AppendLine();

        // Add prefetching hint if enabled

        if (context.Options.EnablePrefetching)
        {
            _ = sb.AppendLine("// Prefetch data for better cache utilization");
            _ = sb.AppendLine("#if NET7_0_OR_GREATER");
            _ = sb.AppendLine("global::System.Runtime.Intrinsics.X86.Sse.Prefetch0(ref Unsafe.AsRef<byte>(null));");
            _ = sb.AppendLine("#endif");
            _ = sb.AppendLine();
        }

        // Main unrolled loop

        _ = sb.AppendLine($"for (int {context.IndexVariable} = {context.StartValue}; {context.IndexVariable} < limit; {context.IndexVariable} += {unrollFactor})");
        _ = sb.AppendLine("{");

        for (var i = 0; i < unrollFactor; i++)
        {
            var unrolledBody = GenerateUnrolledIteration(context, i);
            _ = sb.AppendLine($"    {unrolledBody}");
        }

        _ = sb.AppendLine("}");
        _ = sb.AppendLine();

        // Handle remainder

        _ = sb.AppendLine("// Handle remainder");
        _ = sb.AppendLine($"for (int {context.IndexVariable} = limit; {context.IndexVariable} < {context.LimitVariable}; {context.IndexVariable} += {context.Increment})");
        _ = sb.AppendLine("{");
        _ = sb.AppendLine($"    {context.Body}");
        _ = sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a regular (non-unrolled) loop.
    /// </summary>
    /// <param name="context">The loop generation context.</param>
    /// <returns>Generated regular loop code.</returns>
    public static string GenerateRegularLoop(LoopContext context)
    {
        ArgumentValidation.ThrowIfNull(context);


        var sb = new StringBuilder();


        if (context.Options.ParallelizeIfPossible)
        {
            return GenerateParallelLoop(context);
        }


        _ = sb.AppendLine($"for (int {context.IndexVariable} = {context.StartValue}; {context.IndexVariable} < {context.LimitVariable}; {context.IndexVariable} += {context.Increment})");
        _ = sb.AppendLine("{");
        _ = sb.AppendLine($"    {context.Body}");
        _ = sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Calculates the optimal unroll factor based on loop characteristics.
    /// </summary>
    /// <param name="loopSize">The expected loop iteration count.</param>
    /// <param name="operationComplexity">The complexity of operations in the loop body.</param>
    /// <returns>Optimal unroll factor.</returns>
    public static int CalculateOptimalUnrollFactor(int loopSize, int operationComplexity = 1)
    {
        // Simple heuristic: balance between code size and performance
        if (loopSize < 8)
        {
            return 1; // Don't unroll small loops
        }


        if (loopSize < 32)
        {
            return 2;
        }


        if (loopSize < 128)
        {
            return 4;
        }

        // For large loops, consider operation complexity

        return operationComplexity switch
        {
            <= 2 => 8,
            <= 4 => 4,
            _ => 2
        };
    }

    /// <summary>
    /// Generates a parallelized loop using Parallel.For.
    /// </summary>
    private static string GenerateParallelLoop(LoopContext context)
    {
        var sb = new StringBuilder();


        _ = sb.AppendLine($"Parallel.For({context.StartValue}, {context.LimitVariable}, {context.IndexVariable} =>");
        _ = sb.AppendLine("{");
        _ = sb.AppendLine($"    {context.Body}");
        _ = sb.AppendLine("});");


        return sb.ToString();
    }

    /// <summary>
    /// Generates code for a single unrolled iteration.
    /// </summary>
    private static string GenerateUnrolledIteration(LoopContext context, int offset)
    {
        var indexExpr = offset == 0

            ? context.IndexVariable

            : $"{context.IndexVariable} + {offset}";

        // Replace index variable references in the body

        return context.Body.Replace($"{{{context.IndexVariable}}}", $"{{{indexExpr}}}");
    }

    /// <summary>
    /// Generates a tiled loop for better cache utilization.
    /// </summary>
    /// <param name="outerLimit">The outer loop limit.</param>
    /// <param name="innerLimit">The inner loop limit.</param>
    /// <param name="tileSize">The size of each tile.</param>
    /// <param name="body">The loop body code.</param>
    /// <returns>Generated tiled loop code.</returns>
    public static string GenerateTiledLoop(string outerLimit, string innerLimit, int tileSize, string body)
    {
        ArgumentValidation.ThrowIfNullOrEmpty(outerLimit);
        ArgumentValidation.ThrowIfNullOrEmpty(innerLimit);
        ArgumentValidation.ThrowIfNullOrEmpty(body);


        var sb = new StringBuilder();


        _ = sb.AppendLine($"const int tileSize = {tileSize};");
        _ = sb.AppendLine($"for (int ii = 0; ii < {outerLimit}; ii += tileSize)");
        _ = sb.AppendLine("{");
        _ = sb.AppendLine($"    for (int jj = 0; jj < {innerLimit}; jj += tileSize)");
        _ = sb.AppendLine("    {");
        _ = sb.AppendLine($"        for (int i = ii; i < Math.Min(ii + tileSize, {outerLimit}); i++)");
        _ = sb.AppendLine("        {");
        _ = sb.AppendLine($"            for (int j = jj; j < Math.Min(jj + tileSize, {innerLimit}); j++)");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine($"                {body}");
        _ = sb.AppendLine("            }");
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine("    }");
        _ = sb.AppendLine("}");


        return sb.ToString();
    }

    /// <summary>
    /// Analyzes loop characteristics to determine optimization strategy.
    /// </summary>
    /// <param name="iterationCount">Expected number of iterations.</param>
    /// <param name="hasDataDependency">Whether the loop has data dependencies.</param>
    /// <param name="isReductionLoop">Whether this is a reduction loop.</param>
    /// <returns>Recommended optimization options.</returns>
    public static LoopOptimizationOptions AnalyzeAndRecommendOptimizations(
        int iterationCount,

        bool hasDataDependency,

        bool isReductionLoop)
    {
        var options = new LoopOptimizationOptions();

        // Can't parallelize if there are data dependencies

        options.ParallelizeIfPossible = !hasDataDependency && iterationCount > 1000;

        // Unrolling is beneficial for medium-sized loops without complex dependencies

        options.EnableUnrolling = iterationCount > 8 && iterationCount < 10000 && !isReductionLoop;


        if (options.EnableUnrolling)
        {
            options.UnrollFactor = CalculateOptimalUnrollFactor(iterationCount);
        }

        // Prefetching helps with memory-bound loops

        options.EnablePrefetching = iterationCount > 100;

        // Vectorization for arithmetic-heavy loops

        options.EnableVectorization = !hasDataDependency && iterationCount > 16;


        return options;
    }
}
