// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.ObjectModel;

namespace DotCompute.Generators.Utils.Analysis;

/// <summary>
/// Information about a method invocation.
/// </summary>
public sealed class MethodInvocationInfo
{
    private readonly Collection<string> _arguments = [];

    /// <summary>
    /// Gets or sets the method name.
    /// </summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// Gets the arguments passed to the method.
    /// </summary>
    public Collection<string> Arguments => _arguments;

    /// <summary>
    /// Gets or sets the full expression.
    /// </summary>
    public string FullExpression { get; set; } = string.Empty;
}
