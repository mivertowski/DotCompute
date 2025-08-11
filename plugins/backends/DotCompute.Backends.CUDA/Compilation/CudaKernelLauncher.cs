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
        KernelArguments arguments,
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
            foreach (var arg in arguments.Arguments)
            {
                IntPtr argPtr = PrepareKernelArgument(arg, handles);
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
    public CudaLaunchConfig CalculateOptimalLaunchConfig(KernelArguments arguments)
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
    /// Calculates optimal block size based on device properties
    /// </summary>
    private int CalculateOptimalBlockSize()
    {
        // Use a multiple of warp size for optimal performance
        var warpSize = _deviceProps.WarpSize;
        var maxThreadsPerBlock = _deviceProps.MaxThreadsPerBlock;
        var multiprocessorCount = _deviceProps.MultiProcessorCount;
        
        // Target 4-8 blocks per multiprocessor for good occupancy
        var targetBlocksPerSM = 4;
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
    private int EstimateProblemSize(KernelArguments arguments)
    {
        // Look for buffer sizes or explicit size parameters
        foreach (var arg in arguments.Arguments)
        {
            // Check for memory buffers
            if (arg is ISyncMemoryBuffer buffer)
            {
                // Estimate element count for common data types
                var elementSize = EstimateElementSize(buffer);
                if (elementSize > 0)
                {
                    return (int)(buffer.SizeInBytes / elementSize);
                }
            }
            
            // Check for explicit size parameters (integers)
            if (arg is int intSize && intSize > 0 && intSize < int.MaxValue / 4)
            {
                return intSize;
            }
            
            if (arg is uint uintSize && uintSize > 0 && uintSize < uint.MaxValue / 4)
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
    private static IntPtr PrepareKernelArgument(object arg, List<GCHandle> handles)
    {
        switch (arg)
        {
            case Memory.CudaMemoryBuffer cudaBuffer:
                {
                    var devicePtr = cudaBuffer.DevicePointer;
                    var handle = GCHandle.Alloc(devicePtr, GCHandleType.Pinned);
                    handles.Add(handle);
                    return handle.AddrOfPinnedObject();
                }
                
            case Memory.CudaMemoryBufferView cudaView:
                {
                    var devicePtr = cudaView.DevicePointer;
                    var handle = GCHandle.Alloc(devicePtr, GCHandleType.Pinned);
                    handles.Add(handle);
                    return handle.AddrOfPinnedObject();
                }
                
            default:
                {
                    // Pin value types and references
                    var handle = GCHandle.Alloc(arg, GCHandleType.Pinned);
                    handles.Add(handle);
                    return handle.AddrOfPinnedObject();
                }
        }
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
        // Use 16x16 blocks for good memory coalescing in 2D workloads
        return CudaLaunchConfig.Create2D(width, height, 16, 16);
    }
    
    /// <summary>
    /// Gets optimal configuration for 3D workloads
    /// </summary>
    public CudaLaunchConfig GetOptimalConfigFor3D(int width, int height, int depth)
    {
        // Use 8x8x8 blocks for 3D workloads
        return CudaLaunchConfig.Create3D(width, height, depth, 8, 8, 8);
    }
    
    /// <summary>
    /// Validates launch configuration against device limits
    /// </summary>
    public bool ValidateLaunchConfig(CudaLaunchConfig config)
    {
        // Check block dimensions
        var blockSize = config.BlockX * config.BlockY * config.BlockZ;
        if (blockSize > _deviceProps.MaxThreadsPerBlock)
        {
            return false;
        }
        
        // Check individual block dimensions
        if (config.BlockX > _deviceProps.MaxThreadsDim[0] ||
            config.BlockY > _deviceProps.MaxThreadsDim[1] ||
            config.BlockZ > _deviceProps.MaxThreadsDim[2])
        {
            return false;
        }
        
        // Check grid dimensions
        if (config.GridX > _deviceProps.MaxGridSize[0] ||
            config.GridY > _deviceProps.MaxGridSize[1] ||
            config.GridZ > _deviceProps.MaxGridSize[2])
        {
            return false;
        }
        
        // Check shared memory
        if (config.SharedMemoryBytes > _deviceProps.SharedMemPerBlock)
        {
            return false;
        }
        
        return true;
    }
}