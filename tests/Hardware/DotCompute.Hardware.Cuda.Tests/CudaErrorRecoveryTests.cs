// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Hardware.Cuda.Tests
{
    /* Temporarily disabled - requires refactoring for ProductionCudaAccelerator pattern
    /// <summary>
    /// Comprehensive tests for CUDA error recovery and resilience mechanisms.
    /// </summary>
    public class CudaErrorRecoveryTests : CudaTestBase
    {
        private readonly CudaErrorRecoveryManager _recoveryManager;
        private readonly CudaContextStateManager _stateManager;
        private readonly CudaContext _context;

        public CudaErrorRecoveryTests(ITestOutputHelper output) : base(output)
        {
            if (IsCudaAvailable().Result)
            {
                using var factory = new CudaAcceleratorFactory();
                var accelerator = factory.CreateProductionAccelerator(0) as CudaAccelerator;
                _context = accelerator?.Context ?? new CudaContext(0);
                
                var logger = new TestLogger(output);
                _recoveryManager = new CudaErrorRecoveryManager(_context, logger);
                _stateManager = new CudaContextStateManager(logger);
            }
        }

        [Fact]
        public async Task ErrorRecoveryManager_ShouldRecoverFromTransientErrors()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");

            // Simulate a transient error that should be recoverable
            var recovered = false;
            var attempts = 0;

            var result = await _recoveryManager.ExecuteWithRecoveryAsync(
                async () =>
                {
                    attempts++;
                    if (attempts < 2)
                    {
                        throw new CudaException(CudaError.NotReady, "Simulated transient error");
                    }
                    recovered = true;
                    return true;
                },
                "TestOperation"
            );

            Assert.True(result);
            Assert.True(recovered);
            Assert.Equal(2, attempts);
            
            var stats = _recoveryManager.GetStatistics();
            Assert.True(stats.RecoverySuccessRate > 0);
            
            Output.WriteLine($"Recovery stats: Total errors: {stats.TotalErrors}, Recovered: {stats.RecoveredErrors}");
        }

        [Fact]
        public async Task ErrorRecoveryManager_ShouldFailOnPermanentErrors()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");

            var attempts = 0;

            _ = await Assert.ThrowsAsync<CudaException>(async () =>
            {
                _ = await _recoveryManager.ExecuteWithRecoveryAsync<bool>(
                    async () =>
                    {
                        attempts++;
                        throw new CudaException(CudaError.InvalidValue, "Permanent error");
                    },
                    "TestOperation",
                    RecoveryOptions.Default
                );
            });

            Assert.Equal(1, attempts); // Should not retry for permanent errors
            
            var stats = _recoveryManager.GetStatistics();
            Assert.True(stats.PermanentFailures > 0);
        }

        [Fact]
        public async Task ContextStateManager_ShouldTrackMemoryAllocations()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");

            var ptr1 = new IntPtr(0x1000);
            var ptr2 = new IntPtr(0x2000);

            _stateManager.RegisterMemoryAllocation(ptr1, 1024, MemoryType.Device, "TestBuffer1");
            _stateManager.RegisterMemoryAllocation(ptr2, 2048, MemoryType.Unified, "TestBuffer2");

            var stats = _stateManager.GetStatistics();
            Assert.Equal(2, stats.ActiveAllocations);
            Assert.Equal(3072, stats.TotalMemoryAllocated);

            _stateManager.UnregisterMemoryAllocation(ptr1);
            
            stats = _stateManager.GetStatistics();
            Assert.Equal(1, stats.ActiveAllocations);
            Assert.Equal(1024, stats.TotalMemoryFreed);
            
            Output.WriteLine($"Memory tracking: Allocated: {stats.TotalMemoryAllocated}, Freed: {stats.TotalMemoryFreed}");
        }

        [Fact]
        public async Task ContextStateManager_ShouldCreateAndRestoreSnapshots()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");

            // Register some resources
            _stateManager.RegisterMemoryAllocation(new IntPtr(0x3000), 4096, MemoryType.Device, "SnapshotTest");
            _stateManager.RegisterStream(new IntPtr(0x4000), StreamPriority.High);
            _stateManager.RegisterKernel("testKernel", new byte[] { 0x01, 0x02, 0x03 });

            // Create snapshot
            var snapshot = await _stateManager.CreateSnapshotAsync();
            
            Assert.NotNull(snapshot);
            Assert.Equal(1, snapshot.MemoryAllocations.Count);
            Assert.Equal(1, snapshot.ActiveStreams.Count);
            Assert.Equal(1, snapshot.CompiledKernels.Count);
            
            Output.WriteLine($"Created snapshot {snapshot.SnapshotId} with {snapshot.MemoryAllocations.Count} allocations");

            // Simulate recovery by clearing and restoring
            await _stateManager.PrepareForRecoveryAsync();
            
            var restoreResult = await _stateManager.RestoreFromSnapshotAsync(snapshot);
            
            Assert.True(restoreResult.Success);
            Assert.Equal(1, restoreResult.RestoredKernels);
            Assert.Equal(1, restoreResult.MemoryAllocationsLost); // Memory not automatically restored
            
            Output.WriteLine($"Restoration result: {restoreResult.Message}");
        }

        [Fact]
        public async Task ContextStateManager_ShouldHandleProgressiveRecovery()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");

            // Test different recovery strategies
            var recoveryResult = await _stateManager.PerformProgressiveRecoveryAsync(
                CudaError.NotReady, 
                attemptNumber: 1
            );

            Assert.NotNull(recoveryResult);
            Assert.Equal(RecoveryStrategy.StreamSync, recoveryResult.Strategy);
            
            Output.WriteLine($"Progressive recovery strategy: {recoveryResult.Strategy}, Success: {recoveryResult.Success}");

            // Test escalation on subsequent attempts
            recoveryResult = await _stateManager.PerformProgressiveRecoveryAsync(
                CudaError.IllegalAddress, 
                attemptNumber: 2
            );

            Assert.Equal(RecoveryStrategy.ContextReset, recoveryResult.Strategy);
        }

        [Fact]
        public async Task ErrorRecoveryManager_ShouldRespectCircuitBreaker()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");

            var failureCount = 0;
            
            // Cause multiple failures to trip circuit breaker
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    _ = await _recoveryManager.ExecuteWithRecoveryAsync<bool>(
                        async () =>
                        {
                            failureCount++;
                            throw new CudaException(CudaError.LaunchFailure, "Repeated failure");
                        },
                        $"TestOperation{i}",
                        new RecoveryOptions { MaxRetries = 1 }
                    );
                }
                catch (CudaException)
                {
                    // Expected
                }
            }

            // Circuit should be open now - next call should fail immediately
            var sw = System.Diagnostics.Stopwatch.StartNew();

            _ = await Assert.ThrowsAsync<CudaException>(async () =>
            {
                _ = await _recoveryManager.ExecuteWithRecoveryAsync<bool>(
                    async () => true,
                    "TestOperationCircuitOpen"
                );
            });
            
            sw.Stop();
            
            // Should fail fast due to open circuit
            Assert.True(sw.ElapsedMilliseconds < 100, $"Circuit breaker should fail fast but took {sw.ElapsedMilliseconds}ms");
            
            Output.WriteLine($"Circuit breaker test: {failureCount} failures, circuit open after {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task ErrorRecoveryManager_ShouldTrackResourcesAcrossRecovery()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");

            // Register resources with recovery manager
            var ptr = new IntPtr(0x5000);
            _recoveryManager.RegisterMemoryAllocation(ptr, 8192, MemoryType.Device, "TrackedBuffer");
            
            var stream = new IntPtr(0x6000);
            _recoveryManager.RegisterStream(stream, StreamPriority.Default);
            
            _recoveryManager.RegisterKernel("trackedKernel", new byte[] { 0xFF, 0xFE, 0xFD }, new byte[] { 0x01, 0x02 });

            // Create snapshot through recovery manager
            var snapshot = await _recoveryManager.CreateContextSnapshotAsync();
            
            Assert.NotNull(snapshot);
            Assert.Equal(1, snapshot.MemoryAllocations.Count);
            Assert.Equal(1, snapshot.ActiveStreams.Count);
            Assert.Equal(1, snapshot.CompiledKernels.Count);
            
            var stats = _recoveryManager.GetStatistics();
            Assert.NotNull(stats.ResourceStatistics);
            Assert.Equal(1, stats.ResourceStatistics.ActiveAllocations);
            
            Output.WriteLine($"Resource tracking: {stats.ResourceStatistics.ActiveAllocations} allocations, " +
                          $"{stats.ResourceStatistics.ActiveStreams} streams, " +
                          $"{stats.ResourceStatistics.CompiledKernels} kernels");

            // Cleanup
            _recoveryManager.UnregisterMemoryAllocation(ptr);
            _recoveryManager.UnregisterStream(stream);
        }

        [Fact]
        public async Task RecoveryOptions_ShouldProvidePresetConfigurations()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");

            // Test default options
            var defaultOpts = RecoveryOptions.Default;
            Assert.True(defaultOpts.EnableContextRecovery);
            Assert.True(defaultOpts.EnableCircuitBreaker);
            Assert.Equal(3, defaultOpts.MaxRetries);

            // Test critical options
            var criticalOpts = RecoveryOptions.Critical;
            Assert.True(criticalOpts.EnableContextRecovery);
            Assert.False(criticalOpts.EnableCircuitBreaker); // No circuit breaker for critical ops
            Assert.Equal(5, criticalOpts.MaxRetries);

            // Test fast-fail options
            var fastFailOpts = RecoveryOptions.FastFail;
            Assert.False(fastFailOpts.EnableContextRecovery);
            Assert.True(fastFailOpts.EnableCircuitBreaker);
            Assert.Equal(1, fastFailOpts.MaxRetries);
            
            Output.WriteLine("Recovery options presets validated");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _recoveryManager?.Dispose();
                _stateManager?.Dispose();
                _context?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Test logger implementation for testing.
    /// </summary>
    internal sealed class TestLogger : ILogger
    {
        private readonly ITestOutputHelper _output;

        public TestLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public IDisposable BeginScope<TState>(TState state) => new NoOpDisposable();

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        }

        private class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
    */
}
