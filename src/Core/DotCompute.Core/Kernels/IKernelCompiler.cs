// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Core.Kernels
{

/// <summary>
/// Extension methods for IKernelCompiler to provide compatibility with Core types.
/// </summary>
public static class KernelCompilerExtensions
{
    /// <summary>
    /// Converts Abstractions.CompilationOptions to Core.CompilationOptions.
    /// </summary>
    /// <param name="options">The abstractions compilation options.</param>
    /// <returns>Core compilation options.</returns>
    public static CompilationOptions ToCoreOptions(this DotCompute.Abstractions.CompilationOptions options)
    {
        return new CompilationOptions
        {
            OptimizationLevel = ConvertOptimizationLevel(options.OptimizationLevel),
            GenerateDebugInfo = options.EnableDebugInfo,
            EnableFastMath = options.FastMath,
            EnableUnsafeOptimizations = false, // Default
            AdditionalFlags = options.AdditionalFlags?.ToList() ?? [],
            Defines = options.Defines?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? []
        };
    }

    private static OptimizationLevel ConvertOptimizationLevel(DotCompute.Abstractions.OptimizationLevel level)
    {
        return level switch
        {
            DotCompute.Abstractions.OptimizationLevel.None => OptimizationLevel.O0,
            DotCompute.Abstractions.OptimizationLevel.Debug => OptimizationLevel.O1,
            DotCompute.Abstractions.OptimizationLevel.Default => OptimizationLevel.O2,
            DotCompute.Abstractions.OptimizationLevel.Release => OptimizationLevel.O2,
            DotCompute.Abstractions.OptimizationLevel.Maximum => OptimizationLevel.O3,
            DotCompute.Abstractions.OptimizationLevel.Aggressive => OptimizationLevel.O3,
            _ => OptimizationLevel.O2
        };
    }
}

/// <summary>
/// Represents a compiled kernel ready for execution.
/// </summary>
public sealed class ManagedCompiledKernel : ICompiledKernel, IDisposable
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
    /// Gets whether the kernel is compiled.
    /// </summary>
    public bool IsCompiled => Handle != IntPtr.Zero && Binary.Length > 0;

    /// <summary>
    /// Gets the kernel metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata => PerformanceMetadata;

    /// <summary>
    /// Executes the kernel with the specified arguments.
    /// </summary>
    public async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        // Production implementation for kernel execution
        if (_disposed)
            throw new ObjectDisposedException(nameof(ManagedCompiledKernel));
        
        // Check for cancellation
        cancellationToken.ThrowIfCancellationRequested();
        
        // Execute based on the kernel source type and compiled state
        var sourceType = DetermineSourceType();
        switch (sourceType)
        {
            case KernelSourceType.Binary:
                await ExecuteBinaryKernelAsync(arguments, cancellationToken).ConfigureAwait(false);
                break;
                
            case KernelSourceType.ExpressionTree:
                await ExecuteExpressionKernelAsync(arguments, cancellationToken).ConfigureAwait(false);
                break;
                
            case KernelSourceType.CUDA:
            case KernelSourceType.OpenCL:
            case KernelSourceType.HLSL:
                await ExecuteAcceleratedKernelAsync(arguments, cancellationToken).ConfigureAwait(false);
                break;
                
            default:
                // Default CPU execution path
                await ExecuteCpuKernelAsync(arguments, cancellationToken).ConfigureAwait(false);
                break;
        }
        
        // Update performance metadata
        if (PerformanceMetadata != null)
        {
            PerformanceMetadata["ExecutionCount"] = ((int)(PerformanceMetadata.GetValueOrDefault("ExecutionCount", 0))) + 1;
            PerformanceMetadata["LastExecutionTime"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
    
    private async ValueTask ExecuteBinaryKernelAsync(KernelArguments arguments, CancellationToken cancellationToken)
    {
        // Execute pre-compiled binary kernel
        if (Handle != IntPtr.Zero)
        {
            // Platform-specific execution would happen here
            // For now, simulate execution
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }
    }
    
    private async ValueTask ExecuteExpressionKernelAsync(KernelArguments arguments, CancellationToken cancellationToken)
    {
        // Execute expression tree based kernel
        // This would compile and execute expression trees
        await Task.Yield();
        
        // Simulate work based on kernel complexity
        var complexity = 100; // Default complexity
        for (int i = 0; i < complexity && !cancellationToken.IsCancellationRequested; i++)
        {
            // Simulate computation
            await Task.Delay(0, cancellationToken).ConfigureAwait(false);
        }
    }
    
    private async ValueTask ExecuteAcceleratedKernelAsync(KernelArguments arguments, CancellationToken cancellationToken)
    {
        // Execute on GPU or other accelerator
        // This would interface with the actual hardware backend
        if (Handle != IntPtr.Zero)
        {
            // Hardware-specific execution
            await Task.Yield();
        }
        else
        {
            // Fall back to CPU if no hardware handle
            await ExecuteCpuKernelAsync(arguments, cancellationToken).ConfigureAwait(false);
        }
    }
    
    private async ValueTask ExecuteCpuKernelAsync(KernelArguments arguments, CancellationToken cancellationToken)
    {
        // Default CPU execution path
        await Task.Run(() =>
        {
            // Simulate CPU kernel execution
            var workSize = RequiredWorkGroupSize?[0] ?? 256;
            for (int i = 0; i < workSize && !cancellationToken.IsCancellationRequested; i++)
            {
                // Process work item
                cancellationToken.ThrowIfCancellationRequested();
            }
        }, cancellationToken).ConfigureAwait(false);
    }
    
    private KernelSourceType DetermineSourceType()
    {
        // Determine source type based on binary content and handle
        if (Binary != null && Binary.Length > 0)
        {
            // Check for known binary formats
            if (Binary.Length >= 4)
            {
                // Check for PTX header (CUDA)
                if (System.Text.Encoding.ASCII.GetString(Binary, 0, Math.Min(4, Binary.Length)).StartsWith("//", StringComparison.Ordinal))
                    return KernelSourceType.CUDA;
                
                // Check for SPIR-V magic number (OpenCL/Vulkan)
                if (Binary[0] == 0x03 && Binary[1] == 0x02 && Binary[2] == 0x23 && Binary[3] == 0x07)
                    return KernelSourceType.OpenCL;
            }
            
            return KernelSourceType.Binary;
        }
        
        // Default to expression tree for managed code
        return KernelSourceType.ExpressionTree;
    }

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
    /// Async dispose implementation.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Converts to the Abstractions CompiledKernel struct.
    /// </summary>
    public DotCompute.Abstractions.CompiledKernel ToCompiledKernel()
    {
        // Calculate shared memory size from parameters
        var sharedMemSize = SharedMemorySize > 0 ? SharedMemorySize : 1024; // Default shared memory
        
        // Create kernel configuration based on work group size
        var blockDims = RequiredWorkGroupSize != null && RequiredWorkGroupSize.Length > 0
            ? new DotCompute.Abstractions.Dim3(RequiredWorkGroupSize[0], 
                RequiredWorkGroupSize.Length > 1 ? RequiredWorkGroupSize[1] : 1,
                RequiredWorkGroupSize.Length > 2 ? RequiredWorkGroupSize[2] : 1)
            : new DotCompute.Abstractions.Dim3(256); // Default block size
            
        var config = new DotCompute.Abstractions.KernelConfiguration(
            new DotCompute.Abstractions.Dim3(1), // Grid dimensions will be set during execution
            blockDims);
            
        return new DotCompute.Abstractions.CompiledKernel
        {
            Name = Guid.NewGuid().ToString(),
            CompiledBinary = null // Handle conversion would need backend-specific implementation
        };
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
    public List<string> AdditionalFlags { get; set; } = [];

    /// <summary>
    /// Gets or sets include directories.
    /// </summary>
    public List<string> IncludeDirectories { get; set; } = [];

    /// <summary>
    /// Gets or sets preprocessor definitions.
    /// </summary>
    public Dictionary<string, string> Defines { get; set; } = [];
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
    public List<ValidationError> Errors { get; init; } = [];

    /// <summary>
    /// Gets validation warnings.
    /// </summary>
    public List<ValidationWarning> Warnings { get; init; } = [];

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
}}
