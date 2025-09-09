// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;

namespace DotCompute.Generators.Backend.CPU.Interfaces;

/// <summary>
/// Interface for code generation components.
/// </summary>
public interface ICodeGenerator
{
    /// <summary>
    /// Generates code and appends it to the provided StringBuilder.
    /// </summary>
    /// <param name="sb">The StringBuilder to append generated code to.</param>
    public void Generate(StringBuilder sb);
}