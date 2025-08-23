// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Configuration.Settings.Enums;
using DotCompute.Generators.Configuration.Settings.Features;
using DotCompute.Generators.Configuration.Settings.Validation;
using DotCompute.Generators.Configuration.Style;

namespace DotCompute.Generators.Configuration.Settings;

/// <summary>
/// Configuration settings for code generation, allowing runtime customization
/// of generator behavior and output characteristics.
/// </summary>
/// <remarks>
/// This class serves as the central configuration point for all code generation
/// operations. It aggregates various configuration aspects including optimization
/// levels, feature flags, validation settings, and code styling preferences.
/// </remarks>
public class GeneratorSettings
{
    /// <summary>
    /// Gets or sets the code style configuration.
    /// </summary>
    /// <value>
    /// The code style configuration that controls formatting, naming conventions,
    /// and other stylistic aspects of generated code.
    /// </value>
    public CodeStyle CodeStyle { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the optimization level for generated code.
    /// </summary>
    /// <value>
    /// The optimization level that determines the balance between performance,
    /// code size, and debuggability in the generated code.
    /// </value>
    public Enums.OptimizationLevel OptimizationLevel { get; set; } = Enums.OptimizationLevel.Balanced;
    
    /// <summary>
    /// Gets or sets a value indicating whether to generate debug information.
    /// </summary>
    /// <value>
    /// <c>true</c> if debug information should be included in generated code;
    /// otherwise, <c>false</c>. Debug information includes line mappings and
    /// additional metadata for debugging purposes.
    /// </value>
    public bool GenerateDebugInfo { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether to include performance counters.
    /// </summary>
    /// <value>
    /// <c>true</c> if performance counters should be embedded in generated code
    /// for runtime performance monitoring; otherwise, <c>false</c>.
    /// </value>
    public bool IncludePerformanceCounters { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether to generate XML documentation.
    /// </summary>
    /// <value>
    /// <c>true</c> if XML documentation comments should be generated for all
    /// public members in the generated code; otherwise, <c>false</c>.
    /// </value>
    public bool GenerateXmlDocs { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the target runtime for optimization.
    /// </summary>
    /// <value>
    /// The target runtime environment that influences optimization strategies
    /// and feature availability in the generated code.
    /// </value>
    public Enums.TargetRuntime TargetRuntime { get; set; } = Enums.TargetRuntime.Auto;
    
    /// <summary>
    /// Gets or sets feature flags for selective code generation.
    /// </summary>
    /// <value>
    /// The feature flags that control which advanced features and optimizations
    /// are enabled in the generated code.
    /// </value>
    public Features.FeatureFlags Features { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the validation settings.
    /// </summary>
    /// <value>
    /// The validation settings that control what runtime checks and validations
    /// are included in the generated code.
    /// </value>
    public Validation.ValidationSettings Validation { get; set; } = new();
}