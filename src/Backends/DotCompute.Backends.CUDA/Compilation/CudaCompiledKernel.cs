// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Native;
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
        /// Gets the CUDA function handle for this kernel
        /// </summary>
        public IntPtr FunctionHandle => _function;

        private readonly string _entryPoint;
        private static readonly Dictionary<IntPtr, CudaCompiledKernel> _kernelLookup = new();
        private readonly object _lookupLock = new();

        public CudaCompiledKernel(
            CudaContext context,
            string name,
            string entryPoint,
            byte[] ptxData,
            CompilationOptions? options,
            ILogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ptxData = ptxData ?? throw new ArgumentNullException(nameof(ptxData));

            Name = name ?? throw new ArgumentNullException(nameof(name));
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

                    // Get function handle
                    result = CudaRuntime.cuModuleGetFunction(ref _function, _module, _entryPoint);
                    CudaRuntime.CheckError(result, "Get function");

                    _logger.LogDebug("Loaded CUDA module for kernel '{Name}' with entry point '{EntryPoint}'",
                        Name, _entryPoint);
                }
                finally
                {
                    handle.Free();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load CUDA module for kernel '{Name}'", ex);
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

                // Use the advanced launcher for optimal performance
                await _launcher.LaunchKernelAsync(_function, arguments, null, cancellationToken).ConfigureAwait(false);

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
