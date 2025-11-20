// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Types;
using System.Text;

namespace DotCompute.Backends.Metal.Tests.Compilation;

/// <summary>
/// Factory for creating test kernel definitions with various characteristics.
/// </summary>
public static class TestKernelFactory
{
    /// <summary>
    /// Creates a simple vector addition kernel in MSL.
    /// </summary>
    public static KernelDefinition CreateVectorAddKernel()
    {
        var code = @"
#include <metal_stdlib>
#include <metal_compute>
using namespace metal;

kernel void vector_add(
    device const float* a [[buffer(0)]],
    device const float* b [[buffer(1)]],
    device float* result [[buffer(2)]],
    uint gid [[thread_position_in_grid]])
{
    result[gid] = a[gid] + b[gid];
}";

        return new KernelDefinition("vector_add", code)
        {
            EntryPoint = "vector_add",
            Language = KernelLanguage.Metal
        };
    }

    /// <summary>
    /// Creates a C# kernel that needs translation to MSL.
    /// </summary>
    public static KernelDefinition CreateCSharpKernel()
    {
        var code = @"
public static void ScalarMultiply(ReadOnlySpan<float> input, Span<float> output, float scalar)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = input[idx] * scalar;
    }
}";

        return new KernelDefinition("ScalarMultiply", code)
        {
            EntryPoint = "ScalarMultiply",
            Language = KernelLanguage.CSharp
        };
    }

    /// <summary>
    /// Creates a kernel with invalid MSL syntax.
    /// </summary>
    public static KernelDefinition CreateInvalidKernel()
    {
        var code = @"
kernel void invalid_kernel(
    device float* data [[buffer(0)]]
{
    // Missing closing parenthesis above
    data[0] = 1.0f;
}";

        return new KernelDefinition("invalid_kernel", code)
        {
            EntryPoint = "invalid_kernel",
            Language = KernelLanguage.Metal
        };
    }

    /// <summary>
    /// Creates a kernel with threadgroup memory.
    /// </summary>
    public static KernelDefinition CreateThreadgroupMemoryKernel()
    {
        var code = @"
#include <metal_stdlib>
using namespace metal;

kernel void reduction(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    threadgroup float* shared [[threadgroup(0)]],
    uint tid [[thread_position_in_threadgroup]],
    uint gid [[thread_position_in_grid]],
    uint tg_size [[threads_per_threadgroup]])
{
    shared[tid] = input[gid];
    threadgroup_barrier(mem_flags::mem_threadgroup);

    for (uint s = tg_size / 2; s > 0; s >>= 1)
    {
        if (tid < s)
        {
            shared[tid] += shared[tid + s];
        }
        threadgroup_barrier(mem_flags::mem_threadgroup);
    }

    if (tid == 0)
    {
        output[get_threadgroup_position_in_grid().x] = shared[0];
    }
}";

        return new KernelDefinition("reduction", code)
        {
            EntryPoint = "reduction",
            Language = KernelLanguage.Metal
        };
    }

    /// <summary>
    /// Creates a large kernel (1000+ lines) for testing compilation timeouts.
    /// </summary>
    public static KernelDefinition CreateLargeKernel(int operationCount = 1000)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#include <metal_stdlib>");
        sb.AppendLine("using namespace metal;");
        sb.AppendLine();
        sb.AppendLine("kernel void large_kernel(");
        sb.AppendLine("    device float* data [[buffer(0)]],");
        sb.AppendLine("    uint gid [[thread_position_in_grid]])");
        sb.AppendLine("{");
        sb.AppendLine("    float value = data[gid];");

        for (int i = 0; i < operationCount; i++)
        {
            sb.AppendLine($"    value = value * 1.001f + 0.5f; // Operation {i}");
        }

        sb.AppendLine("    data[gid] = value;");
        sb.AppendLine("}");

        return new KernelDefinition("large_kernel", sb.ToString())
        {
            EntryPoint = "large_kernel",
            Language = KernelLanguage.Metal
        };
    }

    /// <summary>
    /// Creates an empty kernel for edge case testing.
    /// </summary>
    public static KernelDefinition CreateEmptyKernel()
    {
        return new KernelDefinition("empty_kernel", string.Empty)
        {
            EntryPoint = "empty_kernel",
            Language = KernelLanguage.Metal
        };
    }

    /// <summary>
    /// Creates a kernel with optimization hints.
    /// </summary>
    public static KernelDefinition CreateOptimizedKernel()
    {
        var code = @"
#include <metal_stdlib>
using namespace metal;

// Optimization hints
#pragma clang loop vectorize(enable)
#pragma clang loop unroll(enable)

kernel void optimized_saxpy(
    device const float* x [[buffer(0)]],
    device const float* y [[buffer(1)]],
    device float* result [[buffer(2)]],
    constant float& alpha [[buffer(3)]],
    uint gid [[thread_position_in_grid]]) [[threads_per_threadgroup(256)]]
{
    result[gid] = alpha * x[gid] + y[gid];
}";

        return new KernelDefinition("optimized_saxpy", code)
        {
            EntryPoint = "optimized_saxpy",
            Language = KernelLanguage.Metal,
            Metadata = new Dictionary<string, object>
            {
                ["OptimizationLevel"] = "Maximum",
                ["ThreadgroupSize"] = 256
            }
        };
    }

    /// <summary>
    /// Creates a kernel with multiple entry points (unsupported).
    /// </summary>
    public static KernelDefinition CreateMultiEntryKernel()
    {
        var code = @"
#include <metal_stdlib>
using namespace metal;

kernel void kernel_a(device float* data [[buffer(0)]], uint gid [[thread_position_in_grid]])
{
    data[gid] *= 2.0f;
}

kernel void kernel_b(device float* data [[buffer(0)]], uint gid [[thread_position_in_grid]])
{
    data[gid] += 1.0f;
}";

        return new KernelDefinition("multi_entry", code)
        {
            EntryPoint = "kernel_a",  // Only one entry point can be specified
            Language = KernelLanguage.Metal
        };
    }

    /// <summary>
    /// Creates a kernel with specific Metal language version requirement.
    /// </summary>
    public static KernelDefinition CreateMetalVersionKernel(string version = "3.0")
    {
        var code = @"
#include <metal_stdlib>
using namespace metal;

// Requires Metal 3.0 features
kernel void modern_kernel(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    uint gid [[thread_position_in_grid]])
{
    output[gid] = input[gid] * 2.0f;
}";

        return new KernelDefinition("modern_kernel", code)
        {
            EntryPoint = "modern_kernel",
            Language = KernelLanguage.Metal,
            Metadata = new Dictionary<string, object>
            {
                ["RequiredMetalVersion"] = version
            }
        };
    }

    /// <summary>
    /// Creates compilation options with specific settings.
    /// Defaults match CompilationOptions constructor defaults for cache key consistency.
    /// </summary>
    public static CompilationOptions CreateCompilationOptions(
        OptimizationLevel level = OptimizationLevel.Default,
        bool debugInfo = false,
        bool fastMath = true)  // Changed from false to true to match CompilationOptions default
    {
        return new CompilationOptions
        {
            OptimizationLevel = level,
            EnableDebugInfo = debugInfo,
            FastMath = fastMath
            // AdditionalFlags is get-only and already initialized to empty list
        };
    }

    /// <summary>
    /// Creates kernel arguments for testing execution.
    /// </summary>
    public static KernelArguments CreateTestArguments(params object[] args)
    {
        var arguments = new KernelArguments();
        foreach (var arg in args)
        {
            arguments.Add(arg);
        }
        return arguments;
    }
}
