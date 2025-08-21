// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Core.Recovery.Compilation;

/// <summary>
/// Context information for compilation recovery operations, containing all data
/// necessary for attempting kernel compilation with various fallback strategies.
/// </summary>
/// <remarks>
/// The compilation recovery context serves as a container for all information
/// needed during the compilation fallback process. It includes:
/// - Original kernel source code and compilation parameters
/// - Target platform and execution environment details
/// - Flags indicating the type of failure (timeout, compilation error, etc.)
/// - Modified compilation options and interpreter instances for fallback attempts
/// - Cached results that may be used as alternatives
/// 
/// The context can be cloned to preserve original state while attempting
/// different recovery strategies.
/// </remarks>
public class CompilationRecoveryContext
{
    /// <summary>
    /// Gets or sets the name or identifier of the kernel being compiled.
    /// </summary>
    /// <value>The kernel identifier used for logging and caching.</value>
    public string KernelName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the source code of the kernel to be compiled.
    /// </summary>
    /// <value>The complete kernel source code.</value>
    public string SourceCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the original compilation options requested for this kernel.
    /// </summary>
    /// <value>The initial compilation configuration.</value>
    public CompilationOptions CompilationOptions { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the target platform for compilation (e.g., CUDA, OpenCL, CPU).
    /// </summary>
    /// <value>The platform identifier for the compilation target.</value>
    public string TargetPlatform { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets a value indicating whether the compilation failure was due to a timeout.
    /// </summary>
    /// <value>true if the compilation timed out; otherwise, false.</value>
    public bool IsCompilationTimeout { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether the kernel has been simplified during recovery.
    /// </summary>
    /// <value>true if kernel simplification has been applied; otherwise, false.</value>
    public bool IsSimplified { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether interpreter mode should be used for execution.
    /// </summary>
    /// <value>true if interpreter mode is enabled; otherwise, false.</value>
    public bool UseInterpreter { get; set; }
    
    /// <summary>
    /// Gets or sets the path to the plugin responsible for this kernel, if applicable.
    /// </summary>
    /// <value>The file path to the plugin, or null if not plugin-based.</value>
    public string? PluginPath { get; set; }

    /// <summary>
    /// Gets or sets the modified compilation options used during recovery attempts.
    /// These options may differ from the original options due to fallback strategies.
    /// </summary>
    /// <value>The modified compilation configuration, or null if not modified.</value>
    public CompilationOptions? ModifiedOptions { get; set; }
    
    /// <summary>
    /// Gets or sets the interpreter instance created for fallback execution.
    /// </summary>
    /// <value>The kernel interpreter instance, or null if not using interpreter mode.</value>
    public KernelInterpreter? InterpreterInstance { get; set; }
    
    /// <summary>
    /// Gets or sets the cached compilation result that may be used as a fallback.
    /// </summary>
    /// <value>The cached result, or null if no suitable cache entry exists.</value>
    public CachedCompilationResult? CachedResult { get; set; }

    /// <summary>
    /// Creates a deep copy of the compilation recovery context, preserving original values
    /// while allowing modifications for different recovery attempts.
    /// </summary>
    /// <returns>A new instance of <see cref="CompilationRecoveryContext"/> with copied values.</returns>
    /// <remarks>
    /// The clone operation creates a new context with the same base properties but
    /// resets the recovery-specific fields (ModifiedOptions, InterpreterInstance, CachedResult)
    /// to allow fresh recovery attempts.
    /// </remarks>
    public CompilationRecoveryContext Clone()
    {
        return new CompilationRecoveryContext
        {
            KernelName = KernelName,
            SourceCode = SourceCode,
            CompilationOptions = CompilationOptions.Clone(),
            TargetPlatform = TargetPlatform,
            IsCompilationTimeout = IsCompilationTimeout,
            IsSimplified = IsSimplified,
            UseInterpreter = UseInterpreter,
            PluginPath = PluginPath
        };
    }
}