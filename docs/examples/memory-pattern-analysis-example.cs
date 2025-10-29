// Example: Memory Access Pattern Analysis in DotCompute Metal Backend
// This demonstrates the automatic detection and diagnostic generation for memory patterns

using DotCompute.Backends.Metal.Translation;
using Microsoft.Extensions.Logging;

// Example 1: Optimal Coalesced Access (No Warnings)
[Kernel]
public static void VectorAdd(Span<float> a, Span<float> b, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
    {
        result[idx] = a[idx] + b[idx];
        // ✅ Coalesced: Direct thread ID indexing
        // No diagnostic generated
    }
}

// Example 2: Strided Access (Info Diagnostic)
[Kernel]
public static void StridedCopy(Span<float> input, Span<float> output, int stride)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = input[idx * stride];
        // ⚠️ Strided: Multiplication in index
        // Diagnostic: Info level, suggests threadgroup staging
        // Expected: 15-30% bandwidth reduction
    }
}

// Example 3: Scattered Access (Warning)
[Kernel]
public static void GatherData(Span<float> data, Span<int> indices, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
    {
        result[idx] = data[indices[idx]];
        // ❌ Scattered: Indirect indexing through another array
        // Diagnostic: Warning level, critical performance issue
        // Expected: 50-80% bandwidth reduction
    }
}

// Example 4: Mixed Patterns (Multiple Diagnostics)
[Kernel]
public static void ComplexKernel(
    Span<float> input1,
    Span<float> input2,
    Span<int> indices,
    Span<float> output,
    int stride)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        // ✅ Coalesced - optimal
        float a = input1[idx];

        // ⚠️ Strided - info diagnostic
        float b = input2[idx * stride];

        // ❌ Scattered - warning diagnostic
        float c = input1[indices[idx]];

        output[idx] = a + b + c;
    }
}

// Usage Example: Accessing Diagnostics
public class MemoryPatternAnalysisDemo
{
    public void AnalyzeKernel(string kernelSource)
    {
        // Create translator with logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        var logger = loggerFactory.CreateLogger<CSharpToMSLTranslator>();

        var translator = new CSharpToMSLTranslator(logger);

        // Translate kernel (analysis runs automatically)
        var mslCode = translator.Translate(
            kernelSource,
            "MyKernel",
            "my_kernel");

        // Access diagnostics
        var diagnostics = translator.GetDiagnostics();

        Console.WriteLine($"\nMemory Access Analysis:");
        Console.WriteLine($"Total diagnostics: {diagnostics.Count}");
        Console.WriteLine();

        foreach (var diagnostic in diagnostics)
        {
            Console.WriteLine($"[{diagnostic.Severity}] {diagnostic.Component}");
            Console.WriteLine($"  Message: {diagnostic.Message}");

            // Access context
            if (diagnostic.Context.TryGetValue("BufferName", out var bufferName))
                Console.WriteLine($"  Buffer: {bufferName}");

            if (diagnostic.Context.TryGetValue("Pattern", out var pattern))
                Console.WriteLine($"  Pattern: {pattern}");

            if (diagnostic.Context.TryGetValue("LineNumber", out var lineNumber))
                Console.WriteLine($"  Line: {lineNumber}");

            if (diagnostic.Context.TryGetValue("Stride", out var stride))
                Console.WriteLine($"  Stride: {stride}");

            if (diagnostic.Context.TryGetValue("Suggestion", out var suggestion))
                Console.WriteLine($"  💡 Suggestion: {suggestion}");

            Console.WriteLine();
        }

        // Check for warnings
        var warnings = diagnostics.Count(d =>
            d.Severity == MetalDiagnosticMessage.SeverityLevel.Warning);

        if (warnings > 0)
        {
            Console.WriteLine($"⚠️ {warnings} performance warnings detected!");
            Console.WriteLine("Consider optimizing scattered memory access patterns.");
        }
    }
}

// Expected Output for ComplexKernel:
/*

Memory Access Analysis:
Total diagnostics: 2

[Info] MemoryAccessAnalyzer
  Message: Strided memory access detected in 'input2[idx * stride]' with stride 'stride'.
           For large strides, consider threadgroup staging to improve coalescing.
           Expected performance impact: 15-30% bandwidth reduction.
  Buffer: input2
  Pattern: Strided
  Stride: stride
  Line: 59
  💡 Suggestion: For stride > 16, use threadgroup memory staging

[Warning] MemoryAccessAnalyzer
  Message: Scattered memory access detected in 'input1[indices[idx]]'.
           Consider using threadgroup staging for better performance.
           Scattered access can reduce memory bandwidth by 50-80%.
  Buffer: input1
  Pattern: Scattered
  Line: 62
  💡 Suggestion: Use threadgroup memory to coalesce scattered reads

⚠️ 1 performance warnings detected!
Consider optimizing scattered memory access patterns.

*/

// Future: Automatic Optimization (Planned)
// When auto-optimization is implemented, the system will generate:

/*
// Original scattered access:
result[idx] = data[indices[idx]];

// Auto-optimized with threadgroup staging:
threadgroup int sorted_indices[THREADGROUP_SIZE];
threadgroup float cached_data[THREADGROUP_SIZE];

// Sort indices within threadgroup (pseudocode)
sorted_indices[tid_in_group] = indices[gid];
threadgroup_barrier(mem_flags::mem_threadgroup);

// Coalesced batch load
if (tid_in_group < batch_size)
{
    cached_data[tid_in_group] = data[sorted_indices[tid_in_group]];
}
threadgroup_barrier(mem_flags::mem_threadgroup);

// Use cached result
result[idx] = cached_data[local_lookup[tid_in_group]];

// Expected improvement: 50-80% bandwidth recovery
*/
