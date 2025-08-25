namespace DotCompute.Backends.CUDA.Optimization.Types
{
    /// <summary>
    /// Represents a 3-dimensional configuration for CUDA grid and block dimensions.
    /// </summary>
    public struct Dim3
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
        /// Initializes a new instance of the <see cref="Dim3"/> struct.
        /// </summary>
        /// <param name="x">The X dimension.</param>
        /// <param name="y">The Y dimension.</param>
        /// <param name="z">The Z dimension.</param>
        public Dim3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Returns a string representation of the dimensions.
        /// </summary>
        /// <returns>A string in the format (X,Y,Z).</returns>
        public override string ToString() => $"({X},{Y},{Z})";
    }
}