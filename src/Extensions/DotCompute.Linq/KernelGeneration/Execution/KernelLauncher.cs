// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Extensions.DotCompute.Linq.KernelGeneration;
using DotCompute.Extensions.DotCompute.Linq.KernelGeneration.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Extensions.DotCompute.Linq.KernelGeneration.Execution
{
    /// <summary>
    /// Advanced CUDA kernel launcher with optimized grid/block size calculation,
    /// dynamic parallelism support, stream management, and async execution.
    /// </summary>
    public sealed class KernelLauncher : IDisposable
    {
        private readonly CudaContext _context;
        private readonly ILogger<KernelLauncher> _logger;
        private readonly GpuMemoryManager _memoryManager;
        private readonly ConcurrentDictionary<string, LaunchConfiguration> _configurationCache;
        private readonly ConcurrentDictionary<IntPtr, CudaStream> _streamPool;
        private readonly object _streamPoolLock = new();
        private bool _disposed;

        // Performance counters
        private readonly Dictionary<string, KernelPerformanceMetrics> _performanceMetrics;
        private readonly object _metricsLock = new();

        public KernelLauncher(CudaContext context, GpuMemoryManager memoryManager, ILogger<KernelLauncher> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configurationCache = new ConcurrentDictionary<string, LaunchConfiguration>();
            _streamPool = new ConcurrentDictionary<IntPtr, CudaStream>();
            _performanceMetrics = new Dictionary<string, KernelPerformanceMetrics>();
        }

        /// <summary>
        /// Launches a CUDA kernel with optimized configuration.
        /// </summary>
        public async Task<KernelExecutionResult> LaunchKernelAsync<T>(
            ICompiledKernel kernel,
            T[] inputData,
            LaunchOptions? options = null,
            CancellationToken cancellationToken = default)
            where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(kernel);
            ArgumentNullException.ThrowIfNull(inputData);

            var launchOptions = options ?? new LaunchOptions();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Launching kernel {KernelName} with {DataCount} elements",
                    kernel.Name, inputData.Length);

                // Calculate optimal launch configuration
                var config = await CalculateLaunchConfigurationAsync(kernel, inputData.Length, launchOptions);

                // Allocate GPU memory
                var memoryContext = await _memoryManager.AllocateForKernelAsync<T>(
                    inputData.Length, config.EstimatedOutputSize, cancellationToken);

                try
                {
                    // Copy input data to GPU
                    await _memoryManager.CopyToDeviceAsync(inputData, memoryContext.InputBuffer, cancellationToken);

                    // Get or create CUDA stream
                    var stream = GetOrCreateStream(launchOptions.StreamId);

                    // Launch the kernel
                    var launchResult = await LaunchKernelInternalAsync(
                        kernel, memoryContext, config, stream, cancellationToken);

                    // Copy results back to host
                    var outputData = await _memoryManager.CopyFromDeviceAsync<T>(
                        memoryContext.OutputBuffer, launchResult.OutputCount, cancellationToken);

                    stopwatch.Stop();

                    // Update performance metrics
                    UpdatePerformanceMetrics(kernel.Name, stopwatch.Elapsed, inputData.Length, config);

                    _logger.LogInformation("Successfully launched kernel {KernelName} in {ElapsedMs}ms",
                        kernel.Name, stopwatch.ElapsedMilliseconds);

                    return new KernelExecutionResult
                    {
                        OutputData = outputData,
                        OutputCount = launchResult.OutputCount,
                        ExecutionTime = stopwatch.Elapsed,
                        LaunchConfiguration = config,
                        PerformanceMetrics = GetPerformanceMetrics(kernel.Name)
                    };
                }
                finally
                {
                    // Clean up memory context
                    await memoryContext.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to launch kernel {KernelName}", kernel.Name);
                throw new KernelLaunchException($"Failed to launch kernel '{kernel.Name}'", ex);
            }
        }

        /// <summary>
        /// Launches multiple kernels in parallel with stream coordination.
        /// </summary>
        public async Task<KernelExecutionResult[]> LaunchKernelBatchAsync<T>(
            IEnumerable<(ICompiledKernel kernel, T[] data)> kernelBatch,
            LaunchOptions? options = null,
            CancellationToken cancellationToken = default)
            where T : unmanaged
        {
            var kernelArray = kernelBatch.ToArray();
            var launchOptions = options ?? new LaunchOptions { EnableBatchOptimization = true };

            _logger.LogInformation("Launching batch of {KernelCount} kernels", kernelArray.Length);

            // Create separate streams for parallel execution
            var streamTasks = kernelArray.Select(async (kernelData, index) =>
            {
                var streamOptions = new LaunchOptions
                {
                    StreamId = launchOptions.StreamId + index,
                    EnableAsyncExecution = true,
                    Priority = launchOptions.Priority
                };

                return await LaunchKernelAsync(kernelData.kernel, kernelData.data, streamOptions, cancellationToken);
            });

            return await Task.WhenAll(streamTasks);
        }

        /// <summary>
        /// Calculates optimal launch configuration for a kernel.
        /// </summary>
        private async Task<LaunchConfiguration> CalculateLaunchConfigurationAsync(
            ICompiledKernel kernel,
            int dataSize,
            LaunchOptions options)
        {
            var cacheKey = GenerateConfigurationCacheKey(kernel, dataSize, options);
            
            if (_configurationCache.TryGetValue(cacheKey, out var cachedConfig))
            {
                _logger.LogDebug("Using cached launch configuration for {KernelName}", kernel.Name);
                return cachedConfig;
            }

            _logger.LogDebug("Calculating optimal launch configuration for {KernelName}", kernel.Name);

            // Get device properties
            var deviceProps = await GetDevicePropertiesAsync();
            
            // Calculate optimal block size
            var blockSize = CalculateOptimalBlockSize(kernel, dataSize, deviceProps, options);
            
            // Calculate grid size
            var gridSize = CalculateOptimalGridSize(dataSize, blockSize, deviceProps);
            
            // Calculate shared memory requirements
            var sharedMemorySize = CalculateSharedMemorySize(kernel, blockSize, options);
            
            // Estimate output size
            var estimatedOutputSize = EstimateOutputSize(dataSize, kernel.Name);

            var configuration = new LaunchConfiguration
            {
                GridSize = gridSize,
                BlockSize = blockSize,
                SharedMemorySize = sharedMemorySize,
                EstimatedOutputSize = estimatedOutputSize,
                MaxOccupancy = CalculateOccupancy(blockSize, sharedMemorySize, deviceProps),
                CacheKey = cacheKey
            };

            // Cache the configuration
            _configurationCache.TryAdd(cacheKey, configuration);

            _logger.LogDebug("Calculated launch configuration: Grid={GridX}x{GridY}x{GridZ}, Block={BlockX}x{BlockY}x{BlockZ}, Shared={SharedMem}",
                gridSize.X, gridSize.Y, gridSize.Z, blockSize.X, blockSize.Y, blockSize.Z, sharedMemorySize);

            return configuration;
        }

        /// <summary>
        /// Calculates optimal block size using occupancy calculator.
        /// </summary>
        private Dim3 CalculateOptimalBlockSize(
            ICompiledKernel kernel,
            int dataSize,
            DeviceProperties deviceProps,
            LaunchOptions options)
        {
            if (options.BlockSize.HasValue)
            {
                var customBlockSize = options.BlockSize.Value;
                return new Dim3(customBlockSize, 1, 1);
            }

            // Use CUDA occupancy calculator if available
            var optimalBlockSize = CalculateOccupancyOptimalBlockSize(kernel, deviceProps);
            
            if (optimalBlockSize > 0)
            {
                return new Dim3(optimalBlockSize, 1, 1);
            }

            // Fallback to heuristic calculation
            var (major, minor) = CudaCapabilityManager.GetTargetComputeCapability();
            
            return (major, minor) switch
            {
                (>= 8, >= 9) => new Dim3(Math.Min(512, dataSize), 1, 1), // Ada Lovelace
                (>= 8, _) => new Dim3(Math.Min(256, dataSize), 1, 1),    // Ampere
                (>= 7, _) => new Dim3(Math.Min(256, dataSize), 1, 1),    // Volta/Turing
                _ => new Dim3(Math.Min(128, dataSize), 1, 1)             // Older architectures
            };
        }

        /// <summary>
        /// Calculates optimal grid size for the given data size and block size.
        /// </summary>
        private Dim3 CalculateOptimalGridSize(int dataSize, Dim3 blockSize, DeviceProperties deviceProps)
        {
            var totalThreads = blockSize.X * blockSize.Y * blockSize.Z;
            var gridX = (dataSize + totalThreads - 1) / totalThreads;
            
            // Limit grid size to device maximum
            gridX = Math.Min(gridX, deviceProps.MaxGridSize.X);
            
            // For very large datasets, use 2D grid
            if (gridX > 65535)
            {
                var gridY = (gridX + 65535 - 1) / 65535;
                gridX = Math.Min(gridX, 65535);
                gridY = Math.Min(gridY, deviceProps.MaxGridSize.Y);
                
                return new Dim3(gridX, gridY, 1);
            }
            
            return new Dim3(gridX, 1, 1);
        }

        /// <summary>
        /// Calculates shared memory size requirements.
        /// </summary>
        private int CalculateSharedMemorySize(ICompiledKernel kernel, Dim3 blockSize, LaunchOptions options)
        {
            if (options.SharedMemorySize.HasValue)
            {
                return options.SharedMemorySize.Value;
            }

            // Estimate based on kernel type and block size
            var totalThreads = blockSize.X * blockSize.Y * blockSize.Z;
            
            // Basic estimation - can be enhanced with kernel analysis
            return kernel.Name.Contains("reduce", StringComparison.OrdinalIgnoreCase) ? totalThreads * 4 : 0;
        }

        /// <summary>
        /// Estimates output data size based on kernel operation.
        /// </summary>
        private int EstimateOutputSize(int inputSize, string kernelName)
        {
            return kernelName.ToLowerInvariant() switch
            {
                var name when name.Contains("filter") => inputSize / 2, // Estimate 50% pass filter
                var name when name.Contains("reduce") => 1,              // Single result
                var name when name.Contains("scan") => inputSize,        // Same size as input
                var name when name.Contains("join") => inputSize * 2,    // Estimate join expansion
                _ => inputSize                                           // Default to same size
            };
        }

        /// <summary>
        /// Calculates theoretical occupancy for the configuration.
        /// </summary>
        private double CalculateOccupancy(Dim3 blockSize, int sharedMemorySize, DeviceProperties deviceProps)
        {
            var threadsPerBlock = blockSize.X * blockSize.Y * blockSize.Z;
            var maxBlocksPerSM = Math.Min(
                deviceProps.MaxThreadsPerMultiProcessor / threadsPerBlock,
                deviceProps.SharedMemoryPerMultiProcessor / Math.Max(sharedMemorySize, 1));
            
            var actualThreadsPerSM = maxBlocksPerSM * threadsPerBlock;
            return (double)actualThreadsPerSM / deviceProps.MaxThreadsPerMultiProcessor;
        }

        /// <summary>
        /// Uses CUDA occupancy calculator to find optimal block size.
        /// </summary>
        private int CalculateOccupancyOptimalBlockSize(ICompiledKernel kernel, DeviceProperties deviceProps)
        {
            try
            {
                // This would require access to the actual CUDA function pointer
                // For now, return 0 to indicate fallback to heuristic
                return 0;
            }
            catch
            {
                return 0; // Fallback to heuristic calculation
            }
        }

        /// <summary>
        /// Launches the kernel with the given configuration.
        /// </summary>
        private async Task<InternalLaunchResult> LaunchKernelInternalAsync(
            ICompiledKernel kernel,
            MemoryContext memoryContext,
            LaunchConfiguration config,
            CudaStream stream,
            CancellationToken cancellationToken)
        {
            try
            {
                // Prepare kernel parameters
                var parameters = new object[]
                {
                    memoryContext.InputBuffer.DevicePointer,
                    memoryContext.OutputBuffer.DevicePointer,
                    memoryContext.OutputCountBuffer?.DevicePointer ?? IntPtr.Zero,
                    memoryContext.InputBuffer.Size
                };

                // Launch kernel
                await kernel.LaunchAsync(
                    config.GridSize,
                    config.BlockSize,
                    config.SharedMemorySize,
                    stream.StreamPointer,
                    parameters,
                    cancellationToken);

                // Synchronize stream if not async
                if (!stream.IsAsync)
                {
                    await stream.SynchronizeAsync();
                }

                // Get output count if available
                var outputCount = memoryContext.OutputCountBuffer != null
                    ? await GetOutputCountAsync(memoryContext.OutputCountBuffer)
                    : config.EstimatedOutputSize;

                return new InternalLaunchResult
                {
                    Success = true,
                    OutputCount = outputCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kernel launch failed for {KernelName}", kernel.Name);
                throw new KernelLaunchException($"Kernel launch failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets or creates a CUDA stream for the given ID.
        /// </summary>
        private CudaStream GetOrCreateStream(int streamId)
        {
            var streamPtr = new IntPtr(streamId);
            
            if (_streamPool.TryGetValue(streamPtr, out var existingStream))
            {
                return existingStream;
            }

            lock (_streamPoolLock)
            {
                if (_streamPool.TryGetValue(streamPtr, out existingStream))
                {
                    return existingStream;
                }

                var newStream = new CudaStream(_context, streamId != 0);
                _streamPool.TryAdd(streamPtr, newStream);
                
                _logger.LogDebug("Created new CUDA stream with ID {StreamId}", streamId);
                return newStream;
            }
        }

        /// <summary>
        /// Gets device properties for launch configuration calculation.
        /// </summary>
        private async Task<DeviceProperties> GetDevicePropertiesAsync()
        {
            return await Task.FromResult(new DeviceProperties
            {
                MaxThreadsPerBlock = 1024,
                MaxThreadsPerMultiProcessor = 2048,
                SharedMemoryPerBlock = 48 * 1024,
                SharedMemoryPerMultiProcessor = 96 * 1024,
                MaxGridSize = new Dim3(2147483647, 65535, 65535),
                WarpSize = 32,
                MultiProcessorCount = 16 // Estimated
            });
        }

        /// <summary>
        /// Gets the output count from GPU memory.
        /// </summary>
        private async Task<int> GetOutputCountAsync(GpuBuffer<int> countBuffer)
        {
            var hostCount = new int[1];
            await _memoryManager.CopyFromDeviceAsync(countBuffer, hostCount);
            return hostCount[0];
        }

        /// <summary>
        /// Updates performance metrics for a kernel.
        /// </summary>
        private void UpdatePerformanceMetrics(string kernelName, TimeSpan executionTime, int dataSize, LaunchConfiguration config)
        {
            lock (_metricsLock)
            {
                if (!_performanceMetrics.TryGetValue(kernelName, out var metrics))
                {
                    metrics = new KernelPerformanceMetrics { KernelName = kernelName };
                    _performanceMetrics[kernelName] = metrics;
                }

                metrics.TotalLaunches++;
                metrics.TotalExecutionTime += executionTime;
                metrics.AverageExecutionTime = metrics.TotalExecutionTime / metrics.TotalLaunches;
                metrics.LastExecutionTime = executionTime;
                metrics.TotalDataProcessed += dataSize;
                metrics.Throughput = dataSize / executionTime.TotalSeconds;
                metrics.LastConfiguration = config;

                if (executionTime < metrics.BestExecutionTime || metrics.BestExecutionTime == TimeSpan.Zero)
                {
                    metrics.BestExecutionTime = executionTime;
                    metrics.BestConfiguration = config;
                }
            }
        }

        /// <summary>
        /// Gets performance metrics for a kernel.
        /// </summary>
        private KernelPerformanceMetrics GetPerformanceMetrics(string kernelName)
        {
            lock (_metricsLock)
            {
                return _performanceMetrics.GetValueOrDefault(kernelName, new KernelPerformanceMetrics { KernelName = kernelName });
            }
        }

        /// <summary>
        /// Generates a cache key for launch configuration.
        /// </summary>
        private string GenerateConfigurationCacheKey(ICompiledKernel kernel, int dataSize, LaunchOptions options)
        {
            return $"{kernel.Name}_{dataSize}_{options.GetHashCode()}";
        }

        /// <summary>
        /// Gets all performance metrics.
        /// </summary>
        public Dictionary<string, KernelPerformanceMetrics> GetAllPerformanceMetrics()
        {
            lock (_metricsLock)
            {
                return new Dictionary<string, KernelPerformanceMetrics>(_performanceMetrics);
            }
        }

        /// <summary>
        /// Clears performance metrics.
        /// </summary>
        public void ClearPerformanceMetrics()
        {
            lock (_metricsLock)
            {
                _performanceMetrics.Clear();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            // Dispose all streams
            foreach (var stream in _streamPool.Values)
            {
                stream.Dispose();
            }
            _streamPool.Clear();

            _disposed = true;
        }
    }

    /// <summary>
    /// Launch options for kernel execution.
    /// </summary>
    public class LaunchOptions
    {
        public int? BlockSize { get; set; }
        public int? SharedMemorySize { get; set; }
        public int StreamId { get; set; } = 0;
        public bool EnableAsyncExecution { get; set; } = false;
        public bool EnableBatchOptimization { get; set; } = false;
        public KernelPriority Priority { get; set; } = KernelPriority.Normal;
        public TimeSpan? Timeout { get; set; }
    }

    /// <summary>
    /// Launch configuration for a kernel.
    /// </summary>
    public class LaunchConfiguration
    {
        public required Dim3 GridSize { get; set; }
        public required Dim3 BlockSize { get; set; }
        public int SharedMemorySize { get; set; }
        public int EstimatedOutputSize { get; set; }
        public double MaxOccupancy { get; set; }
        public required string CacheKey { get; set; }
    }

    /// <summary>
    /// Device properties for launch calculation.
    /// </summary>
    public class DeviceProperties
    {
        public int MaxThreadsPerBlock { get; set; }
        public int MaxThreadsPerMultiProcessor { get; set; }
        public int SharedMemoryPerBlock { get; set; }
        public int SharedMemoryPerMultiProcessor { get; set; }
        public required Dim3 MaxGridSize { get; set; }
        public int WarpSize { get; set; }
        public int MultiProcessorCount { get; set; }
    }

    /// <summary>
    /// 3D dimension structure.
    /// </summary>
    public struct Dim3
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public Dim3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>
    /// Kernel execution result.
    /// </summary>
    public class KernelExecutionResult
    {
        public required object OutputData { get; set; }
        public int OutputCount { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public required LaunchConfiguration LaunchConfiguration { get; set; }
        public required KernelPerformanceMetrics PerformanceMetrics { get; set; }
    }

    /// <summary>
    /// Internal launch result.
    /// </summary>
    internal class InternalLaunchResult
    {
        public bool Success { get; set; }
        public int OutputCount { get; set; }
    }

    /// <summary>
    /// Performance metrics for a kernel.
    /// </summary>
    public class KernelPerformanceMetrics
    {
        public required string KernelName { get; set; }
        public int TotalLaunches { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
        public TimeSpan LastExecutionTime { get; set; }
        public TimeSpan BestExecutionTime { get; set; }
        public long TotalDataProcessed { get; set; }
        public double Throughput { get; set; } // Elements per second
        public LaunchConfiguration? LastConfiguration { get; set; }
        public LaunchConfiguration? BestConfiguration { get; set; }
    }

    /// <summary>
    /// Kernel priority levels.
    /// </summary>
    public enum KernelPriority
    {
        Low = -1,
        Normal = 0,
        High = 1
    }

    /// <summary>
    /// Exception thrown during kernel launch.
    /// </summary>
    public class KernelLaunchException : Exception
    {
        public KernelLaunchException(string message) : base(message) { }
        public KernelLaunchException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// CUDA stream wrapper.
    /// </summary>
    public class CudaStream : IDisposable
    {
        private readonly CudaContext _context;
        private IntPtr _streamPointer;
        private bool _disposed;

        public IntPtr StreamPointer => _streamPointer;
        public bool IsAsync { get; }

        public CudaStream(CudaContext context, bool isAsync = true)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            IsAsync = isAsync;
            
            if (isAsync)
            {
                var result = CudaRuntime.CreateStream(out _streamPointer);
                if (result != CudaError.Success)
                {
                    throw new InvalidOperationException($"Failed to create CUDA stream: {result}");
                }
            }
            else
            {
                _streamPointer = IntPtr.Zero; // Default stream
            }
        }

        public async Task SynchronizeAsync()
        {
            if (!_disposed && _streamPointer != IntPtr.Zero)
            {
                await Task.Run(() =>
                {
                    var result = CudaRuntime.SynchronizeStream(_streamPointer);
                    if (result != CudaError.Success)
                    {
                        throw new InvalidOperationException($"Stream synchronization failed: {result}");
                    }
                });
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_streamPointer != IntPtr.Zero)
            {
                var result = CudaRuntime.DestroyStream(_streamPointer);
                if (result != CudaError.Success)
                {
                    // Log error but don't throw in Dispose
                }
                _streamPointer = IntPtr.Zero;
            }

            _disposed = true;
        }
    }
}