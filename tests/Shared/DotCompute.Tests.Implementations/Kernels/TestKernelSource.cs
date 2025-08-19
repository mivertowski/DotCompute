using DotCompute.Abstractions;

namespace DotCompute.Tests.Implementations.Kernels;


/// <summary>
/// Test implementation of IKernelSource for testing purposes.
/// </summary>
public sealed class TestKernelSource : IKernelSource
{
    public string Name { get; set; } = "TestKernel";
    public string Code { get; set; } = "";
    public string EntryPoint { get; set; } = "main";
    public KernelLanguage Language { get; set; } = KernelLanguage.OpenCL;
    public string[] Dependencies { get; set; } = [];
}
