// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Memory;

/// <summary>
/// Generic memory buffer interface for backward compatibility.
/// This is an alias/wrapper for IUnifiedMemoryBuffer to support legacy code.
/// </summary>
/// <typeparam name="T">The unmanaged type of elements in the buffer.</typeparam>
public interface IMemoryBuffer<T> : IUnifiedMemoryBuffer<T> where T : unmanaged
{
    // This interface inherits all members from IUnifiedMemoryBuffer
    // and exists for backward compatibility with existing code that
    // references IMemoryBuffer instead of IUnifiedMemoryBuffer
}