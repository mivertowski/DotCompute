// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Tensor core configuration.
    /// </summary>
    public sealed class TensorCoreConfig
    {
        public bool Enabled { get; set; }
        public DataType InputType { get; set; }
        public DataType OutputType { get; set; }
        public int TileSize { get; set; } = 16;
        public string DataType { get; set; } = "TF32";
        public string Precision { get; set; } = "High";
    }
}