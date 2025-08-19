// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Abstractions;

namespace DotCompute.Core.Memory
{

/// <summary>
/// Extension methods to provide compatibility with IBuffer interface changes.
/// </summary>
public static class BufferExtensions
{
    /// <summary>
    /// Extension method to safely get the length for any IMemoryBuffer.
    /// This provides compatibility when IBuffer doesn't have Length property in some implementations.
    /// </summary>
    public static int GetElementCount<T>(this IMemoryBuffer buffer) where T : unmanaged
    {
        return (int)(buffer.SizeInBytes / Unsafe.SizeOf<T>());
    }

    /// <summary>
    /// Extension method to safely get the length for any IBuffer.
    /// This provides a fallback calculation if Length isn't implemented.
    /// </summary>
    public static int GetElementCount<T>(this IBuffer<T> buffer) where T : unmanaged
    {
        try
        {
            return buffer.Length;
        }
        catch
        {
            // Fallback to calculation from SizeInBytes
            return (int)(buffer.SizeInBytes / Unsafe.SizeOf<T>());
        }
    }

    /// <summary>
    /// Safe disposal check that works with both typed and untyped disposable objects.
    /// </summary>
    public static bool IsSafeToDispose(this IDisposable disposable)
    {
        if (disposable is IMemoryBuffer memBuffer)
        {
            return !memBuffer.IsDisposed;
        }
        return true; // Assume safe if we can't check
    }

    /// <summary>
    /// Safe disposal that checks status first.
    /// </summary>
    public static void SafeDispose(this IDisposable disposable)
    {
        if (disposable.IsSafeToDispose())
        {
            disposable.Dispose();
        }
    }
}}
