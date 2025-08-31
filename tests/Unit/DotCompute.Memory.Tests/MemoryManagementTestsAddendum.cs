// Extension to add missing methods to test doubles

namespace DotCompute.Memory.Tests
{
    // Missing methods for TestRawUnifiedBuffer - need to manually add these to the main file:
    /*
    public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long byteOffset, CancellationToken cancellationToken = default) where T : unmanaged
    {
        throw new NotSupportedException("Raw buffer operations not supported in test implementation");
    }

    public ValueTask CopyToAsync<T>(Memory<T> destination, long byteOffset, CancellationToken cancellationToken = default) where T : unmanaged
    {
        throw new NotSupportedException("Raw buffer operations not supported in test implementation");
    }
    */
}