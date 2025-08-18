// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Launch configuration for CUDA kernels
/// </summary>
public readonly struct CudaLaunchConfig
{
    public uint GridX { get; }
    public uint GridY { get; }
    public uint GridZ { get; }
    public uint BlockX { get; }
    public uint BlockY { get; }
    public uint BlockZ { get; }
    public uint SharedMemoryBytes { get; }
    
    public CudaLaunchConfig(
        uint gridX, uint gridY, uint gridZ,
        uint blockX, uint blockY, uint blockZ,
        uint sharedMemoryBytes = 0)
    {
        GridX = gridX;
        GridY = gridY;
        GridZ = gridZ;
        BlockX = blockX;
        BlockY = blockY;
        BlockZ = blockZ;
        SharedMemoryBytes = sharedMemoryBytes;
    }
    
    public static CudaLaunchConfig Create1D(int totalThreads, int blockSize = 256)
    {
        var gridSize = (uint)((totalThreads + blockSize - 1) / blockSize);
        return new CudaLaunchConfig(gridSize, 1, 1, (uint)blockSize, 1, 1);
    }
    
    public static CudaLaunchConfig Create2D(int width, int height, int blockSizeX = 16, int blockSizeY = 16)
    {
        var gridX = (uint)((width + blockSizeX - 1) / blockSizeX);
        var gridY = (uint)((height + blockSizeY - 1) / blockSizeY);
        return new CudaLaunchConfig(gridX, gridY, 1, (uint)blockSizeX, (uint)blockSizeY, 1);
    }
    
    public static CudaLaunchConfig Create3D(
        int width, int height, int depth,
        int blockSizeX = 8, int blockSizeY = 8, int blockSizeZ = 8)
    {
        var gridX = (uint)((width + blockSizeX - 1) / blockSizeX);
        var gridY = (uint)((height + blockSizeY - 1) / blockSizeY);
        var gridZ = (uint)((depth + blockSizeZ - 1) / blockSizeZ);
        return new CudaLaunchConfig(gridX, gridY, gridZ, (uint)blockSizeX, (uint)blockSizeY, (uint)blockSizeZ);
    }
}

/// <summary>
/// Enhanced CUDA kernel launcher with automatic configuration optimization
/// </summary>
public sealed class CudaKernelLauncher
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly int _deviceId;
    private readonly CudaDeviceProperties _deviceProps;
    
    public CudaKernelLauncher(CudaContext context, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _deviceId = context.DeviceId;
        
        // Cache device properties for optimization calculations
        var result = CudaRuntime.cudaGetDeviceProperties(ref _deviceProps, _deviceId);
        if (result != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to get device properties: {result}");
        }
    }
    
    /// <summary>
    /// Launches a CUDA kernel with automatic configuration optimization
    /// </summary>
    public async Task LaunchKernelAsync(
        IntPtr function,
        Abstractions.KernelArguments arguments,
        CudaLaunchConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        _context.MakeCurrent();
        
        // Use provided config or calculate optimal one
        var launchConfig = config ?? CalculateOptimalLaunchConfig(arguments);
        
        // Prepare kernel arguments
        var argPointers = new List<IntPtr>();
        var handles = new List<GCHandle>();
        
        try
        {
            for (int i = 0; i < arguments.Length; i++)
            {
                IntPtr argPtr = PrepareKernelArgument(arguments.Get(i), handles);
                argPointers.Add(argPtr);
            }
            
            // Pin argument array
            var argPtrs = argPointers.ToArray();
            var argPtrsHandle = GCHandle.Alloc(argPtrs, GCHandleType.Pinned);
            
            try
            {
                _logger.LogDebug("Launching CUDA kernel with config: Grid({GridX},{GridY},{GridZ}), Block({BlockX},{BlockY},{BlockZ}), SharedMem={SharedMem}",
                    launchConfig.GridX, launchConfig.GridY, launchConfig.GridZ,
                    launchConfig.BlockX, launchConfig.BlockY, launchConfig.BlockZ,
                    launchConfig.SharedMemoryBytes);
                
                // Launch the kernel
                var result = CudaRuntime.cuLaunchKernel(
                    function,
                    launchConfig.GridX, launchConfig.GridY, launchConfig.GridZ,
                    launchConfig.BlockX, launchConfig.BlockY, launchConfig.BlockZ,
                    launchConfig.SharedMemoryBytes,
                    _context.Stream,
                    argPtrsHandle.AddrOfPinnedObject(),
                    IntPtr.Zero);
                
                CudaRuntime.CheckError(result, "Kernel launch");
                
                // Synchronize asynchronously
                await Task.Run(_context.Synchronize, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                argPtrsHandle.Free();
            }
        }
        finally
        {
            // Clean up argument handles
            foreach (var handle in handles)
            {
                handle.Free();
            }
        }
    }
    
    /// <summary>
    /// Calculates optimal launch configuration based on device properties and workload
    /// </summary>
    public CudaLaunchConfig CalculateOptimalLaunchConfig(Abstractions.KernelArguments arguments)
    {
        // Default to 1D configuration with optimal block size
        var optimalBlockSize = CalculateOptimalBlockSize();
        
        // Try to determine problem size from arguments
        var problemSize = EstimateProblemSize(arguments);
        
        if (problemSize > 0)
        {
            return CudaLaunchConfig.Create1D(problemSize, optimalBlockSize);
        }
        
        // Default configuration for unknown problem sizes
        return new CudaLaunchConfig(1, 1, 1, (uint)optimalBlockSize, 1, 1);
    }
    
    /// <summary>
    /// Calculates optimal block size based on device properties with RTX 2000 Ada optimizations
    /// </summary>
    private int CalculateOptimalBlockSize()
    {
        // Use a multiple of warp size for optimal performance
        var warpSize = _deviceProps.WarpSize;
        var maxThreadsPerBlock = _deviceProps.MaxThreadsPerBlock;
        var multiprocessorCount = _deviceProps.MultiProcessorCount;
        var major = _deviceProps.Major;
        var minor = _deviceProps.Minor;
        
        // RTX 2000 Ada specific optimizations (compute capability 8.9)
        if (major == 8 && minor == 9)
        {
            // Ada has 24 SMs with 1536 threads each
            // Optimal: 3 blocks of 512 threads per SM for max occupancy
            var optimalBlockSize = 512;
            
            // Validate and align to warp size
            optimalBlockSize = Math.Min(optimalBlockSize, maxThreadsPerBlock);
            optimalBlockSize = (optimalBlockSize / warpSize) * warpSize;
            
            _logger.LogDebug("RTX 2000 Ada optimal block size: {BlockSize} threads", optimalBlockSize);
            return optimalBlockSize;
        }
        
        // Target 4-8 blocks per multiprocessor for good occupancy
        var targetBlocksPerSM = major >= 8 ? 6 : 4; // Higher occupancy for Ampere+
        var targetBlockSize = (_deviceProps.MaxThreadsPerMultiProcessor / targetBlocksPerSM);
        
        // Round down to nearest multiple of warp size
        targetBlockSize = (targetBlockSize / warpSize) * warpSize;
        
        // Clamp to valid range
        targetBlockSize = Math.Max(warpSize, Math.Min(targetBlockSize, maxThreadsPerBlock));
        
        return targetBlockSize;
    }
    
    /// <summary>
    /// Estimates problem size from kernel arguments
    /// </summary>
    private int EstimateProblemSize(Abstractions.KernelArguments arguments)
    {
        // Look for buffer sizes or explicit size parameters
        for (int i = 0; i < arguments.Length; i++)
        {
            var argValue = arguments.Get(i);
            
            // Check for memory buffers
            if (argValue is ISyncMemoryBuffer memoryBuffer)
            {
                // Estimate element count for common data types
                var elementSize = EstimateElementSize(memoryBuffer);
                if (elementSize > 0)
                {
                    return (int)(memoryBuffer.SizeInBytes / elementSize);
                }
            }
            
            // Check for explicit size parameters (integers)
            if (argValue is int intSize && intSize > 0 && intSize < int.MaxValue / 4)
            {
                return intSize;
            }
            
            if (argValue is uint uintSize && uintSize > 0 && uintSize < uint.MaxValue / 4)
            {
                return (int)uintSize;
            }
        }
        
        return 0; // Unknown size
    }
    
    /// <summary>
    /// Estimates element size for memory buffers
    /// </summary>
    private static int EstimateElementSize(ISyncMemoryBuffer buffer)
    {
        // Common data types and their sizes
        var commonSizes = new[] { 4, 8, 16, 32 }; // float, double, float4, double4, etc.
        
        foreach (var size in commonSizes)
        {
            if (buffer.SizeInBytes % size == 0)
            {
                var elementCount = buffer.SizeInBytes / size;
                // Reasonable element count range
                if (elementCount >= 100 && elementCount <= 100_000_000)
                {
                    return size;
                }
            }
        }
        
        return 4; // Default to float size
    }
    
    /// <summary>
    /// Prepares a single kernel argument for launch
    /// </summary>
    private static IntPtr PrepareKernelArgument(object argValue, List<GCHandle> handles)
    {
        // Check if it's a memory buffer type first
        if (argValue is ISyncMemoryBuffer memoryBuffer)
        {
            // For memory buffers, we need the device pointer
            if (memoryBuffer is Memory.CudaMemoryBuffer cudaBuffer)
            {
                var devicePtr = cudaBuffer.DevicePointer;
                var handle = GCHandle.Alloc(devicePtr, GCHandleType.Pinned);
                handles.Add(handle);
                return handle.AddrOfPinnedObject();
            }
        }
        
        // For scalar values, pin the value directly
        var scalarHandle = GCHandle.Alloc(argValue, GCHandleType.Pinned);
        handles.Add(scalarHandle);
        return scalarHandle.AddrOfPinnedObject();
    }
    
    /// <summary>
    /// Gets optimal configuration for a specific workload pattern
    /// </summary>
    public CudaLaunchConfig GetOptimalConfigFor1D(int totalElements, int? preferredBlockSize = null)
    {
        var blockSize = preferredBlockSize ?? CalculateOptimalBlockSize();
        return CudaLaunchConfig.Create1D(totalElements, blockSize);
    }
    
    /// <summary>
    /// Gets optimal configuration for 2D workloads (e.g., image processing)
    /// </summary>
    public CudaLaunchConfig GetOptimalConfigFor2D(int width, int height)
    {
        // RTX 2000 Ada optimization for 2D workloads
        if (_deviceProps.Major == 8 && _deviceProps.Minor == 9)
        {
            // Use 16x32 blocks for optimal memory coalescing on Ada
            return CudaLaunchConfig.Create2D(width, height, 16, 32);
        }
        
        // Use 16x16 blocks for good memory coalescing in 2D workloads
        return CudaLaunchConfig.Create2D(width, height, 16, 16);
    }
    
    /// <summary>
    /// Gets optimal configuration for 3D workloads
    /// </summary>
    public CudaLaunchConfig GetOptimalConfigFor3D(int width, int height, int depth)
    {
        // RTX 2000 Ada optimization for 3D workloads
        if (_deviceProps.Major == 8 && _deviceProps.Minor == 9)
        {
            // Use 8x8x8 blocks optimized for Ada's cache hierarchy
            return CudaLaunchConfig.Create3D(width, height, depth, 8, 8, 8);
        }
        
        // Use 8x8x8 blocks for 3D workloads
        return CudaLaunchConfig.Create3D(width, height, depth, 8, 8, 8);
    }
    
    /// <summary>
    /// Validates launch configuration against device limits with Ada-specific checks
    /// </summary>
    public bool ValidateLaunchConfig(CudaLaunchConfig config)
    {
        // Check block dimensions
        var blockSize = config.BlockX * config.BlockY * config.BlockZ;
        if (blockSize > _deviceProps.MaxThreadsPerBlock)
        {
            _logger.LogWarning("Block size {BlockSize} exceeds device limit {MaxThreadsPerBlock}", 
                blockSize, _deviceProps.MaxThreadsPerBlock);
            return false;
        }
        
        // Check individual block dimensions
        if (config.BlockX > _deviceProps.MaxThreadsDim[0] ||
            config.BlockY > _deviceProps.MaxThreadsDim[1] ||
            config.BlockZ > _deviceProps.MaxThreadsDim[2])
        {
            _logger.LogWarning("Block dimensions ({BlockX},{BlockY},{BlockZ}) exceed device limits ({MaxX},{MaxY},{MaxZ})", 
                config.BlockX, config.BlockY, config.BlockZ,
                _deviceProps.MaxThreadsDim[0], _deviceProps.MaxThreadsDim[1], _deviceProps.MaxThreadsDim[2]);
            return false;
        }
        
        // Check grid dimensions
        if (config.GridX > _deviceProps.MaxGridSize[0] ||
            config.GridY > _deviceProps.MaxGridSize[1] ||
            config.GridZ > _deviceProps.MaxGridSize[2])
        {
            _logger.LogWarning("Grid dimensions ({GridX},{GridY},{GridZ}) exceed device limits ({MaxX},{MaxY},{MaxZ})", 
                config.GridX, config.GridY, config.GridZ,
                _deviceProps.MaxGridSize[0], _deviceProps.MaxGridSize[1], _deviceProps.MaxGridSize[2]);
            return false;
        }
        
        // Check shared memory - RTX 2000 Ada has 100KB available
        var maxSharedMem = _deviceProps.SharedMemPerBlock;
        if (_deviceProps.Major == 8 && _deviceProps.Minor == 9)
        {
            // Ada generation can use up to 100KB shared memory with opt-in
            maxSharedMem = 102400; // 100KB
        }
        
        if (config.SharedMemoryBytes > maxSharedMem)
        {
            _logger.LogWarning("Shared memory {SharedMemBytes} bytes exceeds device limit {MaxSharedMem} bytes", 
                config.SharedMemoryBytes, maxSharedMem);
            return false;
        }
        
        // RTX 2000 Ada specific validation
        if (_deviceProps.Major == 8 && _deviceProps.Minor == 9)
        {
            // Optimal occupancy check for Ada
            var blocksPerSM = _deviceProps.MaxThreadsPerMultiProcessor / (int)blockSize;
            if (blocksPerSM < 2)
            {
                _logger.LogInformation("RTX 2000 Ada: Low occupancy detected. Consider reducing block size for better performance");
            }
        }
        
        return true;
    }
}