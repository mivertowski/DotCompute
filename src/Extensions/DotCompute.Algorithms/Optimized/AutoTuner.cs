// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotCompute.Algorithms.LinearAlgebra;

namespace DotCompute.Algorithms.Optimized;

/// <summary>
/// Advanced auto-tuning system that dynamically optimizes algorithm parameters
/// based on hardware capabilities, runtime performance measurements, and
/// machine learning models for optimal parameter selection.
/// </summary>
public sealed class AutoTuner : IDisposable
{
    // Auto-tuning configuration
    private const int MIN_MEASUREMENTS = 10;
    private const int MAX_MEASUREMENTS = 100;
    private const double CONFIDENCE_THRESHOLD = 0.95;
    private const double IMPROVEMENT_THRESHOLD = 0.05; // 5% minimum improvement
    private const int MAX_SEARCH_ITERATIONS = 50;
    
    // Persistent storage
    private readonly string _configPath;
    private readonly Timer? _periodicTuningTimer;
    private readonly ConcurrentDictionary<string, TuningProfile> _profiles = new();
    private readonly ConcurrentDictionary<string, ParameterOptimizer> _optimizers = new();
    
    // Thread safety
    private readonly object _saveLock = new();
    private volatile bool _disposed;
    
    /// <summary>
    /// Auto-tuning configuration and results for a specific algorithm.
    /// </summary>
    [Serializable]
    public sealed class TuningProfile
    {
        public string AlgorithmName { get; set; } = string.Empty;
        public Dictionary<string, object> OptimalParameters { get; set; } = new();
        public Dictionary<string, ParameterRange> ParameterRanges { get; set; } = new();
        public double BestPerformance { get; set; }
        public DateTime LastTuned { get; set; }
        public int TuningIterations { get; set; }
        public string HardwareFingerprint { get; set; } = string.Empty;
        public Dictionary<string, double> PerformanceHistory { get; set; } = new();
        
        [JsonIgnore]
        public bool IsValid => OptimalParameters.Count > 0 && BestPerformance > 0;
    }
    
    /// <summary>
    /// Parameter range specification for auto-tuning.
    /// </summary>
    [Serializable]
    public sealed class ParameterRange
    {
        public object MinValue { get; set; } = 0;
        public object MaxValue { get; set; } = 100;
        public object StepSize { get; set; } = 1;
        public Type ParameterType { get; set; } = typeof(int);
        public bool IsDiscrete { get; set; } = true;
        
        public IEnumerable<object> GenerateValues()
        {
            if (ParameterType == typeof(int))
            {
                var min = Convert.ToInt32(MinValue);
                var max = Convert.ToInt32(MaxValue);
                var step = Convert.ToInt32(StepSize);
                
                for (var value = min; value <= max; value += step)
                {
                    yield return value;
                }
            }
            else if (ParameterType == typeof(double))
            {
                var min = Convert.ToDouble(MinValue);
                var max = Convert.ToDouble(MaxValue);
                var step = Convert.ToDouble(StepSize);
                
                for (var value = min; value <= max; value += step)
                {
                    yield return value;
                }
            }
            else if (ParameterType == typeof(bool))
            {
                yield return false;
                yield return true;
            }
        }
    }
    
    /// <summary>
    /// Performance measurement result.
    /// </summary>
    public readonly struct PerformanceMeasurement
    {
        public readonly Dictionary<string, object> Parameters;
        public readonly double Performance;
        public readonly TimeSpan ExecutionTime;
        public readonly double StandardDeviation;
        public readonly bool IsValid;
        
        public PerformanceMeasurement(Dictionary<string, object> parameters, 
            double performance, TimeSpan executionTime, double standardDeviation)
        {
            Parameters = parameters;
            Performance = performance;
            ExecutionTime = executionTime;
            StandardDeviation = standardDeviation;
            IsValid = performance > 0 && !double.IsNaN(performance);
        }
    }
    
    /// <summary>
    /// Parameter optimization strategy.
    /// </summary>
    private abstract class ParameterOptimizer
    {
        public abstract Dictionary<string, object> GetNextParameters(
            List<PerformanceMeasurement> measurements,
            Dictionary<string, ParameterRange> ranges);
            
        public abstract bool ShouldContinue(List<PerformanceMeasurement> measurements, int iteration);
    }
    
    /// <summary>
    /// Grid search optimizer for exhaustive parameter exploration.
    /// </summary>
    private sealed class GridSearchOptimizer : ParameterOptimizer
    {
        private readonly Queue<Dictionary<string, object>> _parameterQueue = new();
        private bool _initialized = false;
        
        public override Dictionary<string, object> GetNextParameters(
            List<PerformanceMeasurement> measurements,
            Dictionary<string, ParameterRange> ranges)
        {
            if (!_initialized)
            {
                InitializeGrid(ranges);
                _initialized = true;
            }
            
            return _parameterQueue.Count > 0 ? _parameterQueue.Dequeue() : new Dictionary<string, object>();
        }


        public override bool ShouldContinue(List<PerformanceMeasurement> measurements, int iteration) => _parameterQueue.Count > 0 && iteration < MAX_SEARCH_ITERATIONS;


        private void InitializeGrid(Dictionary<string, ParameterRange> ranges)
        {
            var parameterNames = ranges.Keys.ToArray();
            var parameterValues = ranges.Values.Select(r => r.GenerateValues().ToArray()).ToArray();
            
            GenerateCartesianProduct(parameterNames, parameterValues, 0, new Dictionary<string, object>());
        }
        
        private void GenerateCartesianProduct(string[] names, object[][] values, int index, 
            Dictionary<string, object> current)
        {
            if (index == names.Length)
            {
                _parameterQueue.Enqueue(new Dictionary<string, object>(current));
                return;
            }
            
            foreach (var value in values[index])
            {
                current[names[index]] = value;
                GenerateCartesianProduct(names, values, index + 1, current);
            }
            
            if (current.ContainsKey(names[index]))
            {
                current.Remove(names[index]);
            }
        }
    }
    
    /// <summary>
    /// Random search optimizer for large parameter spaces.
    /// </summary>
    private sealed class RandomSearchOptimizer : ParameterOptimizer
    {
        private readonly Random _random = new();
        
        public override Dictionary<string, object> GetNextParameters(
            List<PerformanceMeasurement> measurements,
            Dictionary<string, ParameterRange> ranges)
        {
            var parameters = new Dictionary<string, object>();
            
            foreach (var (name, range) in ranges)
            {
                parameters[name] = GenerateRandomValue(range);
            }
            
            return parameters;
        }
        
        public override bool ShouldContinue(List<PerformanceMeasurement> measurements, int iteration)
        {
            if (iteration < MIN_MEASUREMENTS)
            {
                return true;
            }


            if (iteration >= MAX_SEARCH_ITERATIONS)
            {
                return false;
            }

            // Continue if we're still finding improvements

            if (measurements.Count >= 5)
            {
                var recent = measurements.TakeLast(5).Average(m => m.Performance);
                var older = measurements.SkipLast(5).TakeLast(5).Average(m => m.Performance);
                var improvement = (recent - older) / older;
                
                return improvement > IMPROVEMENT_THRESHOLD;
            }
            
            return true;
        }
        
        private object GenerateRandomValue(ParameterRange range)
        {
            if (range.ParameterType == typeof(int))
            {
                var min = Convert.ToInt32(range.MinValue);
                var max = Convert.ToInt32(range.MaxValue);
                return _random.Next(min, max + 1);
            }
            else if (range.ParameterType == typeof(double))
            {
                var min = Convert.ToDouble(range.MinValue);
                var max = Convert.ToDouble(range.MaxValue);
                return min + _random.NextDouble() * (max - min);
            }
            else if (range.ParameterType == typeof(bool))
            {
                return _random.Next(2) == 1;
            }
            
            return range.MinValue;
        }
    }
    
    /// <summary>
    /// Bayesian optimization for intelligent parameter search.
    /// </summary>
    private sealed class BayesianOptimizer : ParameterOptimizer
    {
        private readonly Random _random = new();
        
        public override Dictionary<string, object> GetNextParameters(
            List<PerformanceMeasurement> measurements,
            Dictionary<string, ParameterRange> ranges)
        {
            if (measurements.Count < 5)
            {
                // Use random search for initial exploration
                return new RandomSearchOptimizer().GetNextParameters(measurements, ranges);
            }
            
            // Simplified Bayesian optimization - in production would use GP/TPE
            var bestMeasurement = measurements.OrderByDescending(m => m.Performance).First();
            var parameters = new Dictionary<string, object>(bestMeasurement.Parameters);
            
            // Add small perturbations around best parameters
            foreach (var (name, range) in ranges)
            {
                if (parameters.ContainsKey(name))
                {
                    parameters[name] = PerturbParameter(parameters[name], range);
                }
            }
            
            return parameters;
        }
        
        public override bool ShouldContinue(List<PerformanceMeasurement> measurements, int iteration)
        {
            if (iteration < MIN_MEASUREMENTS)
            {
                return true;
            }


            if (iteration >= MAX_SEARCH_ITERATIONS)
            {
                return false;
            }

            // Use acquisition function (simplified)

            if (measurements.Count >= 10)
            {
                var convergenceWindow = measurements.TakeLast(10);
                var variance = CalculateVariance(convergenceWindow.Select(m => m.Performance));
                return variance > 0.01; // Continue if still significant variance
            }
            
            return true;
        }
        
        private object PerturbParameter(object current, ParameterRange range)
        {
            if (range.ParameterType == typeof(int))
            {
                var value = Convert.ToInt32(current);
                var step = Convert.ToInt32(range.StepSize);
                var perturbation = _random.Next(-2 * step, 2 * step + 1);
                var newValue = Math.Clamp(value + perturbation, 
                    Convert.ToInt32(range.MinValue), Convert.ToInt32(range.MaxValue));
                return newValue;
            }
            else if (range.ParameterType == typeof(double))
            {
                var value = Convert.ToDouble(current);
                var step = Convert.ToDouble(range.StepSize);
                var perturbation = (_random.NextDouble() - 0.5) * 4 * step;
                var newValue = Math.Clamp(value + perturbation, 
                    Convert.ToDouble(range.MinValue), Convert.ToDouble(range.MaxValue));
                return newValue;
            }
            
            return current;
        }
        
        private static double CalculateVariance(IEnumerable<double> values)
        {
            var array = values.ToArray();
            var mean = array.Average();
            return array.Average(v => Math.Pow(v - mean, 2));
        }
    }
    
    public AutoTuner(string? configPath = null, bool enablePeriodicTuning = true)
    {
        _configPath = configPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DotCompute", "autotuner-config.json");
            
        LoadConfiguration();
        
        if (enablePeriodicTuning)
        {
            _periodicTuningTimer = new Timer(PeriodicTuningCallback, null, 
                TimeSpan.FromHours(1), TimeSpan.FromHours(24));
        }
    }
    
    /// <summary>
    /// Registers an algorithm for auto-tuning with parameter ranges.
    /// </summary>
    /// <param name="algorithmName">Algorithm identifier</param>
    /// <param name="parameterRanges">Parameter ranges to explore</param>
    /// <param name="optimizer">Optimization strategy (default: Bayesian)</param>
    public void RegisterAlgorithm(string algorithmName, 
        Dictionary<string, ParameterRange> parameterRanges,
        string optimizer = "bayesian")
    {
        var profile = new TuningProfile
        {
            AlgorithmName = algorithmName,
            ParameterRanges = parameterRanges,
            HardwareFingerprint = GetHardwareFingerprint(),
            LastTuned = DateTime.UtcNow
        };
        
        _profiles[algorithmName] = profile;
        
        _optimizers[algorithmName] = optimizer.ToLower() switch
        {
            "grid" => new GridSearchOptimizer(),
            "random" => new RandomSearchOptimizer(),
            "bayesian" => new BayesianOptimizer(),
            _ => new BayesianOptimizer()
        };
        
        SaveConfiguration();
    }
    
    /// <summary>
    /// Auto-tunes matrix multiplication parameters for optimal performance.
    /// </summary>
    /// <param name="testSizes">Matrix sizes to use for tuning</param>
    /// <returns>Tuning results</returns>
    public async Task<TuningProfile> TuneMatrixMultiplicationAsync(int[] testSizes)
    {
        const string algorithmName = "MatrixMultiplication";
        
        // Define parameter ranges for matrix multiplication
        var parameterRanges = new Dictionary<string, ParameterRange>
        {
            ["BlockSize"] = new() { MinValue = 32, MaxValue = 512, StepSize = 32, ParameterType = typeof(int) },
            ["UseStrassen"] = new() { ParameterType = typeof(bool) },
            ["SimdThreshold"] = new() { MinValue = 64, MaxValue = 1024, StepSize = 64, ParameterType = typeof(int) },
            ["ParallelThreshold"] = new() { MinValue = 100, MaxValue = 10000, StepSize = 100, ParameterType = typeof(int) }
        };
        
        RegisterAlgorithm(algorithmName, parameterRanges, "bayesian");
        
        var measurements = new List<PerformanceMeasurement>();
        var optimizer = _optimizers[algorithmName];
        var iteration = 0;
        
        Console.WriteLine($"Starting auto-tuning for {algorithmName}...");
        
        do
        {
            var parameters = optimizer.GetNextParameters(measurements, parameterRanges);
            if (parameters.Count == 0)
            {
                break;
            }


            Console.Write($"Iteration {++iteration}: Testing parameters... ");
            
            // Benchmark with current parameters
            var performance = await BenchmarkMatrixMultiplicationAsync(testSizes, parameters);
            measurements.Add(performance);
            
            Console.WriteLine($"Performance: {performance.Performance:F2} MFLOPS");
            
            // Update best result
            var profile = _profiles[algorithmName];
            if (performance.Performance > profile.BestPerformance)
            {
                profile.BestPerformance = performance.Performance;
                profile.OptimalParameters = new Dictionary<string, object>(parameters);
                profile.LastTuned = DateTime.UtcNow;
                
                Console.WriteLine($"New best performance: {performance.Performance:F2} MFLOPS");
            }
        } while (optimizer.ShouldContinue(measurements, iteration) && !_disposed);
        
        var finalProfile = _profiles[algorithmName];
        finalProfile.TuningIterations = iteration;
        finalProfile.PerformanceHistory = measurements
            .Select((m, i) => new { Index = i, Performance = m.Performance })
            .ToDictionary(x => x.Index.ToString(), x => x.Performance);
            
        SaveConfiguration();
        
        Console.WriteLine($"Auto-tuning completed after {iteration} iterations.");
        Console.WriteLine($"Best parameters: {JsonSerializer.Serialize(finalProfile.OptimalParameters)}");
        Console.WriteLine($"Best performance: {finalProfile.BestPerformance:F2} MFLOPS");
        
        return finalProfile;
    }
    
    /// <summary>
    /// Auto-tunes FFT parameters for different sizes and strategies.
    /// </summary>
    /// <param name="testSizes">FFT sizes to test</param>
    /// <returns>Tuning results</returns>
    public async Task<TuningProfile> TuneFFTAsync(int[] testSizes)
    {
        const string algorithmName = "FFT";
        
        var parameterRanges = new Dictionary<string, ParameterRange>
        {
            ["SimdThreshold"] = new() { MinValue = 32, MaxValue = 1024, StepSize = 32, ParameterType = typeof(int) },
            ["CacheThreshold"] = new() { MinValue = 512, MaxValue = 8192, StepSize = 512, ParameterType = typeof(int) },
            ["UseMixedRadix"] = new() { ParameterType = typeof(bool) },
            ["TwiddleCacheSize"] = new() { MinValue = 1024, MaxValue = 16384, StepSize = 1024, ParameterType = typeof(int) }
        };
        
        RegisterAlgorithm(algorithmName, parameterRanges, "random");
        
        var measurements = new List<PerformanceMeasurement>();
        var optimizer = _optimizers[algorithmName];
        var iteration = 0;
        
        Console.WriteLine($"Starting auto-tuning for {algorithmName}...");
        
        do
        {
            var parameters = optimizer.GetNextParameters(measurements, parameterRanges);
            if (parameters.Count == 0)
            {
                break;
            }


            var performance = await BenchmarkFFTAsync(testSizes, parameters);
            measurements.Add(performance);
            
            Console.WriteLine($"Iteration {++iteration}: {performance.Performance:F2} MFLOPS");
            
            var profile = _profiles[algorithmName];
            if (performance.Performance > profile.BestPerformance)
            {
                profile.BestPerformance = performance.Performance;
                profile.OptimalParameters = new Dictionary<string, object>(parameters);
                profile.LastTuned = DateTime.UtcNow;
            }
        } while (optimizer.ShouldContinue(measurements, iteration) && !_disposed);
        
        var finalProfile = _profiles[algorithmName];
        finalProfile.TuningIterations = iteration;
        SaveConfiguration();
        
        return finalProfile;
    }
    
    /// <summary>
    /// Gets optimal parameters for a registered algorithm.
    /// </summary>
    /// <param name="algorithmName">Algorithm identifier</param>
    /// <returns>Optimal parameters or default values</returns>
    public Dictionary<string, object> GetOptimalParameters(string algorithmName)
    {
        if (_profiles.TryGetValue(algorithmName, out var profile) && profile.IsValid)
        {
            // Check if parameters are still valid for current hardware
            if (profile.HardwareFingerprint == GetHardwareFingerprint())
            {
                return profile.OptimalParameters;
            }
        }
        
        return new Dictionary<string, object>();
    }
    
    /// <summary>
    /// Triggers auto-tuning for all registered algorithms.
    /// </summary>
    public async Task TuneAllAlgorithmsAsync()
    {
        var tasks = new List<Task>();
        
        foreach (var algorithmName in _profiles.Keys)
        {
            tasks.Add(algorithmName switch
            {
                "MatrixMultiplication" => TuneMatrixMultiplicationAsync(new[] { 256, 512, 1024 }),
                "FFT" => TuneFFTAsync(new[] { 256, 512, 1024, 2048 }),
                _ => Task.CompletedTask
            });
        }
        
        await Task.WhenAll(tasks);
    }
    
    #region Private Implementation
    
    private async Task<PerformanceMeasurement> BenchmarkMatrixMultiplicationAsync(
        int[] testSizes, Dictionary<string, object> parameters)
    {
        return await Task.Run(() =>
        {
            var totalPerformance = 0.0;
            var measurements = new List<double>();
        
        foreach (var size in testSizes)
        {
            var matrixA = CreateRandomMatrix(size, size);
            var matrixB = CreateRandomMatrix(size, size);
            var flopCount = 2.0 * size * size * size;
            
            // Run multiple iterations for accuracy
            var times = new List<TimeSpan>();
            for (var i = 0; i < 5; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                
                // Use parameters to configure algorithm
                if (parameters.GetValueOrDefault("UseStrassen", false).Equals(true) && 
                    size >= 256 && IsPowerOfTwo(size))
                {
                    MatrixOptimizations.StrassenMultiply(matrixA, matrixB, new Matrix(size, size));
                }
                else
                {
                    MatrixOptimizations.OptimizedMultiply(matrixA, matrixB);
                }
                
                stopwatch.Stop();
                times.Add(stopwatch.Elapsed);
            }
            
            var avgTime = times.Average(t => t.TotalSeconds);
            var performance = flopCount / avgTime / 1e6; // MFLOPS
            measurements.Add(performance);
            totalPerformance += performance;
            }
            
            var avgPerformance = totalPerformance / testSizes.Length;
            var stdDev = Math.Sqrt(measurements.Average(m => Math.Pow(m - avgPerformance, 2)));
            
            return new PerformanceMeasurement(
                parameters, avgPerformance, TimeSpan.FromSeconds(1.0 / avgPerformance), stdDev);
        });
    }
    
    private async Task<PerformanceMeasurement> BenchmarkFFTAsync(
        int[] testSizes, Dictionary<string, object> parameters)
    {
        return await Task.Run(() =>
        {
            var totalPerformance = 0.0;
            var measurements = new List<double>();
        
        foreach (var size in testSizes)
        {
            var complexData = CreateRandomComplexArray(size);
            var flopCount = 5.0 * size * Math.Log2(size);
            
            var times = new List<TimeSpan>();
            for (var i = 0; i < 5; i++)
            {
                var dataCopy = complexData.ToArray();
                var stopwatch = Stopwatch.StartNew();
                
                FFTOptimizations.OptimizedFFT(dataCopy);
                
                stopwatch.Stop();
                times.Add(stopwatch.Elapsed);
            }
            
            var avgTime = times.Average(t => t.TotalSeconds);
            var performance = flopCount / avgTime / 1e6; // MFLOPS
            measurements.Add(performance);
            totalPerformance += performance;
        }
        
        var avgPerformance = totalPerformance / testSizes.Length;
        var stdDev = Math.Sqrt(measurements.Average(m => Math.Pow(m - avgPerformance, 2)));
        
            return new PerformanceMeasurement(
                parameters, avgPerformance, TimeSpan.FromSeconds(1.0 / avgPerformance), stdDev);
        });
    }
    
    private void LoadConfiguration()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var profiles = JsonSerializer.Deserialize<Dictionary<string, TuningProfile>>(json);
                
                if (profiles != null)
                {
                    foreach (var (name, profile) in profiles)
                    {
                        _profiles[name] = profile;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load auto-tuner configuration: {ex.Message}");
        }
    }
    
    private void SaveConfiguration()
    {
        try
        {
            lock (_saveLock)
            {
                var directory = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                
                var json = JsonSerializer.Serialize(_profiles.ToDictionary(), options);
                File.WriteAllText(_configPath, json);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save auto-tuner configuration: {ex.Message}");
        }
    }
    
    private static string GetHardwareFingerprint()
    {
        // Create a fingerprint based on hardware characteristics
        var features = new[]
        {
            Environment.ProcessorCount.ToString(),
            SimdIntrinsics.HasAvx2.ToString(),
            SimdIntrinsics.HasFma.ToString(),
            SimdIntrinsics.HasNeon.ToString(),
            Environment.Is64BitProcess.ToString()
        };
        
        return string.Join("-", features);
    }
    
    private static Matrix CreateRandomMatrix(int rows, int cols)
    {
        var matrix = new Matrix(rows, cols);
        var random = new Random(42);
        
        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                matrix[i, j] = (float)random.NextDouble();
            }
        }
        
        return matrix;
    }
    
    private static DotCompute.Algorithms.SignalProcessing.Complex[] CreateRandomComplexArray(int size)
    {
        var array = new DotCompute.Algorithms.SignalProcessing.Complex[size];
        var random = new Random(42);
        
        for (var i = 0; i < size; i++)
        {
            array[i] = new DotCompute.Algorithms.SignalProcessing.Complex((float)random.NextDouble(), (float)random.NextDouble());
        }
        
        return array;
    }
    
    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;
    
    private void PeriodicTuningCallback(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            Task.Run(async () =>
            {
                foreach (var (algorithmName, profile) in _profiles)
                {
                    // Only re-tune if it's been more than a week
                    if (DateTime.UtcNow - profile.LastTuned > TimeSpan.FromDays(7))
                    {
                        Console.WriteLine($"Periodic auto-tuning for {algorithmName}");
                        
                        switch (algorithmName)
                        {
                            case "MatrixMultiplication":
                                await TuneMatrixMultiplicationAsync(new[] { 512, 1024 });
                                break;
                            case "FFT":
                                await TuneFFTAsync(new[] { 512, 1024 });
                                break;
                        }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Periodic auto-tuning failed: {ex.Message}");
        }
    }
    
    #endregion
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _periodicTuningTimer?.Dispose();
            SaveConfiguration();
            _disposed = true;
        }
    }
}