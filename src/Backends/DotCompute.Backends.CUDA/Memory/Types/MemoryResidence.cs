namespace DotCompute.Backends.CUDA.Memory.Types
{
    /// <summary>
    /// Defines where unified memory is currently resident.
    /// </summary>
    public enum MemoryResidence
    {
        /// <summary>
        /// Memory residence is unknown or not tracked.
        /// </summary>
        Unknown,

        /// <summary>
        /// Memory is resident on the host (CPU).
        /// </summary>
        Host,

        /// <summary>
        /// Memory is resident on the device (GPU).
        /// </summary>
        Device,

        /// <summary>
        /// Memory is split between host and device.
        /// </summary>
        Split,

        /// <summary>
        /// Memory is being migrated between host and device.
        /// </summary>
        Migrating
    }
}