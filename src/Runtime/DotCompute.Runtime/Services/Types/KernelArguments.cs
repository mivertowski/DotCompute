// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Runtime.Services.Types;

/// <summary>
/// Represents arguments to be passed to a kernel for execution.
/// </summary>
public class KernelArguments
{
    /// <summary>
    /// Gets or sets the memory buffers to be passed to the kernel.
    /// </summary>
    public IList<IUnifiedMemoryBuffer> Buffers { get; init; } = [];

    /// <summary>
    /// Gets or sets the scalar arguments to be passed to the kernel.
    /// </summary>
    public IList<object> ScalarArguments { get; init; } = [];

    /// <summary>
    /// Gets or sets the execution configuration (grid size, block size, etc.).
    /// </summary>
    public ExecutionConfiguration? Configuration { get; init; }

    /// <summary>
    /// Gets the total number of arguments.
    /// </summary>
    public int TotalArgumentCount => Buffers.Count + ScalarArguments.Count;

    /// <summary>
    /// Creates a new instance with the specified buffers and scalars.
    /// </summary>
    public static KernelArguments Create(
        IEnumerable<IUnifiedMemoryBuffer>? buffers = null,
        IEnumerable<object>? scalars = null,
        ExecutionConfiguration? configuration = null)
    {
        return new KernelArguments
        {
            Buffers = buffers?.ToList() ?? [],
            ScalarArguments = scalars?.ToList() ?? [],
            Configuration = configuration
        };
    }

    /// <summary>
    /// Validates that the arguments match expected parameter types.
    /// </summary>
    public bool ValidateTypes(Type[] expectedTypes)
    {
        if (expectedTypes.Length != TotalArgumentCount)
        {

            return false;
        }


        var actualTypes = Buffers.Select(b => b.GetType())
            .Concat(ScalarArguments.Select(s => s.GetType()))
            .ToArray();

        for (var i = 0; i < expectedTypes.Length; i++)
        {
            if (!expectedTypes[i].IsAssignableFrom(actualTypes[i]))
            {

                return false;
            }

        }

        return true;
    }
}

/// <summary>
/// Configuration for kernel execution (grid dimensions, shared memory, etc.).
/// </summary>
public class ExecutionConfiguration
{
    /// <summary>
    /// Gets or sets the number of work items in the global work group.
    /// </summary>
    public Dimensions3D GlobalWorkSize { get; init; } = new(1, 1, 1);

    /// <summary>
    /// Gets or sets the number of work items in each local work group.
    /// </summary>
    public Dimensions3D LocalWorkSize { get; init; } = new(1, 1, 1);

    /// <summary>
    /// Gets or sets the amount of shared memory to allocate in bytes.
    /// </summary>
    public int SharedMemoryBytes { get; init; }

    /// <summary>
    /// Gets or sets whether to use cooperative groups.
    /// </summary>
    public bool UseCooperativeGroups { get; init; }

    /// <summary>
    /// Gets or sets the stream/queue for execution (null for default).
    /// </summary>
    public object? ExecutionStream { get; init; }

    /// <summary>
    /// Creates a 1D execution configuration.
    /// </summary>
    public static ExecutionConfiguration Create1D(int globalSize, int localSize = 256)
    {
        return new ExecutionConfiguration
        {
            GlobalWorkSize = new Dimensions3D(globalSize, 1, 1),
            LocalWorkSize = new Dimensions3D(localSize, 1, 1)
        };
    }

    /// <summary>
    /// Creates a 2D execution configuration.
    /// </summary>
    public static ExecutionConfiguration Create2D(
        int globalX, int globalY,
        int localX = 16, int localY = 16)
    {
        return new ExecutionConfiguration
        {
            GlobalWorkSize = new Dimensions3D(globalX, globalY, 1),
            LocalWorkSize = new Dimensions3D(localX, localY, 1)
        };
    }

    /// <summary>
    /// Creates a 3D execution configuration.
    /// </summary>
    public static ExecutionConfiguration Create3D(
        int globalX, int globalY, int globalZ,
        int localX = 8, int localY = 8, int localZ = 8)
    {
        return new ExecutionConfiguration
        {
            GlobalWorkSize = new Dimensions3D(globalX, globalY, globalZ),
            LocalWorkSize = new Dimensions3D(localX, localY, localZ)
        };
    }
}

/// <summary>
/// Represents 3D dimensions for kernel execution.
/// </summary>
public readonly struct Dimensions3D : IEquatable<Dimensions3D>
{
    /// <summary>
    /// Gets the X dimension.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the Y dimension.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Gets the Z dimension.
    /// </summary>
    public int Z { get; }

    /// <summary>
    /// Gets the total number of elements (X * Y * Z).
    /// </summary>
    public int TotalElements => X * Y * Z;

    /// <summary>
    /// Initializes a new instance of the Dimensions3D struct.
    /// </summary>
    public Dimensions3D(int x, int y = 1, int z = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(x);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(y);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(z);

        X = x;
        Y = y;
        Z = z;
    }

    /// <inheritdoc/>
    public bool Equals(Dimensions3D other)
        => X == other.X && Y == other.Y && Z == other.Z;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is Dimensions3D other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(X, Y, Z);

    /// <inheritdoc/>
    public override string ToString()
        => Z == 1 ? (Y == 1 ? $"({X})" : $"({X}, {Y})") : $"({X}, {Y}, {Z})";

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Dimensions3D left, Dimensions3D right)
        => left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Dimensions3D left, Dimensions3D right)
        => !left.Equals(right);
}
