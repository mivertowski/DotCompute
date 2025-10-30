// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;

namespace DotCompute.Generators.Backend.CPU.Interfaces;

/// <summary>
/// Interface for generating code structure elements like headers, namespaces, and class definitions.
/// </summary>
public interface ICodeStructureGenerator
{
    /// <summary>
    /// Generates file header with required using statements.
    /// </summary>
    public void GenerateFileHeader(StringBuilder sb);

    /// <summary>
    /// Generates namespace declaration and class opening.
    /// </summary>
    public void GenerateNamespaceAndClass(StringBuilder sb, string methodName);

    /// <summary>
    /// Generates closing braces for class and namespace.
    /// </summary>
    public void GenerateClassClosing(StringBuilder sb);
}
