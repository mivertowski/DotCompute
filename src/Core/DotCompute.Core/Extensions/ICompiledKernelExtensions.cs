// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;

namespace DotCompute.Core.Extensions
{
    /// <summary>
    /// Extension methods for ICompiledKernel to provide backward compatibility,
    /// additional methods required by tests and legacy code, and convenient overloads
    /// for common kernel execution scenarios.
    /// </summary>
    public static class ICompiledKernelExtensions
    {
        private static readonly ConcurrentDictionary<string, object> s_metadataCache = new();


        /// <summary>
        /// Launches the kernel asynchronously with the given arguments.
        /// This is an alias for the ExecuteAsync method to match test expectations.
        /// </summary>
        /// <param name="kernel">The compiled kernel to execute</param>
        /// <param name="arguments">The kernel arguments</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A ValueTask representing the asynchronous operation</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask LaunchAsync(this ICompiledKernel kernel,

            KernelArguments arguments, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(kernel);
            ArgumentNullException.ThrowIfNull(arguments);


            return kernel.ExecuteAsync(arguments, cancellationToken);
        }

        /// <summary>
        /// Launches the kernel with a specific grid and block configuration.
        /// This provides additional overload for more explicit kernel launch parameters.
        /// </summary>
        /// <param name="kernel">The compiled kernel to execute</param>
        /// <param name="gridDim">Grid dimensions (x, y, z)</param>
        /// <param name="blockDim">Block dimensions (x, y, z)</param>
        /// <param name="arguments">The kernel arguments</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A ValueTask representing the asynchronous operation</returns>
        public static ValueTask LaunchAsync(this ICompiledKernel kernel,

            (int x, int y, int z) gridDim,

            (int x, int y, int z) blockDim,
            KernelArguments arguments,

            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(kernel);
            ArgumentNullException.ThrowIfNull(arguments);

            // Store grid and block dimensions in arguments for kernel execution
            // These will be extracted by the specific backend implementation

            arguments.SetGridDimensions(gridDim);
            arguments.SetBlockDimensions(blockDim);


            return kernel.ExecuteAsync(arguments, cancellationToken);
        }

        /// <summary>
        /// Launches the kernel with launch configuration and buffer arguments (non-generic version).
        /// This overload takes precedence for tests and avoids generic type inference issues.
        /// </summary>
        /// <param name="kernel">The compiled kernel to execute</param>
        /// <param name="launchConfig">Launch configuration (grid/block dimensions)</param>
        /// <param name="arguments">Variable number of kernel arguments</param>
        /// <returns>A ValueTask representing the asynchronous operation</returns>
        public static ValueTask LaunchAsync(this ICompiledKernel kernel,
            object launchConfig,
            params object[] arguments)
        {
            ArgumentNullException.ThrowIfNull(kernel);
            ArgumentNullException.ThrowIfNull(arguments);

            // Console.WriteLine($"[DEBUG] LaunchAsync non-generic called with launchConfig={launchConfig?.GetType().Name}, arguments.Length={arguments?.Length}");


            var kernelArgs = CreateKernelArgumentsFromObjects(arguments ?? []);

            // Store launch configuration for backend-specific handling

            if (launchConfig != null)
            {
                kernelArgs.SetLaunchConfiguration(launchConfig);
            }


            return kernel.ExecuteAsync(kernelArgs);
        }

        /// <summary>
        /// Launches the kernel with launch configuration and buffer arguments (generic version).
        /// This matches the test signature for vector operations with type safety.
        /// </summary>
        /// <typeparam name="T">The data type for type safety</typeparam>
        /// <param name="kernel">The compiled kernel to execute</param>
        /// <param name="launchConfig">Launch configuration (grid/block dimensions)</param>
        /// <param name="arguments">Variable number of kernel arguments</param>
        /// <returns>A ValueTask representing the asynchronous operation</returns>
        public static ValueTask LaunchAsync<T>(this ICompiledKernel kernel,
            object launchConfig,
            params object[] arguments) where T : unmanaged
        {
            ArgumentNullException.ThrowIfNull(kernel);
            ArgumentNullException.ThrowIfNull(arguments);

            // Console.WriteLine($"[DEBUG] LaunchAsync<T> called with launchConfig={launchConfig?.GetType().Name}, arguments.Length={arguments?.Length}");


            var kernelArgs = CreateKernelArgumentsFromObjects(arguments ?? []);

            // Store launch configuration for backend-specific handling

            if (launchConfig != null)
            {
                kernelArgs.SetLaunchConfiguration(launchConfig);
            }


            return kernel.ExecuteAsync(kernelArgs);
        }

        /// <summary>
        /// Launches the kernel with launch configuration and execution context support.
        /// This overload supports CUDA streams for concurrent execution.
        /// Uses IComputeExecution interface to match test expectations.
        /// </summary>
        /// <param name="kernel">The compiled kernel to execute</param>
        /// <param name="launchConfig">Launch configuration (grid/block dimensions)</param>
        /// <param name="stream">CUDA stream or execution context (IComputeExecution interface)</param>
        /// <param name="arguments">Variable number of kernel arguments</param>
        /// <returns>A ValueTask representing the asynchronous operation</returns>
        public static ValueTask LaunchAsync(this ICompiledKernel kernel,
            object launchConfig,
            IComputeExecution stream,
            params object[] arguments)
        {
            ArgumentNullException.ThrowIfNull(kernel);
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(arguments);

            // Console.WriteLine($"[DEBUG] LaunchAsync with IComputeExecution called with launchConfig={launchConfig?.GetType().Name}, stream={stream?.GetType().Name}, arguments.Length={arguments?.Length}");


            var kernelArgs = CreateKernelArgumentsFromObjects(arguments ?? []);

            // Store launch configuration and execution context for backend-specific handling

            if (launchConfig != null)
            {
                kernelArgs.SetLaunchConfiguration(launchConfig);
            }


            if (stream != null)
            {
                kernelArgs.SetExecutionStream(stream);
            }


            return kernel.ExecuteAsync(kernelArgs);
        }

        /// <summary>
        /// Executes the kernel with typed parameters for better compile-time safety.
        /// </summary>
        /// <typeparam name="TArg1">Type of the first argument</typeparam>
        /// <param name="kernel">The compiled kernel to execute</param>
        /// <param name="arg1">First kernel argument</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A ValueTask representing the asynchronous operation</returns>
        public static ValueTask ExecuteAsync<TArg1>(this ICompiledKernel kernel,
            TArg1 arg1,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(kernel);


            var arguments = new KernelArguments
            {
                arg1
            };


            return kernel.ExecuteAsync(arguments, cancellationToken);
        }

        /// <summary>
        /// Executes the kernel with two typed parameters.
        /// </summary>
        /// <typeparam name="TArg1">Type of the first argument</typeparam>
        /// <typeparam name="TArg2">Type of the second argument</typeparam>
        /// <param name="kernel">The compiled kernel to execute</param>
        /// <param name="arg1">First kernel argument</param>
        /// <param name="arg2">Second kernel argument</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A ValueTask representing the asynchronous operation</returns>
        public static ValueTask ExecuteAsync<TArg1, TArg2>(this ICompiledKernel kernel,
            TArg1 arg1,
            TArg2 arg2,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(kernel);


            var arguments = new KernelArguments
            {
                arg1,
                arg2
            };


            return kernel.ExecuteAsync(arguments, cancellationToken);
        }

        /// <summary>
        /// Executes the kernel with three typed parameters.
        /// </summary>
        /// <typeparam name="TArg1">Type of the first argument</typeparam>
        /// <typeparam name="TArg2">Type of the second argument</typeparam>
        /// <typeparam name="TArg3">Type of the third argument</typeparam>
        /// <param name="kernel">The compiled kernel to execute</param>
        /// <param name="arg1">First kernel argument</param>
        /// <param name="arg2">Second kernel argument</param>
        /// <param name="arg3">Third kernel argument</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A ValueTask representing the asynchronous operation</returns>
        public static ValueTask ExecuteAsync<TArg1, TArg2, TArg3>(this ICompiledKernel kernel,
            TArg1 arg1,
            TArg2 arg2,
            TArg3 arg3,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(kernel);


            var arguments = new KernelArguments
            {
                arg1,
                arg2,
                arg3
            };


            return kernel.ExecuteAsync(arguments, cancellationToken);
        }

        /// <summary>
        /// Executes the kernel with execution options.
        /// </summary>
        /// <param name="kernel">The compiled kernel to execute</param>
        /// <param name="arguments">Kernel arguments</param>
        /// <param name="options">Execution options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A ValueTask representing the asynchronous operation</returns>
        public static ValueTask ExecuteAsync(this ICompiledKernel kernel,
            KernelArguments arguments,
            KernelExecutionOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(kernel);
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(options);

            // Store execution options in arguments

            arguments.SetExecutionOptions(options);


            return kernel.ExecuteAsync(arguments, cancellationToken);
        }

        /// <summary>
        /// Executes the kernel with work group dimensions for OpenCL-style execution.
        /// </summary>
        /// <param name="kernel">The compiled kernel to execute</param>
        /// <param name="arguments">Kernel arguments</param>
        /// <param name="globalWorkSize">Global work size dimensions</param>
        /// <param name="localWorkSize">Local work size dimensions (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A ValueTask representing the asynchronous operation</returns>
        public static ValueTask ExecuteAsync(this ICompiledKernel kernel,
            KernelArguments arguments,
            int[] globalWorkSize,
            int[]? localWorkSize = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(kernel);
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(globalWorkSize);

            // Set work group dimensions

            arguments.SetGlobalWorkSize(globalWorkSize);


            if (localWorkSize != null)
            {
                arguments.SetLocalWorkSize(localWorkSize);
            }


            return kernel.ExecuteAsync(arguments, cancellationToken);
        }

        /// <summary>
        /// Bridge method for LINQ-style execution parameters compatibility.
        /// This handles cases where KernelExecutionParameters from the LINQ project is used.
        /// </summary>
        /// <param name="kernel">The compiled kernel to execute</param>
        /// <param name="parameters">Execution parameters object (duck-typed compatibility)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A Task representing the asynchronous operation</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Dynamic property lookup for LINQ-style execution parameters is intentional for duck-typed compatibility")]
        public static Task ExecuteAsync(this ICompiledKernel kernel,
            object parameters,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(kernel);
            ArgumentNullException.ThrowIfNull(parameters);


            var arguments = new KernelArguments();

            // Use reflection to extract properties from the parameters object

            var parametersType = parameters.GetType();

            // Check for GlobalWorkSize property

            var globalWorkSizeProp = parametersType.GetProperty("GlobalWorkSize");
            if (globalWorkSizeProp?.GetValue(parameters) is int[] globalWorkSize)
            {
                arguments.SetGlobalWorkSize(globalWorkSize);
            }

            // Check for LocalWorkSize property

            var localWorkSizeProp = parametersType.GetProperty("LocalWorkSize");
            if (localWorkSizeProp?.GetValue(parameters) is int[] localWorkSize)
            {
                arguments.SetLocalWorkSize(localWorkSize);
            }

            // Check for Arguments property (Dictionary<string, object>)

            var argumentsProp = parametersType.GetProperty("Arguments");
            if (argumentsProp?.GetValue(parameters) is Dictionary<string, object> argsDict)
            {
                foreach (var kvp in argsDict)
                {
                    arguments.Add(kvp.Value);
                }
            }

            // Check for SharedMemorySize property

            var sharedMemoryProp = parametersType.GetProperty("SharedMemorySize");
            if (sharedMemoryProp?.GetValue(parameters) is int sharedMemorySize && sharedMemorySize > 0)
            {
                arguments.SetSharedMemorySize(sharedMemorySize);
            }

            // Convert ValueTask to Task for compatibility

            return kernel.ExecuteAsync(arguments, cancellationToken).AsTask();
        }

        /// <summary>
        /// Gets metadata about the kernel for profiling and debugging.
        /// </summary>
        /// <param name="kernel">The compiled kernel</param>
        /// <returns>A dictionary containing kernel metadata</returns>
        public static ValueTask<Dictionary<string, object>> GetMetadataAsync(this ICompiledKernel kernel)
        {
            ArgumentNullException.ThrowIfNull(kernel);


            var cacheKey = $"metadata_{kernel.GetType().FullName}_{kernel.GetHashCode()}";


            if (s_metadataCache.TryGetValue(cacheKey, out var cachedMetadata) &&

                cachedMetadata is Dictionary<string, object> metadata)
            {
                return ValueTask.FromResult(metadata);
            }


            var newMetadata = new Dictionary<string, object>
            {
                ["KernelType"] = kernel.GetType().Name,
                ["Name"] = GetKernelName(kernel),
                ["Id"] = GetKernelId(kernel),
                ["CreatedAt"] = DateTime.UtcNow,
                ["IsDisposed"] = IsDisposed(kernel)
            };

            _ = s_metadataCache.TryAdd(cacheKey, newMetadata);
            return ValueTask.FromResult(newMetadata);
        }

        /// <summary>
        /// Checks if the kernel has been disposed.
        /// </summary>
        /// <param name="kernel">The compiled kernel</param>
        /// <returns>True if the kernel is disposed, false otherwise</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Dynamic property lookup for kernel disposal detection is intentional for different kernel implementations")]
        public static bool IsDisposed(this ICompiledKernel kernel)
        {
            ArgumentNullException.ThrowIfNull(kernel);

            // Try to access kernel properties to detect disposed state

            try
            {
                // Most kernel implementations will throw ObjectDisposedException if disposed
                _ = kernel.GetType().GetProperty("IsDisposed")?.GetValue(kernel);
                return false;
            }
            catch (ObjectDisposedException)
            {
                return true;
            }
            catch
            {
                // If we can't determine disposal state, assume it's not disposed
                return false;
            }
        }

        /// <summary>
        /// Gets the kernel name safely, handling different interface implementations.
        /// </summary>
        /// <param name="kernel">The compiled kernel</param>
        /// <returns>The kernel name or a default value</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Dynamic property lookup for kernel name is intentional for different kernel implementations")]
        private static string GetKernelName(ICompiledKernel kernel)
        {
            try
            {
                // Check if kernel has Name property (Abstractions interface)
                var nameProperty = kernel.GetType().GetProperty("Name");
                if (nameProperty != null && nameProperty.GetValue(kernel) is string name)
                {
                    return name;
                }


                return kernel.GetType().Name;
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Gets the kernel ID safely, handling different interface implementations.
        /// </summary>
        /// <param name="kernel">The compiled kernel</param>
        /// <returns>The kernel ID or a default value</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Dynamic property lookup for kernel ID is intentional for different kernel implementations")]
        private static Guid GetKernelId(ICompiledKernel kernel)
        {
            try
            {
                // Check if kernel has Id property (Abstractions interface)
                var idProperty = kernel.GetType().GetProperty("Id");
                if (idProperty != null && idProperty.GetValue(kernel) is Guid id)
                {
                    return id;
                }


                return Guid.NewGuid();
            }
            catch
            {
                return Guid.NewGuid();
            }
        }

        /// <summary>
        /// Creates KernelArguments from an array of objects.
        /// </summary>
        /// <param name="arguments">Array of arguments</param>
        /// <returns>KernelArguments instance</returns>
        private static KernelArguments CreateKernelArgumentsFromObjects(object[] arguments)
        {
            var kernelArgs = new KernelArguments();


            if (arguments != null)
            {
                foreach (var arg in arguments)
                {
                    if (arg != null)
                    {
                        kernelArgs.Add(arg);
                    }
                }
            }
            return kernelArgs;
        }
    }

    /// <summary>
    /// Extension methods for KernelArguments to support metadata storage.
    /// Uses a WeakKeyDictionary approach to avoid memory leaks.
    /// </summary>
    public static class KernelArgumentsExtensions
    {
        private static readonly ConditionalWeakTable<KernelArguments, Dictionary<string, object>> s_metadataTable = [];


        private const string GridDimensionsKey = "__grid_dimensions";
        private const string BlockDimensionsKey = "__block_dimensions";
        private const string LaunchConfigKey = "__launch_config";
        private const string ExecutionStreamKey = "__execution_stream";
        private const string ExecutionOptionsKey = "__execution_options";
        private const string GlobalWorkSizeKey = "__global_work_size";
        private const string LocalWorkSizeKey = "__local_work_size";
        private const string SharedMemorySizeKey = "__shared_memory_size";

        /// <summary>
        /// Sets metadata for the kernel arguments.
        /// </summary>
        /// <param name="arguments">The kernel arguments</param>
        /// <param name="key">The metadata key</param>
        /// <param name="value">The metadata value</param>
        public static void SetMetadata(this KernelArguments arguments, string key, object? value)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(key);


            var metadata = s_metadataTable.GetOrCreateValue(arguments);
            metadata[key] = value!;
        }


        /// <summary>
        /// Gets metadata from the kernel arguments.
        /// </summary>
        /// <typeparam name="T">The type of the metadata value</typeparam>
        /// <param name="arguments">The kernel arguments</param>
        /// <param name="key">The metadata key</param>
        /// <param name="defaultValue">The default value to return if key is not found</param>
        /// <returns>The metadata value or default value</returns>
        public static T GetMetadata<T>(this KernelArguments arguments, string key, T defaultValue = default!)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(key);


            if (s_metadataTable.TryGetValue(arguments, out var metadata) &&

                metadata.TryGetValue(key, out var value) &&
                value is T typedValue)
            {
                return typedValue;
            }


            return defaultValue;
        }

        /// <summary>
        /// Sets grid dimensions in the kernel arguments.
        /// </summary>
        /// <param name="arguments">The kernel arguments</param>
        /// <param name="gridDim">Grid dimensions</param>
        public static void SetGridDimensions(this KernelArguments arguments, (int x, int y, int z) gridDim) => arguments.SetMetadata(GridDimensionsKey, gridDim);

        /// <summary>
        /// Gets grid dimensions from the kernel arguments.
        /// </summary>
        /// <param name="arguments">The kernel arguments</param>
        /// <returns>Grid dimensions or default value</returns>
        public static (int x, int y, int z) GetGridDimensions(this KernelArguments arguments) => arguments.GetMetadata(GridDimensionsKey, (1, 1, 1));

        /// <summary>
        /// Sets block dimensions in the kernel arguments.
        /// </summary>
        /// <param name="arguments">The kernel arguments</param>
        /// <param name="blockDim">Block dimensions</param>
        public static void SetBlockDimensions(this KernelArguments arguments, (int x, int y, int z) blockDim) => arguments.SetMetadata(BlockDimensionsKey, blockDim);

        /// <summary>
        /// Gets block dimensions from the kernel arguments.
        /// </summary>
        /// <param name="arguments">The kernel arguments</param>
        /// <returns>Block dimensions or default value</returns>
        public static (int x, int y, int z) GetBlockDimensions(this KernelArguments arguments) => arguments.GetMetadata(BlockDimensionsKey, (1, 1, 1));

        /// <summary>
        /// Sets launch configuration in the kernel arguments.
        /// </summary>
        /// <param name="arguments">The kernel arguments</param>
        /// <param name="launchConfig">Launch configuration</param>
        public static void SetLaunchConfiguration(this KernelArguments arguments, object launchConfig) => arguments.SetMetadata(LaunchConfigKey, launchConfig);

        /// <summary>
        /// Gets launch configuration from the kernel arguments.
        /// </summary>
        /// <param name="arguments">The kernel arguments</param>
        /// <returns>Launch configuration or null</returns>
        public static object? GetLaunchConfiguration(this KernelArguments arguments) => arguments.GetMetadata<object?>(LaunchConfigKey, null);

        /// <summary>
        /// Sets execution stream in the kernel arguments.
        /// </summary>
        /// <param name="arguments">The kernel arguments</param>
        /// <param name="stream">Execution stream</param>
        public static void SetExecutionStream(this KernelArguments arguments, object stream) => arguments.SetMetadata(ExecutionStreamKey, stream);

        /// <summary>
        /// Gets execution stream from the kernel arguments.
        /// </summary>
        /// <param name="arguments">The kernel arguments</param>
        /// <returns>Execution stream or null</returns>
        public static object? GetExecutionStream(this KernelArguments arguments) => arguments.GetMetadata<object?>(ExecutionStreamKey, null);

        /// <summary>
        /// Sets execution options in the kernel arguments.
        /// </summary>
        /// <param name="arguments">The kernel arguments</param>
        /// <param name="options">Execution options</param>
        public static void SetExecutionOptions(this KernelArguments arguments, KernelExecutionOptions options) => arguments.SetMetadata(ExecutionOptionsKey, options);

        /// <summary>
        /// Gets execution options from the kernel arguments.
        /// </summary>
        /// <param name="arguments">The kernel arguments</param>
        /// <returns>Execution options or null</returns>
        public static KernelExecutionOptions? GetExecutionOptions(this KernelArguments arguments) => arguments.GetMetadata<KernelExecutionOptions?>(ExecutionOptionsKey, null);

        /// <summary>
        /// Sets global work size for OpenCL-style kernels.
        /// </summary>
        /// <param name="arguments">The kernel arguments</param>
        /// <param name="globalWorkSize">Global work size array</param>
        public static void SetGlobalWorkSize(this KernelArguments arguments, int[] globalWorkSize) => arguments.SetMetadata(GlobalWorkSizeKey, globalWorkSize);

        /// <summary>
        /// Gets global work size from the kernel arguments.
        /// </summary>
        /// <param name="arguments">The kernel arguments</param>
        /// <returns>Global work size array or null</returns>
        public static int[]? GetGlobalWorkSize(this KernelArguments arguments) => arguments.GetMetadata<int[]?>(GlobalWorkSizeKey, null);

        /// <summary>
        /// Sets local work size for OpenCL-style kernels.
        /// </summary>
        /// <param name="arguments">The kernel arguments</param>
        /// <param name="localWorkSize">Local work size array</param>
        public static void SetLocalWorkSize(this KernelArguments arguments, int[] localWorkSize) => arguments.SetMetadata(LocalWorkSizeKey, localWorkSize);

        /// <summary>
        /// Gets local work size from the kernel arguments.
        /// </summary>
        /// <param name="arguments">The kernel arguments</param>
        /// <returns>Local work size array or null</returns>
        public static int[]? GetLocalWorkSize(this KernelArguments arguments) => arguments.GetMetadata<int[]?>(LocalWorkSizeKey, null);

        /// <summary>
        /// Sets shared memory size in the kernel arguments.
        /// </summary>
        /// <param name="arguments">The kernel arguments</param>
        /// <param name="sharedMemorySize">Shared memory size in bytes</param>
        public static void SetSharedMemorySize(this KernelArguments arguments, int sharedMemorySize) => arguments.SetMetadata(SharedMemorySizeKey, sharedMemorySize);

        /// <summary>
        /// Gets shared memory size from the kernel arguments.
        /// </summary>
        /// <param name="arguments">The kernel arguments</param>
        /// <returns>Shared memory size in bytes or 0</returns>
        public static int GetSharedMemorySize(this KernelArguments arguments) => arguments.GetMetadata(SharedMemorySizeKey, 0);
    }
}
