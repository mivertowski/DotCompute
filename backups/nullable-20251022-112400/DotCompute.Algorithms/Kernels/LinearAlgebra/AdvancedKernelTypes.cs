// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Kernels.LinearAlgebra;
/// <summary>
/// An advanced linear algebra operation enumeration.
/// </summary>

/// <summary>
/// Advanced linear algebra operations.
/// </summary>
public enum AdvancedLinearAlgebraOperation
{
    SparseMatrixVector,
    ConjugateGradient,
    PowerMethod,
    BlockedOperations,
    DoublePrecision,
    TensorCore,
    BidiagonalSVD,
    QRRank1Update,
    CuBLASGEMM,
    TensorCoreGEMM,
    BatchedGEMM,
    ParallelQR,
    QRRefinement,
    AtomicLU,
    ParallelCholesky,
    BlockedCholesky,
    DoublePrecisionMatrix,
    HalfPrecision,
    MixedPrecision,
    StreamOptimized,
    CoalescedTranspose
}

/// <summary>
/// Extended matrix properties for advanced optimizations.
/// </summary>
public class MatrixProperties
{
    /// <summary>
    /// Gets or sets the size.
    /// </summary>
    /// <value>The size.</value>
    public long Size { get; set; }
    /// <summary>
    /// Gets or sets the sparsity ratio.
    /// </summary>
    /// <value>The sparsity ratio.</value>
    public float SparsityRatio { get; set; }
    /// <summary>
    /// Gets or sets the requires high precision.
    /// </summary>
    /// <value>The requires high precision.</value>
    public bool RequiresHighPrecision { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether symmetric.
    /// </summary>
    /// <value>The is symmetric.</value>
    public bool IsSymmetric { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether positive definite.
    /// </summary>
    /// <value>The is positive definite.</value>
    public bool IsPositiveDefinite { get; set; }
    /// <summary>
    /// Gets or sets the condition number.
    /// </summary>
    /// <value>The condition number.</value>
    public float ConditionNumber { get; set; }
    /// <summary>
    /// Gets or sets the storage format.
    /// </summary>
    /// <value>The storage format.</value>
    public string StorageFormat { get; set; } = "Dense"; // Dense, CSR, CSC, etc.
}

/// <summary>
/// Extended hardware information.
/// </summary>
public class HardwareInfo
{
    /// <summary>
    /// Gets or sets the global memory size.
    /// </summary>
    /// <value>The global memory size.</value>
    public long GlobalMemorySize { get; set; }
    /// <summary>
    /// Gets or sets the supports double precision.
    /// </summary>
    /// <value>The supports double precision.</value>
    public bool SupportsDoublePrecision { get; set; }
    /// <summary>
    /// Gets or sets the supports tensor cores.
    /// </summary>
    /// <value>The supports tensor cores.</value>
    public bool SupportsTensorCores { get; set; }
    /// <summary>
    /// Gets or sets the warp size.
    /// </summary>
    /// <value>The warp size.</value>
    public int? WarpSize { get; set; }
    /// <summary>
    /// Gets or sets the architecture.
    /// </summary>
    /// <value>The architecture.</value>
    public string Architecture { get; set; } = "Generic";
    /// <summary>
    /// Gets or sets the compute capability.
    /// </summary>
    /// <value>The compute capability.</value>
    public int ComputeCapability { get; set; }
}

/// <summary>
/// Kernel configuration for advanced operations.
/// </summary>
public class KernelConfiguration
{
    /// <summary>
    /// Gets or sets the precision.
    /// </summary>
    /// <value>The precision.</value>
    public string Precision { get; set; } = "single";
    /// <summary>
    /// Gets or sets the use specialized sparse kernels.
    /// </summary>
    /// <value>The use specialized sparse kernels.</value>
    public bool UseSpecializedSparseKernels { get; set; }
    /// <summary>
    /// Gets or sets the use memory tiling.
    /// </summary>
    /// <value>The use memory tiling.</value>
    public bool UseMemoryTiling { get; set; }
    /// <summary>
    /// Gets or sets the use tensor cores.
    /// </summary>
    /// <value>The use tensor cores.</value>
    public bool UseTensorCores { get; set; }
    /// <summary>
    /// Gets or sets the optimal block size.
    /// </summary>
    /// <value>The optimal block size.</value>
    public int OptimalBlockSize { get; set; } = 64;
    /// <summary>
    /// Gets or sets the tile size.
    /// </summary>
    /// <value>The tile size.</value>
    public int TileSize { get; set; } = 16;
}