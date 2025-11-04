// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Text;

namespace DotCompute.Backends.Metal.Translation;

/// <summary>
/// Generates Metal Shading Language (MSL) kernel templates for common compute operations.
/// Provides production-ready, optimized MSL code for vector operations, reductions, matrix operations, and more.
/// </summary>
/// <remarks>
/// This generator creates complete, standalone MSL kernel code that is ready for compilation.
/// All templates include:
/// - Metal standard library headers
/// - Proper buffer attributes and thread indexing
/// - Bounds checking for safety
/// - GPU-optimized memory access patterns
/// - Documentation comments
/// </remarks>
public static class MslTemplateGenerator
{
    /// <summary>
    /// Generates a vector addition kernel template.
    /// Operation: result[i] = a[i] + b[i]
    /// </summary>
    /// <param name="elementType">Element type (float, int, double, etc.)</param>
    /// <param name="kernelName">Name for the kernel function</param>
    /// <returns>Complete MSL source code</returns>
    public static string GenerateVectorAdd(string elementType = "float", string kernelName = "vector_add")
    {
        var sb = new StringBuilder();
        AppendHeader(sb, kernelName, "Vector addition: result[i] = a[i] + b[i]");

        sb.AppendLine(CultureInfo.InvariantCulture, $"kernel void {kernelName}(");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    device const {elementType}* a [[buffer(0)]],");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    device const {elementType}* b [[buffer(1)]],");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    device {elementType}* result [[buffer(2)]],");
        sb.AppendLine("    uint gid [[thread_position_in_grid]],");
        sb.AppendLine("    uint grid_size [[threads_per_grid]])");
        sb.AppendLine("{");
        sb.AppendLine("    // Bounds check for safety");
        sb.AppendLine("    if (gid < grid_size) {");
        sb.AppendLine("        result[gid] = a[gid] + b[gid];");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a vector multiplication kernel template.
    /// Operation: result[i] = a[i] * scalar
    /// </summary>
    /// <param name="elementType">Element type (float, int, double, etc.)</param>
    /// <param name="kernelName">Name for the kernel function</param>
    /// <returns>Complete MSL source code</returns>
    public static string GenerateVectorMultiply(string elementType = "float", string kernelName = "vector_multiply")
    {
        var sb = new StringBuilder();
        AppendHeader(sb, kernelName, "Scalar multiplication: result[i] = a[i] * scalar");

        sb.AppendLine(CultureInfo.InvariantCulture, $"kernel void {kernelName}(");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    device const {elementType}* a [[buffer(0)]],");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    device {elementType}* result [[buffer(1)]],");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    constant {elementType}& scalar [[buffer(2)]],");
        sb.AppendLine("    uint gid [[thread_position_in_grid]],");
        sb.AppendLine("    uint grid_size [[threads_per_grid]])");
        sb.AppendLine("{");
        sb.AppendLine("    if (gid < grid_size) {");
        sb.AppendLine("        result[gid] = a[gid] * scalar;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a parallel reduction sum kernel using threadgroup memory.
    /// Implements efficient two-phase reduction: threadgroup-level then atomic global.
    /// </summary>
    /// <param name="elementType">Element type (float, int, etc.)</param>
    /// <param name="kernelName">Name for the kernel function</param>
    /// <param name="threadgroupSize">Threadgroup size for shared memory (default 256)</param>
    /// <returns>Complete MSL source code</returns>
    public static string GenerateReductionSum(string elementType = "float", string kernelName = "reduction_sum", int threadgroupSize = 256)
    {
        var sb = new StringBuilder();
        AppendHeader(sb, kernelName, $"Parallel reduction sum using threadgroup memory (size: {threadgroupSize})");

        sb.AppendLine(CultureInfo.InvariantCulture, $"kernel void {kernelName}(");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    device const {elementType}* input [[buffer(0)]],");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    device {elementType}* output [[buffer(1)]],");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    constant uint& element_count [[buffer(2)]],");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    threadgroup {elementType}* shared_data [[threadgroup(0)]],");
        sb.AppendLine("    uint tid [[thread_position_in_threadgroup]],");
        sb.AppendLine("    uint gid [[thread_position_in_grid]],");
        sb.AppendLine("    uint tg_size [[threads_per_threadgroup]])");
        sb.AppendLine("{");
        sb.AppendLine("    // Phase 1: Load data into threadgroup memory with bounds checking");
        sb.AppendLine("    shared_data[tid] = (gid < element_count) ? input[gid] : 0.0;");
        sb.AppendLine("    threadgroup_barrier(mem_flags::mem_threadgroup);");
        sb.AppendLine();
        sb.AppendLine("    // Phase 2: Parallel reduction within threadgroup");
        sb.AppendLine("    for (uint s = tg_size / 2; s > 0; s >>= 1) {");
        sb.AppendLine("        if (tid < s) {");
        sb.AppendLine("            shared_data[tid] += shared_data[tid + s];");
        sb.AppendLine("        }");
        sb.AppendLine("        threadgroup_barrier(mem_flags::mem_threadgroup);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // Phase 3: Thread 0 atomically adds to global result");
        sb.AppendLine("    if (tid == 0) {");

        if (elementType == "float")
        {
            sb.AppendLine("        atomic_fetch_add_explicit((device atomic_float*)output, shared_data[0], memory_order_relaxed);");
        }
        else if (elementType == "int" || elementType == "uint")
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"        atomic_fetch_add_explicit((device atomic_{elementType}*)output, shared_data[0], memory_order_relaxed);");
        }
        else
        {
            sb.AppendLine("        // Non-atomic fallback for unsupported types");
            sb.AppendLine("        output[0] += shared_data[0];");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a matrix multiplication kernel (naive implementation).
    /// Operation: C[row, col] = sum(A[row, k] * B[k, col]) for k in [0, K)
    /// </summary>
    /// <param name="elementType">Element type (float, double, etc.)</param>
    /// <param name="kernelName">Name for the kernel function</param>
    /// <returns>Complete MSL source code</returns>
    public static string GenerateMatrixMultiply(string elementType = "float", string kernelName = "matrix_multiply")
    {
        var sb = new StringBuilder();
        AppendHeader(sb, kernelName, "Matrix multiplication: C = A × B (row-major layout)");

        sb.AppendLine(CultureInfo.InvariantCulture, $"kernel void {kernelName}(");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    device const {elementType}* A [[buffer(0)]],");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    device const {elementType}* B [[buffer(1)]],");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    device {elementType}* C [[buffer(2)]],");
        sb.AppendLine("    constant uint& M [[buffer(3)]],  // Rows of A");
        sb.AppendLine("    constant uint& N [[buffer(4)]],  // Cols of B");
        sb.AppendLine("    constant uint& K [[buffer(5)]],  // Cols of A / Rows of B");
        sb.AppendLine("    uint2 gid [[thread_position_in_grid]])");
        sb.AppendLine("{");
        sb.AppendLine("    uint row = gid.y;");
        sb.AppendLine("    uint col = gid.x;");
        sb.AppendLine();
        sb.AppendLine("    // Bounds check");
        sb.AppendLine("    if (row >= M || col >= N) {");
        sb.AppendLine("        return;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // Compute dot product for C[row, col]");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    {elementType} sum = 0.0;");
        sb.AppendLine("    for (uint k = 0; k < K; ++k) {");
        sb.AppendLine("        sum += A[row * K + k] * B[k * N + col];");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    C[row * N + col] = sum;");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a tiled matrix multiplication kernel with threadgroup memory optimization.
    /// Significantly faster than naive implementation for large matrices.
    /// </summary>
    /// <param name="elementType">Element type (float, double, etc.)</param>
    /// <param name="kernelName">Name for the kernel function</param>
    /// <param name="tileSize">Tile size (typically 16 or 32)</param>
    /// <returns>Complete MSL source code</returns>
    public static string GenerateTiledMatrixMultiply(string elementType = "float", string kernelName = "matrix_multiply_tiled", int tileSize = 16)
    {
        var sb = new StringBuilder();
        AppendHeader(sb, kernelName, $"Tiled matrix multiplication with threadgroup memory (tile size: {tileSize}x{tileSize})");

        sb.AppendLine(CultureInfo.InvariantCulture, $"kernel void {kernelName}(");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    device const {elementType}* A [[buffer(0)]],");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    device const {elementType}* B [[buffer(1)]],");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    device {elementType}* C [[buffer(2)]],");
        sb.AppendLine("    constant uint& M [[buffer(3)]],");
        sb.AppendLine("    constant uint& N [[buffer(4)]],");
        sb.AppendLine("    constant uint& K [[buffer(5)]],");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    threadgroup {elementType}* tile_A [[threadgroup(0)]],");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    threadgroup {elementType}* tile_B [[threadgroup(1)]],");
        sb.AppendLine("    uint2 gid [[thread_position_in_grid]],");
        sb.AppendLine("    uint2 tid [[thread_position_in_threadgroup]])");
        sb.AppendLine("{");
        sb.AppendLine("    uint row = gid.y;");
        sb.AppendLine("    uint col = gid.x;");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    {elementType} sum = 0.0;");
        sb.AppendLine();
        sb.AppendLine("    // Process matrix in tiles");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    uint num_tiles = (K + {tileSize} - 1) / {tileSize};");
        sb.AppendLine("    for (uint t = 0; t < num_tiles; ++t) {");
        sb.AppendLine("        // Load tiles into threadgroup memory");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        uint tile_col = t * {tileSize} + tid.x;");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        uint tile_row = t * {tileSize} + tid.y;");
        sb.AppendLine();
        sb.AppendLine("        // Load A tile");
        sb.AppendLine("        if (row < M && tile_col < K) {");
        sb.AppendLine(CultureInfo.InvariantCulture, $"            tile_A[tid.y * {tileSize} + tid.x] = A[row * K + tile_col];");
        sb.AppendLine("        } else {");
        sb.AppendLine(CultureInfo.InvariantCulture, $"            tile_A[tid.y * {tileSize} + tid.x] = 0.0;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Load B tile");
        sb.AppendLine("        if (tile_row < K && col < N) {");
        sb.AppendLine(CultureInfo.InvariantCulture, $"            tile_B[tid.y * {tileSize} + tid.x] = B[tile_row * N + col];");
        sb.AppendLine("        } else {");
        sb.AppendLine(CultureInfo.InvariantCulture, $"            tile_B[tid.y * {tileSize} + tid.x] = 0.0;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        threadgroup_barrier(mem_flags::mem_threadgroup);");
        sb.AppendLine();
        sb.AppendLine("        // Compute partial dot product");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        for (uint k = 0; k < {tileSize}; ++k) {{");
        sb.AppendLine(CultureInfo.InvariantCulture, $"            sum += tile_A[tid.y * {tileSize} + k] * tile_B[k * {tileSize} + tid.x];");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        threadgroup_barrier(mem_flags::mem_threadgroup);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // Write result");
        sb.AppendLine("    if (row < M && col < N) {");
        sb.AppendLine("        C[row * N + col] = sum;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a vector dot product kernel with parallel reduction.
    /// Operation: result = sum(a[i] * b[i]) for all i
    /// </summary>
    public static string GenerateDotProduct(string elementType = "float", string kernelName = "dot_product")
    {
        var sb = new StringBuilder();
        AppendHeader(sb, kernelName, "Vector dot product with parallel reduction");

        sb.AppendLine(CultureInfo.InvariantCulture, $"kernel void {kernelName}(");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    device const {elementType}* a [[buffer(0)]],");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    device const {elementType}* b [[buffer(1)]],");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    device {elementType}* result [[buffer(2)]],");
        sb.AppendLine("    constant uint& element_count [[buffer(3)]],");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    threadgroup {elementType}* shared_data [[threadgroup(0)]],");
        sb.AppendLine("    uint tid [[thread_position_in_threadgroup]],");
        sb.AppendLine("    uint gid [[thread_position_in_grid]],");
        sb.AppendLine("    uint tg_size [[threads_per_threadgroup]])");
        sb.AppendLine("{");
        sb.AppendLine("    // Load and multiply");
        sb.AppendLine("    shared_data[tid] = (gid < element_count) ? (a[gid] * b[gid]) : 0.0;");
        sb.AppendLine("    threadgroup_barrier(mem_flags::mem_threadgroup);");
        sb.AppendLine();
        sb.AppendLine("    // Parallel reduction");
        sb.AppendLine("    for (uint s = tg_size / 2; s > 0; s >>= 1) {");
        sb.AppendLine("        if (tid < s) {");
        sb.AppendLine("            shared_data[tid] += shared_data[tid + s];");
        sb.AppendLine("        }");
        sb.AppendLine("        threadgroup_barrier(mem_flags::mem_threadgroup);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // Atomic accumulation");
        sb.AppendLine("    if (tid == 0) {");

        if (elementType == "float")
        {
            sb.AppendLine("        atomic_fetch_add_explicit((device atomic_float*)result, shared_data[0], memory_order_relaxed);");
        }
        else
        {
            sb.AppendLine("        result[0] += shared_data[0];");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a SAXPY kernel (Single-precision A*X Plus Y).
    /// Operation: y[i] = alpha * x[i] + y[i]
    /// </summary>
    public static string GenerateSaxpy(string elementType = "float", string kernelName = "saxpy")
    {
        var sb = new StringBuilder();
        AppendHeader(sb, kernelName, "SAXPY: y = alpha * x + y");

        sb.AppendLine(CultureInfo.InvariantCulture, $"kernel void {kernelName}(");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    constant {elementType}& alpha [[buffer(0)]],");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    device const {elementType}* x [[buffer(1)]],");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    device {elementType}* y [[buffer(2)]],");
        sb.AppendLine("    uint gid [[thread_position_in_grid]],");
        sb.AppendLine("    uint grid_size [[threads_per_grid]])");
        sb.AppendLine("{");
        sb.AppendLine("    if (gid < grid_size) {");
        sb.AppendLine("        y[gid] = alpha * x[gid] + y[gid];");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Appends standard Metal header and documentation.
    /// </summary>
    private static void AppendHeader(StringBuilder sb, string kernelName, string description)
    {
        sb.AppendLine("#include <metal_stdlib>");
        sb.AppendLine("#include <metal_compute>");
        sb.AppendLine("using namespace metal;");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"// Kernel: {kernelName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"// Description: {description}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"// Generated by DotCompute.Backends.Metal.Translation.MslTemplateGenerator");
        sb.AppendLine(CultureInfo.InvariantCulture, $"// Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates a custom kernel from a template specification.
    /// </summary>
    /// <param name="operation">Operation type (add, multiply, reduce, matmul, etc.)</param>
    /// <param name="elementType">Element type</param>
    /// <param name="kernelName">Kernel function name</param>
    /// <param name="options">Additional generation options</param>
    /// <returns>Complete MSL source code</returns>
    public static string GenerateKernel(string operation, string elementType = "float", string? kernelName = null, Dictionary<string, object>? options = null)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // Use the normalized operation name directly (analyzer prefers explicit StringComparison)
        kernelName ??= operation.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);

        return operation.ToUpperInvariant() switch
        {
            "VECTOR_ADD" or "ADD" => GenerateVectorAdd(elementType, kernelName),
            "VECTOR_MULTIPLY" or "MULTIPLY" or "SCALE" => GenerateVectorMultiply(elementType, kernelName),
            "REDUCTION_SUM" or "REDUCE_SUM" or "SUM" => GenerateReductionSum(elementType, kernelName),
            "MATRIX_MULTIPLY" or "MATMUL" or "GEMM" => GenerateMatrixMultiply(elementType, kernelName),
            "MATRIX_MULTIPLY_TILED" or "MATMUL_TILED" => GenerateTiledMatrixMultiply(elementType, kernelName),
            "DOT_PRODUCT" or "DOT" => GenerateDotProduct(elementType, kernelName),
            "SAXPY" => GenerateSaxpy(elementType, kernelName),
            _ => throw new NotSupportedException($"Operation '{operation}' is not supported by the template generator")
        };
    }
}
