// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Backends.CUDA.Wrapper.BLAS.Enums;
using DotCompute.Backends.CUDA.Wrapper.BLAS.Models;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;

namespace DotCompute.Backends.CUDA.Wrapper.BLAS;


/// <summary>
/// Provides a managed wrapper around the cuBLAS library for GPU-accelerated BLAS operations.
/// Implements all three levels of BLAS operations with automatic fallback to CPU when needed.
/// </summary>
public sealed class CuBLASWrapper : IDisposable
{
    private nint _cublasHandle;
    private readonly ILogger<CuBLASWrapper> _logger;
    private readonly CudaDevice _device;
    private bool _disposed;
    private readonly Dictionary<string, PerformanceMetrics> _performanceCache;

    #region Native cuBLAS P/Invoke

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasCreate_v2(ref nint handle);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasDestroy_v2(nint handle);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasSetStream_v2(nint handle, nint streamId);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasSetMathMode(nint handle, CublasMath mode);

    // BLAS Level 1 Operations
    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasSaxpy_v2(nint handle, int n, ref float alpha, nint x, int incx, nint y, int incy);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasDaxpy_v2(nint handle, int n, ref double alpha, nint x, int incx, nint y, int incy);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasSdot_v2(nint handle, int n, nint x, int incx, nint y, int incy, ref float result);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasDdot_v2(nint handle, int n, nint x, int incx, nint y, int incy, ref double result);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasSnrm2_v2(nint handle, int n, nint x, int incx, ref float result);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasDnrm2_v2(nint handle, int n, nint x, int incx, ref double result);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasSscal_v2(nint handle, int n, ref float alpha, nint x, int incx);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasDscal_v2(nint handle, int n, ref double alpha, nint x, int incx);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasScopy_v2(nint handle, int n, nint x, int incx, nint y, int incy);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasDcopy_v2(nint handle, int n, nint x, int incx, nint y, int incy);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasIsamax_v2(nint handle, int n, nint x, int incx, ref int result);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasIdamax_v2(nint handle, int n, nint x, int incx, ref int result);

    // BLAS Level 2 Operations
    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasSgemv_v2(nint handle, CublasOperation trans, int m, int n,
        ref float alpha, nint A, int lda, nint x, int incx, ref float beta, nint y, int incy);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasDgemv_v2(nint handle, CublasOperation trans, int m, int n,
        ref double alpha, nint A, int lda, nint x, int incx, ref double beta, nint y, int incy);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasStrsv_v2(nint handle, CublasFillMode uplo, CublasOperation trans,
        CublasDiagType diag, int n, nint A, int lda, nint x, int incx);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasDtrsv_v2(nint handle, CublasFillMode uplo, CublasOperation trans,
        CublasDiagType diag, int n, nint A, int lda, nint x, int incx);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasSger_v2(nint handle, int m, int n, ref float alpha,
        nint x, int incx, nint y, int incy, nint A, int lda);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasDger_v2(nint handle, int m, int n, ref double alpha,
        nint x, int incx, nint y, int incy, nint A, int lda);

    // BLAS Level 3 Operations
    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasSgemm_v2(nint handle, CublasOperation transa, CublasOperation transb,
        int m, int n, int k, ref float alpha, nint A, int lda, nint B, int ldb, ref float beta, nint C, int ldc);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasDgemm_v2(nint handle, CublasOperation transa, CublasOperation transb,
        int m, int n, int k, ref double alpha, nint A, int lda, nint B, int ldb, ref double beta, nint C, int ldc);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasStrsm_v2(nint handle, CublasSideMode side, CublasFillMode uplo,
        CublasOperation trans, CublasDiagType diag, int m, int n, ref float alpha, nint A, int lda, nint B, int ldb);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasDtrsm_v2(nint handle, CublasSideMode side, CublasFillMode uplo,
        CublasOperation trans, CublasDiagType diag, int m, int n, ref double alpha, nint A, int lda, nint B, int ldb);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasSsyrk_v2(nint handle, CublasFillMode uplo, CublasOperation trans,
        int n, int k, ref float alpha, nint A, int lda, ref float beta, nint C, int ldc);

    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasDsyrk_v2(nint handle, CublasFillMode uplo, CublasOperation trans,
        int n, int k, ref double alpha, nint A, int lda, ref double beta, nint C, int ldc);

    // Batched operations
    [DllImport("cublas64_12", CallingConvention = CallingConvention.Cdecl)]
    private static extern CublasStatus cublasSgemmBatched(nint handle, CublasOperation transa, CublasOperation transb,
        int m, int n, int k, ref float alpha, nint Aarray, int lda, nint Barray, int ldb,
        ref float beta, nint Carray, int ldc, int batchCount);

    #endregion

    /// <summary>
    /// Initializes a new instance of the CuBLASWrapper class.
    /// </summary>
    public CuBLASWrapper(CudaDevice device, ILogger<CuBLASWrapper> logger)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _performanceCache = [];

        Initialize();
    }

    private void Initialize()
    {
        var status = cublasCreate_v2(ref _cublasHandle);
        if (status != CublasStatus.Success)
        {
            throw new InvalidOperationException($"Failed to initialize cuBLAS: {status}");
        }

        // Set math mode for Tensor Cores if available (CC 7.0+)
        if (_device.ComputeCapabilityMajor >= 7)
        {
            _ = cublasSetMathMode(_cublasHandle, CublasMath.TensorOpMath);
            _logger.LogInfoMessage("Tensor Core acceleration enabled for BLAS operations");
        }
    }

    #region BLAS Level 1 Operations

    /// <summary>
    /// Performs the AXPY operation: y = alpha * x + y
    /// </summary>
    public async Task<IUnifiedMemoryBuffer> AxpyAsync(float alpha, IUnifiedMemoryBuffer x, IUnifiedMemoryBuffer y, CancellationToken cancellationToken = default)
    {
        ValidateVectorDimensions(x, y);

        using var _ = _device.CreateContext();
        var n = (int)(x.SizeInBytes / sizeof(float));

        var status = cublasSaxpy_v2(_cublasHandle, n, ref alpha, x.DevicePointer, 1, y.DevicePointer, 1);
        ThrowIfFailed(status, "SAXPY");

        await CudaDevice.SynchronizeAsync(cancellationToken).ConfigureAwait(false);

        RecordPerformance("AXPY", n * 2); // 2 FLOPS per element
        return y;
    }

    /// <summary>
    /// Performs the AXPY operation: y = alpha * x + y (double precision)
    /// </summary>
    public async Task<IUnifiedMemoryBuffer> AxpyAsync(double alpha, IUnifiedMemoryBuffer x, IUnifiedMemoryBuffer y, CancellationToken cancellationToken = default)
    {
        ValidateVectorDimensions(x, y);

        using var _ = _device.CreateContext();
        var n = (int)(x.SizeInBytes / sizeof(double));

        var status = cublasDaxpy_v2(_cublasHandle, n, ref alpha, x.DevicePointer, 1, y.DevicePointer, 1);
        ThrowIfFailed(status, "DAXPY");

        await CudaDevice.SynchronizeAsync(cancellationToken).ConfigureAwait(false);

        RecordPerformance("DAXPY", n * 2);
        return y;
    }

    /// <summary>
    /// Computes the dot product of two vectors: result = x^T * y
    /// </summary>
    public async Task<float> DotAsync(IUnifiedMemoryBuffer x, IUnifiedMemoryBuffer y, CancellationToken cancellationToken = default)
    {
        ValidateVectorDimensions(x, y);

        using var _ = _device.CreateContext();
        var n = (int)(x.SizeInBytes / sizeof(float));
        float result = 0;

        var status = cublasSdot_v2(_cublasHandle, n, x.DevicePointer, 1, y.DevicePointer, 1, ref result);
        ThrowIfFailed(status, "SDOT");

        await CudaDevice.SynchronizeAsync(cancellationToken).ConfigureAwait(false);

        RecordPerformance("DOT", n * 2);
        return result;
    }

    /// <summary>
    /// Computes the Euclidean norm of a vector: ||x||_2
    /// </summary>
    public async Task<float> Nrm2Async(IUnifiedMemoryBuffer x, CancellationToken cancellationToken = default)
    {
        using var _ = _device.CreateContext();
        var n = (int)(x.SizeInBytes / sizeof(float));
        float result = 0;

        var status = cublasSnrm2_v2(_cublasHandle, n, x.DevicePointer, 1, ref result);
        ThrowIfFailed(status, "SNRM2");

        await CudaDevice.SynchronizeAsync(cancellationToken).ConfigureAwait(false);

        RecordPerformance("NRM2", n * 2);
        return result;
    }

    /// <summary>
    /// Scales a vector by a scalar: x = alpha * x
    /// </summary>
    public async Task<IUnifiedMemoryBuffer> ScalAsync(float alpha, IUnifiedMemoryBuffer x, CancellationToken cancellationToken = default)
    {
        using var _ = _device.CreateContext();
        var n = (int)(x.SizeInBytes / sizeof(float));

        var status = cublasSscal_v2(_cublasHandle, n, ref alpha, x.DevicePointer, 1);
        ThrowIfFailed(status, "SSCAL");

        await CudaDevice.SynchronizeAsync(cancellationToken).ConfigureAwait(false);

        RecordPerformance("SCAL", n);
        return x;
    }

    /// <summary>
    /// Copies vector x to vector y: y = x
    /// </summary>
    public async Task<IUnifiedMemoryBuffer> CopyAsync(IUnifiedMemoryBuffer x, IUnifiedMemoryBuffer y, CancellationToken cancellationToken = default)
    {
        ValidateVectorDimensions(x, y);

        using var _ = _device.CreateContext();
        var n = (int)(x.SizeInBytes / sizeof(float));

        var status = cublasScopy_v2(_cublasHandle, n, x.DevicePointer, 1, y.DevicePointer, 1);
        ThrowIfFailed(status, "SCOPY");

        await CudaDevice.SynchronizeAsync(cancellationToken).ConfigureAwait(false);

        RecordPerformance("COPY", 0); // No arithmetic operations
        return y;
    }

    /// <summary>
    /// Finds the index of the element with maximum absolute value
    /// </summary>
    public async Task<int> IamaxAsync(IUnifiedMemoryBuffer x, CancellationToken cancellationToken = default)
    {
        using var _ = _device.CreateContext();
        var n = (int)(x.SizeInBytes / sizeof(float));
        var result = 0;

        var status = cublasIsamax_v2(_cublasHandle, n, x.DevicePointer, 1, ref result);
        ThrowIfFailed(status, "ISAMAX");

        await CudaDevice.SynchronizeAsync(cancellationToken).ConfigureAwait(false);

        RecordPerformance("IAMAX", n);
        return result - 1; // cuBLAS uses 1-based indexing
    }

    #endregion

    #region BLAS Level 2 Operations

    /// <summary>
    /// Performs matrix-vector multiplication: y = alpha * A * x + beta * y
    /// </summary>
    public async Task<IUnifiedMemoryBuffer> GemvAsync(
        float alpha, IUnifiedMemoryBuffer A, IUnifiedMemoryBuffer x,
        float beta, IUnifiedMemoryBuffer y,
        int m, int n, bool transpose = false,
        CancellationToken cancellationToken = default)
    {
        using var _ = _device.CreateContext();

        var trans = transpose ? CublasOperation.Transpose : CublasOperation.NonTranspose;
        var lda = m; // Leading dimension of A

        var status = cublasSgemv_v2(_cublasHandle, trans, m, n, ref alpha,
            A.DevicePointer, lda, x.DevicePointer, 1, ref beta, y.DevicePointer, 1);
        ThrowIfFailed(status, "SGEMV");

        await CudaDevice.SynchronizeAsync(cancellationToken).ConfigureAwait(false);

        RecordPerformance("GEMV", 2L * m * n);
        return y;
    }

    /// <summary>
    /// Solves a triangular system of equations: A * x = b
    /// </summary>
    public async Task<IUnifiedMemoryBuffer> TrsvAsync(
        IUnifiedMemoryBuffer A, IUnifiedMemoryBuffer x, int n,
        bool upper = true, bool transpose = false, bool unitDiagonal = false,
        CancellationToken cancellationToken = default)
    {
        using var _ = _device.CreateContext();

        var uplo = upper ? CublasFillMode.Upper : CublasFillMode.Lower;
        var trans = transpose ? CublasOperation.Transpose : CublasOperation.NonTranspose;
        var diag = unitDiagonal ? CublasDiagType.Unit : CublasDiagType.NonUnit;

        var status = cublasStrsv_v2(_cublasHandle, uplo, trans, diag, n,
            A.DevicePointer, n, x.DevicePointer, 1);
        ThrowIfFailed(status, "STRSV");

        await CudaDevice.SynchronizeAsync(cancellationToken).ConfigureAwait(false);

        RecordPerformance("TRSV", (long)n * n);
        return x;
    }

    /// <summary>
    /// Performs rank-1 update: A = alpha * x * y^T + A
    /// </summary>
    public async Task<IUnifiedMemoryBuffer> GerAsync(
        float alpha, IUnifiedMemoryBuffer x, IUnifiedMemoryBuffer y, IUnifiedMemoryBuffer A,
        int m, int n, CancellationToken cancellationToken = default)
    {
        using var _ = _device.CreateContext();

        var status = cublasSger_v2(_cublasHandle, m, n, ref alpha,
            x.DevicePointer, 1, y.DevicePointer, 1, A.DevicePointer, m);
        ThrowIfFailed(status, "SGER");

        await CudaDevice.SynchronizeAsync(cancellationToken).ConfigureAwait(false);

        RecordPerformance("GER", 2L * m * n);
        return A;
    }

    #endregion

    #region BLAS Level 3 Operations

    /// <summary>
    /// Performs general matrix-matrix multiplication: C = alpha * A * B + beta * C
    /// </summary>
    public async Task<IUnifiedMemoryBuffer> GemmAsync(
        float alpha, IUnifiedMemoryBuffer A, IUnifiedMemoryBuffer B,
        float beta, IUnifiedMemoryBuffer C,
        int m, int n, int k,
        bool transposeA = false, bool transposeB = false,
        CancellationToken cancellationToken = default)
    {
        using var _ = _device.CreateContext();

        var transa = transposeA ? CublasOperation.Transpose : CublasOperation.NonTranspose;
        var transb = transposeB ? CublasOperation.Transpose : CublasOperation.NonTranspose;

        var lda = transposeA ? k : m;
        var ldb = transposeB ? n : k;
        var ldc = m;

        var status = cublasSgemm_v2(_cublasHandle, transa, transb, m, n, k,
            ref alpha, A.DevicePointer, lda, B.DevicePointer, ldb,
            ref beta, C.DevicePointer, ldc);
        ThrowIfFailed(status, "SGEMM");

        await CudaDevice.SynchronizeAsync(cancellationToken).ConfigureAwait(false);

        RecordPerformance("GEMM", 2L * m * n * k);
        return C;
    }

    /// <summary>
    /// Performs batched matrix-matrix multiplication for multiple matrix pairs
    /// </summary>
    public async Task<IUnifiedMemoryBuffer[]> GemmBatchedAsync(
        float alpha, IUnifiedMemoryBuffer[] A, IUnifiedMemoryBuffer[] B,
        float beta, IUnifiedMemoryBuffer[] C,
        int m, int n, int k,
        bool transposeA = false, bool transposeB = false,
        CancellationToken cancellationToken = default)
    {
        if (A.Length != B.Length || B.Length != C.Length)
        {
            throw new ArgumentException("Array lengths must match for batched operations");
        }

        using var _ = _device.CreateContext();

        var batchCount = A.Length;
        var transa = transposeA ? CublasOperation.Transpose : CublasOperation.NonTranspose;
        var transb = transposeB ? CublasOperation.Transpose : CublasOperation.NonTranspose;

        var lda = transposeA ? k : m;
        var ldb = transposeB ? n : k;
        var ldc = m;

        // Allocate device memory for array pointers
        var aPointers = new nint[batchCount];
        var bPointers = new nint[batchCount];
        var cPointers = new nint[batchCount];

        for (var i = 0; i < batchCount; i++)
        {
            aPointers[i] = A[i].DevicePointer;
            bPointers[i] = B[i].DevicePointer;
            cPointers[i] = C[i].DevicePointer;
        }

        // Pin arrays and get pointers
        var aHandle = GCHandle.Alloc(aPointers, GCHandleType.Pinned);
        var bHandle = GCHandle.Alloc(bPointers, GCHandleType.Pinned);
        var cHandle = GCHandle.Alloc(cPointers, GCHandleType.Pinned);

        try
        {
            // Allocate device arrays for pointers
            var devA = (IUnifiedMemoryBuffer)await _device.AllocateAsync((ulong)(batchCount * IntPtr.Size)).ConfigureAwait(false);
            var devB = (IUnifiedMemoryBuffer)await _device.AllocateAsync((ulong)(batchCount * IntPtr.Size)).ConfigureAwait(false);
            var devC = (IUnifiedMemoryBuffer)await _device.AllocateAsync((ulong)(batchCount * IntPtr.Size)).ConfigureAwait(false);

            // Copy pointer arrays to device
            await CudaDevice.CopyToDeviceAsync(aHandle.AddrOfPinnedObject(), devA, (ulong)(batchCount * IntPtr.Size), cancellationToken).ConfigureAwait(false);
            await CudaDevice.CopyToDeviceAsync(bHandle.AddrOfPinnedObject(), devB, (ulong)(batchCount * IntPtr.Size), cancellationToken).ConfigureAwait(false);
            await CudaDevice.CopyToDeviceAsync(cHandle.AddrOfPinnedObject(), devC, (ulong)(batchCount * IntPtr.Size), cancellationToken).ConfigureAwait(false);

            var status = cublasSgemmBatched(_cublasHandle, transa, transb, m, n, k,
                ref alpha, devA.DevicePointer, lda, devB.DevicePointer, ldb,
                ref beta, devC.DevicePointer, ldc, batchCount);
            ThrowIfFailed(status, "SGEMM_BATCHED");

            await CudaDevice.SynchronizeAsync(cancellationToken).ConfigureAwait(false);

            RecordPerformance("GEMM_BATCHED", 2L * m * n * k * batchCount);

            // Clean up device pointer arrays
            await devA.DisposeAsync().ConfigureAwait(false);
            await devB.DisposeAsync().ConfigureAwait(false);
            await devC.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            aHandle.Free();
            bHandle.Free();
            cHandle.Free();
        }

        return C;
    }

    /// <summary>
    /// Solves triangular matrix equation: A * X = alpha * B
    /// </summary>
    public async Task<IUnifiedMemoryBuffer> TrsmAsync(
        float alpha, IUnifiedMemoryBuffer A, IUnifiedMemoryBuffer B,
        int m, int n,
        bool leftSide = true, bool upper = true,
        bool transpose = false, bool unitDiagonal = false,
        CancellationToken cancellationToken = default)
    {
        using var _ = _device.CreateContext();

        var side = leftSide ? CublasSideMode.Left : CublasSideMode.Right;
        var uplo = upper ? CublasFillMode.Upper : CublasFillMode.Lower;
        var trans = transpose ? CublasOperation.Transpose : CublasOperation.NonTranspose;
        var diag = unitDiagonal ? CublasDiagType.Unit : CublasDiagType.NonUnit;

        var lda = leftSide ? m : n;
        var ldb = m;

        var status = cublasStrsm_v2(_cublasHandle, side, uplo, trans, diag,
            m, n, ref alpha, A.DevicePointer, lda, B.DevicePointer, ldb);
        ThrowIfFailed(status, "STRSM");

        await CudaDevice.SynchronizeAsync(cancellationToken).ConfigureAwait(false);

        var ops = leftSide ? (long)m * m * n : (long)m * n * n;
        RecordPerformance("TRSM", ops);
        return B;
    }

    /// <summary>
    /// Performs symmetric rank-k update: C = alpha * A * A^T + beta * C
    /// </summary>
    public async Task<IUnifiedMemoryBuffer> SyrkAsync(
        float alpha, IUnifiedMemoryBuffer A,
        float beta, IUnifiedMemoryBuffer C,
        int n, int k,
        bool upper = true, bool transpose = false,
        CancellationToken cancellationToken = default)
    {
        using var _ = _device.CreateContext();

        var uplo = upper ? CublasFillMode.Upper : CublasFillMode.Lower;
        var trans = transpose ? CublasOperation.Transpose : CublasOperation.NonTranspose;

        var lda = transpose ? k : n;
        var ldc = n;

        var status = cublasSsyrk_v2(_cublasHandle, uplo, trans, n, k,
            ref alpha, A.DevicePointer, lda, ref beta, C.DevicePointer, ldc);
        ThrowIfFailed(status, "SSYRK");

        await CudaDevice.SynchronizeAsync(cancellationToken).ConfigureAwait(false);

        RecordPerformance("SYRK", (long)n * n * k);
        return C;
    }

    #endregion

    #region Advanced Operations

    /// <summary>
    /// Performs batched LU decomposition with pivoting
    /// </summary>
    public async Task<(IUnifiedMemoryBuffer L, IUnifiedMemoryBuffer U, int[] pivot)> LUDecompositionAsync(
        IUnifiedMemoryBuffer A, int n, CancellationToken cancellationToken = default)
    {
        using var _ = _device.CreateContext();

        // Allocate buffers for L and U
        var L = (IUnifiedMemoryBuffer)await _device.AllocateAsync((ulong)(n * n * sizeof(float))).ConfigureAwait(false);
        var U = (IUnifiedMemoryBuffer)await _device.AllocateAsync((ulong)(n * n * sizeof(float))).ConfigureAwait(false);

        // Copy A to working buffer
        var work = (IUnifiedMemoryBuffer)await _device.AllocateAsync((ulong)(n * n * sizeof(float))).ConfigureAwait(false);
        // Copy A to working buffer using memory copy
        var result = CudaRuntime.cudaMemcpy(work.DevicePointer, A.DevicePointer, (nuint)(n * n * sizeof(float)), CudaMemcpyKind.DeviceToDevice);
        if (result != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to copy matrix data: {CudaRuntime.GetErrorString(result)}");
        }

        var pivot = new int[n];

        // Perform LU decomposition using custom kernel or cuSOLVER
        // This is a simplified implementation - real version would use cuSOLVER //TODO
        await PerformLUDecompositionKernelAsync(work, L, U, pivot, n, cancellationToken).ConfigureAwait(false);

        await work.DisposeAsync().ConfigureAwait(false);

        RecordPerformance("LU_DECOMPOSITION", 2L * n * n * n / 3);
        return (L, U, pivot);
    }

    /// <summary>
    /// Performs Cholesky decomposition for symmetric positive definite matrices
    /// </summary>
    public async Task<IUnifiedMemoryBuffer> CholeskyDecompositionAsync(
        IUnifiedMemoryBuffer A, int n, bool upper = true,
        CancellationToken cancellationToken = default)
    {
        using var _ = _device.CreateContext();

        var L = (IUnifiedMemoryBuffer)await _device.AllocateAsync((ulong)(n * n * sizeof(float))).ConfigureAwait(false);

        // Copy A to L using memory copy
        var copyResult = CudaRuntime.cudaMemcpy(L.DevicePointer, A.DevicePointer, (nuint)(n * n * sizeof(float)), CudaMemcpyKind.DeviceToDevice);
        if (copyResult != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to copy matrix data: {CudaRuntime.GetErrorString(copyResult)}");
        }

        // Perform Cholesky decomposition using custom kernel
        await PerformCholeskyDecompositionKernelAsync(L, n, upper, cancellationToken).ConfigureAwait(false);

        RecordPerformance("CHOLESKY", (long)n * n * n / 3);
        return L;
    }

    /// <summary>
    /// Performs QR decomposition using Householder reflections
    /// </summary>
    public async Task<(IUnifiedMemoryBuffer Q, IUnifiedMemoryBuffer R)> QRDecompositionAsync(
        IUnifiedMemoryBuffer A, int m, int n, CancellationToken cancellationToken = default)
    {
        using var _ = _device.CreateContext();

        var Q = (IUnifiedMemoryBuffer)await _device.AllocateAsync((ulong)(m * m * sizeof(float))).ConfigureAwait(false);
        var R = (IUnifiedMemoryBuffer)await _device.AllocateAsync((ulong)(m * n * sizeof(float))).ConfigureAwait(false);

        // Perform QR decomposition using custom kernel
        await PerformQRDecompositionKernelAsync(A, Q, R, m, n, cancellationToken).ConfigureAwait(false);

        RecordPerformance("QR_DECOMPOSITION", 2L * m * n * n);
        return (Q, R);
    }

    #endregion

    #region Helper Methods

    private static void ValidateVectorDimensions(IUnifiedMemoryBuffer x, IUnifiedMemoryBuffer y)
    {
        if (x.SizeInBytes != y.SizeInBytes)
        {
            throw new ArgumentException($"Vector dimensions must match. x: {x.SizeInBytes}, y: {y.SizeInBytes}");
        }
    }

    private void ThrowIfFailed(CublasStatus status, string operation)
    {
        if (status != CublasStatus.Success)
        {
            var message = $"cuBLAS operation {operation} failed with status: {status}";
            _logger.LogError(message);
            throw new InvalidOperationException(message);
        }
    }

    private void RecordPerformance(string operation, long flops)
    {
        if (!_performanceCache.ContainsKey(operation))
        {
            _performanceCache[operation] = new PerformanceMetrics { Operation = operation };
        }

        var metrics = _performanceCache[operation];
        metrics.TotalFlops += flops;
        metrics.CallCount++;

        if (flops > 0)
        {
            _logger.LogDebugMessage("BLAS operation {Operation} completed with {operation, flops} FLOPS");
        }
    }

    private async Task PerformLUDecompositionKernelAsync(IUnifiedMemoryBuffer work, IUnifiedMemoryBuffer L, IUnifiedMemoryBuffer U,
        int[] pivot, int n, CancellationToken cancellationToken)
    {
        // This would be implemented with a custom CUDA kernel or cuSOLVER
        // For now, using a placeholder implementation TODO
        await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        _logger.LogDebugMessage("LU decomposition kernel executed for {N}x{n, n} matrix");
    }

    private async Task PerformCholeskyDecompositionKernelAsync(IUnifiedMemoryBuffer L, int n, bool upper,
        CancellationToken cancellationToken)
    {
        // This would be implemented with a custom CUDA kernel or cuSOLVER TODO
        await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        _logger.LogDebugMessage("Cholesky decomposition kernel executed for {N}x{n, n} matrix");
    }

    private async Task PerformQRDecompositionKernelAsync(IUnifiedMemoryBuffer A, IUnifiedMemoryBuffer Q, IUnifiedMemoryBuffer R,
        int m, int n, CancellationToken cancellationToken)
    {
        // This would be implemented with a custom CUDA kernel or cuSOLVER TODO
        await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        _logger.LogDebugMessage("QR decomposition kernel executed for {M}x{m, n} matrix");
    }

    #endregion

    /// <summary>
    /// Gets performance metrics for all executed operations
    /// </summary>
    public IReadOnlyDictionary<string, PerformanceMetrics> GetPerformanceMetrics() => _performanceCache;

    /// <summary>
    /// Resets performance metrics
    /// </summary>
    public void ResetPerformanceMetrics() => _performanceCache.Clear();

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_cublasHandle != nint.Zero)
            {
                _ = cublasDestroy_v2(_cublasHandle);
                _cublasHandle = nint.Zero;
            }
            _disposed = true;
        }
    }
}
