// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;

namespace DotCompute.Linq.Analysis;

/// <summary>
/// Represents dependency information for expression analysis.
/// </summary>
public class DependencyInfo
{
    /// <summary>
    /// Initializes a new instance of the DependencyInfo class.
    /// </summary>
    /// <param name="type">The type of dependency</param>
    /// <param name="name">The name of the dependency</param>
    /// <param name="details">Additional details about the dependency</param>
    public DependencyInfo(DependencyType type, string name, string? details = null)
    {
        Type = type;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Details = details;
    }

    /// <summary>
    /// Gets the type of dependency.
    /// </summary>
    public DependencyType Type { get; }

    /// <summary>
    /// Gets the name of the dependency.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets additional details about the dependency.
    /// </summary>
    public string? Details { get; }

    /// <summary>
    /// Gets or sets whether this dependency is critical for execution.
    /// </summary>
    public bool IsCritical { get; set; } = true;

    /// <summary>
    /// Gets or sets the version requirement for this dependency.
    /// </summary>
    public string? VersionRequirement { get; set; }

    /// <summary>
    /// Gets or sets alternative options for this dependency.
    /// </summary>
    public List<string> Alternatives { get; set; } = [];

    /// <summary>
    /// Gets or sets the performance impact of this dependency.
    /// </summary>
    public PerformanceImpact PerformanceImpact { get; set; } = PerformanceImpact.Medium;

    /// <summary>
    /// Gets or sets additional metadata for this dependency.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];

    /// <summary>
    /// Gets or sets the dependent operation that relies on this dependency.
    /// </summary>
    public string? DependentOperation { get; set; }

    /// <summary>
    /// Gets or sets the list of dependencies that this dependency depends on.
    /// </summary>
    public List<string> Dependencies { get; set; } = [];

    /// <summary>
    /// Gets or sets whether this dependency allows parallelization.
    /// </summary>
    public bool AllowsParallelization { get; set; } = true;

    /// <summary>
    /// Returns a string representation of the dependency.
    /// </summary>
    public override string ToString()
    {
        return $"{Type}: {Name}" + (Details != null ? $" ({Details})" : "");
    }

    /// <summary>
    /// Determines equality based on type and name.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is DependencyInfo other &&

               Type == other.Type &&

               Name == other.Name;
    }

    /// <summary>
    /// Gets hash code based on type and name.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(Type, Name);
    }
}

/// <summary>
/// Types of dependencies that can be identified in expression analysis.
/// </summary>
public enum DependencyType
{
    /// <summary>
    /// Dependency on a .NET type.
    /// </summary>
    Type,

    /// <summary>
    /// Dependency on a method call.
    /// </summary>
    Method,

    /// <summary>
    /// Dependency on a property access.
    /// </summary>
    Property,

    /// <summary>
    /// Dependency on a field access.
    /// </summary>
    Field,

    /// <summary>
    /// Dependency on an assembly.
    /// </summary>
    Assembly,

    /// <summary>
    /// Dependency on a namespace.
    /// </summary>
    Namespace,

    /// <summary>
    /// Dependency on an external library.
    /// </summary>
    Library,

    /// <summary>
    /// Dependency on a compute backend.
    /// </summary>
    Backend,

    /// <summary>
    /// Dependency on a hardware feature.
    /// </summary>
    Hardware,

    /// <summary>
    /// Dependency on runtime capabilities.
    /// </summary>
    Runtime,

    /// <summary>
    /// Dependency on operating system features.
    /// </summary>
    OperatingSystem,

    /// <summary>
    /// Dependency on specific compiler features.
    /// </summary>
    Compiler,

    /// <summary>
    /// Dependency on data or configuration.
    /// </summary>
    Data,

    /// <summary>
    /// Custom or unknown dependency type.
    /// </summary>
    Custom
}

/// <summary>
/// Performance impact levels for dependencies.
/// </summary>
public enum PerformanceImpact
{
    /// <summary>
    /// Negligible performance impact.
    /// </summary>
    None,

    /// <summary>
    /// Low performance impact.
    /// </summary>
    Low,

    /// <summary>
    /// Medium performance impact.
    /// </summary>
    Medium,

    /// <summary>
    /// High performance impact.
    /// </summary>
    High,

    /// <summary>
    /// Critical performance impact - may significantly affect performance.
    /// </summary>
    Critical
}