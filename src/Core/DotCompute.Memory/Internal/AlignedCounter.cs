// <copyright file="AlignedCounter.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Runtime.CompilerServices;
using System.Threading;

namespace DotCompute.Memory.Internal;

/// <summary>
/// Thread-safe counter with cache-line padding to prevent false sharing in high-performance scenarios.
/// </summary>
/// <remarks>
/// This struct uses padding fields to ensure the counter value occupies its own cache line,
/// preventing performance degradation from false sharing when multiple threads access different counters.
/// </remarks>
internal readonly struct AlignedCounter
{
#pragma warning disable CS0649 // Field is never assigned to - it's modified through Unsafe.AsRef
    private readonly long _value;
#pragma warning restore CS0649

    // Cache line padding to prevent false sharing (typical cache line is 64 bytes)
#pragma warning disable CS0169, CA1823 // The padding fields are intentionally unused to prevent false sharing
    private readonly byte _padding1, _padding2, _padding3, _padding4, _padding5, _padding6, _padding7;
    private readonly byte _padding8, _padding9, _padding10, _padding11, _padding12, _padding13, _padding14, _padding15;
    private readonly byte _padding16, _padding17, _padding18, _padding19, _padding20, _padding21, _padding22, _padding23;
    private readonly byte _padding24, _padding25, _padding26, _padding27, _padding28, _padding29, _padding30, _padding31;
    private readonly byte _padding32, _padding33, _padding34, _padding35, _padding36, _padding37, _padding38, _padding39;
    private readonly byte _padding40, _padding41, _padding42, _padding43, _padding44, _padding45, _padding46, _padding47;
    private readonly byte _padding48, _padding49, _padding50, _padding51, _padding52, _padding53, _padding54, _padding55;
#pragma warning restore CS0169, CA1823

    /// <summary>
    /// Gets the current value of the counter using atomic read operation.
    /// </summary>
    /// <value>The current counter value.</value>
    public readonly long Value => Interlocked.Read(ref Unsafe.AsRef(in _value));

    /// <summary>
    /// Atomically increments the counter by one.
    /// </summary>
    /// <returns>The incremented value.</returns>
    public readonly long Increment() => Interlocked.Increment(ref Unsafe.AsRef(in _value));

    /// <summary>
    /// Atomically decrements the counter by one.
    /// </summary>
    /// <returns>The decremented value.</returns>
    public readonly long Decrement() => Interlocked.Decrement(ref Unsafe.AsRef(in _value));

    /// <summary>
    /// Atomically adds the specified value to the counter.
    /// </summary>
    /// <param name="value">The value to add (can be negative for subtraction).</param>
    /// <returns>The new value after addition.</returns>
    public readonly long Add(long value) => Interlocked.Add(ref Unsafe.AsRef(in _value), value);

    /// <summary>
    /// Atomically sets the counter to the specified value.
    /// </summary>
    /// <param name="newValue">The new value to set.</param>
    /// <returns>The original value before the exchange.</returns>
    public readonly long Exchange(long newValue) => Interlocked.Exchange(ref Unsafe.AsRef(in _value), newValue);

    /// <summary>
    /// Atomically compares the counter value with a comparand and, if they are equal, replaces it with a new value.
    /// </summary>
    /// <param name="value">The value that replaces the counter value if the comparison results in equality.</param>
    /// <param name="comparand">The value that is compared to the counter value.</param>
    /// <returns>The original counter value.</returns>
    public readonly long CompareExchange(long value, long comparand) 
        => Interlocked.CompareExchange(ref Unsafe.AsRef(in _value), value, comparand);

    /// <summary>
    /// Resets the counter to zero.
    /// </summary>
    public readonly void Reset() => Exchange(0);

    /// <summary>
    /// Implicitly converts the AlignedCounter to its long value.
    /// </summary>
    /// <param name="counter">The counter to convert.</param>
    public static implicit operator long(AlignedCounter counter) => counter.Value;

    /// <summary>
    /// Returns a string representation of the counter value.
    /// </summary>
    /// <returns>The string representation of the current value.</returns>
    public override readonly string ToString() => Value.ToString();
}