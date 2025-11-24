// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using System.Text.RegularExpressions;

namespace DotCompute.Generators.Kernel.Analysis;

/// <summary>
/// Transpiles C# ring kernel method bodies to CUDA code.
/// </summary>
/// <remarks>
/// Converts RingKernelContext API calls to CUDA intrinsics:
/// <list type="bullet">
/// <item><c>ctx.SyncThreads()</c> → <c>__syncthreads()</c></item>
/// <item><c>ctx.Now()</c> → <c>clock64()</c></item>
/// <item><c>ctx.AtomicAdd(ref x, v)</c> → <c>atomicAdd(&amp;x, v)</c></item>
/// <item><c>ctx.EnqueueOutput(msg)</c> → output buffer serialization</item>
/// </list>
/// </remarks>
public static partial class RingKernelBodyTranspiler
{
    /// <summary>
    /// Transpiles a C# method body to CUDA code.
    /// </summary>
    /// <param name="csharpBody">The C# method body source.</param>
    /// <param name="inputTypeName">The input message type name.</param>
    /// <param name="contextParamName">The context parameter name (e.g., "ctx").</param>
    /// <returns>The transpiled CUDA code.</returns>
    public static string Transpile(string csharpBody, string? inputTypeName, string contextParamName = "ctx")
    {
        if (string.IsNullOrWhiteSpace(csharpBody))
        {
            return "    // Empty handler - default echo behavior\n    return true;";
        }

        var cuda = new StringBuilder();
        var lines = csharpBody.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed == "{" || trimmed == "}")
            {
                continue;
            }

            var cudaLine = TranspileLine(trimmed, contextParamName);
            if (!string.IsNullOrEmpty(cudaLine))
            {
                cuda.AppendLine($"    {cudaLine}");
            }
        }

        // Ensure return statement
        if (!cuda.ToString().Contains("return"))
        {
            cuda.AppendLine("    return true;");
        }

        return cuda.ToString();
    }

    private static string TranspileLine(string line, string ctx)
    {
        // Skip braces
        if (line is "{" or "}")
        {
            return string.Empty;
        }

        // Barrier translations
        line = TranspileBarriers(line, ctx);

        // Temporal translations
        line = TranspileTemporal(line, ctx);

        // Atomic translations
        line = TranspileAtomics(line, ctx);

        // Memory fence translations
        line = TranspileMemoryFences(line, ctx);

        // Warp primitive translations
        line = TranspileWarpPrimitives(line, ctx);

        // Output queue translations
        line = TranspileOutputQueue(line, ctx);

        // Variable declarations
        line = TranspileVariableDeclarations(line);

        return line;
    }

    private static string TranspileBarriers(string line, string ctx)
    {
        line = line.Replace($"{ctx}.SyncThreads()", "__syncthreads()");
        line = line.Replace($"{ctx}.SyncGrid()", "cooperative_groups::this_grid().sync()");
        line = Regex.Replace(line, $@"{ctx}\.SyncWarp\(([^)]*)\)", "__syncwarp($1)");
        line = line.Replace($"{ctx}.SyncWarp()", "__syncwarp(0xFFFFFFFF)");
        return line;
    }

    private static string TranspileTemporal(string line, string ctx)
    {
        line = line.Replace($"{ctx}.Now()", "clock64()");
        line = line.Replace($"{ctx}.Tick()", "atomicAdd(&control_block->hlc_logical, 1LL)");
        return line;
    }

    private static string TranspileAtomics(string line, string ctx)
    {
        // AtomicAdd(ref x, v) → atomicAdd(&x, v)
        line = Regex.Replace(line, $@"{ctx}\.AtomicAdd\(ref\s+(\w+),\s*([^)]+)\)", "atomicAdd(&$1, $2)");
        line = Regex.Replace(line, $@"{ctx}\.AtomicCAS\(ref\s+(\w+),\s*([^,]+),\s*([^)]+)\)", "atomicCAS(&$1, $2, $3)");
        line = Regex.Replace(line, $@"{ctx}\.AtomicExch\(ref\s+(\w+),\s*([^)]+)\)", "atomicExch(&$1, $2)");
        line = Regex.Replace(line, $@"{ctx}\.AtomicMin\(ref\s+(\w+),\s*([^)]+)\)", "atomicMin(&$1, $2)");
        line = Regex.Replace(line, $@"{ctx}\.AtomicMax\(ref\s+(\w+),\s*([^)]+)\)", "atomicMax(&$1, $2)");
        return line;
    }

    private static string TranspileMemoryFences(string line, string ctx)
    {
        line = line.Replace($"{ctx}.ThreadFence()", "__threadfence()");
        line = line.Replace($"{ctx}.ThreadFenceBlock()", "__threadfence_block()");
        line = line.Replace($"{ctx}.ThreadFenceSystem()", "__threadfence_system()");
        return line;
    }

    private static string TranspileWarpPrimitives(string line, string ctx)
    {
        line = Regex.Replace(line, $@"{ctx}\.WarpShuffle\(([^,]+),\s*([^,)]+)(?:,\s*([^)]+))?\)",
            m => $"__shfl_sync({(m.Groups[3].Success ? m.Groups[3].Value : "0xFFFFFFFF")}, {m.Groups[1].Value}, {m.Groups[2].Value})");
        line = Regex.Replace(line, $@"{ctx}\.WarpShuffleDown\(([^,]+),\s*([^,)]+)(?:,\s*([^)]+))?\)",
            m => $"__shfl_down_sync({(m.Groups[3].Success ? m.Groups[3].Value : "0xFFFFFFFF")}, {m.Groups[1].Value}, {m.Groups[2].Value})");
        line = Regex.Replace(line, $@"{ctx}\.WarpBallot\(([^,)]+)(?:,\s*([^)]+))?\)",
            m => $"__ballot_sync({(m.Groups[2].Success ? m.Groups[2].Value : "0xFFFFFFFF")}, {m.Groups[1].Value})");
        line = Regex.Replace(line, $@"{ctx}\.WarpAll\(([^,)]+)(?:,\s*([^)]+))?\)",
            m => $"__all_sync({(m.Groups[2].Success ? m.Groups[2].Value : "0xFFFFFFFF")}, {m.Groups[1].Value})");
        line = Regex.Replace(line, $@"{ctx}\.WarpAny\(([^,)]+)(?:,\s*([^)]+))?\)",
            m => $"__any_sync({(m.Groups[2].Success ? m.Groups[2].Value : "0xFFFFFFFF")}, {m.Groups[1].Value})");
        return line;
    }

    private static string TranspileOutputQueue(string line, string ctx)
    {
        // EnqueueOutput(result) → serialize to output_buffer
        if (line.Contains($"{ctx}.EnqueueOutput("))
        {
            var match = Regex.Match(line, $@"{ctx}\.EnqueueOutput\(([^)]+)\)");
            if (match.Success)
            {
                var varName = match.Groups[1].Value.Trim();
                return $"// Output: {varName}\n" +
                       $"    memcpy(output_buffer, &{varName}, sizeof({varName}));\n" +
                       $"    *output_size_ptr = sizeof({varName});";
            }
        }

        // Property accesses
        line = line.Replace($"{ctx}.IsTerminationRequested", "control_block->should_terminate");
        line = line.Replace($"{ctx}.MessagesProcessed", "control_block->messages_processed");
        line = line.Replace($"{ctx}.ErrorsEncountered", "control_block->errors_encountered");
        line = line.Replace($"{ctx}.ThreadId", "threadIdx.x");
        line = line.Replace($"{ctx}.BlockId", "blockIdx.x");
        line = line.Replace($"{ctx}.GlobalThreadId", "(blockIdx.x * blockDim.x + threadIdx.x)");
        line = line.Replace($"{ctx}.BlockDim", "blockDim.x");
        line = line.Replace($"{ctx}.GridDim", "gridDim.x");
        line = line.Replace($"{ctx}.WarpId", "(threadIdx.x / 32)");
        line = line.Replace($"{ctx}.LaneId", "(threadIdx.x % 32)");

        return line;
    }

    private static string TranspileVariableDeclarations(string line)
    {
        // var x = ... → auto x = ...
        line = Regex.Replace(line, @"\bvar\s+", "auto ");

        // int, float, etc. stay the same
        // long → long long
        line = Regex.Replace(line, @"\blong\s+(?!long)", "long long ");

        // bool → int (CUDA doesn't have native bool in older versions)
        line = Regex.Replace(line, @"\bbool\s+", "int ");

        // true/false → 1/0
        line = line.Replace(" true", " 1").Replace(" false", " 0");
        line = line.Replace("(true)", "(1)").Replace("(false)", "(0)");

        return line;
    }

    /// <summary>
    /// Gets the CUDA type name for a C# type.
    /// </summary>
    public static string GetCudaTypeName(string csharpType)
    {
        return csharpType switch
        {
            "int" => "int",
            "uint" => "unsigned int",
            "long" => "long long",
            "ulong" => "unsigned long long",
            "float" => "float",
            "double" => "double",
            "byte" => "unsigned char",
            "sbyte" => "signed char",
            "short" => "short",
            "ushort" => "unsigned short",
            "bool" => "int",
            _ => csharpType // Custom types pass through
        };
    }
}
