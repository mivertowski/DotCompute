using DotCompute.Abstractions;

namespace DotCompute.Tests.Implementations.Kernels;

/// <summary>
/// Test implementation of IKernelSource for testing purposes.
/// </summary>
public sealed class TestKernelSource : IKernelSource
{
    /// <summary>
    /// Gets the name of the kernel.
    /// </summary>
    public string Name { get; set; } = "TestKernel";

    /// <summary>
    /// Gets the source code or bytecode of the kernel.
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>
    /// Gets the entry point method name for the kernel.
    /// </summary>
    public string EntryPoint { get; set; } = "main";

    /// <summary>
    /// Gets the kernel language or source type.
    /// </summary>
    public KernelLanguage Language { get; set; } = KernelLanguage.OpenCL;

    /// <summary>
    /// Gets the dependencies required by this kernel.
    /// </summary>
    public string[] Dependencies { get; set; } = [];
}
