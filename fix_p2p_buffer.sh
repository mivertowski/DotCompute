#!/bin/bash

echo "Fixing P2PBuffer non-generic buffer issues..."

# Replace CopyFromAsync calls on non-generic buffer
sed -i 's/await _underlyingBuffer\.CopyFromAsync<[^>]*>(\([^,]*\), \([^,]*\), \([^)]*\))/\
if (_underlyingBuffer is IUnifiedMemoryBuffer<byte> typedBuffer) {\
    var sourceBytes = MemoryMarshal.AsBytes(\1.Span);\
    await typedBuffer.CopyFromAsync(sourceBytes, \3);\
} else {\
    throw new InvalidOperationException("Buffer is not typed");\
}/g' src/Core/DotCompute.Core/Memory/P2PBuffer.cs

# Replace CopyToAsync calls on non-generic buffer  
sed -i 's/await _underlyingBuffer\.CopyToAsync<[^>]*>(\([^,]*\), \([^,]*\), \([^)]*\))/\
if (_underlyingBuffer is IUnifiedMemoryBuffer<byte> typedBuffer) {\
    var destBytes = MemoryMarshal.AsBytes(\1.Span);\
    await typedBuffer.CopyToAsync(destBytes, \3);\
} else {\
    throw new InvalidOperationException("Buffer is not typed");\
}/g' src/Core/DotCompute.Core/Memory/P2PBuffer.cs

echo "P2PBuffer fixes complete!"