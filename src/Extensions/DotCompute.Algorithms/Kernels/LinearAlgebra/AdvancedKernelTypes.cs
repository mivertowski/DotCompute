// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Kernels.LinearAlgebra;

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
    public long Size { get; set; }
    public float SparsityRatio { get; set; }
    public bool RequiresHighPrecision { get; set; }
    public bool IsSymmetric { get; set; }
    public bool IsPositiveDefinite { get; set; }
    public float ConditionNumber { get; set; }
    public string StorageFormat { get; set; } = "Dense"; // Dense, CSR, CSC, etc.
}

/// <summary>
/// Extended hardware information.
/// </summary>
public class HardwareInfo
{
    public long GlobalMemorySize { get; set; }
    public bool SupportsDoublePrecision { get; set; }
    public bool SupportsTensorCores { get; set; }
    public int? WarpSize { get; set; }
    public string Architecture { get; set; } = "Generic";
    public int ComputeCapability { get; set; }
}

/// <summary>
/// Kernel configuration for advanced operations.
/// </summary>
public class KernelConfiguration
{
    public string Precision { get; set; } = "single";
    public bool UseSpecializedSparseKernels { get; set; }
    public bool UseMemoryTiling { get; set; }
    public bool UseTensorCores { get; set; }
    public int OptimalBlockSize { get; set; } = 64;
    public int TileSize { get; set; } = 16;
}