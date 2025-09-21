// <copyright file="MockKernelSource.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Abstractions.Types;
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
    /// Gets or sets the kernel source code.
    public string Code { get; set; } = string.Empty;
    /// Gets or sets the kernel language.
    public DotCompute.Abstractions.Kernels.Types.KernelLanguage Language { get; set; } = DotCompute.Abstractions.Kernels.Types.KernelLanguage.CSharpIL;
    /// Gets or sets the kernel entry point.
    public string EntryPoint { get; set; } = "Main";
    /// Gets or sets the source metadata.
    public object? Metadata { get; set; }
    /// Gets or sets the kernel dependencies.
    public string[] Dependencies { get; set; } = [];
}
