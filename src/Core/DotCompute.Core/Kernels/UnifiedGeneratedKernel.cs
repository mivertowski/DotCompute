// <copyright file="UnifiedGeneratedKernel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions.Analysis;

namespace DotCompute.Core.Kernels;

/// <summary>
/// Unified implementation of generated kernel that consolidates all functionality
/// from different kernel implementations across the codebase.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="UnifiedGeneratedKernel"/> class.
/// </remarks>
public sealed class UnifiedGeneratedKernel(
    string name,
    string sourceCode,
    string language = "C",
    string targetBackend = "CPU",
    string entryPoint = "main") : IFullGeneratedKernel
{
    private readonly Dictionary<string, object> _metadata = [];
    private readonly List<IKernelParameter> _parameters = [];
    private readonly List<string> _optimizations = [];
    private bool _disposed;
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>

    // IGeneratedKernel properties
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    /// <summary>
    /// Gets or sets the source code.
    /// </summary>
    /// <value>The source code.</value>
    public string SourceCode { get; } = sourceCode ?? throw new ArgumentNullException(nameof(sourceCode));
    /// <summary>
    /// Gets or sets the language.
    /// </summary>
    /// <value>The language.</value>
    public string Language { get; } = language;
    /// <summary>
    /// Gets or sets the target backend.
    /// </summary>
    /// <value>The target backend.</value>
    public string TargetBackend { get; } = targetBackend;
    /// <summary>
    /// Gets or sets the entry point.
    /// </summary>
    /// <value>The entry point.</value>
    public string EntryPoint { get; } = entryPoint;
    /// <summary>
    /// Gets or sets the metadata.
    /// </summary>
    /// <value>The metadata.</value>
    public IReadOnlyDictionary<string, object> Metadata => _metadata;
    /// <summary>
    /// Gets or sets the compiled kernel.
    /// </summary>
    /// <value>The compiled kernel.</value>

    // IExecutableGeneratedKernel properties
    public ICompiledKernel? CompiledKernel { get; private set; }
    /// <summary>
    /// Gets or sets a value indicating whether compiled.
    /// </summary>
    /// <value>The is compiled.</value>
    public bool IsCompiled => CompiledKernel?.IsValid == true;
    /// <summary>
    /// Gets or sets the parameters.
    /// </summary>
    /// <value>The parameters.</value>
    public IReadOnlyList<IKernelParameter> Parameters => _parameters;
    /// <summary>
    /// Gets or sets the analysis.
    /// </summary>
    /// <value>The analysis.</value>

    // IAnalyzableGeneratedKernel properties
    public IExpressionAnalysisResult? Analysis { get; private set; }
    /// <summary>
    /// Gets or sets the optimizations.
    /// </summary>
    /// <value>The optimizations.</value>
    public IReadOnlyList<string> Optimizations => _optimizations;
    /// <summary>
    /// Gets or sets the complexity metrics.
    /// </summary>
    /// <value>The complexity metrics.</value>
    public IComplexityMetrics ComplexityMetrics => Analysis?.ComplexityMetrics ?? Core.Analysis.UnifiedComplexityMetrics.Builder().Build();
    /// <summary>
    /// Gets or sets the memory manager.
    /// </summary>
    /// <value>The memory manager.</value>

    // IFullGeneratedKernel properties
    public IGpuMemoryManager? MemoryManager { get; private set; }
    /// <summary>
    /// Gets or sets the compiled at.
    /// </summary>
    /// <value>The compiled at.</value>
    public DateTimeOffset CompiledAt { get; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    /// <value>The version.</value>
    public Version Version { get; } = new Version(1, 0, 0, 0);

    /// <summary>
    /// Sets the compiled kernel instance.
    /// </summary>
    public UnifiedGeneratedKernel WithCompiledKernel(ICompiledKernel compiledKernel)
    {
        CompiledKernel = compiledKernel;
        return this;
    }

    /// <summary>
    /// Sets the analysis result.
    /// </summary>
    public UnifiedGeneratedKernel WithAnalysis(IExpressionAnalysisResult analysis)
    {
        Analysis = analysis;
        return this;
    }

    /// <summary>
    /// Sets the memory manager.
    /// </summary>
    public UnifiedGeneratedKernel WithMemoryManager(IGpuMemoryManager memoryManager)
    {
        MemoryManager = memoryManager;
        return this;
    }

    /// <summary>
    /// Adds a parameter to the kernel.
    /// </summary>
    public UnifiedGeneratedKernel AddParameter(IKernelParameter parameter)
    {
        _parameters.Add(parameter ?? throw new ArgumentNullException(nameof(parameter)));
        return this;
    }

    /// <summary>
    /// Adds multiple parameters to the kernel.
    /// </summary>
    public UnifiedGeneratedKernel AddParameters(IEnumerable<IKernelParameter> parameters)
    {
        _parameters.AddRange(parameters ?? throw new ArgumentNullException(nameof(parameters)));
        return this;
    }

    /// <summary>
    /// Adds metadata to the kernel.
    /// </summary>
    public UnifiedGeneratedKernel AddMetadata(string key, object value)
    {
        _metadata[key ?? throw new ArgumentNullException(nameof(key))] = value;
        return this;
    }

    /// <summary>
    /// Adds multiple metadata entries to the kernel.
    /// </summary>
    public UnifiedGeneratedKernel AddMetadata(IEnumerable<KeyValuePair<string, object>> metadata)
    {
        foreach (var kvp in metadata ?? throw new ArgumentNullException(nameof(metadata)))
        {
            _metadata[kvp.Key] = kvp.Value;
        }
        return this;
    }

    /// <summary>
    /// Adds an optimization to the kernel.
    /// </summary>
    public UnifiedGeneratedKernel AddOptimization(string optimization)
    {
        _optimizations.Add(optimization ?? throw new ArgumentNullException(nameof(optimization)));
        return this;
    }

    /// <summary>
    /// Adds multiple optimizations to the kernel.
    /// </summary>
    public UnifiedGeneratedKernel AddOptimizations(IEnumerable<string> optimizations)
    {
        _optimizations.AddRange(optimizations ?? throw new ArgumentNullException(nameof(optimizations)));
        return this;
    }

    /// <summary>
    /// Executes the kernel with the given parameters.
    /// </summary>
    public async Task ExecuteAsync(params object[] parameters)
    {
        ThrowIfDisposed();

        if (!IsCompiled)
        {
            throw new InvalidOperationException("Kernel is not compiled and ready for execution.");
        }

        if (parameters.Length != Parameters.Count)
        {
            throw new ArgumentException($"Expected {Parameters.Count} parameters, but got {parameters.Length}.", nameof(parameters));
        }

        // Parameter validation
        for (var i = 0; i < Parameters.Count; i++)
        {
            var expectedType = Parameters[i].Type;
            var actualValue = parameters[i];

            if (actualValue != null && !expectedType.IsInstanceOfType(actualValue))
            {
                throw new ArgumentException($"Parameter {i} expected type {expectedType.Name}, but got {actualValue.GetType().Name}.", nameof(parameters));
            }
        }

        // TODO: Implement actual kernel execution based on target backend
        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates a copy of this kernel with the specified modifications.
    /// </summary>
    public UnifiedGeneratedKernel Clone()
    {
        var clone = new UnifiedGeneratedKernel(Name, SourceCode, Language, TargetBackend, EntryPoint)
            .WithCompiledKernel(CompiledKernel!)
            .WithAnalysis(Analysis!)
            .WithMemoryManager(MemoryManager!)
            .AddParameters(_parameters)
            .AddMetadata(_metadata)
            .AddOptimizations(_optimizations);

        return clone;
    }

    /// <summary>
    /// Disposes the kernel and releases associated resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        CompiledKernel?.Dispose();
        MemoryManager?.Dispose();

        _disposed = true;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

/// <summary>
/// Implementation of IKernelParameter for unified kernel parameters.
/// </summary>
public sealed class UnifiedKernelParameter(
    string name,
    Type type,
    bool isPointer = false,
    bool isInput = true,
    bool isOutput = false) : IKernelParameter
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    /// <value>The type.</value>
    public Type Type { get; } = type ?? throw new ArgumentNullException(nameof(type));
    /// <summary>
    /// Gets or sets a value indicating whether pointer.
    /// </summary>
    /// <value>The is pointer.</value>
    public bool IsPointer { get; } = isPointer;
    /// <summary>
    /// Gets or sets a value indicating whether input.
    /// </summary>
    /// <value>The is input.</value>
    public bool IsInput { get; } = isInput;
    /// <summary>
    /// Gets or sets a value indicating whether output.
    /// </summary>
    /// <value>The is output.</value>
    public bool IsOutput { get; } = isOutput;
    /// <summary>
    /// Gets to string.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public override string ToString() => $"{Type.Name} {Name}{(IsPointer ? "*" : "")}{(IsOutput ? " [out]" : "")}{(IsInput ? " [in]" : "")}";
}

/// <summary>
/// Factory class for creating unified generated kernels.
/// </summary>
public static class UnifiedGeneratedKernelFactory
{
    /// <summary>
    /// Creates a new UnifiedGeneratedKernel instance.
    /// </summary>
    public static UnifiedGeneratedKernel Create(
        string name,
        string sourceCode,
        string language = "C",
        string targetBackend = "CPU",
        string entryPoint = "main") => new(name, sourceCode, language, targetBackend, entryPoint);

    /// <summary>
    /// Creates a GPU kernel instance.
    /// </summary>
    public static UnifiedGeneratedKernel CreateGpuKernel(
        string name,
        string cudaSourceCode,
        string entryPoint = "kernel_main") => new(name, cudaSourceCode, "CUDA", "CUDA", entryPoint);

    /// <summary>
    /// Creates a CPU kernel instance.
    /// </summary>
    public static UnifiedGeneratedKernel CreateCpuKernel(
        string name,
        string cSourceCode,
        string entryPoint = "main") => new(name, cSourceCode, "C", "CPU", entryPoint);

    /// <summary>
    /// Creates a parameter for the kernel.
    /// </summary>
    public static UnifiedKernelParameter CreateParameter<T>(
        string name,
        bool isPointer = false,
        bool isInput = true,
        bool isOutput = false) => new(name, typeof(T), isPointer, isInput, isOutput);

    /// <summary>
    /// Creates multiple parameters from type specifications.
    /// </summary>
    public static IEnumerable<UnifiedKernelParameter> CreateParameters(params (string name, Type type, bool isPointer, bool isInput, bool isOutput)[] paramSpecs) => paramSpecs.Select(spec => new UnifiedKernelParameter(spec.name, spec.type, spec.isPointer, spec.isInput, spec.isOutput));
}
