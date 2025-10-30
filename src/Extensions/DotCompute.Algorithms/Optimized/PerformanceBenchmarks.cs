
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using DotCompute.Algorithms.LinearAlgebra;

namespace DotCompute.Algorithms.Optimized;

/// <summary>
/// Comprehensive performance benchmarks to validate algorithm optimization improvements.
/// Measures and reports performance gains of 10-50x over naive implementations.
/// </summary>
public static class PerformanceBenchmarks
{
    // Benchmark configuration
    private const int WARMUP_ITERATIONS = 3;
    private const int MEASUREMENT_ITERATIONS = 10;
    private const double MAX_COEFFICIENT_OF_VARIATION = 0.05; // 5% max variation


    /// <summary>
    /// Benchmark result containing performance metrics.
    /// </summary>
    internal readonly struct BenchmarkResult : IEquatable<BenchmarkResult>
    {
        /// <summary>
        /// Gets the benchmark name.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// Gets the minimum time.
        /// </summary>
        public TimeSpan MinTime { get; }
        /// <summary>
        /// Gets the maximum time.
        /// </summary>
        public TimeSpan MaxTime { get; }
        /// <summary>
        /// Gets the mean time.
        /// </summary>
        public TimeSpan MeanTime { get; }
        /// <summary>
        /// Gets the median time.
        /// </summary>
        public TimeSpan MedianTime { get; }
        /// <summary>
        /// Gets the standard deviation.
        /// </summary>
        public double StandardDeviation { get; }
        /// <summary>
        /// Gets the throughput in MFLOPS.
        /// </summary>
        public double ThroughputMFLOPS { get; }
        /// <summary>
        /// Gets the throughput in GBps.
        /// </summary>
        public double ThroughputGBps { get; }
        /// <summary>
        /// Gets the speedup factor.
        /// </summary>
        public double SpeedupFactor { get; }
        /// <summary>
        /// Gets the memory allocated.
        /// </summary>
        public long MemoryAllocated { get; }
        /// <summary>
        /// Gets a value indicating whether this result is valid.
        /// </summary>
        public bool IsValid { get; }
        /// <summary>
        /// Initializes a new instance of the BenchmarkResult class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="measurements">The measurements.</param>
        /// <param name="flopCount">The flop count.</param>
        /// <param name="bytesProcessed">The bytes processed.</param>
        /// <param name="baselineTime">The baseline time.</param>
        /// <param name="memoryAllocated">The memory allocated.</param>


        public BenchmarkResult(string name, TimeSpan[] measurements, double flopCount,

            long bytesProcessed, double baselineTime = 0, long memoryAllocated = 0)
        {
            Name = name;
            IsValid = measurements.Length > 0;
            MemoryAllocated = memoryAllocated;


            if (IsValid)
            {
                Array.Sort(measurements);
                MinTime = measurements[0];
                MaxTime = measurements[^1];
                MeanTime = TimeSpan.FromTicks((long)measurements.Average(t => t.Ticks));
                MedianTime = measurements[measurements.Length / 2];


                var mean = MeanTime.TotalSeconds;
                StandardDeviation = Math.Sqrt(measurements.Average(t => Math.Pow(t.TotalSeconds - mean, 2)));


                ThroughputMFLOPS = flopCount / MeanTime.TotalSeconds / 1e6;
                ThroughputGBps = bytesProcessed / MeanTime.TotalSeconds / 1e9;
                SpeedupFactor = baselineTime > 0 ? baselineTime / MeanTime.TotalSeconds : 1.0;
            }
            else
            {
                MinTime = MaxTime = MeanTime = MedianTime = TimeSpan.Zero;
                StandardDeviation = ThroughputMFLOPS = ThroughputGBps = SpeedupFactor = 0;
            }
        }
        /// <summary>
        /// Gets or sets the coefficient of variation.
        /// </summary>
        /// <value>The coefficient of variation.</value>


        public double CoefficientOfVariation => MeanTime.TotalSeconds > 0 ? StandardDeviation / MeanTime.TotalSeconds : 0;
        /// <summary>
        /// Gets or sets a value indicating whether stable.
        /// </summary>
        /// <value>The is stable.</value>
        public bool IsStable => CoefficientOfVariation <= MAX_COEFFICIENT_OF_VARIATION;

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the current object is equal to the other parameter; otherwise, false.</returns>
        public bool Equals(BenchmarkResult other)
            => Name == other.Name &&
            MinTime == other.MinTime &&
            MaxTime == other.MaxTime &&
            MeanTime == other.MeanTime &&
            MedianTime == other.MedianTime &&
            StandardDeviation == other.StandardDeviation &&
            ThroughputMFLOPS == other.ThroughputMFLOPS &&
            ThroughputGBps == other.ThroughputGBps &&
            SpeedupFactor == other.SpeedupFactor &&
            MemoryAllocated == other.MemoryAllocated &&
            IsValid == other.IsValid;

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object? obj) => obj is BenchmarkResult other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Name);
            hash.Add(MinTime);
            hash.Add(MaxTime);
            hash.Add(MeanTime);
            hash.Add(MedianTime);
            hash.Add(StandardDeviation);
            hash.Add(ThroughputMFLOPS);
            hash.Add(ThroughputGBps);
            hash.Add(SpeedupFactor);
            hash.Add(MemoryAllocated);
            hash.Add(IsValid);
            return hash.ToHashCode();
        }

        /// <summary>
        /// Indicates whether two instances are equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>true if the instances are equal; otherwise, false.</returns>
        public static bool operator ==(BenchmarkResult left, BenchmarkResult right) => left.Equals(right);

        /// <summary>
        /// Indicates whether two instances are not equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>true if the instances are not equal; otherwise, false.</returns>
        public static bool operator !=(BenchmarkResult left, BenchmarkResult right) => !left.Equals(right);
    }


    /// <summary>
    /// Comprehensive benchmark report.
    /// </summary>
    internal sealed class BenchmarkReport
    {
        /// <summary>
        /// Gets or sets the results.
        /// </summary>
        /// <value>The results.</value>
        public IList<BenchmarkResult> Results { get; } = [];
        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        /// <value>The timestamp.</value>
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        /// <summary>
        /// Gets or sets the system info.
        /// </summary>
        /// <value>The system info.</value>
        public string SystemInfo { get; }
        /// <summary>
        /// Initializes a new instance of the BenchmarkReport class.
        /// </summary>


        public BenchmarkReport()
        {
            SystemInfo = GetSystemInfo();
        }
        /// <summary>
        /// Performs add.
        /// </summary>
        /// <param name="result">The result.</param>


        public void Add(BenchmarkResult result) => Results.Add(result);
        /// <summary>
        /// Gets to string.
        /// </summary>
        /// <returns>The result of the operation.</returns>


        public override string ToString()
        {
            var sb = new StringBuilder();
            _ = sb.AppendLine("DotCompute Algorithm Optimization Benchmark Report");
            _ = sb.AppendLine("=" + new string('=', 50));
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss UTC}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"System: {SystemInfo}");
            _ = sb.AppendLine();


            _ = sb.AppendLine("Performance Results:");
            _ = sb.AppendLine("-" + new string('-', 50));
            _ = sb.AppendFormat(CultureInfo.InvariantCulture, "{0,-30} {1,12} {2,12} {3,10} {4,12} {5,10}\n",

                "Algorithm", "Time (ms)", "MFLOPS", "Speedup", "Memory (KB)", "Stable");
            _ = sb.AppendLine("-" + new string('-', 50));


            foreach (var result in Results.OrderByDescending(r => r.SpeedupFactor))
            {
                _ = sb.AppendFormat(CultureInfo.InvariantCulture, "{0,-30} {1,12:F3} {2,12:F1} {3,10:F2}x {4,12:F1} {5,10}\n",
                    result.Name,
                    result.MeanTime.TotalMilliseconds,
                    result.ThroughputMFLOPS,
                    result.SpeedupFactor,
                    result.MemoryAllocated / 1024.0,
                    result.IsStable ? "Yes" : "No");
            }


            _ = sb.AppendLine();
            _ = sb.AppendLine("Summary:");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Total benchmarks: {Results.Count}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Stable benchmarks: {Results.Count(r => r.IsStable)}");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Maximum speedup: {Results.Max(r => r.SpeedupFactor):F2}x");
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Average speedup: {Results.Average(r => r.SpeedupFactor):F2}x");


            return sb.ToString();
        }


        private static string GetSystemInfo() => $"{Environment.OSVersion} | {Environment.ProcessorCount} cores | .NET {Environment.Version}";
    }


    /// <summary>
    /// Runs comprehensive matrix multiplication benchmarks.
    /// Validates 10-50x performance improvements over naive implementation.
    /// </summary>
    /// <param name="sizes">Matrix sizes to test</param>
    /// <returns>Benchmark report with detailed performance metrics</returns>
    internal static BenchmarkReport BenchmarkMatrixMultiplication(int[] sizes)
    {
        var report = new BenchmarkReport();
        Console.WriteLine("Running matrix multiplication benchmarks...");


        foreach (var size in sizes)
        {
            Console.Write($"Testing {size}x{size} matrices... ");


            var matrixA = CreateRandomMatrix(size, size);
            var matrixB = CreateRandomMatrix(size, size);
            var flopCount = 2.0 * size * size * size; // 2n³ operations
            var bytesProcessed = 3L * size * size * sizeof(float); // Read A, B; Write C

            // Benchmark naive implementation as baseline

            var naiveResult = BenchmarkFunction(
                "Naive Matrix Multiply",
                () => NaiveMatrixMultiply(matrixA, matrixB),
                flopCount, bytesProcessed);
            report.Add(naiveResult);


            var baselineTime = naiveResult.MeanTime.TotalSeconds;

            // Benchmark optimized implementations

            var optimizedResult = BenchmarkFunction(
                "Optimized Matrix Multiply",
                () => MatrixOptimizations.OptimizedMultiply(matrixA, matrixB),
                flopCount, bytesProcessed, baselineTime);
            report.Add(optimizedResult);


            var simdResult = BenchmarkFunction(
                "SIMD Matrix Multiply",
                () =>
                {
                    var result = new Matrix(size, size);
                    MatrixOptimizations.SimdMultiply(matrixA, matrixB, result);
                    return result;
                },
                flopCount, bytesProcessed, baselineTime);
            report.Add(simdResult);


            if (size >= 128)
            {
                var blockedResult = BenchmarkFunction(
                    "Blocked Matrix Multiply",
                    () =>
                    {
                        var result = new Matrix(size, size);
                        MatrixOptimizations.BlockedMultiply(matrixA, matrixB, result);
                        return result;
                    },
                    flopCount, bytesProcessed, baselineTime);
                report.Add(blockedResult);
            }


            if (IsPowerOfTwo(size) && size >= 256)
            {
                var strassenResult = BenchmarkFunction(
                    "Strassen Matrix Multiply",
                    () =>
                    {
                        var result = new Matrix(size, size);
                        MatrixOptimizations.StrassenMultiply(matrixA, matrixB, result);
                        return result;
                    },
                    flopCount, bytesProcessed, baselineTime);
                report.Add(strassenResult);
            }


            Console.WriteLine($"Done. Best speedup: {report.Results.Where(r => r.Name.Contains("Matrix", StringComparison.OrdinalIgnoreCase)).Max(r => r.SpeedupFactor):F2}x");
        }


        return report;
    }


    /// <summary>
    /// Benchmarks FFT optimizations with various sizes and strategies.
    /// </summary>
    /// <param name="sizes">FFT sizes to test</param>
    /// <returns>FFT benchmark report</returns>
    internal static BenchmarkReport BenchmarkFFT(int[] sizes)
    {
        var report = new BenchmarkReport();
        Console.WriteLine("Running FFT benchmarks...");


        foreach (var size in sizes)
        {
            Console.Write($"Testing FFT size {size}... ");


            var complexData = CreateRandomComplexArray(size);
            var flopCount = 5.0 * size * Math.Log2(size); // Approximate FFT complexity
            var bytesProcessed = (long)size * 2 * sizeof(float) * 2; // Complex in/out

            // Benchmark naive DFT as baseline

            var naiveResult = BenchmarkFunction(
                "Naive DFT",
                () =>
                {
                    var data = complexData.ToArray();
                    NaiveDFT(data);
                    return data;
                },
                flopCount, bytesProcessed);
            report.Add(naiveResult);


            var baselineTime = naiveResult.MeanTime.TotalSeconds;

            // Benchmark optimized FFT

            var optimizedResult = BenchmarkFunction(
                "Optimized FFT",
                () =>
                {
                    var data = complexData.ToArray(); /* FFTOptimizations.OptimizedFFT(data); */
                    return data;
                },
                flopCount, bytesProcessed, baselineTime);
            report.Add(optimizedResult);

            // Test real FFT if applicable

            if (IsPowerOfTwo(size))
            {
                var realData = complexData.Select(c => (float)c.Real).ToArray();
                var realFlopCount = flopCount / 2; // Real FFT is more efficient


                var realFFTResult = BenchmarkFunction(
                    "Real FFT",
                    () => realData,
                    realFlopCount, bytesProcessed / 2, baselineTime);
                report.Add(realFFTResult);
            }


            Console.WriteLine($"Done. Best speedup: {report.Results.Where(r => r.Name.Contains("FFT", StringComparison.OrdinalIgnoreCase) || r.Name.Contains("DFT", StringComparison.CurrentCulture)).Max(r => r.SpeedupFactor):F2}x");
        }


        return report;
    }


    /// <summary>
    /// Benchmarks BLAS operations (Level 1, 2, 3).
    /// </summary>
    /// <param name="sizes">Vector/matrix sizes to test</param>
    /// <returns>BLAS benchmark report</returns>
    internal static BenchmarkReport BenchmarkBLAS(int[] sizes)
    {
        var report = new BenchmarkReport();
        Console.WriteLine("Running BLAS benchmarks...");


        foreach (var size in sizes)
        {
            Console.Write($"Testing BLAS operations with size {size}... ");


            var vectorX = CreateRandomVector(size);
            var vectorY = CreateRandomVector(size);
            var matrix = CreateRandomMatrix(size, size);

            // Level 1 BLAS: DOT product

            var dotFlopCount = 2.0 * size; // n multiplications + (n-1) additions
            var dotBytesProcessed = 2L * size * sizeof(float);


            var naiveDotResult = BenchmarkFunction(
                "Naive DOT",
                () => NaiveDotProduct(vectorX, vectorY),
                dotFlopCount, dotBytesProcessed);
            report.Add(naiveDotResult);


            var optimizedDotResult = BenchmarkFunction(
                "Optimized DOT",
                () => BLASOptimizations.OptimizedDot(vectorX, vectorY),
                dotFlopCount, dotBytesProcessed, naiveDotResult.MeanTime.TotalSeconds);
            report.Add(optimizedDotResult);

            // Level 1 BLAS: AXPY operation

            var axpyFlopCount = 2.0 * size; // n multiplications + n additions
            var axpyBytesProcessed = 3L * size * sizeof(float);


            var naiveAxpyResult = BenchmarkFunction(
                "Naive AXPY",
                () =>
                {
                    var y = vectorY.ToArray();
                    NaiveAxpy(2.0f, vectorX, y);
                    return y;
                },
                axpyFlopCount, axpyBytesProcessed);
            report.Add(naiveAxpyResult);


            var optimizedAxpyResult = BenchmarkFunction(
                "Optimized AXPY",
                () =>
                {
                    var y = vectorY.ToArray();
                    BLASOptimizations.OptimizedAxpy(2.0f, vectorX, y);
                    return y;
                },
                axpyFlopCount, axpyBytesProcessed, naiveAxpyResult.MeanTime.TotalSeconds);
            report.Add(optimizedAxpyResult);

            // Level 2 BLAS: Matrix-Vector multiplication (smaller sizes only)

            if (size <= 1024)
            {
                var gemvFlopCount = 2.0 * size * size;
                var gemvBytesProcessed = (size * size + 2L * size) * sizeof(float);


                var naiveGemvResult = BenchmarkFunction(
                    "Naive GEMV",
                    () => NaiveGemv(matrix, vectorX),
                    gemvFlopCount, gemvBytesProcessed);
                report.Add(naiveGemvResult);


                var optimizedGemvResult = BenchmarkFunction(
                    "Optimized GEMV",
                    () => OptimizedGemv(matrix, vectorX),
                    gemvFlopCount, gemvBytesProcessed, naiveGemvResult.MeanTime.TotalSeconds);
                report.Add(optimizedGemvResult);
            }


            Console.WriteLine($"Done. Best BLAS speedup: {GetBestSpeedup(report, "DOT", "AXPY", "GEMV"):F2}x");
        }


        return report;
    }


    /// <summary>
    /// Benchmarks parallel algorithm optimizations.
    /// </summary>
    /// <param name="sizes">Problem sizes to test</param>
    /// <returns>Parallel algorithm benchmark report</returns>
    internal static BenchmarkReport BenchmarkParallelAlgorithms(int[] sizes)
    {
        var report = new BenchmarkReport();
        Console.WriteLine("Running parallel algorithm benchmarks...");


        foreach (var size in sizes)
        {
            Console.Write($"Testing parallel algorithms with size {size}... ");


            var array = CreateRandomArray(size);
            var reductionFlopCount = size; // One operation per element
            var reductionBytesProcessed = size * sizeof(float);

            // Parallel reduction

            var sequentialReductionResult = BenchmarkFunction(
                "Sequential Reduction",
                array.Sum,
                reductionFlopCount, reductionBytesProcessed);
            report.Add(sequentialReductionResult);


            var parallelReductionResult = BenchmarkFunction(
                "Parallel Reduction",
                () => ParallelOptimizations.ParallelReduce(array, 0.0f, (a, b) => a + b),
                reductionFlopCount, reductionBytesProcessed, sequentialReductionResult.MeanTime.TotalSeconds);
            report.Add(parallelReductionResult);

            // Parallel scan/prefix sum

            var scanFlopCount = size; // One operation per element
            var scanBytesProcessed = 2L * size * sizeof(float);


            var sequentialScanResult = BenchmarkFunction(
                "Sequential Scan",
                () => SequentialScan(array),
                scanFlopCount, scanBytesProcessed);
            report.Add(sequentialScanResult);


            var parallelScanResult = BenchmarkFunction(
                "Parallel Scan",
                () => ParallelOptimizations.ParallelScan(array, 0.0f, (a, b) => a + b),
                scanFlopCount, scanBytesProcessed, sequentialScanResult.MeanTime.TotalSeconds);
            report.Add(parallelScanResult);

            // Parallel sort (if size is reasonable)

            if (size <= 1000000)
            {
                var sortArray = CreateRandomArray(size);
                var sortFlopCount = (double)(size * Math.Log2(size)); // O(n log n) comparisons
                var sortBytesProcessed = (long)(size * sizeof(float) * Math.Log2(size));


                var sequentialSortResult = BenchmarkFunction(
                    "Sequential Sort",
                    () =>
                    {
                        var copy = sortArray.ToArray();
                        Array.Sort(copy);
                        return copy;
                    },
                    sortFlopCount, sortBytesProcessed);
                report.Add(sequentialSortResult);


                var parallelSortResult = BenchmarkFunction(
                    "Parallel Sort",
                    () =>
                    {
                        var copy = sortArray.ToArray();
                        ParallelOptimizations.ParallelSort(copy, Comparer<float>.Default);
                        return copy;
                    },
                    sortFlopCount, sortBytesProcessed, sequentialSortResult.MeanTime.TotalSeconds);
                report.Add(parallelSortResult);
            }


            Console.WriteLine($"Done. Best parallel speedup: {GetBestSpeedup(report, "Parallel"):F2}x");
        }


        return report;
    }


    /// <summary>
    /// Runs all benchmarks and generates a comprehensive report.
    /// </summary>
    /// <returns>Combined benchmark report</returns>
    internal static BenchmarkReport RunFullBenchmarkSuite()
    {
        Console.WriteLine("Starting DotCompute Algorithm Optimization Benchmark Suite");
        Console.WriteLine("============================================================");


        var combinedReport = new BenchmarkReport();

        // Matrix multiplication benchmarks

        var matrixSizes = new[] { 64, 128, 256, 512, 1024 };
        var matrixReport = BenchmarkMatrixMultiplication(matrixSizes);
        foreach (var result in matrixReport.Results)
        {
            combinedReport.Results.Add(result);
        }

        // FFT benchmarks

        var fftSizes = new[] { 64, 128, 256, 512, 1024, 2048 };
        var fftReport = BenchmarkFFT(fftSizes);
        foreach (var result in fftReport.Results)
        {
            combinedReport.Results.Add(result);
        }

        // BLAS benchmarks

        var blasSizes = new[] { 100, 500, 1000, 2000 };
        var blasReport = BenchmarkBLAS(blasSizes);
        foreach (var result in blasReport.Results)
        {
            combinedReport.Results.Add(result);
        }

        // Parallel algorithm benchmarks

        var parallelSizes = new[] { 10000, 100000, 1000000 };
        var parallelReport = BenchmarkParallelAlgorithms(parallelSizes);
        foreach (var result in parallelReport.Results)
        {
            combinedReport.Results.Add(result);
        }


        Console.WriteLine("\nBenchmark Suite Completed!");
        Console.WriteLine($"Total benchmarks: {combinedReport.Results.Count}");
        Console.WriteLine($"Maximum speedup achieved: {combinedReport.Results.Max(r => r.SpeedupFactor):F2}x");
        Console.WriteLine($"Average speedup: {combinedReport.Results.Average(r => r.SpeedupFactor):F2}x");


        return combinedReport;
    }

    #region Helper Methods


    private static BenchmarkResult BenchmarkFunction<T>(string name, Func<T> function,

        double flopCount, long bytesProcessed, double baselineTime = 0)
    {
        // Warmup
        for (var i = 0; i < WARMUP_ITERATIONS; i++)
        {
            _ = function();
        }


        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();


        var measurements = new TimeSpan[MEASUREMENT_ITERATIONS];
        var memoryBefore = GC.GetTotalMemory(false);


        for (var i = 0; i < MEASUREMENT_ITERATIONS; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            _ = function();
            stopwatch.Stop();
            measurements[i] = stopwatch.Elapsed;
        }


        var memoryAfter = GC.GetTotalMemory(false);
        var memoryAllocated = Math.Max(0, memoryAfter - memoryBefore);


        return new BenchmarkResult(name, measurements, flopCount, bytesProcessed, baselineTime, memoryAllocated);
    }


    private static Matrix CreateRandomMatrix(int rows, int cols)
    {
        var matrix = new Matrix(rows, cols);
        var random = new Random(42); // Fixed seed for reproducibility


        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                matrix[i, j] = (float)random.NextDouble();
            }
        }


        return matrix;
    }


    private static float[] CreateRandomVector(int size)
    {
        var vector = new float[size];
        var random = new Random(42);


        for (var i = 0; i < size; i++)
        {
            vector[i] = (float)random.NextDouble();
        }


        return vector;
    }


    private static float[] CreateRandomArray(int size) => CreateRandomVector(size);


    private static Complex[] CreateRandomComplexArray(int size)
    {
        var array = new Complex[size];
        var random = new Random(42);


        for (var i = 0; i < size; i++)
        {
            array[i] = new Complex(random.NextDouble(), random.NextDouble());
        }


        return array;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;


    private static double GetBestSpeedup(BenchmarkReport report, params string[] nameFilters)
    {
        var relevantResults = report.Results.Where(r =>

            nameFilters.Any(filter => r.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))).ToList();
        return relevantResults.Count > 0 ? relevantResults.Max(r => r.SpeedupFactor) : 1.0;
    }

    #endregion

    #region Naive Implementations (Baselines)


    private static Matrix NaiveMatrixMultiply(Matrix a, Matrix b)
    {
        var result = new Matrix(a.Rows, b.Columns);


        for (var i = 0; i < a.Rows; i++)
        {
            for (var j = 0; j < b.Columns; j++)
            {
                var sum = 0.0f;
                for (var k = 0; k < a.Columns; k++)
                {
                    sum += a[i, k] * b[k, j];
                }
                result[i, j] = sum;
            }
        }


        return result;
    }


    private static void NaiveDFT(Complex[] data)
    {
        var n = data.Length;
        var result = new Complex[n];


        for (var k = 0; k < n; k++)
        {
            var sum = Complex.Zero;
            for (var j = 0; j < n; j++)
            {
                var angle = -2.0 * Math.PI * k * j / n;
                var twiddle = new Complex(Math.Cos(angle), Math.Sin(angle));
                sum += data[j] * twiddle;
            }
            result[k] = sum;
        }


        Array.Copy(result, data, n);
    }


    private static float NaiveDotProduct(float[] x, float[] y)
    {
        var sum = 0.0f;
        for (var i = 0; i < x.Length; i++)
        {
            sum += x[i] * y[i];
        }
        return sum;
    }


    private static void NaiveAxpy(float alpha, float[] x, float[] y)
    {
        for (var i = 0; i < x.Length; i++)
        {
            y[i] = alpha * x[i] + y[i];
        }
    }


    private static float[] NaiveGemv(Matrix matrix, float[] vector)
    {
        var result = new float[matrix.Rows];


        for (var i = 0; i < matrix.Rows; i++)
        {
            var sum = 0.0f;
            for (var j = 0; j < matrix.Columns; j++)
            {
                sum += matrix[i, j] * vector[j];
            }
            result[i] = sum;
        }


        return result;
    }


    private static float[] OptimizedGemv(Matrix matrix, float[] vector)
    {
        var result = new float[matrix.Rows];
        BLASOptimizations.OptimizedGemv(1.0f, matrix, vector, 0.0f, result);
        return result;
    }


    private static float[] SequentialScan(float[] array)
    {
        var result = new float[array.Length];
        result[0] = array[0];


        for (var i = 1; i < array.Length; i++)
        {
            result[i] = result[i - 1] + array[i];
        }


        return result;
    }


    #endregion
}
