// Import organized occupancy types
global using DotCompute.Backends.CUDA.Optimization.Types;

using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Execution.Models;
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
        [DllImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static extern CudaError cudaOccupancyMaxActiveBlocksPerMultiprocessor(
            out int numBlocks,
            IntPtr func,
            int blockSize,
            nuint dynamicSMemSize);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static extern CudaError cudaOccupancyMaxPotentialBlockSize(
            out int minGridSize,
            out int blockSize,
            IntPtr func,
            nuint dynamicSMemSize,
            int blockSizeLimit);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static extern CudaError cudaOccupancyMaxPotentialBlockSizeVariableSMem(
            out int minGridSize,
            out int blockSize,
            IntPtr func,
            BlockSizeToSMemFunc blockSizeToSMemSize,
            int blockSizeLimit);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static extern CudaError cudaFuncGetAttributes(
            out CudaFuncAttributes attr,
            IntPtr func);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
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
        internal async Task<LaunchConfiguration> CalculateOptimalLaunchConfigAsync(
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
        internal async Task<OccupancyCurve> CalculateOccupancyCurveAsync(
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
        internal async Task<RegisterAnalysis> AnalyzeRegisterPressureAsync(
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
        internal async Task<LaunchConfiguration> Calculate2DOptimalConfigAsync(
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
                MaxNestingDepth = Math.Min(maxNestingDepth, deviceProps.MaxDeviceDepth),
                // Calculate parent kernel configuration
                ParentConfig = await CalculateOptimalLaunchConfigAsync(
                    parentKernel, deviceId)
            };

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
    }
}
