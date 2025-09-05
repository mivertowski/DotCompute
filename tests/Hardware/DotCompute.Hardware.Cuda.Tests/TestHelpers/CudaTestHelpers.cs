// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests.TestHelpers
{
    /// <summary>
    /// Helper class for CUDA tests providing simplified access to CUDA components.
    /// </summary>
    public static class CudaTestHelpers
    {
        /// <summary>
        /// Creates a test CUDA accelerator with optional production features.
        /// </summary>
        public static (IAccelerator accelerator, CudaContext context, CudaMemoryManager memoryManager) CreateTestAccelerator(
            int deviceId = 0,
            ILogger? logger = null)
        {
            var factory = new CudaAcceleratorFactory(logger as ILogger<CudaAcceleratorFactory>);
            var productionAccelerator = factory.CreateProductionAccelerator(deviceId);
            
            // For production accelerator, we can't directly access internal components
            // Return null for context and memory manager as they're encapsulated
            CudaContext context = null;
            CudaMemoryManager memoryManager = null;
            
            return (productionAccelerator, context, memoryManager);
        }

        /// <summary>
        /// Creates a standalone CUDA context for testing.
        /// </summary>
        public static CudaContext CreateTestContext(int deviceId = 0)
        {
            return new CudaContext(deviceId);
        }

        /// <summary>
        /// Creates a test kernel compiler.
        /// </summary>
        public static CudaKernelCompiler CreateTestCompiler(CudaContext context, ILogger? logger = null)
        {
            return new CudaKernelCompiler(context, logger ?? NullLogger.Instance);
        }

        /// <summary>
        /// Creates a test kernel cache.
        /// </summary>
        public static CudaKernelCache CreateTestKernelCache(ILogger? logger = null)
        {
            var cacheLogger = logger as ILogger<CudaKernelCache> ?? new NullLogger<CudaKernelCache>();
            return new CudaKernelCache(cacheLogger);
        }

        /// <summary>
        /// Creates a test memory manager.
        /// </summary>
        public static CudaMemoryManager CreateTestMemoryManager(CudaContext context, ILogger? logger = null)
        {
            var device = new CudaDevice(context.DeviceId, logger ?? NullLogger.Instance);
            return new CudaMemoryManager(context, device, logger ?? NullLogger.Instance);
        }

        /// <summary>
        /// Checks if CUDA is available on the system.
        /// </summary>
        public static bool IsCudaAvailable()
        {
            try
            {
                // Use CUDA runtime check instead
                int deviceCount = 0;
                var result = CudaRuntime.cudaGetDeviceCount(out deviceCount);
                return result == CudaError.Success && deviceCount > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a kernel definition for testing.
        /// </summary>
        public static KernelDefinition CreateTestKernelDefinition(
            string name,
            string code,
            KernelLanguage language = KernelLanguage.CUDA)
        {
            return new KernelDefinition
            {
                Name = name,
                Code = code
            };
        }

        /// <summary>
        /// Creates CUDA compilation options with common test settings.
        /// </summary>
        public static CudaCompilationOptions CreateTestCompilationOptions(
            CudaOptimizationLevel optimization = CudaOptimizationLevel.O2,
            bool enableRegisterSpilling = false)
        {
            return new CudaCompilationOptions
            {
                OptimizationLevel = (DotCompute.Abstractions.Types.OptimizationLevel)(int)optimization,
                GenerateDebugInfo = false,
                MaxRegistersPerThread = enableRegisterSpilling ? 32 : 64
            };
        }

        /// <summary>
        /// Creates a test kernel launcher.
        /// </summary>
        public static CudaKernelLauncher CreateTestLauncher(CudaContext context, ILogger? logger = null)
        {
            return new CudaKernelLauncher(context, logger ?? NullLogger.Instance);
        }

        /// <summary>
        /// Helper for creating kernel launch configuration.
        /// </summary>
        public static ((int x, int y, int z) grid, (int x, int y, int z) block) CreateLaunchConfig(
            int gridX = 1, int gridY = 1, int gridZ = 1,
            int blockX = 256, int blockY = 1, int blockZ = 1)
        {
            return (
                (gridX, gridY, gridZ),
                (blockX, blockY, blockZ)
            );
        }
        
        /// <summary>
        /// Creates a kernel arguments instance with proper grid and block dimensions.
        /// </summary>
        public static KernelArguments CreateKernelArguments(
            object[] arguments,
            (int x, int y, int z)? gridDim = null,
            (int x, int y, int z)? blockDim = null)
        {
            var kernelArgs = new KernelArguments();
            
            // Add actual arguments
            foreach (var arg in arguments)
            {
                kernelArgs.Add(arg);
            }
            
            // Set grid dimensions using extension methods if provided
            if (gridDim.HasValue)
            {
                kernelArgs.SetGridDimensions(gridDim.Value);
            }
            
            // Set block dimensions using extension methods if provided
            if (blockDim.HasValue)
            {
                kernelArgs.SetBlockDimensions(blockDim.Value);
            }
            
            return kernelArgs;
        }
    }

    /// <summary>
    /// Kernel language enumeration for test kernels.
    /// </summary>
    public enum KernelLanguage
    {
        CUDA,
        PTX,
        NVVM
    }

    /// <summary>
    /// CUDA optimization levels.
    /// </summary>
    public enum CudaOptimizationLevel
    {
        O0 = 0,
        O1 = 1,
        O2 = 2,
        O3 = 3
    }

    /// <summary>
    /// Extension methods for CUDA tests.
    /// </summary>
    public static class CudaTestExtensions
    {
        /// <summary>
        /// Gets the architecture generation string for an accelerator.
        /// </summary>
        public static string GetArchitectureGeneration(this AcceleratorInfo info)
        {
            // Parse compute capability from the device name or other properties
            // This is a simplified implementation - adjust based on actual requirements
            if (info.Name.Contains("Hopper") || info.Name.Contains("H100"))
                return "Hopper";
            if (info.Name.Contains("Ada") || info.Name.Contains("RTX 40"))
                return "Ada Lovelace";
            if (info.Name.Contains("Ampere") || info.Name.Contains("RTX 30") || info.Name.Contains("A100"))
                return "Ampere";
            if (info.Name.Contains("Turing") || info.Name.Contains("RTX 20") || info.Name.Contains("T4"))
                return "Turing";
            if (info.Name.Contains("Volta") || info.Name.Contains("V100"))
                return "Volta";
            if (info.Name.Contains("Pascal") || info.Name.Contains("GTX 10") || info.Name.Contains("P100"))
                return "Pascal";
            if (info.Name.Contains("Maxwell"))
                return "Maxwell";
            
            return "Unknown";
        }

        /// <summary>
        /// Converts a ProductionCudaAccelerator to a CudaAccelerator for tests that need direct access.
        /// </summary>
        public static CudaAccelerator? AsCudaAccelerator(this IAccelerator accelerator)
        {
            // Since ProductionCudaAccelerator wraps CudaAccelerator internally,
            // we need to handle this specially for tests
            if (accelerator is CudaAcceleratorFactory.ProductionCudaAccelerator production)
            {
                // Return null since we can't directly access the internal CudaAccelerator
                // Tests should use the production accelerator directly via IAccelerator interface
                return null;
            }
            
            return accelerator as CudaAccelerator;
        }

        /// <summary>
        /// Gets the CUDA context from an accelerator.
        /// </summary>
        public static CudaContext? GetCudaContext(this IAccelerator accelerator)
        {
            // ProductionCudaAccelerator doesn't expose StreamManager.Context directly
            // Return null as context is encapsulated
            if (accelerator is CudaAcceleratorFactory.ProductionCudaAccelerator production)
            {
                return null;
            }
            
            // Check if the accelerator context can be cast to CudaContext
            // This won't work with ProductionCudaAccelerator as it uses AcceleratorContext
            // var context = accelerator.Context as CudaContext;
            // if (context != null)
            // {
            //     return context;
            // }
            
            return null;
        }
    }
    
    /// <summary>
    /// Test logger implementation for xUnit output.
    /// </summary>
    public class TestLogger : ILogger
    {
        private readonly Xunit.Abstractions.ITestOutputHelper _output;

        public TestLogger(Xunit.Abstractions.ITestOutputHelper output)
        {
            _output = output;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, 
            Exception? exception, Func<TState, Exception?, string> formatter)
            where TState : notnull
        {
            var message = formatter(state, exception);
            _output.WriteLine($"[{logLevel}] {message}");
            if (exception != null)
            {
                _output.WriteLine($"Exception: {exception}");
            }
        }
    }
}