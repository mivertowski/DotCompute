namespace DotCompute.Backends.CUDA.Optimization.Types
{
    /// <summary>
    /// Represents a 2-dimensional configuration for CUDA operations.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Dim2"/> struct.
    /// </remarks>
    /// <param name="x">The X dimension.</param>
    /// <param name="y">The Y dimension.</param>
    public struct Dim2(int x, int y) : IEquatable<Dim2>
    {
        /// <summary>
        /// Gets the X dimension.
        /// </summary>
        public int X { get; } = x;

        /// <summary>
        /// Gets the Y dimension.
        /// </summary>
        public int Y { get; } = y;

        /// <summary>
        /// Returns a string representation of the dimensions.
        /// </summary>
        /// <returns>A string in the format (X,Y).</returns>
        public override string ToString() => $"({X},{Y})";

        /// <summary>
        /// Determines whether the current instance is equal to another Dim2 instance.
        /// </summary>
        /// <param name="other">The Dim2 instance to compare with this instance.</param>
        /// <returns>true if the instances are equal; otherwise, false.</returns>
        public bool Equals(Dim2 other)
            => X == other.X && Y == other.Y;

        /// <summary>
        /// Determines whether the current instance is equal to a specified object.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns>true if obj is a Dim2 and is equal to this instance; otherwise, false.</returns>
        public override bool Equals(object? obj)
            => obj is Dim2 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A hash code for the current instance.</returns>
        public override int GetHashCode()
            => HashCode.Combine(X, Y);

        /// <summary>
        /// Determines whether two Dim2 instances are equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>true if the instances are equal; otherwise, false.</returns>
        public static bool operator ==(Dim2 left, Dim2 right)
            => left.Equals(right);

        /// <summary>
        /// Determines whether two Dim2 instances are not equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>true if the instances are not equal; otherwise, false.</returns>
        public static bool operator !=(Dim2 left, Dim2 right)
            => !left.Equals(right);
    }
}
