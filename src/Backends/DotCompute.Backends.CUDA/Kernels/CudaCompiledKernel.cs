// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;

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
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>

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
        /// <summary>
        /// Initializes a new instance of the CudaCompiledKernel class.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="name">The name.</param>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="ptxData">The ptx data.</param>
        /// <param name="options">The options.</param>
        /// <param name="logger">The logger.</param>

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

                    // CRITICAL FIX: Enhanced module loading for CUDA 13.0 compatibility
                    // Try progressive fallback strategy for optimal compatibility
                    var loadingStrategy = "unknown";

                    // Strategy 1: JIT options with fallback configurations

                    if (TryLoadModuleWithJitOptions(ptxPtr, out var result))
                    {
                        loadingStrategy = "JIT optimized";
                    }
                    // Strategy 2: Standard module loading (cuModuleLoadData)
                    else
                    {
                        _logger.LogInfoMessage($"JIT module loading failed with {result}, using standard loading for kernel '{Name}'");


                        result = CudaRuntime.cuModuleLoadData(ref _module, ptxPtr);
                        if (result == CudaError.Success)
                        {
                            loadingStrategy = "standard";
                        }
                    }

                    // Check if any loading strategy succeeded

                    if (result != CudaError.Success)
                    {
                        // Enhanced error reporting for CUDA 13.0 troubleshooting
                        var computeCapability = CudaCapabilityManager.GetTargetComputeCapability();
                        var ptxVersion = CudaCapabilityManager.GetCompatiblePtxVersion(computeCapability);


                        var errorDetails = $"CUDA module loading failed for kernel '{Name}' with error: {result}. " +
                            $"Target compute capability: sm_{computeCapability.major}{computeCapability.minor}, " +
                            $"PTX version: {ptxVersion}, " +
                            $"PTX size: {_ptxData.Length} bytes";


                        throw new InvalidOperationException(errorDetails);
                    }


                    _logger.LogInfoMessage($"CUDA module loaded successfully using '{loadingStrategy}' strategy for kernel '{Name}'");

                    // Enhanced function symbol resolution with multiple fallback strategies
                    if (!TryResolveKernelFunction())
                    {
                        throw new InvalidOperationException(
                            $"Failed to resolve kernel function for '{Name}' with entry point '{_entryPoint}'");
                    }
                }
                finally
                {
                    handle.Free();
                }
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                // Enhanced error message with comprehensive debugging information
                var errorMessage = BuildDetailedErrorMessage(ex);
                throw new InvalidOperationException(errorMessage, ex);
            }
        }

        /// <summary>
        /// Attempts to resolve the kernel function using multiple naming strategies
        /// </summary>
        private bool TryResolveKernelFunction()
        {
            // Strategy 1: Try mangled name first (most reliable for C++ kernels)
            var mangledName = CudaKernelCompiler.GetMangledFunctionName(Name, _entryPoint);
            if (!string.IsNullOrEmpty(mangledName))
            {
                var result = CudaRuntime.cuModuleGetFunction(ref _function, _module, mangledName);
                if (result == CudaError.Success)
                {
                    _logger.LogInfoMessage($"Resolved kernel function using mangled name '{mangledName}' for '{Name}'");
                    return true;
                }


                _logger.LogDebugMessage("");
            }

            // Strategy 2: Try original entry point name
            var originalResult = CudaRuntime.cuModuleGetFunction(ref _function, _module, _entryPoint);
            if (originalResult == CudaError.Success)
            {
                _logger.LogInfoMessage($"Resolved kernel function using original name '{_entryPoint}' for '{Name}'");
                return true;
            }


            _logger.LogDebugMessage("");

            // Strategy 3: Try common naming variations for CUDA kernels
            var namingVariations = new[]
            {
                $"_Z{_entryPoint.Length}{_entryPoint}v", // Simple mangling pattern
                $"extern_{_entryPoint}",                 // extern "C" prefix
                _entryPoint.Replace("kernel_", ""),      // Remove kernel_ prefix
                _entryPoint + "_kernel"                  // Add kernel suffix
            };

            foreach (var variation in namingVariations)
            {
                var result = CudaRuntime.cuModuleGetFunction(ref _function, _module, variation);
                if (result == CudaError.Success)
                {
                    _logger.LogInfoMessage($"Resolved kernel function using naming variation '{variation}' for '{Name}'");
                    return true;
                }


                _logger.LogDebugMessage("");
            }

            return false;
        }

        /// <summary>
        /// Builds detailed error message with comprehensive debugging information
        /// </summary>
        private string BuildDetailedErrorMessage(Exception originalException)
        {
            var errorMessage = $"Failed to load CUDA module for kernel '{Name}' with entry point '{_entryPoint}': {originalException.Message}";


            try
            {
                // Add available mangled names information
                var allMangledNames = CudaKernelCompiler.GetAllMangledNames(Name);
                if (allMangledNames != null && allMangledNames.Count > 0)
                {
                    var mangledNamesStr = string.Join(", ", allMangledNames.Select(kvp => $"{kvp.Key} -> {kvp.Value}"));
                    errorMessage += $"\nAvailable mangled names: {mangledNamesStr}";
                }

                // Add compute capability information
                var computeCapability = CudaCapabilityManager.GetTargetComputeCapability();
                var ptxVersion = CudaCapabilityManager.GetCompatiblePtxVersion(computeCapability);
                errorMessage += $"\nCompute capability: sm_{computeCapability.major}{computeCapability.minor}";
                errorMessage += $"\nPTX version: {ptxVersion}";
                errorMessage += $"\nModule size: {_ptxData.Length} bytes";

                // Add CUDA 13.0 specific troubleshooting guidance
                errorMessage += "\nCUDA 13.0 Troubleshooting:";
                errorMessage += "\n- Ensure PTX compilation is used for Ada Lovelace (sm_89) architectures";
                errorMessage += "\n- Verify driver compatibility (CUDA 13.0 + driver 581.15)";
                errorMessage += "\n- Check that kernel functions use extern \"C\" linkage";
                errorMessage += "\n- Consider using conservative PTX versions for better compatibility";
            }
            catch
            {
                // If we can't get debugging info, just return basic error message
                errorMessage += "\n(Unable to gather additional debugging information)";
            }


            return errorMessage;
        }

        /// <summary>
        /// Attempts to load module with JIT options for better CUDA 13.0 compatibility
        /// Uses progressive fallback strategy with different optimization levels
        /// </summary>
        private bool TryLoadModuleWithJitOptions(IntPtr ptxPtr, out CudaError result)
        {
            // Try multiple JIT configurations in order of preference for CUDA 13.0
            var jitConfigurations = new[]
            {
                // Configuration 1: Optimal for CUDA 13.0 with RTX 2000 Ada
                new JitConfiguration
                {
                    OptimizationLevel = 3, // O3 - maximum optimization for Ada architecture
                    GenerateDebugInfo = 0,
                    GenerateLineInfo = 1,  // Line info helps with debugging without perf cost
                    LogVerbose = 0,
                    MaxRegisters = 64,     // Optimal for Ada Lovelace
                    Description = "Ada Lovelace optimized"
                },

                // Configuration 2: Conservative fallback
                new JitConfiguration
                {
                    OptimizationLevel = 2, // O2 - balanced optimization
                    GenerateDebugInfo = 0,
                    GenerateLineInfo = 0,
                    LogVerbose = 0,
                    MaxRegisters = 32,     // Conservative register usage
                    Description = "Conservative balanced"
                },

                // Configuration 3: Minimal safe options
                new JitConfiguration
                {
                    OptimizationLevel = 1, // O1 - minimal optimization
                    GenerateDebugInfo = 0,
                    GenerateLineInfo = 0,
                    LogVerbose = 0,
                    MaxRegisters = 0,      // Let compiler decide
                    Description = "Minimal safe"
                }
            };

            foreach (var config in jitConfigurations)
            {
                if (TryLoadWithSpecificJitConfig(ptxPtr, config, out result))
                {
                    return true;
                }


                _logger.LogDebugMessage($"JIT configuration '{config.Description}' failed with {result}, trying next");
            }

            result = CudaError.Unknown;
            return false;
        }

        /// <summary>
        /// JIT configuration for CUDA module loading
        /// </summary>
        private class JitConfiguration
        {
            /// <summary>
            /// Gets or sets the optimization level.
            /// </summary>
            /// <value>The optimization level.</value>
            public int OptimizationLevel { get; init; }
            /// <summary>
            /// Gets or sets the generate debug info.
            /// </summary>
            /// <value>The generate debug info.</value>
            public int GenerateDebugInfo { get; init; }
            /// <summary>
            /// Gets or sets the generate line info.
            /// </summary>
            /// <value>The generate line info.</value>
            public int GenerateLineInfo { get; init; }
            /// <summary>
            /// Gets or sets the log verbose.
            /// </summary>
            /// <value>The log verbose.</value>
            public int LogVerbose { get; init; }
            /// <summary>
            /// Gets or sets the max registers.
            /// </summary>
            /// <value>The max registers.</value>
            public int MaxRegisters { get; init; }
            /// <summary>
            /// Gets or sets the description.
            /// </summary>
            /// <value>The description.</value>
            public string Description { get; init; } = string.Empty;
        }

        /// <summary>
        /// Attempts to load module with a specific JIT configuration
        /// </summary>
        private bool TryLoadWithSpecificJitConfig(IntPtr ptxPtr, JitConfiguration config, out CudaError result)
        {
            try
            {
                var jitOptions = new List<CUjit_option>
                {
                    CUjit_option.CU_JIT_OPTIMIZATION_LEVEL,
                    CUjit_option.CU_JIT_GENERATE_DEBUG_INFO,
                    CUjit_option.CU_JIT_GENERATE_LINE_INFO,
                    CUjit_option.CU_JIT_LOG_VERBOSE
                };

                var jitOptionValues = new List<IntPtr>
                {
                    new(config.OptimizationLevel),
                    new(config.GenerateDebugInfo),
                    new(config.GenerateLineInfo),
                    new(config.LogVerbose)
                };

                // Add max registers option if specified
                if (config.MaxRegisters > 0)
                {
                    jitOptions.Add(CUjit_option.CU_JIT_MAX_REGISTERS);
                    jitOptionValues.Add(new IntPtr(config.MaxRegisters));
                }

                // Pin the options arrays
                var optionsArray = jitOptions.ToArray();
                var valuesArray = jitOptionValues.ToArray();
                var optionsHandle = GCHandle.Alloc(optionsArray, GCHandleType.Pinned);
                var valuesHandle = GCHandle.Alloc(valuesArray, GCHandleType.Pinned);

                try
                {
                    result = CudaRuntime.cuModuleLoadDataEx(
                        ref _module,
                        ptxPtr,
                        (uint)optionsArray.Length,
                        optionsHandle.AddrOfPinnedObject(),
                        valuesHandle.AddrOfPinnedObject());

                    if (result == CudaError.Success)
                    {
                        _logger.LogInformation(
                            "Successfully loaded module using JIT configuration '{Description}' " +
                            "(O{OptLevel}, {MaxRegs} max registers) for kernel '{Name}'",

                            config.Description, config.OptimizationLevel,

                            config.MaxRegisters > 0 ? config.MaxRegisters.ToString() : "auto", Name);
                        return true;
                    }


                    return false;
                }
                finally
                {
                    optionsHandle.Free();
                    valuesHandle.Free();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Exception during JIT configuration '{Description}' for kernel '{Name}'",

                    config.Description, Name);
                result = CudaError.Unknown;
                return false;
            }
        }
        /// <summary>
        /// Gets execute asynchronously.
        /// </summary>
        /// <param name="arguments">The arguments.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

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
                _logger.LogDebugMessage(" arguments");

                // Try to extract launch configuration from arguments metadata
                CudaLaunchConfig? config = null;
                var launchConfigObj = arguments.GetLaunchConfiguration();


                if (launchConfigObj != null)
                {
                    _logger.LogDebugMessage("");

                    // Check if it's a KernelLaunchConfiguration type from abstractions

                    if (launchConfigObj is KernelLaunchConfiguration launchConfig)
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


                        _logger.LogDebugMessage($"Converted LaunchConfiguration to CudaLaunchConfig: Grid({config.Value.GridX},{config.Value.GridY},{config.Value.GridZ}) Block({config.Value.BlockX},{config.Value.BlockY},{config.Value.BlockZ}) SharedMem={config.Value.SharedMemoryBytes}");
                    }
                    // Note: Direct CudaLaunchConfig matching removed since we use KernelLaunchConfiguration
                    else
                    {
                        _logger.LogWarningMessage($"");
                        config = null;
                    }
                }

                // Use the advanced launcher for optimal performance
                await _launcher.LaunchKernelAsync(_function, arguments, config, cancellationToken).ConfigureAwait(false);

                _logger.LogDebugMessage("'");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage("'");
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
                _logger.LogDebugMessage("' with custom configuration");

                await _launcher.LaunchKernelAsync(_function, arguments, config, cancellationToken).ConfigureAwait(false);

                _logger.LogDebugMessage("' with custom config");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage("' with custom config");
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
        /// <summary>
        /// Gets dispose asynchronously.
        /// </summary>
        /// <returns>The result of the operation.</returns>

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
                _logger.LogErrorMessage(ex, "Error during CUDA compiled kernel disposal");
            }
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

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
                    _logger.LogErrorMessage(ex, "Error unloading CUDA module");
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
