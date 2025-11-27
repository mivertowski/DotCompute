// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Abstractions.RingKernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.RingKernels;

/// <summary>
/// PageRank-specific transpilation logic for MetalRingKernelCompiler.
/// </summary>
/// <remarks>
/// This partial class extends MetalRingKernelCompiler with methods to transpile
/// PageRank Ring Kernels from C# to Metal Shading Language (MSL).
/// </remarks>
public sealed partial class MetalRingKernelCompiler
{
    /// <summary>
    /// Compiles PageRank-specific ring kernel to MSL.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="sourceCode">C# source code (not used - hardcoded kernels for Phase 6.4).</param>
    /// <param name="config">Ring kernel configuration.</param>
    /// <returns>Generated MSL source code for PageRank kernel.</returns>
    public string CompilePageRankKernelToMSL(
        string kernelId,
        string sourceCode,
        RingKernelConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ArgumentNullException.ThrowIfNull(config);

        _logger.LogInformation(
            "Compiling PageRank kernel '{KernelId}' to MSL",
            kernelId);

        var mslSource = new StringBuilder(8192);

        // Generate headers
        GenerateHeaders(mslSource);

        // Generate PageRank message type structs
        GeneratePageRankMessageStructs(mslSource);

        // Generate message queue structure (templatized)
        GenerateMessageQueueStructure(mslSource, config);

        // Generate control structure
        GenerateControlStructure(mslSource);

        // Generate kernel-specific code based on kernel ID
        if (kernelId == "metal_pagerank_contribution_sender")
        {
            GenerateContributionSenderKernel(mslSource, config);
        }
        else if (kernelId == "metal_pagerank_rank_aggregator")
        {
            GenerateRankAggregatorKernel(mslSource, config);
        }
        else if (kernelId == "metal_pagerank_convergence_checker")
        {
            GenerateConvergenceCheckerKernel(mslSource, config);
        }
        else
        {
            throw new ArgumentException($"Unknown PageRank kernel ID: {kernelId}", nameof(kernelId));
        }

        var result = mslSource.ToString();
        _logger.LogDebug("Generated {Length} bytes of MSL code for PageRank kernel '{KernelId}'", result.Length, kernelId);

        return result;
    }

    /// <summary>
    /// Generates MSL struct definitions for PageRank message types.
    /// </summary>
    private static void GeneratePageRankMessageStructs(StringBuilder sb)
    {
        sb.AppendLine("// PageRank message type definitions (from PageRankMetalMessages.cs)");
        sb.AppendLine();

        // MetalGraphNode struct
        sb.AppendLine("struct MetalGraphNode {");
        sb.AppendLine("    int NodeId;");
        sb.AppendLine("    float CurrentRank;");
        sb.AppendLine("    int EdgeCount;");
        sb.AppendLine("    int _padding;");
        sb.AppendLine("    int TargetNodeIds[32];  // Inline array (EdgeArray32)");
        sb.AppendLine("};");
        sb.AppendLine();

        // PageRankContribution struct
        sb.AppendLine("struct PageRankContribution {");
        sb.AppendLine("    int SourceNodeId;");
        sb.AppendLine("    int TargetNodeId;");
        sb.AppendLine("    float Contribution;");
        sb.AppendLine("    int Iteration;");
        sb.AppendLine("};");
        sb.AppendLine();

        // RankAggregationResult struct
        sb.AppendLine("struct RankAggregationResult {");
        sb.AppendLine("    int NodeId;");
        sb.AppendLine("    float NewRank;");
        sb.AppendLine("    float Delta;");
        sb.AppendLine("    int Iteration;");
        sb.AppendLine("};");
        sb.AppendLine();

        // ConvergenceCheckResult struct
        sb.AppendLine("struct ConvergenceCheckResult {");
        sb.AppendLine("    int Iteration;");
        sb.AppendLine("    float MaxDelta;");
        sb.AppendLine("    int HasConverged;");
        sb.AppendLine("    int NodesProcessed;");
        sb.AppendLine("};");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates ContributionSender kernel MSL code.
    /// </summary>
    private static void GenerateContributionSenderKernel(StringBuilder sb, RingKernelConfig config)
    {
        sb.AppendLine("// ContributionSender Ring Kernel");
        sb.AppendLine("// Receives MetalGraphNode, sends PageRankContribution to RankAggregator");
        sb.AppendLine("kernel void metal_pagerank_contribution_sender_kernel(");
        sb.AppendLine("    device MetalGraphNode* input_buffer           [[buffer(0)]],");
        sb.AppendLine("    device atomic_int* input_head                 [[buffer(1)]],");
        sb.AppendLine("    device atomic_int* input_tail                 [[buffer(2)]],");
        sb.AppendLine("    device PageRankContribution* output_buffer    [[buffer(3)]],");
        sb.AppendLine("    device atomic_int* output_head                [[buffer(4)]],");
        sb.AppendLine("    device atomic_int* output_tail                [[buffer(5)]],");
        sb.AppendLine("    device KernelControl* control                 [[buffer(6)]],");
        sb.AppendLine("    constant int& queue_capacity                  [[buffer(7)]],");
        sb.AppendLine("    uint thread_id [[thread_position_in_grid]],");
        sb.AppendLine("    uint threadgroup_id [[threadgroup_position_in_grid]],");
        sb.AppendLine("    uint thread_in_threadgroup [[thread_position_in_threadgroup]])");
        sb.AppendLine("{");
        sb.AppendLine("    // Persistent kernel loop");
        sb.AppendLine("    while (true) {");
        sb.AppendLine("        // Check for termination");
        sb.AppendLine("        if (atomic_load_explicit(&control->terminate, memory_order_relaxed) == 1) {");
        sb.AppendLine("            break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Wait for activation");
        sb.AppendLine("        while (atomic_load_explicit(&control->active, memory_order_relaxed) == 0) {");
        sb.AppendLine("            if (atomic_load_explicit(&control->terminate, memory_order_relaxed) == 1) {");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            threadgroup_barrier(mem_flags::mem_none);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Try to dequeue from input (manual implementation)");
        sb.AppendLine("        int current_head = atomic_load_explicit(input_head, memory_order_relaxed);");
        sb.AppendLine("        int current_tail = atomic_load_explicit(input_tail, memory_order_relaxed);");
        sb.AppendLine();
        sb.AppendLine("        if (current_head != current_tail) {");
        sb.AppendLine("            int next_head = (current_head + 1) & (queue_capacity - 1);");
        sb.AppendLine("            if (atomic_compare_exchange_weak_explicit(input_head, &current_head, next_head,");
        sb.AppendLine("                                                         memory_order_relaxed,");
        sb.AppendLine("                                                         memory_order_relaxed)) {");
        sb.AppendLine("                MetalGraphNode nodeInfo = input_buffer[current_head];");
        sb.AppendLine();
        sb.AppendLine("                // Transpiled from ContributionSenderKernel C# code");
        sb.AppendLine("                threadgroup_barrier(mem_flags::mem_threadgroup);");
        sb.AppendLine();
        sb.AppendLine("                // Calculate contribution per outbound edge");
        sb.AppendLine("                int edgeCount = nodeInfo.EdgeCount;");
        sb.AppendLine("                if (edgeCount > 0 && edgeCount <= 32) {");
        sb.AppendLine("                    float contribution = nodeInfo.CurrentRank / edgeCount;");
        sb.AppendLine();
        sb.AppendLine("                    // Send contribution to each neighbor");
        sb.AppendLine("                    for (int i = 0; i < edgeCount; i++) {");
        sb.AppendLine("                        int targetId = nodeInfo.TargetNodeIds[i];");
        sb.AppendLine();
        sb.AppendLine("                        // Enqueue to output (manual implementation)");
        sb.AppendLine("                        int out_tail = atomic_load_explicit(output_tail, memory_order_relaxed);");
        sb.AppendLine("                        int out_next_tail = (out_tail + 1) & (queue_capacity - 1);");
        sb.AppendLine("                        int out_head = atomic_load_explicit(output_head, memory_order_relaxed);");
        sb.AppendLine();
        sb.AppendLine("                        if (out_next_tail != out_head) {");
        sb.AppendLine("                            if (atomic_compare_exchange_weak_explicit(output_tail, &out_tail, out_next_tail,");
        sb.AppendLine("                                                                         memory_order_relaxed,");
        sb.AppendLine("                                                                         memory_order_relaxed)) {");
        sb.AppendLine("                                PageRankContribution msg;");
        sb.AppendLine("                                msg.SourceNodeId = nodeInfo.NodeId;");
        sb.AppendLine("                                msg.TargetNodeId = targetId;");
        sb.AppendLine("                                msg.Contribution = contribution;");
        sb.AppendLine("                                msg.Iteration = 0;");
        sb.AppendLine("                                output_buffer[out_tail] = msg;");
        sb.AppendLine("                            }");
        sb.AppendLine("                        }");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                threadgroup_barrier(mem_flags::mem_device);");
        sb.AppendLine("                atomic_fetch_add_explicit(&control->msg_count, 1U, memory_order_relaxed);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates RankAggregator kernel MSL code.
    /// </summary>
    private static void GenerateRankAggregatorKernel(StringBuilder sb, RingKernelConfig config)
    {
        sb.AppendLine("// RankAggregator Ring Kernel");
        sb.AppendLine("// Receives PageRankContribution, sends RankAggregationResult to ConvergenceChecker");
        sb.AppendLine("kernel void metal_pagerank_rank_aggregator_kernel(");
        sb.AppendLine("    device PageRankContribution* input_buffer         [[buffer(0)]],");
        sb.AppendLine("    device atomic_int* input_head                     [[buffer(1)]],");
        sb.AppendLine("    device atomic_int* input_tail                     [[buffer(2)]],");
        sb.AppendLine("    device RankAggregationResult* output_buffer       [[buffer(3)]],");
        sb.AppendLine("    device atomic_int* output_head                    [[buffer(4)]],");
        sb.AppendLine("    device atomic_int* output_tail                    [[buffer(5)]],");
        sb.AppendLine("    device KernelControl* control                     [[buffer(6)]],");
        sb.AppendLine("    constant int& queue_capacity                      [[buffer(7)]],");
        sb.AppendLine("    uint thread_id [[thread_position_in_grid]],");
        sb.AppendLine("    uint threadgroup_id [[threadgroup_position_in_grid]],");
        sb.AppendLine("    uint thread_in_threadgroup [[thread_position_in_threadgroup]])");
        sb.AppendLine("{");
        sb.AppendLine("    const float DampingFactor = 0.85f;");
        sb.AppendLine();
        sb.AppendLine("    // Persistent kernel loop");
        sb.AppendLine("    while (true) {");
        sb.AppendLine("        // Check for termination");
        sb.AppendLine("        if (atomic_load_explicit(&control->terminate, memory_order_relaxed) == 1) {");
        sb.AppendLine("            break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Wait for activation");
        sb.AppendLine("        while (atomic_load_explicit(&control->active, memory_order_relaxed) == 0) {");
        sb.AppendLine("            if (atomic_load_explicit(&control->terminate, memory_order_relaxed) == 1) {");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            threadgroup_barrier(mem_flags::mem_none);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Try to dequeue from input");
        sb.AppendLine("        int current_head = atomic_load_explicit(input_head, memory_order_relaxed);");
        sb.AppendLine("        int current_tail = atomic_load_explicit(input_tail, memory_order_relaxed);");
        sb.AppendLine();
        sb.AppendLine("        if (current_head != current_tail) {");
        sb.AppendLine("            int next_head = (current_head + 1) & (queue_capacity - 1);");
        sb.AppendLine("            if (atomic_compare_exchange_weak_explicit(input_head, &current_head, next_head,");
        sb.AppendLine("                                                         memory_order_relaxed,");
        sb.AppendLine("                                                         memory_order_relaxed)) {");
        sb.AppendLine("                PageRankContribution contribution = input_buffer[current_head];");
        sb.AppendLine();
        sb.AppendLine("                threadgroup_barrier(mem_flags::mem_threadgroup);");
        sb.AppendLine();
        sb.AppendLine("                float dampedContribution = DampingFactor * contribution.Contribution;");
        sb.AppendLine();
        sb.AppendLine("                // Enqueue to output");
        sb.AppendLine("                int out_tail = atomic_load_explicit(output_tail, memory_order_relaxed);");
        sb.AppendLine("                int out_next_tail = (out_tail + 1) & (queue_capacity - 1);");
        sb.AppendLine("                int out_head = atomic_load_explicit(output_head, memory_order_relaxed);");
        sb.AppendLine();
        sb.AppendLine("                if (out_next_tail != out_head) {");
        sb.AppendLine("                    if (atomic_compare_exchange_weak_explicit(output_tail, &out_tail, out_next_tail,");
        sb.AppendLine("                                                                 memory_order_relaxed,");
        sb.AppendLine("                                                                 memory_order_relaxed)) {");
        sb.AppendLine("                        RankAggregationResult result;");
        sb.AppendLine("                        result.NodeId = contribution.TargetNodeId;");
        sb.AppendLine("                        result.NewRank = dampedContribution;");
        sb.AppendLine("                        result.Delta = 0;");
        sb.AppendLine("                        result.Iteration = contribution.Iteration;");
        sb.AppendLine("                        output_buffer[out_tail] = result;");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                threadgroup_barrier(mem_flags::mem_device);");
        sb.AppendLine("                atomic_fetch_add_explicit(&control->msg_count, 1U, memory_order_relaxed);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates ConvergenceChecker kernel MSL code.
    /// </summary>
    private static void GenerateConvergenceCheckerKernel(StringBuilder sb, RingKernelConfig config)
    {
        sb.AppendLine("// ConvergenceChecker Ring Kernel");
        sb.AppendLine("// Receives RankAggregationResult, outputs ConvergenceCheckResult");
        sb.AppendLine("kernel void metal_pagerank_convergence_checker_kernel(");
        sb.AppendLine("    device RankAggregationResult* input_buffer        [[buffer(0)]],");
        sb.AppendLine("    device atomic_int* input_head                     [[buffer(1)]],");
        sb.AppendLine("    device atomic_int* input_tail                     [[buffer(2)]],");
        sb.AppendLine("    device ConvergenceCheckResult* output_buffer      [[buffer(3)]],");
        sb.AppendLine("    device atomic_int* output_head                    [[buffer(4)]],");
        sb.AppendLine("    device atomic_int* output_tail                    [[buffer(5)]],");
        sb.AppendLine("    device KernelControl* control                     [[buffer(6)]],");
        sb.AppendLine("    constant int& queue_capacity                      [[buffer(7)]],");
        sb.AppendLine("    uint thread_id [[thread_position_in_grid]],");
        sb.AppendLine("    uint threadgroup_id [[threadgroup_position_in_grid]],");
        sb.AppendLine("    uint thread_in_threadgroup [[thread_position_in_threadgroup]])");
        sb.AppendLine("{");
        sb.AppendLine("    const float ConvergenceThreshold = 0.0001f;");
        sb.AppendLine();
        sb.AppendLine("    // Persistent kernel loop");
        sb.AppendLine("    while (true) {");
        sb.AppendLine("        // Check for termination");
        sb.AppendLine("        if (atomic_load_explicit(&control->terminate, memory_order_relaxed) == 1) {");
        sb.AppendLine("            break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Wait for activation");
        sb.AppendLine("        while (atomic_load_explicit(&control->active, memory_order_relaxed) == 0) {");
        sb.AppendLine("            if (atomic_load_explicit(&control->terminate, memory_order_relaxed) == 1) {");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            threadgroup_barrier(mem_flags::mem_none);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Try to dequeue from input");
        sb.AppendLine("        int current_head = atomic_load_explicit(input_head, memory_order_relaxed);");
        sb.AppendLine("        int current_tail = atomic_load_explicit(input_tail, memory_order_relaxed);");
        sb.AppendLine();
        sb.AppendLine("        if (current_head != current_tail) {");
        sb.AppendLine("            int next_head = (current_head + 1) & (queue_capacity - 1);");
        sb.AppendLine("            if (atomic_compare_exchange_weak_explicit(input_head, &current_head, next_head,");
        sb.AppendLine("                                                         memory_order_relaxed,");
        sb.AppendLine("                                                         memory_order_relaxed)) {");
        sb.AppendLine("                RankAggregationResult result = input_buffer[current_head];");
        sb.AppendLine();
        sb.AppendLine("                // Transpiled from ConvergenceCheckerKernel C# code");
        sb.AppendLine("                // Compute absolute delta for convergence check");
        sb.AppendLine("                float absDelta = fabs(result.Delta);");
        sb.AppendLine();
        sb.AppendLine("                // Check if this result indicates non-convergence");
        sb.AppendLine("                bool hasConverged = absDelta < ConvergenceThreshold;");
        sb.AppendLine();
        sb.AppendLine("                // ctx.ThreadFence() -> memory fence");
        sb.AppendLine("                threadgroup_barrier(mem_flags::mem_device);");
        sb.AppendLine();
        sb.AppendLine("                // Enqueue to output");
        sb.AppendLine("                int out_tail = atomic_load_explicit(output_tail, memory_order_relaxed);");
        sb.AppendLine("                int out_next_tail = (out_tail + 1) & (queue_capacity - 1);");
        sb.AppendLine("                int out_head = atomic_load_explicit(output_head, memory_order_relaxed);");
        sb.AppendLine();
        sb.AppendLine("                if (out_next_tail != out_head) {");
        sb.AppendLine("                    if (atomic_compare_exchange_weak_explicit(output_tail, &out_tail, out_next_tail,");
        sb.AppendLine("                                                                 memory_order_relaxed,");
        sb.AppendLine("                                                                 memory_order_relaxed)) {");
        sb.AppendLine("                        ConvergenceCheckResult checkResult;");
        sb.AppendLine("                        checkResult.Iteration = result.Iteration;");
        sb.AppendLine("                        checkResult.MaxDelta = absDelta;");
        sb.AppendLine("                        checkResult.HasConverged = hasConverged ? 1 : 0;");
        sb.AppendLine("                        checkResult.NodesProcessed = 1;");
        sb.AppendLine("                        output_buffer[out_tail] = checkResult;");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                threadgroup_barrier(mem_flags::mem_device);");
        sb.AppendLine("                atomic_fetch_add_explicit(&control->msg_count, 1U, memory_order_relaxed);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
    }
}
