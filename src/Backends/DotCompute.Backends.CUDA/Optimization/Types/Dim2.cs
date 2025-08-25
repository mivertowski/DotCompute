namespace DotCompute.Backends.CUDA.Optimization.Types
{
    /// <summary>
    /// Represents a 2-dimensional configuration for CUDA operations.
    /// </summary>
    public struct Dim2
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
        /// Initializes a new instance of the <see cref="Dim2"/> struct.
        /// </summary>
        /// <param name="x">The X dimension.</param>
        /// <param name="y">The Y dimension.</param>
        public Dim2(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Returns a string representation of the dimensions.
        /// </summary>
        /// <returns>A string in the format (X,Y).</returns>
        public override string ToString() => $"({X},{Y})";
    }
}