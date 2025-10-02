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
    public struct Dim2(int x, int y)
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
    }
}