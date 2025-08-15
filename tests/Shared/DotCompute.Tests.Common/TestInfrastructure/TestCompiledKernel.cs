// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Shared.TestInfrastructure;

/// <summary>
/// Test implementation of compiled kernel for unit testing.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class TestCompiledKernel : ICompiledKernel
{
    public string Name { get; }
    public string SourceCode { get; }
    public CompilationOptions Options { get; }
    public long SizeInBytes { get; } = 1024; // Mock size
    public bool IsDisposed { get; private set; }

    public TestCompiledKernel(string name, string sourceCode, CompilationOptions options)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        SourceCode = sourceCode ?? throw new ArgumentNullException(nameof(sourceCode));
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Constructor that accepts byte array code(for KernelDefinition.Code compatibility)
    /// </summary>
    public TestCompiledKernel(string name, byte[] code, CompilationOptions options)
        : this(name, System.Text.Encoding.UTF8.GetString(code ?? throw new ArgumentNullException(nameof(code))), options)
    {
    }

    public ValueTask ExecuteAsync(
        KernelArguments arguments, 
        CancellationToken cancellationToken = default)
    {
        if(IsDisposed) 
            throw new ObjectDisposedException(nameof(TestCompiledKernel));
        
        // Mock execution - just return completed task
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
