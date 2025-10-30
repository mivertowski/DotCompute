// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Tensor core configuration.
    /// </summary>
    public sealed class TensorCoreConfig
    {
        /// <summary>
        /// Gets or sets the enabled.
        /// </summary>
        /// <value>The enabled.</value>
        public bool Enabled { get; set; }
        /// <summary>
        /// Gets or sets the input type.
        /// </summary>
        /// <value>The input type.</value>
        public DataType InputType { get; set; }
        /// <summary>
        /// Gets or sets the output type.
        /// </summary>
        /// <value>The output type.</value>
        public DataType OutputType { get; set; }
        /// <summary>
        /// Gets or sets the tile size.
        /// </summary>
        /// <value>The tile size.</value>
        public int TileSize { get; set; } = 16;
        /// <summary>
        /// Gets or sets the data type.
        /// </summary>
        /// <value>The data type.</value>
        public string DataType { get; set; } = "TF32";
        /// <summary>
        /// Gets or sets the precision.
        /// </summary>
        /// <value>The precision.</value>
        public string Precision { get; set; } = "High";
    }
}
