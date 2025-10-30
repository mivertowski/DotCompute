// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Models;

namespace DotCompute.Generators.Backend.CPU.Interfaces;

/// <summary>
/// Interface for orchestrating code generation across multiple specialized generators.
/// </summary>
public interface ICodeGeneratorOrchestrator
{
    /// <summary>
    /// Generates complete CPU implementation by orchestrating specialized generators.
    /// </summary>
    /// <param name="context">The generation context containing all necessary information.</param>
    /// <returns>The generated C# code as a string.</returns>
    public string GenerateCode(KernelGenerationContext context);
}
