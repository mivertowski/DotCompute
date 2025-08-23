// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Configuration.Style.Enums;

/// <summary>
/// Specifies the indentation style options for generated code formatting.
/// </summary>
/// <remarks>
/// The choice between spaces and tabs affects code appearance and consistency
/// across different editors and environments. Spaces provide consistent visual
/// alignment regardless of tab width settings, while tabs allow developers
/// to customize indentation width in their preferred editor.
/// </remarks>
public enum IndentationStyle
{
    /// <summary>
    /// Use space characters for indentation.
    /// </summary>
    /// <remarks>
    /// Spaces provide consistent visual alignment across all editors and environments,
    /// regardless of tab width settings. This is the most common choice in modern
    /// development environments and ensures predictable code formatting.
    /// The number of spaces per indentation level is controlled by the IndentSize property.
    /// </remarks>
    Spaces,
    
    /// <summary>
    /// Use tab characters for indentation.
    /// </summary>
    /// <remarks>
    /// Tabs allow individual developers to customize the visual width of indentation
    /// in their preferred editor while maintaining consistent logical structure.
    /// However, this can lead to alignment issues with mixed tabs and spaces,
    /// and inconsistent appearance across different viewing environments.
    /// </remarks>
    Tabs
}