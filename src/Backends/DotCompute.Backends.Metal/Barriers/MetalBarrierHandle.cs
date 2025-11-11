// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Barriers;

namespace DotCompute.Backends.Metal.Barriers;

/// <summary>
/// Metal-specific implementation of a barrier handle.
/// </summary>
/// <remarks>
/// <para>
/// Metal barriers are compile-time constructs injected into MSL kernels rather than
/// runtime objects. This handle primarily serves to:
/// <list type="bullet">
/// <item><description>Store barrier metadata for code generation</description></item>
/// <item><description>Track barrier lifecycle and usage</description></item>
/// <item><description>Provide unified API across backends</description></item>
/// </list>
/// </para>
/// <para>
/// Unlike CUDA cooperative groups, Metal barriers are:
/// <list type="bullet">
/// <item><description>Statically inserted at compile time via MSL code generation</description></item>
/// <item><description>Hardware-accelerated with ~10-20ns latency</description></item>
/// <item><description>Scope-limited (threadgroup or simdgroup only, no grid-wide sync)</description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class MetalBarrierHandle : IBarrierHandle
{
    private readonly int _barrierId;
    private readonly BarrierScope _scope;
    private readonly int _capacity;
    private readonly string? _name;
    private readonly MetalBarrierScope _metalScope;
    private readonly MetalMemoryFenceFlags _fenceFlags;
    private bool _isActive;
    private int _syncCount;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new Metal barrier handle.
    /// </summary>
    /// <param name="barrierId">Unique identifier for this barrier.</param>
    /// <param name="scope">Barrier synchronization scope.</param>
    /// <param name="capacity">Maximum number of threads that can synchronize.</param>
    /// <param name="name">Optional barrier name for debugging.</param>
    /// <param name="fenceFlags">Memory fence flags for this barrier.</param>
    public MetalBarrierHandle(
        int barrierId,
        BarrierScope scope,
        int capacity,
        string? name,
        MetalMemoryFenceFlags fenceFlags)
    {
        _barrierId = barrierId;
        _scope = scope;
        _capacity = capacity;
        _name = name;
        _fenceFlags = fenceFlags;

        // Map abstract scope to Metal-specific scope
        _metalScope = scope switch
        {
            BarrierScope.ThreadBlock => MetalBarrierScope.Threadgroup,
            BarrierScope.Warp => MetalBarrierScope.Simdgroup,
            BarrierScope.Grid => throw new NotSupportedException(
                "Grid-wide barriers are not supported in Metal. " +
                "Use multiple kernel dispatches with CPU-side synchronization instead."),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported barrier scope")
        };

        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <inheritdoc/>
    public int BarrierId => _barrierId;

    /// <inheritdoc/>
    public BarrierScope Scope => _scope;

    /// <inheritdoc/>
    public int Capacity => _capacity;

    /// <inheritdoc/>
    public string? Name => _name;

    /// <inheritdoc/>
    public bool IsActive
    {
        get
        {
            lock (_lock)
            {
                return _isActive;
            }
        }
    }

    /// <inheritdoc/>
    public int SyncCount
    {
        get
        {
            lock (_lock)
            {
                return _syncCount;
            }
        }
    }

    /// <inheritdoc/>
    public int ThreadsWaiting
    {
        get
        {
            lock (_lock)
            {
                // For Metal, ThreadsWaiting is estimated based on whether barrier is active
                // Actual thread counting would require native Metal support
                return _isActive ? _capacity : 0;
            }
        }
    }

    /// <inheritdoc/>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the Metal-specific barrier scope.
    /// </summary>
    internal MetalBarrierScope MetalScope => _metalScope;

    /// <summary>
    /// Gets the memory fence flags for this barrier.
    /// </summary>
    internal MetalMemoryFenceFlags FenceFlags => _fenceFlags;

    /// <inheritdoc/>
    public void Sync()
    {
        lock (_lock)
        {
            _syncCount++;
            _isActive = true;
        }
    }

    /// <inheritdoc/>
    public void Reset()
    {
        lock (_lock)
        {
            _syncCount = 0;
            _isActive = false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Reset();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Generates the MSL barrier call code for this barrier.
    /// </summary>
    /// <returns>MSL code string for barrier synchronization.</returns>
    internal string GenerateMslBarrierCode()
    {
        var barrierFunction = _metalScope switch
        {
            MetalBarrierScope.Threadgroup => "threadgroup_barrier",
            MetalBarrierScope.Simdgroup => "simdgroup_barrier",
            _ => throw new InvalidOperationException($"Unsupported Metal barrier scope: {_metalScope}")
        };

        var fenceFlag = _fenceFlags switch
        {
            MetalMemoryFenceFlags.None => "mem_flags::mem_none",
            MetalMemoryFenceFlags.Device => "mem_flags::mem_device",
            MetalMemoryFenceFlags.Threadgroup => "mem_flags::mem_threadgroup",
            MetalMemoryFenceFlags.Texture => "mem_flags::mem_texture",
            MetalMemoryFenceFlags.DeviceAndThreadgroup => "mem_flags::mem_device_and_threadgroup",
            _ => "mem_flags::mem_device_and_threadgroup" // Safe default
        };

        // Generate barrier with optional debug annotation
        if (!string.IsNullOrEmpty(_name))
        {
            return $"// Barrier: {_name}\n    {barrierFunction}({fenceFlag});";
        }

        return $"{barrierFunction}({fenceFlag});";
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"MetalBarrier[Id={_barrierId}, Scope={_scope}, Capacity={_capacity}, " +
               $"Name={_name ?? "unnamed"}, Active={IsActive}, SyncCount={SyncCount}]";
    }
}
