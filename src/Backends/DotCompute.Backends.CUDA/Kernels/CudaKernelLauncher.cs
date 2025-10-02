// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;

namespace DotCompute.Backends.CUDA.Compilation
{

    /// <summary>
    /// Launch configuration for CUDA kernels
    /// </summary>
    public readonly struct CudaLaunchConfig(
        uint gridX, uint gridY, uint gridZ,
        uint blockX, uint blockY, uint blockZ,
        uint sharedMemoryBytes = 0)
    {
        public uint GridX { get; } = gridX;
        public uint GridY { get; } = gridY;
        public uint GridZ { get; } = gridZ;
        public uint BlockX { get; } = blockX;
        public uint BlockY { get; } = blockY;
        public uint BlockZ { get; } = blockZ;
        public uint SharedMemoryBytes { get; } = sharedMemoryBytes;

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
            CancellationToken cancellationToken = default) => await LaunchKernelInternalAsync(function, arguments, config, false, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Launches a CUDA cooperative kernel for grid-wide synchronization (CUDA 13.0+)
        /// </summary>
        public async Task LaunchCooperativeKernelAsync(
            IntPtr function,
            KernelArguments arguments,
            CudaLaunchConfig? config = null,
            CancellationToken cancellationToken = default) => await LaunchKernelInternalAsync(function, arguments, config, true, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Internal kernel launch implementation supporting both regular and cooperative launches
        /// </summary>
        private async Task LaunchKernelInternalAsync(
            IntPtr function,
            KernelArguments arguments,
            CudaLaunchConfig? config,
            bool useCooperativeLaunch,
            CancellationToken cancellationToken)
        {
            _context.MakeCurrent();

            // Use provided config or calculate optimal one
            var launchConfig = config ?? CalculateOptimalLaunchConfig(arguments);

            // Prepare kernel arguments
            var argPointers = new List<IntPtr>();
            var handles = new List<GCHandle>();
            var unmanagedAllocations = new List<IntPtr>(); // Track unmanaged memory allocations

            try
            {
                for (var i = 0; i < arguments.Count; i++)
                {
                    var arg = arguments.Get(i) ?? throw new ArgumentNullException($"Argument at index {i} is null");

                    // Enhanced diagnostic logging for debugging scale kernel issues

                    _logger.LogInfoMessage($"Preparing kernel argument {i}: Type={arg.GetType().Name}, Value={arg}, FullName={arg.GetType().FullName}");


                    var argPtr = PrepareKernelArgument(arg, handles, unmanagedAllocations, _logger);
                    argPointers.Add(argPtr);


                    _logger.LogDebugMessage($"Kernel argument {i} prepared: Pointer=0x{argPtr.ToInt64()}");
                }

                // Pin argument array - this creates an array of pointers where each entry points to an argument value
                var argPtrs = argPointers.ToArray();
                var argPtrsHandle = GCHandle.Alloc(argPtrs, GCHandleType.Pinned);

                try
                {
                    _logger.LogDebugMessage($"Launching CUDA kernel with config: Grid({launchConfig.GridX},{launchConfig.GridY},{launchConfig.GridZ}), Block({launchConfig.BlockX},{launchConfig.BlockY},{launchConfig.BlockZ}), SharedMem={launchConfig.SharedMemoryBytes}, ArgCount={argPointers.Count}");

                    // Additional diagnostic logging for debugging

                    _logger.LogDebugMessage($"Total threads: {launchConfig.GridX * launchConfig.GridY * launchConfig.GridZ * launchConfig.BlockX * launchConfig.BlockY * launchConfig.BlockZ}, Function ptr: 0x{function.ToInt64()}, Stream: 0x{_context.Stream.ToInt64()}");

                    // Log first few argument pointers for debugging

                    for (var i = 0; i < Math.Min(5, argPointers.Count); i++)
                    {
                        unsafe
                        {
                            var ptr = argPointers[i];
                            if (ptr != IntPtr.Zero)
                            {
                                // Try to read the value at the pointer location
                                var value = *(IntPtr*)ptr;
                                _logger.LogDebugMessage($"Arg[{i}]: Ptr=0x{ptr.ToInt64()} -> Value=0x{value.ToInt64()}");
                            }
                        }
                    }

                    // Launch the kernel
                    CudaError result;


                    if (useCooperativeLaunch)
                    {
                        // Check if device supports cooperative launches
                        if (_deviceProps.Major < 6)
                        {
                            throw new NotSupportedException($"Cooperative kernel launches require compute capability 6.0+, but device has {_deviceProps.Major}.{_deviceProps.Minor}");
                        }

                        _logger.LogDebugMessage("Launching cooperative kernel for grid-wide synchronization");


                        result = CudaRuntime.cuLaunchCooperativeKernel(
                            function,
                            launchConfig.GridX, launchConfig.GridY, launchConfig.GridZ,
                            launchConfig.BlockX, launchConfig.BlockY, launchConfig.BlockZ,
                            launchConfig.SharedMemoryBytes,
                            _context.Stream,
                            argPtrsHandle.AddrOfPinnedObject());
                    }
                    else
                    {
                        result = CudaRuntime.cuLaunchKernel(
                            function,
                            launchConfig.GridX, launchConfig.GridY, launchConfig.GridZ,
                            launchConfig.BlockX, launchConfig.BlockY, launchConfig.BlockZ,
                            launchConfig.SharedMemoryBytes,
                            _context.Stream,
                            argPtrsHandle.AddrOfPinnedObject(),
                            IntPtr.Zero);
                    }

                    CudaRuntime.CheckError(result, useCooperativeLaunch ? "Cooperative kernel launch" : "Kernel launch");

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

                // Clean up unmanaged memory allocations

                foreach (var ptr in unmanagedAllocations)
                {
                    if (ptr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
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
        /// Calculates optimal block size based on device properties with RTX 2000 Ada optimizations
        /// </summary>
        private int CalculateOptimalBlockSize()
        {
            // Use a multiple of warp size for optimal performance
            var warpSize = _deviceProps.WarpSize;
            var maxThreadsPerBlock = _deviceProps.MaxThreadsPerBlock;
            _ = _deviceProps.MultiProcessorCount;
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

                _logger.LogDebugMessage(" threads");
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
        private static int EstimateProblemSize(KernelArguments arguments)
        {
            // Look for buffer sizes or explicit size parameters
            for (var i = 0; i < arguments.Count; i++)
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
                    if (elementCount is >= 100 and <= 100_000_000)
                    {
                        return size;
                    }
                }
            }

            return 4; // Default to float size
        }

        /// <summary>
        /// Prepares a single kernel argument for launch following ILGPU/NVIDIA best practices
        /// </summary>
        private static IntPtr PrepareKernelArgument(object argValue, List<GCHandle> handles, List<IntPtr> unmanagedAllocations, ILogger logger)
        {
            // Validate input
            if (argValue == null)
            {
                throw new ArgumentNullException(nameof(argValue), "Kernel argument cannot be null");
            }

            // ILGPU-inspired blittable type validation for better error reporting

            var argType = argValue.GetType();
            if (!IsValidKernelParameterType(argType))
            {
                logger?.LogWarning("Potentially invalid kernel parameter type: {Type}. " +
                    "CUDA kernels prefer blittable value types for optimal performance.", argType.FullName);
            }

            // First, check for SimpleCudaUnifiedMemoryBuffer<T> specifically

            if (argType.IsGenericType &&

                argType.FullName != null &&

                argType.FullName.Contains("SimpleCudaUnifiedMemoryBuffer"))
            {
                // Use reflection to get DevicePointer property
                var devicePtrProperty = argType.GetProperty("DevicePointer");
                if (devicePtrProperty != null && devicePtrProperty.GetValue(argValue) is IntPtr devicePtr)
                {
                    unsafe
                    {
                        var ptrStorage = Marshal.AllocHGlobal(sizeof(IntPtr));
                        *(IntPtr*)ptrStorage = devicePtr;
                        unmanagedAllocations.Add(ptrStorage);


                        logger?.LogInformation("SimpleCudaUnifiedMemoryBuffer (first check): DevicePtr=0x{DevicePtr:X}, Storage=0x{Storage:X}",
                            devicePtr.ToInt64(), ptrStorage.ToInt64());


                        return ptrStorage;
                    }
                }
            }

            // Second, check for other CudaMemoryBuffer<T> types

            if (argType.IsGenericType &&

                argType.FullName != null &&

                argType.FullName.Contains("CudaMemoryBuffer"))
            {
                // Try to get the DevicePointer property (internal)
                var devicePtrProp = argType.GetProperty("DevicePointer",

                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);


                if (devicePtrProp != null && devicePtrProp.GetValue(argValue) is IntPtr devicePtr && devicePtr != IntPtr.Zero)
                {
                    // CRITICAL: We need to store the device pointer value and return a pointer TO it
                    // CUDA expects a pointer to the argument value, not the value itself
                    unsafe
                    {
                        // Allocate unmanaged memory to hold the device pointer value
                        var ptrStorage = Marshal.AllocHGlobal(sizeof(IntPtr));
                        *(IntPtr*)ptrStorage = devicePtr;
                        // Track this allocation for cleanup
                        unmanagedAllocations.Add(ptrStorage);


                        logger?.LogDebug("CudaMemoryBuffer: DevicePtr=0x{DevicePtr:X}, Storage=0x{Storage:X}",
                            devicePtr.ToInt64(), ptrStorage.ToInt64());


                        return ptrStorage;
                    }
                }

                // Fallback to field if property not found

                var devicePtrField = argType.GetField("_devicePtr",

                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);


                if (devicePtrField != null && devicePtrField.GetValue(argValue) is IntPtr fieldPtr && fieldPtr != IntPtr.Zero)
                {
                    // CRITICAL: We need to store the device pointer value and return a pointer TO it
                    unsafe
                    {
                        // Allocate unmanaged memory to hold the device pointer value
                        var ptrStorage = Marshal.AllocHGlobal(sizeof(IntPtr));
                        *(IntPtr*)ptrStorage = fieldPtr;
                        // Track this allocation for cleanup
                        unmanagedAllocations.Add(ptrStorage);


                        logger?.LogDebug("CudaMemoryBuffer (field): DevicePtr=0x{DevicePtr:X}, Storage=0x{Storage:X}",
                            fieldPtr.ToInt64(), ptrStorage.ToInt64());


                        return ptrStorage;
                    }
                }
            }

            // Check if it's a memory buffer type second
            if (argValue is ISyncMemoryBuffer)
            {
                // For CUDA memory buffers, we need the device pointer
                if (argValue is Memory.CudaMemoryBuffer cudaBuffer)
                {
                    var devicePtr = cudaBuffer.DevicePointer;
                    // CRITICAL: Store the device pointer value and return a pointer TO it
                    unsafe
                    {
                        var ptrStorage = Marshal.AllocHGlobal(sizeof(IntPtr));
                        *(IntPtr*)ptrStorage = devicePtr;
                        unmanagedAllocations.Add(ptrStorage);


                        logger?.LogDebug("CudaMemoryBuffer: DevicePtr=0x{DevicePtr:X}, Storage=0x{Storage:X}",
                            devicePtr.ToInt64(), ptrStorage.ToInt64());


                        return ptrStorage;
                    }
                }

                // This code was moved above the ISyncMemoryBuffer check

                // Handle SimpleCudaUnifiedMemoryBuffer<T> specifically

                if (argValue.GetType().Name.StartsWith("SimpleCudaUnifiedMemoryBuffer"))
                {
                    // Use reflection to get DevicePointer property
                    var devicePtrProperty = argValue.GetType().GetProperty("DevicePointer");
                    if (devicePtrProperty != null && devicePtrProperty.GetValue(argValue) is IntPtr devicePtr)
                    {
                        unsafe
                        {
                            var ptrStorage = Marshal.AllocHGlobal(sizeof(IntPtr));
                            *(IntPtr*)ptrStorage = devicePtr;
                            unmanagedAllocations.Add(ptrStorage);


                            logger?.LogDebug("SimpleCudaUnifiedMemoryBuffer: DevicePtr=0x{DevicePtr:X}, Storage=0x{Storage:X}",
                                devicePtr.ToInt64(), ptrStorage.ToInt64());


                            return ptrStorage;
                        }
                    }
                }

                // Handle other types of unified memory buffers
                if (argValue is IUnifiedMemoryBuffer unifiedBuffer)
                {
                    // For other unified memory buffer types, we might need different handling
                    // For now, try to get the device pointer if available through dynamic access
                    var bufferType = unifiedBuffer.GetType();
                    var devicePtrProperty = bufferType.GetProperty("DevicePointer");


                    if (devicePtrProperty != null && devicePtrProperty.GetValue(unifiedBuffer) is IntPtr devicePtr)
                    {
                        // CRITICAL: Store the device pointer value and return a pointer TO it
                        unsafe
                        {
                            var ptrStorage = Marshal.AllocHGlobal(sizeof(IntPtr));
                            *(IntPtr*)ptrStorage = devicePtr;
                            unmanagedAllocations.Add(ptrStorage);


                            logger?.LogDebug("IUnifiedMemoryBuffer: DevicePtr=0x{DevicePtr:X}, Storage=0x{Storage:X}",
                                devicePtr.ToInt64(), ptrStorage.ToInt64());


                            return ptrStorage;
                        }
                    }
                }
            }

            // Handle primitive types and blittable structs
            if (CanPinDirectly(argValue))
            {
                // For scalars, we also need to allocate unmanaged memory and copy the value
                // This ensures proper memory alignment and lifetime management
                unsafe
                {
                    var valueType = argValue.GetType();
                    var size = Marshal.SizeOf(valueType);
                    var ptrStorage = Marshal.AllocHGlobal(size);

                    // Copy the value to unmanaged memory

                    Marshal.StructureToPtr(argValue, ptrStorage, false);


                    unmanagedAllocations.Add(ptrStorage);


                    logger?.LogInformation("Scalar argument: Type={Type}, Value={Value}, Size={Size}, Ptr=0x{Ptr:X}",
                        valueType.Name, argValue, size, ptrStorage.ToInt64());


                    return ptrStorage;
                }
            }

            // Handle arrays of blittable types
            if (argValue is Array array && array.Length > 0)
            {
                var elementType = array.GetType().GetElementType()!;
                if (IsBlittableType(elementType))
                {
                    var arrayHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
                    handles.Add(arrayHandle);
                    return arrayHandle.AddrOfPinnedObject();
                }
            }

            // For non-blittable objects, try to extract a pinnable value
            // This handles cases like passing complex objects that need marshaling
            if (TryExtractPinnableValue(argValue, out var pinnableValue))
            {
                var extractedHandle = GCHandle.Alloc(pinnableValue, GCHandleType.Pinned);
                handles.Add(extractedHandle);
                return extractedHandle.AddrOfPinnedObject();
            }

            // Last resort: convert to a pinnable representation
            // For objects that cannot be pinned, try to serialize to bytes or convert to IntPtr
            if (argValue is IntPtr ptr)
            {
                var ptrHandle = GCHandle.Alloc(ptr, GCHandleType.Pinned);
                handles.Add(ptrHandle);
                return ptrHandle.AddrOfPinnedObject();
            }

            // For other objects, throw a more descriptive exception
            throw new ArgumentException($"Cannot marshal argument of type '{argValue.GetType().FullName}' for CUDA kernel. " +
                                      "Supported types: primitives, blittable structs, arrays of blittable types, memory buffers, and IntPtr.",

                                      nameof(argValue));
        }

        /// <summary>
        /// Validates if a type is suitable for CUDA kernel parameters (ILGPU-inspired)
        /// Based on ILGPU's blittable type checks and NVIDIA best practices
        /// </summary>
        private static bool IsValidKernelParameterType(Type type)
        {
            // ILGPU rule: All parameter types must be value types
            if (!type.IsValueType)
            {
                return false;
            }

            // Primitive types are always valid

            if (type.IsPrimitive)
            {
                return true;
            }

            // Enums are valid (they're value types with primitive underlying types)

            if (type.IsEnum)
            {
                return true;
            }

            // Check for common valid struct types

            if (type == typeof(IntPtr) || type == typeof(UIntPtr))
            {

                return true;
            }

            // Generic types need special handling - memory buffers are passed as views/pointers

            if (type.IsGenericType)
            {
                _ = type.GetGenericTypeDefinition();
                // Allow memory buffer types - they'll be converted to device pointers
                if (type.FullName?.Contains("MemoryBuffer") == true ||

                    type.FullName?.Contains("ArrayView") == true)
                {

                    return true;
                }

            }

            // For other structs, check if they're likely blittable
            // This is a heuristic - true blittable checking requires runtime marshaling validation

            return type.IsValueType && !type.IsGenericType;
        }

        /// <summary>
        /// Checks if a value can be pinned directly with GCHandleType.Pinned
        /// </summary>
        private static bool CanPinDirectly(object value)
        {
            if (value == null)
            {
                return false;
            }

            var type = value.GetType();

            // Primitive types can be pinned

            if (type.IsPrimitive)
            {
                return true;
            }

            // IntPtr and UIntPtr can be pinned
            if (type == typeof(IntPtr) || type == typeof(UIntPtr))
            {
                return true;
            }

            // Enums can be pinned
            if (type.IsEnum)
            {
                return true;
            }

            // Value types without references can be pinned
            if (type.IsValueType && IsBlittableType(type))
            {
                return true;
            }


            return false;
        }

        /// <summary>
        /// Checks if a type is blittable (can be pinned and has the same representation in managed and unmanaged code)
        /// </summary>
        private static bool IsBlittableType(Type type)
        {
            if (type.IsPrimitive)
            {
                return true;
            }


            if (type == typeof(IntPtr) || type == typeof(UIntPtr))
            {
                return true;
            }


            if (type.IsEnum)
            {
                return true;
            }


            if (type.IsValueType)
            {
                // Common blittable structs
                if (type == typeof(Guid) ||

                    type == typeof(DateTime) ||

                    type == typeof(decimal))
                {
                    return false; // These have special layouts
                }

                // Check if all fields are blittable
                var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                foreach (var field in fields)
                {
                    if (!IsBlittableType(field.FieldType))
                    {
                        return false;
                    }

                }
                return true;
            }


            return false;
        }

        /// <summary>
        /// Attempts to extract a pinnable value from a complex object
        /// </summary>
        private static bool TryExtractPinnableValue(object obj, out object pinnableValue)
        {
            pinnableValue = obj;

            // Check for common patterns like wrapper objects with Value properties

            var objType = obj.GetType();
            var valueProperty = objType.GetProperty("Value");
            if (valueProperty != null && CanPinDirectly(valueProperty.GetValue(obj)!))
            {
                pinnableValue = valueProperty.GetValue(obj)!;
                return true;
            }

            // Check for data properties

            var dataProperty = objType.GetProperty("Data");
            if (dataProperty != null)
            {
                var dataValue = dataProperty.GetValue(obj);
                if (dataValue != null && CanPinDirectly(dataValue))
                {
                    pinnableValue = dataValue;
                    return true;
                }
            }


            return false;
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
                _logger.LogWarningMessage($"Block size {blockSize} exceeds device limit {_deviceProps.MaxThreadsPerBlock}");
                return false;
            }

            // Check individual block dimensions
            if (config.BlockX > _deviceProps.MaxThreadsDimX ||
                config.BlockY > _deviceProps.MaxThreadsDimY ||
                config.BlockZ > _deviceProps.MaxThreadsDimZ)
            {
                _logger.LogWarningMessage($"Block dimensions ({config.BlockX},{config.BlockY},{config.BlockZ}) exceed device limits ({_deviceProps.MaxThreadsDimX},{_deviceProps.MaxThreadsDimY},{_deviceProps.MaxThreadsDimZ})");
                return false;
            }

            // Check grid dimensions
            if (config.GridX > _deviceProps.MaxGridSizeX ||
                config.GridY > _deviceProps.MaxGridSizeY ||
                config.GridZ > _deviceProps.MaxGridSizeZ)
            {
                _logger.LogWarningMessage($"Grid dimensions ({config.GridX},{config.GridY},{config.GridZ}) exceed device limits ({_deviceProps.MaxGridSizeX},{_deviceProps.MaxGridSizeY},{_deviceProps.MaxGridSizeZ})");
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
                _logger.LogWarningMessage($"Shared memory {config.SharedMemoryBytes} bytes exceeds device limit {maxSharedMem} bytes");
                return false;
            }

            // RTX 2000 Ada specific validation
            if (_deviceProps.Major == 8 && _deviceProps.Minor == 9)
            {
                // Optimal occupancy check for Ada
                var blocksPerSM = _deviceProps.MaxThreadsPerMultiProcessor / (int)blockSize;
                if (blocksPerSM < 2)
                {
                    _logger.LogInfoMessage("RTX 2000 Ada: Low occupancy detected. Consider reducing block size for better performance");
                }
            }

            return true;
        }
    }
}
