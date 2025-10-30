// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Enums;

/// <summary>
/// Defines the different levels of implementation difficulty for performance recommendations.
/// Helps prioritize optimization efforts based on the effort required versus potential benefit.
/// </summary>
public enum ImplementationDifficulty
{
    /// <summary>
    /// Trivial change requiring minimal effort and no architectural modifications.
    /// Can typically be implemented with simple configuration changes.
    /// </summary>
    Trivial,

    /// <summary>
    /// Easy to implement with straightforward code changes.
    /// Requires basic development effort but no major design changes.
    /// </summary>
    Easy,

    /// <summary>
    /// Moderate difficulty requiring some design consideration and testing.
    /// May involve refactoring existing code or adding new components.
    /// </summary>
    Moderate,

    /// <summary>
    /// Difficult to implement requiring significant development effort.
    /// May involve architectural changes or complex algorithmic modifications.
    /// </summary>
    Difficult,

    /// <summary>
    /// Very complex change requiring extensive development and testing.
    /// May involve fundamental architectural redesign or advanced optimizations.
    /// </summary>
    Complex
}
