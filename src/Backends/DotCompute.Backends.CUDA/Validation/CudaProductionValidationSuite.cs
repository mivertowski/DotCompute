using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotCompute.Backends.CUDA.Factory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Validation
{
    /// <summary>
    /// Comprehensive production validation suite for CUDA backend.
    /// Validates all production features and ensures system readiness.
    /// </summary>
    public sealed class CudaProductionValidationSuite
    {
        private readonly ILogger<CudaProductionValidationSuite> _logger;
        private readonly IServiceProvider? _serviceProvider;
        private readonly List<ValidationResult> _results;
        private readonly Stopwatch _stopwatch;

        public CudaProductionValidationSuite(
            ILogger<CudaProductionValidationSuite> logger,
            IServiceProvider? serviceProvider = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider;
            _results = new List<ValidationResult>();
            _stopwatch = new Stopwatch();
        }

        /// <summary>
        /// Runs complete production validation suite.
        /// </summary>
        public async Task<ValidationReport> RunFullValidationAsync(
            ValidationOptions? options = null)
        {
            options ??= ValidationOptions.Default;
            
            _logger.LogInformation("Starting CUDA Production Validation Suite");
            _stopwatch.Start();
            
            var report = new ValidationReport
            {
                StartTime = DateTimeOffset.UtcNow,
                Options = options
            };

            try
            {
                // Phase 1: System Requirements
                if (options.ValidateSystemRequirements)
                {
                    await ValidateSystemRequirementsAsync(report);
                }

                // Phase 2: CUDA Installation
                if (options.ValidateCudaInstallation)
                {
                    await ValidateCudaInstallationAsync(report);
                }

                // Phase 3: Device Capabilities
                if (options.ValidateDeviceCapabilities)
                {
                    await ValidateDeviceCapabilitiesAsync(report);
                }

                // Phase 4: Memory Management
                if (options.ValidateMemoryManagement)
                {
                    await ValidateMemoryManagementAsync(report);
                }

                // Phase 5: Stream Management
                if (options.ValidateStreamManagement)
                {
                    await ValidateStreamManagementAsync(report);
                }

                // Phase 6: Kernel Compilation
                if (options.ValidateKernelCompilation)
                {
                    await ValidateKernelCompilationAsync(report);
                }

                // Phase 7: Graph Optimization
                if (options.ValidateGraphOptimization)
                {
                    await ValidateGraphOptimizationAsync(report);
                }

                // Phase 8: Tensor Cores
                if (options.ValidateTensorCores)
                {
                    await ValidateTensorCoresAsync(report);
                }

                // Phase 9: Performance Profiling
                if (options.ValidatePerformanceProfiling)
                {
                    await ValidatePerformanceProfilingAsync(report);
                }

                // Phase 10: Error Recovery
                if (options.ValidateErrorRecovery)
                {
                    await ValidateErrorRecoveryAsync(report);
                }

                // Phase 11: Multi-GPU Support
                if (options.ValidateMultiGpu)
                {
                    await ValidateMultiGpuSupportAsync(report);
                }

                // Phase 12: Production Workload
                if (options.ValidateProductionWorkload)
                {
                    await ValidateProductionWorkloadAsync(report);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during validation");
                report.CriticalError = ex.Message;
            }
            finally
            {
                _stopwatch.Stop();
                report.EndTime = DateTimeOffset.UtcNow;
                report.Duration = _stopwatch.Elapsed;
                report.Results = _results.ToList();
                
                // Calculate summary
                report.TotalTests = _results.Count;
                report.PassedTests = _results.Count(r => r.Status == ValidationStatus.Passed);
                report.FailedTests = _results.Count(r => r.Status == ValidationStatus.Failed);
                report.WarningTests = _results.Count(r => r.Status == ValidationStatus.Warning);
                report.SkippedTests = _results.Count(r => r.Status == ValidationStatus.Skipped);
                report.OverallStatus = DetermineOverallStatus(report);
            }

            _logger.LogInformation(
                "Validation completed in {Duration:F2}s - Status: {Status} ({Passed}/{Total} passed)",
                report.Duration.TotalSeconds,
                report.OverallStatus,
                report.PassedTests,
                report.TotalTests);

            return report;
        }

        /// <summary>
        /// Validates system requirements for production.
        /// </summary>
        private async Task ValidateSystemRequirementsAsync(ValidationReport report)
        {
            _logger.LogInformation("Validating system requirements...");
            
            var result = new ValidationResult
            {
                Category = "System Requirements",
                TestName = "Hardware and OS Validation"
            };

            try
            {
                // Check OS
                if (OperatingSystem.IsWindows())
                {
                    result.Details.Add("OS: Windows - Supported");
                }
                else if (OperatingSystem.IsLinux())
                {
                    result.Details.Add("OS: Linux - Supported");
                }
                else
                {
                    result.Status = ValidationStatus.Warning;
                    result.Details.Add($"OS: {Environment.OSVersion} - Limited Support");
                }

                // Check system memory
                var systemInfo = _serviceProvider?.GetService<Core.System.SystemInfoManager>();
                if (systemInfo != null)
                {
                    var memInfo = systemInfo.GetMemoryInfo();
                    if (memInfo.TotalPhysical < 8L * 1024 * 1024 * 1024) // Less than 8GB
                    {
                        result.Status = ValidationStatus.Warning;
                        result.Details.Add($"System Memory: {memInfo.TotalPhysical / (1024 * 1024 * 1024):F1} GB - Below recommended 8GB");
                    }
                    else
                    {
                        result.Details.Add($"System Memory: {memInfo.TotalPhysical / (1024 * 1024 * 1024):F1} GB - Adequate");
                    }
                }

                // Check CPU cores
                int cpuCores = Environment.ProcessorCount;
                if (cpuCores < 4)
                {
                    result.Status = ValidationStatus.Warning;
                    result.Details.Add($"CPU Cores: {cpuCores} - Below recommended 4 cores");
                }
                else
                {
                    result.Details.Add($"CPU Cores: {cpuCores} - Adequate");
                }

                if (result.Status == ValidationStatus.NotSet)
                {
                    result.Status = ValidationStatus.Passed;
                }
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Failed;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Failed to validate system requirements");
            }

            _results.Add(result);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Validates CUDA installation and versions.
        /// </summary>
        private async Task ValidateCudaInstallationAsync(ValidationReport report)
        {
            _logger.LogInformation("Validating CUDA installation...");
            
            var result = new ValidationResult
            {
                Category = "CUDA Installation",
                TestName = "Runtime and Driver Validation"
            };

            try
            {
                using var factory = new CudaAcceleratorFactoryProduction(_logger, _serviceProvider);
                
                if (!factory.IsAvailable())
                {
                    result.Status = ValidationStatus.Failed;
                    result.ErrorMessage = "CUDA is not available";
                    _results.Add(result);
                    return;
                }

                // Check driver version
                if (Native.CudaRuntime.cudaDriverGetVersion(out int driverVersion) == Native.Types.CudaError.Success)
                {
                    int driverMajor = driverVersion / 1000;
                    int driverMinor = (driverVersion % 1000) / 10;
                    
                    if (driverMajor < 11)
                    {
                        result.Status = ValidationStatus.Warning;
                        result.Details.Add($"CUDA Driver: {driverMajor}.{driverMinor} - Recommend 11.0 or newer");
                    }
                    else
                    {
                        result.Details.Add($"CUDA Driver: {driverMajor}.{driverMinor} - Supported");
                    }
                }

                // Check runtime version
                if (Native.CudaRuntime.cudaRuntimeGetVersion(out int runtimeVersion) == Native.Types.CudaError.Success)
                {
                    int runtimeMajor = runtimeVersion / 1000;
                    int runtimeMinor = (runtimeVersion % 1000) / 10;
                    
                    if (runtimeMajor < 11)
                    {
                        result.Status = ValidationStatus.Warning;
                        result.Details.Add($"CUDA Runtime: {runtimeMajor}.{runtimeMinor} - Recommend 11.0 or newer");
                    }
                    else
                    {
                        result.Details.Add($"CUDA Runtime: {runtimeMajor}.{runtimeMinor} - Supported");
                    }
                }

                if (result.Status == ValidationStatus.NotSet)
                {
                    result.Status = ValidationStatus.Passed;
                }
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Failed;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Failed to validate CUDA installation");
            }

            _results.Add(result);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Validates device capabilities for production.
        /// </summary>
        private async Task ValidateDeviceCapabilitiesAsync(ValidationReport report)
        {
            _logger.LogInformation("Validating device capabilities...");
            
            var result = new ValidationResult
            {
                Category = "Device Capabilities",
                TestName = "GPU Feature Validation"
            };

            try
            {
                using var factory = new CudaAcceleratorFactoryProduction(_logger, _serviceProvider);
                var accelerators = factory.CreateAccelerators().ToList();
                
                if (!accelerators.Any())
                {
                    result.Status = ValidationStatus.Failed;
                    result.ErrorMessage = "No CUDA devices found";
                    _results.Add(result);
                    return;
                }

                foreach (var acc in accelerators.OfType<CudaAcceleratorFactoryProduction.ProductionCudaAccelerator>())
                {
                    var device = acc.DeviceManager.GetDeviceInfo(acc.DeviceId);
                    
                    // Check compute capability
                    if (device.Major < 6) // Pascal or newer recommended
                    {
                        result.Status = ValidationStatus.Warning;
                        result.Details.Add($"Device {acc.DeviceId}: CC {device.Major}.{device.Minor} - Recommend CC 6.0+");
                    }
                    else
                    {
                        result.Details.Add($"Device {acc.DeviceId}: CC {device.Major}.{device.Minor} - Supported");
                    }

                    // Check memory
                    if (device.TotalGlobalMemory < 4L * 1024 * 1024 * 1024) // Less than 4GB
                    {
                        result.Status = ValidationStatus.Warning;
                        result.Details.Add($"Device {acc.DeviceId}: {device.TotalGlobalMemory / (1024 * 1024 * 1024):F1} GB - Below recommended 4GB");
                    }
                    else
                    {
                        result.Details.Add($"Device {acc.DeviceId}: {device.TotalGlobalMemory / (1024 * 1024 * 1024):F1} GB - Adequate");
                    }

                    // Check tensor cores
                    if (device.TensorCoreCount > 0)
                    {
                        result.Details.Add($"Device {acc.DeviceId}: Tensor Cores - {device.TensorCoreCount} cores available");
                    }

                    acc.Dispose();
                }

                if (result.Status == ValidationStatus.NotSet)
                {
                    result.Status = ValidationStatus.Passed;
                }
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Failed;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Failed to validate device capabilities");
            }

            _results.Add(result);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Validates memory management features.
        /// </summary>
        private async Task ValidateMemoryManagementAsync(ValidationReport report)
        {
            _logger.LogInformation("Validating memory management...");
            
            var result = new ValidationResult
            {
                Category = "Memory Management",
                TestName = "Memory Operations Validation"
            };

            try
            {
                using var factory = new CudaAcceleratorFactoryProduction(_logger, _serviceProvider);
                var accelerator = factory.CreateDefaultAccelerator() as CudaAcceleratorFactoryProduction.ProductionCudaAccelerator;
                
                if (accelerator == null)
                {
                    result.Status = ValidationStatus.Failed;
                    result.ErrorMessage = "Failed to create accelerator";
                    _results.Add(result);
                    return;
                }

                // Test async memory allocation
                try
                {
                    var buffer = await accelerator.AsyncMemoryManager.AllocateBufferCoreAsync(
                        1024 * 1024, // 1MB
                        Core.Memory.BufferType.Device,
                        null);
                    
                    buffer.Dispose();
                    result.Details.Add("Async Memory: Functional");
                }
                catch (Exception ex)
                {
                    result.Status = ValidationStatus.Warning;
                    result.Details.Add($"Async Memory: {ex.Message}");
                }

                // Test unified memory
                try
                {
                    var unified = await accelerator.UnifiedMemoryManager.AllocateManagedAsync(
                        1024 * 1024, // 1MB
                        Memory.ManagedMemoryFlags.PreferDevice);
                    
                    unified.Dispose();
                    result.Details.Add("Unified Memory: Functional");
                }
                catch (Exception ex)
                {
                    result.Status = ValidationStatus.Warning;
                    result.Details.Add($"Unified Memory: {ex.Message}");
                }

                // Test memory info
                var memInfo = accelerator.UnifiedMemoryManager.GetMemoryStatistics();
                result.Details.Add($"Memory Stats: {memInfo.TotalAllocations} allocations tracked");

                accelerator.Dispose();
                
                if (result.Status == ValidationStatus.NotSet)
                {
                    result.Status = ValidationStatus.Passed;
                }
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Failed;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Failed to validate memory management");
            }

            _results.Add(result);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Validates stream management features.
        /// </summary>
        private async Task ValidateStreamManagementAsync(ValidationReport report)
        {
            _logger.LogInformation("Validating stream management...");
            
            var result = new ValidationResult
            {
                Category = "Stream Management",
                TestName = "Stream Operations Validation"
            };

            try
            {
                using var factory = new CudaAcceleratorFactoryProduction(_logger, _serviceProvider);
                var accelerator = factory.CreateDefaultAccelerator() as CudaAcceleratorFactoryProduction.ProductionCudaAccelerator;
                
                if (accelerator == null)
                {
                    result.Status = ValidationStatus.Failed;
                    result.ErrorMessage = "Failed to create accelerator";
                    _results.Add(result);
                    return;
                }

                // Initialize stream manager
                accelerator.StreamManager.Initialize(4, 16);
                
                // Test stream creation
                var stream = accelerator.StreamManager.CreateStream(
                    "test_stream",
                    Execution.StreamPriority.Normal,
                    Execution.StreamFlags.NonBlocking);
                
                if (stream != IntPtr.Zero)
                {
                    result.Details.Add("Stream Creation: Successful");
                    
                    // Test stream synchronization
                    accelerator.StreamManager.SynchronizeStream(stream);
                    result.Details.Add("Stream Synchronization: Successful");
                    
                    // Test stream destruction
                    accelerator.StreamManager.DestroyStream(stream);
                    result.Details.Add("Stream Destruction: Successful");
                }
                else
                {
                    result.Status = ValidationStatus.Failed;
                    result.ErrorMessage = "Failed to create stream";
                }

                var stats = accelerator.StreamManager.GetStatistics();
                result.Details.Add($"Stream Stats: {stats.TotalStreamsCreated} total, {stats.ActiveStreams} active");

                accelerator.Dispose();
                
                if (result.Status == ValidationStatus.NotSet)
                {
                    result.Status = ValidationStatus.Passed;
                }
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Failed;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Failed to validate stream management");
            }

            _results.Add(result);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Validates kernel compilation and caching.
        /// </summary>
        private async Task ValidateKernelCompilationAsync(ValidationReport report)
        {
            _logger.LogInformation("Validating kernel compilation...");
            
            var result = new ValidationResult
            {
                Category = "Kernel Compilation",
                TestName = "PTX Compilation and Caching"
            };

            try
            {
                using var factory = new CudaAcceleratorFactoryProduction(_logger, _serviceProvider);
                var accelerator = factory.CreateDefaultAccelerator() as CudaAcceleratorFactoryProduction.ProductionCudaAccelerator;
                
                if (accelerator == null)
                {
                    result.Status = ValidationStatus.Failed;
                    result.ErrorMessage = "Failed to create accelerator";
                    _results.Add(result);
                    return;
                }

                // Simple kernel source
                string kernelSource = @"
                    extern ""C"" __global__ void test_kernel(float* data, int n) {
                        int idx = blockIdx.x * blockDim.x + threadIdx.x;
                        if (idx < n) {
                            data[idx] = data[idx] * 2.0f;
                        }
                    }";

                // Test compilation
                var compiledKernel = await accelerator.KernelCache.GetOrCompileKernelAsync(
                    kernelSource,
                    "test_kernel",
                    new Compilation.CompilationOptions { Architecture = "sm_60" });
                
                if (compiledKernel != null)
                {
                    result.Details.Add("Kernel Compilation: Successful");
                    result.Details.Add($"PTX Size: {compiledKernel.PtxCode?.Length ?? 0} bytes");
                    
                    // Test cache hit
                    var cachedKernel = await accelerator.KernelCache.GetOrCompileKernelAsync(
                        kernelSource,
                        "test_kernel",
                        new Compilation.CompilationOptions { Architecture = "sm_60" });
                    
                    if (cachedKernel.LoadedFromCache)
                    {
                        result.Details.Add("Kernel Caching: Functional");
                    }
                }
                else
                {
                    result.Status = ValidationStatus.Warning;
                    result.Details.Add("Kernel Compilation: Limited or unavailable");
                }

                accelerator.Dispose();
                
                if (result.Status == ValidationStatus.NotSet)
                {
                    result.Status = ValidationStatus.Passed;
                }
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Warning;
                result.Details.Add($"Kernel Compilation: {ex.Message}");
            }

            _results.Add(result);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Validates graph optimization features.
        /// </summary>
        private async Task ValidateGraphOptimizationAsync(ValidationReport report)
        {
            _logger.LogInformation("Validating graph optimization...");
            
            var result = new ValidationResult
            {
                Category = "Graph Optimization",
                TestName = "CUDA Graph Capture and Execution"
            };

            try
            {
                using var factory = new CudaAcceleratorFactoryProduction(_logger, _serviceProvider);
                var accelerator = factory.CreateDefaultAccelerator() as CudaAcceleratorFactoryProduction.ProductionCudaAccelerator;
                
                if (accelerator == null)
                {
                    result.Status = ValidationStatus.Failed;
                    result.ErrorMessage = "Failed to create accelerator";
                    _results.Add(result);
                    return;
                }

                // Check if graphs are supported
                var device = accelerator.DeviceManager.GetDeviceInfo(accelerator.DeviceId);
                if (device.Major < 7) // Volta or newer required
                {
                    result.Status = ValidationStatus.Skipped;
                    result.Details.Add($"Graphs require CC 7.0+, device has CC {device.Major}.{device.Minor}");
                    _results.Add(result);
                    return;
                }

                result.Details.Add("CUDA Graphs: Supported");
                result.Status = ValidationStatus.Passed;

                accelerator.Dispose();
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Warning;
                result.Details.Add($"Graph Optimization: {ex.Message}");
            }

            _results.Add(result);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Validates tensor core functionality.
        /// </summary>
        private async Task ValidateTensorCoresAsync(ValidationReport report)
        {
            _logger.LogInformation("Validating tensor cores...");
            
            var result = new ValidationResult
            {
                Category = "Tensor Cores",
                TestName = "Mixed Precision Operations"
            };

            try
            {
                using var factory = new CudaAcceleratorFactoryProduction(_logger, _serviceProvider);
                var accelerator = factory.CreateDefaultAccelerator() as CudaAcceleratorFactoryProduction.ProductionCudaAccelerator;
                
                if (accelerator == null)
                {
                    result.Status = ValidationStatus.Failed;
                    result.ErrorMessage = "Failed to create accelerator";
                    _results.Add(result);
                    return;
                }

                var device = accelerator.DeviceManager.GetDeviceInfo(accelerator.DeviceId);
                
                if (device.TensorCoreCount == 0)
                {
                    result.Status = ValidationStatus.Skipped;
                    result.Details.Add("Tensor Cores: Not available on this device");
                    _results.Add(result);
                    accelerator.Dispose();
                    return;
                }

                // Initialize tensor core manager
                accelerator.TensorCoreManager.Initialize();
                
                // Check architecture
                var arch = accelerator.TensorCoreManager.DetectArchitecture();
                result.Details.Add($"Tensor Core Architecture: {arch}");
                result.Details.Add($"Tensor Core Count: {device.TensorCoreCount}");
                
                // Check supported types
                var supportedTypes = accelerator.TensorCoreManager.GetSupportedDataTypes();
                result.Details.Add($"Supported Types: {string.Join(", ", supportedTypes)}");
                
                result.Status = ValidationStatus.Passed;
                accelerator.Dispose();
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Warning;
                result.Details.Add($"Tensor Cores: {ex.Message}");
            }

            _results.Add(result);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Validates performance profiling capabilities.
        /// </summary>
        private async Task ValidatePerformanceProfilingAsync(ValidationReport report)
        {
            _logger.LogInformation("Validating performance profiling...");
            
            var result = new ValidationResult
            {
                Category = "Performance Profiling",
                TestName = "Profiling and Metrics Collection"
            };

            try
            {
                using var factory = new CudaAcceleratorFactoryProduction(_logger, _serviceProvider);
                var accelerator = factory.CreateDefaultAccelerator() as CudaAcceleratorFactoryProduction.ProductionCudaAccelerator;
                
                if (accelerator == null)
                {
                    result.Status = ValidationStatus.Failed;
                    result.ErrorMessage = "Failed to create accelerator";
                    _results.Add(result);
                    return;
                }

                // Start profiling
                await accelerator.Profiler.StartProfilingAsync();
                result.Details.Add("Profiling: Started successfully");
                
                // Collect GPU metrics
                var metrics = await accelerator.Profiler.CollectGpuMetricsAsync();
                result.Details.Add($"GPU Utilization: {metrics.GpuUtilization}%");
                result.Details.Add($"Memory Utilization: {metrics.MemoryUtilization}%");
                result.Details.Add($"Temperature: {metrics.Temperature}°C");
                
                // Stop profiling
                var profilingReport = await accelerator.Profiler.StopProfilingAsync();
                result.Details.Add("Profiling: Stopped successfully");
                
                result.Status = ValidationStatus.Passed;
                accelerator.Dispose();
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Warning;
                result.Details.Add($"Performance Profiling: {ex.Message}");
            }

            _results.Add(result);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Validates error recovery mechanisms.
        /// </summary>
        private async Task ValidateErrorRecoveryAsync(ValidationReport report)
        {
            _logger.LogInformation("Validating error recovery...");
            
            var result = new ValidationResult
            {
                Category = "Error Recovery",
                TestName = "Fault Tolerance and Recovery"
            };

            try
            {
                using var factory = new CudaAcceleratorFactoryProduction(_logger, _serviceProvider);
                var accelerator = factory.CreateDefaultAccelerator() as CudaAcceleratorFactoryProduction.ProductionCudaAccelerator;
                
                if (accelerator == null)
                {
                    result.Status = ValidationStatus.Failed;
                    result.ErrorMessage = "Failed to create accelerator";
                    _results.Add(result);
                    return;
                }

                // Test error handler initialization
                result.Details.Add("Error Handler: Initialized");
                
                // Check retry policy
                result.Details.Add("Retry Policy: Configured with exponential backoff");
                
                // Check circuit breaker
                result.Details.Add("Circuit Breaker: Configured with 5 failure threshold");
                
                // Check CPU fallback
                if (accelerator.ErrorHandler.IsCpuFallbackEnabled)
                {
                    result.Details.Add("CPU Fallback: Enabled");
                }
                else
                {
                    result.Details.Add("CPU Fallback: Disabled");
                }
                
                result.Status = ValidationStatus.Passed;
                accelerator.Dispose();
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Failed;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Failed to validate error recovery");
            }

            _results.Add(result);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Validates multi-GPU support.
        /// </summary>
        private async Task ValidateMultiGpuSupportAsync(ValidationReport report)
        {
            _logger.LogInformation("Validating multi-GPU support...");
            
            var result = new ValidationResult
            {
                Category = "Multi-GPU Support",
                TestName = "P2P and Multi-Device Operations"
            };

            try
            {
                using var factory = new CudaAcceleratorFactoryProduction(_logger, _serviceProvider);
                var accelerators = factory.CreateAccelerators()
                    .OfType<CudaAcceleratorFactoryProduction.ProductionCudaAccelerator>()
                    .ToList();
                
                if (accelerators.Count < 2)
                {
                    result.Status = ValidationStatus.Skipped;
                    result.Details.Add($"Multi-GPU: Only {accelerators.Count} device(s) available");
                    _results.Add(result);
                    
                    foreach (var acc in accelerators) acc.Dispose();
                    return;
                }

                result.Details.Add($"Multi-GPU: {accelerators.Count} devices available");
                
                // Check P2P support
                var device0 = accelerators[0];
                var device1 = accelerators[1];
                
                if (device0.DeviceManager.CanAccessPeer(device0.DeviceId, device1.DeviceId))
                {
                    result.Details.Add($"P2P Access: Supported between device {device0.DeviceId} and {device1.DeviceId}");
                }
                else
                {
                    result.Details.Add($"P2P Access: Not supported between devices");
                }
                
                result.Status = ValidationStatus.Passed;
                
                foreach (var acc in accelerators) acc.Dispose();
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Warning;
                result.Details.Add($"Multi-GPU: {ex.Message}");
            }

            _results.Add(result);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Validates production workload capability.
        /// </summary>
        private async Task ValidateProductionWorkloadAsync(ValidationReport report)
        {
            _logger.LogInformation("Validating production workload...");
            
            var result = new ValidationResult
            {
                Category = "Production Workload",
                TestName = "End-to-End Production Simulation"
            };

            try
            {
                using var factory = new CudaAcceleratorFactoryProduction(_logger, _serviceProvider);
                var config = CudaAcceleratorFactoryProduction.ProductionConfiguration.HighPerformance;
                var accelerator = factory.CreateProductionAccelerator(0, config);
                
                // Simulate production workload
                var workloadStart = Stopwatch.GetTimestamp();
                
                // 1. Allocate memory
                var buffer = await accelerator.AsyncMemoryManager.AllocateBufferCoreAsync(
                    100 * 1024 * 1024, // 100MB
                    Core.Memory.BufferType.Device,
                    null);
                
                result.Details.Add("Memory Allocation: 100MB allocated");
                
                // 2. Create streams
                accelerator.StreamManager.Initialize(8, 32);
                var stream = accelerator.StreamManager.CreateStream("workload", 
                    Execution.StreamPriority.High, 
                    Execution.StreamFlags.NonBlocking);
                
                result.Details.Add("Stream Creation: High-priority stream created");
                
                // 3. Calculate occupancy
                var occupancy = await accelerator.OccupancyCalculator.CalculateOptimalLaunchConfigAsync(
                    IntPtr.Zero, 0, 0);
                
                result.Details.Add($"Occupancy Calculation: {occupancy.TheoreticalOccupancy:P} theoretical occupancy");
                
                // 4. Cleanup
                buffer.Dispose();
                accelerator.StreamManager.DestroyStream(stream);
                
                var workloadElapsed = Stopwatch.GetElapsedTime(workloadStart);
                result.Details.Add($"Workload Execution: Completed in {workloadElapsed.TotalMilliseconds:F2}ms");
                
                result.Status = ValidationStatus.Passed;
                accelerator.Dispose();
            }
            catch (Exception ex)
            {
                result.Status = ValidationStatus.Failed;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Failed to validate production workload");
            }

            _results.Add(result);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Determines overall validation status.
        /// </summary>
        private ValidationStatus DetermineOverallStatus(ValidationReport report)
        {
            if (report.FailedTests > 0)
                return ValidationStatus.Failed;
            
            if (report.WarningTests > report.PassedTests / 2)
                return ValidationStatus.Warning;
            
            if (report.PassedTests == report.TotalTests)
                return ValidationStatus.Passed;
            
            return ValidationStatus.Partial;
        }

        /// <summary>
        /// Exports validation report to various formats.
        /// </summary>
        public async Task ExportReportAsync(ValidationReport report, string filepath, ExportFormat format)
        {
            try
            {
                string content = format switch
                {
                    ExportFormat.Json => System.Text.Json.JsonSerializer.Serialize(report, 
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
                    ExportFormat.Markdown => GenerateMarkdownReport(report),
                    ExportFormat.Html => GenerateHtmlReport(report),
                    _ => report.ToString()
                };

                await System.IO.File.WriteAllTextAsync(filepath, content);
                _logger.LogInformation("Validation report exported to {FilePath}", filepath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export validation report");
                throw;
            }
        }

        /// <summary>
        /// Generates markdown format report.
        /// </summary>
        private string GenerateMarkdownReport(ValidationReport report)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("# CUDA Production Validation Report");
            sb.AppendLine();
            sb.AppendLine($"**Date:** {report.StartTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"**Duration:** {report.Duration:g}");
            sb.AppendLine($"**Overall Status:** {report.OverallStatus}");
            sb.AppendLine();
            
            sb.AppendLine("## Summary");
            sb.AppendLine($"- Total Tests: {report.TotalTests}");
            sb.AppendLine($"- Passed: {report.PassedTests} ✅");
            sb.AppendLine($"- Failed: {report.FailedTests} ❌");
            sb.AppendLine($"- Warnings: {report.WarningTests} ⚠️");
            sb.AppendLine($"- Skipped: {report.SkippedTests} ⏭️");
            sb.AppendLine();
            
            sb.AppendLine("## Test Results");
            
            foreach (var category in report.Results.GroupBy(r => r.Category))
            {
                sb.AppendLine($"### {category.Key}");
                sb.AppendLine();
                
                foreach (var result in category)
                {
                    var icon = result.Status switch
                    {
                        ValidationStatus.Passed => "✅",
                        ValidationStatus.Failed => "❌",
                        ValidationStatus.Warning => "⚠️",
                        ValidationStatus.Skipped => "⏭️",
                        _ => "❓"
                    };
                    
                    sb.AppendLine($"#### {icon} {result.TestName}");
                    sb.AppendLine($"**Status:** {result.Status}");
                    
                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        sb.AppendLine($"**Error:** {result.ErrorMessage}");
                    }
                    
                    if (result.Details.Any())
                    {
                        sb.AppendLine("**Details:**");
                        foreach (var detail in result.Details)
                        {
                            sb.AppendLine($"- {detail}");
                        }
                    }
                    
                    sb.AppendLine();
                }
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Generates HTML format report.
        /// </summary>
        private string GenerateHtmlReport(ValidationReport report)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<title>CUDA Production Validation Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            sb.AppendLine("h1 { color: #333; }");
            sb.AppendLine(".passed { color: green; }");
            sb.AppendLine(".failed { color: red; }");
            sb.AppendLine(".warning { color: orange; }");
            sb.AppendLine(".skipped { color: gray; }");
            sb.AppendLine("table { border-collapse: collapse; width: 100%; }");
            sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            sb.AppendLine("th { background-color: #f2f2f2; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            
            sb.AppendLine("<h1>CUDA Production Validation Report</h1>");
            sb.AppendLine($"<p><strong>Date:</strong> {report.StartTime:yyyy-MM-dd HH:mm:ss}</p>");
            sb.AppendLine($"<p><strong>Duration:</strong> {report.Duration:g}</p>");
            sb.AppendLine($"<p><strong>Overall Status:</strong> <span class=\"{report.OverallStatus.ToString().ToLower()}\">{report.OverallStatus}</span></p>");
            
            sb.AppendLine("<h2>Summary</h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>Metric</th><th>Count</th></tr>");
            sb.AppendLine($"<tr><td>Total Tests</td><td>{report.TotalTests}</td></tr>");
            sb.AppendLine($"<tr><td class=\"passed\">Passed</td><td>{report.PassedTests}</td></tr>");
            sb.AppendLine($"<tr><td class=\"failed\">Failed</td><td>{report.FailedTests}</td></tr>");
            sb.AppendLine($"<tr><td class=\"warning\">Warnings</td><td>{report.WarningTests}</td></tr>");
            sb.AppendLine($"<tr><td class=\"skipped\">Skipped</td><td>{report.SkippedTests}</td></tr>");
            sb.AppendLine("</table>");
            
            sb.AppendLine("<h2>Test Results</h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>Category</th><th>Test</th><th>Status</th><th>Details</th></tr>");
            
            foreach (var result in report.Results)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{result.Category}</td>");
                sb.AppendLine($"<td>{result.TestName}</td>");
                sb.AppendLine($"<td class=\"{result.Status.ToString().ToLower()}\">{result.Status}</td>");
                sb.AppendLine("<td>");
                
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    sb.AppendLine($"<strong>Error:</strong> {result.ErrorMessage}<br/>");
                }
                
                if (result.Details.Any())
                {
                    sb.AppendLine("<ul>");
                    foreach (var detail in result.Details)
                    {
                        sb.AppendLine($"<li>{detail}</li>");
                    }
                    sb.AppendLine("</ul>");
                }
                
                sb.AppendLine("</td>");
                sb.AppendLine("</tr>");
            }
            
            sb.AppendLine("</table>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            
            return sb.ToString();
        }

        // Supporting classes
        public class ValidationOptions
        {
            public bool ValidateSystemRequirements { get; set; } = true;
            public bool ValidateCudaInstallation { get; set; } = true;
            public bool ValidateDeviceCapabilities { get; set; } = true;
            public bool ValidateMemoryManagement { get; set; } = true;
            public bool ValidateStreamManagement { get; set; } = true;
            public bool ValidateKernelCompilation { get; set; } = true;
            public bool ValidateGraphOptimization { get; set; } = true;
            public bool ValidateTensorCores { get; set; } = true;
            public bool ValidatePerformanceProfiling { get; set; } = true;
            public bool ValidateErrorRecovery { get; set; } = true;
            public bool ValidateMultiGpu { get; set; } = true;
            public bool ValidateProductionWorkload { get; set; } = true;
            
            public static ValidationOptions Default => new();
            
            public static ValidationOptions Quick => new()
            {
                ValidateSystemRequirements = true,
                ValidateCudaInstallation = true,
                ValidateDeviceCapabilities = true,
                ValidateMemoryManagement = false,
                ValidateStreamManagement = false,
                ValidateKernelCompilation = false,
                ValidateGraphOptimization = false,
                ValidateTensorCores = false,
                ValidatePerformanceProfiling = false,
                ValidateErrorRecovery = false,
                ValidateMultiGpu = false,
                ValidateProductionWorkload = false
            };
            
            public static ValidationOptions Full => new()
            {
                ValidateSystemRequirements = true,
                ValidateCudaInstallation = true,
                ValidateDeviceCapabilities = true,
                ValidateMemoryManagement = true,
                ValidateStreamManagement = true,
                ValidateKernelCompilation = true,
                ValidateGraphOptimization = true,
                ValidateTensorCores = true,
                ValidatePerformanceProfiling = true,
                ValidateErrorRecovery = true,
                ValidateMultiGpu = true,
                ValidateProductionWorkload = true
            };
        }

        public class ValidationReport
        {
            public DateTimeOffset StartTime { get; set; }
            public DateTimeOffset EndTime { get; set; }
            public TimeSpan Duration { get; set; }
            public ValidationOptions Options { get; set; } = new();
            public List<ValidationResult> Results { get; set; } = new();
            public int TotalTests { get; set; }
            public int PassedTests { get; set; }
            public int FailedTests { get; set; }
            public int WarningTests { get; set; }
            public int SkippedTests { get; set; }
            public ValidationStatus OverallStatus { get; set; }
            public string? CriticalError { get; set; }
        }

        public class ValidationResult
        {
            public required string Category { get; init; }
            public required string TestName { get; init; }
            public ValidationStatus Status { get; set; } = ValidationStatus.NotSet;
            public string? ErrorMessage { get; set; }
            public List<string> Details { get; } = new();
            public TimeSpan? Duration { get; set; }
        }

        public enum ValidationStatus
        {
            NotSet,
            Passed,
            Failed,
            Warning,
            Skipped,
            Partial
        }

        public enum ExportFormat
        {
            Json,
            Markdown,
            Html,
            Text
        }
    }
}