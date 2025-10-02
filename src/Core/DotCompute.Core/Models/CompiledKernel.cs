namespace DotCompute.Core.Models
{
    /// <summary>
    /// Represents a compiled CUDA kernel.
    /// </summary>
    public class CompiledKernel
    {
        /// <summary>
        /// Gets or sets the kernel name.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Gets or sets the PTX code.
        /// </summary>
        public string? PtxCode { get; set; }

        /// <summary>
        /// Gets or sets the PTX intermediate representation.
        /// </summary>
        public string Ptx { get; set; } = "";

        /// <summary>
        /// Gets or sets the binary code (cubin).
        /// </summary>
        public byte[]? BinaryCode { get; set; }

        /// <summary>
        /// Gets or sets the CUBIN binary data.
        /// </summary>
        public byte[] Cubin { get; set; } = [];

        /// <summary>
        /// Gets or sets the compiled binary data.
        /// </summary>
        public byte[] Binary { get; set; } = [];

        /// <summary>
        /// Gets or sets the CUDA module handle.
        /// </summary>
        public IntPtr ModuleHandle { get; set; }

        /// <summary>
        /// Gets or sets the kernel function handle.
        /// </summary>
        public IntPtr FunctionHandle { get; set; }

        /// <summary>
        /// Gets or sets the compilation timestamp.
        /// </summary>
        public DateTimeOffset CompiledAt { get; set; }

        /// <summary>
        /// Gets or sets the compute capability version used for compilation.
        /// </summary>
        public Version ComputeCapability { get; set; } = new Version(0, 0);

        /// <summary>
        /// Gets or sets the time spent during compilation.
        /// </summary>
        public TimeSpan CompilationTime { get; set; }

        /// <summary>
        /// Gets or sets whether this kernel was loaded from cache.
        /// </summary>
        public bool LoadedFromCache { get; set; }

        /// <summary>
        /// Gets or sets the target architecture.
        /// </summary>
        public string Architecture { get; set; } = "";

        /// <summary>
        /// Gets or sets the source code hash for cache validation.
        /// </summary>
        public string SourceHash { get; set; } = "";

        /// <summary>
        /// Gets or sets kernel attributes.
        /// </summary>
        public KernelAttributes? Attributes { get; set; }

        /// <summary>
        /// Gets or sets the compilation log.
        /// </summary>
        public string? CompilationLog { get; set; }

        /// <summary>
        /// Gets or sets the entry point function name for this kernel.
        /// </summary>
        public string EntryPoint { get; set; } = "";

        /// <summary>
        /// Gets or sets the target device or accelerator type for this kernel.
        /// </summary>
        public string TargetDevice { get; set; } = "Unknown";

        /// <summary>
        /// Gets or sets the required shared memory size in bytes for this kernel.
        /// </summary>
        public long RequiredSharedMemory { get; set; }

        /// <summary>
        /// Gets or sets the maximum threads per block supported by this kernel.
        /// </summary>
        public int MaxThreadsPerBlock { get; set; } = 1024;
    }
}