// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Configuration.Settings.Enums;

/// <summary>
/// Defines target runtime environments for code generation optimization,
/// allowing the generator to tailor output for specific execution contexts.
/// </summary>
/// <remarks>
/// The target runtime affects various aspects of code generation including
/// available APIs, performance characteristics, memory management patterns,
/// and platform-specific optimizations. Selecting the correct runtime
/// ensures optimal performance and compatibility.
/// </remarks>
public enum TargetRuntime
{
    /// <summary>
    /// Automatically detect the most appropriate runtime based on the current environment.
    /// </summary>
    /// <remarks>
    /// This option uses runtime detection to determine the best optimization
    /// strategy. The generator will analyze the current execution environment
    /// and select the most appropriate optimizations and API usage patterns.
    /// </remarks>
    Auto,

    /// <summary>
    /// Target .NET Framework runtime (legacy Windows-only runtime).
    /// </summary>
    /// <remarks>
    /// Optimizes for .NET Framework 4.x environments, using Framework-specific
    /// APIs and optimization patterns. This runtime has different performance
    /// characteristics and API availability compared to modern .NET runtimes.
    /// </remarks>
    NetFramework,

    /// <summary>
    /// Target .NET Core or .NET 5+ runtime (cross-platform modern runtime).
    /// </summary>
    /// <remarks>
    /// Optimizes for modern .NET runtimes (.NET Core 3.x, .NET 5+) which
    /// provide better performance, cross-platform compatibility, and access
    /// to the latest runtime optimizations and APIs.
    /// </remarks>
    NetCore,

    /// <summary>
    /// Target Unity game engine runtime environment.
    /// </summary>
    /// <remarks>
    /// Optimizes for Unity's specialized runtime which has unique constraints
    /// including limited API surface, specific garbage collection patterns,
    /// and platform-specific considerations for game development scenarios.
    /// </remarks>
    Unity,


    /// <summary>
    /// Target Xamarin/MAUI mobile runtime environments.
    /// </summary>
    /// <remarks>
    /// Optimizes for mobile runtimes including iOS and Android platforms
    /// accessed through Xamarin or .NET MAUI. Considers memory constraints,
    /// battery usage, and mobile-specific performance characteristics.
    /// </remarks>
    Mobile
}