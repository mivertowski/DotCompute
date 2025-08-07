// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Core.Kernels;

/// <summary>
/// Interface for compiling kernels for specific accelerators.
/// </summary>
public interface IKernelCompiler
{
    /// <summary>
    /// Gets the supported accelerator type.
    /// </summary>
    AcceleratorType AcceleratorType { get; }

    /// <summary>
    /// Compiles a kernel from source code.
    /// </summary>
    /// <param name="kernel">The generated kernel to compile.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The compiled kernel.</returns>
    ValueTask<ManagedCompiledKernel> CompileAsync(GeneratedKernel kernel, CompilationOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates kernel source code.
    /// </summary>
    /// <param name="kernel">The kernel to validate.</param>
    /// <returns>Validation result with any errors or warnings.</returns>
    KernelValidationResult Validate(GeneratedKernel kernel);

    /// <summary>
    /// Gets the default compilation options for this compiler.
    /// </summary>
    /// <returns>Default compilation options.</returns>
    CompilationOptions GetDefaultOptions();
}

/// <summary>
/// Represents a compiled kernel ready for execution.
/// </summary>
public sealed class ManagedCompiledKernel : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the compiled binary data.
    /// </summary>
    public required byte[] Binary { get; init; }

    /// <summary>
    /// Gets the kernel handle (platform-specific).
    /// </summary>
    public IntPtr Handle { get; init; }

    /// <summary>
    /// Gets the kernel parameters.
    /// </summary>
    public required KernelParameter[] Parameters { get; init; }

    /// <summary>
    /// Gets the required work group size.
    /// </summary>
    public int[]? RequiredWorkGroupSize { get; init; }

    /// <summary>
    /// Gets the shared memory size in bytes.
    /// </summary>
    public int SharedMemorySize { get; init; }

    /// <summary>
    /// Gets the compilation log.
    /// </summary>
    public string? CompilationLog { get; init; }

    /// <summary>
    /// Gets performance metadata from compilation.
    /// </summary>
    public Dictionary<string, object>? PerformanceMetadata { get; init; }

    /// <summary>
    /// Disposes the compiled kernel.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            if (Handle != IntPtr.Zero)
            {
                // Platform-specific cleanup would go here
                // For now, we'll just mark as disposed
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Converts to the Abstractions CompiledKernel struct.
    /// </summary>
    public DotCompute.Abstractions.CompiledKernel ToCompiledKernel()
    {
        // Calculate shared memory size from parameters
        int sharedMemSize = SharedMemorySize > 0 ? SharedMemorySize : 1024; // Default shared memory
        
        // Create kernel configuration based on work group size
        var blockDims = RequiredWorkGroupSize != null && RequiredWorkGroupSize.Length > 0
            ? new DotCompute.Abstractions.Dim3(RequiredWorkGroupSize[0], 
                RequiredWorkGroupSize.Length > 1 ? RequiredWorkGroupSize[1] : 1,
                RequiredWorkGroupSize.Length > 2 ? RequiredWorkGroupSize[2] : 1)
            : new DotCompute.Abstractions.Dim3(256); // Default block size
            
        var config = new DotCompute.Abstractions.KernelConfiguration(
            new DotCompute.Abstractions.Dim3(1), // Grid dimensions will be set during execution
            blockDims);
            
        return new DotCompute.Abstractions.CompiledKernel(
            Guid.NewGuid(),
            Handle,
            sharedMemSize,
            config);
    }
}

/// <summary>
/// Compilation options for kernel compilation.
/// </summary>
public sealed class CompilationOptions
{
    /// <summary>
    /// Gets or sets the optimization level.
    /// </summary>
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.O2;

    /// <summary>
    /// Gets or sets whether to generate debug information.
    /// </summary>
    public bool GenerateDebugInfo { get; set; }

    /// <summary>
    /// Gets or sets whether to enable fast math operations.
    /// </summary>
    public bool EnableFastMath { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable finite math only.
    /// </summary>
    public bool FiniteMathOnly { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable unsafe optimizations.
    /// </summary>
    public bool EnableUnsafeOptimizations { get; set; }

    /// <summary>
    /// Gets or sets the target architecture (e.g., sm_75 for CUDA).
    /// </summary>
    public string? TargetArchitecture { get; set; }

    /// <summary>
    /// Gets or sets additional compiler flags.
    /// </summary>
    public List<string> AdditionalFlags { get; set; } = new();

    /// <summary>
    /// Gets or sets include directories.
    /// </summary>
    public List<string> IncludeDirectories { get; set; } = new();

    /// <summary>
    /// Gets or sets preprocessor definitions.
    /// </summary>
    public Dictionary<string, string> Defines { get; set; } = new();
}

/// <summary>
/// Kernel validation result.
/// </summary>
public sealed class KernelValidationResult
{
    /// <summary>
    /// Gets whether the kernel is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets validation errors.
    /// </summary>
    public List<ValidationError> Errors { get; init; } = new();

    /// <summary>
    /// Gets validation warnings.
    /// </summary>
    public List<ValidationWarning> Warnings { get; init; } = new();

    /// <summary>
    /// Gets resource usage estimates.
    /// </summary>
    public ResourceUsageEstimate? ResourceUsage { get; init; }
}

/// <summary>
/// Validation error.
/// </summary>
public sealed class ValidationError
{
    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets or sets the line number where the error occurred.
    /// </summary>
    public int? Line { get; init; }

    /// <summary>
    /// Gets or sets the column number where the error occurred.
    /// </summary>
    public int? Column { get; init; }
}

/// <summary>
/// Validation warning.
/// </summary>
public sealed class ValidationWarning
{
    /// <summary>
    /// Gets or sets the warning code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets or sets the warning message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets or sets the line number where the warning occurred.
    /// </summary>
    public int? Line { get; init; }

    /// <summary>
    /// Gets or sets the column number where the warning occurred.
    /// </summary>
    public int? Column { get; init; }

    /// <summary>
    /// Gets or sets the severity level.
    /// </summary>
    public WarningSeverity Severity { get; init; } = WarningSeverity.Warning;
}

/// <summary>
/// Resource usage estimate.
/// </summary>
public sealed class ResourceUsageEstimate
{
    /// <summary>
    /// Gets or sets the register count per thread.
    /// </summary>
    public int RegistersPerThread { get; init; }

    /// <summary>
    /// Gets or sets the shared memory per block in bytes.
    /// </summary>
    public int SharedMemoryPerBlock { get; init; }

    /// <summary>
    /// Gets or sets the constant memory usage in bytes.
    /// </summary>
    public int ConstantMemoryUsage { get; init; }

    /// <summary>
    /// Gets or sets the maximum threads per block.
    /// </summary>
    public int MaxThreadsPerBlock { get; init; }

    /// <summary>
    /// Gets or sets the occupancy estimate (0-1).
    /// </summary>
    public float OccupancyEstimate { get; init; }
}

/// <summary>
/// Optimization level for kernel compilation.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>
    /// No optimization
    /// </summary>
    O0,

    /// <summary>
    /// Basic optimization
    /// </summary>
    O1,

    /// <summary>
    /// Standard optimization
    /// </summary>
    O2,

    /// <summary>
    /// Aggressive optimization
    /// </summary>
    O3,

    /// <summary>
    /// Size optimization
    /// </summary>
    Os
}

/// <summary>
/// Warning severity levels.
/// </summary>
public enum WarningSeverity
{
    /// <summary>
    /// Informational message
    /// </summary>
    Info,

    /// <summary>
    /// Warning
    /// </summary>
    Warning,

    /// <summary>
    /// Serious warning
    /// </summary>
    Serious
}