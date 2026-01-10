// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text.RegularExpressions;

namespace DotCompute.Backends.Metal.Translation;

/// <summary>
/// Translates C# atomic operations to Metal Shading Language atomic functions.
/// Handles both Interlocked.* and AtomicOps.* patterns.
/// </summary>
/// <remarks>
/// <para>
/// Metal provides atomic operations through the <c>&lt;metal_atomic&gt;</c> header:
/// </para>
/// <list type="bullet">
/// <item><description><c>atomic_fetch_add_explicit</c> - Add and return original</description></item>
/// <item><description><c>atomic_fetch_sub_explicit</c> - Subtract and return original</description></item>
/// <item><description><c>atomic_fetch_min_explicit</c> - Min and return original</description></item>
/// <item><description><c>atomic_fetch_max_explicit</c> - Max and return original</description></item>
/// <item><description><c>atomic_fetch_and_explicit</c> - AND and return original</description></item>
/// <item><description><c>atomic_fetch_or_explicit</c> - OR and return original</description></item>
/// <item><description><c>atomic_fetch_xor_explicit</c> - XOR and return original</description></item>
/// <item><description><c>atomic_exchange_explicit</c> - Exchange and return original</description></item>
/// <item><description><c>atomic_compare_exchange_weak_explicit</c> - CAS operation</description></item>
/// <item><description><c>atomic_load_explicit</c> / <c>atomic_store_explicit</c></description></item>
/// </list>
/// </remarks>
public sealed class MetalAtomicTranslator
{
    /// <summary>
    /// Pattern to match ref parameter extraction for atomics.
    /// </summary>
    private static readonly Regex RefParameterPattern = new(
        @"ref\s+(\w+)",
        RegexOptions.Compiled);

    /// <summary>
    /// Translates C# atomic operations to Metal atomic functions.
    /// </summary>
    /// <param name="line">The source line containing atomic operations.</param>
    /// <returns>The translated line with Metal atomic functions.</returns>
    public string TranslateAtomicOperations(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var result = line;

        // Translate Interlocked.* operations (existing pattern)
        result = TranslateInterlockedOperations(result);

        // Translate AtomicOps.* operations (new DotCompute pattern)
        result = TranslateAtomicOpsOperations(result);

        // Translate ThreadFence operations
        result = TranslateThreadFence(result);

        return result;
    }

    /// <summary>
    /// Translates standard .NET Interlocked operations to Metal atomics.
    /// </summary>
    private static string TranslateInterlockedOperations(string line)
    {
        var result = line;

        // Interlocked operations → Metal atomic operations
        result = result.Replace("Interlocked.Add(", "atomic_fetch_add_explicit(", StringComparison.Ordinal);
        result = result.Replace("Interlocked.Increment(", "atomic_fetch_add_explicit(", StringComparison.Ordinal);
        result = result.Replace("Interlocked.Decrement(", "atomic_fetch_sub_explicit(", StringComparison.Ordinal);
        result = result.Replace("Interlocked.Exchange(", "atomic_exchange_explicit(", StringComparison.Ordinal);
        result = result.Replace("Interlocked.CompareExchange(", "atomic_compare_exchange_weak_explicit(", StringComparison.Ordinal);
        result = result.Replace("Interlocked.Read(", "atomic_load_explicit(", StringComparison.Ordinal);
        result = result.Replace("Interlocked.And(", "atomic_fetch_and_explicit(", StringComparison.Ordinal);
        result = result.Replace("Interlocked.Or(", "atomic_fetch_or_explicit(", StringComparison.Ordinal);

        // Add memory order for atomics if not specified
        if (result.Contains("atomic_", StringComparison.Ordinal) &&
            !result.Contains("memory_order", StringComparison.Ordinal))
        {
            result = result.Replace(");", ", memory_order_relaxed);", StringComparison.Ordinal);
        }

        return result;
    }

    /// <summary>
    /// Translates DotCompute AtomicOps.* operations to Metal atomics.
    /// </summary>
    private static string TranslateAtomicOpsOperations(string line)
    {
        var result = line;

        // AtomicOps.AtomicAdd → atomic_fetch_add_explicit
        result = result.Replace("AtomicOps.AtomicAdd(", "atomic_fetch_add_explicit(", StringComparison.Ordinal);

        // AtomicOps.AtomicSub → atomic_fetch_sub_explicit
        result = result.Replace("AtomicOps.AtomicSub(", "atomic_fetch_sub_explicit(", StringComparison.Ordinal);

        // AtomicOps.AtomicExchange → atomic_exchange_explicit
        result = result.Replace("AtomicOps.AtomicExchange(", "atomic_exchange_explicit(", StringComparison.Ordinal);

        // AtomicOps.AtomicCompareExchange → atomic_compare_exchange_weak_explicit
        result = result.Replace("AtomicOps.AtomicCompareExchange(", "atomic_compare_exchange_weak_explicit(", StringComparison.Ordinal);

        // AtomicOps.AtomicMin → atomic_fetch_min_explicit
        result = result.Replace("AtomicOps.AtomicMin(", "atomic_fetch_min_explicit(", StringComparison.Ordinal);

        // AtomicOps.AtomicMax → atomic_fetch_max_explicit
        result = result.Replace("AtomicOps.AtomicMax(", "atomic_fetch_max_explicit(", StringComparison.Ordinal);

        // AtomicOps.AtomicAnd → atomic_fetch_and_explicit
        result = result.Replace("AtomicOps.AtomicAnd(", "atomic_fetch_and_explicit(", StringComparison.Ordinal);

        // AtomicOps.AtomicOr → atomic_fetch_or_explicit
        result = result.Replace("AtomicOps.AtomicOr(", "atomic_fetch_or_explicit(", StringComparison.Ordinal);

        // AtomicOps.AtomicXor → atomic_fetch_xor_explicit
        result = result.Replace("AtomicOps.AtomicXor(", "atomic_fetch_xor_explicit(", StringComparison.Ordinal);

        // AtomicOps.AtomicLoad → atomic_load_explicit
        result = result.Replace("AtomicOps.AtomicLoad(", "atomic_load_explicit(", StringComparison.Ordinal);

        // AtomicOps.AtomicStore → atomic_store_explicit
        result = result.Replace("AtomicOps.AtomicStore(", "atomic_store_explicit(", StringComparison.Ordinal);

        // Remove 'ref' keyword for Metal (Metal uses pointers)
        result = RefParameterPattern.Replace(result, "&$1");

        // Add memory order if not present
        if (result.Contains("atomic_", StringComparison.Ordinal) &&
            !result.Contains("memory_order", StringComparison.Ordinal))
        {
            result = result.Replace(");", ", memory_order_relaxed);", StringComparison.Ordinal);
        }

        return result;
    }

    /// <summary>
    /// Translates AtomicOps.ThreadFence to Metal memory barriers.
    /// </summary>
    private static string TranslateThreadFence(string line)
    {
        var result = line;

        // ThreadFence with MemoryScope
        result = result.Replace(
            "AtomicOps.ThreadFence(MemoryScope.Block)",
            "threadgroup_barrier(mem_flags::mem_threadgroup)",
            StringComparison.Ordinal);

        result = result.Replace(
            "AtomicOps.ThreadFence(MemoryScope.Device)",
            "threadgroup_barrier(mem_flags::mem_device)",
            StringComparison.Ordinal);

        result = result.Replace(
            "AtomicOps.ThreadFence(MemoryScope.System)",
            "threadgroup_barrier(mem_flags::mem_device)",  // Metal doesn't have system scope
            StringComparison.Ordinal);

        // Default ThreadFence() without scope
        result = result.Replace(
            "AtomicOps.ThreadFence()",
            "threadgroup_barrier(mem_flags::mem_device)",
            StringComparison.Ordinal);

        // MemoryBarrier
        result = result.Replace(
            "AtomicOps.MemoryBarrier()",
            "threadgroup_barrier(mem_flags::mem_device)",
            StringComparison.Ordinal);

        return result;
    }

    /// <summary>
    /// Translates MemoryOrder enum values to Metal memory_order constants.
    /// </summary>
    /// <param name="csharpOrder">The C# MemoryOrder value.</param>
    /// <returns>The corresponding Metal memory_order constant.</returns>
    public static string TranslateMemoryOrder(string csharpOrder)
    {
        return csharpOrder switch
        {
            "MemoryOrder.Relaxed" => "memory_order_relaxed",
            "MemoryOrder.Acquire" => "memory_order_acquire",
            "MemoryOrder.Release" => "memory_order_release",
            "MemoryOrder.AcquireRelease" => "memory_order_acq_rel",
            "MemoryOrder.SequentiallyConsistent" => "memory_order_seq_cst",
            _ => "memory_order_relaxed"
        };
    }

    /// <summary>
    /// Generates Metal atomic type declarations for kernel parameters.
    /// </summary>
    /// <param name="typeName">The C# type name (int, uint, float, etc.).</param>
    /// <returns>The Metal atomic type declaration.</returns>
    public static string GetAtomicType(string typeName)
    {
        return typeName.ToUpperInvariant() switch
        {
            "INT" or "INT32" => "atomic_int",
            "UINT" or "UINT32" => "atomic_uint",
            "LONG" or "INT64" => "atomic_long",
            "ULONG" or "UINT64" => "atomic_ulong",
            "FLOAT" => "atomic_float",  // Requires Metal 3.0+
            _ => $"atomic<{typeName}>"
        };
    }

    /// <summary>
    /// Checks if the given line contains atomic operations that need translation.
    /// </summary>
    /// <param name="line">The source line to check.</param>
    /// <returns>True if the line contains atomic operations.</returns>
    public static bool ContainsAtomicOperations(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        return line.Contains("AtomicOps.", StringComparison.Ordinal) ||
               line.Contains("Interlocked.", StringComparison.Ordinal);
    }

    /// <summary>
    /// Generates MSL include directives for atomic operations.
    /// </summary>
    /// <returns>MSL include statements for atomic support.</returns>
    public static string GenerateAtomicIncludes()
    {
        return """
            #include <metal_atomic>
            using namespace metal;
            """;
    }
}
