using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Execution.Models;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Optimization
{
    /// <summary>
    /// Production-grade CUDA occupancy calculator for optimal kernel launch configuration.
    /// Maximizes GPU utilization by calculating ideal block and grid dimensions.
    /// </summary>
    public sealed partial class CudaOccupancyCalculator
    {
        private readonly ILogger<CudaOccupancyCalculator> _logger;
        private readonly Dictionary<int, DeviceProperties> _devicePropertiesCache;

        // CUDA API imports

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaOccupancyMaxActiveBlocksPerMultiprocessor(
            out int numBlocks,
            IntPtr func,
            int blockSize,
            nuint dynamicSMemSize);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaOccupancyMaxPotentialBlockSize(
            out int minGridSize,
            out int blockSize,
            IntPtr func,
            nuint dynamicSMemSize,
            int blockSizeLimit);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaOccupancyMaxPotentialBlockSizeVariableSMem(
            out int minGridSize,
            out int blockSize,
            IntPtr func,
            BlockSizeToSMemFunc blockSizeToSMemSize,
            int blockSizeLimit);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaFuncGetAttributes(
            out CudaFuncAttributes attr,
            IntPtr func);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaDeviceGetAttribute(
            out int value,
            CudaDeviceAttribute attr,
            int device);

        // Delegate for variable shared memory calculation
        private delegate nuint BlockSizeToSMemFunc(int blockSize);
        /// <summary>
        /// Initializes a new instance of the CudaOccupancyCalculator class.
        /// </summary>
        /// <param name="logger">The logger.</param>

        public CudaOccupancyCalculator(ILogger<CudaOccupancyCalculator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _devicePropertiesCache = [];


            LogInitialized(_logger);
        }

        /// <summary>
        /// Calculates optimal launch configuration for a kernel.
        /// </summary>
        public async Task<LaunchConfiguration> CalculateOptimalLaunchConfigAsync(
            IntPtr kernelFunc,
            int deviceId,
            nuint dynamicSharedMemory = 0,
            LaunchConstraints? constraints = null)
        {
            constraints ??= LaunchConstraints.Default;


            LogCalculatingConfig(_logger, deviceId);

            // Get device properties
            var deviceProps = await GetDevicePropertiesAsync(deviceId);

            // Get kernel attributes

            var kernelAttrs = await GetKernelAttributesAsync(kernelFunc);

            // Calculate optimal block size

            var error = cudaOccupancyMaxPotentialBlockSize(
                out _,
                out var blockSize,
                kernelFunc,
                dynamicSharedMemory,
                constraints.MaxBlockSize ?? deviceProps.MaxThreadsPerBlock);


            if (error != CudaError.Success)
            {
                throw new OccupancyException($"Failed to calculate optimal block size: {error}");
            }

            // Apply constraints
            blockSize = ApplyBlockSizeConstraints(blockSize, constraints, deviceProps);

            // Calculate grid size based on problem size

            var gridSize = CalculateGridSize(
                constraints.ProblemSize ?? deviceProps.MaxGridSize,
                blockSize,
                deviceProps);

            // Calculate theoretical occupancy
            var occupancy = await CalculateOccupancyAsync(
                kernelFunc,
                blockSize,
                dynamicSharedMemory,
                kernelAttrs,
                deviceProps);

            // Create launch configuration
            var config = new LaunchConfiguration
            {
                BlockSize = new Abstractions.Types.Dim3(blockSize, 1, 1),
                GridSize = new Abstractions.Types.Dim3(gridSize, 1, 1),
                SharedMemoryBytes = dynamicSharedMemory + (nuint)kernelAttrs.SharedSizeBytes,
                TheoreticalOccupancy = occupancy.Percentage,
                ActiveWarps = occupancy.ActiveWarps,
                ActiveBlocks = occupancy.ActiveBlocks,
                RegistersPerThread = kernelAttrs.NumRegs,
                DeviceId = deviceId
            };

            // Optimize for specific patterns if requested
            if (constraints.OptimizationHint != OptimizationHint.None)
            {
                config = await OptimizeForPatternAsync(config, constraints.OptimizationHint, deviceProps);
            }

            LogOptimalConfig(_logger, config.GridSize, config.BlockSize, config.TheoreticalOccupancy);

            return config;
        }

        /// <summary>
        /// Calculates occupancy for multiple block sizes to find optimal configuration.
        /// </summary>
        public async Task<OccupancyCurve> CalculateOccupancyCurveAsync(
            IntPtr kernelFunc,
            int deviceId,
            nuint dynamicSharedMemory = 0,
            int minBlockSize = 32,
            int maxBlockSize = 1024)
        {
            var deviceProps = await GetDevicePropertiesAsync(deviceId);
            var kernelAttrs = await GetKernelAttributesAsync(kernelFunc);


            var curve = new OccupancyCurve
            {
                KernelName = kernelAttrs.KernelName,
                DeviceId = deviceId
            };

            // Test different block sizes
            for (var blockSize = minBlockSize; blockSize <= maxBlockSize; blockSize += 32)
            {
                if (blockSize > deviceProps.MaxThreadsPerBlock)
                {
                    break;
                }


                try
                {
                    var occupancy = await CalculateOccupancyAsync(
                        kernelFunc,
                        blockSize,
                        dynamicSharedMemory,
                        kernelAttrs,
                        deviceProps);

                    curve.DataPoints.Add(new OccupancyDataPoint
                    {
                        BlockSize = blockSize,
                        Occupancy = occupancy.Percentage,
                        ActiveWarps = occupancy.ActiveWarps,
                        ActiveBlocks = occupancy.ActiveBlocks,
                        LimitingFactor = occupancy.LimitingFactor
                    });
                }
                catch (Exception ex)
                {
                    LogOccupancyCalculationFailed(_logger, ex, blockSize);
                }
            }

            // Find optimal points
            if (curve.DataPoints.Count > 0)
            {
                curve.OptimalBlockSize = curve.DataPoints
                    .OrderByDescending(p => p.Occupancy)
                    .ThenBy(p => p.BlockSize)
                    .First().BlockSize;


                curve.MaxOccupancy = curve.DataPoints.Max(p => p.Occupancy);
            }

            return curve;
        }

        /// <summary>
        /// Analyzes register pressure and suggests optimizations.
        /// </summary>
        public async Task<RegisterAnalysis> AnalyzeRegisterPressureAsync(
            IntPtr kernelFunc,
            int deviceId,
            int blockSize)
        {
            var deviceProps = await GetDevicePropertiesAsync(deviceId);
            var kernelAttrs = await GetKernelAttributesAsync(kernelFunc);


            var analysis = new RegisterAnalysis
            {
                RegistersPerThread = kernelAttrs.NumRegs,
                MaxRegistersPerBlock = deviceProps.RegistersPerBlock,
                MaxRegistersPerSM = deviceProps.RegistersPerMultiprocessor
            };

            // Calculate register usage
            var registersPerBlock = kernelAttrs.NumRegs * blockSize;
            analysis.RegistersUsedPerBlock = registersPerBlock;

            // Calculate how many blocks can run per SM based on registers

            var blocksPerSmRegisters = deviceProps.RegistersPerMultiprocessor / registersPerBlock;
            analysis.MaxBlocksPerSmRegisters = blocksPerSmRegisters;

            // Calculate warp occupancy

            var warpsPerBlock = (blockSize + deviceProps.WarpSize - 1) / deviceProps.WarpSize;
            var activeWarps = Math.Min(
                blocksPerSmRegisters * warpsPerBlock,
                deviceProps.MaxWarpsPerMultiprocessor);


            analysis.WarpOccupancy = (double)activeWarps / deviceProps.MaxWarpsPerMultiprocessor;

            // Determine if register pressure is limiting factor

            analysis.IsRegisterLimited = blocksPerSmRegisters < deviceProps.MaxBlocksPerMultiprocessor;

            // Suggest optimizations

            if (analysis.IsRegisterLimited)
            {
                analysis.Suggestions.Add($"Kernel is register limited ({kernelAttrs.NumRegs} regs/thread)");

                // Calculate ideal register count

                var idealRegisters = deviceProps.RegistersPerMultiprocessor /

                    (deviceProps.MaxBlocksPerMultiprocessor * blockSize);


                if (idealRegisters < kernelAttrs.NumRegs)
                {
                    analysis.Suggestions.Add(
                        $"Reduce register usage to {idealRegisters} for full occupancy");
                }

                // Suggest compiler flags

                analysis.Suggestions.Add("Consider using --maxrregcount compiler flag");
                analysis.Suggestions.Add("Use __launch_bounds__ to limit register usage");
            }

            return analysis;
        }

        /// <summary>
        /// Calculates optimal configuration for 2D/3D kernels.
        /// </summary>
        public async Task<LaunchConfiguration> Calculate2DOptimalConfigAsync(
            IntPtr kernelFunc,
            int deviceId,
            Dim2 problemSize,
            nuint dynamicSharedMemory = 0)
        {
            var deviceProps = await GetDevicePropertiesAsync(deviceId);

            // Common 2D block sizes for different architectures

            var blockSizesToTest = new[]
            {
                new Dim2(32, 32),  // Good for memory coalescing
                new Dim2(16, 16),  // Lower register pressure
                new Dim2(32, 16),  // Mixed approach
                new Dim2(64, 4),   // Good for row-major access
                new Dim2(8, 8),    // Minimal configuration
            };

            LaunchConfiguration? bestConfig = null;
            double bestOccupancy = 0;

            foreach (var blockDim in blockSizesToTest)
            {
                var blockSize = blockDim.X * blockDim.Y;


                if (blockSize > deviceProps.MaxThreadsPerBlock)
                {
                    continue;
                }


                try
                {
                    // Calculate occupancy for this configuration
                    var kernelAttrs = await GetKernelAttributesAsync(kernelFunc);
                    var occupancy = await CalculateOccupancyAsync(
                        kernelFunc,
                        blockSize,
                        dynamicSharedMemory,
                        kernelAttrs,
                        deviceProps);

                    if (occupancy.Percentage > bestOccupancy)
                    {
                        bestOccupancy = occupancy.Percentage;

                        // Calculate grid dimensions

                        var gridX = (problemSize.X + blockDim.X - 1) / blockDim.X;
                        var gridY = (problemSize.Y + blockDim.Y - 1) / blockDim.Y;


                        bestConfig = new LaunchConfiguration
                        {
                            BlockSize = new Abstractions.Types.Dim3(blockDim.X, blockDim.Y, 1),
                            GridSize = new Abstractions.Types.Dim3(gridX, gridY, 1),
                            SharedMemoryBytes = dynamicSharedMemory + (nuint)kernelAttrs.SharedSizeBytes,
                            TheoreticalOccupancy = occupancy.Percentage,
                            ActiveWarps = occupancy.ActiveWarps,
                            ActiveBlocks = occupancy.ActiveBlocks,
                            RegistersPerThread = kernelAttrs.NumRegs,
                            DeviceId = deviceId
                        };
                    }
                }
                catch (Exception ex)
                {
                    Log2DConfigTestFailed(_logger, ex, blockDim);
                }
            }

            if (bestConfig == null)
            {
                throw new OccupancyException("Failed to find valid 2D launch configuration");
            }

            LogOptimal2DConfig(_logger, bestConfig.GridSize.X, bestConfig.GridSize.Y, bestConfig.BlockSize.X, bestConfig.BlockSize.Y, bestConfig.TheoreticalOccupancy);

            return bestConfig;
        }

        /// <summary>
        /// Calculates configuration for dynamic parallelism kernels.
        /// </summary>
        public async Task<DynamicParallelismConfig> CalculateDynamicParallelismConfigAsync(
            IntPtr parentKernel,
            IntPtr childKernel,
            int deviceId,
            int maxNestingDepth = 2)
        {
            var deviceProps = await GetDevicePropertiesAsync(deviceId);

            // Check if device supports dynamic parallelism

            if (deviceProps.ComputeCapability < 35)
            {
                throw new OccupancyException(
                    $"Device {deviceId} does not support dynamic parallelism (CC {deviceProps.ComputeCapability / 10.0:F1})");
            }

            var config = new DynamicParallelismConfig
            {
                MaxNestingDepth = Math.Min(maxNestingDepth, deviceProps.MaxDeviceDepth)
            };

            // Calculate parent kernel configuration
            config.ParentConfig = await CalculateOptimalLaunchConfigAsync(
                parentKernel, deviceId);

            // Calculate child kernel configuration with reduced resources
            // Child kernels share resources with parent
            var childConstraints = new LaunchConstraints
            {
                MaxBlockSize = Math.Min(256, deviceProps.MaxThreadsPerBlock / 2),
                ProblemSize = deviceProps.MaxGridSize / 2
            };


            config.ChildConfig = await CalculateOptimalLaunchConfigAsync(
                childKernel, deviceId, 0, childConstraints);

            // Calculate resource budget
            config.TotalWarpsAvailable = deviceProps.MaxWarpsPerMultiprocessor *

                                         deviceProps.MultiprocessorCount;


            config.ParentWarpsNeeded = config.ParentConfig.ActiveWarps;
            config.ChildWarpsPerParent = config.ChildConfig.ActiveWarps;
            config.MaxChildrenPerParent = (config.TotalWarpsAvailable - config.ParentWarpsNeeded) /

                                          config.ChildWarpsPerParent;

            LogDynamicParallelismConfig(_logger, config.MaxNestingDepth, config.MaxChildrenPerParent);

            return config;
        }

        /// <summary>
        /// Gets device properties with caching.
        /// </summary>
        private async Task<DeviceProperties> GetDevicePropertiesAsync(int deviceId)
        {
            if (_devicePropertiesCache.TryGetValue(deviceId, out var cached))
            {
                return cached;
            }

            var props = new DeviceProperties { DeviceId = deviceId };

            // Get all relevant device attributes
            _ = cudaDeviceGetAttribute(out var value, CudaDeviceAttribute.MaxThreadsPerBlock, deviceId);
            props.MaxThreadsPerBlock = value;

            _ = cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxBlockDimX, deviceId);
            props.MaxBlockDimX = value;

            _ = cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxBlockDimY, deviceId);
            props.MaxBlockDimY = value;

            _ = cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxBlockDimZ, deviceId);
            props.MaxBlockDimZ = value;

            _ = cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxGridDimX, deviceId);
            props.MaxGridSize = value;

            _ = cudaDeviceGetAttribute(out value, CudaDeviceAttribute.WarpSize, deviceId);
            props.WarpSize = value;

            _ = cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxRegistersPerBlock, deviceId);
            props.RegistersPerBlock = value;

            _ = cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxRegistersPerMultiprocessor, deviceId);
            props.RegistersPerMultiprocessor = value;

            _ = cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxSharedMemoryPerBlock, deviceId);
            props.SharedMemoryPerBlock = (nuint)value;

            _ = cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxSharedMemoryPerMultiprocessor, deviceId);
            props.SharedMemoryPerMultiprocessor = (nuint)value;

            _ = cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MultiprocessorCount, deviceId);
            props.MultiprocessorCount = value;

            _ = cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxBlocksPerMultiprocessor, deviceId);
            props.MaxBlocksPerMultiprocessor = value;

            _ = cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxWarpsPerMultiprocessor, deviceId);
            props.MaxWarpsPerMultiprocessor = value;

            _ = cudaDeviceGetAttribute(out value, CudaDeviceAttribute.ComputeCapabilityMajor, deviceId);
            props.ComputeCapability = value * 10;

            _ = cudaDeviceGetAttribute(out value, CudaDeviceAttribute.ComputeCapabilityMinor, deviceId);
            props.ComputeCapability += value;

            _ = cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxDeviceDepth, deviceId);
            props.MaxDeviceDepth = value;

            _devicePropertiesCache[deviceId] = props;


            await Task.CompletedTask;
            return props;
        }

        /// <summary>
        /// Gets kernel function attributes.
        /// </summary>
        private static async Task<CudaFuncAttributes> GetKernelAttributesAsync(IntPtr kernelFunc)
        {
            var error = cudaFuncGetAttributes(out var attrs, kernelFunc);
            if (error != CudaError.Success)
            {
                throw new OccupancyException($"Failed to get kernel attributes: {error}");
            }

            await Task.CompletedTask;
            return attrs;
        }

        /// <summary>
        /// Calculates theoretical occupancy.
        /// </summary>
        private static async Task<OccupancyResult> CalculateOccupancyAsync(
            IntPtr kernelFunc,
            int blockSize,
            nuint dynamicSharedMemory,
            CudaFuncAttributes kernelAttrs,
            DeviceProperties deviceProps)
        {
            var result = new OccupancyResult();

            // Calculate blocks per SM based on different limits
            var warpsPerBlock = (blockSize + deviceProps.WarpSize - 1) / deviceProps.WarpSize;

            // Thread/warp limit

            var blocksPerSmWarps = deviceProps.MaxWarpsPerMultiprocessor / warpsPerBlock;

            // Register limit

            var registersPerBlock = kernelAttrs.NumRegs * blockSize;
            var blocksPerSmRegisters = registersPerBlock > 0

                ? deviceProps.RegistersPerMultiprocessor / registersPerBlock

                : int.MaxValue;

            // Shared memory limit

            var sharedMemPerBlock = dynamicSharedMemory + (nuint)kernelAttrs.SharedSizeBytes;
            var blocksPerSmSharedMem = sharedMemPerBlock > 0
                ? (int)(deviceProps.SharedMemoryPerMultiprocessor / sharedMemPerBlock)
                : int.MaxValue;

            // Block limit

            var blocksPerSmBlocks = deviceProps.MaxBlocksPerMultiprocessor;

            // Find limiting factor

            result.ActiveBlocks = Math.Min(
                Math.Min(blocksPerSmWarps, blocksPerSmRegisters),
                Math.Min(blocksPerSmSharedMem, blocksPerSmBlocks));

            // Determine limiting factor

            if (result.ActiveBlocks == blocksPerSmWarps)
            {
                result.LimitingFactor = "Warps";
            }

            else if (result.ActiveBlocks == blocksPerSmRegisters)
            {
                result.LimitingFactor = "Registers";
            }

            else if (result.ActiveBlocks == blocksPerSmSharedMem)
            {
                result.LimitingFactor = "Shared Memory";
            }
            else
            {
                result.LimitingFactor = "Blocks";
            }

            // Calculate active warps and occupancy

            result.ActiveWarps = result.ActiveBlocks * warpsPerBlock;
            result.Percentage = (double)result.ActiveWarps / deviceProps.MaxWarpsPerMultiprocessor;


            await Task.CompletedTask;
            return result;
        }

        /// <summary>
        /// Applies user constraints to block size.
        /// </summary>
        private static int ApplyBlockSizeConstraints(
            int calculatedBlockSize,
            LaunchConstraints constraints,
            DeviceProperties deviceProps)
        {
            var blockSize = calculatedBlockSize;

            // Apply max block size constraint
            if (constraints.MaxBlockSize.HasValue)
            {
                blockSize = Math.Min(blockSize, constraints.MaxBlockSize.Value);
            }

            // Apply min block size constraint
            if (constraints.MinBlockSize.HasValue)
            {
                blockSize = Math.Max(blockSize, constraints.MinBlockSize.Value);
            }

            // Ensure multiple of warp size for efficiency
            if (constraints.RequireWarpMultiple)
            {
                blockSize = ((blockSize + deviceProps.WarpSize - 1) / deviceProps.WarpSize) * deviceProps.WarpSize;
            }

            // Ensure power of 2 if requested
            if (constraints.RequirePowerOfTwo)
            {
                blockSize = NextPowerOfTwo(blockSize);
            }

            return Math.Min(blockSize, deviceProps.MaxThreadsPerBlock);
        }

        /// <summary>
        /// Calculates grid size based on problem size and block size.
        /// </summary>
        private static int CalculateGridSize(int problemSize, int blockSize, DeviceProperties deviceProps)
        {
            var gridSize = (problemSize + blockSize - 1) / blockSize;
            return Math.Min(gridSize, deviceProps.MaxGridSize);
        }

        /// <summary>
        /// Optimizes configuration for specific access patterns.
        /// </summary>
        private static async Task<LaunchConfiguration> OptimizeForPatternAsync(
            LaunchConfiguration baseConfig,
            OptimizationHint hint,
            DeviceProperties deviceProps)
        {
            var optimized = baseConfig;

            switch (hint)
            {
                case OptimizationHint.MemoryBound:
                    // Prefer configurations that maximize memory throughput
                    // Use block sizes that are multiples of 128 for coalescing
                    if (optimized.BlockSize.X % 128 != 0)
                    {
                        var newSize = ((optimized.BlockSize.X / 128) + 1) * 128;
                        if (newSize <= deviceProps.MaxThreadsPerBlock)
                        {
                            optimized.BlockSize = new Abstractions.Types.Dim3(newSize, 1, 1);
                            optimized.GridSize = new Abstractions.Types.Dim3(
                                (optimized.GridSize.X * baseConfig.BlockSize.X + newSize - 1) / newSize,
                                1, 1);
                        }
                    }
                    break;

                case OptimizationHint.ComputeBound:
                    // Maximize occupancy for compute-bound kernels
                    // Already optimized by default calculation
                    break;

                case OptimizationHint.Latency:
                    // Minimize kernel launch overhead
                    // Use larger blocks to reduce grid size
                    var largerBlock = Math.Min(
                        NextPowerOfTwo(optimized.BlockSize.X * 2),
                        deviceProps.MaxThreadsPerBlock);


                    if (largerBlock > optimized.BlockSize.X)
                    {
                        optimized.BlockSize = new Abstractions.Types.Dim3(largerBlock, 1, 1);
                        optimized.GridSize = new Abstractions.Types.Dim3(
                            (optimized.GridSize.X * baseConfig.BlockSize.X + largerBlock - 1) / largerBlock,
                            1, 1);
                    }
                    break;

                case OptimizationHint.Balanced:
                    // Balance between occupancy and other factors
                    // Use default configuration
                    break;
            }

            await Task.CompletedTask;
            return optimized;
        }

        /// <summary>
        /// Calculates next power of two.
        /// </summary>
        private static int NextPowerOfTwo(int value)
        {
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            value++;
            return value;
        }
        /// <summary>
        /// A class that represents launch constraints.
        /// </summary>


        public class LaunchConstraints
        {
            /// <summary>
            /// Gets or sets the min block size.
            /// </summary>
            /// <value>The min block size.</value>
            public int? MinBlockSize { get; set; }
            /// <summary>
            /// Gets or sets the max block size.
            /// </summary>
            /// <value>The max block size.</value>
            public int? MaxBlockSize { get; set; }
            /// <summary>
            /// Gets or sets the problem size.
            /// </summary>
            /// <value>The problem size.</value>
            public int? ProblemSize { get; set; }
            /// <summary>
            /// Gets or sets the require warp multiple.
            /// </summary>
            /// <value>The require warp multiple.</value>
            public bool RequireWarpMultiple { get; set; } = true;
            /// <summary>
            /// Gets or sets the require power of two.
            /// </summary>
            /// <value>The require power of two.</value>
            public bool RequirePowerOfTwo { get; set; }
            /// <summary>
            /// Gets or sets the optimization hint.
            /// </summary>
            /// <value>The optimization hint.</value>

            public OptimizationHint OptimizationHint { get; set; } = OptimizationHint.Balanced;
            /// <summary>
            /// Gets or sets the default.
            /// </summary>
            /// <value>The default.</value>


            public static LaunchConstraints Default => new();
        }
        /// <summary>
        /// A class that represents occupancy result.
        /// </summary>

        public class OccupancyResult
        {
            /// <summary>
            /// Gets or sets the percentage.
            /// </summary>
            /// <value>The percentage.</value>
            public double Percentage { get; set; }
            /// <summary>
            /// Gets or sets the active warps.
            /// </summary>
            /// <value>The active warps.</value>
            public int ActiveWarps { get; set; }
            /// <summary>
            /// Gets or sets the active blocks.
            /// </summary>
            /// <value>The active blocks.</value>
            public int ActiveBlocks { get; set; }
            /// <summary>
            /// Gets or sets the limiting factor.
            /// </summary>
            /// <value>The limiting factor.</value>
            public string LimitingFactor { get; set; } = "";
        }
        /// <summary>
        /// A class that represents occupancy curve.
        /// </summary>

        public class OccupancyCurve
        {
            /// <summary>
            /// Gets or sets the kernel name.
            /// </summary>
            /// <value>The kernel name.</value>
            public string KernelName { get; set; } = "";
            /// <summary>
            /// Gets or sets the device identifier.
            /// </summary>
            /// <value>The device id.</value>
            public int DeviceId { get; set; }
            /// <summary>
            /// Gets or sets the data points.
            /// </summary>
            /// <value>The data points.</value>
            public IList<OccupancyDataPoint> DataPoints { get; } = [];
            /// <summary>
            /// Gets or sets the optimal block size.
            /// </summary>
            /// <value>The optimal block size.</value>
            public int OptimalBlockSize { get; set; }
            /// <summary>
            /// Gets or sets the max occupancy.
            /// </summary>
            /// <value>The max occupancy.</value>
            public double MaxOccupancy { get; set; }
        }
        /// <summary>
        /// A class that represents occupancy data point.
        /// </summary>

        public class OccupancyDataPoint
        {
            /// <summary>
            /// Gets or sets the block size.
            /// </summary>
            /// <value>The block size.</value>
            public int BlockSize { get; set; }
            /// <summary>
            /// Gets or sets the occupancy.
            /// </summary>
            /// <value>The occupancy.</value>
            public double Occupancy { get; set; }
            /// <summary>
            /// Gets or sets the active warps.
            /// </summary>
            /// <value>The active warps.</value>
            public int ActiveWarps { get; set; }
            /// <summary>
            /// Gets or sets the active blocks.
            /// </summary>
            /// <value>The active blocks.</value>
            public int ActiveBlocks { get; set; }
            /// <summary>
            /// Gets or sets the limiting factor.
            /// </summary>
            /// <value>The limiting factor.</value>
            public string LimitingFactor { get; set; } = "";
        }
        /// <summary>
        /// A class that represents register analysis.
        /// </summary>

        public class RegisterAnalysis
        {
            /// <summary>
            /// Gets or sets the registers per thread.
            /// </summary>
            /// <value>The registers per thread.</value>
            public int RegistersPerThread { get; set; }
            /// <summary>
            /// Gets or sets the registers used per block.
            /// </summary>
            /// <value>The registers used per block.</value>
            public int RegistersUsedPerBlock { get; set; }
            /// <summary>
            /// Gets or sets the max registers per block.
            /// </summary>
            /// <value>The max registers per block.</value>
            public int MaxRegistersPerBlock { get; set; }
            /// <summary>
            /// Gets or sets the max registers per s m.
            /// </summary>
            /// <value>The max registers per s m.</value>
            public int MaxRegistersPerSM { get; set; }
            /// <summary>
            /// Gets or sets the max blocks per sm registers.
            /// </summary>
            /// <value>The max blocks per sm registers.</value>
            public int MaxBlocksPerSmRegisters { get; set; }
            /// <summary>
            /// Gets or sets the warp occupancy.
            /// </summary>
            /// <value>The warp occupancy.</value>
            public double WarpOccupancy { get; set; }
            /// <summary>
            /// Gets or sets a value indicating whether register limited.
            /// </summary>
            /// <value>The is register limited.</value>
            public bool IsRegisterLimited { get; set; }
            /// <summary>
            /// Gets or sets the suggestions.
            /// </summary>
            /// <value>The suggestions.</value>
            public IList<string> Suggestions { get; } = [];
        }


        private class DeviceProperties
        {
            /// <summary>
            /// Gets or sets the device identifier.
            /// </summary>
            /// <value>The device id.</value>
            public int DeviceId { get; set; }
            /// <summary>
            /// Gets or sets the max threads per block.
            /// </summary>
            /// <value>The max threads per block.</value>
            public int MaxThreadsPerBlock { get; set; }
            /// <summary>
            /// Gets or sets the max block dim x.
            /// </summary>
            /// <value>The max block dim x.</value>
            public int MaxBlockDimX { get; set; }
            /// <summary>
            /// Gets or sets the max block dim y.
            /// </summary>
            /// <value>The max block dim y.</value>
            public int MaxBlockDimY { get; set; }
            /// <summary>
            /// Gets or sets the max block dim z.
            /// </summary>
            /// <value>The max block dim z.</value>
            public int MaxBlockDimZ { get; set; }
            /// <summary>
            /// Gets or sets the max grid size.
            /// </summary>
            /// <value>The max grid size.</value>
            public int MaxGridSize { get; set; }
            /// <summary>
            /// Gets or sets the warp size.
            /// </summary>
            /// <value>The warp size.</value>
            public int WarpSize { get; set; }
            /// <summary>
            /// Gets or sets the registers per block.
            /// </summary>
            /// <value>The registers per block.</value>
            public int RegistersPerBlock { get; set; }
            /// <summary>
            /// Gets or sets the registers per multiprocessor.
            /// </summary>
            /// <value>The registers per multiprocessor.</value>
            public int RegistersPerMultiprocessor { get; set; }
            /// <summary>
            /// Gets or sets the shared memory per block.
            /// </summary>
            /// <value>The shared memory per block.</value>
            public nuint SharedMemoryPerBlock { get; set; }
            /// <summary>
            /// Gets or sets the shared memory per multiprocessor.
            /// </summary>
            /// <value>The shared memory per multiprocessor.</value>
            public nuint SharedMemoryPerMultiprocessor { get; set; }
            /// <summary>
            /// Gets or sets the multiprocessor count.
            /// </summary>
            /// <value>The multiprocessor count.</value>
            public int MultiprocessorCount { get; set; }
            /// <summary>
            /// Gets or sets the max blocks per multiprocessor.
            /// </summary>
            /// <value>The max blocks per multiprocessor.</value>
            public int MaxBlocksPerMultiprocessor { get; set; }
            /// <summary>
            /// Gets or sets the max warps per multiprocessor.
            /// </summary>
            /// <value>The max warps per multiprocessor.</value>
            public int MaxWarpsPerMultiprocessor { get; set; }
            /// <summary>
            /// Gets or sets the compute capability.
            /// </summary>
            /// <value>The compute capability.</value>
            public int ComputeCapability { get; set; }
            /// <summary>
            /// Gets or sets the max device depth.
            /// </summary>
            /// <value>The max device depth.</value>
            public int MaxDeviceDepth { get; set; }
        }
        /// <summary>
        /// A dim3 structure.
        /// </summary>

        public struct Dim3(int x, int y, int z)
        {
            /// <summary>
            /// Gets or sets the x.
            /// </summary>
            /// <value>The x.</value>
            public int X { get; } = x;
            /// <summary>
            /// Gets or sets the y.
            /// </summary>
            /// <value>The y.</value>
            public int Y { get; } = y;
            /// <summary>
            /// Gets or sets the z.
            /// </summary>
            /// <value>The z.</value>
            public int Z { get; } = z;
            /// <summary>
            /// Gets to string.
            /// </summary>
            /// <returns>The result of the operation.</returns>

            public override string ToString() => $"({X},{Y},{Z})";

            /// <summary>
            /// Determines whether the specified object is equal to the current Dim3.
            /// </summary>
            public override bool Equals(object? obj) => obj is Dim3 other && X == other.X && Y == other.Y && Z == other.Z;

            /// <summary>
            /// Returns the hash code for this Dim3.
            /// </summary>
            public override int GetHashCode() => HashCode.Combine(X, Y, Z);

            /// <summary>
            /// Determines whether two Dim3 instances are equal.
            /// </summary>
            public static bool operator ==(Dim3 left, Dim3 right) => left.Equals(right);

            /// <summary>
            /// Determines whether two Dim3 instances are not equal.
            /// </summary>
            public static bool operator !=(Dim3 left, Dim3 right) => !left.Equals(right);

        }
        /// <summary>
        /// A dim2 structure.
        /// </summary>

        public struct Dim2(int x, int y) : IEquatable<Dim2>
        {
            /// <summary>
            /// Gets or sets the x.
            /// </summary>
            /// <value>The x.</value>
            public int X { get; } = x;
            /// <summary>
            /// Gets or sets the y.
            /// </summary>
            /// <value>The y.</value>
            public int Y { get; } = y;
            /// <summary>
            /// Gets to string.
            /// </summary>
            /// <returns>The result of the operation.</returns>

            public override string ToString() => $"({X},{Y})";

            /// <summary>
            /// Indicates whether the current object is equal to another object of the same type.
            /// </summary>
            /// <param name="other">An object to compare with this object.</param>
            /// <returns>true if the current object is equal to the other parameter; otherwise, false.</returns>
            public bool Equals(Dim2 other) => X == other.X && Y == other.Y;

            /// <summary>
            /// Determines whether the specified object is equal to the current object.
            /// </summary>
            /// <param name="obj">The object to compare with the current object.</param>
            /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
            public override bool Equals(object? obj) => obj is Dim2 other && Equals(other);

            /// <summary>
            /// Returns the hash code for this instance.
            /// </summary>
            /// <returns>A 32-bit signed integer hash code.</returns>
            public override int GetHashCode() => HashCode.Combine(X, Y);

            /// <summary>
            /// Indicates whether two instances are equal.
            /// </summary>
            /// <param name="left">The first instance to compare.</param>
            /// <param name="right">The second instance to compare.</param>
            /// <returns>true if the instances are equal; otherwise, false.</returns>
            public static bool operator ==(Dim2 left, Dim2 right) => left.Equals(right);

            /// <summary>
            /// Indicates whether two instances are not equal.
            /// </summary>
            /// <param name="left">The first instance to compare.</param>
            /// <param name="right">The second instance to compare.</param>
            /// <returns>true if the instances are not equal; otherwise, false.</returns>
            public static bool operator !=(Dim2 left, Dim2 right) => !left.Equals(right);
        }
        /// <summary>
        /// An optimization hint enumeration.
        /// </summary>

        public enum OptimizationHint
        {
            None,
            Balanced,
            MemoryBound,
            ComputeBound,
            Latency
        }
        /// <summary>
        /// An cuda device attribute enumeration.
        /// </summary>


        private enum CudaDeviceAttribute
        {
            MaxThreadsPerBlock = 1,
            MaxBlockDimX = 2,
            MaxBlockDimY = 3,
            MaxBlockDimZ = 4,
            MaxGridDimX = 5,
            MaxGridDimY = 6,
            MaxGridDimZ = 7,
            MaxSharedMemoryPerBlock = 8,
            TotalConstantMemory = 9,
            WarpSize = 10,
            MaxPitch = 11,
            MaxRegistersPerBlock = 12,
            ClockRate = 13,
            TextureAlignment = 14,
            MultiprocessorCount = 16,
            MaxRegistersPerMultiprocessor = 82,
            MaxSharedMemoryPerMultiprocessor = 81,
            MaxBlocksPerMultiprocessor = 106,
            MaxWarpsPerMultiprocessor = 67,
            ComputeCapabilityMajor = 75,
            ComputeCapabilityMinor = 76,
            MaxDeviceDepth = 111
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CudaFuncAttributes
        {
            /// <summary>
            /// The shared size bytes.
            /// </summary>
            public nuint SharedSizeBytes;
            /// <summary>
            /// The const size bytes.
            /// </summary>
            public nuint ConstSizeBytes;
            /// <summary>
            /// The local size bytes.
            /// </summary>
            public nuint LocalSizeBytes;
            /// <summary>
            /// The max threads per block.
            /// </summary>
            public int MaxThreadsPerBlock;
            /// <summary>
            /// The num regs.
            /// </summary>
            public int NumRegs;
            /// <summary>
            /// The ptx version.
            /// </summary>
            public int PtxVersion;
            /// <summary>
            /// The binary version.
            /// </summary>
            public int BinaryVersion;
            /// <summary>
            /// The cache mode c a.
            /// </summary>
            public IntPtr CacheModeCA;
            /// <summary>
            /// The max dynamic shared size bytes.
            /// </summary>
            public int MaxDynamicSharedSizeBytes;
            /// <summary>
            /// The preferred shmem carveout.
            /// </summary>
            public int PreferredShmemCarveout;
            /// <summary>
            /// The name.
            /// </summary>
            public IntPtr Name;

            /// <summary>
            /// Gets the kernel name as a string from the IntPtr.
            /// </summary>
            public readonly string KernelName
            {
                get
                {
                    try
                    {
                        return Name != IntPtr.Zero ? Marshal.PtrToStringAnsi(Name) ?? "Unknown" : "Unknown";
                    }
                    catch
                    {
                        return "Unknown";
                    }
                }
            }
        }

        private class OccupancyException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the OccupancyException class.
            /// </summary>
            /// <param name="message">The message.</param>
            public OccupancyException(string message) : base(message) { }
            /// <summary>
            /// Initializes a new instance of the OccupancyException class.
            /// </summary>
            public OccupancyException()
            {
            }
            /// <summary>
            /// Initializes a new instance of the OccupancyException class.
            /// </summary>
            /// <param name="message">The message.</param>
            /// <param name="innerException">The inner exception.</param>
            public OccupancyException(string message, Exception innerException) : base(message, innerException)
            {
            }
        }
    }
}