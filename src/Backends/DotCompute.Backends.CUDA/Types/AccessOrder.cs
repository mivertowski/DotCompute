namespace DotCompute.Backends.CUDA.Analysis.Enums
{
    /// <summary>
    /// Defines the memory access order patterns for 2D data structures.
    /// </summary>
    public enum AccessOrder
    {
        /// <summary>
        /// Row-major order where consecutive elements in a row are stored contiguously.
        /// Optimal for accessing data row by row.
        /// </summary>
        RowMajor,

        /// <summary>
        /// Column-major order where consecutive elements in a column are stored contiguously.
        /// Optimal for accessing data column by column.
        /// </summary>
        ColumnMajor
    }
}