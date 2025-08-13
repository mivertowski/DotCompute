// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Backends.CUDA.Types;

/// <summary>
/// CUDA-specific wrapper for KernelArguments to provide CUDA-specific functionality
/// </summary>
public sealed class CudaKernelArguments
{
    private readonly object[] _arguments;

    /// <summary>
    /// Initializes a new instance of KernelArguments with the specified arguments.
    /// </summary>
    /// <param name="args">The kernel arguments.</param>
    public CudaKernelArguments(params object[] args)
    {
        _arguments = args ?? throw new ArgumentNullException(nameof(args));
    }

    /// <summary>
    /// Gets the argument at the specified index.
    /// </summary>
    /// <param name="index">The index of the argument.</param>
    /// <returns>The argument value.</returns>
    public object this[int index] => _arguments[index];

    /// <summary>
    /// Gets the number of arguments.
    /// </summary>
    public int Count => _arguments.Length;

    /// <summary>
    /// Gets all arguments as an array.
    /// </summary>
    public object[] ToArray() => _arguments.ToArray();

    /// <summary>
    /// Converts from core KernelArgument array to CUDA KernelArguments.
    /// </summary>
    /// <param name="arguments">The core kernel arguments.</param>
    /// <returns>CUDA-specific kernel arguments.</returns>
    public static CudaKernelArguments FromCore(DotCompute.Core.Kernels.KernelArgument[] arguments)
    {
        var args = arguments.Select(arg => arg.Value).ToArray();
        return new CudaKernelArguments(args);
    }

    /// <summary>
    /// Converts from core KernelArguments struct to CUDA KernelArguments class.
    /// </summary>
    /// <param name="arguments">The core kernel arguments.</param>
    /// <returns>CUDA-specific kernel arguments.</returns>
    public static CudaKernelArguments FromCore(DotCompute.Abstractions.KernelArguments arguments)
    {
        return new CudaKernelArguments(arguments.Arguments.ToArray());
    }

    /// <summary>
    /// Converts to core KernelArguments struct.
    /// </summary>
    /// <returns>Core kernel arguments.</returns>
    public DotCompute.Abstractions.KernelArguments ToCore()
    {
        return new DotCompute.Abstractions.KernelArguments(_arguments);
    }
}

/// <summary>
/// Compilation exception for kernel compilation failures
/// </summary>
public class KernelCompilationException : Exception
{
    /// <summary>
    /// Gets the compiler log if available.
    /// </summary>
    public string? CompilerLog { get; }

    public KernelCompilationException() : base() { }

    public KernelCompilationException(string message) : base(message) { }

    public KernelCompilationException(string message, Exception innerException) : base(message, innerException) { }

    public KernelCompilationException(string message, string? compilerLog) : base(message)
    {
        CompilerLog = compilerLog;
    }

    public KernelCompilationException(string message, string? compilerLog, Exception innerException) : base(message, innerException)
    {
        CompilerLog = compilerLog;
    }
}


/// <summary>
/// CUDA memory statistics for tracking memory usage
/// </summary>
public sealed class CudaMemoryStatistics
{
    public long TotalMemoryBytes { get; set; }
    public long UsedMemoryBytes { get; set; }
    public long FreeMemoryBytes => TotalMemoryBytes - UsedMemoryBytes;
    public int AllocationCount { get; set; }
    public int DeallocationCount { get; set; }
    public long PeakUsageBytes { get; set; }
    public double FragmentationRatio { get; set; }
    public TimeSpan TotalAllocationTime { get; set; }
    public TimeSpan TotalDeallocationTime { get; set; }
}

/// <summary>
/// CUDA-specific validation result
/// </summary>
internal sealed class ValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string>? Warnings { get; set; }

    private ValidationResult() { }

    public static ValidationResult Success() => new() { IsValid = true };

    public static ValidationResult SuccessWithWarnings(params string[] warnings) => new()
    {
        IsValid = true,
        Warnings = warnings.ToList()
    };
    
    public static ValidationResult Success(string? warningMessage) => new()
    {
        IsValid = true,
        Warnings = warningMessage != null ? new List<string> { warningMessage } : null
    };

    public static ValidationResult Failure(string errorMessage) => new()
    {
        IsValid = false,
        ErrorMessage = errorMessage
    };
}

/// <summary>
/// Cache statistics for CUDA kernel compiler
/// </summary>
public sealed class CacheStatistics
{
    public int TotalEntries { get; set; }
    public long TotalSizeBytes { get; set; }
    public double AverageAccessCount { get; set; }
    public DateTime? OldestEntryTime { get; set; }
    public DateTime? NewestEntryTime { get; set; }
    public double HitRate { get; set; }
}