// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;

namespace DotCompute.Tests.Common.TestInfrastructure;


/// <summary>
/// Test implementation of compiled kernel for unit testing.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class TestCompiledKernel(string name, string sourceCode, CompilationOptions options) : ICompiledKernel
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
    public string SourceCode { get; } = sourceCode ?? throw new ArgumentNullException(nameof(sourceCode));
    public CompilationOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));
    public long SizeInBytes { get; } = 1024; // Mock size
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Constructor that accepts byte array code (for KernelDefinition.Code compatibility)
    /// </summary>
    public TestCompiledKernel(string name, byte[] code, CompilationOptions options)
        : this(name, System.Text.Encoding.UTF8.GetString(code ?? throw new ArgumentNullException(nameof(code))), options)
    {
    }

    public ValueTask ExecuteAsync(
        KernelArguments arguments,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        // Mock execution - just return completed task
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
