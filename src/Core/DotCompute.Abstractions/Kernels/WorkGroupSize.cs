using System.Runtime.InteropServices;

namespace DotCompute.Abstractions.Kernels;

/// <summary>
/// Represents the work group size for kernel execution
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct WorkGroupSize : IEquatable<WorkGroupSize>
{
    /// <summary>
    /// The X dimension size
    /// </summary>
    public readonly int X;

    /// <summary>
    /// The Y dimension size
    /// </summary>
    public readonly int Y;

    /// <summary>
    /// The Z dimension size
    /// </summary>
    public readonly int Z;

    /// <summary>
    /// Creates a new WorkGroupSize with the specified dimensions
    /// </summary>
    /// <param name="x">The X dimension size</param>
    /// <param name="y">The Y dimension size</param>
    /// <param name="z">The Z dimension size</param>
    public WorkGroupSize(int x, int y = 1, int z = 1)
    {
        if (x <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "X dimension must be positive");
        }

        if (y <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Y dimension must be positive");
        }


        if (z <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(z), "Z dimension must be positive");
        }


        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// Gets the total number of work items in the work group
    /// </summary>
    public int TotalSize => X * Y * Z;

    /// <summary>
    /// Gets whether this is a 1D work group
    /// </summary>
    public bool Is1D => Y == 1 && Z == 1;

    /// <summary>
    /// Gets whether this is a 2D work group
    /// </summary>
    public bool Is2D => Z == 1 && !Is1D;

    /// <summary>
    /// Gets whether this is a 3D work group
    /// </summary>
    public bool Is3D => Z > 1;

    /// <summary>
    /// Gets the number of dimensions
    /// </summary>
    public int Dimensions
    {
        get
        {
            if (Z > 1)
            {
                return 3;
            }


            if (Y > 1)
            {
                return 2;
            }


            return 1;
        }
    }

    /// <summary>
    /// A 1D work group of size 1
    /// </summary>
    public static readonly WorkGroupSize One = new(1);

    /// <summary>
    /// A common 1D work group size of 256
    /// </summary>
    public static readonly WorkGroupSize Size256 = new(256);

    /// <summary>
    /// A common 1D work group size of 512
    /// </summary>
    public static readonly WorkGroupSize Size512 = new(512);

    /// <summary>
    /// A common 1D work group size of 1024
    /// </summary>
    public static readonly WorkGroupSize Size1024 = new(1024);

    /// <summary>
    /// A common 2D work group size of 16x16
    /// </summary>
    public static readonly WorkGroupSize Size16x16 = new(16, 16);

    /// <summary>
    /// A common 2D work group size of 32x32
    /// </summary>
    public static readonly WorkGroupSize Size32x32 = new(32, 32);

    /// <summary>
    /// Creates a 1D work group size
    /// </summary>
    /// <param name="x">The size in the X dimension</param>
    /// <returns>A new WorkGroupSize</returns>
    public static WorkGroupSize Size1D(int x) => new(x);

    /// <summary>
    /// Creates a 2D work group size
    /// </summary>
    /// <param name="x">The size in the X dimension</param>
    /// <param name="y">The size in the Y dimension</param>
    /// <returns>A new WorkGroupSize</returns>
    public static WorkGroupSize Size2D(int x, int y) => new(x, y);

    /// <summary>
    /// Creates a 3D work group size
    /// </summary>
    /// <param name="x">The size in the X dimension</param>
    /// <param name="y">The size in the Y dimension</param>
    /// <param name="z">The size in the Z dimension</param>
    /// <returns>A new WorkGroupSize</returns>
    public static WorkGroupSize Size3D(int x, int y, int z) => new(x, y, z);

    /// <summary>
    /// Calculates the optimal work group size for a given problem size
    /// </summary>
    /// <param name="problemSize">The total problem size</param>
    /// <param name="maxWorkGroupSize">The maximum work group size supported by the device</param>
    /// <returns>An optimal WorkGroupSize</returns>
    public static WorkGroupSize CalculateOptimal(int problemSize, int maxWorkGroupSize = 1024)
    {
        // Find the largest power of 2 that divides the problem size and is <= maxWorkGroupSize
        var size = Math.Min(maxWorkGroupSize, problemSize);

        // Round down to the nearest power of 2
        size = 1 << (int)Math.Floor(Math.Log2(size));

        // Ensure minimum size of 32 for good occupancy
        size = Math.Max(32, size);

        return new WorkGroupSize(size);
    }

    /// <summary>
    /// Calculates the optimal 2D work group size for given dimensions
    /// </summary>
    /// <param name="width">The width of the problem</param>
    /// <param name="height">The height of the problem</param>
    /// <param name="maxWorkGroupSize">The maximum work group size supported by the device</param>
    /// <returns>An optimal WorkGroupSize</returns>
    public static WorkGroupSize CalculateOptimal2D(int width, int height, int maxWorkGroupSize = 1024)
    {
        // Start with square dimensions
        var size = (int)Math.Sqrt(maxWorkGroupSize);

        // Find the largest square that fits
        while (size * size > maxWorkGroupSize)
        {
            size--;
        }

        // Adjust for actual dimensions

        var x = Math.Min(size, width);
        var y = Math.Min(size, height);

        // Ensure we don't exceed max work group size
        while (x * y > maxWorkGroupSize)
        {
            if (x > y)
            {
                x--;
            }
            else
            {
                y--;
            }

        }

        return new WorkGroupSize(x, y);
    }

    /// <summary>
    /// Checks if this work group size is valid for the given device constraints
    /// </summary>
    /// <param name="maxWorkGroupSize">Maximum work group size</param>
    /// <param name="maxWorkItemSizes">Maximum work item sizes for each dimension</param>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValidFor(int maxWorkGroupSize, int[] maxWorkItemSizes)
    {
        if (TotalSize > maxWorkGroupSize)
        {
            return false;
        }


        if (maxWorkItemSizes.Length >= 1 && X > maxWorkItemSizes[0])
        {
            return false;
        }


        if (maxWorkItemSizes.Length >= 2 && Y > maxWorkItemSizes[1])
        {
            return false;
        }


        if (maxWorkItemSizes.Length >= 3 && Z > maxWorkItemSizes[2])
        {
            return false;
        }


        return true;
    }

    /// <summary>
    /// Returns the work group size as an array
    /// </summary>
    /// <returns>An array containing [X, Y, Z]</returns>
    public int[] ToArray() => new[] { X, Y, Z };

    /// <summary>
    /// Equality comparison
    /// </summary>
    public bool Equals(WorkGroupSize other)
    {
        return X == other.X && Y == other.Y && Z == other.Z;
    }

    /// <summary>
    /// Equality comparison
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is WorkGroupSize other && Equals(other);
    }

    /// <summary>
    /// Gets the hash code
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }

    /// <summary>
    /// String representation
    /// </summary>
    public override string ToString()
    {
        if (Is1D)
        {
            return $"({X})";
        }


        if (Is2D)
        {
            return $"({X}, {Y})";
        }


        return $"({X}, {Y}, {Z})";
    }

    /// <summary>
    /// Equality operator
    /// </summary>
    public static bool operator ==(WorkGroupSize left, WorkGroupSize right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator
    /// </summary>
    public static bool operator !=(WorkGroupSize left, WorkGroupSize right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Implicit conversion from int to 1D WorkGroupSize
    /// </summary>
    public static implicit operator WorkGroupSize(int size) => new(size);

    /// <summary>
    /// Implicit conversion from (int, int) tuple to 2D WorkGroupSize
    /// </summary>
    public static implicit operator WorkGroupSize((int x, int y) size) => new(size.x, size.y);

    /// <summary>
    /// Implicit conversion from (int, int, int) tuple to 3D WorkGroupSize
    /// </summary>
    public static implicit operator WorkGroupSize((int x, int y, int z) size) => new(size.x, size.y, size.z);
}