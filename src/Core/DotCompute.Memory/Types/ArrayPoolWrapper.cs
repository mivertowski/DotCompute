// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Buffers;
using Microsoft.Extensions.ObjectPool;

namespace DotCompute.Memory.Types;

/// <summary>
/// Simple wrapper to make ArrayPool compatible with ObjectPool interface.
/// </summary>
/// <typeparam name="T">Element type.</typeparam>
internal class ArrayPoolWrapper<T> : ObjectPool<T[]> where T : unmanaged
{
    private readonly ArrayPool<T> _arrayPool = ArrayPool<T>.Shared;
    /// <summary>
    /// Gets the .
    /// </summary>
    /// <returns>The .</returns>

    public override T[] Get() => _arrayPool.Rent(1024); // Default size, will be resized as needed
    /// <summary>
    /// Performs return.
    /// </summary>
    /// <param name="obj">The obj.</param>

    public override void Return(T[] obj) => _arrayPool.Return(obj);
}