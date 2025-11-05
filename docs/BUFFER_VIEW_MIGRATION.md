# CUDA Buffer View Migration Guide

**Status**: CudaMemoryBufferView is deprecated as of v0.2.0-alpha and will be removed in v0.3.0

## Overview

`CudaMemoryBufferView` and `CudaMemoryBufferView<T>` were internal implementation details used for buffer slicing operations. These classes have been marked as `[Obsolete]` and `internal` because they exposed implementation details that should remain hidden from consumers.

## What Changed

### Before (v0.1.x)

The buffer view classes were public, which could lead to confusion:

```csharp
// This was possible but NOT recommended
CudaMemoryBufferView<float> view = ...;  // Direct instantiation (wrong)
```

### After (v0.2.0+)

The classes are now internal and obsolete:

```csharp
// CudaMemoryBufferView is now internal - cannot be used directly
// Use IUnifiedMemoryBuffer instead through CudaMemoryManager
```

## Migration Path

### For Buffer Slicing

**Before:**
```csharp
// If you were somehow using CudaMemoryBufferView directly (incorrect usage)
var view = new CudaMemoryBufferView<float>(devicePtr, length, parentBuffer);
```

**After:**
```csharp
// Use the memory manager's CreateSlice methods (correct usage)
IMemoryManager memoryManager = accelerator.Memory;
IUnifiedMemoryBuffer<float> buffer = await memoryManager.AllocateAsync<float>(1024);

// Create a slice through the memory manager
IUnifiedMemoryBuffer<float> slice = memoryManager.CreateSlice(buffer, offset: 100, length: 200);
```

### For Working with Buffers

**Recommended Pattern:**
```csharp
// Always use interface types, never concrete implementations
IUnifiedMemoryBuffer<float> buffer = await accelerator.Memory.AllocateAsync<float>(size);

// Work with the buffer through the interface
await buffer.CopyFromAsync(data);
await buffer.CopyToAsync(result);

// Create slices through the memory manager
var slice = accelerator.Memory.CreateSlice(buffer, offset, length);
```

## Why This Change?

1. **Encapsulation**: Buffer views are internal implementation details of the CUDA backend
2. **Consistency**: All buffer operations should go through `IMemoryManager` and `IUnifiedMemoryBuffer`
3. **Safety**: Direct buffer view creation bypasses safety checks in the memory manager
4. **Flexibility**: Internal implementation can evolve without breaking user code

## Impact Assessment

### Affected Code

- **Internal Only**: These classes were only used internally by `CudaMemoryManager`
- **No Public API Impact**: No public APIs exposed these types directly
- **Test Coverage**: No test code references these classes
- **Zero Breaking Changes**: All public APIs continue to work unchanged

### Timeline

- **v0.2.0-alpha** (November 2025): Classes marked as `[Obsolete]` and `internal`
- **v0.3.0** (Q1 2026): Classes will be removed entirely

## Best Practices

### ✅ DO

- Use `IUnifiedMemoryBuffer<T>` and `IUnifiedMemoryBuffer` for all buffer operations
- Create slices through `IMemoryManager.CreateSlice()` methods
- Work with buffers through their interfaces
- Let the memory manager handle buffer lifecycle

### ❌ DON'T

- Directly instantiate `CudaMemoryBufferView` or `CudaMemoryBufferView<T>`
- Cast `IUnifiedMemoryBuffer` to concrete implementation types
- Access internal buffer implementation details
- Manage buffer memory outside of the memory manager

## Example: Complete Buffer Workflow

```csharp
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

public async Task ProcessDataAsync(IAccelerator accelerator)
{
    // Allocate buffer through memory manager
    IUnifiedMemoryBuffer<float> buffer =
        await accelerator.Memory.AllocateAsync<float>(1024);

    try
    {
        // Write data to buffer
        var data = new float[1024];
        await buffer.CopyFromAsync(data);

        // Create a slice for processing subset
        IUnifiedMemoryBuffer<float> slice =
            accelerator.Memory.CreateSlice(buffer, offset: 100, length: 500);

        // Work with the slice (it's also IUnifiedMemoryBuffer<float>)
        var sliceData = new float[500];
        await slice.CopyToAsync(sliceData);

        // Slices don't own memory - parent disposal cleans up everything
        slice.Dispose();  // Just marks as disposed, doesn't free memory
    }
    finally
    {
        // Dispose parent buffer (frees GPU memory)
        await buffer.DisposeAsync();
    }
}
```

## Technical Details

### Memory Ownership

- **CudaMemoryBuffer**: Owns GPU memory, responsible for deallocation
- **CudaMemoryBufferView**: Non-owning view, parent buffer handles cleanup
- **Disposal Pattern**: Views mark themselves disposed but don't free memory

### Implementation Notes

The buffer view pattern enables:
- Zero-copy slicing (no additional GPU allocations)
- Efficient sub-buffer operations
- Proper memory lifecycle management
- Type-safe buffer operations

## Questions?

If you encounter issues during migration:

1. Check that you're using `IUnifiedMemoryBuffer` interface types
2. Verify all buffer operations go through `IMemoryManager`
3. Review the [Memory Management Guide](./guides/memory-management.md)
4. File an issue at https://github.com/mivertowski/DotCompute/issues

## Related Documentation

- [Memory Management Guide](./guides/memory-management.md)
- [CUDA Backend Guide](./guides/cuda-backend.md)
- [API Reference - IUnifiedMemoryBuffer](./api/DotCompute.Abstractions.Memory.IUnifiedMemoryBuffer.html)
- [API Reference - IMemoryManager](./api/DotCompute.Abstractions.Memory.IMemoryManager.html)
