using System;
using System.Collections.Generic;

namespace DotCompute.Abstractions;

/// <summary>
/// Represents a compute kernel that can be compiled and executed on an accelerator.
/// Implementations should be structs for AOT compatibility and performance.
/// </summary>
public interface IKernel
{
    /// <summary>
    /// Gets the unique name of this kernel.
    /// </summary>
    static abstract string Name { get; }
    
    /// <summary>
    /// Gets the source code or IL representation of the kernel.
    /// </summary>
    static abstract string Source { get; }
    
    /// <summary>
    /// Gets the entry point method name for the kernel.
    /// </summary>
    static abstract string EntryPoint { get; }
    
    /// <summary>
    /// Gets the required shared memory size in bytes.
    /// </summary>
    static abstract int RequiredSharedMemory { get; }
}

/// <summary>
/// Defines the parameters for compiling a kernel.
/// </summary>
public sealed class KernelDefinition
{
    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// Gets the source code or IL.
    /// </summary>
    public string Source { get; }
    
    /// <summary>
    /// Gets the entry point method name.
    /// </summary>
    public string EntryPoint { get; }
    
    /// <summary>
    /// Gets the compilation options.
    /// </summary>
    public CompilationOptions Options { get; }
    
    /// <summary>
    /// Gets the kernel parameters metadata.
    /// </summary>
    public IReadOnlyList<KernelParameter> Parameters { get; }
    
    public KernelDefinition(
        string name,
        string source,
        string entryPoint,
        CompilationOptions options,
        IReadOnlyList<KernelParameter> parameters)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        EntryPoint = entryPoint ?? throw new ArgumentNullException(nameof(entryPoint));
        Options = options;
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }
    
    /// <summary>
    /// Creates a kernel definition from a kernel type.
    /// </summary>
    public static KernelDefinition FromKernel<TKernel>() where TKernel : IKernel
    {
        return new KernelDefinition(
            TKernel.Name,
            TKernel.Source,
            TKernel.EntryPoint,
            CompilationOptions.Default,
            Array.Empty<KernelParameter>());
    }
}

/// <summary>
/// Represents compilation options for a kernel.
/// </summary>
public sealed class CompilationOptions
{
    /// <summary>
    /// Gets the optimization level.
    /// </summary>
    public OptimizationLevel OptimizationLevel { get; init; }
    
    /// <summary>
    /// Gets whether to enable fast math operations.
    /// </summary>
    public bool EnableFastMath { get; init; }
    
    /// <summary>
    /// Gets whether to generate debug information.
    /// </summary>
    public bool GenerateDebugInfo { get; init; }
    
    /// <summary>
    /// Gets the target architecture.
    /// </summary>
    public string? TargetArchitecture { get; init; }
    
    /// <summary>
    /// Gets additional compiler flags.
    /// </summary>
    public IReadOnlyList<string> AdditionalFlags { get; init; }
    
    /// <summary>
    /// Gets preprocessor definitions.
    /// </summary>
    public IReadOnlyDictionary<string, string> Defines { get; init; }
    
    public CompilationOptions()
    {
        OptimizationLevel = OptimizationLevel.Release;
        EnableFastMath = true;
        GenerateDebugInfo = false;
        AdditionalFlags = Array.Empty<string>();
        Defines = new Dictionary<string, string>();
    }
    
    /// <summary>
    /// Gets default compilation options.
    /// </summary>
    public static CompilationOptions Default { get; } = new();
    
    /// <summary>
    /// Gets debug compilation options.
    /// </summary>
    public static CompilationOptions Debug { get; } = new()
    {
        OptimizationLevel = OptimizationLevel.Debug,
        GenerateDebugInfo = true,
        EnableFastMath = false
    };
}

/// <summary>
/// Defines optimization levels for kernel compilation.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>
    /// No optimizations, fastest compilation.
    /// </summary>
    None,
    
    /// <summary>
    /// Debug-friendly optimizations.
    /// </summary>
    Debug,
    
    /// <summary>
    /// Standard release optimizations.
    /// </summary>
    Release,
    
    /// <summary>
    /// Maximum optimizations, may increase compile time.
    /// </summary>
    Maximum
}

/// <summary>
/// Represents a kernel parameter.
/// </summary>
public sealed class KernelParameter
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// Gets the parameter type.
    /// </summary>
    public Type Type { get; }
    
    /// <summary>
    /// Gets whether this is an input parameter.
    /// </summary>
    public bool IsInput { get; }
    
    /// <summary>
    /// Gets whether this is an output parameter.
    /// </summary>
    public bool IsOutput { get; }
    
    /// <summary>
    /// Gets the parameter index.
    /// </summary>
    public int Index { get; }
    
    public KernelParameter(string name, Type type, int index, bool isInput = true, bool isOutput = false)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Index = index;
        IsInput = isInput;
        IsOutput = isOutput;
    }
}