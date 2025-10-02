// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Hardware.Cuda.Tests
{
    /* Temporarily disabled - requires refactoring for ProductionCudaAccelerator pattern
    /// <summary>
    /// Tests for CUDA performance monitoring and profiling features.
    /// </summary>
    public class CudaPerformanceMonitoringTests : CudaTestBase
    {
        private readonly NvmlWrapper _nvml;
        private readonly CuptiWrapper _cupti;
        private readonly CudaKernelProfiler _profiler;
        private readonly CudaAccelerator? _accelerator;

        public CudaPerformanceMonitoringTests(ITestOutputHelper output) : base(output)
        {
            var logger = new TestLogger(output);
            _nvml = new NvmlWrapper(logger);
            _cupti = new CuptiWrapper(logger);
            
            if (IsCudaAvailable().Result)
            {
                using var factory = new CudaAcceleratorFactory();
                _accelerator = factory.CreateProductionAccelerator(0) as CudaAccelerator;
                
                if (_accelerator != null)
                {
                    _profiler = new CudaKernelProfiler(_accelerator.Context, logger);
                }
            }
        }

        [Fact]
        public async Task NvmlWrapper_ShouldInitializeAndGetGpuMetrics()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");

            var initialized = _nvml.Initialize();
            
            if (!initialized)
            {
                Output.WriteLine("NVML not available - skipping test");
                return;
            }

            Assert.True(initialized);

            // Get metrics for device 0
            var metrics = _nvml.GetDeviceMetrics(0);
            
            Assert.NotNull(metrics);
            if (metrics.IsAvailable)
            {
                Assert.True(metrics.Temperature > 0 && metrics.Temperature < 100, $"Temperature {metrics.Temperature}°C seems invalid");
                Assert.True(metrics.PowerUsage >= 0, $"Power usage {metrics.PowerUsage}W should be non-negative");
                Assert.True(metrics.MemoryTotal > 0, "Total memory should be positive");
                Assert.True(metrics.GpuUtilization >= 0 && metrics.GpuUtilization <= 100, $"GPU utilization {metrics.GpuUtilization}% out of range");
                
                Output.WriteLine($"GPU Metrics: {metrics}");
                Output.WriteLine($"  Temperature: {metrics.Temperature}°C");
                Output.WriteLine($"  Power: {metrics.PowerUsage:F1}W");
                Output.WriteLine($"  GPU Utilization: {metrics.GpuUtilization}%");
                Output.WriteLine($"  Memory: {metrics.MemoryUsed / 1048576}MB / {metrics.MemoryTotal / 1048576}MB ({metrics.MemoryUtilization:F1}%)");
                Output.WriteLine($"  Clocks: Core={metrics.GraphicsClockMHz}MHz, Memory={metrics.MemoryClockMHz}MHz");
                
                if (metrics.IsThrottling)
                {
                    Output.WriteLine($"  THROTTLING: {metrics.ThrottleReasons}");
                }
            }
            else
            {
                Output.WriteLine("GPU metrics not available");
            }
        }

        [Fact]
        public async Task CuptiWrapper_ShouldInitializeAndDiscoverMetrics()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");

            var initialized = _cupti.Initialize();
            
            if (!initialized)
            {
                Output.WriteLine("CUPTI not available - skipping test");
                return;
            }

            Assert.True(initialized);

            // Start profiling session
            var session = _cupti.StartProfiling(new[] 
            { 
                "achieved_occupancy", 
                "sm_efficiency",
                "dram_read_throughput",
                "dram_write_throughput"
            });

            Assert.NotNull(session);
            Assert.NotEmpty(session.RequestedMetrics);
            
            // Simulate some GPU work
            if (_accelerator != null)
            {
                var buffer = _accelerator.AllocateBuffer<float>(1024);
                await buffer.ClearAsync();
                await _accelerator.SynchronizeAsync();
                buffer.Dispose();
            }

            // Collect metrics
            var metrics = _cupti.CollectMetrics(session);
            
            Assert.NotNull(metrics);
            
            Output.WriteLine("CUPTI Metrics:");
            Output.WriteLine($"  Kernel Executions: {metrics.KernelExecutions}");
            Output.WriteLine($"  Memory Transfers: {metrics.MemoryTransfers}");
            
            if (metrics.MetricValues.Any())
            {
                Output.WriteLine($"  Achieved Occupancy: {metrics.AchievedOccupancy:F2}");
                Output.WriteLine($"  SM Efficiency: {metrics.SmEfficiency:F2}");
                Output.WriteLine($"  DRAM Read Throughput: {metrics.DramReadThroughput:F2} GB/s");
                Output.WriteLine($"  DRAM Write Throughput: {metrics.DramWriteThroughput:F2} GB/s");
            }
        }

        [Fact]
        public async Task CudaKernelProfiler_ShouldProfileKernelExecution()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");
            
            Skip.If(_profiler == null || _accelerator == null, "Profiler or accelerator not available");

            // Start profiling
            _profiler.StartProfiling();

            // Create and execute a simple kernel
            var kernelCode = @"
                extern ""C"" __global__ void vectorAdd(float* a, float* b, float* c, int n) {
                    int idx = blockIdx.x * blockDim.x + threadIdx.x;
                    if (idx < n) {
                        c[idx] = a[idx] + b[idx];
                    }
                }
            ";

            var kernel = await _accelerator.CompileKernelAsync(
                new KernelDefinition
                {
                    Name = "vectorAdd",
                    Code = kernelCode,
                    Language = KernelLanguage.CUDA
                }
            );

            const int size = 1024 * 1024;
            var bufferA = _accelerator.AllocateBuffer<float>(size);
            var bufferB = _accelerator.AllocateBuffer<float>(size);
            var bufferC = _accelerator.AllocateBuffer<float>(size);

            // Initialize data
            var dataA = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
            var dataB = Enumerable.Range(0, size).Select(i => (float)(i * 2)).ToArray();
            await bufferA.CopyFromAsync(dataA.AsMemory());
            await bufferB.CopyFromAsync(dataB.AsMemory());

            // Profile kernel execution
            var executionMetrics = await _profiler.ProfileKernelExecutionAsync(
                "vectorAdd",
                async () =>
                {
                    await kernel.ExecuteAsync(new KernelArguments
                    {
                        GridDimensions = new GridDimensions(256, 1, 1),
                        BlockDimensions = new BlockDimensions(256, 1, 1),
                        Arguments = new object[] { bufferA, bufferB, bufferC, size }
                    });
                }
            );

            Assert.NotNull(executionMetrics);
            Assert.True(executionMetrics.ExecutionTime.TotalMilliseconds > 0);
            
            // Stop profiling and get report
            _profiler.StopProfiling();
            var report = await _profiler.GenerateReportAsync();

            Assert.NotNull(report);
            Assert.Contains("vectorAdd", report.KernelMetrics.Keys);
            
            var kernelMetrics = report.KernelMetrics["vectorAdd"];
            Assert.True(kernelMetrics.TotalExecutionTime > TimeSpan.Zero);
            Assert.True(kernelMetrics.ExecutionCount > 0);
            
            Output.WriteLine("Kernel Profiling Results:");
            Output.WriteLine($"  Kernel: vectorAdd");
            Output.WriteLine($"  Execution Time: {executionMetrics.ExecutionTime.TotalMilliseconds:F3}ms");
            Output.WriteLine($"  Memory Transferred: {executionMetrics.MemoryTransferredBytes / (1024.0 * 1024.0):F2} MB");
            Output.WriteLine($"  Achieved Occupancy: {executionMetrics.AchievedOccupancy:F2}");
            Output.WriteLine($"  SM Efficiency: {executionMetrics.SmEfficiency:F2}");
            Output.WriteLine($"  Memory Bandwidth: {executionMetrics.MemoryBandwidth:F2} GB/s");
            
            if (report.BottleneckAnalysis != null)
            {
                Output.WriteLine($"  Primary Bottleneck: {report.BottleneckAnalysis.PrimaryBottleneck}");
                Output.WriteLine($"  Secondary Bottleneck: {report.BottleneckAnalysis.SecondaryBottleneck}");
                
                foreach (var recommendation in report.BottleneckAnalysis.Recommendations)
                {
                    Output.WriteLine($"    - {recommendation}");
                }
            }

            // Cleanup
            bufferA.Dispose();
            bufferB.Dispose();
            bufferC.Dispose();
            kernel.Dispose();
        }

        [Fact]
        public async Task KernelProfiler_ShouldIdentifyBottlenecks()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");
            
            Skip.If(_profiler == null || _accelerator == null, "Profiler or accelerator not available");

            // Create a memory-bound kernel
            var memoryBoundKernel = @"
                extern ""C"" __global__ void memoryBound(float* data, int n) {
                    int idx = blockIdx.x * blockDim.x + threadIdx.x;
                    if (idx < n) {
                        // Strided memory access pattern (poor coalescing)
                        for (int i = 0; i < 100; i++) {
                            data[(idx * 17) % n] += 1.0f;
                        }
                    }
                }
            ";

            var kernel = await _accelerator.CompileKernelAsync(
                new KernelDefinition
                {
                    Name = "memoryBound",
                    Code = memoryBoundKernel,
                    Language = KernelLanguage.CUDA
                }
            );

            const int size = 1024 * 1024;
            var buffer = _accelerator.AllocateBuffer<float>(size);

            _profiler.StartProfiling();

            var metrics = await _profiler.ProfileKernelExecutionAsync(
                "memoryBound",
                async () =>
                {
                    await kernel.ExecuteAsync(new KernelArguments
                    {
                        GridDimensions = new GridDimensions(256, 1, 1),
                        BlockDimensions = new BlockDimensions(256, 1, 1),
                        Arguments = new object[] { buffer, size }
                    });
                }
            );

            _profiler.StopProfiling();
            var report = await _profiler.GenerateReportAsync();

            Assert.NotNull(report.BottleneckAnalysis);
            
            // Memory-bound kernels should show memory-related bottlenecks
            Assert.True(
                report.BottleneckAnalysis.PrimaryBottleneck == Core.Kernels.Types.BottleneckType.MemoryBandwidth ||
                report.BottleneckAnalysis.PrimaryBottleneck == Core.Kernels.Types.BottleneckType.MemoryLatency,
                $"Expected memory bottleneck but got {report.BottleneckAnalysis.PrimaryBottleneck}"
            );

            Output.WriteLine("Bottleneck Analysis:");
            Output.WriteLine($"  Primary: {report.BottleneckAnalysis.PrimaryBottleneck}");
            Output.WriteLine($"  Severity: {report.BottleneckAnalysis.Severity}");
            Output.WriteLine($"  Recommendations: {string.Join(", ", report.BottleneckAnalysis.Recommendations)}");

            // Cleanup
            buffer.Dispose();
            kernel.Dispose();
        }

        [Fact]
        public async Task PerformanceMonitoring_ShouldTrackMemoryUsage()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");
            
            Skip.If(_accelerator == null, "Accelerator not available");

            var nvmlAvailable = _nvml.Initialize();
            
            if (!nvmlAvailable)
            {
                Output.WriteLine("NVML not available for memory tracking");
                return;
            }

            // Get initial memory state
            var initialMetrics = _nvml.GetDeviceMetrics(0);
            var initialMemoryUsed = initialMetrics.MemoryUsed;

            // Allocate some buffers
            const int bufferSize = 100 * 1024 * 1024; // 100 MB
            var buffers = new IMemoryBuffer<float>[5];
            
            for (var i = 0; i < buffers.Length; i++)
            {
                buffers[i] = _accelerator.AllocateBuffer<float>(bufferSize / sizeof(float));
                
                // Get memory after allocation
                var metrics = _nvml.GetDeviceMetrics(0);
                var memoryDelta = (long)metrics.MemoryUsed - (long)initialMemoryUsed;
                
                Output.WriteLine($"After allocating buffer {i + 1}:");
                Output.WriteLine($"  Memory Used: {metrics.MemoryUsed / 1048576} MB");
                Output.WriteLine($"  Delta from start: {memoryDelta / 1048576} MB");
            }

            // Free buffers and check memory
            foreach (var buffer in buffers)
            {
                buffer.Dispose();
            }

            // Force garbage collection and wait
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(100);

            var finalMetrics = _nvml.GetDeviceMetrics(0);
            var finalMemoryDelta = Math.Abs((long)finalMetrics.MemoryUsed - (long)initialMemoryUsed);

            Output.WriteLine($"Final memory state:");
            Output.WriteLine($"  Memory Used: {finalMetrics.MemoryUsed / 1048576} MB");
            Output.WriteLine($"  Delta from start: {finalMemoryDelta / 1048576} MB");

            // Memory should be mostly freed (allow some tolerance for driver overhead)
            Assert.True(finalMemoryDelta < 50 * 1048576, $"Memory not properly freed: {finalMemoryDelta / 1048576} MB still in use");
        }

        [Fact]
        public async Task PerformanceMonitoring_ShouldDetectThrottling()
        {
            var hasCuda = IsCudaAvailable();
            Skip.If(!hasCuda, "CUDA is not available");

            var nvmlAvailable = _nvml.Initialize();
            
            if (!nvmlAvailable)
            {
                Output.WriteLine("NVML not available for throttling detection");
                return;
            }

            var metrics = _nvml.GetDeviceMetrics(0);
            
            if (metrics.IsAvailable)
            {
                Output.WriteLine($"Throttling Status: {(metrics.IsThrottling ? "YES" : "NO")}");
                
                if (metrics.IsThrottling)
                {
                    Output.WriteLine($"Throttle Reasons: {metrics.ThrottleReasons}");
                    Assert.NotEmpty(metrics.ThrottleReasons);
                }
                
                Output.WriteLine($"Current Temperature: {metrics.Temperature}°C");
                Output.WriteLine($"Current Power: {metrics.PowerUsage:F1}W");
                Output.WriteLine($"Fan Speed: {metrics.FanSpeedPercent}%");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _profiler?.Dispose();
                _nvml?.Dispose();
                _cupti?.Dispose();
                _accelerator?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
    */
}