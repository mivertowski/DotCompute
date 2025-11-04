# CPU Backend Architecture

The CPU backend provides SIMD-accelerated execution for systems without GPU hardware.

## SIMD Vectorization

For detailed information on SIMD operations, see the [SIMD Vectorization Guide](../advanced/simd-vectorization.md).

## Architecture

The CPU backend uses .NET's Vector<T> types and platform-specific intrinsics (AVX2, AVX512, NEON) for optimal performance.

## Related Documentation

- [SIMD Vectorization](../advanced/simd-vectorization.md)
- [Backend Integration](backend-integration.md)
