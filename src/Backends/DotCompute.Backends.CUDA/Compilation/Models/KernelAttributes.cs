namespace DotCompute.Backends.CUDA.Compilation.Models
{
    /// <summary>
    /// Attributes of a compiled CUDA kernel.
    /// </summary>
    public class KernelAttributes
    {
        /// <summary>
        /// Gets or sets the number of registers used per thread.
        /// </summary>
        public int RegistersPerThread { get; set; }

        /// <summary>
        /// Gets or sets the shared memory used in bytes.
        /// </summary>
        public int SharedMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets the constant memory used in bytes.
        /// </summary>
        public int ConstantMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets the local memory used per thread in bytes.
        /// </summary>
        public int LocalMemoryBytesPerThread { get; set; }

        /// <summary>
        /// Gets or sets the maximum threads per block.
        /// </summary>
        public int MaxThreadsPerBlock { get; set; }

        /// <summary>
        /// Gets or sets the PTX version.
        /// </summary>
        public int PtxVersion { get; set; }

        /// <summary>
        /// Gets or sets the binary version.
        /// </summary>
        public int BinaryVersion { get; set; }

        /// <summary>
        /// Gets or sets whether the kernel uses dynamic shared memory.
        /// </summary>
        public bool UsesDynamicSharedMemory { get; set; }

        /// <summary>
        /// Gets or sets whether the kernel uses texture memory.
        /// </summary>
        public bool UsesTextureMemory { get; set; }
    }
}