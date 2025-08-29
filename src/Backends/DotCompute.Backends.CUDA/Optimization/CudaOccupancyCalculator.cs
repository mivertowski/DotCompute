using System;
using System.Collections.Generic;
using System.Linq;
using global::System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotCompute.Backends.CUDA.DeviceManagement;
using DotCompute.Backends.CUDA.Execution.Models;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Optimization
{
    /// <summary>
    /// Production-grade CUDA occupancy calculator for optimal kernel launch configuration.
    /// Maximizes GPU utilization by calculating ideal block and grid dimensions.
    /// </summary>
    public sealed class CudaOccupancyCalculator
    {
        private readonly ILogger<CudaOccupancyCalculator> _logger;
        private readonly Dictionary<int, DeviceProperties> _devicePropertiesCache;
        
        // CUDA API imports
        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaOccupancyMaxActiveBlocksPerMultiprocessor(
            out int numBlocks,
            IntPtr func,
            int blockSize,
            ulong dynamicSMemSize);

        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaOccupancyMaxPotentialBlockSize(
            out int minGridSize,
            out int blockSize,
            IntPtr func,
            ulong dynamicSMemSize,
            int blockSizeLimit);

        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaOccupancyMaxPotentialBlockSizeVariableSMem(
            out int minGridSize,
            out int blockSize,
            IntPtr func,
            BlockSizeToSMemFunc blockSizeToSMemSize,
            int blockSizeLimit);

        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaFuncGetAttributes(
            out CudaFuncAttributes attr,
            IntPtr func);

        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaDeviceGetAttribute(
            out int value,
            CudaDeviceAttribute attr,
            int device);

        // Delegate for variable shared memory calculation
        private delegate ulong BlockSizeToSMemFunc(int blockSize);

        public CudaOccupancyCalculator(ILogger<CudaOccupancyCalculator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _devicePropertiesCache = new Dictionary<int, DeviceProperties>();
            
            _logger.LogInformation("CUDA Occupancy Calculator initialized");
        }

        /// <summary>
        /// Calculates optimal launch configuration for a kernel.
        /// </summary>
        public async Task<LaunchConfiguration> CalculateOptimalLaunchConfigAsync(
            IntPtr kernelFunc,
            int deviceId,
            ulong dynamicSharedMemory = 0,
            LaunchConstraints? constraints = null)
        {
            constraints ??= LaunchConstraints.Default;
            
            _logger.LogDebug(
                "Calculating optimal launch config for kernel on device {DeviceId}",
                deviceId);

            // Get device properties
            var deviceProps = await GetDevicePropertiesAsync(deviceId);
            
            // Get kernel attributes
            var kernelAttrs = await GetKernelAttributesAsync(kernelFunc);
            
            // Calculate optimal block size
            var error = cudaOccupancyMaxPotentialBlockSize(
                out int minGridSize,
                out int blockSize,
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
            int gridSize = CalculateGridSize(
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
                BlockSize = new Dim3(blockSize, 1, 1),
                GridSize = new Dim3(gridSize, 1, 1),
                SharedMemoryBytes = dynamicSharedMemory + (ulong)kernelAttrs.SharedSizeBytes,
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

            _logger.LogInformation(
                "Optimal launch config: Grid={GridSize}, Block={BlockSize}, Occupancy={Occupancy:P}",
                config.GridSize,
                config.BlockSize,
                config.TheoreticalOccupancy);

            return config;
        }

        /// <summary>
        /// Calculates occupancy for multiple block sizes to find optimal configuration.
        /// </summary>
        public async Task<OccupancyCurve> CalculateOccupancyCurveAsync(
            IntPtr kernelFunc,
            int deviceId,
            ulong dynamicSharedMemory = 0,
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
            for (int blockSize = minBlockSize; blockSize <= maxBlockSize; blockSize += 32)
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
                    _logger.LogWarning(ex, 
                        "Failed to calculate occupancy for block size {BlockSize}", blockSize);
                }
            }

            // Find optimal points
            if (curve.DataPoints.Any())
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
            int registersPerBlock = kernelAttrs.NumRegs * blockSize;
            analysis.RegistersUsedPerBlock = registersPerBlock;
            
            // Calculate how many blocks can run per SM based on registers
            int blocksPerSmRegisters = deviceProps.RegistersPerMultiprocessor / registersPerBlock;
            analysis.MaxBlocksPerSmRegisters = blocksPerSmRegisters;
            
            // Calculate warp occupancy
            int warpsPerBlock = (blockSize + deviceProps.WarpSize - 1) / deviceProps.WarpSize;
            int activeWarps = Math.Min(
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
                int idealRegisters = deviceProps.RegistersPerMultiprocessor / 
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
            ulong dynamicSharedMemory = 0)
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
                int blockSize = blockDim.X * blockDim.Y;
                
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
                        int gridX = (problemSize.X + blockDim.X - 1) / blockDim.X;
                        int gridY = (problemSize.Y + blockDim.Y - 1) / blockDim.Y;
                        
                        bestConfig = new LaunchConfiguration
                        {
                            BlockSize = new Dim3(blockDim.X, blockDim.Y, 1),
                            GridSize = new Dim3(gridX, gridY, 1),
                            SharedMemoryBytes = dynamicSharedMemory + (ulong)kernelAttrs.SharedSizeBytes,
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
                    _logger.LogWarning(ex, 
                        "Failed to test 2D configuration {BlockDim}", blockDim);
                }
            }

            if (bestConfig == null)
            {
                throw new OccupancyException("Failed to find valid 2D launch configuration");
            }

            _logger.LogInformation(
                "Optimal 2D config: Grid=({GridX},{GridY}), Block=({BlockX},{BlockY}), Occupancy={Occupancy:P}",
                bestConfig.GridSize.X, bestConfig.GridSize.Y,
                bestConfig.BlockSize.X, bestConfig.BlockSize.Y,
                bestConfig.TheoreticalOccupancy);

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
                    $"Device {deviceId} does not support dynamic parallelism (CC {deviceProps.ComputeCapability/10.0:F1})");
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

            _logger.LogInformation(
                "Dynamic parallelism config: Max depth={Depth}, Max children/parent={MaxChildren}",
                config.MaxNestingDepth,
                config.MaxChildrenPerParent);

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
            cudaDeviceGetAttribute(out int value, CudaDeviceAttribute.MaxThreadsPerBlock, deviceId);
            props.MaxThreadsPerBlock = value;

            cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxBlockDimX, deviceId);
            props.MaxBlockDimX = value;

            cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxBlockDimY, deviceId);
            props.MaxBlockDimY = value;

            cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxBlockDimZ, deviceId);
            props.MaxBlockDimZ = value;

            cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxGridDimX, deviceId);
            props.MaxGridSize = value;

            cudaDeviceGetAttribute(out value, CudaDeviceAttribute.WarpSize, deviceId);
            props.WarpSize = value;

            cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxRegistersPerBlock, deviceId);
            props.RegistersPerBlock = value;

            cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxRegistersPerMultiprocessor, deviceId);
            props.RegistersPerMultiprocessor = value;

            cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxSharedMemoryPerBlock, deviceId);
            props.SharedMemoryPerBlock = value;

            cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxSharedMemoryPerMultiprocessor, deviceId);
            props.SharedMemoryPerMultiprocessor = value;

            cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MultiprocessorCount, deviceId);
            props.MultiprocessorCount = value;

            cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxBlocksPerMultiprocessor, deviceId);
            props.MaxBlocksPerMultiprocessor = value;

            cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxWarpsPerMultiprocessor, deviceId);
            props.MaxWarpsPerMultiprocessor = value;

            cudaDeviceGetAttribute(out value, CudaDeviceAttribute.ComputeCapabilityMajor, deviceId);
            props.ComputeCapability = value * 10;

            cudaDeviceGetAttribute(out value, CudaDeviceAttribute.ComputeCapabilityMinor, deviceId);
            props.ComputeCapability += value;

            cudaDeviceGetAttribute(out value, CudaDeviceAttribute.MaxDeviceDepth, deviceId);
            props.MaxDeviceDepth = value;

            _devicePropertiesCache[deviceId] = props;
            
            await Task.CompletedTask;
            return props;
        }

        /// <summary>
        /// Gets kernel function attributes.
        /// </summary>
        private async Task<CudaFuncAttributes> GetKernelAttributesAsync(IntPtr kernelFunc)
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
        private async Task<OccupancyResult> CalculateOccupancyAsync(
            IntPtr kernelFunc,
            int blockSize,
            ulong dynamicSharedMemory,
            CudaFuncAttributes kernelAttrs,
            DeviceProperties deviceProps)
        {
            var result = new OccupancyResult();

            // Calculate blocks per SM based on different limits
            int warpsPerBlock = (blockSize + deviceProps.WarpSize - 1) / deviceProps.WarpSize;
            
            // Thread/warp limit
            int blocksPerSmWarps = deviceProps.MaxWarpsPerMultiprocessor / warpsPerBlock;
            
            // Register limit
            int registersPerBlock = kernelAttrs.NumRegs * blockSize;
            int blocksPerSmRegisters = registersPerBlock > 0 
                ? deviceProps.RegistersPerMultiprocessor / registersPerBlock 
                : int.MaxValue;
            
            // Shared memory limit
            ulong sharedMemPerBlock = dynamicSharedMemory + (ulong)kernelAttrs.SharedSizeBytes;
            int blocksPerSmSharedMem = sharedMemPerBlock > 0
                ? (int)(deviceProps.SharedMemoryPerMultiprocessor / sharedMemPerBlock)
                : int.MaxValue;
            
            // Block limit
            int blocksPerSmBlocks = deviceProps.MaxBlocksPerMultiprocessor;
            
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
        private int ApplyBlockSizeConstraints(
            int calculatedBlockSize,
            LaunchConstraints constraints,
            DeviceProperties deviceProps)
        {
            int blockSize = calculatedBlockSize;

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
        private int CalculateGridSize(int problemSize, int blockSize, DeviceProperties deviceProps)
        {
            int gridSize = (problemSize + blockSize - 1) / blockSize;
            return Math.Min(gridSize, deviceProps.MaxGridSize);
        }

        /// <summary>
        /// Optimizes configuration for specific access patterns.
        /// </summary>
        private async Task<LaunchConfiguration> OptimizeForPatternAsync(
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
                        int newSize = ((optimized.BlockSize.X / 128) + 1) * 128;
                        if (newSize <= deviceProps.MaxThreadsPerBlock)
                        {
                            optimized.BlockSize = new Dim3(newSize, 1, 1);
                            optimized.GridSize = new Dim3(
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
                    int largerBlock = Math.Min(
                        NextPowerOfTwo(optimized.BlockSize.X * 2),
                        deviceProps.MaxThreadsPerBlock);
                    
                    if (largerBlock > optimized.BlockSize.X)
                    {
                        optimized.BlockSize = new Dim3(largerBlock, 1, 1);
                        optimized.GridSize = new Dim3(
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
        private int NextPowerOfTwo(int value)
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

        // Supporting classes
        public class LaunchConfiguration
        {
            public Dim3 BlockSize { get; set; }
            public Dim3 GridSize { get; set; }
            public ulong SharedMemoryBytes { get; set; }
            public double TheoreticalOccupancy { get; set; }
            public int ActiveWarps { get; set; }
            public int ActiveBlocks { get; set; }
            public int RegistersPerThread { get; set; }
            public int DeviceId { get; set; }
        }

        public class LaunchConstraints
        {
            public int? MinBlockSize { get; set; }
            public int? MaxBlockSize { get; set; }
            public int? ProblemSize { get; set; }
            public bool RequireWarpMultiple { get; set; } = true;
            public bool RequirePowerOfTwo { get; set; }

            public OptimizationHint OptimizationHint { get; set; } = OptimizationHint.Balanced;
            
            public static LaunchConstraints Default => new();
        }

        public class OccupancyResult
        {
            public double Percentage { get; set; }
            public int ActiveWarps { get; set; }
            public int ActiveBlocks { get; set; }
            public string LimitingFactor { get; set; } = "";
        }

        public class OccupancyCurve
        {
            public string KernelName { get; set; } = "";
            public int DeviceId { get; set; }
            public List<OccupancyDataPoint> DataPoints { get; } = new();
            public int OptimalBlockSize { get; set; }
            public double MaxOccupancy { get; set; }
        }

        public class OccupancyDataPoint
        {
            public int BlockSize { get; set; }
            public double Occupancy { get; set; }
            public int ActiveWarps { get; set; }
            public int ActiveBlocks { get; set; }
            public string LimitingFactor { get; set; } = "";
        }

        public class RegisterAnalysis
        {
            public int RegistersPerThread { get; set; }
            public int RegistersUsedPerBlock { get; set; }
            public int MaxRegistersPerBlock { get; set; }
            public int MaxRegistersPerSM { get; set; }
            public int MaxBlocksPerSmRegisters { get; set; }
            public double WarpOccupancy { get; set; }
            public bool IsRegisterLimited { get; set; }
            public List<string> Suggestions { get; } = new();
        }


        private class DeviceProperties
        {
            public int DeviceId { get; set; }
            public int MaxThreadsPerBlock { get; set; }
            public int MaxBlockDimX { get; set; }
            public int MaxBlockDimY { get; set; }
            public int MaxBlockDimZ { get; set; }
            public int MaxGridSize { get; set; }
            public int WarpSize { get; set; }
            public int RegistersPerBlock { get; set; }
            public int RegistersPerMultiprocessor { get; set; }
            public int SharedMemoryPerBlock { get; set; }
            public int SharedMemoryPerMultiprocessor { get; set; }
            public int MultiprocessorCount { get; set; }
            public int MaxBlocksPerMultiprocessor { get; set; }
            public int MaxWarpsPerMultiprocessor { get; set; }
            public int ComputeCapability { get; set; }
            public int MaxDeviceDepth { get; set; }
        }

        public struct Dim3
        {
            public int X { get; }
            public int Y { get; }
            public int Z { get; }

            public Dim3(int x, int y, int z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public override string ToString() => $"({X},{Y},{Z})";
        }

        public struct Dim2
        {
            public int X { get; }
            public int Y { get; }

            public Dim2(int x, int y)
            {
                X = x;
                Y = y;
            }

            public override string ToString() => $"({X},{Y})";
        }

        public enum OptimizationHint
        {
            None,
            Balanced,
            MemoryBound,
            ComputeBound,
            Latency
        }

        // CUDA enums
        private enum CudaError
        {
            Success = 0,
            // Add other error codes as needed
        }

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
            public ulong SharedSizeBytes;
            public ulong ConstSizeBytes;
            public ulong LocalSizeBytes;
            public int MaxThreadsPerBlock;
            public int NumRegs;
            public int PtxVersion;
            public int BinaryVersion;
            public IntPtr CacheModeCA;
            public int MaxDynamicSharedSizeBytes;
            public int PreferredShmemCarveout;
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
            public OccupancyException(string message) : base(message) { }
            public OccupancyException()
            {
            }
            public OccupancyException(string message, Exception innerException) : base(message, innerException)
            {
            }
        }
    }
}