// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// This file is maintained for backward compatibility.
// Individual types have been moved to their respective files in the Settings folder structure.

using DotCompute.Generators.Configuration.Settings;

namespace DotCompute.Generators.Configuration;

// Re-export the main settings class for backward compatibility
[System.Obsolete("Use DotCompute.Generators.Configuration.Settings.GeneratorSettings instead. This alias will be removed in a future version.", false)]
public class GeneratorSettings : Settings.GeneratorSettings
{
}

// Re-export enums for backward compatibility
[System.Obsolete("Use DotCompute.Generators.Configuration.Settings.Enums.OptimizationLevel instead. This alias will be removed in a future version.", false)]
public enum OptimizationLevel
{
    None = Settings.Enums.OptimizationLevel.None,
    Balanced = Settings.Enums.OptimizationLevel.Balanced,
    Aggressive = Settings.Enums.OptimizationLevel.Aggressive,
    Size = Settings.Enums.OptimizationLevel.Size
}

[System.Obsolete("Use DotCompute.Generators.Configuration.Settings.Enums.TargetRuntime instead. This alias will be removed in a future version.", false)]
public enum TargetRuntime
{
    Auto = Settings.Enums.TargetRuntime.Auto,
    NetFramework = Settings.Enums.TargetRuntime.NetFramework,
    NetCore = Settings.Enums.TargetRuntime.NetCore,
    Unity = Settings.Enums.TargetRuntime.Unity,
    Mobile = Settings.Enums.TargetRuntime.Mobile
}

// Re-export classes for backward compatibility
[System.Obsolete("Use DotCompute.Generators.Configuration.Settings.Features.FeatureFlags instead. This alias will be removed in a future version.", false)]
public class FeatureFlags : Settings.Features.FeatureFlags
{
}

[System.Obsolete("Use DotCompute.Generators.Configuration.Settings.Validation.ValidationSettings instead. This alias will be removed in a future version.", false)]
public class ValidationSettings : Settings.Validation.ValidationSettings
{
}