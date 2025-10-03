namespace DotCompute.Abstractions.Types
{
    /// <summary>
    /// Defines the types of memory transfers in CUDA.
    /// </summary>
    public enum MemoryTransferType
    {
        /// <summary>
        /// Transfer from host (CPU) memory to device (GPU) memory.
        /// </summary>
        HostToDevice,

        /// <summary>
        /// Transfer from device (GPU) memory to host (CPU) memory.
        /// </summary>
        DeviceToHost,

        /// <summary>
        /// Transfer between two device (GPU) memory locations.
        /// </summary>
        DeviceToDevice,

        /// <summary>
        /// Transfer between two host (CPU) memory locations.
        /// </summary>
        HostToHost,

        /// <summary>
        /// Transfer involving unified memory that can be accessed by both host and device.
        /// </summary>
        UnifiedMemory
    }
}
