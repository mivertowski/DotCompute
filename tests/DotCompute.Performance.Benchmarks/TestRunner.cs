using System.Text.Json;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DotCompute.Performance.Benchmarks.Analysis;
using DotCompute.Performance.Benchmarks.Benchmarks;
using DotCompute.Performance.Benchmarks.Config;
using DotCompute.Performance.Benchmarks.Stress;

namespace DotCompute.Performance.Benchmarks;

/// <summary>
/// Comprehensive test runner for all performance benchmarks and stress tests
/// </summary>
public class TestRunner
{
    private readonly ILogger<TestRunner> _logger;
    private readonly PerformanceAnalyzer _analyzer;
    private readonly IServiceProvider _serviceProvider;
    private readonly BenchmarkTargets _targets;

    public TestRunner(ILogger<TestRunner> logger, PerformanceAnalyzer analyzer, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _analyzer = analyzer;
        _serviceProvider = serviceProvider;
        _targets = LoadBenchmarkTargets();
    }

    public static async Task<int> Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        var runner = host.Services.GetRequiredService<TestRunner>();
        
        try
        {
            await runner.RunAllTests();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test run failed: {ex.Message}");
            return 1;
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.AddDebug();
                });
                
                services.AddSingleton<PerformanceAnalyzer>();
                services.AddSingleton<TestRunner>();
                services.AddSingleton<MemoryLeakStressTest>();
                services.AddSingleton<ConcurrentAllocationStressTest>();
                services.AddSingleton<HighFrequencyTransferStressTest>();
            });

    public async Task RunAllTests()
    {
        _logger.LogInformation("Starting comprehensive performance test suite");
        
        var testResults = new List<TestResult>();
        
        // Phase 1: Basic Benchmarks
        _logger.LogInformation("=== Phase 1: Basic Benchmarks ===");
        testResults.Add(await RunMemoryAllocationBenchmarks());
        testResults.Add(await RunVectorizationBenchmarks());
        testResults.Add(await RunTransferBenchmarks());
        testResults.Add(await RunThreadPoolBenchmarks());
        
        // Phase 2: Stress Tests
        _logger.LogInformation("=== Phase 2: Stress Tests ===");
        testResults.Add(await RunMemoryLeakStressTest());
        testResults.Add(await RunConcurrentAllocationStressTest());
        testResults.Add(await RunHighFrequencyTransferStressTest());
        
        // Phase 3: Analysis and Reporting
        _logger.LogInformation("=== Phase 3: Analysis and Reporting ===");
        await GenerateComprehensiveReport(testResults);
        
        _logger.LogInformation("Performance test suite completed successfully");
    }

    private async Task<TestResult> RunMemoryAllocationBenchmarks()
    {
        _logger.LogInformation("Running memory allocation benchmarks...");
        
        var profile = await _analyzer.StartProfiling("MemoryAllocation", TimeSpan.FromMinutes(5));
        
        try
        {
            var summary = BenchmarkRunner.Run<MemoryAllocationBenchmarks>(BenchmarkConfig.GetMemoryIntensiveConfig());
            var bottlenecks = await _analyzer.AnalyzeBottlenecks(profile);
            
            return new TestResult
            {
                TestName = "MemoryAllocation",
                Success = summary.HasCriticalValidationErrors == false,
                Summary = summary,
                BottleneckAnalysis = bottlenecks,
                Recommendations = GenerateMemoryAllocationRecommendations(summary, bottlenecks)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory allocation benchmarks failed");
            return new TestResult
            {
                TestName = "MemoryAllocation",
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<TestResult> RunVectorizationBenchmarks()
    {
        _logger.LogInformation("Running vectorization benchmarks...");
        
        var profile = await _analyzer.StartProfiling("Vectorization", TimeSpan.FromMinutes(5));
        
        try
        {
            var summary = BenchmarkRunner.Run<VectorizationBenchmarks>(BenchmarkConfig.GetVectorizationConfig());
            var bottlenecks = await _analyzer.AnalyzeBottlenecks(profile);
            
            return new TestResult
            {
                TestName = "Vectorization",
                Success = summary.HasCriticalValidationErrors == false,
                Summary = summary,
                BottleneckAnalysis = bottlenecks,
                Recommendations = GenerateVectorizationRecommendations(summary, bottlenecks)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vectorization benchmarks failed");
            return new TestResult
            {
                TestName = "Vectorization",
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<TestResult> RunTransferBenchmarks()
    {
        _logger.LogInformation("Running transfer benchmarks...");
        
        var profile = await _analyzer.StartProfiling("Transfer", TimeSpan.FromMinutes(5));
        
        try
        {
            var summary = BenchmarkRunner.Run<TransferBenchmarks>(BenchmarkConfig.GetThroughputConfig());
            var bottlenecks = await _analyzer.AnalyzeBottlenecks(profile);
            
            return new TestResult
            {
                TestName = "Transfer",
                Success = summary.HasCriticalValidationErrors == false,
                Summary = summary,
                BottleneckAnalysis = bottlenecks,
                Recommendations = GenerateTransferRecommendations(summary, bottlenecks)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transfer benchmarks failed");
            return new TestResult
            {
                TestName = "Transfer",
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<TestResult> RunThreadPoolBenchmarks()
    {
        _logger.LogInformation("Running thread pool benchmarks...");
        
        var profile = await _analyzer.StartProfiling("ThreadPool", TimeSpan.FromMinutes(5));
        
        try
        {
            var summary = BenchmarkRunner.Run<ThreadPoolBenchmarks>(BenchmarkConfig.GetDefaultConfig());
            var bottlenecks = await _analyzer.AnalyzeBottlenecks(profile);
            
            return new TestResult
            {
                TestName = "ThreadPool",
                Success = summary.HasCriticalValidationErrors == false,
                Summary = summary,
                BottleneckAnalysis = bottlenecks,
                Recommendations = GenerateThreadPoolRecommendations(summary, bottlenecks)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Thread pool benchmarks failed");
            return new TestResult
            {
                TestName = "ThreadPool",
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<TestResult> RunMemoryLeakStressTest()
    {
        _logger.LogInformation("Running 24-hour memory leak stress test (shortened for demo)...");
        
        try
        {
            var stressTest = _serviceProvider.GetRequiredService<MemoryLeakStressTest>();
            
            // Run for 1 hour instead of 24 hours for demo purposes
            var duration = TimeSpan.FromHours(1);
            await stressTest.RunStressTest(duration);
            
            return new TestResult
            {
                TestName = "MemoryLeakStress",
                Success = true,
                Recommendations = new List<string>
                {
                    "Memory leak test completed successfully",
                    "Review detailed memory usage reports in Analysis folder",
                    "Monitor for memory growth patterns over extended periods"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory leak stress test failed");
            return new TestResult
            {
                TestName = "MemoryLeakStress",
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<TestResult> RunConcurrentAllocationStressTest()
    {
        _logger.LogInformation("Running concurrent allocation stress test...");
        
        try
        {
            var stressTest = _serviceProvider.GetRequiredService<ConcurrentAllocationStressTest>();
            
            var threadCount = Environment.ProcessorCount * 2;
            var duration = TimeSpan.FromMinutes(30);
            
            await stressTest.RunStressTest(threadCount, duration);
            
            return new TestResult
            {
                TestName = "ConcurrentAllocationStress",
                Success = true,
                Recommendations = new List<string>
                {
                    "Concurrent allocation stress test completed successfully",
                    "Review allocation patterns and failure rates",
                    "Consider implementing back-pressure mechanisms for high-load scenarios"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Concurrent allocation stress test failed");
            return new TestResult
            {
                TestName = "ConcurrentAllocationStress",
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<TestResult> RunHighFrequencyTransferStressTest()
    {
        _logger.LogInformation("Running high-frequency transfer stress test...");
        
        try
        {
            var stressTest = _serviceProvider.GetRequiredService<HighFrequencyTransferStressTest>();
            
            var producerCount = Environment.ProcessorCount / 2;
            var consumerCount = Environment.ProcessorCount / 2;
            var duration = TimeSpan.FromMinutes(30);
            
            await stressTest.RunStressTest(producerCount, consumerCount, duration);
            
            return new TestResult
            {
                TestName = "HighFrequencyTransferStress",
                Success = true,
                Recommendations = new List<string>
                {
                    "High-frequency transfer stress test completed successfully",
                    "Review transfer patterns and throughput metrics",
                    "Consider optimizing transfer mechanisms for sustained high-frequency loads"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "High-frequency transfer stress test failed");
            return new TestResult
            {
                TestName = "HighFrequencyTransferStress",
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private List<string> GenerateMemoryAllocationRecommendations(BenchmarkDotNet.Reports.Summary summary, BottleneckAnalysis bottlenecks)
    {
        var recommendations = new List<string>();
        
        // Add specific recommendations based on benchmark results
        recommendations.Add("Consider using ArrayPool<T> for frequently allocated arrays");
        recommendations.Add("Use stackalloc for small, short-lived arrays");
        recommendations.Add("Implement object pooling for expensive-to-create objects");
        
        if (bottlenecks.MemoryBottlenecks?.IsMemoryBottleneck == true)
        {
            recommendations.AddRange(bottlenecks.MemoryBottlenecks.Recommendations);
        }
        
        return recommendations;
    }

    private List<string> GenerateVectorizationRecommendations(BenchmarkDotNet.Reports.Summary summary, BottleneckAnalysis bottlenecks)
    {
        var recommendations = new List<string>();
        
        recommendations.Add("Use System.Numerics.Vector<T> for cross-platform vectorization");
        recommendations.Add("Consider using System.Runtime.Intrinsics for platform-specific optimizations");
        recommendations.Add("Ensure data alignment for optimal vectorization performance");
        recommendations.Add("Profile with disassembly to verify vectorization is occurring");
        
        return recommendations;
    }

    private List<string> GenerateTransferRecommendations(BenchmarkDotNet.Reports.Summary summary, BottleneckAnalysis bottlenecks)
    {
        var recommendations = new List<string>();
        
        recommendations.Add("Use Memory<T> and Span<T> for efficient memory operations");
        recommendations.Add("Consider using channels for producer-consumer scenarios");
        recommendations.Add("Implement batching for small, frequent transfers");
        recommendations.Add("Use async operations for I/O-bound transfers");
        
        return recommendations;
    }

    private List<string> GenerateThreadPoolRecommendations(BenchmarkDotNet.Reports.Summary summary, BottleneckAnalysis bottlenecks)
    {
        var recommendations = new List<string>();
        
        recommendations.Add("Use Task.Run for CPU-bound work");
        recommendations.Add("Use async/await for I/O-bound operations");
        recommendations.Add("Consider custom task schedulers for specialized workloads");
        recommendations.Add("Monitor thread pool starvation in high-load scenarios");
        
        if (bottlenecks.ThreadPoolBottlenecks?.IsThreadPoolBottleneck == true)
        {
            recommendations.AddRange(bottlenecks.ThreadPoolBottlenecks.Recommendations);
        }
        
        return recommendations;
    }

    private async Task GenerateComprehensiveReport(List<TestResult> testResults)
    {
        var report = new
        {
            TestSuiteInfo = new
            {
                RunTime = DateTime.UtcNow,
                Environment = new
                {
                    ProcessorCount = Environment.ProcessorCount,
                    WorkingSet = Environment.WorkingSet,
                    OSVersion = Environment.OSVersion.ToString(),
                    RuntimeVersion = Environment.Version.ToString()
                },
                Targets = _targets
            },
            TestResults = testResults.Select(r => new
            {
                r.TestName,
                r.Success,
                r.ErrorMessage,
                r.Recommendations,
                BenchmarkSummary = r.Summary?.Reports?.Count() ?? 0,
                HasBottlenecks = r.BottleneckAnalysis?.MemoryBottlenecks?.IsMemoryBottleneck == true ||
                                r.BottleneckAnalysis?.CpuBottlenecks?.IsCpuBottleneck == true ||
                                r.BottleneckAnalysis?.ThreadPoolBottlenecks?.IsThreadPoolBottleneck == true
            }).ToArray(),
            OverallStatus = new
            {
                TotalTests = testResults.Count,
                PassedTests = testResults.Count(r => r.Success),
                FailedTests = testResults.Count(r => !r.Success),
                SuccessRate = testResults.Count(r => r.Success) / (double)testResults.Count * 100
            },
            Recommendations = testResults.SelectMany(r => r.Recommendations).Distinct().ToArray()
        };
        
        _logger.LogInformation("Comprehensive Performance Test Report: {@Report}", report);
        
        // Save detailed report to file
        var reportPath = Path.Combine("Analysis", $"comprehensive-performance-report-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        }));
        
        _logger.LogInformation("Comprehensive performance report saved to: {ReportPath}", reportPath);
    }

    private BenchmarkTargets LoadBenchmarkTargets()
    {
        try
        {
            var json = File.ReadAllText("Config/BenchmarkTargets.json");
            return JsonSerializer.Deserialize<BenchmarkTargets>(json) ?? new BenchmarkTargets();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to load benchmark targets: {Error}", ex.Message);
            return new BenchmarkTargets();
        }
    }
}

public class TestResult
{
    public string TestName { get; set; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public BenchmarkDotNet.Reports.Summary? Summary { get; set; }
    public BottleneckAnalysis? BottleneckAnalysis { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

public class BenchmarkTargets
{
    public PerformanceTargets? PerformanceTargets { get; set; }
    public StressTestTargets? StressTestTargets { get; set; }
    public BenchmarkSettings? BenchmarkSettings { get; set; }
    public HardwareBaselines? HardwareBaselines { get; set; }
}

public class PerformanceTargets
{
    public MemoryTargets? Memory { get; set; }
    public VectorizationTargets? Vectorization { get; set; }
    public TransferTargets? Transfer { get; set; }
    public ThreadPoolTargets? ThreadPool { get; set; }
}

public class MemoryTargets
{
    public AllocationSpeedTargets? AllocationSpeed { get; set; }
    public ThroughputTargets? Throughput { get; set; }
}

public class AllocationSpeedTargets
{
    public Target? ManagedArray { get; set; }
    public Target? ArrayPool { get; set; }
    public Target? NativeMemory { get; set; }
}

public class ThroughputTargets
{
    public Target? SmallAllocations { get; set; }
    public Target? LargeAllocations { get; set; }
}

public class VectorizationTargets
{
    public SpeedupTargets? Speedup { get; set; }
    public ThroughputTargets? Throughput { get; set; }
}

public class SpeedupTargets
{
    public Target? Addition { get; set; }
    public Target? Multiplication { get; set; }
    public Target? DotProduct { get; set; }
}

public class TransferTargets
{
    public BandwidthTargets? Bandwidth { get; set; }
    public LatencyTargets? Latency { get; set; }
}

public class BandwidthTargets
{
    public Target? MemoryToMemory { get; set; }
    public Target? HostToDevice { get; set; }
}

public class LatencyTargets
{
    public Target? SmallTransfers { get; set; }
    public Target? LargeTransfers { get; set; }
}

public class ThreadPoolTargets
{
    public EfficiencyTargets? Efficiency { get; set; }
    public ScalabilityTargets? Scalability { get; set; }
}

public class EfficiencyTargets
{
    public Target? TaskSpawning { get; set; }
    public Target? WorkItemProcessing { get; set; }
}

public class ScalabilityTargets
{
    public Target? ConcurrentTasks { get; set; }
    public Target? QueueThroughput { get; set; }
}

public class StressTestTargets
{
    public MemoryLeakTarget? MemoryLeak { get; set; }
    public ConcurrentAllocationTarget? ConcurrentAllocation { get; set; }
    public HighFrequencyTransferTarget? HighFrequencyTransfer { get; set; }
}

public class MemoryLeakTarget
{
    public string? Duration { get; set; }
    public string? MaxMemoryGrowth { get; set; }
    public string? MaxGen2Collections { get; set; }
    public string? Description { get; set; }
}

public class ConcurrentAllocationTarget
{
    public int ThreadCount { get; set; }
    public string? Duration { get; set; }
    public string? MaxFailureRate { get; set; }
    public string? MinThroughput { get; set; }
    public string? Description { get; set; }
}

public class HighFrequencyTransferTarget
{
    public int ProducerCount { get; set; }
    public int ConsumerCount { get; set; }
    public string? Duration { get; set; }
    public string? MaxLatency { get; set; }
    public string? MinThroughput { get; set; }
    public string? Description { get; set; }
}

public class BenchmarkSettings
{
    public int WarmupIterations { get; set; }
    public int MeasurementIterations { get; set; }
    public int InvocationCount { get; set; }
    public int UnrollFactor { get; set; }
    public string? Strategy { get; set; }
    public string? Platform { get; set; }
    public string? Runtime { get; set; }
    public bool GcServer { get; set; }
    public bool GcConcurrent { get; set; }
}

public class HardwareBaselines
{
    public int CpuCores { get; set; }
    public int MemoryGB { get; set; }
    public int L1CacheKB { get; set; }
    public int L2CacheMB { get; set; }
    public int L3CacheMB { get; set; }
    public double MemoryBandwidthGBps { get; set; }
    public string? Description { get; set; }
}

public class Target
{
    public string? TargetValue { get; set; }
    public string? Description { get; set; }
}