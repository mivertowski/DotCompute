// <copyright file="KernelMetadata.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using DotCompute.Abstractions.Kernels.Types;
using System.Collections.Generic;
using System.Linq.Expressions;
using DotCompute.Abstractions.Types;
using DotCompute.Linq.Operators.Models;
using DotCompute.Linq.Operators;
using Models = DotCompute.Linq.Operators.Models;
namespace DotCompute.Linq.KernelGeneration;
/// <summary>
/// Contains comprehensive metadata about a kernel for compilation and optimization.
/// This includes performance hints, resource requirements, and compilation options.
/// </summary>
public sealed class KernelMetadata : IEquatable<KernelMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KernelMetadata"/> class.
    /// </summary>
    /// <param name="name">The kernel name.</param>
    /// <param name="language">The target kernel language.</param>
    /// <exception cref="ArgumentException">Thrown when name is null or whitespace.</exception>
    public KernelMetadata(string name, DotCompute.Abstractions.Kernels.Types.KernelLanguage language)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Kernel name cannot be null or whitespace.", nameof(name));
        }
        Name = name;
        Language = language;
        CompilationHints = [];
        PerformanceHints = [];
        CreatedAt = DateTimeOffset.UtcNow;
    }
    /// Gets the kernel name.
    public string Name { get; }
    /// Gets the target kernel language.
    public DotCompute.Abstractions.Kernels.Types.KernelLanguage Language { get; }
    /// Gets or sets the optimization level for compilation.
    public Models.OptimizationLevel OptimizationLevel { get; set; } = Models.OptimizationLevel.O2;
    /// Gets or sets the precision mode for floating-point operations.
    public PrecisionMode PrecisionMode { get; set; } = PrecisionMode.Default;
    /// Gets or sets the expected work group size for optimization.
    public int[]? WorkGroupSize { get; set; }
    /// Gets or sets the maximum number of registers per thread.
    public int? MaxRegistersPerThread { get; set; }
    /// Gets or sets the shared memory size in bytes.
    public int SharedMemorySize { get; set; }
    /// Gets or sets the constant memory size in bytes.
    public int ConstantMemorySize { get; set; }
    /// Gets or sets whether the kernel uses fast math optimizations.
    public bool UseFastMath { get; set; }
    /// Gets or sets whether the kernel supports vectorization.
    public bool SupportsVectorization { get; set; } = true;
    /// Gets or sets the minimum compute capability required.
    public string? MinComputeCapability { get; set; }
    /// Gets or sets the source expression that generated this kernel.
    public Expression? SourceExpression { get; set; }
    /// Gets or sets compilation-specific hints for the backend compiler.
    public Dictionary<string, object> CompilationHints { get; set; }
    /// Gets or sets performance optimization hints.
    public Dictionary<string, object> PerformanceHints { get; set; }
    /// Gets or sets the estimated computational complexity.
    public ComputationalComplexity? Complexity { get; set; }
    /// Gets or sets memory access pattern information.
    public MemoryAccessPattern? MemoryPattern { get; set; }
    /// Gets or sets whether the kernel is deterministic.
    public bool IsDeterministic { get; set; } = true;
    /// Gets or sets whether the kernel is thread-safe.
    public bool IsThreadSafe { get; set; } = true;
    /// Gets or sets the kernel version for caching purposes.
    public string Version { get; set; } = "1.0.0";
    /// Gets the timestamp when this metadata was created.
    public DateTimeOffset CreatedAt { get; }
    /// Gets or sets additional custom metadata.
    public Dictionary<string, object>? CustomMetadata { get; set; }
    /// Gets or sets additional properties (alias for CustomMetadata).
    public Dictionary<string, object>? Properties => CustomMetadata ?? [];
    /// Gets or sets the generation timestamp.
    public DateTimeOffset GenerationTimestamp => CreatedAt;
    /// Gets or sets the compiler version used.
    public string CompilerVersion { get; set; } = "1.0.0";
    /// Adds a compilation hint for the backend compiler.
    /// <param name="key">The hint key.</param>
    /// <param name="value">The hint value.</param>
    public void AddCompilationHint(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Hint key cannot be null or whitespace.", nameof(key));
        CompilationHints[key] = value ?? throw new ArgumentNullException(nameof(value));
    }
    /// Adds a performance optimization hint.
    public void AddPerformanceHint(string key, object value)
    {
        PerformanceHints[key] = value ?? throw new ArgumentNullException(nameof(value));
    }
    /// Gets a compilation hint by key.
    /// <typeparam name="T">The type of the hint value.</typeparam>
    /// <returns>The hint value if found; otherwise, default(T).</returns>
    public T? GetCompilationHint<T>(string key)
    {
        if (CompilationHints.TryGetValue(key, out var value) && value is T typedValue)
            return typedValue;
        return default;
    }
    /// Gets a performance hint by key.
    public T? GetPerformanceHint<T>(string key)
    {
        if (PerformanceHints.TryGetValue(key, out var value) && value is T typedValue)
            return typedValue;
        return default;
    }
    /// Creates a copy of the metadata with updated values.
    /// <returns>A new instance with copied values.</returns>
    public KernelMetadata Clone()
    {
        var clone = new KernelMetadata(Name, Language)
        {
            OptimizationLevel = OptimizationLevel,
            PrecisionMode = PrecisionMode,
            WorkGroupSize = WorkGroupSize?.ToArray(),
            MaxRegistersPerThread = MaxRegistersPerThread,
            SharedMemorySize = SharedMemorySize,
            ConstantMemorySize = ConstantMemorySize,
            UseFastMath = UseFastMath,
            SupportsVectorization = SupportsVectorization,
            MinComputeCapability = MinComputeCapability,
            SourceExpression = SourceExpression,
            Complexity = Complexity,
            MemoryPattern = MemoryPattern,
            IsDeterministic = IsDeterministic,
            IsThreadSafe = IsThreadSafe,
            Version = Version,
            CustomMetadata = CustomMetadata != null ? new Dictionary<string, object>(CustomMetadata) : null
        };
        foreach (var hint in CompilationHints)
            clone.CompilationHints[hint.Key] = hint.Value;
        foreach (var hint in PerformanceHints)
            clone.PerformanceHints[hint.Key] = hint.Value;
        return clone;
    }
    /// <inheritdoc/>
    public bool Equals(KernelMetadata? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return Name == other.Name &&
               Language == other.Language &&
               Version == other.Version;
    }
    public override bool Equals(object? obj) => obj is KernelMetadata other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Name, Language, Version);
    public override string ToString() => $"KernelMetadata(Name: {Name}, Language: {Language}, Version: {Version})";
}
/// Represents computational complexity categories for kernel optimization.
public enum ComputationalComplexity
{
    /// Simple operations like element-wise arithmetic.
    Simple,
    /// Moderate complexity operations like reductions.
    Moderate,
    /// Complex operations like convolutions or matrix operations.
    Complex,
    /// Very complex operations like FFTs or sorting.
    VeryComplex
}
/// Represents memory access patterns for optimization hints.
public enum MemoryAccessPattern
{
    /// Sequential memory access pattern.
    Sequential,
    /// Random memory access pattern.
    Random,
    /// Strided memory access pattern.
    Strided,
    /// Coalesced memory access pattern (GPU optimized).
    Coalesced,
    /// Scattered memory access pattern.
    Scattered,
    /// Broadcast memory access pattern.
    Broadcast
}
