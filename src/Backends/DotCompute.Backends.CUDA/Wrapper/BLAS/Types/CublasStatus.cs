// <copyright file="CublasStatus.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Wrapper.BLAS.Types;

/// <summary>
/// Represents cuBLAS library status codes.
/// These codes indicate the result of cuBLAS API calls.
/// </summary>
public enum CublasStatus
{
    /// <summary>
    /// The operation completed successfully.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The cuBLAS library was not initialized.
    /// Call cublasCreate() before using cuBLAS functions.
    /// </summary>
    NotInitialized = 1,

    /// <summary>
    /// Resource allocation failed inside the cuBLAS library.
    /// Usually indicates insufficient GPU memory.
    /// </summary>
    AllocFailed = 3,

    /// <summary>
    /// An unsupported value or parameter was passed to the function.
    /// Check function arguments for invalid values.
    /// </summary>
    InvalidValue = 7,

    /// <summary>
    /// The function requires a feature absent from the device architecture.
    /// Check GPU compute capability requirements.
    /// </summary>
    ArchMismatch = 8,

    /// <summary>
    /// An access to GPU memory space failed.
    /// May indicate corrupted memory or invalid pointers.
    /// </summary>
    MappingError = 11,

    /// <summary>
    /// The GPU program failed to execute.
    /// This is usually caused by a launch failure of the kernel.
    /// </summary>
    ExecutionFailed = 13,

    /// <summary>
    /// An internal cuBLAS operation failed.
    /// This error is usually caused by a cudaMemcpyAsync() failure.
    /// </summary>
    InternalError = 14,

    /// <summary>
    /// The functionality requested is not supported.
    /// Check cuBLAS version and GPU capabilities.
    /// </summary>
    NotSupported = 15,

    /// <summary>
    /// The functionality requested requires a license.
    /// Ensure proper licensing for commercial use.
    /// </summary>
    LicenseError = 16
}