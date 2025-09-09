namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Flags for CUDA managed memory allocation and behavior.
    /// </summary>
    [System.Flags]
    public enum ManagedMemoryFlags
    {
        /// <summary>
        /// Default managed memory behavior.
        /// </summary>
        None = 0,

        /// <summary>
        /// Memory should be initially resident on the device.
        /// </summary>
        PreferDevice = 0x01,


        /// <summary>
        /// Memory should be initially resident on the device (CUDA native value).
        /// </summary>
        PreferDeviceNative = 0x10,

        /// <summary>
        /// Memory should be initially resident on the host.
        /// </summary>
        PreferHost = 2,

        /// <summary>
        /// Enable read-mostly optimization.
        /// </summary>
        ReadMostly = 4,

        /// <summary>
        /// Memory is accessed by a single device.
        /// </summary>
        SingleDevice = 8,

        /// <summary>
        /// Memory is shared across multiple devices.
        /// </summary>
        MultiDevice = 16,

        /// <summary>
        /// Enable prefetching optimizations.
        /// </summary>
        EnablePrefetch = 32,

        /// <summary>
        /// Track access patterns for optimization.
        /// </summary>
        TrackAccess = 64
    }
}
