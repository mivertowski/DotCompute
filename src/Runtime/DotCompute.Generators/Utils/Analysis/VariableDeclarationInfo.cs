// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Utils.Analysis;

/// <summary>
/// Information about a variable declaration.
/// </summary>
public sealed class VariableDeclarationInfo
{
    /// <summary>
    /// Gets or sets the variable name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the variable type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the variable has an initializer.
    /// </summary>
    public bool HasInitializer { get; set; }

    /// <summary>
    /// Gets or sets the initializer expression.
    /// </summary>
    public string? InitializerExpression { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the variable is const.
    /// </summary>
    public bool IsConst { get; set; }
}
