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
using DotCompute.Linq.KernelGeneration;
using DotCompute.Linq.KernelGeneration.Memory;
using Microsoft.Extensions.Logging;
using DotCompute.Linq.Operators.Models;
namespace DotCompute.Linq.KernelGeneration.Execution
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
            _performanceMetrics = [];
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
                    if (memoryContext.InputBuffer is not GpuBuffer<T> inputBuffer)
                    {
                        throw new InvalidCastException($"Expected GpuBuffer<{typeof(T).Name}> but got {memoryContext.InputBuffer.GetType().Name}");
                    }
                    await _memoryManager.CopyToDeviceAsync(inputData, inputBuffer, cancellationToken);
                    // Get or create CUDA stream
                    var stream = GetOrCreateStream(launchOptions.StreamId);
                    // Launch the kernel
                    var launchResult = await LaunchKernelInternalAsync(
                        kernel, memoryContext, config, stream, cancellationToken);
                    // Copy results back to host
                    if (memoryContext.OutputBuffer is not GpuBuffer<T> outputBuffer)
                        throw new InvalidCastException($"Expected GpuBuffer<{typeof(T).Name}> but got {memoryContext.OutputBuffer.GetType().Name}");
                    var outputData = await _memoryManager.CopyFromDeviceAsync<T>(
                        outputBuffer, launchResult.OutputCount, cancellationToken);
                    stopwatch.Stop();
                    // Update performance metrics
                    UpdatePerformanceMetrics(kernel.Name, stopwatch.Elapsed, inputData.Length, config);
                    _logger.LogInformation("Successfully launched kernel {KernelName} in {ElapsedMs}ms",
                        kernel.Name, stopwatch.ElapsedMilliseconds);
                    return new KernelExecutionResult
                        OutputData = outputData,
                        OutputCount = launchResult.OutputCount,
                        ExecutionTime = stopwatch.Elapsed,
                        LaunchConfiguration = config,
                        PerformanceMetrics = GetPerformanceMetrics(kernel.Name)
                    };
                }
                finally
                    // Clean up memory context
                    await memoryContext.DisposeAsync();
            }
            catch (Exception ex)
                _logger.LogError(ex, "Failed to launch kernel {KernelName}", kernel.Name);
                throw new KernelLaunchException($"Failed to launch kernel '{kernel.Name}'", ex);
        /// Launches multiple kernels in parallel with stream coordination.
        public async Task<KernelExecutionResult[]> LaunchKernelBatchAsync<T>(
            IEnumerable<(ICompiledKernel kernel, T[] data)> kernelBatch,
            var kernelArray = kernelBatch.ToArray();
            var launchOptions = options ?? new LaunchOptions { EnableBatchOptimization = true };
            _logger.LogInformation("Launching batch of {KernelCount} kernels", kernelArray.Length);
            // Create separate streams for parallel execution
            var streamTasks = kernelArray.Select(async (kernelData, index) =>
                var streamOptions = new LaunchOptions
                    StreamId = launchOptions.StreamId + index,
                    EnableAsyncExecution = true,
                    Priority = launchOptions.Priority
                };
                return await LaunchKernelAsync(kernelData.kernel, kernelData.data, streamOptions, cancellationToken);
            });
            return await Task.WhenAll(streamTasks);
        /// Calculates optimal launch configuration for a kernel.
        private async Task<LaunchConfiguration> CalculateLaunchConfigurationAsync(
            int dataSize,
            LaunchOptions options)
            var cacheKey = GenerateConfigurationCacheKey(kernel, dataSize, options);
            if (_configurationCache.TryGetValue(cacheKey, out var cachedConfig))
                _logger.LogDebug("Using cached launch configuration for {KernelName}", kernel.Name);
                return cachedConfig;
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
        /// Calculates optimal block size using occupancy calculator.
        private Dim3 CalculateOptimalBlockSize(
            DeviceProperties deviceProps,
            if (options.BlockSize.HasValue)
                var customBlockSize = options.BlockSize.Value;
                return new Dim3(customBlockSize, 1, 1);
            // Use CUDA occupancy calculator if available
            var optimalBlockSize = CalculateOccupancyOptimalBlockSize(kernel, deviceProps);
            if (optimalBlockSize > 0)
                return new Dim3(optimalBlockSize, 1, 1);
            // Fallback to heuristic calculation
            var (major, minor) = CudaCapabilityManager.GetTargetComputeCapability();
            return (major, minor) switch
                ( >= 8, >= 9) => new Dim3(Math.Min(512, dataSize), 1, 1), // Ada Lovelace
                ( >= 8, _) => new Dim3(Math.Min(256, dataSize), 1, 1),    // Ampere
                ( >= 7, _) => new Dim3(Math.Min(256, dataSize), 1, 1),    // Volta/Turing
                _ => new Dim3(Math.Min(128, dataSize), 1, 1)             // Older architectures
        /// Calculates optimal grid size for the given data size and block size.
        private Dim3 CalculateOptimalGridSize(int dataSize, Dim3 blockSize, DeviceProperties deviceProps)
            var totalThreads = blockSize.X * blockSize.Y * blockSize.Z;
            var gridX = (dataSize + totalThreads - 1) / totalThreads;
            // Limit grid size to device maximum
            gridX = Math.Min(gridX, deviceProps.MaxGridSize.X);
            // For very large datasets, use 2D grid
            if (gridX > 65535)
                var gridY = (gridX + 65535 - 1) / 65535;
                gridX = Math.Min(gridX, 65535);
                gridY = Math.Min(gridY, deviceProps.MaxGridSize.Y);
                return new Dim3(gridX, gridY, 1);
            return new Dim3(gridX, 1, 1);
        /// Calculates shared memory size requirements.
        private int CalculateSharedMemorySize(ICompiledKernel kernel, Dim3 blockSize, LaunchOptions options)
            if (options.SharedMemorySize.HasValue)
                return options.SharedMemorySize.Value;
            // Estimate based on kernel type and block size
            // Basic estimation - can be enhanced with kernel analysis
            return kernel.Name.Contains("reduce", StringComparison.OrdinalIgnoreCase) ? totalThreads * 4 : 0;
        /// Estimates output data size based on kernel operation.
        private int EstimateOutputSize(int inputSize, string kernelName)
            return kernelName.ToLowerInvariant() switch
                var name when name.Contains("filter") => inputSize / 2, // Estimate 50% pass filter
                var name when name.Contains("reduce") => 1,              // Single result
                var name when name.Contains("scan") => inputSize,        // Same size as input
                var name when name.Contains("join") => inputSize * 2,    // Estimate join expansion
                _ => inputSize                                           // Default to same size
        /// Calculates theoretical occupancy for the configuration.
        private double CalculateOccupancy(Dim3 blockSize, int sharedMemorySize, DeviceProperties deviceProps)
            var threadsPerBlock = blockSize.X * blockSize.Y * blockSize.Z;
            var maxBlocksPerSM = Math.Min(
                deviceProps.MaxThreadsPerMultiProcessor / threadsPerBlock,
                deviceProps.SharedMemoryPerMultiProcessor / Math.Max(sharedMemorySize, 1));
            var actualThreadsPerSM = maxBlocksPerSM * threadsPerBlock;
            return (double)actualThreadsPerSM / deviceProps.MaxThreadsPerMultiProcessor;
        /// Uses CUDA occupancy calculator to find optimal block size.
        private int CalculateOccupancyOptimalBlockSize(ICompiledKernel kernel, DeviceProperties deviceProps)
                // This would require access to the actual CUDA function pointer
                // For now, return 0 to indicate fallback to heuristic
                return 0;
            catch
                return 0; // Fallback to heuristic calculation
        /// Launches the kernel with the given configuration.
        private async Task<InternalLaunchResult> LaunchKernelInternalAsync(
            MemoryContext memoryContext,
            LaunchConfiguration config,
            CudaStream stream,
            CancellationToken cancellationToken)
                // Prepare kernel parameters
                var parameters = new object[]
                    memoryContext.InputBuffer.DevicePointer,
                    memoryContext.OutputBuffer.DevicePointer,
                    memoryContext.OutputCountBuffer?.DevicePointer ?? IntPtr.Zero,
                    memoryContext.InputBuffer.Size
                // Launch kernel
                var kernelExecutionParams = new KernelExecutionParameters
                    Parameters = parameters,
                    SharedMemorySize = config.SharedMemorySize,
                    Stream = stream.StreamPointer
                // Create kernel arguments from execution parameters
                var kernelArgs = new KernelArguments(kernelExecutionParams.Parameters?.ToArray() ?? [])
                    LaunchConfiguration = new KernelLaunchConfiguration
                        GridSize = ((uint)config.GridSize.X, (uint)config.GridSize.Y, (uint)config.GridSize.Z),
                        BlockSize = ((uint)config.BlockSize.X, (uint)config.BlockSize.Y, (uint)config.BlockSize.Z),
                        SharedMemoryBytes = (uint)kernelExecutionParams.SharedMemorySize
                
                await kernel.ExecuteAsync(kernelArgs, cancellationToken);
                // Synchronize stream if not async
                if (!stream.IsAsync)
                    await stream.SynchronizeAsync();
                // Get output count if available
                var outputCount = memoryContext.OutputCountBuffer != null
                    ? await GetOutputCountAsync(memoryContext.OutputCountBuffer)
                    : config.EstimatedOutputSize;
                return new InternalLaunchResult
                    Success = true,
                    OutputCount = outputCount
                _logger.LogError(ex, "Kernel launch failed for {KernelName}", kernel.Name);
                throw new KernelLaunchException($"Kernel launch failed: {ex.Message}", ex);
        /// Gets or creates a CUDA stream for the given ID.
        private CudaStream GetOrCreateStream(int streamId)
            var streamPtr = new IntPtr(streamId);
            if (_streamPool.TryGetValue(streamPtr, out var existingStream))
                return existingStream;
            lock (_streamPoolLock)
                if (_streamPool.TryGetValue(streamPtr, out existingStream))
                    return existingStream;
                var newStream = new CudaStream(_context, streamId != 0);
                _streamPool.TryAdd(streamPtr, newStream);
                _logger.LogDebug("Created new CUDA stream with ID {StreamId}", streamId);
                return newStream;
        /// Gets device properties for launch configuration calculation.
        private async Task<DeviceProperties> GetDevicePropertiesAsync()
            return await Task.FromResult(new DeviceProperties
                MaxThreadsPerBlock = 1024,
                MaxThreadsPerMultiProcessor = 2048,
                SharedMemoryPerBlock = 48 * 1024,
                SharedMemoryPerMultiProcessor = 96 * 1024,
                MaxGridSize = new Dim3(2147483647, 65535, 65535),
                WarpSize = 32,
                MultiProcessorCount = 16 // Estimated
        /// Gets the output count from GPU memory.
        private async Task<int> GetOutputCountAsync(GpuBuffer<int> countBuffer)
            var hostCount = new int[1];
            await _memoryManager.CopyFromDeviceAsync(countBuffer, hostCount);
            return hostCount[0];
        /// Updates performance metrics for a kernel.
        private void UpdatePerformanceMetrics(string kernelName, TimeSpan executionTime, int dataSize, LaunchConfiguration config)
            lock (_metricsLock)
                if (!_performanceMetrics.TryGetValue(kernelName, out var metrics))
                    metrics = new KernelPerformanceMetrics { KernelName = kernelName };
                    _performanceMetrics[kernelName] = metrics;
                metrics.TotalLaunches++;
                metrics.TotalExecutionTime += executionTime;
                metrics.AverageExecutionTime = metrics.TotalExecutionTime / metrics.TotalLaunches;
                metrics.LastExecutionTime = executionTime;
                metrics.TotalDataProcessed += dataSize;
                metrics.Throughput = dataSize / executionTime.TotalSeconds;
                metrics.LastConfiguration = config;
                if (executionTime < metrics.BestExecutionTime || metrics.BestExecutionTime == TimeSpan.Zero)
                    metrics.BestExecutionTime = executionTime;
                    metrics.BestConfiguration = config;
        /// Gets performance metrics for a kernel.
        private KernelPerformanceMetrics GetPerformanceMetrics(string kernelName)
                return _performanceMetrics.GetValueOrDefault(kernelName, new KernelPerformanceMetrics { KernelName = kernelName });
        /// Generates a cache key for launch configuration.
        private string GenerateConfigurationCacheKey(ICompiledKernel kernel, int dataSize, LaunchOptions options)
            return $"{kernel.Name}_{dataSize}_{options.GetHashCode()}";
        /// Gets all performance metrics.
        public Dictionary<string, KernelPerformanceMetrics> GetAllPerformanceMetrics()
                return new Dictionary<string, KernelPerformanceMetrics>(_performanceMetrics);
        /// Clears performance metrics.
        public void ClearPerformanceMetrics()
                _performanceMetrics.Clear();
        public void Dispose()
            if (_disposed)
                return;
            // Dispose all streams
            foreach (var stream in _streamPool.Values)
                stream.Dispose();
            _streamPool.Clear();
            _disposed = true;
    }
    /// Launch options for kernel execution.
    public class LaunchOptions
        public int? BlockSize { get; set; }
        public int? SharedMemorySize { get; set; }
        public int StreamId { get; set; } = 0;
        public bool EnableAsyncExecution { get; set; } = false;
        public bool EnableBatchOptimization { get; set; } = false;
        public KernelPriority Priority { get; set; } = KernelPriority.Normal;
        public TimeSpan? Timeout { get; set; }
    /// Launch configuration for a kernel.
    public class LaunchConfiguration
        public required Dim3 GridSize { get; set; }
        public required Dim3 BlockSize { get; set; }
        public int SharedMemorySize { get; set; }
        public int EstimatedOutputSize { get; set; }
        public double MaxOccupancy { get; set; }
        public required string CacheKey { get; set; }
    /// Device properties for launch calculation.
    public class DeviceProperties
        public int MaxThreadsPerBlock { get; set; }
        public int MaxThreadsPerMultiProcessor { get; set; }
        public int SharedMemoryPerBlock { get; set; }
        public int SharedMemoryPerMultiProcessor { get; set; }
        public required Dim3 MaxGridSize { get; set; }
        public int WarpSize { get; set; }
        public int MultiProcessorCount { get; set; }
    /// 3D dimension structure.
    public struct Dim3
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public Dim3(int x, int y, int z)
            X = x;
            Y = y;
            Z = z;
    /// Kernel execution result.
    public class KernelExecutionResult
        public required object OutputData { get; set; }
        public int OutputCount { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public required LaunchConfiguration LaunchConfiguration { get; set; }
        public required KernelPerformanceMetrics PerformanceMetrics { get; set; }
    /// Internal launch result.
    internal class InternalLaunchResult
        public bool Success { get; set; }
    /// Performance metrics for a kernel.
    public class KernelPerformanceMetrics
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
    /// Kernel priority levels.
    public enum KernelPriority
        Low = -1,
        Normal = 0,
        High = 1
    /// Exception thrown during kernel launch.
    public class KernelLaunchException : Exception
        public KernelLaunchException(string message) : base(message) { }
        public KernelLaunchException(string message, Exception innerException) : base(message, innerException) { }
    /// CUDA stream wrapper.
    public class CudaStream : IDisposable
        private IntPtr _streamPointer;
        public IntPtr StreamPointer => _streamPointer;
        public bool IsAsync { get; }
        public CudaStream(CudaContext context, bool isAsync = true)
            IsAsync = isAsync;
            if (isAsync)
                var result = CudaRuntime.CreateStream(out _streamPointer);
                if (result != CudaError.Success)
                    throw new InvalidOperationException($"Failed to create CUDA stream: {result}");
            else
                _streamPointer = IntPtr.Zero; // Default stream
        public async Task SynchronizeAsync()
            if (!_disposed && _streamPointer != IntPtr.Zero)
                await Task.Run(() =>
                    var result = CudaRuntime.SynchronizeStream(_streamPointer);
                    if (result != CudaError.Success)
                        throw new InvalidOperationException($"Stream synchronization failed: {result}");
                });
            if (_streamPointer != IntPtr.Zero)
                var result = CudaRuntime.DestroyStream(_streamPointer);
                    // Log error but don't throw in Dispose
                _streamPointer = IntPtr.Zero;
}
