using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Hardware.RTX2000.Tests.Libraries;


/// <summary>
/// Integration tests for cuBLAS(CUDA Basic Linear Algebra Subroutines) on RTX 2000 Ada Generation.
/// Tests GPU-accelerated linear algebra operations and performance validation.
/// </summary>
[Trait("Category", "RTX2000")]
[Trait("Category", "cuBLAS")]
[Trait("Category", "LinearAlgebra")]
[Trait("Category", "RequiresGPU")]
public sealed class CuBLASIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
#pragma warning disable CA1823 // Unused field - Logger for future use
    private static readonly ILogger Logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    // Logger messages
    private static readonly Action<ILogger, string, Exception?> LogCuBlasOperation =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(4001), "cuBLAS operation: {Operation}");
#pragma warning restore CA1823
    private nint _cudaContext;
    private nint _cublasHandle;
    private bool _cublasInitialized;

    public CuBLASIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        InitializeCuBLAS();
    }

    private void InitializeCuBLAS()
    {
        try
        {
            // Initialize CUDA
            var result = CudaInit(0);
            if (result != 0)
            {
                _output.WriteLine($"CUDA initialization failed with error code: {result}");
                return;
            }

            // Create CUDA context
            result = CudaCtxCreate(ref _cudaContext, 0, 0);
            if (result != 0)
            {
                _output.WriteLine($"CUDA context creation failed with error code: {result}");
                return;
            }

            // Create cuBLAS handle
            var cublasResult = CublasCreate(ref _cublasHandle);
            if (cublasResult == CublasStatus.CUBLAS_STATUS_SUCCESS)
            {
                _cublasInitialized = true;
                _output.WriteLine("cuBLAS initialized successfully");

                // Get cuBLAS version
                var version = 0;
                _ = CublasGetVersion(_cublasHandle, ref version);
                _output.WriteLine($"cuBLAS version: {version}");
            }
            else
            {
                _output.WriteLine($"cuBLAS initialization failed with status: {cublasResult}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"cuBLAS initialization exception: {ex.Message}");
        }
    }

    [SkippableFact]
    public async Task CuBLASInitialization_ShouldSucceed()
    {
        Skip.IfNot(_cublasInitialized, "cuBLAS not available");

        _ = _cublasHandle.Should().NotBe(nint.Zero, "cuBLAS handle should be valid");

        // Test basic cuBLAS functionality
        var result = CublasSetPointerMode(_cublasHandle, CublasPointerMode.CUBLAS_POINTER_MODE_HOST);
        _ = result.Should().Be(CublasStatus.CUBLAS_STATUS_SUCCESS, "Setting pointer mode should succeed");

        _output.WriteLine("✓ cuBLAS initialization and basic operations validated");
        await Task.CompletedTask;
    }

    [SkippableFact]
    public async Task VectorDotProduct_ShouldComputeAccurately()
    {
        Skip.IfNot(_cublasInitialized, "cuBLAS not available");

        const int vectorSize = 1000000; // 1M elements
        const float expectedDot = 333332833333.0f; // Precalculated for test data

        nint d_x = nint.Zero, d_y = nint.Zero;

        try
        {
            // Allocate device memory
            var result = CudaMalloc(ref d_x, vectorSize * sizeof(float));
            Assert.Equal(0, result); // Memory allocation for vector x should succeed

            result = CudaMalloc(ref d_y, vectorSize * sizeof(float));
            Assert.Equal(0, result); // Memory allocation for vector y should succeed

            // Prepare test vectors
            var h_x = new float[vectorSize];
            var h_y = new float[vectorSize];

            for (var i = 0; i < vectorSize; i++)
            {
                h_x[i] = i + 1.0f;
                h_y[i] = (i + 1.0f) * 2.0f;
            }

            // Copy vectors to device
            var h_x_handle = GCHandle.Alloc(h_x, GCHandleType.Pinned);
            var h_y_handle = GCHandle.Alloc(h_y, GCHandleType.Pinned);

            try
            {
                result = CudaMemcpyHtoD(d_x, h_x_handle.AddrOfPinnedObject(), vectorSize * sizeof(float));
                Assert.Equal(0, result); // Copy vector x to device should succeed

                result = CudaMemcpyHtoD(d_y, h_y_handle.AddrOfPinnedObject(), vectorSize * sizeof(float));
                Assert.Equal(0, result); // Copy vector y to device should succeed
            }
            finally
            {
                h_x_handle.Free();
                h_y_handle.Free();
            }

            // Compute dot product using cuBLAS
            var dotResult = 0.0f;
            var sw = Stopwatch.StartNew();

            var cublasResult = CublasSdot(_cublasHandle, vectorSize, d_x, 1, d_y, 1, ref dotResult);

            sw.Stop();
            _ = cublasResult.Should().Be(CublasStatus.CUBLAS_STATUS_SUCCESS, "cuBLAS dot product should succeed");

            _output.WriteLine($"Dot product computed in {sw.ElapsedTicks * 1000000.0 / Stopwatch.Frequency:F1} μs");
            _output.WriteLine($"Result: {dotResult:E6}, Expected: {expectedDot:E6}");

            // Validate result(allow for floating-point precision)
            var relativeError = (float)Math.Abs((dotResult - expectedDot) / expectedDot);
            _ = relativeError.Should().BeLessThan(1e-5f, "Dot product should be accurate within floating-point precision");

            // Performance validation - should be much faster than CPU
            var elementsPerSecond = vectorSize / (sw.ElapsedTicks * 1000000.0 / Stopwatch.Frequency / 1e6);
            _output.WriteLine($"Performance: {elementsPerSecond:E2} elements/second");

            _ = elementsPerSecond.Should().BeGreaterThan(1e8, "cuBLAS should achieve high throughput");

            _output.WriteLine("✓ Vector dot product accuracy and performance validated");
        }
        finally
        {
            if (d_x != nint.Zero)
                _ = CudaFree(d_x);
            if (d_y != nint.Zero)
                _ = CudaFree(d_y);
        }

        await Task.CompletedTask;
    }

    [SkippableFact]
    public async Task MatrixVectorMultiplication_ShouldPerformCorrectly()
    {
        Skip.IfNot(_cublasInitialized, "cuBLAS not available");

        const int matrixRows = 2048;
        const int matrixCols = 2048;
        const float alpha = 1.0f;
        const float beta = 0.0f;

        nint d_A = nint.Zero, d_x = nint.Zero, d_y = nint.Zero;

        try
        {
            // Allocate device memory
            var result = CudaMalloc(ref d_A, matrixRows * matrixCols * sizeof(float));
            Assert.Equal(0, result); // Matrix A allocation should succeed;

            result = CudaMalloc(ref d_x, matrixCols * sizeof(float));
            Assert.Equal(0, result); // Vector x allocation should succeed;

            result = CudaMalloc(ref d_y, matrixRows * sizeof(float));
            Assert.Equal(0, result); // Vector y allocation should succeed;

            // Initialize matrix and vector
            var h_A = new float[matrixRows * matrixCols];
            var h_x = new float[matrixCols];
            var random = new Random(42);

            for (var i = 0; i < matrixRows * matrixCols; i++)
            {
                h_A[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            }

            for (var i = 0; i < matrixCols; i++)
            {
                h_x[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            }

            // Copy data to device
            var h_A_handle = GCHandle.Alloc(h_A, GCHandleType.Pinned);
            var h_x_handle = GCHandle.Alloc(h_x, GCHandleType.Pinned);

            try
            {
                result = CudaMemcpyHtoD(d_A, h_A_handle.AddrOfPinnedObject(), matrixRows * matrixCols * sizeof(float));
                Assert.Equal(0, result); // Matrix A copy should succeed;

                result = CudaMemcpyHtoD(d_x, h_x_handle.AddrOfPinnedObject(), matrixCols * sizeof(float));
                Assert.Equal(0, result); // Vector x copy should succeed;
            }
            finally
            {
                h_A_handle.Free();
                h_x_handle.Free();
            }

            // Perform matrix-vector multiplication: y = alpha * A * x + beta * y
            var sw = Stopwatch.StartNew();

            var alphaLocal = alpha;
            var betaLocal = beta;
            var cublasResult = CublasSgemv(
                _cublasHandle,
                CublasOperation.CUBLAS_OP_N, // No transpose
                matrixRows, matrixCols,
                ref alphaLocal,
                d_A, matrixRows, // lda = matrixRows(column-major)
                d_x, 1,
                ref betaLocal,
                d_y, 1);

            sw.Stop();
            _ = cublasResult.Should().Be(CublasStatus.CUBLAS_STATUS_SUCCESS, "Matrix-vector multiplication should succeed");

            _output.WriteLine($"Matrix-vector multiplication{matrixRows}x{matrixCols}) completed in {sw.ElapsedMilliseconds} ms");

            // Calculate performance metrics
            var totalOps = (long)matrixRows * matrixCols * 2; // FMA operations
            var gflops = totalOps / (sw.ElapsedMilliseconds / 1000.0) / 1e9;

            _output.WriteLine($"Performance: {gflops:F2} GFLOPS");

            // Validate performance - RTX 2000 Ada Gen should achieve substantial GFLOPS
            _ = gflops.Should().BeGreaterThan(100.0, "Matrix-vector multiplication should achieve high GFLOPS");

            // Verify result by copying back and spot-checking
            var h_y = new float[matrixRows];
            var h_y_handle = GCHandle.Alloc(h_y, GCHandleType.Pinned);

            try
            {
                result = CudaMemcpyDtoH(h_y_handle.AddrOfPinnedObject(), d_y, matrixRows * sizeof(float));
                Assert.Equal(0, result); // Result vector copy should succeed;

                // Spot-check a few results manually
                for (var row = 0; row < Math.Min(5, matrixRows); row++)
                {
                    var expectedValue = 0.0f;
                    for (var col = 0; col < matrixCols; col++)
                    {
                        expectedValue += h_A[row + col * matrixRows] * h_x[col]; // Column-major indexing
                    }

                    var error = (float)(Math.Abs(h_y[row] - expectedValue) / Math.Max(Math.Abs(expectedValue), 1e-6f));
                    _ = error.Should().BeLessThan(1e-4f, $"Result at row {row} should be accurate");
                }
            }
            finally
            {
                h_y_handle.Free();
            }

            _output.WriteLine("✓ Matrix-vector multiplication accuracy and performance validated");
        }
        finally
        {
            if (d_A != nint.Zero)
                _ = CudaFree(d_A);
            if (d_x != nint.Zero)
                _ = CudaFree(d_x);
            if (d_y != nint.Zero)
                _ = CudaFree(d_y);
        }

        await Task.CompletedTask;
    }

    [SkippableFact]
    public async Task MatrixMatrixMultiplication_ShouldAchieveHighPerformance()
    {
        Skip.IfNot(_cublasInitialized, "cuBLAS not available");

        const int matrixSize = 1024; // 1024x1024 matrices
        const float alpha = 1.0f;
        const float beta = 0.0f;

        nint d_A = nint.Zero, d_B = nint.Zero, d_C = nint.Zero;

        try
        {
            var matrixElements = matrixSize * matrixSize;
            var matrixSizeBytes = matrixElements * sizeof(float);

            // Allocate device memory
            var result = CudaMalloc(ref d_A, matrixSizeBytes);
            Assert.Equal(0, result); // Matrix A allocation should succeed;

            result = CudaMalloc(ref d_B, matrixSizeBytes);
            Assert.Equal(0, result); // Matrix B allocation should succeed;

            result = CudaMalloc(ref d_C, matrixSizeBytes);
            Assert.Equal(0, result); // Matrix C allocation should succeed;

            // Initialize matrices with random data
            var h_A = new float[matrixElements];
            var h_B = new float[matrixElements];
            var random = new Random(42);

            for (var i = 0; i < matrixElements; i++)
            {
                h_A[i] = (float)(random.NextDouble() * 2.0 - 1.0);
                h_B[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            }

            // Copy matrices to device
            var h_A_handle = GCHandle.Alloc(h_A, GCHandleType.Pinned);
            var h_B_handle = GCHandle.Alloc(h_B, GCHandleType.Pinned);

            try
            {
                result = CudaMemcpyHtoD(d_A, h_A_handle.AddrOfPinnedObject(), matrixSizeBytes);
                Assert.Equal(0, result); // Matrix A copy should succeed;

                result = CudaMemcpyHtoD(d_B, h_B_handle.AddrOfPinnedObject(), matrixSizeBytes);
                Assert.Equal(0, result); // Matrix B copy should succeed;
            }
            finally
            {
                h_A_handle.Free();
                h_B_handle.Free();
            }

            _output.WriteLine($"Performing {matrixSize}x{matrixSize} matrix multiplication...");

            // Warm-up run
            var alphaLocal = alpha;
            var betaLocal = beta;
            _ = CublasSgemm(_cublasHandle,
                CublasOperation.CUBLAS_OP_N, CublasOperation.CUBLAS_OP_N,
                matrixSize, matrixSize, matrixSize,
                ref alphaLocal,
                d_A, matrixSize,
                d_B, matrixSize,
                ref betaLocal,
                d_C, matrixSize);

            _ = CudaCtxSynchronize();

            // Timed run
            var sw = Stopwatch.StartNew();

            alphaLocal = alpha;
            betaLocal = beta;
            var cublasResult = CublasSgemm(
                _cublasHandle,
                CublasOperation.CUBLAS_OP_N, CublasOperation.CUBLAS_OP_N,
                matrixSize, matrixSize, matrixSize,
                ref alphaLocal,
                d_A, matrixSize,
                d_B, matrixSize,
                ref betaLocal,
                d_C, matrixSize);

            _ = CudaCtxSynchronize();
            sw.Stop();

            _ = cublasResult.Should().Be(CublasStatus.CUBLAS_STATUS_SUCCESS, "Matrix multiplication should succeed");

            // Calculate performance metrics
            var totalOps = (long)matrixSize * matrixSize * matrixSize * 2; // FMA operations
            var gflops = totalOps / (sw.ElapsedMilliseconds / 1000.0) / 1e9;
            var matrixSizeGB = matrixSizeBytes * 3 / (1024.0 * 1024.0 * 1024.0);
            var bandwidth = matrixSizeGB / (sw.ElapsedMilliseconds / 1000.0);

            _output.WriteLine($"Matrix multiplication performance:");
            _output.WriteLine($"  Execution time: {sw.ElapsedMilliseconds} ms");
            _output.WriteLine($"  Performance: {gflops:F2} GFLOPS");
            _output.WriteLine($"  Memory bandwidth: {bandwidth:F2} GB/s");
            _output.WriteLine($"  Matrix size: {matrixSize}x{matrixSize}");

            // Performance validation for RTX 2000 Ada Gen
            _ = gflops.Should().BeGreaterThan(1000.0, "Matrix multiplication should achieve >1 TFLOPS on RTX 2000 Ada Gen");
            _ = bandwidth.Should().BeGreaterThan(100.0, "Should achieve substantial memory bandwidth");

            // Validate a portion of the result
            var h_C = new float[matrixElements];
            var h_C_handle = GCHandle.Alloc(h_C, GCHandleType.Pinned);

            try
            {
                result = CudaMemcpyDtoH(h_C_handle.AddrOfPinnedObject(), d_C, matrixSizeBytes);
                Assert.Equal(0, result); // Result matrix copy should succeed;

                // Verify a few random elements using CPU computation
                for (var verification = 0; verification < 10; verification++)
                {
                    var row = random.Next(matrixSize);
                    var col = random.Next(matrixSize);

                    var expectedValue = 0.0f;
                    for (var k = 0; k < matrixSize; k++)
                    {
                        expectedValue += h_A[row + k * matrixSize] * h_B[k + col * matrixSize];
                    }

                    var actualValue = h_C[row + col * matrixSize];
                    var error = Math.Abs(actualValue - expectedValue) / Math.Max(Math.Abs(expectedValue), 1e-6f);

                    _ = error.Should().BeLessThan(1e-3f, $"Result at{row},{col}) should be accurate within tolerance");
                }
            }
            finally
            {
                h_C_handle.Free();
            }

            _output.WriteLine("✓ Matrix-matrix multiplication performance and accuracy validated");
        }
        finally
        {
            if (d_A != nint.Zero)
                _ = CudaFree(d_A);
            if (d_B != nint.Zero)
                _ = CudaFree(d_B);
            if (d_C != nint.Zero)
                _ = CudaFree(d_C);
        }

        await Task.CompletedTask;
    }

    [SkippableFact]
    public async Task BatchedOperations_ShouldScaleEfficiently()
    {
        Skip.IfNot(_cublasInitialized, "cuBLAS not available");

        const int batchSize = 100;
        const int matrixSize = 256;
        const float alpha = 1.0f;
        const float beta = 0.0f;

        var matrixElements = matrixSize * matrixSize;
        var matrixSizeBytes = matrixElements * sizeof(float);
        var totalSizeBytes = matrixSizeBytes * batchSize;

        nint d_A = nint.Zero, d_B = nint.Zero, d_C = nint.Zero;

        try
        {
            // Allocate device memory for batched matrices
            var result = CudaMalloc(ref d_A, totalSizeBytes);
            Assert.Equal(0, result); // Batched matrix A allocation should succeed;

            result = CudaMalloc(ref d_B, totalSizeBytes);
            Assert.Equal(0, result); // Batched matrix B allocation should succeed;

            result = CudaMalloc(ref d_C, totalSizeBytes);
            Assert.Equal(0, result); // Batched matrix C allocation should succeed;

            // Initialize batched matrices
            var h_A = new float[matrixElements * batchSize];
            var h_B = new float[matrixElements * batchSize];
            var random = new Random(42);

            for (var i = 0; i < h_A.Length; i++)
            {
                h_A[i] = (float)(random.NextDouble() * 2.0 - 1.0);
                h_B[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            }

            // Copy to device
            var h_A_handle = GCHandle.Alloc(h_A, GCHandleType.Pinned);
            var h_B_handle = GCHandle.Alloc(h_B, GCHandleType.Pinned);

            try
            {
                result = CudaMemcpyHtoD(d_A, h_A_handle.AddrOfPinnedObject(), totalSizeBytes);
                Assert.Equal(0, result); // Batched matrix A copy should succeed;

                result = CudaMemcpyHtoD(d_B, h_B_handle.AddrOfPinnedObject(), totalSizeBytes);
                Assert.Equal(0, result); // Batched matrix B copy should succeed;
            }
            finally
            {
                h_A_handle.Free();
                h_B_handle.Free();
            }

            _output.WriteLine($"Performing batched matrix multiplication: {batchSize} matrices of {matrixSize}x{matrixSize}");

            // Create arrays of device pointers for batched operation
            var h_A_array = new nint[batchSize];
            var h_B_array = new nint[batchSize];
            var h_C_array = new nint[batchSize];

            for (var i = 0; i < batchSize; i++)
            {
                h_A_array[i] = new nint(d_A.ToInt64() + i * matrixSizeBytes);
                h_B_array[i] = new nint(d_B.ToInt64() + i * matrixSizeBytes);
                h_C_array[i] = new nint(d_C.ToInt64() + i * matrixSizeBytes);
            }

            // Copy pointer arrays to device
            nint d_A_array = nint.Zero, d_B_array = nint.Zero, d_C_array = nint.Zero;

            result = CudaMalloc(ref d_A_array, batchSize * nint.Size);
            Assert.Equal(0, result); // Device pointer array A allocation should succeed;

            result = CudaMalloc(ref d_B_array, batchSize * nint.Size);
            Assert.Equal(0, result); // Device pointer array B allocation should succeed;

            result = CudaMalloc(ref d_C_array, batchSize * nint.Size);
            Assert.Equal(0, result); // Device pointer array C allocation should succeed;

            var h_A_array_handle = GCHandle.Alloc(h_A_array, GCHandleType.Pinned);
            var h_B_array_handle = GCHandle.Alloc(h_B_array, GCHandleType.Pinned);
            var h_C_array_handle = GCHandle.Alloc(h_C_array, GCHandleType.Pinned);

            try
            {
                result = CudaMemcpyHtoD(d_A_array, h_A_array_handle.AddrOfPinnedObject(), batchSize * nint.Size);
                Assert.Equal(0, result); // Device pointer array A copy should succeed;

                result = CudaMemcpyHtoD(d_B_array, h_B_array_handle.AddrOfPinnedObject(), batchSize * nint.Size);
                Assert.Equal(0, result); // Device pointer array B copy should succeed;

                result = CudaMemcpyHtoD(d_C_array, h_C_array_handle.AddrOfPinnedObject(), batchSize * nint.Size);
                Assert.Equal(0, result); // Device pointer array C copy should succeed;

                // Perform batched matrix multiplication
                var sw = Stopwatch.StartNew();

                var alphaLocal = alpha;
                var betaLocal = beta;
                var cublasResult = CublasSgemmBatched(
                    _cublasHandle,
                    CublasOperation.CUBLAS_OP_N, CublasOperation.CUBLAS_OP_N,
                    matrixSize, matrixSize, matrixSize,
                    ref alphaLocal,
                    d_A_array, matrixSize,
                    d_B_array, matrixSize,
                    ref betaLocal,
                    d_C_array, matrixSize,
                    batchSize);

                _ = CudaCtxSynchronize();
                sw.Stop();

                _ = cublasResult.Should().Be(CublasStatus.CUBLAS_STATUS_SUCCESS, "Batched matrix multiplication should succeed");

                // Calculate performance metrics
                var totalOps = (long)batchSize * matrixSize * matrixSize * matrixSize * 2;
                var gflops = totalOps / (sw.ElapsedMilliseconds / 1000.0) / 1e9;
                var avgTimePerMatrix = sw.ElapsedMilliseconds / (double)batchSize;

                _output.WriteLine($"Batched matrix multiplication performance:");
                _output.WriteLine($"  Total execution time: {sw.ElapsedMilliseconds} ms");
                _output.WriteLine($"  Average time per matrix: {avgTimePerMatrix:F3} ms");
                _output.WriteLine($"  Total performance: {gflops:F2} GFLOPS");
                _output.WriteLine($"  Batch size: {batchSize}");

                // Performance validation
                _ = gflops.Should().BeGreaterThan(500.0, "Batched operations should achieve high throughput");
                _ = avgTimePerMatrix.Should().BeLessThan(50.0, "Individual matrices should be processed quickly");

                _output.WriteLine("✓ Batched matrix operations performance validated");
            }
            finally
            {
                h_A_array_handle.Free();
                h_B_array_handle.Free();
                h_C_array_handle.Free();

                if (d_A_array != nint.Zero)
                    _ = CudaFree(d_A_array);
                if (d_B_array != nint.Zero)
                    _ = CudaFree(d_B_array);
                if (d_C_array != nint.Zero)
                    _ = CudaFree(d_C_array);
            }
        }
        finally
        {
            if (d_A != nint.Zero)
                _ = CudaFree(d_A);
            if (d_B != nint.Zero)
                _ = CudaFree(d_B);
            if (d_C != nint.Zero)
                _ = CudaFree(d_C);
        }

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_cublasHandle != nint.Zero)
        {
            _ = CublasDestroy(_cublasHandle);
            _cublasHandle = nint.Zero;
        }

        if (_cudaContext != nint.Zero)
        {
            _ = CudaCtxDestroy(_cudaContext);
            _cudaContext = nint.Zero;
        }

        _cublasInitialized = false;
        GC.SuppressFinalize(this);
    }

    #region Native Methods and Enums

    // CUDA Driver API
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuInit", ExactSpelling = true)]
    private static extern int CudaInit(uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuCtxCreate_v2", ExactSpelling = true)]
    private static extern int CudaCtxCreate(ref nint ctx, uint flags, int dev);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuCtxDestroy_v2", ExactSpelling = true)]
    private static extern int CudaCtxDestroy(nint ctx);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuCtxSynchronize", ExactSpelling = true)]
    private static extern int CudaCtxSynchronize();

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuMemAlloc_v2", ExactSpelling = true)]
    private static extern int CudaMalloc(ref nint dptr, long bytesize);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuMemFree_v2", ExactSpelling = true)]
    private static extern int CudaFree(nint dptr);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuMemcpyHtoD_v2", ExactSpelling = true)]
    private static extern int CudaMemcpyHtoD(nint dstDevice, nint srcHost, long byteCount);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("nvcuda", EntryPoint = "cuMemcpyDtoH_v2", ExactSpelling = true)]
    private static extern int CudaMemcpyDtoH(nint dstHost, nint srcDevice, long byteCount);

    // cuBLAS API
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("cublas64_12", EntryPoint = "cublasCreate_v2", ExactSpelling = true)]
    private static extern CublasStatus CublasCreate(ref nint handle);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("cublas64_12", EntryPoint = "cublasDestroy_v2", ExactSpelling = true)]
    private static extern CublasStatus CublasDestroy(nint handle);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("cublas64_12", EntryPoint = "cublasGetVersion_v2", ExactSpelling = true)]
    private static extern CublasStatus CublasGetVersion(nint handle, ref int version);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("cublas64_12", EntryPoint = "cublasSetPointerMode_v2", ExactSpelling = true)]
    private static extern CublasStatus CublasSetPointerMode(nint handle, CublasPointerMode mode);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("cublas64_12", EntryPoint = "cublasSdot_v2", ExactSpelling = true)]
    private static extern CublasStatus CublasSdot(nint handle, int n, nint x, int incx, nint y, int incy, ref float result);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("cublas64_12", EntryPoint = "cublasSgemv_v2", ExactSpelling = true)]
    private static extern CublasStatus CublasSgemv(nint handle, CublasOperation trans,
        int m, int n, ref float alpha, nint A, int lda, nint x, int incx,
        ref float beta, nint y, int incy);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("cublas64_12", EntryPoint = "cublasSgemm_v2", ExactSpelling = true)]
    private static extern CublasStatus CublasSgemm(nint handle, CublasOperation transa, CublasOperation transb,
        int m, int n, int k, ref float alpha, nint A, int lda, nint B, int ldb,
        ref float beta, nint C, int ldc);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("cublas64_12", EntryPoint = "cublasSgemmBatched", ExactSpelling = true)]
    private static extern CublasStatus CublasSgemmBatched(nint handle, CublasOperation transa, CublasOperation transb,
        int m, int n, int k, ref float alpha, nint Aarray, int lda, nint Barray, int ldb,
        ref float beta, nint Carray, int ldc, int batchCount);

    // cuBLAS enums
    internal enum CublasStatus
    {
        CUBLAS_STATUS_SUCCESS = 0,
        CUBLAS_STATUS_NOT_INITIALIZED = 1,
        CUBLAS_STATUS_ALLOC_FAILED = 3,
        CUBLAS_STATUS_INVALID_VALUE = 7,
        CUBLAS_STATUS_ARCH_MISMATCH = 8,
        CUBLAS_STATUS_MAPPING_ERROR = 11,
        CUBLAS_STATUS_EXECUTION_FAILED = 13,
        CUBLAS_STATUS_INTERNAL_ERROR = 14,
        CUBLAS_STATUS_NOT_SUPPORTED = 15,
        CUBLAS_STATUS_LICENSE_ERROR = 16
    }

    internal enum CublasOperation
    {
        CUBLAS_OP_N = 0,
        CUBLAS_OP_T = 1,
        CUBLAS_OP_C = 2
    }

    internal enum CublasPointerMode
    {
        CUBLAS_POINTER_MODE_HOST = 0,
        CUBLAS_POINTER_MODE_DEVICE = 1
    }

    #endregion
}

/// <summary>
/// Helper attribute to skip tests when conditions aren't met.
/// </summary>
internal sealed class SkippableFactAttribute : FactAttribute
{
    public override string? Skip { get; set; }
}

/// <summary>
/// Helper class for skipping tests conditionally.
/// </summary>
internal static class Skip
{
    public static void IfNot(bool condition, string reason)
    {
        if (!condition)
        {
            throw new SkipException(reason);
        }
    }
}

/// <summary>
/// Exception thrown to skip a test.
/// </summary>
internal sealed class SkipException : Exception
{
    public SkipException() : base() { }
    public SkipException(string reason) : base(reason) { }
    public SkipException(string message, Exception innerException) : base(message, innerException) { }
}
