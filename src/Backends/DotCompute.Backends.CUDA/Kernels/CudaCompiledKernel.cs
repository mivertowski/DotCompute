// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Core.Extensions;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - CUDA backend has dynamic logging requirements

namespace DotCompute.Backends.CUDA.Compilation
{

    /// <summary>
    /// Represents a compiled CUDA kernel ready for execution
    /// </summary>
    public sealed class CudaCompiledKernel : ICompiledKernel, IDisposable
    {
        private readonly CudaContext _context;
        private readonly ILogger _logger;
        private readonly byte[] _ptxData;
        private readonly CudaKernelLauncher _launcher;
        private IntPtr _module;
        private IntPtr _function;
        private bool _disposed;

        public string Name { get; }

        /// <summary>
        /// Gets the unique identifier for this compiled kernel
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Gets the CUDA function handle for this kernel
        /// </summary>
        public IntPtr FunctionHandle => _function;

        private readonly string _entryPoint;
        private static readonly Dictionary<IntPtr, CudaCompiledKernel> _kernelLookup = [];
        private readonly object _lookupLock = new();

        public CudaCompiledKernel(
            CudaContext context,
            string name,
            string entryPoint,
            byte[] ptxData,
            Abstractions.CompilationOptions? options,
            ILogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ptxData = ptxData ?? throw new ArgumentNullException(nameof(ptxData));

            Name = name ?? throw new ArgumentNullException(nameof(name));
            Id = Guid.NewGuid();
            _entryPoint = entryPoint ?? throw new ArgumentNullException(nameof(entryPoint));
            _launcher = new CudaKernelLauncher(_context, _logger);

            LoadModule();

            // Register in lookup table
            lock (_lookupLock)
            {
                _kernelLookup[_function] = this;
            }
        }

        /// <summary>
        /// Converts this CudaCompiledKernel to a CompiledKernel struct
        /// </summary>
        public CompiledKernel ToCompiledKernel()
        {
            var kernelId = Guid.NewGuid(); // Generate unique ID for this kernel instance
            var configuration = new KernelConfiguration(
                new Dim3(1), // Default grid dimensions
                new Dim3(1)  // Default block dimensions
            );

            return new CompiledKernel(kernelId, _function, 0, configuration);
        }

        /// <summary>
        /// Attempts to retrieve the CudaCompiledKernel from a CompiledKernel struct
        /// </summary>
        public static CudaCompiledKernel? FromCompiledKernel(CompiledKernel kernel)
        {
            lock (_kernelLookup)
            {
                // Use kernel.Id as string key instead of IntPtr
                return _kernelLookup.ContainsKey(kernel.GetHashCode()) ? _kernelLookup[kernel.GetHashCode()] : null;
            }
        }


        private void LoadModule()
        {
            try
            {
                _context.MakeCurrent();

                // Pin PTX data
                var handle = GCHandle.Alloc(_ptxData, GCHandleType.Pinned);
                try
                {
                    var ptxPtr = handle.AddrOfPinnedObject();

                    // Load module from PTX
                    var result = CudaRuntime.cuModuleLoadData(ref _module, ptxPtr);
                    CudaRuntime.CheckError(result, "Module load");

                    // Try to get the mangled function name for proper symbol resolution
                    var actualEntryPoint = _entryPoint;
                    var mangledName = Compilation.CudaKernelCompiler.GetMangledFunctionName(Name, _entryPoint);
                    if (!string.IsNullOrEmpty(mangledName))
                    {
                        actualEntryPoint = mangledName;
                        _logger.LogDebug("Using mangled function name '{MangledName}' for kernel '{Name}' entry point '{EntryPoint}'",
                            mangledName, Name, _entryPoint);
                    }
                    else
                    {
                        _logger.LogDebug("No mangled name found for '{EntryPoint}', using original name for kernel '{Name}'",
                            _entryPoint, Name);
                    }

                    // Get function handle using the appropriate name (mangled or original)
                    result = CudaRuntime.cuModuleGetFunction(ref _function, _module, actualEntryPoint);
                    CudaRuntime.CheckError(result, "Get function");

                    _logger.LogDebug("Successfully loaded CUDA module for kernel '{Name}' with entry point '{EntryPoint}' -> '{ActualEntryPoint}'",
                        Name, _entryPoint, actualEntryPoint);
                }
                finally
                {
                    handle.Free();
                }
            }
            catch (Exception ex)
            {
                // Enhanced error message with debugging information
                var errorMessage = $"Failed to load CUDA module for kernel '{Name}' with entry point '{_entryPoint}'";
                var allMangledNames = Compilation.CudaKernelCompiler.GetAllMangledNames(Name);
                if (allMangledNames != null && allMangledNames.Count > 0)
                {
                    var mangledNamesStr = string.Join(", ", allMangledNames.Select(kvp => $"{kvp.Key} -> {kvp.Value}"));
                    errorMessage += $". Available mangled names: {mangledNamesStr}";
                }
                
                throw new InvalidOperationException(errorMessage, ex);
            }
        }

        public async ValueTask ExecuteAsync(
            KernelArguments arguments,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (arguments.Arguments.Count == 0)
            {
                throw new ArgumentException("No arguments provided for kernel execution", nameof(arguments));
            }

            try
            {
                _logger.LogDebug("Executing CUDA kernel '{Name}' with {ArgCount} arguments", Name, arguments.Arguments.Count);

                // Try to extract launch configuration from arguments metadata
                CudaLaunchConfig? config = null;
                var launchConfigObj = arguments.GetLaunchConfiguration();
                
                if (launchConfigObj != null)
                {
                    _logger.LogDebug("Found launch configuration in arguments metadata: {Config}", launchConfigObj);
                    
                    // Check if it's a LaunchConfiguration type from abstractions
                    if (launchConfigObj is LaunchConfiguration launchConfig)
                    {
                        // Convert LaunchConfiguration to CudaLaunchConfig
                        config = new CudaLaunchConfig(
                            (uint)launchConfig.GridSize.X, 
                            (uint)launchConfig.GridSize.Y, 
                            (uint)launchConfig.GridSize.Z,
                            (uint)launchConfig.BlockSize.X, 
                            (uint)launchConfig.BlockSize.Y, 
                            (uint)launchConfig.BlockSize.Z,
                            (uint)launchConfig.SharedMemoryBytes);
                        
                        _logger.LogDebug("Converted LaunchConfiguration to CudaLaunchConfig: Grid({GridX},{GridY},{GridZ}) Block({BlockX},{BlockY},{BlockZ}) SharedMem={SharedMem}",
                            config.Value.GridX, config.Value.GridY, config.Value.GridZ,
                            config.Value.BlockX, config.Value.BlockY, config.Value.BlockZ,
                            config.Value.SharedMemoryBytes);
                    }
                    else if (launchConfigObj is CudaLaunchConfig cudaConfig)
                    {
                        // Already a CudaLaunchConfig
                        config = cudaConfig;
                        _logger.LogDebug("Using CudaLaunchConfig directly: Grid({GridX},{GridY},{GridZ}) Block({BlockX},{BlockY},{BlockZ}) SharedMem={SharedMem}",
                            config.Value.GridX, config.Value.GridY, config.Value.GridZ,
                            config.Value.BlockX, config.Value.BlockY, config.Value.BlockZ,
                            config.Value.SharedMemoryBytes);
                    }
                }

                // Use the advanced launcher for optimal performance
                await _launcher.LaunchKernelAsync(_function, arguments, config, cancellationToken).ConfigureAwait(false);

                _logger.LogDebug("Successfully executed CUDA kernel '{Name}'", Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute CUDA kernel '{Name}'", Name);
                throw new InvalidOperationException($"Failed to execute CUDA kernel '{Name}'", ex);
            }
        }

        /// <summary>
        /// Executes the kernel with custom launch configuration
        /// </summary>
        public async ValueTask ExecuteWithConfigAsync(
            KernelArguments arguments,
            CudaLaunchConfig config,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (arguments.Arguments.Count == 0)
            {
                throw new ArgumentException("No arguments provided for kernel execution", nameof(arguments));
            }

            // Validate the launch configuration
            if (!_launcher.ValidateLaunchConfig(config))
            {
                throw new ArgumentException("Invalid launch configuration for this device", nameof(config));
            }

            try
            {
                _logger.LogDebug("Executing CUDA kernel '{Name}' with custom configuration", Name);

                await _launcher.LaunchKernelAsync(_function, arguments, config, cancellationToken).ConfigureAwait(false);

                _logger.LogDebug("Successfully executed CUDA kernel '{Name}' with custom config", Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute CUDA kernel '{Name}' with custom config", Name);
                throw new InvalidOperationException($"Failed to execute CUDA kernel '{Name}' with custom config", ex);
            }
        }

        /// <summary>
        /// Gets optimal launch configuration for the specified problem size
        /// </summary>
        public CudaLaunchConfig GetOptimalLaunchConfig(int totalElements)
        {
            ThrowIfDisposed();
            return _launcher.GetOptimalConfigFor1D(totalElements);
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (_module != IntPtr.Zero)
                {
                    await Task.Run(() =>
                    {
                        _context.MakeCurrent();
                        _ = CudaRuntime.cuModuleUnload(_module);
                        _module = IntPtr.Zero;
                        _function = IntPtr.Zero;
                    }).ConfigureAwait(false);
                }

                _disposed = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during CUDA compiled kernel disposal");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                try
                {
                    if (_module != IntPtr.Zero)
                    {
                        _context.MakeCurrent();
                        _ = CudaRuntime.cuModuleUnload(_module);
                        _module = IntPtr.Zero;
                        _function = IntPtr.Zero;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error unloading CUDA module");
                }
            }

            // Remove from lookup table
            lock (_lookupLock)
            {
                _ = _kernelLookup.Remove(_function);
            }

            _disposed = true;
        }
    }
}
