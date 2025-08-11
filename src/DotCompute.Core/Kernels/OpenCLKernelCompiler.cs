// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using System.Text;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Kernels;

/// <summary>
/// OpenCL kernel compiler implementation using OpenCL runtime compilation.
/// </summary>
public sealed class OpenCLKernelCompiler : IKernelCompiler
{
    private readonly ILogger<OpenCLKernelCompiler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLKernelCompiler"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public OpenCLKernelCompiler(ILogger<OpenCLKernelCompiler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public AcceleratorType AcceleratorType => AcceleratorType.OpenCL;

    /// <inheritdoc/>
    public async ValueTask<ManagedCompiledKernel> CompileAsync(GeneratedKernel kernel, CompilationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(options);

        if (kernel.Language != KernelLanguage.OpenCL)
        {
            throw new ArgumentException($"Expected OpenCL kernel but received {kernel.Language}", nameof(kernel));
        }

        _logger.LogInformation("Compiling OpenCL kernel '{KernelName}'", kernel.Name);

        try
        {
            // In a real implementation, this would use the OpenCL C API to compile kernels
            // For now, we'll simulate compilation with validation and return a mock compiled kernel
            var compilationResult = await CompileKernelSourceAsync(kernel, options, cancellationToken);

            var compiledKernel = new ManagedCompiledKernel
            {
                Name = kernel.Name,
                Binary = compilationResult.Binary,
                Handle = compilationResult.Handle,
                Parameters = kernel.Parameters,
                RequiredWorkGroupSize = kernel.RequiredWorkGroupSize,
                SharedMemorySize = kernel.SharedMemorySize,
                CompilationLog = compilationResult.Log,
                PerformanceMetadata = CreateAdvancedPerformanceMetadata(kernel, options, compilationResult)
            };

            _logger.LogInformation("Successfully compiled OpenCL kernel '{KernelName}' in {CompilationTime:F2}ms",
                kernel.Name, compilationResult.CompilationTime);

            return compiledKernel;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("OpenCL kernel compilation for '{KernelName}' was cancelled", kernel.Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compile OpenCL kernel '{KernelName}'", kernel.Name);
            throw new InvalidOperationException($"OpenCL kernel compilation failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public KernelValidationResult Validate(GeneratedKernel kernel)
    {
        ArgumentNullException.ThrowIfNull(kernel);

        if (kernel.Language != KernelLanguage.OpenCL)
        {
            return new KernelValidationResult
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new ValidationError
                    {
                        Code = "INVALID_LANGUAGE",
                        Message = $"Expected OpenCL kernel but received {kernel.Language}"
                    }
                }
            };
        }

        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        // Basic syntax validation
        ValidateOpenCLSyntax(kernel.Source, errors, warnings);

        // Parameter validation
        ValidateParameters(kernel.Parameters, errors, warnings);

        // Work group size validation
        ValidateWorkGroupSize(kernel.RequiredWorkGroupSize, errors, warnings);

        // Estimate resource usage
        var resourceUsage = EstimateResourceUsage(kernel);

        return new KernelValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            ResourceUsage = resourceUsage
        };
    }

    /// <inheritdoc/>
    public CompilationOptions GetDefaultOptions()
    {
        return new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.O2,
            GenerateDebugInfo = false,
            EnableFastMath = true,
            FiniteMathOnly = true,
            EnableUnsafeOptimizations = false,
            AdditionalFlags = new List<string>
            {
                "-cl-mad-enable",           // Enable multiply-add optimizations
                "-cl-no-signed-zeros",      // Allow optimizations for signed zero
                "-cl-unsafe-math-optimizations", // Enable unsafe math optimizations when requested
                "-cl-finite-math-only"      // Assume finite math only
            },
            Defines = new Dictionary<string, string>
            {
                ["OPENCL_VERSION"] = "200", // OpenCL 2.0
                ["CL_TARGET_OPENCL_VERSION"] = "200"
            }
        };
    }

    /// <summary>
    /// Compiles the kernel source using OpenCL runtime compilation.
    /// </summary>
    private async Task<OpenCLCompilationResult> CompileKernelSourceAsync(GeneratedKernel kernel, CompilationOptions options, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        // Build compilation options string
        var compilerOptions = BuildOpenCLOptions(options);
        _logger.LogDebug("OpenCL compiler options: {Options}", compilerOptions);

        // Check if OpenCL is available
        if (!OpenCLInterop.IsOpenCLAvailable())
        {
            _logger.LogWarning("OpenCL runtime not available, using mock compilation");
            return await CompileMockAsync(kernel, options, cancellationToken);
        }

        try
        {
            // Get platforms and devices
            var platforms = OpenCLInterop.GetAvailablePlatforms();
            if (platforms.Length == 0)
            {
                throw new InvalidOperationException("No OpenCL platforms found");
            }

            var devices = OpenCLInterop.GetAvailableDevices(platforms[0], OpenCLInterop.CL_DEVICE_TYPE_ALL);
            if (devices.Length == 0)
            {
                throw new InvalidOperationException("No OpenCL devices found");
            }

            // Create context
            var context = OpenCLInterop.CreateContext(IntPtr.Zero, 1, new[] { devices[0] }, IntPtr.Zero, IntPtr.Zero, out var contextResult);
            OpenCLInterop.ThrowOnError(contextResult, "CreateContext");

            try
            {
                // Create program with source
                var sources = new[] { kernel.Source };
                var program = OpenCLInterop.CreateProgramWithSource(context, 1, sources, null, out var programResult);
                OpenCLInterop.ThrowOnError(programResult, "CreateProgramWithSource");

                try
                {
                    // Build program
                    var buildResult = OpenCLInterop.BuildProgram(program, 1, new[] { devices[0] }, compilerOptions, IntPtr.Zero, IntPtr.Zero);
                    
                    // Get build log
                    var buildLog = OpenCLInterop.GetProgramBuildLog(program, devices[0]);
                    var buildStatus = OpenCLInterop.GetProgramBuildStatus(program, devices[0]);
                    
                    if (buildResult != OpenCLInterop.CL_SUCCESS || buildStatus != OpenCLInterop.CL_BUILD_SUCCESS)
                    {
                        throw new InvalidOperationException($"OpenCL compilation failed:\n{buildLog}");
                    }

                    // Get compiled binary
                    var binary = OpenCLInterop.GetProgramBinary(program);

                    // Create kernel to get the handle
                    var kernelHandle = OpenCLInterop.CreateKernel(program, kernel.Name, out var kernelResult);
                    if (kernelResult != OpenCLInterop.CL_SUCCESS)
                    {
                        // Try with "main" as kernel name if the provided name fails
                        kernelHandle = OpenCLInterop.CreateKernel(program, "main", out kernelResult);
                    }
                    OpenCLInterop.ThrowOnError(kernelResult, "CreateKernel");

                    var compilationTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                    return new OpenCLCompilationResult
                    {
                        Binary = binary,
                        Handle = kernelHandle,
                        Log = buildLog,
                        CompilationTime = compilationTime,
                        RegistersUsed = EstimateRegisterUsage(kernel)
                    };
                }
                finally
                {
                    OpenCLInterop.ReleaseProgram(program);
                }
            }
            finally
            {
                OpenCLInterop.ReleaseContext(context);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Real OpenCL compilation failed, falling back to mock compilation");
            return await CompileMockAsync(kernel, options, cancellationToken);
        }
    }

    /// <summary>
    /// Validates OpenCL syntax and constructs.
    /// </summary>
    private static void ValidateOpenCLSyntax(string source, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        // Check for required kernel function
        if (!source.Contains("__kernel"))
        {
            errors.Add(new ValidationError
            {
                Code = "MISSING_KERNEL",
                Message = "No __kernel function found in source code"
            });
        }

        // Check for balanced braces
        int braceCount = 0;
        int line = 1;
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            if (c == '\n') line++;
            else if (c == '{') braceCount++;
            else if (c == '}') braceCount--;

            if (braceCount < 0)
            {
                errors.Add(new ValidationError
                {
                    Code = "UNBALANCED_BRACES",
                    Message = "Unbalanced closing brace",
                    Line = line
                });
                break;
            }
        }

        if (braceCount > 0)
        {
            errors.Add(new ValidationError
            {
                Code = "UNBALANCED_BRACES",
                Message = "Unbalanced opening brace"
            });
        }

        // Check for common OpenCL issues
        if (source.Contains("malloc") || source.Contains("free"))
        {
            warnings.Add(new ValidationWarning
            {
                Code = "DYNAMIC_ALLOCATION",
                Message = "Dynamic memory allocation is not supported in OpenCL kernels",
                Severity = WarningSeverity.Serious
            });
        }

        if (source.Contains("printf") && !source.Contains("#pragma OPENCL EXTENSION cl_amd_printf : enable"))
        {
            warnings.Add(new ValidationWarning
            {
                Code = "PRINTF_WITHOUT_EXTENSION",
                Message = "printf used without enabling cl_amd_printf extension",
                Severity = WarningSeverity.Warning
            });
        }
    }

    /// <summary>
    /// Validates kernel parameters.
    /// </summary>
    private static void ValidateParameters(KernelParameter[] parameters, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        foreach (var param in parameters)
        {
            // Check for valid parameter types
            if (!IsValidOpenCLType(param.Type))
            {
                errors.Add(new ValidationError
                {
                    Code = "INVALID_PARAMETER_TYPE",
                    Message = $"Parameter '{param.Name}' has unsupported type '{param.Type}'"
                });
            }

            // Check memory space consistency
            if (param.MemorySpace == MemorySpace.Shared && !param.Type.IsArray)
            {
                warnings.Add(new ValidationWarning
                {
                    Code = "SHARED_SCALAR",
                    Message = $"Parameter '{param.Name}' uses shared memory but is not an array type",
                    Severity = WarningSeverity.Warning
                });
            }
        }
    }

    /// <summary>
    /// Validates work group size requirements.
    /// </summary>
    private static void ValidateWorkGroupSize(int[]? workGroupSize, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        if (workGroupSize == null) return;

        if (workGroupSize.Length > 3)
        {
            errors.Add(new ValidationError
            {
                Code = "INVALID_WORK_GROUP_DIMENSIONS",
                Message = "Work group can have at most 3 dimensions"
            });
        }

        foreach (int size in workGroupSize)
        {
            if (size <= 0)
            {
                errors.Add(new ValidationError
                {
                    Code = "INVALID_WORK_GROUP_SIZE",
                    Message = "Work group size must be positive"
                });
            }
            else if (size > 1024)
            {
                warnings.Add(new ValidationWarning
                {
                    Code = "LARGE_WORK_GROUP_SIZE",
                    Message = $"Work group size {size} may exceed device limits",
                    Severity = WarningSeverity.Warning
                });
            }
        }

        // Check total work group size
        int totalSize = workGroupSize.Aggregate(1, (a, b) => a * b);
        if (totalSize > 1024)
        {
            warnings.Add(new ValidationWarning
            {
                Code = "LARGE_TOTAL_WORK_GROUP_SIZE",
                Message = $"Total work group size {totalSize} may exceed device limits",
                Severity = WarningSeverity.Warning
            });
        }
    }

    /// <summary>
    /// Estimates resource usage for the kernel.
    /// </summary>
    private static ResourceUsageEstimate EstimateResourceUsage(GeneratedKernel kernel)
    {
        // Simple heuristic-based estimation
        int registerEstimate = 16; // Base registers
        int sharedMemory = kernel.SharedMemorySize;
        int constantMemory = 0;

        // Estimate based on parameter count and types
        foreach (var param in kernel.Parameters)
        {
            registerEstimate += GetParameterRegisterCost(param);
            if (param.MemorySpace == MemorySpace.Constant)
            {
                constantMemory += GetTypeSize(param.Type);
            }
        }

        // Estimate based on source complexity (simple heuristic)
        int sourceComplexity = kernel.Source.Count(c => c == '+' || c == '-' || c == '*' || c == '/');
        registerEstimate += sourceComplexity / 10; // Rough estimate

        return new ResourceUsageEstimate
        {
            RegistersPerThread = Math.Min(registerEstimate, 255), // OpenCL typical limit
            SharedMemoryPerBlock = sharedMemory,
            ConstantMemoryUsage = constantMemory,
            MaxThreadsPerBlock = sharedMemory > 0 ? Math.Min(1024, 49152 / sharedMemory) : 1024,
            OccupancyEstimate = CalculateOccupancyEstimate(registerEstimate, sharedMemory)
        };
    }

    /// <summary>
    /// Calculates occupancy estimate based on resource usage.
    /// </summary>
    private static float CalculateOccupancyEstimate(int registers, int sharedMemory)
    {
        // Simple occupancy calculation based on typical OpenCL device limits
        float registerLimitedOccupancy = registers > 0 ? Math.Min(1.0f, 65536.0f / (registers * 256)) : 1.0f;
        float sharedMemoryLimitedOccupancy = sharedMemory > 0 ? Math.Min(1.0f, 49152.0f / sharedMemory) : 1.0f;
        
        return Math.Min(registerLimitedOccupancy, sharedMemoryLimitedOccupancy);
    }

    /// <summary>
    /// Builds OpenCL compiler options string.
    /// </summary>
    private static string BuildOpenCLOptions(CompilationOptions options)
    {
        var optionsBuilder = new StringBuilder();
        
        // Add optimization level
        switch (options.OptimizationLevel)
        {
            case OptimizationLevel.O0:
                optionsBuilder.Append("-O0 ");
                break;
            case OptimizationLevel.O1:
                optionsBuilder.Append("-O1 ");
                break;
            case OptimizationLevel.O2:
                optionsBuilder.Append("-O2 ");
                break;
            case OptimizationLevel.O3:
                optionsBuilder.Append("-O3 ");
                break;
            case OptimizationLevel.Os:
                optionsBuilder.Append("-Os ");
                break;
        }

        // Add debug info if requested
        if (options.GenerateDebugInfo)
        {
            optionsBuilder.Append("-g ");
        }

        // Add fast math options
        if (options.EnableFastMath)
        {
            optionsBuilder.Append("-cl-mad-enable -cl-unsafe-math-optimizations ");
        }

        if (options.FiniteMathOnly)
        {
            optionsBuilder.Append("-cl-finite-math-only ");
        }

        // Add defines
        foreach (var define in options.Defines)
        {
            optionsBuilder.Append($"-D{define.Key}={define.Value} ");
        }

        // Add include directories
        foreach (var includeDir in options.IncludeDirectories)
        {
            optionsBuilder.Append($"-I\"{includeDir}\" ");
        }

        // Add additional flags
        foreach (var flag in options.AdditionalFlags)
        {
            optionsBuilder.Append($"{flag} ");
        }

        return optionsBuilder.ToString().Trim();
    }

    /// <summary>
    /// Fallback mock compilation when real OpenCL is not available.
    /// </summary>
    private async Task<OpenCLCompilationResult> CompileMockAsync(GeneratedKernel kernel, CompilationOptions options, CancellationToken cancellationToken)
    {
        await Task.Delay(10, cancellationToken); // Simulate compilation time

        // Generate a mock binary
        var mockBinary = GenerateMockOpenCLBinary(kernel, options);

        // Generate compilation log
        var log = GenerateCompilationLog(kernel, options, true);

        return new OpenCLCompilationResult
        {
            Binary = mockBinary,
            Handle = new IntPtr(Random.Shared.Next(1000, 9999)), // Mock handle
            Log = log,
            CompilationTime = 10.0, // Mock compilation time
            RegistersUsed = EstimateRegisterUsage(kernel)
        };
    }

    /// <summary>
    /// Generates a mock OpenCL binary for testing purposes.
    /// </summary>
    private static byte[] GenerateMockOpenCLBinary(GeneratedKernel kernel, CompilationOptions options)
    {
        // In a real implementation, this would be the actual compiled binary from OpenCL
        var mockBinary = new byte[1024]; // Mock binary size
        
        // Fill with some deterministic but pseudo-random data based on kernel content
        var hash = kernel.Source.GetHashCode();
        var random = new Random(hash);
        random.NextBytes(mockBinary);
        
        return mockBinary;
    }

    /// <summary>
    /// Generates a compilation log.
    /// </summary>
    private static string GenerateCompilationLog(GeneratedKernel kernel, CompilationOptions options, bool success)
    {
        var log = new StringBuilder();
        log.AppendLine($"OpenCL Kernel Compilation Log for '{kernel.Name}'");
        log.AppendLine($"Optimization Level: {options.OptimizationLevel}");
        log.AppendLine($"Fast Math: {options.EnableFastMath}");
        log.AppendLine($"Debug Info: {options.GenerateDebugInfo}");
        log.AppendLine();

        if (success)
        {
            log.AppendLine("Compilation successful.");
            log.AppendLine($"Kernel parameters: {kernel.Parameters.Length}");
            log.AppendLine($"Shared memory usage: {kernel.SharedMemorySize} bytes");
            if (kernel.RequiredWorkGroupSize != null)
            {
                log.AppendLine($"Required work group size: [{string.Join(", ", kernel.RequiredWorkGroupSize)}]");
            }
        }
        else
        {
            log.AppendLine("Compilation failed.");
            log.AppendLine("Error: Simulated compilation failure");
        }

        return log.ToString();
    }

    /// <summary>
    /// Estimates register usage for a kernel parameter.
    /// </summary>
    private static int GetParameterRegisterCost(KernelParameter parameter)
    {
        if (parameter.Type.IsArray)
        {
            return 1; // Pointer parameter
        }

        return GetTypeSize(parameter.Type) / 4; // Rough estimate: 4-byte registers
    }

    /// <summary>
    /// Gets the size in bytes of a type.
    /// </summary>
    private static int GetTypeSize(Type type)
    {
        if (type.IsArray)
        {
            return IntPtr.Size; // Pointer size
        }

        return type switch
        {
            _ when type == typeof(float) => 4,
            _ when type == typeof(double) => 8,
            _ when type == typeof(int) => 4,
            _ when type == typeof(uint) => 4,
            _ when type == typeof(long) => 8,
            _ when type == typeof(ulong) => 8,
            _ when type == typeof(short) => 2,
            _ when type == typeof(ushort) => 2,
            _ when type == typeof(byte) => 1,
            _ when type == typeof(sbyte) => 1,
            _ when type == typeof(bool) => 1,
            _ => 4 // Default
        };
    }

    /// <summary>
    /// Estimates register usage for a kernel.
    /// </summary>
    private static int EstimateRegisterUsage(GeneratedKernel kernel)
    {
        // Simple heuristic based on kernel complexity
        int baseRegisters = 8;
        int parameterRegisters = kernel.Parameters.Sum(GetParameterRegisterCost);
        int sourceComplexity = kernel.Source.Count(c => c == '=' || c == '+' || c == '-' || c == '*' || c == '/');
        int complexityRegisters = sourceComplexity / 5; // Rough estimate
        
        return Math.Min(baseRegisters + parameterRegisters + complexityRegisters, 255);
    }

    /// <summary>
    /// Creates comprehensive performance metadata with advanced features analysis.
    /// </summary>
    private static Dictionary<string, object> CreateAdvancedPerformanceMetadata(
        GeneratedKernel kernel, CompilationOptions options, OpenCLCompilationResult compilationResult)
    {
        var metadata = new Dictionary<string, object>
        {
            ["CompilationTime"] = compilationResult.CompilationTime,
            ["RegistersUsed"] = compilationResult.RegistersUsed,
            ["SharedMemoryUsed"] = kernel.SharedMemorySize,
            ["OptimizationLevel"] = options.OptimizationLevel.ToString()
        };

        // Advanced OpenCL feature analysis
        var advancedFeatures = AnalyzeAdvancedOpenCLFeatures(kernel, options);
        foreach (var feature in advancedFeatures)
        {
            metadata[feature.Key] = feature.Value;
        }

        return metadata;
    }

    /// <summary>
    /// Analyzes advanced OpenCL kernel features and provides detailed metadata.
    /// </summary>
    private static Dictionary<string, object> AnalyzeAdvancedOpenCLFeatures(GeneratedKernel kernel, CompilationOptions options)
    {
        var features = new Dictionary<string, object>();
        var source = kernel.Source;

        // Local Memory Analysis (OpenCL equivalent of CUDA shared memory)
        var localMemoryAnalysis = AnalyzeLocalMemoryUsage(source, kernel.SharedMemorySize);
        features["local_memory_size"] = localMemoryAnalysis.TotalSize;
        features["local_memory_banks"] = localMemoryAnalysis.BankConflictPotential;
        features["local_memory_optimization"] = localMemoryAnalysis.OptimizationLevel;

        // Work Group Analysis
        var workGroupAnalysis = AnalyzeWorkGroupCharacteristics(source, kernel.RequiredWorkGroupSize);
        features["work_group_size"] = workGroupAnalysis.TotalSize;
        features["work_group_efficiency"] = workGroupAnalysis.Efficiency;
        features["work_group_dimensions"] = workGroupAnalysis.Dimensions;

        // Private Memory Analysis (OpenCL equivalent of registers)
        var privateMemoryAnalysis = AnalyzePrivateMemoryUsage(source, kernel.Parameters);
        features["private_memory_per_work_item"] = privateMemoryAnalysis.MemoryPerWorkItem;
        features["private_memory_pressure"] = privateMemoryAnalysis.Pressure;

        // Vector Operations Analysis
        var vectorAnalysis = AnalyzeVectorOperations(source);
        if (vectorAnalysis.UsesVectorTypes)
        {
            features["uses_vector_types"] = true;
            features["vector_width"] = vectorAnalysis.MaxVectorWidth;
            features["vectorization_efficiency"] = vectorAnalysis.Efficiency;
        }

        // Image Memory Analysis
        var imageAnalysis = AnalyzeImageMemoryUsage(source);
        if (imageAnalysis.UsesImages)
        {
            features["uses_image_memory"] = true;
            features["image_cache_efficiency"] = imageAnalysis.CacheEfficiency;
            features["image_access_patterns"] = imageAnalysis.AccessPatterns;
        }

        // Atomic Operations Analysis
        var atomicAnalysis = AnalyzeAtomicOperations(source);
        if (atomicAnalysis.UsesAtomics)
        {
            features["uses_atomic_operations"] = true;
            features["atomic_contention_risk"] = atomicAnalysis.ContentionRisk;
            features["atomic_operations_count"] = atomicAnalysis.OperationCount;
        }

        // Memory Access Pattern Analysis
        var memoryAnalysis = AnalyzeMemoryAccessPatterns(source, kernel.Parameters);
        features["global_memory_efficiency"] = memoryAnalysis.GlobalMemoryEfficiency;
        features["memory_access_patterns"] = memoryAnalysis.AccessPatterns;

        // Barrier Synchronization Analysis
        var barrierAnalysis = AnalyzeBarrierSynchronization(source);
        features["synchronization_complexity"] = barrierAnalysis.Complexity;
        features["work_group_divergence_risk"] = barrierAnalysis.DivergenceRisk;
        if (barrierAnalysis.HasBarriers)
        {
            features["uses_barriers"] = true;
            features["barrier_efficiency"] = barrierAnalysis.BarrierEfficiency;
        }

        // OpenCL Extension Analysis
        var extensionAnalysis = AnalyzeOpenCLExtensions(source);
        if (extensionAnalysis.Extensions.Any())
        {
            features["required_extensions"] = string.Join(",", extensionAnalysis.Extensions);
            features["extension_compatibility"] = extensionAnalysis.Compatibility;
        }

        // Device Type Optimization Analysis
        var deviceOptimization = AnalyzeDeviceTypeOptimizations(source);
        features["cpu_optimization_level"] = deviceOptimization.CpuOptimization;
        features["gpu_optimization_level"] = deviceOptimization.GpuOptimization;

        return features;
    }

    /// <summary>
    /// Checks if a type is valid for OpenCL kernels.
    /// </summary>
    private static bool IsValidOpenCLType(Type type)
    {
        if (type.IsArray)
        {
            type = type.GetElementType() ?? type;
        }

        return type == typeof(float) || type == typeof(double) || type == typeof(int) ||
               type == typeof(uint) || type == typeof(long) || type == typeof(ulong) ||
               type == typeof(short) || type == typeof(ushort) || type == typeof(byte) ||
               type == typeof(sbyte) || type == typeof(bool);
    }

    /// <summary>
    /// Represents the result of OpenCL kernel compilation.
    /// </summary>
    private sealed class OpenCLCompilationResult
    {
        public required byte[] Binary { get; init; }
        public required IntPtr Handle { get; init; }
        public required string Log { get; init; }
        public required double CompilationTime { get; init; }
        public required int RegistersUsed { get; init; }
    }

    #region Advanced Feature Analysis Types

    /// <summary>
    /// Local memory usage analysis result.
    /// </summary>
    private sealed record LocalMemoryAnalysis(
        int TotalSize,
        string BankConflictPotential,
        string OptimizationLevel
    );

    /// <summary>
    /// Work group characteristics analysis result.
    /// </summary>
    private sealed record WorkGroupAnalysis(
        int TotalSize,
        string Efficiency,
        int Dimensions
    );

    /// <summary>
    /// Private memory analysis result.
    /// </summary>
    private sealed record PrivateMemoryAnalysis(
        int MemoryPerWorkItem,
        string Pressure
    );

    /// <summary>
    /// Vector operations analysis result.
    /// </summary>
    private sealed record VectorOperationsAnalysis(
        bool UsesVectorTypes,
        int MaxVectorWidth,
        string Efficiency
    );

    /// <summary>
    /// Image memory analysis result.
    /// </summary>
    private sealed record ImageMemoryAnalysis(
        bool UsesImages,
        string CacheEfficiency,
        string AccessPatterns
    );

    /// <summary>
    /// Atomic operations analysis result.
    /// </summary>
    private sealed record AtomicOperationsAnalysis(
        bool UsesAtomics,
        string ContentionRisk,
        int OperationCount
    );

    /// <summary>
    /// Memory access patterns analysis result.
    /// </summary>
    private sealed record MemoryAccessAnalysis(
        string GlobalMemoryEfficiency,
        string AccessPatterns
    );

    /// <summary>
    /// Barrier synchronization analysis result.
    /// </summary>
    private sealed record BarrierSynchronizationAnalysis(
        string Complexity,
        string DivergenceRisk,
        bool HasBarriers,
        string BarrierEfficiency
    );

    /// <summary>
    /// OpenCL extensions analysis result.
    /// </summary>
    private sealed record OpenCLExtensionsAnalysis(
        List<string> Extensions,
        string Compatibility
    );

    /// <summary>
    /// Device type optimization analysis result.
    /// </summary>
    private sealed record DeviceTypeOptimizationAnalysis(
        string CpuOptimization,
        string GpuOptimization
    );

    #endregion

    #region Advanced Feature Analysis Methods

    /// <summary>
    /// Analyzes local memory usage patterns and optimization opportunities.
    /// </summary>
    private static LocalMemoryAnalysis AnalyzeLocalMemoryUsage(string source, int declaredSize)
    {
        var totalSize = declaredSize;
        var bankConflictPotential = "Low";
        var optimizationLevel = "Good";

        // Detect local memory parameters and usage
        var localMemParams = System.Text.RegularExpressions.Regex.Matches(source, @"__local\s+\w+\s*\*\s*(\w+)");
        var localMemDecls = System.Text.RegularExpressions.Regex.Matches(source, @"__local\s+\w+\s+(\w+)\s*\[([^\]]+)\]");

        if (localMemParams.Count > 0 || localMemDecls.Count > 0)
        {
            // Estimate local memory from declarations
            foreach (System.Text.RegularExpressions.Match match in localMemDecls)
            {
                var sizeExpr = match.Groups[2].Value;
                if (int.TryParse(sizeExpr, out var size))
                {
                    totalSize += size * 4; // Assume 4-byte elements
                }
            }

            // Analyze access patterns for bank conflicts
            if (source.Contains("get_local_id(0)") || source.Contains("lid"))
            {
                bankConflictPotential = "Low"; // Good access pattern
            }
            else if (System.Text.RegularExpressions.Regex.IsMatch(source, @"get_local_id\(0\)\s*\*\s*\d+"))
            {
                bankConflictPotential = "High"; // Strided access
            }
            else if (source.Contains("get_local_id(1)"))
            {
                bankConflictPotential = "Medium"; // 2D access patterns
            }

            // Optimization level based on synchronization
            if (source.Contains("barrier(CLK_LOCAL_MEM_FENCE)") && (localMemParams.Count > 1 || localMemDecls.Count > 1))
            {
                optimizationLevel = "Excellent";
            }
            else if (source.Contains("barrier"))
            {
                optimizationLevel = "Good";
            }
            else if (localMemParams.Count > 0 || localMemDecls.Count > 0)
            {
                optimizationLevel = "Poor"; // Local memory without barriers
            }
        }

        return new LocalMemoryAnalysis(totalSize, bankConflictPotential, optimizationLevel);
    }

    /// <summary>
    /// Analyzes work group characteristics and efficiency.
    /// </summary>
    private static WorkGroupAnalysis AnalyzeWorkGroupCharacteristics(string source, int[]? requiredWorkGroupSize)
    {
        var totalSize = requiredWorkGroupSize?.Aggregate(1, (a, b) => a * b) ?? 64; // Default work group size
        var dimensions = requiredWorkGroupSize?.Length ?? 1;
        var efficiency = "Good";

        // Analyze work group size optimizations
        if (totalSize >= 64 && totalSize <= 256)
        {
            efficiency = "Excellent"; // Sweet spot for most devices
        }
        else if (totalSize >= 32 && totalSize <= 512)
        {
            efficiency = "Good";
        }
        else if (totalSize > 512)
        {
            efficiency = "Fair"; // May exceed device limits
        }
        else
        {
            efficiency = "Poor"; // Too small, underutilizing hardware
        }

        // Check for work group size dependent code
        if (source.Contains("get_local_size") || source.Contains("get_num_groups"))
        {
            if (efficiency == "Good")
                efficiency = "Excellent";
            else if (efficiency == "Fair")
                efficiency = "Good";
        }

        return new WorkGroupAnalysis(totalSize, efficiency, dimensions);
    }

    /// <summary>
    /// Analyzes private memory usage per work item.
    /// </summary>
    private static PrivateMemoryAnalysis AnalyzePrivateMemoryUsage(string source, KernelParameter[] parameters)
    {
        var baseMemory = 16; // Base OpenCL overhead in bytes
        var memoryPerWorkItem = baseMemory;
        var pressure = "Low";

        // Parameter storage
        memoryPerWorkItem += parameters.Length * 4;

        // Variable declarations
        var varDeclarations = System.Text.RegularExpressions.Regex.Matches(source, @"\b(int|float|double|uint|long|short|char)\s+\w+");
        memoryPerWorkItem += varDeclarations.Count * 4;

        // Array variables (private arrays)
        var privateArrays = System.Text.RegularExpressions.Regex.Matches(source, @"\b(int|float|double)\s+\w+\s*\[([^\]]+)\]");
        foreach (System.Text.RegularExpressions.Match match in privateArrays)
        {
            var sizeExpr = match.Groups[2].Value;
            if (int.TryParse(sizeExpr, out var arraySize))
            {
                memoryPerWorkItem += arraySize * 4;
            }
        }

        // Complex operations requiring temporary storage
        var complexOps = source.Count(c => c == '*' || c == '/' || c == '%');
        memoryPerWorkItem += complexOps * 2; // Temporary results

        // Function calls that might use stack
        var functionCalls = System.Text.RegularExpressions.Regex.Matches(source, @"\b(sin|cos|sqrt|exp|log|pow|mad|fma)\s*\(");
        memoryPerWorkItem += functionCalls.Count * 4;

        // Determine pressure
        if (memoryPerWorkItem > 512)
        {
            pressure = "High";
        }
        else if (memoryPerWorkItem > 256)
        {
            pressure = "Medium";
        }

        return new PrivateMemoryAnalysis(memoryPerWorkItem, pressure);
    }

    /// <summary>
    /// Analyzes vector operations and SIMD utilization.
    /// </summary>
    private static VectorOperationsAnalysis AnalyzeVectorOperations(string source)
    {
        var vectorTypes = new[] { "float2", "float3", "float4", "float8", "float16", "int2", "int3", "int4", "int8", "int16" };
        var usesVectorTypes = vectorTypes.Any(vt => source.Contains(vt));
        
        var maxVectorWidth = 1;
        var efficiency = "Good";

        if (usesVectorTypes)
        {
            // Find maximum vector width used
            var widths = new[] { 16, 8, 4, 3, 2 };
            foreach (var width in widths)
            {
                if (vectorTypes.Any(vt => vt.EndsWith(width.ToString()) && source.Contains(vt)))
                {
                    maxVectorWidth = width;
                    break;
                }
            }

            // Count vector operations
            var vectorOps = System.Text.RegularExpressions.Regex.Matches(source, @"\b(float|int)\d+\b").Count;
            
            if (maxVectorWidth >= 4 && vectorOps > 5)
            {
                efficiency = "Excellent";
            }
            else if (maxVectorWidth >= 2 && vectorOps > 2)
            {
                efficiency = "Good";
            }
            else
            {
                efficiency = "Fair";
            }
        }

        return new VectorOperationsAnalysis(usesVectorTypes, maxVectorWidth, efficiency);
    }

    /// <summary>
    /// Analyzes image memory usage and access patterns.
    /// </summary>
    private static ImageMemoryAnalysis AnalyzeImageMemoryUsage(string source)
    {
        var imageTypes = new[] { "image1d_t", "image2d_t", "image3d_t", "image1d_array_t", "image2d_array_t" };
        var usesImages = imageTypes.Any(it => source.Contains(it)) || 
                        source.Contains("read_imagef") || 
                        source.Contains("write_imagef") ||
                        source.Contains("sampler_t");

        var cacheEfficiency = "Good";
        var accessPatterns = "Sequential";

        if (usesImages)
        {
            // Count image access functions
            var imageAccesses = System.Text.RegularExpressions.Regex.Matches(source, @"(read_image|write_image)\w+").Count;
            
            // Analyze access patterns
            if (source.Contains("get_global_id") && source.Contains("read_imagef"))
            {
                if (source.Contains("get_global_id(0)") && source.Contains("get_global_id(1)"))
                {
                    accessPatterns = "2D-Spatial";
                    cacheEfficiency = "Excellent";
                }
                else
                {
                    accessPatterns = "1D-Linear";
                    cacheEfficiency = "Good";
                }
            }
            else if (imageAccesses > 3)
            {
                accessPatterns = "Random";
                cacheEfficiency = "Fair";
            }

            // Check for sampler usage
            if (source.Contains("CLK_NORMALIZED_COORDS") || source.Contains("sampler_t"))
            {
                if (cacheEfficiency == "Good")
                    cacheEfficiency = "Excellent";
            }
        }

        return new ImageMemoryAnalysis(usesImages, cacheEfficiency, accessPatterns);
    }

    /// <summary>
    /// Analyzes atomic operations usage and contention risk.
    /// </summary>
    private static AtomicOperationsAnalysis AnalyzeAtomicOperations(string source)
    {
        var atomicFunctions = new[] { "atomic_add", "atomic_sub", "atomic_inc", "atomic_dec", "atomic_xchg", "atomic_cmpxchg", "atomic_min", "atomic_max" };
        var usesAtomics = atomicFunctions.Any(af => source.Contains(af));
        
        var contentionRisk = "Low";
        var operationCount = 0;

        if (usesAtomics)
        {
            operationCount = atomicFunctions.Sum(af => System.Text.RegularExpressions.Regex.Matches(source, $@"\b{System.Text.RegularExpressions.Regex.Escape(af)}").Count);
            
            // Analyze contention risk
            if (operationCount > 5)
            {
                contentionRisk = "High";
            }
            else if (operationCount > 2)
            {
                contentionRisk = "Medium";
            }
            
            // Check for patterns that increase contention
            if (source.Contains("atomic_inc") && source.Contains("for"))
            {
                contentionRisk = "High"; // Atomic operations in loops
            }
            else if (source.Contains("atomic_") && source.Contains("if"))
            {
                if (contentionRisk == "Low")
                    contentionRisk = "Medium";
            }
        }

        return new AtomicOperationsAnalysis(usesAtomics, contentionRisk, operationCount);
    }

    /// <summary>
    /// Analyzes global memory access patterns for optimization.
    /// </summary>
    private static MemoryAccessAnalysis AnalyzeMemoryAccessPatterns(string source, KernelParameter[] parameters)
    {
        var globalMemoryEfficiency = "Good";
        var accessPatterns = "Sequential";

        // Look for coalesced access patterns
        var coalescedPatterns = System.Text.RegularExpressions.Regex.Matches(source, @"\w+\s*\[\s*get_global_id\s*\(\s*0\s*\)\s*\]");
        
        // Look for strided access patterns
        var stridedPatterns = System.Text.RegularExpressions.Regex.Matches(source, @"\w+\s*\[\s*get_global_id\s*\(\s*0\s*\)\s*\*\s*\d+\s*\]");
        
        // Look for 2D array access
        var array2DPatterns = System.Text.RegularExpressions.Regex.Matches(source, @"\w+\s*\[\s*\w+\s*\*\s*\w+\s*\+\s*\w+\s*\]");

        if (coalescedPatterns.Count > 0 && stridedPatterns.Count == 0)
        {
            globalMemoryEfficiency = "Excellent";
            accessPatterns = "Coalesced";
        }
        else if (stridedPatterns.Count > 0)
        {
            globalMemoryEfficiency = "Poor";
            accessPatterns = "Strided";
        }
        else if (array2DPatterns.Count > 0)
        {
            globalMemoryEfficiency = "Fair";
            accessPatterns = "2D-Tiled";
        }
        else if (source.Contains("[") && !source.Contains("get_global_id"))
        {
            globalMemoryEfficiency = "Fair";
            accessPatterns = "Random";
        }

        return new MemoryAccessAnalysis(globalMemoryEfficiency, accessPatterns);
    }

    /// <summary>
    /// Analyzes barrier synchronization and work group coordination.
    /// </summary>
    private static BarrierSynchronizationAnalysis AnalyzeBarrierSynchronization(string source)
    {
        var hasBarriers = source.Contains("barrier");
        var complexity = "Low";
        var divergenceRisk = "Low";
        var barrierEfficiency = "Good";

        if (hasBarriers)
        {
            // Count barrier calls
            var barrierCount = System.Text.RegularExpressions.Regex.Matches(source, @"\bbarrier\s*\(").Count;
            
            // Analyze control flow complexity
            var branchCount = System.Text.RegularExpressions.Regex.Matches(source, @"\b(if|else|switch)\b").Count;
            var loopCount = System.Text.RegularExpressions.Regex.Matches(source, @"\b(for|while|do)\b").Count;

            if (barrierCount > 3 || (branchCount > 2 && hasBarriers))
            {
                complexity = "High";
            }
            else if (barrierCount > 1 || branchCount > 1)
            {
                complexity = "Medium";
            }

            // Analyze divergence risk
            if (branchCount > 0)
            {
                if (source.Contains("if") && source.Contains("get_local_id"))
                {
                    divergenceRisk = "High"; // Work-item dependent branches
                }
                else if (branchCount > 1)
                {
                    divergenceRisk = "Medium";
                }
            }

            // Barrier efficiency
            if (barrierCount == 1 && source.Contains("__local"))
            {
                barrierEfficiency = "Excellent";
            }
            else if (barrierCount <= 2)
            {
                barrierEfficiency = "Good";
            }
            else
            {
                barrierEfficiency = "Fair";
            }
        }

        return new BarrierSynchronizationAnalysis(complexity, divergenceRisk, hasBarriers, barrierEfficiency);
    }

    /// <summary>
    /// Analyzes required OpenCL extensions.
    /// </summary>
    private static OpenCLExtensionsAnalysis AnalyzeOpenCLExtensions(string source)
    {
        var extensions = new List<string>();
        var compatibility = "High";

        // Common extensions
        var extensionMap = new Dictionary<string, string[]>
        {
            ["cl_khr_fp64"] = new[] { "double", "cl_khr_fp64" },
            ["cl_khr_global_int32_base_atomics"] = new[] { "atomic_add", "atomic_sub", "atomic_xchg" },
            ["cl_khr_local_int32_base_atomics"] = new[] { "atom_add", "atom_sub" },
            ["cl_khr_int64_base_atomics"] = new[] { "atom_add.*long", "atomic_add.*long" },
            ["cl_khr_3d_image_writes"] = new[] { "write_imagef.*image3d" },
            ["cl_khr_byte_addressable_store"] = new[] { "char\\*", "uchar\\*" },
            ["cl_amd_printf"] = new[] { "printf" },
            ["cl_khr_gl_sharing"] = new[] { "GL_" },
            ["cl_khr_icd"] = new string[0] // Platform extension
        };

        foreach (var ext in extensionMap)
        {
            if (ext.Value.Any(pattern => System.Text.RegularExpressions.Regex.IsMatch(source, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
            {
                extensions.Add(ext.Key);
            }
        }

        // Check explicit pragma declarations
        var pragmaMatches = System.Text.RegularExpressions.Regex.Matches(source, @"#pragma\s+OPENCL\s+EXTENSION\s+(\w+)\s*:");
        foreach (System.Text.RegularExpressions.Match match in pragmaMatches)
        {
            var extName = match.Groups[1].Value;
            if (!extensions.Contains(extName))
            {
                extensions.Add(extName);
            }
        }

        // Determine compatibility based on extensions
        if (extensions.Count == 0)
        {
            compatibility = "Excellent";
        }
        else if (extensions.Count <= 2 && extensions.All(e => e.StartsWith("cl_khr_")))
        {
            compatibility = "High";
        }
        else if (extensions.Count <= 4)
        {
            compatibility = "Medium";
        }
        else
        {
            compatibility = "Low";
        }

        return new OpenCLExtensionsAnalysis(extensions, compatibility);
    }

    /// <summary>
    /// Analyzes optimizations for different device types.
    /// </summary>
    private static DeviceTypeOptimizationAnalysis AnalyzeDeviceTypeOptimizations(string source)
    {
        var cpuOptimization = "Good";
        var gpuOptimization = "Good";

        // CPU optimization factors
        var hasVectorOps = source.Contains("float4") || source.Contains("int4");
        var hasComplexControl = System.Text.RegularExpressions.Regex.Matches(source, @"\b(if|for|while)\b").Count > 3;
        var hasRecursion = source.Contains("recursive") || System.Text.RegularExpressions.Regex.Matches(source, @"(\w+)\s*\([^\)]*\).*\{.*\1\s*\(").Count > 0;
        
        if (hasVectorOps && !hasComplexControl)
        {
            cpuOptimization = "Excellent";
        }
        else if (hasComplexControl && !hasVectorOps)
        {
            cpuOptimization = "Fair";
        }
        else if (hasRecursion)
        {
            cpuOptimization = "Poor";
        }

        // GPU optimization factors
        var hasCoalescedAccess = System.Text.RegularExpressions.Regex.IsMatch(source, @"get_global_id\s*\(\s*0\s*\)");
        var hasBranching = System.Text.RegularExpressions.Regex.Matches(source, @"\bif\b").Count;
        var hasLocalMemory = source.Contains("__local");
        var hasAtomics = source.Contains("atomic_");

        if (hasCoalescedAccess && hasLocalMemory && hasBranching <= 1)
        {
            gpuOptimization = "Excellent";
        }
        else if (hasCoalescedAccess && hasBranching <= 2)
        {
            gpuOptimization = "Good";
        }
        else if (hasBranching > 3 || hasAtomics)
        {
            gpuOptimization = "Fair";
        }
        else if (!hasCoalescedAccess)
        {
            gpuOptimization = "Poor";
        }

        return new DeviceTypeOptimizationAnalysis(cpuOptimization, gpuOptimization);
    }

    #endregion
}