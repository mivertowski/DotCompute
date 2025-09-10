// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Pipelines;
using DotCompute.Abstractions.Types;
using DotCompute.Linq.Pipelines.Analysis;
using DotCompute.Linq.Pipelines.Complex;
using DotCompute.Linq.Pipelines.Diagnostics;
using DotCompute.Linq.Pipelines.Models;
using DotCompute.Linq.Pipelines.Optimization;
using DotCompute.Linq.Pipelines.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Pipelines.Examples;

/// <summary>
/// Comprehensive demonstration of all LINQ pipeline capabilities including complex queries,
/// streaming operations, error handling, and advanced optimization techniques.
/// </summary>
public class ComprehensivePipelineDemo
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ComprehensivePipelineDemo> _logger;
    private readonly IPipelinePerformanceAnalyzer _performanceAnalyzer;
    private readonly PipelineErrorHandler _errorHandler;

    /// <summary>
    /// Initializes a new instance of the ComprehensivePipelineDemo class.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection</param>
    public ComprehensivePipelineDemo(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = serviceProvider.GetRequiredService<ILogger<ComprehensivePipelineDemo>>();
        _performanceAnalyzer = serviceProvider.GetRequiredService<IPipelinePerformanceAnalyzer>();
        _errorHandler = serviceProvider.GetRequiredService<PipelineErrorHandler>();
    }

    /// <summary>
    /// Demonstrates basic LINQ-to-Pipeline conversion and execution.
    /// </summary>
    public async Task DemonstrateBasicPipelineAsync()
    {
        _logger.LogInformation("Starting basic pipeline demonstration");

        // Create sample data
        var data = Enumerable.Range(1, 10000).Select(i => new SampleData
        {
            Id = i,
            Value = i * 1.5f,
            Category = i % 10,
            IsActive = i % 2 == 0
        }).ToArray();

        try
        {
            // Convert to pipeline and execute basic operations
            var pipeline = data.AsComputePipeline(_serviceProvider)
                .ThenSelect<SampleData, ProcessedData>(item => new ProcessedData
                {
                    Id = item.Id,
                    ComputedValue = item.Value * 2,
                    Category = item.Category
                })
                .ThenWhere<ProcessedData>(item => item.ComputedValue > 100)
                .ThenAggregate<ProcessedData>((a, b) => new ProcessedData
                {
                    Id = Math.Max(a.Id, b.Id),
                    ComputedValue = a.ComputedValue + b.ComputedValue,
                    Category = a.Category
                });

            var result = await pipeline.ExecutePipelineAsync<ProcessedData>();
            
            _logger.LogInformation("Basic pipeline executed successfully. Result: {Result}", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Basic pipeline demonstration failed");
            throw;
        }
    }

    /// <summary>
    /// Demonstrates complex GPU-optimized query operations.
    /// </summary>
    public async Task DemonstrateComplexQueryPatternsAsync()
    {
        _logger.LogInformation("Starting complex query patterns demonstration");

        // Create larger dataset for GPU acceleration
        var data = GenerateLargeDataset(100000);

        try
        {
            // GPU-optimized grouping operation
            var groupingPipeline = data.AsComputePipeline(_serviceProvider)
                .GroupByGpu(item => item.Category, new GpuGroupingOptions
                {
                    PreferredBackend = "CUDA",
                    BatchSize = 10000,
                    EnableLocalReduction = true
                });

            var groupedResults = await groupingPipeline.ExecutePipelineAsync<IEnumerable<IGrouping<int, SampleData>>>();

            // GPU-optimized join operation
            var secondaryData = GenerateSecondaryDataset(1000);
            var joinPipeline = data.AsComputePipeline(_serviceProvider)
                .JoinGpu(secondaryData, 
                    outer => outer.Category,
                    inner => inner.CategoryId,
                    (outer, inner) => new JoinedData
                    {
                        Id = outer.Id,
                        Value = outer.Value,
                        CategoryName = inner.CategoryName,
                        Multiplier = inner.Multiplier
                    },
                    new GpuJoinOptions
                    {
                        JoinAlgorithm = JoinAlgorithm.SortMergeJoin,
                        EnableHashOptimization = true
                    });

            var joinResults = await joinPipeline.ExecutePipelineAsync<IEnumerable<JoinedData>>();

            // Moving average with sliding window
            var movingAveragePipeline = data.AsComputePipeline(_serviceProvider)
                .MovingAverage(item => item.Value, windowSize: 50, new WindowOptions
                {
                    WindowType = WindowType.Sliding,
                    EdgeHandling = EdgeHandling.ZeroPadding
                });

            var averageResults = await movingAveragePipeline.ExecutePipelineAsync<IEnumerable<float>>();

            _logger.LogInformation("Complex query patterns executed. Groups: {GroupCount}, Joins: {JoinCount}, Averages: {AvgCount}",
                groupedResults.Count(), joinResults.Count(), averageResults.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Complex query patterns demonstration failed");
            throw;
        }
    }

    /// <summary>
    /// Demonstrates real-time streaming pipeline processing.
    /// </summary>
    public async Task DemonstrateStreamingPipelineAsync()
    {
        _logger.LogInformation("Starting streaming pipeline demonstration");

        try
        {
            // Create a streaming data source
            var streamingData = GenerateStreamingData();

            // Set up streaming pipeline with micro-batching
            var streamingPipeline = streamingData.AsStreamingPipeline(_serviceProvider, 
                batchSize: 1000, 
                windowSize: TimeSpan.FromSeconds(5));

            // Process with batching and anomaly detection
            var processedStream = streamingPipeline
                .WithBatching(new BatchingOptions
                {
                    BatchSize = 1000,
                    MaxWaitTime = TimeSpan.FromMilliseconds(100),
                    EnableBackpressure = true
                })
                .DetectAnomalies(new AnomalyDetectionOptions
                {
                    Algorithm = AnomalyAlgorithm.IsolationForest,
                    Threshold = 0.1f,
                    WindowSize = 1000
                })
                .ContinuousAggregate(new AggregationOptions
                {
                    AggregationType = AggregationType.MovingAverage,
                    WindowSize = TimeSpan.FromMinutes(5),
                    UpdateInterval = TimeSpan.FromSeconds(1)
                });

            // Consume streaming results
            int processedCount = 0;
            await foreach (var batch in processedStream.Take(10))
            {
                processedCount++;
                _logger.LogDebug("Processed streaming batch {BatchNumber} with {ItemCount} items", 
                    processedCount, batch.ToString()?.Length ?? 0);
            }

            _logger.LogInformation("Streaming pipeline processed {BatchCount} batches", processedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Streaming pipeline demonstration failed");
            throw;
        }
    }

    /// <summary>
    /// Demonstrates advanced optimization techniques and performance analysis.
    /// </summary>
    public async Task DemonstrateAdvancedOptimizationAsync()
    {
        _logger.LogInformation("Starting advanced optimization demonstration");

        var data = GenerateLargeDataset(50000);

        try
        {
            // Create a complex pipeline for optimization
            var complexPipeline = data.AsComputePipeline(_serviceProvider)
                .ThenSelect<SampleData, ProcessedData>(item => new ProcessedData
                {
                    Id = item.Id,
                    ComputedValue = item.Value * Math.Sqrt(item.Id),
                    Category = item.Category
                })
                .ThenWhere<ProcessedData>(item => item.ComputedValue > 10)
                .ThenGroupBy<ProcessedData, int>(item => item.Category);

            // Analyze performance characteristics
            var performanceReport = await _performanceAnalyzer.AnalyzePipelineAsync(
                Expression.Constant(complexPipeline));

            _logger.LogInformation("Performance Analysis Results:");
            _logger.LogInformation("- Estimated execution time: {ExecutionTime}ms", performanceReport.EstimatedExecutionTimeMs);
            _logger.LogInformation("- Memory usage: {MemoryUsage}MB", performanceReport.EstimatedMemoryUsageMB);
            _logger.LogInformation("- Parallelization potential: {Potential}%", performanceReport.ParallelizationPotential);

            // Apply intelligent caching
            var cachedPipeline = complexPipeline
                .WithIntelligentCaching<ProcessedData>(CachePolicy.Default);

            // Optimize query plan
            var optimizer = _serviceProvider.GetRequiredService<IAdvancedPipelineOptimizer>();
            var optimizedPipeline = await optimizer.OptimizeQueryPlanAsync(cachedPipeline);

            // Execute with performance profiling
            var profilingOptions = new ProfilingOptions
            {
                EnableDetailedTimings = true,
                TrackMemoryUsage = true,
                EnableBottleneckAnalysis = true
            };

            var profiledPipeline = optimizedPipeline.EnableProfiling(true, profilingOptions);
            var result = await profiledPipeline.ExecutePipelineAsync<IEnumerable<IGrouping<int, ProcessedData>>>();

            // Get performance insights
            var diagnostics = await profiledPipeline.GetDiagnosticsAsync();
            _logger.LogInformation("Optimization Results:");
            _logger.LogInformation("- Total stages: {StageCount}", diagnostics.StageCount);
            _logger.LogInformation("- Execution time: {ExecutionTime}ms", diagnostics.TotalExecutionTimeMs);
            _logger.LogInformation("- Cache hits: {CacheHits}", diagnostics.CacheHitRate);

            _logger.LogInformation("Advanced optimization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Advanced optimization demonstration failed");
            throw;
        }
    }

    /// <summary>
    /// Demonstrates comprehensive error handling and recovery strategies.
    /// </summary>
    public async Task DemonstrateErrorHandlingAsync()
    {
        _logger.LogInformation("Starting error handling demonstration");

        // Create data that will cause various types of errors
        var problematicData = GenerateProblematicDataset();

        try
        {
            // Set up pipeline with comprehensive error handling
            var errorHandlingPipeline = problematicData.AsComputePipeline(_serviceProvider)
                .OnError<SampleData>(async (exception, data) =>
                {
                    _logger.LogWarning("Error processing item {Id}: {Error}", data.Id, exception.Message);
                    
                    // Apply recovery strategy based on error type
                    return await _errorHandler.RecoverFromDataProcessingErrorAsync(exception, data);
                })
                .Retry(maxAttempts: 3, delay: TimeSpan.FromMilliseconds(100))
                .CircuitBreaker(new CircuitBreakerOptions
                {
                    FailureThreshold = 5,
                    RecoveryTimeout = TimeSpan.FromSeconds(30),
                    HalfOpenSuccessThreshold = 2
                })
                .Timeout(TimeSpan.FromMinutes(5));

            // Execute with error monitoring
            var result = await _errorHandler.ExecuteWithErrorHandlingAsync(
                errorHandlingPipeline, 
                PipelineErrorType.DataValidationError | PipelineErrorType.ComputationError);

            _logger.LogInformation("Error handling demonstration completed. Result: {HasValue}", result.HasValue);
        }
        catch (AggregateException ex)
        {
            _logger.LogError("Multiple errors occurred during pipeline execution:");
            foreach (var innerEx in ex.InnerExceptions)
            {
                _logger.LogError(innerEx, "- {ErrorType}: {Message}", innerEx.GetType().Name, innerEx.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling demonstration failed");
            throw;
        }
    }

    /// <summary>
    /// Demonstrates integration with the full DotCompute infrastructure.
    /// </summary>
    public async Task DemonstrateInfrastructureIntegrationAsync()
    {
        _logger.LogInformation("Starting infrastructure integration demonstration");

        var data = GenerateLargeDataset(25000);

        try
        {
            // Get backend recommendations
            var queryable = data.AsQueryable();
            var backendRecommendation = await queryable.RecommendOptimalBackendAsync(_serviceProvider);

            _logger.LogInformation("Backend Recommendation: {Backend} (Score: {Score})", 
                backendRecommendation.RecommendedBackend, backendRecommendation.ConfidenceScore);

            // Estimate memory usage
            var memoryEstimate = await queryable.EstimateMemoryUsageAsync(_serviceProvider);
            _logger.LogInformation("Memory Estimate: {Memory}MB (Peak: {Peak}MB)", 
                memoryEstimate.EstimatedUsageMB, memoryEstimate.PeakUsageMB);

            // Create pipeline with backend selector integration
            var orchestrator = _serviceProvider.GetRequiredService<IComputeOrchestrator>();
            var pipeline = data.AsComputePipeline(_serviceProvider)
                .ThenSelect<SampleData, ProcessedData>(item => new ProcessedData
                {
                    Id = item.Id,
                    ComputedValue = item.Value * 3.14f,
                    Category = item.Category
                })
                .Optimize(OptimizationStrategy.Adaptive);

            // Execute through orchestrator
            var executionContext = new PipelineExecutionContext
            {
                PreferredBackend = backendRecommendation.RecommendedBackend,
                MaxMemoryUsage = memoryEstimate.EstimatedUsageMB * 1024 * 1024,
                EnableProfiling = true,
                Priority = ExecutionPriority.High
            };

            var result = await pipeline.ExecuteWithContextAsync<IEnumerable<ProcessedData>>(
                executionContext, CancellationToken.None);

            _logger.LogInformation("Infrastructure integration completed successfully");
            _logger.LogInformation("- Execution time: {Time}ms", result.ExecutionTime.TotalMilliseconds);
            _logger.LogInformation("- Memory used: {Memory}MB", result.ResourceMetrics.PeakMemoryUsage / 1024 / 1024);
            _logger.LogInformation("- Backend used: {Backend}", result.ExecutionContext.ActualBackend);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Infrastructure integration demonstration failed");
            throw;
        }
    }

    /// <summary>
    /// Runs all pipeline demonstrations in sequence.
    /// </summary>
    public async Task RunAllDemonstrationsAsync()
    {
        _logger.LogInformation("=== Starting Comprehensive Pipeline Demonstrations ===");

        try
        {
            await DemonstrateBasicPipelineAsync();
            await DemonstrateComplexQueryPatternsAsync();
            await DemonstrateStreamingPipelineAsync();
            await DemonstrateAdvancedOptimizationAsync();
            await DemonstrateErrorHandlingAsync();
            await DemonstrateInfrastructureIntegrationAsync();

            _logger.LogInformation("=== All Pipeline Demonstrations Completed Successfully ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline demonstrations failed");
            throw;
        }
    }

    #region Helper Methods and Data Generation

    private SampleData[] GenerateLargeDataset(int size)
    {
        var random = new Random(42);
        return Enumerable.Range(1, size).Select(i => new SampleData
        {
            Id = i,
            Value = (float)(random.NextDouble() * 1000),
            Category = random.Next(0, 10),
            IsActive = random.Next(0, 2) == 1,
            Data = GenerateRandomBytes(random, 64)
        }).ToArray();
    }

    private CategoryData[] GenerateSecondaryDataset(int size)
    {
        var categories = new[]
        {
            "Electronics", "Clothing", "Books", "Food", "Sports",
            "Home", "Garden", "Automotive", "Health", "Beauty"
        };

        return Enumerable.Range(0, size).Select(i => new CategoryData
        {
            CategoryId = i % 10,
            CategoryName = categories[i % categories.Length],
            Multiplier = 1.0f + (i % 5) * 0.1f,
            IsEnabled = i % 3 != 0
        }).ToArray();
    }

    private async IAsyncEnumerable<SampleData> GenerateStreamingData()
    {
        var random = new Random(42);
        
        for (int i = 0; i < 10000; i++)
        {
            await Task.Delay(1, CancellationToken.None); // Simulate streaming delay
            
            yield return new SampleData
            {
                Id = i,
                Value = (float)(random.NextDouble() * 100 + Math.Sin(i * 0.01) * 20),
                Category = random.Next(0, 5),
                IsActive = true,
                Data = GenerateRandomBytes(random, 32)
            };
        }
    }

    private SampleData[] GenerateProblematicDataset()
    {
        var data = new List<SampleData>();
        var random = new Random(42);

        // Add normal data
        for (int i = 0; i < 100; i++)
        {
            data.Add(new SampleData
            {
                Id = i,
                Value = (float)random.NextDouble() * 100,
                Category = random.Next(0, 5),
                IsActive = true
            });
        }

        // Add problematic data that will cause errors
        data.Add(new SampleData { Id = 999, Value = float.NaN, Category = 0, IsActive = true });
        data.Add(new SampleData { Id = 998, Value = float.PositiveInfinity, Category = 1, IsActive = true });
        data.Add(new SampleData { Id = 997, Value = -1000, Category = -1, IsActive = false }); // Invalid category

        return data.ToArray();
    }

    private byte[] GenerateRandomBytes(Random random, int size)
    {
        var bytes = new byte[size];
        random.NextBytes(bytes);
        return bytes;
    }

    #endregion

    #region Data Models

    /// <summary>
    /// Sample data structure for pipeline demonstrations.
    /// </summary>
    public struct SampleData
    {
        public int Id { get; set; }
        public float Value { get; set; }
        public int Category { get; set; }
        public bool IsActive { get; set; }
        // Note: Arrays are not unmanaged, so we remove this field for pipeline compatibility
        // Use separate arrays if needed for specific operations
    }

    /// <summary>
    /// Processed data structure for transformation demonstrations.
    /// </summary>
    public struct ProcessedData
    {
        public int Id { get; set; }
        public float ComputedValue { get; set; }
        public int Category { get; set; }
        // DateTime is not unmanaged, so we use a timestamp instead
        public long ProcessedAtTicks { get; set; }
        
        public DateTime ProcessedAt 
        { 
            get => new DateTime(ProcessedAtTicks);
            set => ProcessedAtTicks = value.Ticks;
        }
    }

    /// <summary>
    /// Category data for join operations.
    /// </summary>
    public struct CategoryData
    {
        public int CategoryId { get; set; }
        // String is not unmanaged, so we use a fixed-size char array or remove it
        // For pipeline operations, we'll work with CategoryId only
        public float Multiplier { get; set; }
        public bool IsEnabled { get; set; }
    }

    /// <summary>
    /// Joined data structure for join demonstrations.
    /// </summary>
    public struct JoinedData
    {
        public int Id { get; set; }
        public float Value { get; set; }
        // String is not unmanaged, using category ID instead
        public int CategoryId { get; set; }
        public float Multiplier { get; set; }
    }

    /// <summary>
    /// Pipeline execution context for infrastructure integration.
    /// </summary>
    public class PipelineExecutionContext : IPipelineExecutionContext
    {
        public Guid ContextId { get; } = Guid.NewGuid();
        public string PreferredBackend { get; set; } = "CPU";
        public string ActualBackend { get; set; } = "CPU";
        public long MaxMemoryUsage { get; set; }
        public bool EnableProfiling { get; set; }
        public ExecutionPriority Priority { get; set; } = ExecutionPriority.Normal;
        
        // IPipelineExecutionContext interface properties
        public bool EnableDetailedMetrics { get; set; } = true;
        public bool TrackMemoryUsage { get; set; } = true;
        public bool CollectTimingData { get; set; } = true;
        
        // Interface implementations (simplified for demo)
        public IPipelineConfiguration Configuration => throw new NotImplementedException();
        public IPipelineResourceManager ResourceManager => throw new NotImplementedException();
        public IPipelineCacheManager CacheManager => throw new NotImplementedException();
        public ITelemetryCollector TelemetryCollector => throw new NotImplementedException();
        public CancellationToken CancellationToken => CancellationToken.None;
        public ILogger Logger => throw new NotImplementedException();
        public IServiceProvider ServiceProvider => throw new NotImplementedException();
        public IReadOnlyDictionary<string, object> Properties => new Dictionary<string, object>();
    }

    #endregion
}

/// <summary>
/// Configuration options for GPU grouping operations.
/// </summary>
public class GpuGroupingOptions
{
    public string PreferredBackend { get; set; } = "CUDA";
    public int BatchSize { get; set; } = 1000;
    public bool EnableLocalReduction { get; set; } = true;
}

/// <summary>
/// Configuration options for GPU join operations.
/// </summary>
public class GpuJoinOptions
{
    public JoinAlgorithm JoinAlgorithm { get; set; } = JoinAlgorithm.HashJoin;
    public bool EnableHashOptimization { get; set; } = true;
}

/// <summary>
/// Join algorithms for GPU operations.
/// </summary>
public enum JoinAlgorithm
{
    HashJoin,
    SortMergeJoin,
    NestedLoopJoin
}

/// <summary>
/// Window configuration options.
/// </summary>
public class WindowOptions
{
    public WindowType WindowType { get; set; } = WindowType.Sliding;
    public EdgeHandling EdgeHandling { get; set; } = EdgeHandling.ZeroPadding;
}

/// <summary>
/// Window types for data processing.
/// </summary>
public enum WindowType
{
    Sliding,
    Tumbling,
    Hopping
}

/// <summary>
/// Edge handling strategies.
/// </summary>
public enum EdgeHandling
{
    ZeroPadding,
    Reflection,
    Truncation
}

/// <summary>
/// Batching configuration options.
/// </summary>
public class BatchingOptions
{
    public int BatchSize { get; set; } = 1000;
    public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromMilliseconds(100);
    public bool EnableBackpressure { get; set; } = true;
}

/// <summary>
/// Anomaly detection configuration options.
/// </summary>
public class AnomalyDetectionOptions
{
    public AnomalyAlgorithm Algorithm { get; set; } = AnomalyAlgorithm.IsolationForest;
    public float Threshold { get; set; } = 0.1f;
    public int WindowSize { get; set; } = 1000;
}

/// <summary>
/// Anomaly detection algorithms.
/// </summary>
public enum AnomalyAlgorithm
{
    IsolationForest,
    OneClassSVM,
    StatisticalOutlier
}

/// <summary>
/// Aggregation configuration options.
/// </summary>
public class AggregationOptions
{
    public AggregationType AggregationType { get; set; } = AggregationType.Sum;
    public TimeSpan WindowSize { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Aggregation types for continuous processing.
/// </summary>
public enum AggregationType
{
    Sum,
    Average,
    MovingAverage,
    Count,
    Min,
    Max
}