namespace DotCompute.Tests.Common.Fixtures;

/// <summary>
/// Provides common test data sets, kernel definitions, and expected results for DotCompute testing.
/// This fixture ensures consistent test data across all test projects and provides optimized
/// data generation for various computational scenarios.
/// </summary>
public static class TestDataFixture
{
    #region Common Data Sets

    /// <summary>
    /// Small data set for quick unit tests (1K elements).
    /// </summary>
    public static class Small
    {
        /// <summary>
        /// The size.
        /// </summary>
        public const int Size = 1024;


        /// <summary>
        /// Gets a small array of sequential float values.
        /// </summary>
        public static float[] SequentialFloats => GenerateSequentialFloats(Size);


        /// <summary>
        /// Gets a small array of random float values.
        /// </summary>
        public static float[] RandomFloats => GenerateRandomFloats(Size, 42);


        /// <summary>
        /// Gets a small array of sequential integer values.
        /// </summary>
        public static int[] SequentialInts => GenerateSequentialInts(Size);


        /// <summary>
        /// Gets a small array of random integer values.
        /// </summary>
        public static int[] RandomInts => GenerateRandomInts(Size, 0, 1000, 42);
    }

    /// <summary>
    /// Medium data set for integration tests (64K elements).
    /// </summary>
    public static class Medium
    {
        /// <summary>
        /// The size.
        /// </summary>
        public const int Size = 64 * 1024;


        /// <summary>
        /// Gets a medium array of sequential float values.
        /// </summary>
        public static float[] SequentialFloats => GenerateSequentialFloats(Size);


        /// <summary>
        /// Gets a medium array of random float values.
        /// </summary>
        public static float[] RandomFloats => GenerateRandomFloats(Size, 12345);


        /// <summary>
        /// Gets a medium array of sequential integer values.
        /// </summary>
        public static int[] SequentialInts => GenerateSequentialInts(Size);


        /// <summary>
        /// Gets a medium array of random integer values.
        /// </summary>
        public static int[] RandomInts => GenerateRandomInts(Size, 0, 10000, 12345);
    }

    /// <summary>
    /// Large data set for performance tests (16M elements).
    /// </summary>
    public static class Large
    {
        /// <summary>
        /// The size.
        /// </summary>
        public const int Size = 16 * 1024 * 1024;


        /// <summary>
        /// Gets a large array of sequential float values.
        /// </summary>
        public static float[] SequentialFloats => GenerateSequentialFloats(Size);


        /// <summary>
        /// Gets a large array of random float values.
        /// </summary>
        public static float[] RandomFloats => GenerateRandomFloats(Size, 67890);


        /// <summary>
        /// Gets a large array of sequential integer values.
        /// </summary>
        public static int[] SequentialInts => GenerateSequentialInts(Size);


        /// <summary>
        /// Gets a large array of random integer values.
        /// </summary>
        public static int[] RandomInts => GenerateRandomInts(Size, 0, 100000, 67890);
    }

    #endregion

    #region Matrix Data Sets

    /// <summary>
    /// Provides common matrix data sets for linear algebra operations.
    /// </summary>
    public static class Matrices
    {
        /// <summary>
        /// Small 32x32 matrix for unit tests.
        /// </summary>
        public static class Small32x32
        {
            /// <summary>
            /// The rows.
            /// </summary>
            public const int Rows = 32;
            /// <summary>
            /// The columns.
            /// </summary>
            public const int Columns = 32;
            /// <summary>
            /// The size.
            /// </summary>
            public const int Size = Rows * Columns;
            /// <summary>
            /// Gets or sets the identity.
            /// </summary>
            /// <value>The identity.</value>


            public static float[] Identity => GenerateIdentityMatrix(Rows, Columns);
            /// <summary>
            /// Gets or sets the random.
            /// </summary>
            /// <value>The random.</value>
            public static float[] Random => GenerateRandomFloats(Size, 111);
            /// <summary>
            /// Gets or sets the sequential.
            /// </summary>
            /// <value>The sequential.</value>
            public static float[] Sequential => GenerateSequentialFloats(Size);
        }

        /// <summary>
        /// Medium 256x256 matrix for integration tests.
        /// </summary>
        public static class Medium256x256
        {
            /// <summary>
            /// The rows.
            /// </summary>
            public const int Rows = 256;
            /// <summary>
            /// The columns.
            /// </summary>
            public const int Columns = 256;
            /// <summary>
            /// The size.
            /// </summary>
            public const int Size = Rows * Columns;
            /// <summary>
            /// Gets or sets the identity.
            /// </summary>
            /// <value>The identity.</value>


            public static float[] Identity => GenerateIdentityMatrix(Rows, Columns);
            /// <summary>
            /// Gets or sets the random.
            /// </summary>
            /// <value>The random.</value>
            public static float[] Random => GenerateRandomFloats(Size, 222);
            /// <summary>
            /// Gets or sets the sequential.
            /// </summary>
            /// <value>The sequential.</value>
            public static float[] Sequential => GenerateSequentialFloats(Size);
        }

        /// <summary>
        /// Large 1024x1024 matrix for performance tests.
        /// </summary>
        public static class Large1024x1024
        {
            /// <summary>
            /// The rows.
            /// </summary>
            public const int Rows = 1024;
            /// <summary>
            /// The columns.
            /// </summary>
            public const int Columns = 1024;
            /// <summary>
            /// The size.
            /// </summary>
            public const int Size = Rows * Columns;
            /// <summary>
            /// Gets or sets the identity.
            /// </summary>
            /// <value>The identity.</value>


            public static float[] Identity => GenerateIdentityMatrix(Rows, Columns);
            /// <summary>
            /// Gets or sets the random.
            /// </summary>
            /// <value>The random.</value>
            public static float[] Random => GenerateRandomFloats(Size, 333);
            /// <summary>
            /// Gets or sets the sequential.
            /// </summary>
            /// <value>The sequential.</value>
            public static float[] Sequential => GenerateSequentialFloats(Size);
        }
    }

    #endregion

    #region Kernel Definitions

    /// <summary>
    /// Common kernel source code for testing compilation and execution.
    /// </summary>
    public static class TestKernels
    {
        /// <summary>
        /// The vector add cuda.
        /// </summary>
        /// <summary>
        /// Simple vector addition kernel (CUDA C).
        /// </summary>
        public const string VectorAddCuda = @"
extern ""C"" __global__ void vectorAdd(float* a, float* b, float* result, int size)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < size)
    {
        result[idx] = a[idx] + b[idx];
    }
}";
        /// <summary>
        /// The vector add open c l.
        /// </summary>

        /// <summary>
        /// Simple vector addition kernel (OpenCL).
        /// </summary>
        public const string VectorAddOpenCL = @"
__kernel void vectorAdd(__global const float* a, 
                       __global const float* b, 
                       __global float* result, 
                       int size)
{
    int idx = get_global_id(0);
    if (idx < size)
    {
        result[idx] = a[idx] + b[idx];
    }
}";
        /// <summary>
        /// The matrix multiply cuda.
        /// </summary>

        /// <summary>
        /// Matrix multiplication kernel (CUDA C).
        /// </summary>
        public const string MatrixMultiplyCuda = @"
extern ""C"" __global__ void matrixMultiply(float* a, float* b, float* result, 
                                          int rowsA, int colsA, int colsB)
{
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    
    if (row < rowsA && col < colsB)
    {
        float sum = 0.0f;
        for (int k = 0; k < colsA; k++)
        {
            sum += a[row * colsA + k] * b[k * colsB + col];
        }
        result[row * colsB + col] = sum;
    }
}";
        /// <summary>
        /// The reduction sum cuda.
        /// </summary>

        /// <summary>
        /// Reduction sum kernel (CUDA C).
        /// </summary>
        public const string ReductionSumCuda = @"
extern ""C"" __global__ void reductionSum(float* input, float* output, int size)
{
    __shared__ float sdata[256];
    
    int tid = threadIdx.x;
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    
    sdata[tid] = (idx < size) ? input[idx] : 0;
    __syncthreads();
    
    for (int s = blockDim.x / 2; s > 0; s >>= 1)
    {
        if (tid < s)
        {
            sdata[tid] += sdata[tid + s];
        }
        __syncthreads();
    }
    
    if (tid == 0) output[blockIdx.x] = sdata[0];
}";
        /// <summary>
        /// The element wise operation cuda.
        /// </summary>

        /// <summary>
        /// Element-wise operation kernel (CUDA C).
        /// </summary>
        public const string ElementWiseOperationCuda = @"
extern ""C"" __global__ void elementWiseOperation(float* input, float* output, 
                                                float scalar, int size, int operation)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < size)
    {
        switch (operation)
        {
            case 0: output[idx] = input[idx] + scalar; break;
            case 1: output[idx] = input[idx] * scalar; break;
            case 2: output[idx] = input[idx] - scalar; break;
            case 3: output[idx] = input[idx] / scalar; break;
            default: output[idx] = input[idx]; break;
        }
    }
}";
        /// <summary>
        /// The complex computation cuda.
        /// </summary>

        /// <summary>
        /// Complex computation kernel for performance testing (CUDA C).
        /// </summary>
        public const string ComplexComputationCuda = @"
extern ""C"" __global__ void complexComputation(float* input, float* output, int size)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < size)
    {
        float x = input[idx];
        float result = 0.0f;
        
        // Perform multiple mathematical operations
        result = sinf(x) * cosf(x) + expf(x * 0.1f);
        result = sqrtf(fabsf(result)) + logf(1.0f + fabsf(x));
        result = powf(result, 1.5f) + tanf(x * 0.5f);
        
        output[idx] = result;
    }
}";
    }

    #endregion

    #region Expected Results

    /// <summary>
    /// Provides expected results for validation of common operations.
    /// </summary>
    public static class ExpectedResults
    {
        /// <summary>
        /// Calculates expected result for vector addition.
        /// </summary>
        /// <param name="a">First input vector.</param>
        /// <param name="b">Second input vector.</param>
        /// <returns>Expected result vector.</returns>
        public static float[] VectorAdd(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException("Vector lengths must match");

            var result = new float[a.Length];
            for (var i = 0; i < a.Length; i++)
            {
                result[i] = a[i] + b[i];
            }
            return result;
        }

        /// <summary>
        /// Calculates expected result for vector scalar multiplication.
        /// </summary>
        /// <param name="vector">Input vector.</param>
        /// <param name="scalar">Scalar value.</param>
        /// <returns>Expected result vector.</returns>
        public static float[] VectorScalarMultiply(float[] vector, float scalar)
        {
            var result = new float[vector.Length];
            for (var i = 0; i < vector.Length; i++)
            {
                result[i] = vector[i] * scalar;
            }
            return result;
        }

        /// <summary>
        /// Calculates expected result for matrix multiplication.
        /// </summary>
        /// <param name="a">First matrix (row-major order).</param>
        /// <param name="b">Second matrix (row-major order).</param>
        /// <param name="rowsA">Number of rows in matrix A.</param>
        /// <param name="colsA">Number of columns in matrix A.</param>
        /// <param name="colsB">Number of columns in matrix B.</param>
        /// <returns>Expected result matrix.</returns>
        public static float[] MatrixMultiply(float[] a, float[] b, int rowsA, int colsA, int colsB)
        {
            var result = new float[rowsA * colsB];


            for (var row = 0; row < rowsA; row++)
            {
                for (var col = 0; col < colsB; col++)
                {
                    var sum = 0.0f;
                    for (var k = 0; k < colsA; k++)
                    {
                        sum += a[row * colsA + k] * b[k * colsB + col];
                    }
                    result[row * colsB + col] = sum;
                }
            }


            return result;
        }

        /// <summary>
        /// Calculates expected result for array sum reduction.
        /// </summary>
        /// <param name="input">Input array.</param>
        /// <returns>Expected sum.</returns>
        public static float ReductionSum(float[] input) => input.Sum();

        /// <summary>
        /// Calculates expected result for element-wise operations.
        /// </summary>
        /// <param name="input">Input array.</param>
        /// <param name="scalar">Scalar operand.</param>
        /// <param name="operation">Operation type (0=add, 1=multiply, 2=subtract, 3=divide).</param>
        /// <returns>Expected result array.</returns>
        public static float[] ElementWiseOperation(float[] input, float scalar, int operation)
        {
            var result = new float[input.Length];


            for (var i = 0; i < input.Length; i++)
            {
                result[i] = operation switch
                {
                    0 => input[i] + scalar,
                    1 => input[i] * scalar,
                    2 => input[i] - scalar,
                    3 => input[i] / scalar,
                    _ => input[i]
                };
            }


            return result;
        }
    }

    #endregion

    #region Specialized Data Generators

    /// <summary>
    /// Generates test data for specific computational patterns.
    /// </summary>
    public static class SpecializedData
    {
        /// <summary>
        /// Generates data for numerical precision testing.
        /// </summary>
        /// <param name="size">Size of the data set.</param>
        /// <returns>Array with values that test floating-point precision.</returns>
        public static float[] PrecisionTestData(int size)
        {
            var data = new float[size];
            var random = new Random(9999);


            for (var i = 0; i < size; i++)
            {
                // Mix of small, large, and precision-sensitive values
                data[i] = i switch
                {
                    var x when x % 3 == 0 => (float)(random.NextDouble() * 1e-6), // Very small values
                    var x when x % 3 == 1 => (float)(random.NextDouble() * 1e6),  // Large values
                    _ => (float)(random.NextDouble() * 2.0 - 1.0)                 // Normal range
                };
            }


            return data;
        }

        /// <summary>
        /// Generates sparse data with mostly zero values.
        /// </summary>
        /// <param name="size">Size of the data set.</param>
        /// <param name="sparsityRatio">Ratio of non-zero elements (0.0 to 1.0).</param>
        /// <param name="seed">Random seed.</param>
        /// <returns>Sparse array.</returns>
        public static float[] SparseData(int size, float sparsityRatio = 0.1f, int seed = 7777)
        {
            var data = new float[size];
            var random = new Random(seed);


            for (var i = 0; i < size; i++)
            {
                if (random.NextSingle() < sparsityRatio)
                {
                    data[i] = (float)(random.NextDouble() * 100.0 - 50.0);
                }
                // else remains 0.0f
            }


            return data;
        }

        /// <summary>
        /// Generates data with specific patterns for algorithm validation.
        /// </summary>
        /// <param name="size">Size of the data set.</param>
        /// <param name="pattern">Pattern type.</param>
        /// <returns>Patterned data array.</returns>
        public static float[] PatternedData(int size, DataPattern pattern)
        {
            var data = new float[size];


            for (var i = 0; i < size; i++)
            {
                data[i] = pattern switch
                {
                    DataPattern.Sine => (float)Math.Sin(i * 2.0 * Math.PI / 64.0),
                    DataPattern.Cosine => (float)Math.Cos(i * 2.0 * Math.PI / 64.0),
                    DataPattern.Linear => i * 0.1f,
                    DataPattern.Quadratic => i * i * 0.001f,
                    DataPattern.Exponential => (float)Math.Exp(i * 0.01),
                    DataPattern.Logarithmic => (float)Math.Log(i + 1),
                    _ => 0.0f
                };
            }


            return data;
        }
    }
    /// <summary>
    /// An data pattern enumeration.
    /// </summary>

    /// <summary>
    /// Data pattern types for specialized test data generation.
    /// </summary>
    public enum DataPattern
    {
        Sine,
        Cosine,
        Linear,
        Quadratic,
        Exponential,
        Logarithmic
    }

    #endregion

    #region Private Helper Methods

    private static float[] GenerateSequentialFloats(int size, float start = 0.0f, float increment = 1.0f)
    {
        var data = new float[size];
        for (var i = 0; i < size; i++)
        {
            data[i] = start + i * increment;
        }
        return data;
    }

    private static int[] GenerateSequentialInts(int size, int start = 0, int increment = 1)
    {
        var data = new int[size];
        for (var i = 0; i < size; i++)
        {
            data[i] = start + i * increment;
        }
        return data;
    }

    private static float[] GenerateRandomFloats(int size, int seed, float min = -1.0f, float max = 1.0f)
    {
        var random = new Random(seed);
        var data = new float[size];
        var range = max - min;


        for (var i = 0; i < size; i++)
        {
            data[i] = min + (float)random.NextDouble() * range;
        }
        return data;
    }

    private static int[] GenerateRandomInts(int size, int min, int max, int seed)
    {
        var random = new Random(seed);
        var data = new int[size];


        for (var i = 0; i < size; i++)
        {
            data[i] = random.Next(min, max);
        }
        return data;
    }

    private static float[] GenerateIdentityMatrix(int rows, int cols)
    {
        var matrix = new float[rows * cols];
        for (var i = 0; i < Math.Min(rows, cols); i++)
        {
            matrix[i * cols + i] = 1.0f;
        }
        return matrix;
    }

    #endregion
}
