// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;
using Moq;
using System;

namespace DotCompute.Core.Tests;

/// <summary>
/// Comprehensive error handling and recovery tests for DotCompute accelerators.
/// Tests both synchronous and asynchronous error scenarios with proper recovery mechanisms.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ErrorHandling")]
public sealed class ErrorHandlingTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IUnifiedMemoryManager> _mockMemory;
    private readonly List<TestErrorAccelerator> _accelerators = [];
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the ErrorHandlingTests class.
    /// </summary>

    public ErrorHandlingTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockMemory = new Mock<IUnifiedMemoryManager>();
    }
    /// <summary>
    /// Gets device reset_ recovery_ should restore operations after reset.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #region Device Error Tests

    [Fact]
    [Trait("TestType", "DeviceErrors")]
    [Trait("Scenario", "DeviceReset")]
    public async Task DeviceReset_Recovery_ShouldRestoreOperationsAfterReset()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.SimulateDeviceError = true;
        accelerator.AllowRecovery = true;

        // Act & Assert - First call should fail
        var definition = new KernelDefinition("device_reset_test", "__kernel void test() {}", "test");


        var act = async () => await accelerator.CompileKernelAsync(definition);
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Device error*");

        // Enable recovery
        accelerator.SimulateDeviceError = false;

        // Second call should succeed after device reset

        var result = await accelerator.CompileKernelAsync(definition);
        _ = result.Should().NotBeNull();
        _ = accelerator.DeviceResetCount.Should().Be(1);
    }
    /// <summary>
    /// Gets hardware failure_ simulation_ should trigger circuit breaker.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "DeviceErrors")]
    [Trait("Scenario", "HardwareFailure")]
    public async Task HardwareFailure_Simulation_ShouldTriggerCircuitBreaker()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableCircuitBreaker = true;
        accelerator.CircuitBreakerThreshold = 3;
        accelerator.SimulateHardwareFailure = true;

        var definition = new KernelDefinition("hardware_failure_test", "__kernel void test() {}", "test");

        // Act - Trigger multiple failures to open circuit breaker
        var tasks = new List<Task>();
        for (var i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    _ = await accelerator.CompileKernelAsync(definition);
                }
                catch
                {
                    // Expected failures
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Circuit breaker should be open
        _ = accelerator.CircuitBreakerState.Should().Be(CircuitBreakerState.Open);

        // New requests should fail immediately

        var act = async () => await accelerator.CompileKernelAsync(definition);
        _ = await act.Should().ThrowAsync<CircuitBreakerOpenException>();
    }
    /// <summary>
    /// Gets driver errors_ various types_ should be handled appropriately.
    /// </summary>
    /// <param name="errorType">The error type.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>The result of the operation.</returns>

    [Theory]
    [InlineData("DriverError", "Driver communication failed")]
    [InlineData("DeviceDisconnected", "Device has been disconnected")]
    [InlineData("ThermalThrottling", "Device overheating detected")]
    [Trait("TestType", "DeviceErrors")]
    [Trait("Scenario", "DriverErrors")]
    public async Task DriverErrors_VariousTypes_ShouldBeHandledAppropriately(
        string errorType, string errorMessage)
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.CustomErrorMessage = errorMessage;
        accelerator.SimulateDriverError = true;

        var definition = new KernelDefinition($"driver_error_{errorType}", "__kernel void test() {}", "test");

        // Act & Assert
        var act = async () => await accelerator.CompileKernelAsync(definition);
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{errorMessage}*");

        // Verify error logging - check that compilation failure was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to compile kernel", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    /// <summary>
    /// Gets kernel launch failure_ with retry_ should retry and eventually succeed.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Kernel Error Tests

    [Fact]
    [Trait("TestType", "KernelErrors")]
    [Trait("Scenario", "LaunchFailure")]
    public async Task KernelLaunchFailure_WithRetry_ShouldRetryAndEventuallySucceed()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableRetryPolicy = true;
        accelerator.MaxRetryAttempts = 3;
        accelerator.SimulateKernelLaunchFailure = true;
        accelerator.FailureCountBeforeSuccess = 2; // Succeed on 3rd attempt

        var definition = new KernelDefinition("launch_failure_test", "__kernel void test() {}", "test");

        // Act
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        _ = result.Should().NotBeNull();
        _ = accelerator.RetryAttemptCount.Should().Be(2);
        _ = accelerator.LastSuccessfulCompilation.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
    /// <summary>
    /// Gets invalid kernel parameters_ validation_ should throw validation exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "KernelErrors")]
    [Trait("Scenario", "InvalidParameters")]
    public async Task InvalidKernelParameters_Validation_ShouldThrowValidationException()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        var definition = new KernelDefinition("", null!, null!); // Invalid parameters

        // Act & Assert
        var act = async () => await accelerator.CompileKernelAsync(definition);
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Kernel validation failed*");
    }
    /// <summary>
    /// Gets kernel compilation_ timeout_ should cancel operation gracefully.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "KernelErrors")]
    [Trait("Scenario", "Timeout")]
    public async Task KernelCompilation_Timeout_ShouldCancelOperationGracefully()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.CompilationDelay = TimeSpan.FromSeconds(5);

        var definition = new KernelDefinition("timeout_test", "__kernel void test() {}", "test");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        var act = async () => await accelerator.CompileKernelAsync(definition, cancellationToken: cts.Token);
        _ = await act.Should().ThrowAsync<OperationCanceledException>();

        _ = accelerator.CompilationCancelled.Should().BeTrue();
    }
    /// <summary>
    /// Gets kernel stack overflow_ detection_ should throw with diagnostics.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "KernelErrors")]
    [Trait("Scenario", "StackOverflow")]
    public async Task KernelStackOverflow_Detection_ShouldThrowWithDiagnostics()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.SimulateStackOverflow = true;

        var definition = new KernelDefinition("stack_overflow_test",

            "__kernel void recursive() { recursive(); }", "test");

        // Act & Assert
        var act = async () => await accelerator.CompileKernelAsync(definition);
        _ = await act.Should().ThrowAsync<StackOverflowException>();

        // Verify diagnostic information is captured
        _ = accelerator.LastStackOverflowInfo.Should().NotBeNull();
        _ = accelerator.LastStackOverflowInfo!.KernelName.Should().Be("stack_overflow_test");
    }
    /// <summary>
    /// Gets memory allocation_ failure_ should trigger cleanup and retry.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Memory Error Tests

    [Fact]
    [Trait("TestType", "MemoryErrors")]
    [Trait("Scenario", "AllocationFailure")]
    public async Task MemoryAllocation_Failure_ShouldTriggerCleanupAndRetry()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableMemoryRecovery = true;
        accelerator.SimulateMemoryAllocationFailure = true;
        accelerator.MemoryFailureCountBeforeSuccess = 1;

        var definition = new KernelDefinition("memory_test", "__kernel void test() {}", "test");

        // Act
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        _ = result.Should().NotBeNull();
        _ = accelerator.MemoryCleanupCount.Should().BeGreaterThan(0);
        _ = accelerator.GarbageCollectionTriggered.Should().BeTrue();
    }
    /// <summary>
    /// Gets memory transfer_ error_ should retry with different strategy.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "MemoryErrors")]
    [Trait("Scenario", "TransferError")]
    public async Task MemoryTransfer_Error_ShouldRetryWithDifferentStrategy()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.SimulateMemoryTransferError = true;
        accelerator.EnableAlternativeTransferStrategy = true;

        var definition = new KernelDefinition("transfer_error_test", "__kernel void test() {}", "test");

        // Act
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        _ = result.Should().NotBeNull();
        _ = accelerator.AlternativeTransferUsed.Should().BeTrue();
    }
    /// <summary>
    /// Gets memory corruption_ detection_ should invalidate cache and retry.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "MemoryErrors")]
    [Trait("Scenario", "Corruption")]
    public async Task MemoryCorruption_Detection_ShouldInvalidateCacheAndRetry()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableMemoryCorruptionDetection = true;
        accelerator.SimulateMemoryCorruption = true;

        var definition = new KernelDefinition("corruption_test", "__kernel void test() {}", "test");

        // Compile once to cache
        _ = await accelerator.CompileKernelAsync(definition);

        // Enable corruption simulation

        accelerator.TriggerMemoryCorruption = true;

        // Act - Second compilation should detect corruption
        var act = async () => await accelerator.CompileKernelAsync(definition);
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Memory corruption detected*");

        // Assert
        _ = accelerator.CacheInvalidatedDueToCorruption.Should().BeTrue();
    }
    /// <summary>
    /// Gets retry policy_ exponential backoff_ should increase delays between retries.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Recovery Mechanism Tests

    [Fact]
    [Trait("TestType", "Recovery")]
    [Trait("Scenario", "RetryPolicy")]
    public async Task RetryPolicy_ExponentialBackoff_ShouldIncreaseDelaysBetweenRetries()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableRetryPolicy = true;
        accelerator.MaxRetryAttempts = 3;
        accelerator.UseExponentialBackoff = true;
        accelerator.SimulateTransientFailure = true;
        accelerator.FailureCountBeforeSuccess = 3;

        var definition = new KernelDefinition("backoff_test", "__kernel void test() {}", "test");

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await accelerator.CompileKernelAsync(definition);
        stopwatch.Stop();

        // Assert
        _ = result.Should().NotBeNull();
        _ = accelerator.RetryDelays.Should().HaveCount(3);

        // Verify exponential backoff (each delay should be roughly double the previous)

        for (var i = 1; i < accelerator.RetryDelays.Count; i++)
        {
            _ = accelerator.RetryDelays[i].Should().BeGreaterThan(accelerator.RetryDelays[i - 1]);
        }
    }
    /// <summary>
    /// Gets circuit breaker_ half open state_ should test recovery gradually.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Recovery")]
    [Trait("Scenario", "CircuitBreaker")]
    public async Task CircuitBreaker_HalfOpenState_ShouldTestRecoveryGradually()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableCircuitBreaker = true;
        accelerator.CircuitBreakerThreshold = 2;
        accelerator.CircuitBreakerTimeout = TimeSpan.FromMilliseconds(100);

        var definition = new KernelDefinition("circuit_test", "__kernel void test() {}", "test");

        // Trigger failures to open circuit
        accelerator.SimulateTransientFailure = true;
        for (var i = 0; i < 3; i++)
        {
            try { _ = await accelerator.CompileKernelAsync(definition); }
            catch { /* Expected */ }
        }

        // Wait for circuit breaker timeout
        await Task.Delay(150);

        // Assert circuit is half-open
        _ = accelerator.CircuitBreakerState.Should().Be(CircuitBreakerState.HalfOpen);

        // Enable success for recovery test
        accelerator.SimulateTransientFailure = false;

        // Act - Should succeed and close circuit
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        _ = result.Should().NotBeNull();
        _ = accelerator.CircuitBreakerState.Should().Be(CircuitBreakerState.Closed);
    }
    /// <summary>
    /// Gets fallback strategy_ c p u execution_ should activate when g p u fails.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Recovery")]
    [Trait("Scenario", "FallbackStrategy")]
    public async Task FallbackStrategy_CPUExecution_ShouldActivateWhenGPUFails()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableCpuFallback = true;
        accelerator.SimulatePermanentGpuFailure = true;

        var definition = new KernelDefinition("fallback_test", "__kernel void test() {}", "test");

        // Act
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        _ = result.Should().NotBeNull();
        _ = accelerator.CpuFallbackActivated.Should().BeTrue();
        _ = accelerator.LastExecutionMode.Should().Be("CPU");
    }
    /// <summary>
    /// Gets state restoration_ after error_ should restore valid state.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Recovery")]
    [Trait("Scenario", "StateRestoration")]
    public async Task StateRestoration_AfterError_ShouldRestoreValidState()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableStateCheckpointing = true;

        // Create initial state
        var definition1 = new KernelDefinition("state_test_1", "__kernel void test1() {}", "test");
        _ = await accelerator.CompileKernelAsync(definition1);


        var initialStateChecksum = accelerator.CalculateStateChecksum();

        // Simulate error that corrupts state
        accelerator.SimulateStateCorruption = true;
        var definition2 = new KernelDefinition("state_test_2", "__kernel void test2() {}", "test");


        try
        {
            _ = await accelerator.CompileKernelAsync(definition2);
        }
        catch
        {
            // Expected error
        }

        // Act - State should be restored
        accelerator.RestoreFromCheckpoint();

        // Assert
        var restoredStateChecksum = accelerator.CalculateStateChecksum();
        _ = restoredStateChecksum.Should().Be(initialStateChecksum);
        _ = accelerator.StateRestorationCount.Should().Be(1);
    }
    /// <summary>
    /// Gets exception handling_ chain_ should preserve inner exceptions.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Error Propagation Tests

    [Fact]
    [Trait("TestType", "ErrorPropagation")]
    [Trait("Scenario", "ExceptionChain")]
    public async Task ExceptionHandling_Chain_ShouldPreserveInnerExceptions()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.SimulateNestedErrors = true;

        var definition = new KernelDefinition("chain_test", "__kernel void test() {}", "test");

        // Act & Assert
        var act = async () => await accelerator.CompileKernelAsync(definition);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        // Verify exception chain is preserved
        _ = exception.Which.InnerException.Should().NotBeNull();
        _ = exception.Which.InnerException.Should().BeOfType<ArgumentException>();
        _ = exception.Which.InnerException!.InnerException.Should().BeOfType<OutOfMemoryException>();
    }
    /// <summary>
    /// Gets error context_ preservation_ should maintain operation metadata.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "ErrorPropagation")]
    [Trait("Scenario", "ContextPreservation")]
    public async Task ErrorContext_Preservation_ShouldMaintainOperationMetadata()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableContextPreservation = true;
        accelerator.SimulateContextualError = true;

        var definition = new KernelDefinition("context_test", "__kernel void test() {}", "test");

        // Act & Assert
        var act = async () => await accelerator.CompileKernelAsync(definition);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        // Verify context is preserved
        _ = exception.Which.Data.Contains("KernelName").Should().BeTrue();
        _ = exception.Which.Data.Contains("AcceleratorType").Should().BeTrue();
        _ = exception.Which.Data.Contains("Timestamp").Should().BeTrue();
        _ = exception.Which.Data["KernelName"].Should().Be("context_test");
    }
    /// <summary>
    /// Gets error diagnostics_ collection_ should capture system state.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "ErrorPropagation")]
    [Trait("Scenario", "DiagnosticInfo")]
    public async Task ErrorDiagnostics_Collection_ShouldCaptureSystemState()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableDiagnosticCollection = true;
        accelerator.SimulateError = true;

        var definition = new KernelDefinition("diagnostic_test", "__kernel void test() {}", "test");

        // Act
        try
        {
            _ = await accelerator.CompileKernelAsync(definition);
        }
        catch
        {
            // Expected
        }

        // Assert
        _ = accelerator.LastDiagnosticInfo.Should().NotBeNull();
        _ = accelerator.LastDiagnosticInfo.Should().NotBeNull();
        _ = accelerator.LastDiagnosticInfo!.ContainsKey("MemoryUsage").Should().BeTrue();
        _ = accelerator.LastDiagnosticInfo!.ContainsKey("ThreadCount").Should().BeTrue();
        _ = accelerator.LastDiagnosticInfo!.ContainsKey("SystemLoad").Should().BeTrue();
    }
    /// <summary>
    /// Gets concurrent errors_ multiple threads_ should handle gracefully.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Concurrent Error Scenarios

    [Fact]
    [Trait("TestType", "ConcurrentErrors")]
    [Trait("Scenario", "ConcurrentFailures")]
    public async Task ConcurrentErrors_MultipleThreads_ShouldHandleGracefully()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableConcurrentErrorHandling = true;
        accelerator.SimulateRandomErrors = true;

        var definitions = Enumerable.Range(0, 10)
            .Select(i => new KernelDefinition($"concurrent_test_{i}", "__kernel void test() {}", "test"))
            .ToArray();

        // Act
        var tasks = definitions.Select(async def =>
        {
            try
            {
                return await accelerator.CompileKernelAsync(def);
            }
            catch
            {
                return null; // Some failures expected
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        var successCount = results.Count(r => r != null);
        var failureCount = results.Length - successCount;

        _ = successCount.Should().BeGreaterThan(0, "Some operations should succeed");
        _ = failureCount.Should().BeGreaterThan(0, "Some operations should fail");
        _ = accelerator.ConcurrentErrorCount.Should().Be(failureCount);
    }
    /// <summary>
    /// Gets error handling_ race conditions_ should maintain consistency.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "ConcurrentErrors")]
    [Trait("Scenario", "RaceConditions")]
    public async Task ErrorHandling_RaceConditions_ShouldMaintainConsistency()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableRaceConditionSimulation = true;

        var definition = new KernelDefinition("race_test", "__kernel void test() {}", "test");

        // Act - Multiple concurrent operations with potential race conditions
        var tasks = new List<Task>();
        for (var i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    _ = await accelerator.CompileKernelAsync(definition);
                }
                catch
                {
                    // Expected due to race conditions
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Internal state should remain consistent
        _ = accelerator.InternalState.Should().NotBeNull();
        _ = accelerator.StateConsistencyViolations.Should().Be(0);
    }
    /// <summary>
    /// Gets async operation_ cancellation_ should cleanup resources properly.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Async Error Scenarios

    [Fact]
    [Trait("TestType", "AsyncErrors")]
    [Trait("Scenario", "TaskCancellation")]
    public async Task AsyncOperation_Cancellation_ShouldCleanupResourcesProperly()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.AsyncDelay = TimeSpan.FromSeconds(2);
        accelerator.TrackResourceCleanup = true;

        var definition = new KernelDefinition("cancel_test", "__kernel void test() {}", "test");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        var act = async () => await accelerator.CompileKernelAsync(definition, cancellationToken: cts.Token);
        _ = await act.Should().ThrowAsync<OperationCanceledException>();

        // Assert - Resources should be cleaned up
        _ = accelerator.ResourcesCleanedUpOnCancellation.Should().BeTrue();
        _ = accelerator.ActiveResourceCount.Should().Be(0);
    }
    /// <summary>
    /// Gets async operation_ deadlock prevention_ should timeout gracefully.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "AsyncErrors")]
    [Trait("Scenario", "DeadlockPrevention")]
    public async Task AsyncOperation_DeadlockPrevention_ShouldTimeoutGracefully()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.SimulateDeadlock = true;
        accelerator.DeadlockTimeout = TimeSpan.FromMilliseconds(500);

        var definition = new KernelDefinition("deadlock_test", "__kernel void test() {}", "test");

        // Act & Assert
        var act = async () => await accelerator.CompileKernelAsync(definition);
        _ = await act.Should().ThrowAsync<TimeoutException>()
            .WithMessage("*Deadlock detected*");

        _ = accelerator.DeadlockDetected.Should().BeTrue();
    }
    /// <summary>
    /// Gets device unavailable_ graceful_ should provide user friendly message.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Additional Error Handling Tests

    [Fact]
    [Trait("TestType", "DeviceErrors")]
    [Trait("Scenario", "DeviceUnavailable")]
    public async Task DeviceUnavailable_Graceful_ShouldProvideUserFriendlyMessage()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.SimulateDeviceUnavailable = true;

        var definition = new KernelDefinition("unavailable_test", "__kernel void test() {}", "test");

        // Act & Assert
        var act = async () => await accelerator.CompileKernelAsync(definition);
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Device is currently unavailable*");

        _ = accelerator.DeviceUnavailableDetected.Should().BeTrue();
    }
    /// <summary>
    /// Gets power management_ thermal throttling_ should reduce performance gracefully.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "DeviceErrors")]
    [Trait("Scenario", "PowerManagement")]
    public async Task PowerManagement_ThermalThrottling_ShouldReducePerformanceGracefully()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.SimulateThermalThrottling = true;
        accelerator.EnablePerformanceDegradation = true;

        var definition = new KernelDefinition("thermal_test", "__kernel void test() {}", "test");

        // Act
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        _ = result.Should().NotBeNull();
        _ = accelerator.ThermalThrottlingActivated.Should().BeTrue();
        _ = accelerator.PerformanceDegradationLevel.Should().BeGreaterThan(0);
    }
    /// <summary>
    /// Gets kernel compiler_ crash_ should recover with fallback compiler.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "KernelErrors")]
    [Trait("Scenario", "CompilerCrash")]
    public async Task KernelCompiler_Crash_ShouldRecoverWithFallbackCompiler()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.SimulateCompilerCrash = true;
        accelerator.EnableFallbackCompiler = true;

        var definition = new KernelDefinition("compiler_crash_test", "__kernel void test() {}", "test");

        // Act
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        _ = result.Should().NotBeNull();
        _ = accelerator.CompilerCrashDetected.Should().BeTrue();
        _ = accelerator.FallbackCompilerUsed.Should().BeTrue();
        _ = accelerator.LastCompilerUsed.Should().Be("FallbackCompiler");
    }
    /// <summary>
    /// Gets kernel version_ mismatch_ should throw compatibility exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "KernelErrors")]
    [Trait("Scenario", "VersionMismatch")]
    public async Task KernelVersion_Mismatch_ShouldThrowCompatibilityException()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.SimulateVersionMismatch = true;

        var definition = new KernelDefinition("version_test", "__kernel void test() {}", "test");

        // Act & Assert
        var act = async () => await accelerator.CompileKernelAsync(definition);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*version mismatch*");

        _ = exception.Which.Data.Contains("RequiredVersion").Should().BeTrue();
        _ = exception.Which.Data.Contains("AvailableVersion").Should().BeTrue();
        _ = exception.Which.Data.Contains("IsBackwardCompatible").Should().BeTrue();
    }
    /// <summary>
    /// Gets memory leak_ detection_ should trigger garbage collection.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "MemoryErrors")]
    [Trait("Scenario", "MemoryLeak")]
    public async Task MemoryLeak_Detection_ShouldTriggerGarbageCollection()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableMemoryLeakDetection = true;
        accelerator.SimulateMemoryLeak = true;
        accelerator.MemoryLeakThreshold = 10 * 1024 * 1024; // 10MB

        var definitions = Enumerable.Range(0, 5)
            .Select(i => new KernelDefinition($"leak_test_{i}", "__kernel void test() {}", "test"))
            .ToArray();

        // Act - Multiple operations to trigger leak detection
        foreach (var def in definitions)
        {
            _ = await accelerator.CompileKernelAsync(def);
        }

        // Assert
        _ = accelerator.MemoryLeakDetected.Should().BeTrue();
        _ = accelerator.ForcedGarbageCollections.Should().BeGreaterThan(0);
    }
    /// <summary>
    /// Gets memory fragmentation_ recovery_ should defragment and continue.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "MemoryErrors")]
    [Trait("Scenario", "FragmentationRecovery")]
    public async Task MemoryFragmentation_Recovery_ShouldDefragmentAndContinue()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.SimulateMemoryFragmentation = true;
        accelerator.EnableDefragmentation = true;
        accelerator.FragmentationThreshold = 0.7f; // 70% fragmentation

        var definition = new KernelDefinition("fragmentation_test", "__kernel void test() {}", "test");

        // Act
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        _ = result.Should().NotBeNull();
        _ = accelerator.MemoryFragmentationDetected.Should().BeTrue();
        _ = accelerator.DefragmentationPerformed.Should().BeTrue();
        _ = accelerator.PostDefragmentationFragmentationLevel.Should().BeLessThan(0.3f);
    }
    /// <summary>
    /// Gets buffer overflow_ protection_ should prevent corruption.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "MemoryErrors")]
    [Trait("Scenario", "BufferOverflow")]
    public async Task BufferOverflow_Protection_ShouldPreventCorruption()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableBufferOverflowProtection = true;
        accelerator.SimulateBufferOverflow = true;

        var definition = new KernelDefinition("overflow_test", "__kernel void test() {}", "test");

        // Act & Assert
        var act = async () => await accelerator.CompileKernelAsync(definition);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Buffer overflow detected*");

        _ = exception.Which.Data.Contains("BufferSize").Should().BeTrue();
        _ = exception.Which.Data.Contains("AttemptedAccess").Should().BeTrue();
        _ = exception.Which.Data.Contains("ProtectionEnabled").Should().BeTrue();

        _ = accelerator.BufferOverflowPrevented.Should().BeTrue();
    }
    /// <summary>
    /// Gets backup execution_ plan_ should activate when primary fails.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Recovery")]
    [Trait("Scenario", "BackupPlan")]
    public async Task BackupExecution_Plan_ShouldActivateWhenPrimaryFails()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableBackupExecutionPlan = true;
        accelerator.SimulatePrimaryPlanFailure = true;

        var definition = new KernelDefinition("backup_test", "__kernel void test() {}", "test");

        // Act
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        _ = result.Should().NotBeNull();
        _ = accelerator.PrimaryPlanFailed.Should().BeTrue();
        _ = accelerator.BackupPlanActivated.Should().BeTrue();
        _ = accelerator.LastExecutionPlan.Should().Be("BackupPlan");
    }
    /// <summary>
    /// Gets health check_ monitoring_ should detect and report issues.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Recovery")]
    [Trait("Scenario", "HealthCheck")]
    public async Task HealthCheck_Monitoring_ShouldDetectAndReportIssues()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableHealthChecking = true;
        accelerator.HealthCheckInterval = TimeSpan.FromMilliseconds(100);
        accelerator.SimulateHealthDegradation = true;

        var definition = new KernelDefinition("health_test", "__kernel void test() {}", "test");

        // Act
        _ = await accelerator.CompileKernelAsync(definition);
        await Task.Delay(200); // Allow health check to run

        // Assert
        _ = accelerator.HealthChecksPerformed.Should().BeGreaterThan(0);
        _ = accelerator.HealthDegradationDetected.Should().BeTrue();
        _ = accelerator.CurrentHealthScore.Should().BeLessThan(1.0f);
    }
    /// <summary>
    /// Gets self healing_ mechanism_ should automatically resolve issues.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Recovery")]
    [Trait("Scenario", "SelfHealing")]
    public async Task SelfHealing_Mechanism_ShouldAutomaticallyResolveIssues()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableSelfHealing = true;
        accelerator.SimulateHealableError = true;
        accelerator.MaxHealingAttempts = 3;

        var definition = new KernelDefinition("healing_test", "__kernel void test() {}", "test");

        // Act
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        _ = result.Should().NotBeNull();
        _ = accelerator.HealableErrorDetected.Should().BeTrue();
        _ = accelerator.SelfHealingActivated.Should().BeTrue();
        _ = accelerator.HealingAttemptsUsed.Should().BeGreaterThan(0);
        _ = accelerator.HealingSuccessful.Should().BeTrue();
    }
    /// <summary>
    /// Gets stack trace_ preservation_ should maintain original call stack.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "ErrorPropagation")]
    [Trait("Scenario", "StackTracePreservation")]
    public async Task StackTrace_Preservation_ShouldMaintainOriginalCallStack()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.PreserveStackTraces = true;
        accelerator.SimulateDeepStackError = true;

        var definition = new KernelDefinition("stack_test", "__kernel void test() {}", "test");

        // Act & Assert
        var act = async () => await accelerator.CompileKernelAsync(definition);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();

        // Verify stack trace contains method names from the call chain
        _ = exception.Which.StackTrace.Should().Contain("CompileKernelCoreAsync");
        _ = exception.Which.StackTrace.Should().Contain("DeepMethodCall");
        _ = exception.Which.Data.Contains("OriginalStackTrace").Should().BeTrue();
    }
    /// <summary>
    /// Gets error_ correlation_ should link related errors.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "ErrorPropagation")]
    [Trait("Scenario", "ErrorCorrelation")]
    public async Task Error_Correlation_ShouldLinkRelatedErrors()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableErrorCorrelation = true;
        accelerator.SimulateCorrelatedErrors = true;

        var definitions = new[]
        {
            new KernelDefinition("corr_test_1", "__kernel void test1() {}", "test"),
            new KernelDefinition("corr_test_2", "__kernel void test2() {}", "test")
        };

        var correlationId = Guid.NewGuid();
        accelerator.SetCorrelationId(correlationId);

        // Act - Trigger multiple related errors
        var exceptions = new List<Exception>();
        foreach (var def in definitions)
        {
            try
            {
                _ = await accelerator.CompileKernelAsync(def);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        // Assert
        _ = exceptions.Should().HaveCount(2);
        _ = exceptions.Should().AllSatisfy(ex =>
        {
            _ = ex.Data.Contains("CorrelationId").Should().BeTrue();
            _ = ex.Data["CorrelationId"].Should().Be(correlationId);
        });

        _ = accelerator.CorrelatedErrorsDetected.Should().BeTrue();
    }
    /// <summary>
    /// Gets concurrent operations_ thread safety_ should maintain error handling integrity.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "ConcurrentErrors")]
    [Trait("Scenario", "ThreadSafety")]
    public async Task ConcurrentOperations_ThreadSafety_ShouldMaintainErrorHandlingIntegrity()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableConcurrentErrorHandling = true;
        accelerator.SimulateThreadSafetyIssues = true;

        var definition = new KernelDefinition("thread_safety_test", "__kernel void test() {}", "test");

        // Act - Launch many concurrent operations
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(async () =>
            {
                try
                {
                    var kernel = await accelerator.CompileKernelAsync(definition);
                    return kernel != null;
                }
                catch
                {
                    return false;
                }
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        var successCount = results.Count(r => r);
        var failureCount = results.Length - successCount;

        // Some operations should succeed and some should fail
        _ = successCount.Should().BeGreaterThan(0);
        _ = failureCount.Should().BeGreaterThan(0);

        // No thread safety violations should occur
        _ = accelerator.ThreadSafetyViolations.Should().Be(0);
        _ = accelerator.ConcurrentAccessErrors.Should().Be(0);
    }
    /// <summary>
    /// Gets nested async_ calls_ should preserve exception context.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "AsyncErrors")]
    [Trait("Scenario", "NestedAsyncCalls")]
    public async Task NestedAsync_Calls_ShouldPreserveExceptionContext()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableNestedAsyncSimulation = true;
        accelerator.SimulateNestedAsyncError = true;
        accelerator.AsyncNestingLevel = 5;

        var definition = new KernelDefinition("nested_async_test", "__kernel void test() {}", "test");

        // Act & Assert
        var act = async () => await accelerator.CompileKernelAsync(definition);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nested async error*");

        // Verify nested context is preserved
        _ = exception.Which.Data.Contains("NestingLevel").Should().BeTrue();
        _ = exception.Which.Data.Contains("AsyncCallChain").Should().BeTrue();
        _ = exception.Which.Data["NestingLevel"].Should().Be(5);
    }
    /// <summary>
    /// Gets configure await_ false_ should not deadlock in sync context.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "AsyncErrors")]
    [Trait("Scenario", "ConfigureAwaitBehavior")]
    public async Task ConfigureAwait_False_ShouldNotDeadlockInSyncContext()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.UseConfigureAwaitFalse = true;
        accelerator.SimulateSyncContextDeadlock = true;

        var definition = new KernelDefinition("sync_context_test", "__kernel void test() {}", "test");

        // Act - This should complete without deadlock
        var result = await accelerator.CompileKernelAsync(definition).ConfigureAwait(false);

        // Assert
        _ = result.Should().NotBeNull();
        _ = accelerator.SyncContextDeadlockAvoided.Should().BeTrue();
        _ = accelerator.ConfigureAwaitUsedCorrectly.Should().BeTrue();
    }
    /// <summary>
    /// Performs synchronous validation_ invalid input_ should throw immediately.
    /// </summary>

    [Fact]
    [Trait("TestType", "SyncErrors")]
    [Trait("Scenario", "SynchronousValidation")]
    public void SynchronousValidation_InvalidInput_ShouldThrowImmediately()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableSynchronousValidation = true;

        // Act & Assert - Various invalid inputs
        var act1 = () => accelerator.ValidateKernelParameters(null!);
        _ = act1.Should().Throw<ArgumentNullException>()
            .WithParameterName("parameters");

        var act2 = () => accelerator.ValidateKernelParameters(new Dictionary<string, object>
        {

            ["invalid@key"] = "value"

        });
        _ = act2.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid parameter key format*");

        var act3 = () => accelerator.ValidateMemorySize(-1);
        _ = act3.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("size");

        _ = accelerator.SynchronousValidationCount.Should().Be(3);
    }
    /// <summary>
    /// Performs resource locking_ contention_ should handle timeouts.
    /// </summary>

    [Fact]
    [Trait("TestType", "SyncErrors")]
    [Trait("Scenario", "ResourceLocking")]
    public void ResourceLocking_Contention_ShouldHandleTimeouts()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableResourceLocking = true;
        accelerator.ResourceLockTimeout = TimeSpan.FromMilliseconds(100);
        accelerator.SimulateResourceContention = true;

        // Act & Assert
        var act = () => accelerator.AcquireExclusiveResource("test-resource");
        _ = act.Should().Throw<TimeoutException>()
            .WithMessage("*Resource lock timeout*");

        _ = accelerator.ResourceContentionDetected.Should().BeTrue();
        _ = accelerator.LockTimeoutCount.Should().Be(1);
    }

    #endregion

    #region Helper Methods

    private TestErrorAccelerator CreateTestAccelerator()
    {
        var info = new AcceleratorInfo(
            AcceleratorType.CPU,
            "Test Error Accelerator",
            "1.0",
            1024 * 1024 * 1024,
            4,
            3000,
            new Version(1, 0),
            1024 * 1024,
            true
        );

        var accelerator = new TestErrorAccelerator(info, _mockMemory.Object, _mockLogger.Object);
        _accelerators.Add(accelerator);
        return accelerator;
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var accelerator in _accelerators)
            {
                if (!accelerator.IsDisposed)
                {
                    try
                    {
                        _ = accelerator.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(1));
                    }
                    catch
                    {
                        // Ignore disposal errors in cleanup
                    }
                }
            }
            _disposed = true;
        }
    }

    #endregion

    /// <summary>
    /// Test accelerator implementation with comprehensive error simulation capabilities.
    /// </summary>
    private sealed class TestErrorAccelerator(AcceleratorInfo info, IUnifiedMemoryManager memory, ILogger logger) : BaseAccelerator(info, AcceleratorType.CPU, memory, new AcceleratorContext(IntPtr.Zero, 0), logger)
    {
        private readonly ConcurrentDictionary<string, object> _internalState = new();
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
        private readonly int _stateConsistencyViolations;
#pragma warning restore CS0649
        private volatile int _activeResourceCount;
        /// <summary>
        /// Gets or sets the simulate device error.
        /// </summary>
        /// <value>The simulate device error.</value>

        // Device Error Simulation
        public bool SimulateDeviceError { get; set; }
        /// <summary>
        /// Gets or sets the simulate hardware failure.
        /// </summary>
        /// <value>The simulate hardware failure.</value>
        public bool SimulateHardwareFailure { get; set; }
        /// <summary>
        /// Gets or sets the simulate driver error.
        /// </summary>
        /// <value>The simulate driver error.</value>
        public bool SimulateDriverError { get; set; }
        /// <summary>
        /// Gets or sets the allow recovery.
        /// </summary>
        /// <value>The allow recovery.</value>
        public bool AllowRecovery { get; set; }
        /// <summary>
        /// Gets or sets the custom error message.
        /// </summary>
        /// <value>The custom error message.</value>
        public string CustomErrorMessage { get; set; } = "Simulated error";
        /// <summary>
        /// Gets or sets the device reset count.
        /// </summary>
        /// <value>The device reset count.</value>
        public int DeviceResetCount { get; private set; }
        /// <summary>
        /// Gets or sets the simulate kernel launch failure.
        /// </summary>
        /// <value>The simulate kernel launch failure.</value>

        // Kernel Error Simulation
        public bool SimulateKernelLaunchFailure { get; set; }
        /// <summary>
        /// Gets or sets the simulate stack overflow.
        /// </summary>
        /// <value>The simulate stack overflow.</value>
        public bool SimulateStackOverflow { get; set; }
        /// <summary>
        /// Gets or sets the failure count before success.
        /// </summary>
        /// <value>The failure count before success.</value>
        public int FailureCountBeforeSuccess { get; set; } = int.MaxValue;
        /// <summary>
        /// Gets or sets the compilation delay.
        /// </summary>
        /// <value>The compilation delay.</value>
        public TimeSpan CompilationDelay { get; set; }
        /// <summary>
        /// Gets or sets the compilation cancelled.
        /// </summary>
        /// <value>The compilation cancelled.</value>
        public bool CompilationCancelled { get; private set; }
        /// <summary>
        /// Gets or sets the last stack overflow info.
        /// </summary>
        /// <value>The last stack overflow info.</value>
        public StackOverflowInfo? LastStackOverflowInfo { get; private set; }
        /// <summary>
        /// Gets or sets the simulate memory allocation failure.
        /// </summary>
        /// <value>The simulate memory allocation failure.</value>

        // Memory Error Simulation
        public bool SimulateMemoryAllocationFailure { get; set; }
        /// <summary>
        /// Gets or sets the simulate memory transfer error.
        /// </summary>
        /// <value>The simulate memory transfer error.</value>
        public bool SimulateMemoryTransferError { get; set; }
        /// <summary>
        /// Gets or sets the simulate memory corruption.
        /// </summary>
        /// <value>The simulate memory corruption.</value>
        public bool SimulateMemoryCorruption { get; set; }
        /// <summary>
        /// Gets or sets the enable memory corruption detection.
        /// </summary>
        /// <value>The enable memory corruption detection.</value>
        public bool EnableMemoryCorruptionDetection { get; set; }
        /// <summary>
        /// Gets or sets the trigger memory corruption.
        /// </summary>
        /// <value>The trigger memory corruption.</value>
        public bool TriggerMemoryCorruption { get; set; }
        /// <summary>
        /// Gets or sets the memory failure count before success.
        /// </summary>
        /// <value>The memory failure count before success.</value>
        public int MemoryFailureCountBeforeSuccess { get; set; } = int.MaxValue;
        /// <summary>
        /// Gets or sets the memory cleanup count.
        /// </summary>
        /// <value>The memory cleanup count.</value>
        public int MemoryCleanupCount { get; private set; }
        /// <summary>
        /// Gets or sets the garbage collection triggered.
        /// </summary>
        /// <value>The garbage collection triggered.</value>
        public bool GarbageCollectionTriggered { get; private set; }
        /// <summary>
        /// Gets or sets the alternative transfer used.
        /// </summary>
        /// <value>The alternative transfer used.</value>
        public bool AlternativeTransferUsed { get; private set; }
        /// <summary>
        /// Gets or sets the enable alternative transfer strategy.
        /// </summary>
        /// <value>The enable alternative transfer strategy.</value>
        public bool EnableAlternativeTransferStrategy { get; set; }
        /// <summary>
        /// Gets or sets the cache invalidated due to corruption.
        /// </summary>
        /// <value>The cache invalidated due to corruption.</value>
        public bool CacheInvalidatedDueToCorruption { get; private set; }
        /// <summary>
        /// Gets or sets the enable retry policy.
        /// </summary>
        /// <value>The enable retry policy.</value>

        // Recovery Mechanisms
        public bool EnableRetryPolicy { get; set; }
        /// <summary>
        /// Gets or sets the enable circuit breaker.
        /// </summary>
        /// <value>The enable circuit breaker.</value>
        public bool EnableCircuitBreaker { get; set; }
        /// <summary>
        /// Gets or sets the enable memory recovery.
        /// </summary>
        /// <value>The enable memory recovery.</value>
        public bool EnableMemoryRecovery { get; set; }
        /// <summary>
        /// Gets or sets the enable cpu fallback.
        /// </summary>
        /// <value>The enable cpu fallback.</value>
        public bool EnableCpuFallback { get; set; }
        /// <summary>
        /// Gets or sets the enable state checkpointing.
        /// </summary>
        /// <value>The enable state checkpointing.</value>
        public bool EnableStateCheckpointing { get; set; }
        /// <summary>
        /// Gets or sets the max retry attempts.
        /// </summary>
        /// <value>The max retry attempts.</value>
        public int MaxRetryAttempts { get; set; } = 3;
        /// <summary>
        /// Gets or sets the use exponential backoff.
        /// </summary>
        /// <value>The use exponential backoff.</value>
        public bool UseExponentialBackoff { get; set; }
        /// <summary>
        /// Gets or sets the circuit breaker threshold.
        /// </summary>
        /// <value>The circuit breaker threshold.</value>
        public int CircuitBreakerThreshold { get; set; } = 3;
        /// <summary>
        /// Gets or sets the circuit breaker timeout.
        /// </summary>
        /// <value>The circuit breaker timeout.</value>
        public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromSeconds(1);
        /// <summary>
        /// Gets or sets the circuit breaker state.
        /// </summary>
        /// <value>The circuit breaker state.</value>
        public CircuitBreakerState CircuitBreakerState { get; private set; } = CircuitBreakerState.Closed;
        /// <summary>
        /// Gets or sets the retry attempt count.
        /// </summary>
        /// <value>The retry attempt count.</value>
        public int RetryAttemptCount { get; private set; }
        /// <summary>
        /// Gets or sets the retry delays.
        /// </summary>
        /// <value>The retry delays.</value>
        public List<TimeSpan> RetryDelays { get; } = [];
        /// <summary>
        /// Gets or sets the last successful compilation.
        /// </summary>
        /// <value>The last successful compilation.</value>
        public DateTime? LastSuccessfulCompilation { get; private set; }
        /// <summary>
        /// Gets or sets the cpu fallback activated.
        /// </summary>
        /// <value>The cpu fallback activated.</value>
        public bool CpuFallbackActivated { get; private set; }
        /// <summary>
        /// Gets or sets the last execution mode.
        /// </summary>
        /// <value>The last execution mode.</value>
        public string LastExecutionMode { get; private set; } = "GPU";
        /// <summary>
        /// Gets or sets the state restoration count.
        /// </summary>
        /// <value>The state restoration count.</value>
        public int StateRestorationCount { get; private set; }
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
        private readonly string? _checkpointState;
        /// <summary>
        /// Gets or sets the simulate nested errors.
        /// </summary>
        /// <value>The simulate nested errors.</value>
#pragma warning restore CS0649

        // Error Propagation
        public bool SimulateNestedErrors { get; set; }
        /// <summary>
        /// Gets or sets the enable context preservation.
        /// </summary>
        /// <value>The enable context preservation.</value>
        public bool EnableContextPreservation { get; set; }
        /// <summary>
        /// Gets or sets the enable diagnostic collection.
        /// </summary>
        /// <value>The enable diagnostic collection.</value>
        public bool EnableDiagnosticCollection { get; set; }
        /// <summary>
        /// Gets or sets the simulate contextual error.
        /// </summary>
        /// <value>The simulate contextual error.</value>
        public bool SimulateContextualError { get; set; }
        /// <summary>
        /// Gets or sets the simulate error.
        /// </summary>
        /// <value>The simulate error.</value>
        public bool SimulateError { get; set; }
        /// <summary>
        /// Gets or sets the last diagnostic info.
        /// </summary>
        /// <value>The last diagnostic info.</value>
        public Dictionary<string, object>? LastDiagnosticInfo { get; private set; }
        /// <summary>
        /// Gets or sets the enable concurrent error handling.
        /// </summary>
        /// <value>The enable concurrent error handling.</value>

        // Concurrency
        public bool EnableConcurrentErrorHandling { get; set; }
        /// <summary>
        /// Gets or sets the simulate random errors.
        /// </summary>
        /// <value>The simulate random errors.</value>
        public bool SimulateRandomErrors { get; set; }
        /// <summary>
        /// Gets or sets the enable race condition simulation.
        /// </summary>
        /// <value>The enable race condition simulation.</value>
        public bool EnableRaceConditionSimulation { get; set; }
        /// <summary>
        /// Gets or sets the concurrent error count.
        /// </summary>
        /// <value>The concurrent error count.</value>
        public int ConcurrentErrorCount { get; private set; }
        /// <summary>
        /// Gets or sets the state consistency violations.
        /// </summary>
        /// <value>The state consistency violations.</value>
        public int StateConsistencyViolations => _stateConsistencyViolations;
        /// <summary>
        /// Gets or sets the internal state.
        /// </summary>
        /// <value>The internal state.</value>
        public ConcurrentDictionary<string, object> InternalState => _internalState;
        /// <summary>
        /// Gets or sets the async delay.
        /// </summary>
        /// <value>The async delay.</value>

        // Async Operations
        public TimeSpan AsyncDelay { get; set; }
        /// <summary>
        /// Gets or sets the track resource cleanup.
        /// </summary>
        /// <value>The track resource cleanup.</value>
        public bool TrackResourceCleanup { get; set; }
        /// <summary>
        /// Gets or sets the resources cleaned up on cancellation.
        /// </summary>
        /// <value>The resources cleaned up on cancellation.</value>
        public bool ResourcesCleanedUpOnCancellation { get; private set; }
        /// <summary>
        /// Gets or sets the active resource count.
        /// </summary>
        /// <value>The active resource count.</value>
        public int ActiveResourceCount => _activeResourceCount;
        /// <summary>
        /// Gets or sets the simulate deadlock.
        /// </summary>
        /// <value>The simulate deadlock.</value>
        public bool SimulateDeadlock { get; set; }
        /// <summary>
        /// Gets or sets the deadlock timeout.
        /// </summary>
        /// <value>The deadlock timeout.</value>
        public TimeSpan DeadlockTimeout { get; set; } = TimeSpan.FromSeconds(5);
        /// <summary>
        /// Gets or sets the deadlock detected.
        /// </summary>
        /// <value>The deadlock detected.</value>
        public bool DeadlockDetected { get; private set; }
        /// <summary>
        /// Gets or sets the simulate transient failure.
        /// </summary>
        /// <value>The simulate transient failure.</value>

        // General Error Simulation
        public bool SimulateTransientFailure { get; set; }
        /// <summary>
        /// Gets or sets the simulate permanent gpu failure.
        /// </summary>
        /// <value>The simulate permanent gpu failure.</value>
        public bool SimulatePermanentGpuFailure { get; set; }
        /// <summary>
        /// Gets or sets the simulate state corruption.
        /// </summary>
        /// <value>The simulate state corruption.</value>
        public bool SimulateStateCorruption { get; set; }
        /// <summary>
        /// Gets or sets the simulate device unavailable.
        /// </summary>
        /// <value>The simulate device unavailable.</value>

        // Additional Device Error Simulation

        public bool SimulateDeviceUnavailable { get; set; }
        /// <summary>
        /// Gets or sets the device unavailable detected.
        /// </summary>
        /// <value>The device unavailable detected.</value>
        public bool DeviceUnavailableDetected { get; private set; }
        /// <summary>
        /// Gets or sets the simulate thermal throttling.
        /// </summary>
        /// <value>The simulate thermal throttling.</value>
        public bool SimulateThermalThrottling { get; set; }
        /// <summary>
        /// Gets or sets the enable performance degradation.
        /// </summary>
        /// <value>The enable performance degradation.</value>
        public bool EnablePerformanceDegradation { get; set; }
        /// <summary>
        /// Gets or sets the thermal throttling activated.
        /// </summary>
        /// <value>The thermal throttling activated.</value>
        public bool ThermalThrottlingActivated { get; private set; }
        /// <summary>
        /// Gets or sets the performance degradation level.
        /// </summary>
        /// <value>The performance degradation level.</value>
        public float PerformanceDegradationLevel { get; private set; }
        /// <summary>
        /// Gets or sets the simulate compiler crash.
        /// </summary>
        /// <value>The simulate compiler crash.</value>

        // Additional Kernel Error Simulation

        public bool SimulateCompilerCrash { get; set; }
        /// <summary>
        /// Gets or sets the enable fallback compiler.
        /// </summary>
        /// <value>The enable fallback compiler.</value>
        public bool EnableFallbackCompiler { get; set; }
        /// <summary>
        /// Gets or sets the compiler crash detected.
        /// </summary>
        /// <value>The compiler crash detected.</value>
        public bool CompilerCrashDetected { get; private set; }
        /// <summary>
        /// Gets or sets the fallback compiler used.
        /// </summary>
        /// <value>The fallback compiler used.</value>
        public bool FallbackCompilerUsed { get; private set; }
        /// <summary>
        /// Gets or sets the last compiler used.
        /// </summary>
        /// <value>The last compiler used.</value>
        public string LastCompilerUsed { get; private set; } = "PrimaryCompiler";
        /// <summary>
        /// Gets or sets the simulate version mismatch.
        /// </summary>
        /// <value>The simulate version mismatch.</value>
        public bool SimulateVersionMismatch { get; set; }
        /// <summary>
        /// Gets or sets the enable memory leak detection.
        /// </summary>
        /// <value>The enable memory leak detection.</value>

        // Additional Memory Error Simulation  

        public bool EnableMemoryLeakDetection { get; set; }
        /// <summary>
        /// Gets or sets the simulate memory leak.
        /// </summary>
        /// <value>The simulate memory leak.</value>
        public bool SimulateMemoryLeak { get; set; }
        /// <summary>
        /// Gets or sets the memory leak detected.
        /// </summary>
        /// <value>The memory leak detected.</value>
        public bool MemoryLeakDetected { get; private set; }
        /// <summary>
        /// Gets or sets the memory leak threshold.
        /// </summary>
        /// <value>The memory leak threshold.</value>
        public long MemoryLeakThreshold { get; set; } = 50 * 1024 * 1024; // 50MB
        /// <summary>
        /// Gets or sets the forced garbage collections.
        /// </summary>
        /// <value>The forced garbage collections.</value>
        public int ForcedGarbageCollections { get; private set; }
        /// <summary>
        /// Gets or sets the simulate memory fragmentation.
        /// </summary>
        /// <value>The simulate memory fragmentation.</value>
        public bool SimulateMemoryFragmentation { get; set; }
        /// <summary>
        /// Gets or sets the enable defragmentation.
        /// </summary>
        /// <value>The enable defragmentation.</value>
        public bool EnableDefragmentation { get; set; }
        /// <summary>
        /// Gets or sets the memory fragmentation detected.
        /// </summary>
        /// <value>The memory fragmentation detected.</value>
        public bool MemoryFragmentationDetected { get; private set; }
        /// <summary>
        /// Gets or sets the defragmentation performed.
        /// </summary>
        /// <value>The defragmentation performed.</value>
        public bool DefragmentationPerformed { get; private set; }
        /// <summary>
        /// Gets or sets the fragmentation threshold.
        /// </summary>
        /// <value>The fragmentation threshold.</value>
        public float FragmentationThreshold { get; set; } = 0.6f;
        /// <summary>
        /// Gets or sets the post defragmentation fragmentation level.
        /// </summary>
        /// <value>The post defragmentation fragmentation level.</value>
        public float PostDefragmentationFragmentationLevel { get; private set; }
        /// <summary>
        /// Gets or sets the enable buffer overflow protection.
        /// </summary>
        /// <value>The enable buffer overflow protection.</value>
        public bool EnableBufferOverflowProtection { get; set; }
        /// <summary>
        /// Gets or sets the simulate buffer overflow.
        /// </summary>
        /// <value>The simulate buffer overflow.</value>
        public bool SimulateBufferOverflow { get; set; }
        /// <summary>
        /// Gets or sets the buffer overflow prevented.
        /// </summary>
        /// <value>The buffer overflow prevented.</value>
        public bool BufferOverflowPrevented { get; private set; }
        /// <summary>
        /// Gets or sets the enable backup execution plan.
        /// </summary>
        /// <value>The enable backup execution plan.</value>

        // Additional Recovery Mechanisms

        public bool EnableBackupExecutionPlan { get; set; }
        /// <summary>
        /// Gets or sets the simulate primary plan failure.
        /// </summary>
        /// <value>The simulate primary plan failure.</value>
        public bool SimulatePrimaryPlanFailure { get; set; }
        /// <summary>
        /// Gets or sets the primary plan failed.
        /// </summary>
        /// <value>The primary plan failed.</value>
        public bool PrimaryPlanFailed { get; private set; }
        /// <summary>
        /// Gets or sets the backup plan activated.
        /// </summary>
        /// <value>The backup plan activated.</value>
        public bool BackupPlanActivated { get; private set; }
        /// <summary>
        /// Gets or sets the last execution plan.
        /// </summary>
        /// <value>The last execution plan.</value>
        public string LastExecutionPlan { get; private set; } = "PrimaryPlan";
        /// <summary>
        /// Gets or sets the enable health checking.
        /// </summary>
        /// <value>The enable health checking.</value>
        public bool EnableHealthChecking { get; set; }
        /// <summary>
        /// Gets or sets the health check interval.
        /// </summary>
        /// <value>The health check interval.</value>
        public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(1);
        /// <summary>
        /// Gets or sets the simulate health degradation.
        /// </summary>
        /// <value>The simulate health degradation.</value>
        public bool SimulateHealthDegradation { get; set; }
        /// <summary>
        /// Gets or sets the health checks performed.
        /// </summary>
        /// <value>The health checks performed.</value>
        public int HealthChecksPerformed { get; private set; }
        /// <summary>
        /// Gets or sets the health degradation detected.
        /// </summary>
        /// <value>The health degradation detected.</value>
        public bool HealthDegradationDetected { get; private set; }
        /// <summary>
        /// Gets or sets the current health score.
        /// </summary>
        /// <value>The current health score.</value>
        public float CurrentHealthScore { get; private set; } = 1.0f;
        /// <summary>
        /// Gets or sets the enable self healing.
        /// </summary>
        /// <value>The enable self healing.</value>
        public bool EnableSelfHealing { get; set; }
        /// <summary>
        /// Gets or sets the simulate healable error.
        /// </summary>
        /// <value>The simulate healable error.</value>
        public bool SimulateHealableError { get; set; }
        /// <summary>
        /// Gets or sets the max healing attempts.
        /// </summary>
        /// <value>The max healing attempts.</value>
        public int MaxHealingAttempts { get; set; } = 3;
        /// <summary>
        /// Gets or sets the healable error detected.
        /// </summary>
        /// <value>The healable error detected.</value>
        public bool HealableErrorDetected { get; private set; }
        /// <summary>
        /// Gets or sets the self healing activated.
        /// </summary>
        /// <value>The self healing activated.</value>
        public bool SelfHealingActivated { get; private set; }
        /// <summary>
        /// Gets or sets the healing attempts used.
        /// </summary>
        /// <value>The healing attempts used.</value>
        public int HealingAttemptsUsed { get; private set; }
        /// <summary>
        /// Gets or sets the healing successful.
        /// </summary>
        /// <value>The healing successful.</value>
        public bool HealingSuccessful { get; private set; }
        /// <summary>
        /// Gets or sets the preserve stack traces.
        /// </summary>
        /// <value>The preserve stack traces.</value>

        // Additional Error Propagation

        public bool PreserveStackTraces { get; set; }
        /// <summary>
        /// Gets or sets the simulate deep stack error.
        /// </summary>
        /// <value>The simulate deep stack error.</value>
        public bool SimulateDeepStackError { get; set; }
        /// <summary>
        /// Gets or sets the enable error correlation.
        /// </summary>
        /// <value>The enable error correlation.</value>
        public bool EnableErrorCorrelation { get; set; }
        /// <summary>
        /// Gets or sets the simulate correlated errors.
        /// </summary>
        /// <value>The simulate correlated errors.</value>
        public bool SimulateCorrelatedErrors { get; set; }
        /// <summary>
        /// Gets or sets the correlated errors detected.
        /// </summary>
        /// <value>The correlated errors detected.</value>
        public bool CorrelatedErrorsDetected { get; private set; }
        private Guid _correlationId;
        /// <summary>
        /// Gets or sets the simulate thread safety issues.
        /// </summary>
        /// <value>The simulate thread safety issues.</value>

        // Additional Concurrency Features

        public bool SimulateThreadSafetyIssues { get; set; }
        /// <summary>
        /// Gets or sets the thread safety violations.
        /// </summary>
        /// <value>The thread safety violations.</value>
        public int ThreadSafetyViolations { get; private set; }
        /// <summary>
        /// Gets or sets the concurrent access errors.
        /// </summary>
        /// <value>The concurrent access errors.</value>
        public int ConcurrentAccessErrors { get; private set; }
        /// <summary>
        /// Gets or sets the enable nested async simulation.
        /// </summary>
        /// <value>The enable nested async simulation.</value>

        // Additional Async Features

        public bool EnableNestedAsyncSimulation { get; set; }
        /// <summary>
        /// Gets or sets the simulate nested async error.
        /// </summary>
        /// <value>The simulate nested async error.</value>
        public bool SimulateNestedAsyncError { get; set; }
        /// <summary>
        /// Gets or sets the async nesting level.
        /// </summary>
        /// <value>The async nesting level.</value>
        public int AsyncNestingLevel { get; set; } = 1;
        /// <summary>
        /// Gets or sets the use configure await false.
        /// </summary>
        /// <value>The use configure await false.</value>
        public bool UseConfigureAwaitFalse { get; set; }
        /// <summary>
        /// Gets or sets the simulate sync context deadlock.
        /// </summary>
        /// <value>The simulate sync context deadlock.</value>
        public bool SimulateSyncContextDeadlock { get; set; }
        /// <summary>
        /// Gets or sets the sync context deadlock avoided.
        /// </summary>
        /// <value>The sync context deadlock avoided.</value>
        public bool SyncContextDeadlockAvoided { get; private set; }
        /// <summary>
        /// Gets or sets the configure await used correctly.
        /// </summary>
        /// <value>The configure await used correctly.</value>
        public bool ConfigureAwaitUsedCorrectly { get; private set; }
        /// <summary>
        /// Gets or sets the enable synchronous validation.
        /// </summary>
        /// <value>The enable synchronous validation.</value>

        // Additional Sync Features

        public bool EnableSynchronousValidation { get; set; }
        /// <summary>
        /// Gets or sets the synchronous validation count.
        /// </summary>
        /// <value>The synchronous validation count.</value>
        public int SynchronousValidationCount { get; private set; }
        /// <summary>
        /// Gets or sets the enable resource locking.
        /// </summary>
        /// <value>The enable resource locking.</value>
        public bool EnableResourceLocking { get; set; }
        /// <summary>
        /// Gets or sets the resource lock timeout.
        /// </summary>
        /// <value>The resource lock timeout.</value>
        public TimeSpan ResourceLockTimeout { get; set; } = TimeSpan.FromSeconds(1);
        /// <summary>
        /// Gets or sets the simulate resource contention.
        /// </summary>
        /// <value>The simulate resource contention.</value>
        public bool SimulateResourceContention { get; set; }
        /// <summary>
        /// Gets or sets the resource contention detected.
        /// </summary>
        /// <value>The resource contention detected.</value>
        public bool ResourceContentionDetected { get; private set; }
        /// <summary>
        /// Gets or sets the lock timeout count.
        /// </summary>
        /// <value>The lock timeout count.</value>
        public int LockTimeoutCount { get; private set; }

        private int _attemptCount;
        private int _memoryFailureCount;
        private readonly Random _random = new();

        protected override async ValueTask<ICompiledKernel> CompileKernelCoreAsync(
            KernelDefinition definition,
            CompilationOptions options,
            CancellationToken cancellationToken)
        {
            // Check circuit breaker first
            if (EnableCircuitBreaker && CircuitBreakerState == CircuitBreakerState.Open)
            {
                throw new CircuitBreakerOpenException("Circuit breaker is open due to repeated failures");
            }

            _ = Interlocked.Increment(ref _activeResourceCount);

            try
            {
                // Handle async delay and cancellation
                if (AsyncDelay > TimeSpan.Zero)
                {
                    await Task.Delay(AsyncDelay, cancellationToken);
                }

                if (CompilationDelay > TimeSpan.Zero)
                {
                    await Task.Delay(CompilationDelay, cancellationToken);
                }

                // Deadlock simulation
                if (SimulateDeadlock)
                {
                    var deadlockTask = Task.Delay(Timeout.Infinite, cancellationToken);
                    var timeoutTask = Task.Delay(DeadlockTimeout, CancellationToken.None);


                    var completedTask = await Task.WhenAny(deadlockTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        DeadlockDetected = true;
                        throw new TimeoutException("Deadlock detected during kernel compilation");
                    }
                }

                // Handle various error scenarios with retry logic
                if (EnableRetryPolicy && SimulateKernelLaunchFailure)
                {
                    // Retry logic for kernel launch failures
                    var attempts = 0;
                    while (attempts < MaxRetryAttempts)
                    {
                        try
                        {
                            await SimulateErrors();
                            break; // Success, exit retry loop
                        }
                        catch (InvalidOperationException ex) when (ex.Message == "Kernel launch failed" && attempts < MaxRetryAttempts - 1)
                        {
                            attempts++;
                            RetryAttemptCount = attempts;
                            await HandleRetryWithBackoff(attempts);
                            // Continue to next retry attempt
                        }
                    }
                }
                else
                {
                    // No retry, just simulate errors
                    await SimulateErrors();
                }

                // Create successful result
                var mockKernel = new Mock<ICompiledKernel>();
                _ = mockKernel.Setup(x => x.Id).Returns(Guid.NewGuid());
                _ = mockKernel.Setup(x => x.Name).Returns(definition.Name);

                LastSuccessfulCompilation = DateTime.UtcNow;
                return mockKernel.Object;
            }
            catch (OperationCanceledException)
            {
                CompilationCancelled = true;


                if (TrackResourceCleanup)
                {
                    ResourcesCleanedUpOnCancellation = true;
                }


                throw;
            }
            finally
            {
                _ = Interlocked.Decrement(ref _activeResourceCount);
            }
        }

        private async Task SimulateErrors()
        {
            // Device errors
            if (SimulateDeviceError && !AllowRecovery)
            {
                throw new InvalidOperationException($"Device error: {CustomErrorMessage}");
            }

            if (SimulateDeviceUnavailable)
            {
                DeviceUnavailableDetected = true;
                throw new InvalidOperationException("Device is currently unavailable. Please check hardware connections and drivers.");
            }

            if (SimulateThermalThrottling && EnablePerformanceDegradation)
            {
                ThermalThrottlingActivated = true;
                PerformanceDegradationLevel = 0.6f; // 60% performance reduction
                // Don't throw - this is graceful degradation
            }

            if (SimulateHardwareFailure)
            {
                IncrementCircuitBreakerFailures();
                throw new InvalidOperationException("Hardware failure detected");
            }

            if (SimulateDriverError)
            {
                throw new InvalidOperationException($"Driver error: {CustomErrorMessage}");
            }

            // Kernel errors
            if (SimulateCompilerCrash && EnableFallbackCompiler)
            {
                CompilerCrashDetected = true;
                FallbackCompilerUsed = true;
                LastCompilerUsed = "FallbackCompiler";
                // Don't throw - fallback compiler recovers
            }
            else if (SimulateCompilerCrash && !EnableFallbackCompiler)
            {
                CompilerCrashDetected = true;
                throw new InvalidOperationException("Primary compiler crashed and no fallback available");
            }

            if (SimulateVersionMismatch)
            {
                var ex = new InvalidOperationException("Kernel version mismatch detected");
                ex.Data["RequiredVersion"] = "2.0";
                ex.Data["AvailableVersion"] = "1.5";
                ex.Data["IsBackwardCompatible"] = false;
                throw ex;
            }

            if (SimulateKernelLaunchFailure)
            {
                var currentAttempt = Interlocked.Increment(ref _attemptCount);
                if (currentAttempt <= FailureCountBeforeSuccess)
                {
                    if (EnableRetryPolicy)
                    {
                        await HandleRetryWithBackoff(currentAttempt);
                    }
                    throw new InvalidOperationException("Kernel launch failed");
                }
                else
                {
                    RetryAttemptCount = currentAttempt - 1;
                }
            }

            if (SimulateStackOverflow)
            {
                LastStackOverflowInfo = new StackOverflowInfo
                {
                    KernelName = "stack_overflow_test",
                    StackDepth = 1000,
                    DetectedAt = DateTime.UtcNow
                };
                throw new StackOverflowException("Stack overflow in kernel execution");
            }

            // Memory errors
            if (SimulateMemoryAllocationFailure)
            {
                await HandleMemoryError();
            }

            if (SimulateMemoryTransferError && !EnableAlternativeTransferStrategy)
            {
                throw new InvalidOperationException("Memory transfer failed");
            }

            if (SimulateMemoryTransferError && EnableAlternativeTransferStrategy)
            {
                AlternativeTransferUsed = true;
            }

            if (SimulateMemoryLeak && EnableMemoryLeakDetection)
            {
                // Simulate memory leak detection after several operations
                var currentMemoryUsage = GC.GetTotalMemory(false);
                if (currentMemoryUsage > MemoryLeakThreshold)
                {
                    MemoryLeakDetected = true;
                    ForcedGarbageCollections++;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            if (SimulateMemoryFragmentation && EnableDefragmentation)
            {
                MemoryFragmentationDetected = true;
                DefragmentationPerformed = true;
                PostDefragmentationFragmentationLevel = 0.2f; // Improved after defragmentation
            }

            if (SimulateBufferOverflow && EnableBufferOverflowProtection)
            {
                BufferOverflowPrevented = true;
                var ex = new InvalidOperationException("Buffer overflow detected and prevented");
                ex.Data["BufferSize"] = 1024;
                ex.Data["AttemptedAccess"] = 2048;
                ex.Data["ProtectionEnabled"] = true;
                throw ex;
            }

            if (SimulateMemoryCorruption && TriggerMemoryCorruption)
            {
                if (EnableMemoryCorruptionDetection)
                {
                    CacheInvalidatedDueToCorruption = true;
                    throw new InvalidOperationException("Memory corruption detected");
                }
            }

            // General error scenarios
            if (SimulateTransientFailure && EnableRetryPolicy)
            {
                var currentAttempt = Interlocked.Increment(ref _attemptCount);
                if (currentAttempt <= FailureCountBeforeSuccess)
                {
                    await HandleRetryWithBackoff(currentAttempt);
                    throw new InvalidOperationException("Transient failure");
                }
            }

            if (SimulatePermanentGpuFailure && EnableCpuFallback)
            {
                CpuFallbackActivated = true;
                LastExecutionMode = "CPU";
                return; // Success via CPU fallback
            }

            if (SimulatePermanentGpuFailure && !EnableCpuFallback)
            {
                throw new InvalidOperationException("Permanent GPU failure");
            }

            if (SimulateNestedErrors)
            {
                var innerException = new OutOfMemoryException("Out of memory");
                var middleException = new ArgumentException("Invalid argument", innerException);
                throw new InvalidOperationException("Top-level error", middleException);
            }

            if (SimulateContextualError && EnableContextPreservation)
            {
                var ex = new InvalidOperationException("Contextual error");
                ex.Data["KernelName"] = "context_test";
                ex.Data["AcceleratorType"] = Type.ToString();
                ex.Data["Timestamp"] = DateTime.UtcNow;
                throw ex;
            }

            if (SimulateError && EnableDiagnosticCollection)
            {
                CollectDiagnosticInfo();
                throw new InvalidOperationException("Error with diagnostics");
            }

            if (SimulateRandomErrors && EnableConcurrentErrorHandling)
            {
                if (_random.NextDouble() < 0.3) // 30% failure rate
                {
                    _ = Interlocked.Increment(ref _concurrentErrorCount);
                    throw new InvalidOperationException("Random concurrent error");
                }
            }

            // Recovery and execution plan errors
            if (SimulatePrimaryPlanFailure && EnableBackupExecutionPlan)
            {
                PrimaryPlanFailed = true;
                BackupPlanActivated = true;
                LastExecutionPlan = "BackupPlan";
                // Don't throw - backup plan recovers
            }

            if (SimulateHealthDegradation && EnableHealthChecking)
            {
                HealthDegradationDetected = true;
                CurrentHealthScore = 0.4f; // Poor health
                HealthChecksPerformed++;
            }

            if (SimulateHealableError && EnableSelfHealing)
            {
                HealableErrorDetected = true;
                SelfHealingActivated = true;
                HealingAttemptsUsed = Math.Min(MaxHealingAttempts, 2);
                HealingSuccessful = true;
                // Don't throw - self-healing resolves the issue
            }

            // Error propagation and correlation
            if (SimulateDeepStackError && PreserveStackTraces)
            {
                DeepMethodCall1();
            }

            if (SimulateCorrelatedErrors && EnableErrorCorrelation)
            {
                CorrelatedErrorsDetected = true;
                var ex = new InvalidOperationException("Correlated error occurred");
                ex.Data["CorrelationId"] = _correlationId;
                throw ex;
            }

            // Nested async errors
            if (SimulateNestedAsyncError && EnableNestedAsyncSimulation)
            {
                await SimulateNestedAsyncCall(AsyncNestingLevel);
            }

            // Sync context handling
            if (SimulateSyncContextDeadlock && UseConfigureAwaitFalse)
            {
                SyncContextDeadlockAvoided = true;
                ConfigureAwaitUsedCorrectly = true;
                // Proper ConfigureAwait(false) usage prevents deadlock
            }

            if (SimulateStateCorruption)
            {
                _internalState["corrupted"] = true;
                throw new InvalidOperationException("State corruption detected");
            }
        }

        private async Task HandleMemoryError()
        {
            var currentFailure = Interlocked.Increment(ref _memoryFailureCount);


            if (EnableMemoryRecovery && currentFailure <= MemoryFailureCountBeforeSuccess)
            {
                // Simulate memory cleanup
                MemoryCleanupCount++;
                GarbageCollectionTriggered = true;

                // Simulate cleanup delay

                await Task.Delay(50);


                throw new OutOfMemoryException("Memory allocation failed");
            }
        }

        private async Task HandleRetryWithBackoff(int attemptNumber)
        {
            if (!UseExponentialBackoff) return;

            var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attemptNumber - 1));
            RetryDelays.Add(delay);
            await Task.Delay(delay);
        }

        private void IncrementCircuitBreakerFailures()
        {
            if (!EnableCircuitBreaker) return;

            var failureCount = Interlocked.Increment(ref _circuitBreakerFailures);
            if (failureCount >= CircuitBreakerThreshold)
            {
                CircuitBreakerState = CircuitBreakerState.Open;
                _ = Task.Delay(CircuitBreakerTimeout).ContinueWith(_ =>
                {
                    CircuitBreakerState = CircuitBreakerState.HalfOpen;
                });
            }
        }

        private void CollectDiagnosticInfo()
        {
            LastDiagnosticInfo = new Dictionary<string, object>
            {
                ["MemoryUsage"] = GC.GetTotalMemory(false),
                ["ThreadCount"] = Environment.ProcessorCount,
                ["SystemLoad"] = 0.5, // Simulated
                ["Timestamp"] = DateTime.UtcNow
            };
        }
        /// <summary>
        /// Calculates the state checksum.
        /// </summary>
        /// <returns>The calculated state checksum.</returns>

        public string CalculateStateChecksum() => _internalState.Count.ToString();
        /// <summary>
        /// Performs restore from checkpoint.
        /// </summary>

        public void RestoreFromCheckpoint()
        {
            if (_checkpointState != null)
            {
                StateRestorationCount++;
                _internalState.Clear();
                // Restore state (simplified)
                _internalState["restored"] = true;
            }
        }

        protected override ValueTask SynchronizeCoreAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        // Additional helper methods for new error simulations
        private void DeepMethodCall1() => DeepMethodCall2();

        private void DeepMethodCall2() => DeepMethodCall3();

        private void DeepMethodCall3()
        {
            var ex = new InvalidOperationException("Deep stack error from level 3");
            ex.Data["OriginalStackTrace"] = Environment.StackTrace;
            throw ex;
        }

        private async Task SimulateNestedAsyncCall(int nestingLevel)
        {
            if (nestingLevel <= 1)
            {
                var ex = new InvalidOperationException("Nested async error at base level");
                ex.Data["NestingLevel"] = AsyncNestingLevel;
                ex.Data["AsyncCallChain"] = $"Level_{nestingLevel}";
                throw ex;
            }

            await SimulateNestedAsyncCall(nestingLevel - 1);
        }
        /// <summary>
        /// Sets the correlation id.
        /// </summary>
        /// <param name="correlationId">The correlation identifier.</param>

        public void SetCorrelationId(Guid correlationId) => _correlationId = correlationId;
        /// <summary>
        /// Validates the kernel parameters.
        /// </summary>
        /// <param name="parameters">The parameters.</param>

        // Synchronous validation methods
        public void ValidateKernelParameters(Dictionary<string, object>? parameters)
        {
            SynchronousValidationCount++;


            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            foreach (var kvp in parameters)
            {
                if (kvp.Key.Contains('@', StringComparison.OrdinalIgnoreCase) || kvp.Key.Contains('#'))
                    throw new ArgumentException("Invalid parameter key format", nameof(parameters));
            }
        }
        /// <summary>
        /// Validates the memory size.
        /// </summary>
        /// <param name="size">The size.</param>

        public void ValidateMemorySize(long size)
        {
            SynchronousValidationCount++;


            if (size < 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Memory size cannot be negative");
        }
        /// <summary>
        /// Performs acquire exclusive resource.
        /// </summary>
        /// <param name="resourceName">The resource name.</param>

        public void AcquireExclusiveResource(string resourceName)
        {
            if (SimulateResourceContention)
            {
                ResourceContentionDetected = true;
                LockTimeoutCount++;
                throw new TimeoutException($"Resource lock timeout for '{resourceName}' after {ResourceLockTimeout}");
            }
        }

        private volatile int _circuitBreakerFailures;
        private volatile int _concurrentErrorCount;
        /// <summary>
        /// A class that represents stack overflow info.
        /// </summary>

        public class StackOverflowInfo
        {
            /// <summary>
            /// Gets or sets the kernel name.
            /// </summary>
            /// <value>The kernel name.</value>
            public string? KernelName { get; set; }
            /// <summary>
            /// Gets or sets the stack depth.
            /// </summary>
            /// <value>The stack depth.</value>
            public int StackDepth { get; set; }
            /// <summary>
            /// Gets or sets the detected at.
            /// </summary>
            /// <value>The detected at.</value>
            public DateTime DetectedAt { get; set; }
        }
    }
}
/// <summary>
/// An circuit breaker state enumeration.
/// </summary>

/// <summary>
/// Circuit breaker state enumeration for testing
/// </summary>
public enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen
}

/// <summary>
/// Exception thrown when circuit breaker is in open state
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    /// <summary>
    /// Initializes a new instance of the CircuitBreakerOpenException class.
    /// </summary>
    public CircuitBreakerOpenException() { }
    /// <summary>
    /// Initializes a new instance of the CircuitBreakerOpenException class.
    /// </summary>
    /// <param name="message">The message.</param>
    public CircuitBreakerOpenException(string message) : base(message) { }
    /// <summary>
    /// Initializes a new instance of the CircuitBreakerOpenException class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CircuitBreakerOpenException(string message, Exception innerException) : base(message, innerException) { }
}