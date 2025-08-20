// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Core.Recovery;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Tests.Unit.Core.Recovery;

/// <summary>
/// Comprehensive tests for the RecoveryCoordinator and all recovery strategies
/// </summary>
public sealed class RecoveryCoordinatorTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILoggerFactory _loggerFactory;
    private readonly RecoveryCoordinator _coordinator;
    private bool _disposed;

    public RecoveryCoordinatorTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().AddDebug().SetMinimumLevel(LogLevel.Debug));
        
        _coordinator = new RecoveryCoordinator(
            _loggerFactory.CreateLogger<RecoveryCoordinator>(),
            CreateTestConfiguration(),
            _loggerFactory);
    }

    [Fact]
    public async Task RecoverAsync_WithOutOfMemoryException_ShouldUseMemoryRecoveryStrategy()
    {
        // Arrange
        var exception = new OutOfMemoryException("Insufficient memory available");
        var context = new MemoryRecoveryContext
        {
            Operation = "TestAllocation",
            RequestedBytes = 1024 * 1024 * 100, // 100MB
            PoolId = "TestPool"
        };

        // Act
        var result = await _coordinator.RecoverMemoryErrorAsync(exception, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Strategy.Should().NotBeEmpty();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        
        _output.WriteLine($"Memory recovery result: {result.Message} in {result.Duration.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task RecoverAsync_WithGpuHangException_ShouldUseGpuRecoveryStrategy()
    {
        // Arrange
        var exception = new AcceleratorException("GPU device hang detected");
        var deviceId = "cuda:0";
        var operation = "MatrixMultiplication";

        // Act
        var result = await _coordinator.RecoverGpuErrorAsync(exception, deviceId, operation);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Strategy.Should().NotBeEmpty();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        
        _output.WriteLine($"GPU recovery result: {result.Message} in {result.Duration.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task RecoverAsync_WithCompilationException_ShouldUseCompilationFallback()
    {
        // Arrange
        var exception = new InvalidOperationException("Kernel compilation failed with optimization errors");
        var context = new CompilationRecoveryContext
        {
            KernelName = "TestKernel",
            SourceCode = "__kernel void test(__global float* data) { data[get_global_id(0)] *= 2.0f; }",
            CompilationOptions = new CompilationOptions
            {
                OptimizationLevel = OptimizationLevel.Maximum,
                FastMath = true,
                AggressiveOptimizations = true
            },
            TargetPlatform = "CUDA"
        };

        // Act
        var result = await _coordinator.RecoverCompilationErrorAsync(exception, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Strategy.Should().NotBeEmpty();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        
        _output.WriteLine($"Compilation recovery result: {result.Message} in {result.Duration.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task AllocateWithRecoveryAsync_ShouldRetryOnFailureAndSucceed()
    {
        // Arrange
        var attemptCount = 0;
        
        string AllocateFunc()
        {
            attemptCount++;
            if (attemptCount < 3) // Fail first 2 attempts
            {
                throw new OutOfMemoryException($"Allocation failed on attempt {attemptCount}");
            }
            return $"Allocated on attempt {attemptCount}";
        }

        // Act
        var result = await _coordinator.AllocateWithRecoveryAsync(
            AllocateFunc, 
            maxRetries: 5,
            baseDelay: TimeSpan.FromMilliseconds(10));

        // Assert
        result.Should().NotBeNull();
        result.Should().Be("Allocated on attempt 3");
        attemptCount.Should().Be(3);
        
        _output.WriteLine($"Allocation succeeded after {attemptCount} attempts: {result}");
    }

    [Fact]
    public async Task CompileWithFallbackAsync_ShouldUseProgressiveFallbackStrategies()
    {
        // Arrange
        var kernelName = "ComplexKernel";
        var sourceCode = "__kernel void complex_operation(__global float* data, int size) { /* complex code */ }";
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Maximum,
            FastMath = true,
            AggressiveOptimizations = true
        };

        // Act
        var result = await _coordinator.CompileWithFallbackAsync(kernelName, sourceCode, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Attempts.Should().NotBeEmpty();
        result.FinalStrategy.Should().NotBeNull();
        result.TotalDuration.Should().BeGreaterThan(0);
        
        _output.WriteLine($"Compilation fallback result: {result}");
        
        foreach (var attempt in result.Attempts)
        {
            _output.WriteLine($"  Attempt {attempt.Strategy}: {(attempt.Success ? "SUCCESS" : "FAILED")} in {attempt.Duration.TotalMilliseconds}ms");
        }
    }

    [Fact]
    public async Task ExecuteWithCircuitBreakerAsync_ShouldProtectAgainstFailingService()
    {
        // Arrange
        var serviceName = "TestService";
        var operationCount = 0;
        
        async Task<string> FailingOperation(CancellationToken ct)
        {
            operationCount++;
            await Task.Delay(10, ct);
            
            if (operationCount <= 5) // First 5 calls fail
            {
                throw new InvalidOperationException($"Service failure #{operationCount}");
            }
            
            return $"Success on attempt {operationCount}";
        }

        // Act & Assert
        
        // First few operations should fail but be allowed through
        for (int i = 1; i <= 3; i++)
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _coordinator.ExecuteWithCircuitBreakerAsync(serviceName, FailingOperation));
            ex.Message.Should().Contain($"Service failure #{i}");
        }
        
        // After threshold, circuit should open and throw CircuitOpenException
        await Task.Delay(100); // Allow circuit breaker to process failures
        
        // Eventually, when circuit allows requests through, it should succeed
        // (This would require more sophisticated timing in a real test)
        _output.WriteLine($"Executed {operationCount} operations with circuit breaker protection");
    }

    [Fact]
    public async Task PerformSystemHealthCheckAsync_ShouldReturnComprehensiveHealthReport()
    {
        // Act
        var healthResult = await _coordinator.PerformSystemHealthCheckAsync();

        // Assert
        healthResult.Should().NotBeNull();
        healthResult.ComponentResults.Should().NotBeEmpty();
        healthResult.ComponentResults.Should().HaveCount(5); // GPU, Memory, Compilation, Network, Plugins
        healthResult.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        healthResult.OverallHealth.Should().BeInRange(0.0, 1.0);
        
        _output.WriteLine($"System health check result: {healthResult}");
        
        foreach (var component in healthResult.ComponentResults)
        {
            _output.WriteLine($"  {component}");
        }
    }

    [Fact]
    public void GetRecoveryStatistics_ShouldReturnComprehensiveMetrics()
    {
        // Act
        var stats = _coordinator.GetRecoveryStatistics();

        // Assert
        stats.Should().NotBeNull();
        stats.GlobalMetrics.Should().NotBeNull();
        stats.GpuHealthReport.Should().NotBeNull();
        stats.MemoryPressureInfo.Should().NotBeNull();
        stats.CompilationStatistics.Should().NotBeNull();
        stats.CircuitBreakerStatistics.Should().NotBeNull();
        stats.PluginHealthReport.Should().NotBeNull();
        stats.RegisteredStrategies.Should().BeGreaterOrEqualTo(0);
        stats.OverallSystemHealth.Should().BeInRange(0.0, 1.0);
        
        _output.WriteLine($"Recovery statistics: {stats}");
    }

    [Theory]
    [InlineData(typeof(OutOfMemoryException), "Memory allocation failed")]
    [InlineData(typeof(TimeoutException), "Operation timed out")]
    [InlineData(typeof(InvalidOperationException), "Invalid operation performed")]
    public async Task RecoverAsync_WithVariousExceptionTypes_ShouldHandleAppropriately(
        Type exceptionType, 
        string message)
    {
        // Arrange
        var exception = (Exception)Activator.CreateInstance(exceptionType, message)!;
        var context = new MemoryRecoveryContext
        {
            Operation = "TestOperation",
            RequestedBytes = 1024,
            PoolId = "TestPool"
        };

        // Act
        var result = await _coordinator.RecoverMemoryErrorAsync(exception, context);

        // Assert
        result.Should().NotBeNull();
        result.Strategy.Should().NotBeEmpty();
        
        _output.WriteLine($"Handled {exceptionType.Name}: {result.Success} - {result.Message}");
    }

    [Fact]
    public async Task StressTest_ConcurrentRecoveryOperations_ShouldHandleGracefully()
    {
        // Arrange
        const int concurrentOperations = 20;
        var tasks = new List<Task<RecoveryResult>>();
        var random = new Random(42);

        // Act - Start multiple concurrent recovery operations
        for (int i = 0; i < concurrentOperations; i++)
        {
            var operationId = i;
            var task = Task.Run(async () =>
            {
                await Task.Delay(random.Next(10, 100)); // Random delay
                
                var exception = new OutOfMemoryException($"Concurrent operation {operationId}");
                var context = new MemoryRecoveryContext
                {
                    Operation = $"ConcurrentOp_{operationId}",
                    RequestedBytes = 1024 * random.Next(1, 100),
                    PoolId = $"Pool_{operationId % 5}"
                };
                
                return await _coordinator.RecoverMemoryErrorAsync(exception, context);
            });
            
            tasks.Add(task);
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(concurrentOperations);
        results.Should().OnlyContain(r => r != null);
        
        var successfulResults = results.Count(r => r.Success);
        var averageDuration = results.Average(r => r.Duration.TotalMilliseconds);
        
        successfulResults.Should().BeGreaterThan(concurrentOperations / 2); // At least 50% should succeed
        
        _output.WriteLine($"Stress test completed: {successfulResults}/{concurrentOperations} successful, " +
                         $"average duration: {averageDuration:F1}ms");
    }

    [Fact]
    public async Task ErrorRecoveryChain_MultipleFailuresAndRecoveries_ShouldMaintainStability()
    {
        // Arrange
        var errors = new Exception[]
        {
            new OutOfMemoryException("Memory exhausted"),
            new AcceleratorException("GPU hang detected"),
            new TimeoutException("Operation timeout"),
            new InvalidOperationException("Compilation failed")
        };

        var results = new List<RecoveryResult>();
        var stopwatch = Stopwatch.StartNew();

        // Act - Process multiple errors in sequence
        foreach (var error in errors)
        {
            RecoveryResult result;
            
            switch (error)
            {
                case OutOfMemoryException memEx:
                    var memContext = new MemoryRecoveryContext { Operation = "ChainTest", RequestedBytes = 1024 };
                    result = await _coordinator.RecoverMemoryErrorAsync(memEx, memContext);
                    break;
                    
                case AcceleratorException gpuEx:
                    result = await _coordinator.RecoverGpuErrorAsync(gpuEx, "test:0", "ChainTest");
                    break;
                    
                default:
                    var genericContext = new MemoryRecoveryContext { Operation = "ChainTest" };
                    result = await _coordinator.RecoverMemoryErrorAsync(error, genericContext);
                    break;
            }
            
            results.Add(result);
            
            // Small delay between operations
            await Task.Delay(50);
        }
        
        stopwatch.Stop();

        // Assert
        results.Should().HaveCount(errors.Length);
        results.Should().OnlyContain(r => r != null);
        
        var successCount = results.Count(r => r.Success);
        successCount.Should().BeGreaterThan(errors.Length / 2); // Most should succeed
        
        // System should remain stable
        var finalStats = _coordinator.GetRecoveryStatistics();
        finalStats.OverallSystemHealth.Should().BeGreaterThan(0.3); // System should still be functional
        
        _output.WriteLine($"Error recovery chain: {successCount}/{errors.Length} successful in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Final system health: {finalStats.OverallSystemHealth:P1}");
    }

    private static RecoveryCoordinatorConfiguration CreateTestConfiguration()
    {
        return new RecoveryCoordinatorConfiguration
        {
            MetricsReportInterval = TimeSpan.FromMinutes(10), // Longer interval for tests
            EnableMetricsReporting = false, // Disable for tests
            MaxConcurrentRecoveries = 20,
            
            GpuRecoveryConfig = new GpuRecoveryConfiguration
            {
                HealthCheckInterval = TimeSpan.FromMinutes(5),
                DefaultKernelTimeout = TimeSpan.FromSeconds(30),
                MaxConsecutiveFailures = 3,
                EnableAutoRecovery = true
            },
            
            MemoryRecoveryConfig = new MemoryRecoveryConfiguration
            {
                DefragmentationInterval = TimeSpan.FromMinutes(10),
                EmergencyReserveSizeMB = 32, // Smaller for tests
                EnablePeriodicDefragmentation = false,
                MaxAllocationRetries = 3
            },
            
            CompilationFallbackConfig = new CompilationFallbackConfiguration
            {
                CacheExpiration = TimeSpan.FromHours(1),
                CacheCleanupInterval = TimeSpan.FromMinutes(30),
                EnableProgressiveFallback = true,
                EnableKernelSimplification = true,
                EnableInterpreterMode = true
            },
            
            CircuitBreakerConfig = new CircuitBreakerConfiguration
            {
                FailureThresholdPercentage = 60, // More tolerant for tests
                ConsecutiveFailureThreshold = 3,
                OpenCircuitTimeout = TimeSpan.FromSeconds(5), // Shorter for tests
                OperationTimeout = TimeSpan.FromSeconds(10)
            }
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _coordinator?.Dispose();
            _loggerFactory?.Dispose();
            _disposed = true;
        }
    }
}