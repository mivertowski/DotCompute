// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using System.Text.RegularExpressions;
using DotCompute.Backends.Metal.Barriers;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1307 // StringComparison - ordinal matching is appropriate for code markers

namespace DotCompute.Backends.Metal.Kernels;

/// <summary>
/// Partial class extension for barrier and fence code injection during MSL compilation.
/// </summary>
public sealed partial class MetalKernelCompiler
{
    [GeneratedRegex(@"^(\s*)//\s*@BARRIER\s*$", RegexOptions.Multiline)]
    private static partial Regex BarrierMarkerPattern();

    [GeneratedRegex(@"^(\s*)//\s*@FENCE:THREADGROUP\s*$", RegexOptions.Multiline)]
    private static partial Regex ThreadgroupFencePattern();

    [GeneratedRegex(@"^(\s*)//\s*@FENCE:DEVICE\s*$", RegexOptions.Multiline)]
    private static partial Regex DeviceFencePattern();

    [GeneratedRegex(@"^(\s*)//\s*@FENCE:TEXTURE\s*$", RegexOptions.Multiline)]
    private static partial Regex TextureFencePattern();

    [GeneratedRegex(@"^(\s*)//\s*@FENCE:ALL\s*$", RegexOptions.Multiline)]
    private static partial Regex AllFencePattern();
    /// <summary>
    /// Injects barrier and fence primitives into MSL source code.
    /// </summary>
    /// <param name="mslSource">The original MSL source code.</param>
    /// <param name="barrierHandle">Optional barrier handle for injection.</param>
    /// <returns>MSL source code with injected barriers and fences.</returns>
    /// <remarks>
    /// <para>
    /// This method performs compile-time injection of Metal synchronization primitives.
    /// It searches for special markers in the source code and replaces them with the
    /// appropriate MSL barrier calls.
    /// </para>
    /// <para>
    /// <strong>Supported Markers:</strong>
    /// <list type="bullet">
    /// <item><description><c>// @BARRIER</c> - Inject barrier based on provided handle</description></item>
    /// <item><description><c>// @FENCE:THREADGROUP</c> - Inject threadgroup memory fence</description></item>
    /// <item><description><c>// @FENCE:DEVICE</c> - Inject device memory fence</description></item>
    /// <item><description><c>// @FENCE:TEXTURE</c> - Inject texture memory fence</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Example MSL Source:</strong>
    /// <code>
    /// kernel void my_kernel(device int* data [[buffer(0)]]) {
    ///     uint tid = thread_position_in_threadgroup;
    ///
    ///     data[tid] = compute_value(tid);
    ///     // @BARRIER  // Will be replaced with threadgroup_barrier(...)
    ///
    ///     int neighbor = data[tid + 1];
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Limitations:</strong>
    /// <list type="bullet">
    /// <item><description>Markers must be on their own line</description></item>
    /// <item><description>Whitespace before markers is preserved</description></item>
    /// <item><description>No AST-based analysis (markers are required)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Future Work (Phase 3B):</strong>
    /// - Automatic fence insertion based on memory access patterns
    /// - AST-based analysis for optimal barrier placement
    /// - Integration with MetalBarrierProvider fence queue
    /// - Support for FenceLocation semantics (AtEntry, AtExit, etc.)
    /// </para>
    /// </remarks>
    private string InjectBarrierAndFenceCode(string mslSource, MetalBarrierHandle? barrierHandle = null)
    {
        if (string.IsNullOrEmpty(mslSource))
        {
            return mslSource;
        }

        var source = new StringBuilder(mslSource);
        var injectionCount = 0;

        // Pattern 1: @BARRIER marker - inject barrier from handle
        if (barrierHandle != null)
        {
            var barrierCode = barrierHandle.GenerateMslBarrierCode();

            source = new StringBuilder(BarrierMarkerPattern().Replace(source.ToString(), match =>
            {
                injectionCount++;
                var indent = match.Groups[1].Value;
                return $"{indent}{barrierCode} // Injected barrier (ID={barrierHandle.BarrierId})";
            }));

            if (injectionCount > 0)
            {
                _logger.LogDebug(
                    "Injected {Count} barrier(s) for handle ID={BarrierId}, scope={Scope}",
                    injectionCount, barrierHandle.BarrierId, barrierHandle.Scope);
            }
        }

        // Pattern 2: @FENCE:THREADGROUP marker
        source = new StringBuilder(ThreadgroupFencePattern().Replace(source.ToString(), match =>
        {
            injectionCount++;
            var indent = match.Groups[1].Value;
            return $"{indent}threadgroup_barrier(mem_flags::mem_threadgroup); // Injected threadgroup fence";
        }));

        // Pattern 3: @FENCE:DEVICE marker
        source = new StringBuilder(DeviceFencePattern().Replace(source.ToString(), match =>
        {
            injectionCount++;
            var indent = match.Groups[1].Value;
            return $"{indent}threadgroup_barrier(mem_flags::mem_device); // Injected device fence";
        }));

        // Pattern 4: @FENCE:TEXTURE marker
        source = new StringBuilder(TextureFencePattern().Replace(source.ToString(), match =>
        {
            injectionCount++;
            var indent = match.Groups[1].Value;
            return $"{indent}threadgroup_barrier(mem_flags::mem_texture); // Injected texture fence";
        }));

        // Pattern 5: @FENCE:ALL marker (device + threadgroup)
        source = new StringBuilder(AllFencePattern().Replace(source.ToString(), match =>
        {
            injectionCount++;
            var indent = match.Groups[1].Value;
            return $"{indent}threadgroup_barrier(mem_flags::mem_device_and_threadgroup); // Injected full fence";
        }));

        if (injectionCount > 0)
        {
            _logger.LogInformation(
                "Injected {Count} total barrier/fence primitive(s) into MSL source",
                injectionCount);
        }

        return source.ToString();
    }

    /// <summary>
    /// Generates MSL barrier code for a specific fence type and flags.
    /// </summary>
    /// <param name="fenceFlags">The Metal memory fence flags.</param>
    /// <returns>MSL barrier statement.</returns>
    /// <remarks>
    /// This is a utility method for generating barrier code outside of the compilation pipeline.
    /// Used by barrier and memory ordering providers for documentation and testing.
    /// </remarks>
    internal static string GenerateMslBarrierCode(MetalMemoryFenceFlags fenceFlags)
    {
        var flagString = fenceFlags switch
        {
            MetalMemoryFenceFlags.None => "mem_flags::mem_none",
            MetalMemoryFenceFlags.Device => "mem_flags::mem_device",
            MetalMemoryFenceFlags.Threadgroup => "mem_flags::mem_threadgroup",
            MetalMemoryFenceFlags.Texture => "mem_flags::mem_texture",
            MetalMemoryFenceFlags.DeviceAndThreadgroup => "mem_flags::mem_device_and_threadgroup",
            _ => "mem_flags::mem_device_and_threadgroup" // Safe default
        };

        return $"threadgroup_barrier({flagString});";
    }

    /// <summary>
    /// Checks if MSL source code contains barrier/fence markers.
    /// </summary>
    /// <param name="mslSource">The MSL source code to check.</param>
    /// <returns>True if markers are present; otherwise false.</returns>
    internal static bool ContainsBarrierMarkers(string mslSource)
    {
        if (string.IsNullOrEmpty(mslSource))
        {
            return false;
        }

        return mslSource.Contains("@BARRIER") ||
               mslSource.Contains("@FENCE:");
    }
}
