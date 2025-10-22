// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Execution;

/// <summary>
/// High-level Metal command encoder abstraction for compute operations,
/// providing a simplified interface over native Metal compute command encoders.
/// </summary>
public sealed class MetalCommandEncoder : IDisposable
{
    private readonly IntPtr _encoder;
    private readonly IntPtr _commandBuffer;
    private readonly ILogger<MetalCommandEncoder> _logger;
    private readonly List<MetalEncoderCommand> _commands;
    private readonly Dictionary<int, IntPtr> _boundBuffers;
    private volatile bool _disposed;
    private volatile bool _encodingEnded;

    public MetalCommandEncoder(IntPtr commandBuffer, ILogger<MetalCommandEncoder> logger)
    {
        _commandBuffer = commandBuffer != IntPtr.Zero ? commandBuffer : throw new ArgumentNullException(nameof(commandBuffer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _commands = [];
        _boundBuffers = [];

        // Create the compute command encoder
        _encoder = MetalNative.CreateComputeCommandEncoder(_commandBuffer);
        if (_encoder == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create Metal compute command encoder");
        }

        _logger.LogTrace("Created Metal command encoder for command buffer {CommandBuffer}", _commandBuffer);
    }

    /// <summary>
    /// Gets the native encoder handle
    /// </summary>
    public IntPtr EncoderHandle => _encoder;

    /// <summary>
    /// Gets whether encoding has been ended
    /// </summary>
    public bool IsEncodingEnded => _encodingEnded;

    /// <summary>
    /// Sets the compute pipeline state for subsequent operations
    /// </summary>
    public void SetComputePipelineState(IntPtr pipelineState)
    {
        ThrowIfDisposed();
        ThrowIfEncodingEnded();

        if (pipelineState == IntPtr.Zero)
        {
            throw new ArgumentException("Pipeline state cannot be null", nameof(pipelineState));
        }

        MetalNative.SetComputePipelineState(_encoder, pipelineState);


        var command = new MetalEncoderCommand
        {
            Type = MetalCommandType.SetPipelineState,
            PipelineState = pipelineState,
            Timestamp = DateTimeOffset.UtcNow
        };
        _commands.Add(command);

        _logger.LogTrace("Set compute pipeline state {PipelineState} on encoder {Encoder}", pipelineState, _encoder);
    }

    /// <summary>
    /// Binds a buffer to the specified index
    /// </summary>
    public void SetBuffer(IntPtr buffer, nuint offset, int index)
    {
        ThrowIfDisposed();
        ThrowIfEncodingEnded();

        if (buffer == IntPtr.Zero)
        {
            throw new ArgumentException("Buffer cannot be null", nameof(buffer));
        }

        if (index is < 0 or > 31) // Metal supports up to 32 buffer bindings
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Buffer index must be between 0 and 31");
        }

        MetalNative.SetBuffer(_encoder, buffer, offset, index);
        _boundBuffers[index] = buffer;

        var command = new MetalEncoderCommand
        {
            Type = MetalCommandType.SetBuffer,
            Buffer = buffer,
            BufferOffset = (long)offset,
            BufferIndex = index,
            Timestamp = DateTimeOffset.UtcNow
        };
        _commands.Add(command);

        _logger.LogTrace("Bound buffer {Buffer} at offset {Offset} to index {Index} on encoder {Encoder}",

            buffer, offset, index, _encoder);
    }

    /// <summary>
    /// Sets constant data at the specified index
    /// </summary>
    public unsafe void SetBytes<T>(T data, int index) where T : unmanaged
    {
        ThrowIfDisposed();
        ThrowIfEncodingEnded();

        if (index is < 0 or > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Buffer index must be between 0 and 31");
        }

        var size = (nuint)sizeof(T);
        var ptr = System.Runtime.CompilerServices.Unsafe.AsPointer(ref data);
        MetalNative.SetBytes(_encoder, (IntPtr)ptr, size, index);

        var command = new MetalEncoderCommand
        {
            Type = MetalCommandType.SetBytes,
            BufferIndex = index,
            BytesSize = (long)size,
            Timestamp = DateTimeOffset.UtcNow
        };
        _commands.Add(command);

        _logger.LogTrace("Set {Size} bytes of constant data at index {Index} on encoder {Encoder}", size, index, _encoder);
    }

    /// <summary>
    /// Sets constant data from a byte array
    /// </summary>
    public unsafe void SetBytes(ReadOnlySpan<byte> data, int index)
    {
        ThrowIfDisposed();
        ThrowIfEncodingEnded();

        if (index is < 0 or > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Buffer index must be between 0 and 31");
        }

        if (data.Length == 0)
        {
            throw new ArgumentException("Data cannot be empty", nameof(data));
        }

        fixed (byte* ptr = data)
        {
            MetalNative.SetBytes(_encoder, (IntPtr)ptr, (nuint)data.Length, index);
        }

        var command = new MetalEncoderCommand
        {
            Type = MetalCommandType.SetBytes,
            BufferIndex = index,
            BytesSize = data.Length,
            Timestamp = DateTimeOffset.UtcNow
        };
        _commands.Add(command);

        _logger.LogTrace("Set {Size} bytes from span at index {Index} on encoder {Encoder}", data.Length, index, _encoder);
    }

    /// <summary>
    /// Dispatches threadgroups for compute execution
    /// </summary>
    public void DispatchThreadgroups(MetalDispatchSize gridSize, MetalDispatchSize threadgroupSize)
    {
        ThrowIfDisposed();
        ThrowIfEncodingEnded();

        ValidateDispatchSize(gridSize, nameof(gridSize));
        ValidateDispatchSize(threadgroupSize, nameof(threadgroupSize));

        var nativeGridSize = new MetalSize
        {

            width = (nuint)gridSize.Width,

            height = (nuint)gridSize.Height,

            depth = (nuint)gridSize.Depth
        };


        var nativeThreadgroupSize = new MetalSize
        {

            width = (nuint)threadgroupSize.Width,

            height = (nuint)threadgroupSize.Height,

            depth = (nuint)threadgroupSize.Depth

        };

        MetalNative.DispatchThreadgroups(_encoder, nativeGridSize, nativeThreadgroupSize);

        var command = new MetalEncoderCommand
        {
            Type = MetalCommandType.DispatchThreadgroups,
            GridSize = gridSize,
            ThreadgroupSize = threadgroupSize,
            Timestamp = DateTimeOffset.UtcNow
        };
        _commands.Add(command);

        _logger.LogDebug("Dispatched threadgroups: grid=({GridW}, {GridH}, {GridD}), threadgroup=({TgW}, {TgH}, {TgD})",
            gridSize.Width, gridSize.Height, gridSize.Depth,
            threadgroupSize.Width, threadgroupSize.Height, threadgroupSize.Depth);
    }

    /// <summary>
    /// Dispatches threadgroups with 1D configuration
    /// </summary>
    public void DispatchThreadgroups1D(int gridWidth, int threadgroupWidth)
    {
        DispatchThreadgroups(
            new MetalDispatchSize(gridWidth, 1, 1),
            new MetalDispatchSize(threadgroupWidth, 1, 1));
    }

    /// <summary>
    /// Dispatches threadgroups with 2D configuration
    /// </summary>
    public void DispatchThreadgroups2D(int gridWidth, int gridHeight, int threadgroupWidth, int threadgroupHeight)
    {
        DispatchThreadgroups(
            new MetalDispatchSize(gridWidth, gridHeight, 1),
            new MetalDispatchSize(threadgroupWidth, threadgroupHeight, 1));
    }

    /// <summary>
    /// Ends encoding and finalizes the command encoder
    /// </summary>
    public void EndEncoding()
    {
        ThrowIfDisposed();

        if (_encodingEnded)
        {
            return; // Already ended
        }

        MetalNative.EndEncoding(_encoder);
        _encodingEnded = true;

        var command = new MetalEncoderCommand
        {
            Type = MetalCommandType.EndEncoding,
            Timestamp = DateTimeOffset.UtcNow
        };
        _commands.Add(command);

        _logger.LogTrace("Ended encoding for encoder {Encoder} with {CommandCount} commands", _encoder, _commands.Count);
    }

    /// <summary>
    /// Gets a summary of all commands encoded
    /// </summary>
    public MetalEncodingStats GetEncodingStats()
    {
        var stats = new MetalEncodingStats
        {
            TotalCommands = _commands.Count,
            BuffersSet = _boundBuffers.Count,
            IsEncodingEnded = _encodingEnded,
            FirstCommandTime = _commands.Count > 0 ? _commands[0].Timestamp : (DateTimeOffset?)null,
            LastCommandTime = _commands.Count > 0 ? _commands[^1].Timestamp : (DateTimeOffset?)null
        };

        // Count command types
        foreach (var group in _commands.GroupBy(c => c.Type))
        {
            stats.CommandTypeCounts[group.Key] = group.Count();
        }

        // Calculate encoding duration
        if (stats.FirstCommandTime.HasValue && stats.LastCommandTime.HasValue)
        {
            stats.EncodingDuration = stats.LastCommandTime.Value - stats.FirstCommandTime.Value;
        }

        return stats;
    }

    /// <summary>
    /// Gets detailed information about all encoded commands
    /// </summary>
    public IReadOnlyList<MetalEncoderCommand> GetCommandHistory() => _commands.AsReadOnly();

    private static void ValidateDispatchSize(MetalDispatchSize size, string paramName)
    {
        if (size.Width <= 0 || size.Height <= 0 || size.Depth <= 0)
        {
            throw new ArgumentException($"All dimensions must be positive: {size}", paramName);
        }

        if (size.Width > 65535 || size.Height > 65535 || size.Depth > 65535)
        {
            throw new ArgumentException($"Dispatch dimensions too large: {size}", paramName);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private void ThrowIfEncodingEnded()
    {
        if (_encodingEnded)
        {
            throw new InvalidOperationException("Cannot encode commands after EndEncoding() has been called");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // End encoding if not already done
            if (!_encodingEnded)
            {
                try
                {
                    EndEncoding();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error ending encoding during disposal");
                }
            }

            // Release the native encoder
            if (_encoder != IntPtr.Zero)
            {
                try
                {
                    MetalNative.ReleaseEncoder(_encoder);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error releasing encoder during disposal");
                }
            }

            var stats = GetEncodingStats();
            _logger.LogTrace("Disposed Metal command encoder: {CommandCount} commands encoded, duration: {Duration}ms",
                stats.TotalCommands, stats.EncodingDuration?.TotalMilliseconds ?? 0);
        }
    }
}

/// <summary>
/// Factory for creating Metal command encoders
/// </summary>
public sealed class MetalCommandEncoderFactory(ILogger<MetalCommandEncoder> logger)
{
    private readonly ILogger<MetalCommandEncoder> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<IntPtr, int> _activeEncoders = new();

    /// <summary>
    /// Creates a new command encoder for the specified command buffer
    /// </summary>
    public MetalCommandEncoder CreateEncoder(IntPtr commandBuffer)
    {
        var encoder = new MetalCommandEncoder(commandBuffer, _logger);
        _ = _activeEncoders.AddOrUpdate(commandBuffer, 1, (_, count) => count + 1);
        return encoder;
    }

    /// <summary>
    /// Gets the number of active encoders for a command buffer
    /// </summary>
    public int GetActiveEncoderCount(IntPtr commandBuffer) => _activeEncoders.TryGetValue(commandBuffer, out var count) ? count : 0;

    internal void NotifyEncoderDisposed(IntPtr commandBuffer)
    {
        _ = _activeEncoders.AddOrUpdate(commandBuffer, 0, (_, count) => Math.Max(0, count - 1));
        if (_activeEncoders[commandBuffer] == 0)
        {
            _ = _activeEncoders.TryRemove(commandBuffer, out _);
        }
    }
}

#region Supporting Types

/// <summary>
/// Represents a dispatch size for Metal compute operations
/// </summary>
public readonly struct MetalDispatchSize(int width, int height, int depth) : IEquatable<MetalDispatchSize>
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public int Depth { get; } = depth;

    public long TotalThreads => (long)Width * Height * Depth;

    public bool Equals(MetalDispatchSize other)
        => Width == other.Width && Height == other.Height && Depth == other.Depth;

    public override bool Equals(object? obj) => obj is MetalDispatchSize other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Width, Height, Depth);

    public override string ToString() => $"({Width}, {Height}, {Depth})";

    public static bool operator ==(MetalDispatchSize left, MetalDispatchSize right) => left.Equals(right);
    public static bool operator !=(MetalDispatchSize left, MetalDispatchSize right) => !left.Equals(right);
}

/// <summary>
/// Types of encoder commands
/// </summary>
public enum MetalCommandType
{
    SetPipelineState,
    SetBuffer,
    SetBytes,
    DispatchThreadgroups,
    EndEncoding
}

/// <summary>
/// Represents a command that was encoded
/// </summary>
public sealed class MetalEncoderCommand
{
    public MetalCommandType Type { get; set; }
    public IntPtr PipelineState { get; set; }
    public IntPtr Buffer { get; set; }
    public long BufferOffset { get; set; }
    public int BufferIndex { get; set; }
    public long BytesSize { get; set; }
    public MetalDispatchSize GridSize { get; set; }
    public MetalDispatchSize ThreadgroupSize { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Statistics about encoding operations
/// </summary>
public sealed class MetalEncodingStats
{
    public int TotalCommands { get; set; }
    public int BuffersSet { get; set; }
    public bool IsEncodingEnded { get; set; }
    public DateTimeOffset? FirstCommandTime { get; set; }
    public DateTimeOffset? LastCommandTime { get; set; }
    public TimeSpan? EncodingDuration { get; set; }
    public Dictionary<MetalCommandType, int> CommandTypeCounts { get; } = [];
}



#endregion