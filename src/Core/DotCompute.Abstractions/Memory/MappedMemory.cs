// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Memory
{
    /// <summary>
    /// Represents a mapped memory region that provides direct access to buffer memory.
    /// </summary>
    /// <typeparam name="T">The element type of the mapped memory.</typeparam>
    /// <remarks>
    /// Initializes a new instance of the <see cref="MappedMemory{T}"/> class.
    /// </remarks>
    public sealed class MappedMemory<T>(Memory<T> memory, Action? unmapAction = null) : IDisposable where T : unmanaged
    {
        private readonly Action? _unmapAction = unmapAction;
        private bool _disposed;

        /// <summary>
        /// Gets the memory span for the mapped region.
        /// </summary>
        public Memory<T> Memory { get; } = memory;

        /// <summary>
        /// Gets the span for the mapped region.
        /// </summary>
        public Span<T> Span => Memory.Span;

        /// <summary>
        /// Gets the length of the mapped region in elements.
        /// </summary>
        public int Length => Memory.Length;

        /// <summary>
        /// Gets whether this mapped memory is valid.
        /// </summary>
        public bool IsValid => !_disposed && Memory.Length > 0;

        /// <summary>
        /// Creates an invalid mapped memory instance.
        /// </summary>
        public static MappedMemory<T> Invalid => new(Memory<T>.Empty);

        /// <summary>
        /// Unmaps the memory and releases resources.
        /// </summary>
        public void Unmap() => Dispose();

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!_disposed)
            {
                _unmapAction?.Invoke();
                _disposed = true;
            }
        }
    }
}