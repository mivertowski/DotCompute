// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// CA1822: Methods are intentionally static as they represent GPU intrinsics
// that will be translated to backend-specific atomic operations. The stub
// implementations are placeholders that will be replaced by code generation.
#pragma warning disable CA1822

using System.Runtime.CompilerServices;
using System.Threading;

namespace DotCompute.Abstractions.Atomics;

/// <summary>
/// Provides first-class GPU atomic operations that compile to native atomics on each backend.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a unified API for atomic operations across all DotCompute backends
/// (CPU SIMD, CUDA, OpenCL, Metal). Methods in this class are translated to native atomic
/// instructions by the backend-specific compilers and transpilers.
/// </para>
/// <para>
/// <b>Backend Translations:</b>
/// </para>
/// <list type="table">
/// <listheader>
///     <term>Operation</term>
///     <description>CUDA</description>
///     <description>OpenCL</description>
///     <description>CPU (.NET)</description>
/// </listheader>
/// <item>
///     <term>AtomicAdd</term>
///     <description>atomicAdd</description>
///     <description>atomic_add</description>
///     <description>Interlocked.Add</description>
/// </item>
/// <item>
///     <term>AtomicSub</term>
///     <description>atomicSub</description>
///     <description>atomic_sub</description>
///     <description>Interlocked.Add(-value)</description>
/// </item>
/// <item>
///     <term>AtomicExchange</term>
///     <description>atomicExch</description>
///     <description>atomic_xchg</description>
///     <description>Interlocked.Exchange</description>
/// </item>
/// <item>
///     <term>AtomicCompareExchange</term>
///     <description>atomicCAS</description>
///     <description>atomic_cmpxchg</description>
///     <description>Interlocked.CompareExchange</description>
/// </item>
/// <item>
///     <term>AtomicMin</term>
///     <description>atomicMin</description>
///     <description>atomic_min</description>
///     <description>CAS loop</description>
/// </item>
/// <item>
///     <term>AtomicMax</term>
///     <description>atomicMax</description>
///     <description>atomic_max</description>
///     <description>CAS loop</description>
/// </item>
/// <item>
///     <term>AtomicAnd</term>
///     <description>atomicAnd</description>
///     <description>atomic_and</description>
///     <description>Interlocked.And</description>
/// </item>
/// <item>
///     <term>AtomicOr</term>
///     <description>atomicOr</description>
///     <description>atomic_or</description>
///     <description>Interlocked.Or</description>
/// </item>
/// <item>
///     <term>AtomicXor</term>
///     <description>atomicXor</description>
///     <description>atomic_xor</description>
///     <description>CAS loop</description>
/// </item>
/// </list>
/// <para>
/// <b>64-bit Atomics:</b> Most operations support 64-bit types (long, ulong, double).
/// On CUDA, 64-bit atomics require Compute Capability 6.0+ for native support.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Basic atomic operations
/// int sum = 0;
/// Atomics.AtomicAdd(ref sum, 1);
///
/// // Compare and swap for lock-free patterns
/// int expected = 0;
/// int desired = 1;
/// int actual = Atomics.AtomicCompareExchange(ref lockVar, expected, desired);
/// if (actual == expected)
/// {
///     // Successfully acquired lock
/// }
///
/// // Memory ordering with fences
/// Atomics.Store(ref data, value, MemoryOrder.Release);
/// Atomics.ThreadFence(MemoryScope.Device);
/// </code>
/// </example>
public static class AtomicOps
{
    // ============================================================================
    // Atomic Add Operations
    // ============================================================================

    /// <summary>
    /// Atomically adds a value to an integer and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target integer.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>The original value before the addition.</returns>
    /// <remarks>
    /// <para>Translated to <c>atomicAdd(&amp;target, value)</c> in CUDA.</para>
    /// <para>Translated to <c>Interlocked.Add(ref target, value) - value</c> on CPU.</para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AtomicAdd(ref int target, int value)
        => Interlocked.Add(ref target, value) - value;

    /// <summary>
    /// Atomically adds a value to an unsigned integer and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target unsigned integer.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>The original value before the addition.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint AtomicAdd(ref uint target, uint value)
    {
        uint original;
        var current = Volatile.Read(ref target);
        do
        {
            original = current;
            current = Interlocked.CompareExchange(ref target, original + value, original);
        } while (current != original);
        return original;
    }

    /// <summary>
    /// Atomically adds a value to a long integer and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target long integer.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>The original value before the addition.</returns>
    /// <remarks>
    /// <para><b>CUDA Requirements:</b> Native 64-bit atomics require CC 6.0+.</para>
    /// <para>Translated to <c>atomicAdd((unsigned long long*)&amp;target, value)</c> in CUDA.</para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AtomicAdd(ref long target, long value)
        => Interlocked.Add(ref target, value) - value;

    /// <summary>
    /// Atomically adds a value to an unsigned long integer and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target unsigned long integer.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>The original value before the addition.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong AtomicAdd(ref ulong target, ulong value)
    {
        ulong original;
        var current = Volatile.Read(ref target);
        do
        {
            original = current;
            current = Interlocked.CompareExchange(ref target, original + value, original);
        } while (current != original);
        return original;
    }

    /// <summary>
    /// Atomically adds a value to a float and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target float.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>The original value before the addition.</returns>
    /// <remarks>
    /// <para><b>CUDA Requirements:</b> Native float atomicAdd requires CC 2.0+.</para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float AtomicAdd(ref float target, float value)
    {
        float original;
        var current = Volatile.Read(ref target);
        do
        {
            original = current;
            var newValue = original + value;
            var originalBits = BitConverter.SingleToInt32Bits(original);
            var newBits = BitConverter.SingleToInt32Bits(newValue);
            var currentBits = Interlocked.CompareExchange(
                ref Unsafe.As<float, int>(ref target),
                newBits,
                originalBits);
            current = BitConverter.Int32BitsToSingle(currentBits);
        } while (BitConverter.SingleToInt32Bits(current) != BitConverter.SingleToInt32Bits(original));
        return original;
    }

    /// <summary>
    /// Atomically adds a value to a double and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target double.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>The original value before the addition.</returns>
    /// <remarks>
    /// <para><b>CUDA Requirements:</b> Native double atomicAdd requires CC 6.0+.</para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double AtomicAdd(ref double target, double value)
    {
        double original;
        var current = Volatile.Read(ref target);
        do
        {
            original = current;
            var newValue = original + value;
            var originalBits = BitConverter.DoubleToInt64Bits(original);
            var newBits = BitConverter.DoubleToInt64Bits(newValue);
            var currentBits = Interlocked.CompareExchange(
                ref Unsafe.As<double, long>(ref target),
                newBits,
                originalBits);
            current = BitConverter.Int64BitsToDouble(currentBits);
        } while (BitConverter.DoubleToInt64Bits(current) != BitConverter.DoubleToInt64Bits(original));
        return original;
    }

    // ============================================================================
    // Atomic Subtract Operations
    // ============================================================================

    /// <summary>
    /// Atomically subtracts a value from an integer and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target integer.</param>
    /// <param name="value">The value to subtract.</param>
    /// <returns>The original value before the subtraction.</returns>
    /// <remarks>Translated to <c>atomicSub(&amp;target, value)</c> in CUDA.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AtomicSub(ref int target, int value)
        => Interlocked.Add(ref target, -value) + value;

    /// <summary>
    /// Atomically subtracts a value from an unsigned integer and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target unsigned integer.</param>
    /// <param name="value">The value to subtract.</param>
    /// <returns>The original value before the subtraction.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint AtomicSub(ref uint target, uint value)
    {
        uint original;
        var current = Volatile.Read(ref target);
        do
        {
            original = current;
            current = Interlocked.CompareExchange(ref target, original - value, original);
        } while (current != original);
        return original;
    }

    /// <summary>
    /// Atomically subtracts a value from a long integer and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target long integer.</param>
    /// <param name="value">The value to subtract.</param>
    /// <returns>The original value before the subtraction.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AtomicSub(ref long target, long value)
        => Interlocked.Add(ref target, -value) + value;

    /// <summary>
    /// Atomically subtracts a value from an unsigned long integer and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target unsigned long integer.</param>
    /// <param name="value">The value to subtract.</param>
    /// <returns>The original value before the subtraction.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong AtomicSub(ref ulong target, ulong value)
    {
        ulong original;
        var current = Volatile.Read(ref target);
        do
        {
            original = current;
            current = Interlocked.CompareExchange(ref target, original - value, original);
        } while (current != original);
        return original;
    }

    // ============================================================================
    // Atomic Exchange Operations
    // ============================================================================

    /// <summary>
    /// Atomically exchanges an integer value and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target integer.</param>
    /// <param name="value">The value to store.</param>
    /// <returns>The original value before the exchange.</returns>
    /// <remarks>Translated to <c>atomicExch(&amp;target, value)</c> in CUDA.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AtomicExchange(ref int target, int value)
        => Interlocked.Exchange(ref target, value);

    /// <summary>
    /// Atomically exchanges an unsigned integer value and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target unsigned integer.</param>
    /// <param name="value">The value to store.</param>
    /// <returns>The original value before the exchange.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint AtomicExchange(ref uint target, uint value)
        => Interlocked.Exchange(ref target, value);

    /// <summary>
    /// Atomically exchanges a long integer value and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target long integer.</param>
    /// <param name="value">The value to store.</param>
    /// <returns>The original value before the exchange.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AtomicExchange(ref long target, long value)
        => Interlocked.Exchange(ref target, value);

    /// <summary>
    /// Atomically exchanges an unsigned long integer value and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target unsigned long integer.</param>
    /// <param name="value">The value to store.</param>
    /// <returns>The original value before the exchange.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong AtomicExchange(ref ulong target, ulong value)
        => Interlocked.Exchange(ref target, value);

    /// <summary>
    /// Atomically exchanges a float value and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target float.</param>
    /// <param name="value">The value to store.</param>
    /// <returns>The original value before the exchange.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float AtomicExchange(ref float target, float value)
    {
        var result = Interlocked.Exchange(
            ref Unsafe.As<float, int>(ref target),
            BitConverter.SingleToInt32Bits(value));
        return BitConverter.Int32BitsToSingle(result);
    }

    /// <summary>
    /// Atomically exchanges a double value and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target double.</param>
    /// <param name="value">The value to store.</param>
    /// <returns>The original value before the exchange.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double AtomicExchange(ref double target, double value)
    {
        var result = Interlocked.Exchange(
            ref Unsafe.As<double, long>(ref target),
            BitConverter.DoubleToInt64Bits(value));
        return BitConverter.Int64BitsToDouble(result);
    }

    // ============================================================================
    // Atomic Compare-Exchange Operations
    // ============================================================================

    /// <summary>
    /// Atomically compares and exchanges an integer value.
    /// </summary>
    /// <param name="target">Reference to the target integer.</param>
    /// <param name="expected">The value to compare against.</param>
    /// <param name="desired">The value to store if comparison succeeds.</param>
    /// <returns>The original value (use to check if exchange occurred: result == expected).</returns>
    /// <remarks>Translated to <c>atomicCAS(&amp;target, expected, desired)</c> in CUDA.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AtomicCompareExchange(ref int target, int expected, int desired)
        => Interlocked.CompareExchange(ref target, desired, expected);

    /// <summary>
    /// Atomically compares and exchanges an unsigned integer value.
    /// </summary>
    /// <param name="target">Reference to the target unsigned integer.</param>
    /// <param name="expected">The value to compare against.</param>
    /// <param name="desired">The value to store if comparison succeeds.</param>
    /// <returns>The original value (use to check if exchange occurred: result == expected).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint AtomicCompareExchange(ref uint target, uint expected, uint desired)
        => Interlocked.CompareExchange(ref target, desired, expected);

    /// <summary>
    /// Atomically compares and exchanges a long integer value.
    /// </summary>
    /// <param name="target">Reference to the target long integer.</param>
    /// <param name="expected">The value to compare against.</param>
    /// <param name="desired">The value to store if comparison succeeds.</param>
    /// <returns>The original value (use to check if exchange occurred: result == expected).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AtomicCompareExchange(ref long target, long expected, long desired)
        => Interlocked.CompareExchange(ref target, desired, expected);

    /// <summary>
    /// Atomically compares and exchanges an unsigned long integer value.
    /// </summary>
    /// <param name="target">Reference to the target unsigned long integer.</param>
    /// <param name="expected">The value to compare against.</param>
    /// <param name="desired">The value to store if comparison succeeds.</param>
    /// <returns>The original value (use to check if exchange occurred: result == expected).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong AtomicCompareExchange(ref ulong target, ulong expected, ulong desired)
        => Interlocked.CompareExchange(ref target, desired, expected);

    /// <summary>
    /// Atomically compares and exchanges a float value.
    /// </summary>
    /// <param name="target">Reference to the target float.</param>
    /// <param name="expected">The value to compare against.</param>
    /// <param name="desired">The value to store if comparison succeeds.</param>
    /// <returns>The original value (use to check if exchange occurred: result == expected).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float AtomicCompareExchange(ref float target, float expected, float desired)
    {
        var result = Interlocked.CompareExchange(
            ref Unsafe.As<float, int>(ref target),
            BitConverter.SingleToInt32Bits(desired),
            BitConverter.SingleToInt32Bits(expected));
        return BitConverter.Int32BitsToSingle(result);
    }

    /// <summary>
    /// Atomically compares and exchanges a double value.
    /// </summary>
    /// <param name="target">Reference to the target double.</param>
    /// <param name="expected">The value to compare against.</param>
    /// <param name="desired">The value to store if comparison succeeds.</param>
    /// <returns>The original value (use to check if exchange occurred: result == expected).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double AtomicCompareExchange(ref double target, double expected, double desired)
    {
        var result = Interlocked.CompareExchange(
            ref Unsafe.As<double, long>(ref target),
            BitConverter.DoubleToInt64Bits(desired),
            BitConverter.DoubleToInt64Bits(expected));
        return BitConverter.Int64BitsToDouble(result);
    }

    // ============================================================================
    // Atomic Min/Max Operations
    // ============================================================================

    /// <summary>
    /// Atomically computes the minimum and stores it.
    /// </summary>
    /// <param name="target">Reference to the target integer.</param>
    /// <param name="value">The value to compare.</param>
    /// <returns>The original value before the operation.</returns>
    /// <remarks>Translated to <c>atomicMin(&amp;target, value)</c> in CUDA.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AtomicMin(ref int target, int value)
    {
        int original;
        var current = Volatile.Read(ref target);
        do
        {
            original = current;
            if (value >= original)
            {
                return original;
            }
            current = Interlocked.CompareExchange(ref target, value, original);
        } while (current != original);
        return original;
    }

    /// <summary>
    /// Atomically computes the minimum and stores it.
    /// </summary>
    /// <param name="target">Reference to the target unsigned integer.</param>
    /// <param name="value">The value to compare.</param>
    /// <returns>The original value before the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint AtomicMin(ref uint target, uint value)
    {
        uint original;
        var current = Volatile.Read(ref target);
        do
        {
            original = current;
            if (value >= original)
            {
                return original;
            }
            current = Interlocked.CompareExchange(ref target, value, original);
        } while (current != original);
        return original;
    }

    /// <summary>
    /// Atomically computes the minimum and stores it.
    /// </summary>
    /// <param name="target">Reference to the target long integer.</param>
    /// <param name="value">The value to compare.</param>
    /// <returns>The original value before the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AtomicMin(ref long target, long value)
    {
        long original;
        var current = Volatile.Read(ref target);
        do
        {
            original = current;
            if (value >= original)
            {
                return original;
            }
            current = Interlocked.CompareExchange(ref target, value, original);
        } while (current != original);
        return original;
    }

    /// <summary>
    /// Atomically computes the minimum and stores it.
    /// </summary>
    /// <param name="target">Reference to the target unsigned long integer.</param>
    /// <param name="value">The value to compare.</param>
    /// <returns>The original value before the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong AtomicMin(ref ulong target, ulong value)
    {
        ulong original;
        var current = Volatile.Read(ref target);
        do
        {
            original = current;
            if (value >= original)
            {
                return original;
            }
            current = Interlocked.CompareExchange(ref target, value, original);
        } while (current != original);
        return original;
    }

    /// <summary>
    /// Atomically computes the maximum and stores it.
    /// </summary>
    /// <param name="target">Reference to the target integer.</param>
    /// <param name="value">The value to compare.</param>
    /// <returns>The original value before the operation.</returns>
    /// <remarks>Translated to <c>atomicMax(&amp;target, value)</c> in CUDA.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AtomicMax(ref int target, int value)
    {
        int original;
        var current = Volatile.Read(ref target);
        do
        {
            original = current;
            if (value <= original)
            {
                return original;
            }
            current = Interlocked.CompareExchange(ref target, value, original);
        } while (current != original);
        return original;
    }

    /// <summary>
    /// Atomically computes the maximum and stores it.
    /// </summary>
    /// <param name="target">Reference to the target unsigned integer.</param>
    /// <param name="value">The value to compare.</param>
    /// <returns>The original value before the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint AtomicMax(ref uint target, uint value)
    {
        uint original;
        var current = Volatile.Read(ref target);
        do
        {
            original = current;
            if (value <= original)
            {
                return original;
            }
            current = Interlocked.CompareExchange(ref target, value, original);
        } while (current != original);
        return original;
    }

    /// <summary>
    /// Atomically computes the maximum and stores it.
    /// </summary>
    /// <param name="target">Reference to the target long integer.</param>
    /// <param name="value">The value to compare.</param>
    /// <returns>The original value before the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AtomicMax(ref long target, long value)
    {
        long original;
        var current = Volatile.Read(ref target);
        do
        {
            original = current;
            if (value <= original)
            {
                return original;
            }
            current = Interlocked.CompareExchange(ref target, value, original);
        } while (current != original);
        return original;
    }

    /// <summary>
    /// Atomically computes the maximum and stores it.
    /// </summary>
    /// <param name="target">Reference to the target unsigned long integer.</param>
    /// <param name="value">The value to compare.</param>
    /// <returns>The original value before the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong AtomicMax(ref ulong target, ulong value)
    {
        ulong original;
        var current = Volatile.Read(ref target);
        do
        {
            original = current;
            if (value <= original)
            {
                return original;
            }
            current = Interlocked.CompareExchange(ref target, value, original);
        } while (current != original);
        return original;
    }

    // ============================================================================
    // Atomic Bitwise Operations
    // ============================================================================

    /// <summary>
    /// Atomically performs a bitwise AND operation and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target integer.</param>
    /// <param name="value">The value to AND with.</param>
    /// <returns>The original value before the operation.</returns>
    /// <remarks>Translated to <c>atomicAnd(&amp;target, value)</c> in CUDA.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AtomicAnd(ref int target, int value)
        => Interlocked.And(ref target, value);

    /// <summary>
    /// Atomically performs a bitwise AND operation and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target unsigned integer.</param>
    /// <param name="value">The value to AND with.</param>
    /// <returns>The original value before the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint AtomicAnd(ref uint target, uint value)
        => Interlocked.And(ref target, value);

    /// <summary>
    /// Atomically performs a bitwise AND operation and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target long integer.</param>
    /// <param name="value">The value to AND with.</param>
    /// <returns>The original value before the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AtomicAnd(ref long target, long value)
        => Interlocked.And(ref target, value);

    /// <summary>
    /// Atomically performs a bitwise AND operation and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target unsigned long integer.</param>
    /// <param name="value">The value to AND with.</param>
    /// <returns>The original value before the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong AtomicAnd(ref ulong target, ulong value)
        => Interlocked.And(ref target, value);

    /// <summary>
    /// Atomically performs a bitwise OR operation and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target integer.</param>
    /// <param name="value">The value to OR with.</param>
    /// <returns>The original value before the operation.</returns>
    /// <remarks>Translated to <c>atomicOr(&amp;target, value)</c> in CUDA.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AtomicOr(ref int target, int value)
        => Interlocked.Or(ref target, value);

    /// <summary>
    /// Atomically performs a bitwise OR operation and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target unsigned integer.</param>
    /// <param name="value">The value to OR with.</param>
    /// <returns>The original value before the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint AtomicOr(ref uint target, uint value)
        => Interlocked.Or(ref target, value);

    /// <summary>
    /// Atomically performs a bitwise OR operation and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target long integer.</param>
    /// <param name="value">The value to OR with.</param>
    /// <returns>The original value before the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AtomicOr(ref long target, long value)
        => Interlocked.Or(ref target, value);

    /// <summary>
    /// Atomically performs a bitwise OR operation and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target unsigned long integer.</param>
    /// <param name="value">The value to OR with.</param>
    /// <returns>The original value before the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong AtomicOr(ref ulong target, ulong value)
        => Interlocked.Or(ref target, value);

    /// <summary>
    /// Atomically performs a bitwise XOR operation and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target integer.</param>
    /// <param name="value">The value to XOR with.</param>
    /// <returns>The original value before the operation.</returns>
    /// <remarks>Translated to <c>atomicXor(&amp;target, value)</c> in CUDA.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AtomicXor(ref int target, int value)
    {
        int original;
        var current = Volatile.Read(ref target);
        do
        {
            original = current;
            current = Interlocked.CompareExchange(ref target, original ^ value, original);
        } while (current != original);
        return original;
    }

    /// <summary>
    /// Atomically performs a bitwise XOR operation and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target unsigned integer.</param>
    /// <param name="value">The value to XOR with.</param>
    /// <returns>The original value before the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint AtomicXor(ref uint target, uint value)
    {
        uint original;
        var current = Volatile.Read(ref target);
        do
        {
            original = current;
            current = Interlocked.CompareExchange(ref target, original ^ value, original);
        } while (current != original);
        return original;
    }

    /// <summary>
    /// Atomically performs a bitwise XOR operation and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target long integer.</param>
    /// <param name="value">The value to XOR with.</param>
    /// <returns>The original value before the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AtomicXor(ref long target, long value)
    {
        long original;
        var current = Volatile.Read(ref target);
        do
        {
            original = current;
            current = Interlocked.CompareExchange(ref target, original ^ value, original);
        } while (current != original);
        return original;
    }

    /// <summary>
    /// Atomically performs a bitwise XOR operation and returns the original value.
    /// </summary>
    /// <param name="target">Reference to the target unsigned long integer.</param>
    /// <param name="value">The value to XOR with.</param>
    /// <returns>The original value before the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong AtomicXor(ref ulong target, ulong value)
    {
        ulong original;
        var current = Volatile.Read(ref target);
        do
        {
            original = current;
            current = Interlocked.CompareExchange(ref target, original ^ value, original);
        } while (current != original);
        return original;
    }

    // ============================================================================
    // Atomic Load/Store with Memory Ordering
    // ============================================================================

    /// <summary>
    /// Atomically loads an integer value with specified memory ordering.
    /// </summary>
    /// <param name="target">Reference to the target integer.</param>
    /// <param name="order">Memory ordering semantics.</param>
    /// <returns>The current value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AtomicLoad(ref int target, MemoryOrder order = MemoryOrder.SequentiallyConsistent)
    {
        var value = Volatile.Read(ref target);
        if (order >= MemoryOrder.Acquire)
        {
            Thread.MemoryBarrier();
        }

        return value;
    }

    /// <summary>
    /// Atomically loads a long value with specified memory ordering.
    /// </summary>
    /// <param name="target">Reference to the target long.</param>
    /// <param name="order">Memory ordering semantics.</param>
    /// <returns>The current value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AtomicLoad(ref long target, MemoryOrder order = MemoryOrder.SequentiallyConsistent)
    {
        var value = Volatile.Read(ref target);
        if (order >= MemoryOrder.Acquire)
        {
            Thread.MemoryBarrier();
        }

        return value;
    }

    /// <summary>
    /// Atomically stores an integer value with specified memory ordering.
    /// </summary>
    /// <param name="target">Reference to the target integer.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="order">Memory ordering semantics.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AtomicStore(ref int target, int value, MemoryOrder order = MemoryOrder.SequentiallyConsistent)
    {
        if (order >= MemoryOrder.Release)
        {
            Thread.MemoryBarrier();
        }

        Volatile.Write(ref target, value);
        if (order == MemoryOrder.SequentiallyConsistent)
        {
            Thread.MemoryBarrier();
        }
    }

    /// <summary>
    /// Atomically stores a long value with specified memory ordering.
    /// </summary>
    /// <param name="target">Reference to the target long.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="order">Memory ordering semantics.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AtomicStore(ref long target, long value, MemoryOrder order = MemoryOrder.SequentiallyConsistent)
    {
        if (order >= MemoryOrder.Release)
        {
            Thread.MemoryBarrier();
        }

        Volatile.Write(ref target, value);
        if (order == MemoryOrder.SequentiallyConsistent)
        {
            Thread.MemoryBarrier();
        }
    }

    // ============================================================================
    // Memory Fences
    // ============================================================================

    /// <summary>
    /// Issues a memory fence with the specified scope.
    /// </summary>
    /// <param name="scope">The memory scope for the fence.</param>
    /// <remarks>
    /// <para>
    /// On CPU, this maps to <c>Thread.MemoryBarrier()</c> for all scopes.
    /// On GPU backends, this maps to the appropriate fence instruction:
    /// </para>
    /// <list type="bullet">
    /// <item><description>CUDA: <c>__threadfence_block()</c>, <c>__threadfence()</c>, <c>__threadfence_system()</c></description></item>
    /// <item><description>OpenCL: <c>mem_fence(CLK_LOCAL_MEM_FENCE)</c>, <c>mem_fence(CLK_GLOBAL_MEM_FENCE)</c></description></item>
    /// <item><description>Metal: <c>threadgroup_barrier</c>, <c>device_barrier</c></description></item>
    /// </list>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThreadFence(MemoryScope scope = MemoryScope.Device)
    {
        // On CPU, all fences are equivalent to a full memory barrier
        Thread.MemoryBarrier();
    }

    /// <summary>
    /// Issues a full memory barrier ensuring all prior memory operations
    /// are visible before any subsequent operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MemoryBarrier()
        => Thread.MemoryBarrier();
}
