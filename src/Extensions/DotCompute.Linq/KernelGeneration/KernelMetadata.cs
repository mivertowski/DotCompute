// <copyright file="KernelMetadata.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
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
    public KernelMetadata(string name, KernelLanguage language)
    {
        if (string.IsNullOrWhiteSpace(name))
        {

            throw new ArgumentException("Kernel name cannot be null or whitespace.", nameof(name));
        }


        Name = name;
        Language = language;
        CompilationHints = new Dictionary<string, object>();
        PerformanceHints = new Dictionary<string, object>();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the target kernel language.
    /// </summary>
    public KernelLanguage Language { get; }

    /// <summary>
    /// Gets or sets the optimization level for compilation.
    /// </summary>
    public Models.OptimizationLevel OptimizationLevel { get; set; } = Models.OptimizationLevel.O2;

    /// <summary>
    /// Gets or sets the precision mode for floating-point operations.
    /// </summary>
    public PrecisionMode PrecisionMode { get; set; } = PrecisionMode.Default;

    /// <summary>
    /// Gets or sets the expected work group size for optimization.
    /// </summary>
    public int[]? WorkGroupSize { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of registers per thread.
    /// </summary>
    public int? MaxRegistersPerThread { get; set; }

    /// <summary>
    /// Gets or sets the shared memory size in bytes.
    /// </summary>
    public int SharedMemorySize { get; set; }

    /// <summary>
    /// Gets or sets the constant memory size in bytes.
    /// </summary>
    public int ConstantMemorySize { get; set; }

    /// <summary>
    /// Gets or sets whether the kernel uses fast math optimizations.
    /// </summary>
    public bool UseFastMath { get; set; }

    /// <summary>
    /// Gets or sets whether the kernel supports vectorization.
    /// </summary>
    public bool SupportsVectorization { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum compute capability required.
    /// </summary>
    public string? MinComputeCapability { get; set; }

    /// <summary>
    /// Gets or sets the source expression that generated this kernel.
    /// </summary>
    public Expression? SourceExpression { get; set; }

    /// <summary>
    /// Gets or sets compilation-specific hints for the backend compiler.
    /// </summary>
    public Dictionary<string, object> CompilationHints { get; set; }

    /// <summary>
    /// Gets or sets performance optimization hints.
    /// </summary>
    public Dictionary<string, object> PerformanceHints { get; set; }

    /// <summary>
    /// Gets or sets the estimated computational complexity.
    /// </summary>
    public ComputationalComplexity? Complexity { get; set; }

    /// <summary>
    /// Gets or sets memory access pattern information.
    /// </summary>
    public MemoryAccessPattern? MemoryPattern { get; set; }

    /// <summary>
    /// Gets or sets whether the kernel is deterministic.
    /// </summary>
    public bool IsDeterministic { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the kernel is thread-safe.
    /// </summary>
    public bool IsThreadSafe { get; set; } = true;

    /// <summary>
    /// Gets or sets the kernel version for caching purposes.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets the timestamp when this metadata was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets or sets additional custom metadata.
    /// </summary>
    public Dictionary<string, object>? CustomMetadata { get; set; }

    /// <summary>
    /// Adds a compilation hint for the backend compiler.
    /// </summary>
    /// <param name="key">The hint key.</param>
    /// <param name="value">The hint value.</param>
    public void AddCompilationHint(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {

            throw new ArgumentException("Hint key cannot be null or whitespace.", nameof(key));
        }


        CompilationHints[key] = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Adds a performance optimization hint.
    /// </summary>
    /// <param name="key">The hint key.</param>
    /// <param name="value">The hint value.</param>
    public void AddPerformanceHint(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {

            throw new ArgumentException("Hint key cannot be null or whitespace.", nameof(key));
        }


        PerformanceHints[key] = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets a compilation hint by key.
    /// </summary>
    /// <typeparam name="T">The type of the hint value.</typeparam>
    /// <param name="key">The hint key.</param>
    /// <returns>The hint value if found; otherwise, default(T).</returns>
    public T? GetCompilationHint<T>(string key)
    {
        if (CompilationHints.TryGetValue(key, out var value) && value is T typedValue)
        {

            return typedValue;
        }


        return default;
    }

    /// <summary>
    /// Gets a performance hint by key.
    /// </summary>
    /// <typeparam name="T">The type of the hint value.</typeparam>
    /// <param name="key">The hint key.</param>
    /// <returns>The hint value if found; otherwise, default(T).</returns>
    public T? GetPerformanceHint<T>(string key)
    {
        if (PerformanceHints.TryGetValue(key, out var value) && value is T typedValue)
        {

            return typedValue;
        }


        return default;
    }

    /// <summary>
    /// Creates a copy of the metadata with updated values.
    /// </summary>
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
        {
            clone.CompilationHints[hint.Key] = hint.Value;
        }

        foreach (var hint in PerformanceHints)
        {
            clone.PerformanceHints[hint.Key] = hint.Value;
        }


        return clone;
    }

    /// <inheritdoc/>
    public bool Equals(KernelMetadata? other)
    {
        if (other is null)
        {
            return false;
        }


        if (ReferenceEquals(this, other))
        {
            return true;
        }


        return Name == other.Name &&
               Language == other.Language &&
               Version == other.Version;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is KernelMetadata other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Language, Version);

    /// <inheritdoc/>
    public override string ToString() => $"KernelMetadata(Name: {Name}, Language: {Language}, Version: {Version})";
}

/// <summary>
/// Represents computational complexity categories for kernel optimization.
/// </summary>
public enum ComputationalComplexity
{
    /// <summary>
    /// Simple operations like element-wise arithmetic.
    /// </summary>
    Simple,

    /// <summary>
    /// Moderate complexity operations like reductions.
    /// </summary>
    Moderate,

    /// <summary>
    /// Complex operations like convolutions or matrix operations.
    /// </summary>
    Complex,

    /// <summary>
    /// Very complex operations like FFTs or sorting.
    /// </summary>
    VeryComplex
}

/// <summary>
/// Represents memory access patterns for optimization hints.
/// </summary>
public enum MemoryAccessPattern
{
    /// <summary>
    /// Sequential memory access pattern.
    /// </summary>
    Sequential,

    /// <summary>
    /// Random memory access pattern.
    /// </summary>
    Random,

    /// <summary>
    /// Strided memory access pattern.
    /// </summary>
    Strided,

    /// <summary>
    /// Coalesced memory access pattern (optimal for GPU).
    /// </summary>
    Coalesced,

    /// <summary>
    /// Broadcast memory access pattern.
    /// </summary>
    Broadcast
}