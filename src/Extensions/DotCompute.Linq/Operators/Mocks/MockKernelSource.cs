// <copyright file="MockKernelSource.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions;

namespace DotCompute.Linq.Operators.Mocks;

/// <summary>
/// Mock kernel source implementation for testing purposes.
/// </summary>
internal class MockKernelSource : IKernelSource
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kernel source code.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kernel language.
    /// </summary>
    public KernelLanguage Language { get; set; } = KernelLanguage.CSharpIL;

    /// <summary>
    /// Gets or sets the kernel entry point.
    /// </summary>
    public string EntryPoint { get; set; } = "Main";

    /// <summary>
    /// Gets or sets the source metadata.
    /// </summary>
    public object? Metadata { get; set; }
}