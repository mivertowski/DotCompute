// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#include <metal_stdlib>
using namespace metal;

/// <summary>
/// Simple vector multiplication kernel for integration testing.
/// Computes: result[i] = a[i] * b[i] for each element.
/// </summary>
/// <param name="a">Input vector A (read-only).</param>
/// <param name="b">Input vector B (read-only).</param>
/// <param name="result">Output vector (write-only).</param>
/// <param name="gid">Global thread ID - automatically provided by Metal.</param>
kernel void vector_mul(
    device const float* a [[buffer(0)]],
    device const float* b [[buffer(1)]],
    device float* result [[buffer(2)]],
    uint gid [[thread_position_in_grid]])
{
    result[gid] = a[gid] * b[gid];
}
