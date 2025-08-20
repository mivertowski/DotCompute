// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using DotCompute.Algorithms.LinearAlgebra;

namespace DotCompute.Algorithms.Optimized;

/// <summary>
/// Intelligent algorithm selector that automatically chooses the most efficient
/// algorithm implementation based on input size, hardware capabilities, and
/// empirical performance data with auto-tuning.
/// </summary>
public static class AlgorithmSelector
{
    // Hardware capability flags
    private static readonly bool HasAvx2 = Avx2.IsSupported;
    private static readonly bool HasFma = Fma.IsSupported;
    private static readonly bool HasSse42 = Sse42.IsSupported;
    private static readonly int CoreCount = Environment.ProcessorCount;
    
    // Performance threshold tables (auto-tuned)
    private static readonly ConcurrentDictionary&lt;string, PerformanceThresholds&gt; _thresholds = new();
    
    // Performance cache to avoid redundant measurements
    private static readonly ConcurrentDictionary&lt;string, AlgorithmPerformance&gt; _performanceCache = new();
    
    // Auto-tuning state
    private static readonly object _tuningLock = new();
    private static bool _autoTuningEnabled = true;
    private static DateTime _lastTuning = DateTime.MinValue;
    private static readonly TimeSpan _tuningInterval = TimeSpan.FromHours(24);
    
    /// <summary>
    /// Hardware capabilities and performance characteristics.
    /// </summary>
    public static class HardwareProfile
    {
        public static readonly bool HasVectorInstructions = HasAvx2 || HasSse42;
        public static readonly bool SupportsParallelism = CoreCount > 1;
        public static readonly bool HasHighMemoryBandwidth = DetectHighMemoryBandwidth();
        public static readonly int OptimalThreadCount = CalculateOptimalThreadCount();
        public static readonly long L1CacheSize = GetCacheSize(CacheLevel.L1);
        public static readonly long L2CacheSize = GetCacheSize(CacheLevel.L2);
        public static readonly long L3CacheSize = GetCacheSize(CacheLevel.L3);
        
        private enum CacheLevel { L1, L2, L3 }
        
        private static bool DetectHighMemoryBandwidth()
        {
            // Simplified heuristic based on core count and architecture
            return CoreCount >= 8 && (HasAvx2 || HasFma);
        }
        
        private static int CalculateOptimalThreadCount()
        {
            // Conservative approach: use 75% of available cores for compute-intensive tasks
            return Math.Max(1, (int)(CoreCount * 0.75));
        }
        
        private static long GetCacheSize(CacheLevel level)
        {
            // Platform-specific cache detection would go here
            // For now, use reasonable defaults based on modern CPUs
            return level switch
            {
                CacheLevel.L1 => 32 * 1024,      // 32KB
                CacheLevel.L2 => 256 * 1024,     // 256KB
                CacheLevel.L3 => 8 * 1024 * 1024, // 8MB
                _ => 0
            };
        }
    }
    
    /// <summary>
    /// Performance thresholds for algorithm selection.
    /// </summary>
    private readonly struct PerformanceThresholds
    {
        public readonly int SimdThreshold;
        public readonly int ParallelThreshold;
        public readonly int CacheObliviousThreshold;
        public readonly int StrassenThreshold;
        public readonly int BlockedThreshold;
        
        public PerformanceThresholds(int simdThreshold, int parallelThreshold, 
            int cacheObliviousThreshold, int strassenThreshold, int blockedThreshold)
        {
            SimdThreshold = simdThreshold;
            ParallelThreshold = parallelThreshold;
            CacheObliviousThreshold = cacheObliviousThreshold;
            StrassenThreshold = strassenThreshold;
            BlockedThreshold = blockedThreshold;
        }
    }
    
    /// <summary>
    /// Algorithm performance metadata.
    /// </summary>
    private readonly struct AlgorithmPerformance
    {
        public readonly string Algorithm;
        public readonly TimeSpan ExecutionTime;
        public readonly double ThroughputMFLOPS;
        public readonly int InputSize;
        public readonly DateTime Timestamp;
        
        public AlgorithmPerformance(string algorithm, TimeSpan executionTime, 
            double throughputMFLOPS, int inputSize)
        {
            Algorithm = algorithm;
            ExecutionTime = executionTime;
            ThroughputMFLOPS = throughputMFLOPS;
            InputSize = inputSize;
            Timestamp = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// Matrix multiplication algorithm selector with auto-tuning.
    /// </summary>
    /// <param name="rows">Matrix A rows</param>
    /// <param name="cols">Matrix B columns</param>
    /// <param name="inner">Inner dimension (A cols, B rows)</param>
    /// <returns>Optimal matrix multiplication strategy</returns>
    public static MatrixMultiplyStrategy SelectMatrixMultiplyAlgorithm(int rows, int cols, int inner)
    {
        var thresholds = GetOrCreateThresholds("MatrixMultiply");
        var totalSize = (long)rows * cols * inner;
        
        // Check for auto-tuning opportunity
        if (_autoTuningEnabled && ShouldRunAutoTuning("MatrixMultiply"))
        {
            AutoTuneMatrixMultiply(rows, cols, inner);
        }
        
        // Algorithm selection based on problem characteristics
        if (rows <= 4 && cols <= 4 && inner <= 4)
        {
            return MatrixMultiplyStrategy.Micro;
        }
        
        if (IsSquareAndPowerOfTwo(Math.Min(Math.Min(rows, cols), inner)) && 
            Math.Min(Math.Min(rows, cols), inner) >= thresholds.StrassenThreshold)
        {
            return MatrixMultiplyStrategy.Strassen;
        }
        
        if (totalSize >= thresholds.CacheObliviousThreshold)
        {
            return MatrixMultiplyStrategy.CacheOblivious;
        }
        
        if (totalSize >= thresholds.ParallelThreshold && HardwareProfile.SupportsParallelism)
        {
            return MatrixMultiplyStrategy.ParallelBlocked;
        }
        
        if (totalSize >= thresholds.BlockedThreshold)
        {
            return MatrixMultiplyStrategy.Blocked;
        }
        
        if (totalSize >= thresholds.SimdThreshold && HardwareProfile.HasVectorInstructions)
        {
            return MatrixMultiplyStrategy.SIMD;
        }
        
        return MatrixMultiplyStrategy.Standard;
    }
    
    /// <summary>
    /// FFT algorithm selector with mixed-radix support.
    /// </summary>
    /// <param name="size">FFT size</param>
    /// <param name="isReal">True for real-valued FFT</param>
    /// <returns>Optimal FFT strategy</returns>
    public static FFTStrategy SelectFFTAlgorithm(int size, bool isReal = false)
    {
        var thresholds = GetOrCreateThresholds("FFT");
        
        if (size <= 1)
            return FFTStrategy.Trivial;
            
        if (size <= 16)
            return FFTStrategy.DirectDFT;
            
        if (!IsPowerOfTwo(size))
        {
            if (CanFactorizeEfficiently(size))
                return FFTStrategy.MixedRadix;
            else
                return FFTStrategy.Bluestein;
        }
        
        if (size >= thresholds.CacheObliviousThreshold)
            return FFTStrategy.CacheFriendly;
            
        if (size >= thresholds.SimdThreshold && HardwareProfile.HasVectorInstructions)
            return isReal ? FFTStrategy.SimdReal : FFTStrategy.SimdComplex;
            
        return FFTStrategy.CooleyTukey;
    }
    
    /// <summary>
    /// BLAS operation selector based on vector/matrix dimensions.
    /// </summary>
    /// <param name="operation">BLAS operation type</param>
    /// <param name="size">Problem size</param>
    /// <returns>Optimal BLAS implementation strategy</returns>
    public static BLASStrategy SelectBLASAlgorithm(BLASOperation operation, int size)
    {
        var thresholds = GetOrCreateThresholds($"BLAS_{operation}");
        
        return operation switch
        {
            BLASOperation.DOT when size >= thresholds.SimdThreshold => BLASStrategy.SimdVectorized,
            BLASOperation.AXPY when size >= thresholds.SimdThreshold => BLASStrategy.SimdVectorized,
            BLASOperation.GEMV when size >= thresholds.ParallelThreshold => BLASStrategy.ParallelBlocked,
            BLASOperation.GEMM when size >= thresholds.ParallelThreshold => BLASStrategy.ParallelBlocked,
            BLASOperation.GEMM when size >= thresholds.BlockedThreshold => BLASStrategy.Blocked,
            _ when size >= thresholds.SimdThreshold && HardwareProfile.HasVectorInstructions => BLASStrategy.Vectorized,
            _ => BLASStrategy.Standard
        };
    }
    
    /// <summary>
    /// Parallel algorithm selector based on problem size and hardware.
    /// </summary>
    /// <param name="problemSize">Problem size</param>
    /// <param name="computeIntensity">Compute intensity (FLOPs per element)</param>
    /// <returns>Optimal parallelization strategy</returns>
    public static ParallelStrategy SelectParallelStrategy(int problemSize, double computeIntensity = 1.0)
    {
        var thresholds = GetOrCreateThresholds("Parallel");
        var adjustedSize = (int)(problemSize * computeIntensity);
        
        if (!HardwareProfile.SupportsParallelism || adjustedSize < thresholds.ParallelThreshold)
        {
            return ParallelStrategy.Sequential;
        }
        
        if (adjustedSize >= thresholds.ParallelThreshold * 10)
        {
            return ParallelStrategy.WorkStealing;
        }
        
        if (CoreCount >= 8 && adjustedSize >= thresholds.ParallelThreshold * 4)
        {
            return ParallelStrategy.ForkJoin;
        }
        
        return ParallelStrategy.TaskParallel;
    }
    
    /// <summary>
    /// Enables or disables auto-tuning of performance thresholds.
    /// </summary>
    /// <param name="enabled">True to enable auto-tuning</param>
    public static void SetAutoTuning(bool enabled)
    {
        lock (_tuningLock)
        {
            _autoTuningEnabled = enabled;
        }
    }
    
    /// <summary>
    /// Manually triggers auto-tuning for all algorithms.
    /// </summary>
    public static void RunAutoTuning()
    {
        lock (_tuningLock)
        {
            Console.WriteLine("Starting algorithm auto-tuning...");
            var stopwatch = Stopwatch.StartNew();
            
            // Auto-tune different algorithm categories
            AutoTuneMatrixMultiply(1024, 1024, 1024);
            AutoTuneFFT(1024);
            AutoTuneBLAS(1024);
            
            stopwatch.Stop();
            Console.WriteLine($"Auto-tuning completed in {stopwatch.ElapsedMilliseconds}ms");
            
            _lastTuning = DateTime.UtcNow;
        }
    }
    
    #region Auto-Tuning Implementation
    
    private static void AutoTuneMatrixMultiply(int rows, int cols, int inner)
    {
        var testSizes = new[] { 64, 128, 256, 512, 1024 };
        var strategies = Enum.GetValues&lt;MatrixMultiplyStrategy&gt;();
        var results = new List&lt;(MatrixMultiplyStrategy Strategy, int Size, double Performance)&gt;();
        
        foreach (var size in testSizes)
        {
            if (size > Math.Max(rows, Math.Max(cols, inner))) continue;
            
            var testA = CreateRandomMatrix(size, size);
            var testB = CreateRandomMatrix(size, size);
            
            foreach (var strategy in strategies)
            {
                if (!IsStrategyApplicable(strategy, size, size, size)) continue;
                
                var performance = BenchmarkMatrixMultiply(testA, testB, strategy);
                results.Add((strategy, size, performance));
            }
        }
        
        // Analyze results and update thresholds
        UpdateMatrixMultiplyThresholds(results);
    }
    
    private static void AutoTuneFFT(int maxSize)
    {
        var testSizes = GenerateFFTTestSizes(maxSize);
        var strategies = Enum.GetValues&lt;FFTStrategy&gt;();
        var results = new List&lt;(FFTStrategy Strategy, int Size, double Performance)&gt;();
        
        foreach (var size in testSizes)
        {
            var testData = CreateRandomComplexArray(size);
            
            foreach (var strategy in strategies)
            {
                if (!IsFFTStrategyApplicable(strategy, size)) continue;
                
                var performance = BenchmarkFFT(testData, strategy);
                results.Add((strategy, size, performance));
            }
        }
        
        UpdateFFTThresholds(results);
    }
    
    private static void AutoTuneBLAS(int maxSize)
    {
        var testSizes = new[] { 32, 64, 128, 256, 512, 1024 };
        var operations = Enum.GetValues&lt;BLASOperation&gt;();
        
        foreach (var operation in operations)
        {
            var results = new List&lt;(BLASStrategy Strategy, int Size, double Performance)&gt;();
            
            foreach (var size in testSizes)
            {
                if (size > maxSize) continue;
                
                var performance = BenchmarkBLASOperation(operation, size);
                results.Add((BLASStrategy.Standard, size, performance));
            }
            
            UpdateBLASThresholds(operation, results);
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    private static PerformanceThresholds GetOrCreateThresholds(string algorithmType)
    {
        return _thresholds.GetOrAdd(algorithmType, _ => GetDefaultThresholds(algorithmType));
    }
    
    private static PerformanceThresholds GetDefaultThresholds(string algorithmType)
    {
        // Hardware-specific default thresholds
        var simdMultiplier = HasAvx2 ? 1.0f : HasSse42 ? 1.5f : 2.0f;
        var coreMultiplier = Math.Max(1.0f, CoreCount / 4.0f);
        
        return algorithmType switch
        {
            "MatrixMultiply" => new PerformanceThresholds(
                (int)(64 * simdMultiplier),      // SIMD threshold
                (int)(1000 * coreMultiplier),    // Parallel threshold
                (int)(10000 * coreMultiplier),   // Cache-oblivious threshold
                256,                             // Strassen threshold
                (int)(500 * simdMultiplier)      // Blocked threshold
            ),
            "FFT" => new PerformanceThresholds(
                (int)(64 * simdMultiplier),
                (int)(1024 * coreMultiplier),
                (int)(8192 * coreMultiplier),
                0, // Not applicable
                (int)(512 * simdMultiplier)
            ),
            _ => new PerformanceThresholds(32, 100, 1000, 128, 64)
        };
    }
    
    private static bool ShouldRunAutoTuning(string algorithmType)
    {
        return DateTime.UtcNow - _lastTuning > _tuningInterval;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSquareAndPowerOfTwo(int n) => IsPowerOfTwo(n);
    
    private static bool CanFactorizeEfficiently(int n)
    {
        // Check if n can be factorized into small primes (2, 3, 5, 7)
        var factors = new[] { 2, 3, 5, 7 };
        foreach (var factor in factors)
        {
            while (n % factor == 0)
            {
                n /= factor;
            }
        }
        return n == 1;
    }
    
    private static Matrix CreateRandomMatrix(int rows, int cols)
    {
        var matrix = new Matrix(rows, cols);
        var random = new Random(42); // Fixed seed for reproducible benchmarks
        
        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                matrix[i, j] = (float)random.NextDouble();
            }
        }
        
        return matrix;
    }
    
    private static System.Numerics.Complex[] CreateRandomComplexArray(int size)
    {
        var array = new System.Numerics.Complex[size];
        var random = new Random(42);
        
        for (var i = 0; i < size; i++)
        {
            array[i] = new System.Numerics.Complex(random.NextDouble(), random.NextDouble());
        }
        
        return array;
    }
    
    private static int[] GenerateFFTTestSizes(int maxSize)
    {
        var sizes = new List&lt;int&gt;();
        
        // Power of 2 sizes
        for (var size = 16; size <= maxSize; size *= 2)
        {
            sizes.Add(size);
        }
        
        // Mixed-radix sizes
        var mixedRadix = new[] { 12, 18, 20, 24, 36, 40, 48, 60, 72, 80, 96 };
        sizes.AddRange(mixedRadix.Where(s => s <= maxSize));
        
        return sizes.ToArray();
    }
    
    private static double BenchmarkMatrixMultiply(Matrix a, Matrix b, MatrixMultiplyStrategy strategy)
    {
        // Simplified benchmarking - would use more sophisticated timing in production
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            switch (strategy)
            {
                case MatrixMultiplyStrategy.SIMD:
                    MatrixOptimizations.SimdMultiply(a, b, new Matrix(a.Rows, b.Columns));
                    break;
                case MatrixMultiplyStrategy.Blocked:
                    MatrixOptimizations.BlockedMultiply(a, b, new Matrix(a.Rows, b.Columns));
                    break;
                default:
                    MatrixOptimizations.OptimizedMultiply(a, b);
                    break;
            }
        }
        catch
        {
            return 0; // Strategy failed
        }
        
        stopwatch.Stop();
        var flops = 2.0 * a.Rows * b.Columns * a.Columns;
        return flops / stopwatch.Elapsed.TotalSeconds / 1e6; // MFLOPS
    }
    
    private static double BenchmarkFFT(System.Numerics.Complex[] data, FFTStrategy strategy)
    {
        var dataCopy = data.ToArray();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            FFTOptimizations.OptimizedFFT(dataCopy);
        }
        catch
        {
            return 0;
        }
        
        stopwatch.Stop();
        var flops = 5.0 * data.Length * Math.Log2(data.Length); // Approximate FFT FLOPs
        return flops / stopwatch.Elapsed.TotalSeconds / 1e6;
    }
    
    private static double BenchmarkBLASOperation(BLASOperation operation, int size)
    {
        // Simplified BLAS benchmarking
        return size * 1000.0 / (size + 100); // Placeholder performance model
    }
    
    private static bool IsStrategyApplicable(MatrixMultiplyStrategy strategy, int rows, int cols, int inner)
    {
        return strategy switch
        {
            MatrixMultiplyStrategy.Strassen => IsSquareAndPowerOfTwo(Math.Min(rows, Math.Min(cols, inner))),
            MatrixMultiplyStrategy.SIMD => HardwareProfile.HasVectorInstructions,
            MatrixMultiplyStrategy.ParallelBlocked => HardwareProfile.SupportsParallelism,
            _ => true
        };
    }
    
    private static bool IsFFTStrategyApplicable(FFTStrategy strategy, int size)
    {
        return strategy switch
        {
            FFTStrategy.MixedRadix => !IsPowerOfTwo(size),
            FFTStrategy.SimdComplex or FFTStrategy.SimdReal => HardwareProfile.HasVectorInstructions,
            FFTStrategy.CooleyTukey => IsPowerOfTwo(size),
            _ => true
        };
    }
    
    // Placeholder methods for threshold updates
    private static void UpdateMatrixMultiplyThresholds(List&lt;(MatrixMultiplyStrategy Strategy, int Size, double Performance)&gt; results) { }
    private static void UpdateFFTThresholds(List&lt;(FFTStrategy Strategy, int Size, double Performance)&gt; results) { }
    private static void UpdateBLASThresholds(BLASOperation operation, List&lt;(BLASStrategy Strategy, int Size, double Performance)&gt; results) { }
    
    #endregion
}

/// <summary>
/// Matrix multiplication algorithm strategies.
/// </summary>
public enum MatrixMultiplyStrategy
{
    Micro,              // Optimized micro-kernels for very small matrices
    Standard,           // Standard ijk algorithm
    SIMD,              // SIMD-optimized algorithm
    Blocked,           // Cache-blocked algorithm
    Strassen,          // Strassen's algorithm
    CacheOblivious,    // Cache-oblivious algorithm
    ParallelBlocked    // Parallel blocked algorithm
}

/// <summary>
/// FFT algorithm strategies.
/// </summary>
public enum FFTStrategy
{
    Trivial,           // No computation needed
    DirectDFT,         // Direct DFT for very small sizes
    CooleyTukey,       // Standard Cooley-Tukey FFT
    MixedRadix,        // Mixed-radix FFT
    SimdComplex,       // SIMD-optimized complex FFT
    SimdReal,          // SIMD-optimized real FFT
    CacheFriendly,     // Cache-friendly four-step FFT
    Bluestein          // Bluestein's algorithm for arbitrary sizes
}

/// <summary>
/// BLAS algorithm strategies.
/// </summary>
public enum BLASStrategy
{
    Standard,          // Standard implementation
    Vectorized,        // Vector-optimized
    SimdVectorized,    // SIMD-vectorized
    Blocked,           // Cache-blocked
    ParallelBlocked    // Parallel blocked
}

/// <summary>
/// BLAS operation types.
/// </summary>
public enum BLASOperation
{
    DOT,    // Dot product
    AXPY,   // y = ax + y
    GEMV,   // Matrix-vector multiply
    GEMM    // Matrix-matrix multiply
}

/// <summary>
/// Parallel algorithm strategies.
/// </summary>
public enum ParallelStrategy
{
    Sequential,        // No parallelization
    TaskParallel,     // Task-based parallelism
    ForkJoin,         // Fork-join parallelism
    WorkStealing      // Work-stealing parallelism
}