// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Kernels;


/// <summary>
/// Extended kernel execution context for CPU backend with additional helper methods.
/// </summary>
internal sealed class ExtendedKernelExecutionContext
{
    private readonly Dictionary<int, object> _parameters = [];
    private readonly Dictionary<int, Memory<byte>> _buffers = [];
    private readonly DotCompute.Core.KernelExecutionContext _innerContext;

    public ExtendedKernelExecutionContext()
    {
        _innerContext = new DotCompute.Core.KernelExecutionContext
        {
            Name = "CPU Kernel",
            WorkDimensions = new[] { 1L }
        };
    }

    public string Name => _innerContext.Name;
    public IReadOnlyList<long>? WorkDimensions => _innerContext.WorkDimensions;
    public object[]? Arguments => _innerContext.Arguments;

    /// <summary>
    /// Sets a parameter at the given index.
    /// </summary>
    public void SetParameter(int index, object parameter) => _parameters[index] = parameter;

    /// <summary>
    /// Gets a parameter at the given index.
    /// </summary>
    public object? GetParameter(int index) => _parameters.TryGetValue(index, out var value) ? value : null;

    /// <summary>
    /// Sets a buffer at the given index.
    /// </summary>
    public void SetBuffer(int index, Memory<byte> buffer) => _buffers[index] = buffer;

    /// <summary>
    /// Sets a typed buffer at the given index.
    /// </summary>
    public void SetBuffer<T>(int index, Memory<T> buffer) where T : unmanaged
    {
        var byteSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<T, byte>(buffer.Span);
        var byteBuffer = new Memory<byte>(byteSpan.ToArray());
        _buffers[index] = byteBuffer;
    }

    /// <summary>
    /// Gets a buffer at the given index.
    /// </summary>
    public Memory<T> GetBuffer<T>(int index) where T : unmanaged
    {
        if (!_buffers.TryGetValue(index, out var buffer))
        {
            return Memory<T>.Empty;
        }

        var span = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, T>(buffer.Span);
        return new Memory<T>(span.ToArray());
    }

    /// <summary>
    /// Gets a scalar value at the given index.
    /// </summary>
    public T GetScalar<T>(int index) where T : struct
    {
        var param = GetParameter(index);
        if (param is T value)
        {
            return value;
        }
        return default;
    }
}
