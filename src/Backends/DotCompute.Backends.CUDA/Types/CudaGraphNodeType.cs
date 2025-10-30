namespace DotCompute.Backends.CUDA.Graphs.Types
{
    /// <summary>
    /// Types of nodes in a CUDA graph.
    /// </summary>
    public enum CudaGraphNodeType
    {
        /// <summary>
        /// Kernel execution node.
        /// </summary>
        Kernel = 0,

        /// <summary>
        /// Memory copy node.
        /// </summary>
        Memcpy = 1,

        /// <summary>
        /// Memory set node.
        /// </summary>
        Memset = 2,

        /// <summary>
        /// Host function callback node.
        /// </summary>
        Host = 3,

        /// <summary>
        /// Child graph node.
        /// </summary>
        Graph = 4,

        /// <summary>
        /// Empty node for synchronization.
        /// </summary>
        Empty = 5,

        /// <summary>
        /// Event wait node.
        /// </summary>
        WaitEvent = 6,

        /// <summary>
        /// Event record node.
        /// </summary>
        EventRecord = 7,

        /// <summary>
        /// External semaphore signal node.
        /// </summary>
        SemaphoreSignal = 8,

        /// <summary>
        /// External semaphore wait node.
        /// </summary>
        SemaphoreWait = 9,

        /// <summary>
        /// Memory allocation node.
        /// </summary>
        MemAlloc = 10,

        /// <summary>
        /// Memory free node.
        /// </summary>
        MemFree = 11
    }
}
