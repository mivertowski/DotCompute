# Backend Integration Architecture

The Backend Integration system provides a plugin-based architecture for compute acceleration, allowing DotCompute to support multiple hardware platforms (CPU, CUDA, Metal, OpenCL) through a unified interface.

## Architecture Overview

```
Application Code
    ↓
IComputeOrchestrator
    ↓
IAcceleratorFactory
    ↓
┌─────────────────────────────────────────────────┐
│         Backend Plugin System                   │
├─────────────────────────────────────────────────┤
│  PluginManager → PluginLoader → BackendPlugin  │
│      ↓              ↓               ↓           │
│  Discovery     Isolation       IAccelerator     │
└─────────────────────────────────────────────────┘
    ↓                ↓                ↓
CPU Backend    CUDA Backend    Metal Backend
(AVX512/AVX2)  (NVIDIA GPU)   (Apple Silicon)
```

## IAccelerator Interface

The core abstraction for all compute backends:

```csharp
public interface IAccelerator : IAsyncDisposable
{
    /// <summary>
    /// Unique identifier for this accelerator instance
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Type of accelerator (CPU, CUDA, Metal, OpenCL)
    /// </summary>
    AcceleratorType Type { get; }

    /// <summary>
    /// Device name (e.g., "NVIDIA RTX 2000", "Apple M1 Pro")
    /// </summary>
    string DeviceName { get; }

    /// <summary>
    /// Backend capabilities (e.g., compute capability, feature levels)
    /// </summary>
    AcceleratorCapabilities Capabilities { get; }

    /// <summary>
    /// Compiles kernel source code for this backend
    /// </summary>
    Task<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Allocates device memory
    /// </summary>
    Task<IUnifiedBuffer<T>> AllocateAsync<T>(
        long elementCount,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Synchronizes with device (waits for all operations to complete)
    /// </summary>
    Task SynchronizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current memory usage statistics
    /// </summary>
    MemoryStatistics GetMemoryStatistics();
}
```

**Design Rationale**:
- **Async-first**: Non-blocking operations for all I/O and device interactions
- **Unified memory**: `IUnifiedBuffer<T>` abstracts device-specific memory
- **Capability detection**: Runtime feature detection for optimal code paths
- **Explicit synchronization**: Clear control over device/host synchronization
- **Resource tracking**: Memory statistics for monitoring and optimization

## Backend Plugin Architecture

### Plugin Lifecycle

```
1. Discovery
   - Scan plugin directories
   - Load plugin manifests
   - Validate signatures (optional)
   ↓
2. Loading
   - Create isolated AssemblyLoadContext
   - Load plugin assembly
   - Resolve dependencies
   ↓
3. Initialization
   - Create plugin instance
   - Call Initialize()
   - Register with AcceleratorManager
   ↓
4. Runtime
   - Handle CreateAccelerator requests
   - Execute kernel compilation
   - Manage device resources
   ↓
5. Unloading (Hot-Reload)
   - Call Dispose()
   - Unload AssemblyLoadContext
   - Clean up resources
```

### IBackendPlugin Interface

```csharp
public interface IBackendPlugin : IAsyncDisposable
{
    /// <summary>
    /// Plugin metadata (name, version, author)
    /// </summary>
    PluginMetadata Metadata { get; }

    /// <summary>
    /// Supported accelerator type
    /// </summary>
    AcceleratorType SupportedType { get; }

    /// <summary>
    /// Initializes the plugin
    /// </summary>
    Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken);

    /// <summary>
    /// Creates an accelerator instance
    /// </summary>
    Task<IAccelerator> CreateAcceleratorAsync(
        AcceleratorConfiguration configuration,
        CancellationToken cancellationToken);

    /// <summary>
    /// Enumerates available devices for this backend
    /// </summary>
    Task<IReadOnlyList<DeviceInfo>> EnumerateDevicesAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks if this backend is available on the current system
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets plugin health status
    /// </summary>
    PluginHealthStatus GetHealthStatus();
}
```

## Backend Implementations

### CPU Backend

**Implementation**: `CpuAccelerator` in `DotCompute.Backends.CPU`

**Key Features**:
- SIMD vectorization (AVX512, AVX2, SSE4.2, NEON)
- Multi-threaded execution via `Parallel.For`
- Zero-copy operations with `Span<T>`
- Hardware capability detection at runtime

**Compilation Strategy**:
```csharp
// CPU kernels use .NET JIT with SIMD intrinsics
public async Task<ICompiledKernel> CompileKernelAsync(KernelDefinition definition)
{
    // 1. Analyze kernel for SIMD opportunities
    var vectorizable = AnalyzeVectorizability(definition);

    // 2. Generate SIMD-optimized code
    if (vectorizable && Vector.IsHardwareAccelerated)
    {
        return GenerateSimdKernel(definition);
    }

    // 3. Fall back to scalar implementation
    return GenerateScalarKernel(definition);
}
```

**Performance**: 8-23x speedup on vectorizable operations

### CUDA Backend

**Implementation**: `CudaAccelerator` in `DotCompute.Backends.CUDA`

**Key Features**:
- NVIDIA GPU support (Compute Capability 5.0+)
- NVRTC runtime compilation
- PTX and CUBIN generation
- Unified memory and pinned memory optimization

**Compilation Strategy**:
```csharp
// CUDA kernels compiled at runtime via NVRTC
public async Task<ICompiledKernel> CompileKernelAsync(KernelDefinition definition)
{
    // 1. Detect compute capability
    var computeCapability = CudaCapabilityManager.GetTargetComputeCapability();

    // 2. Choose compilation target
    if (computeCapability >= new Version(7, 0))
    {
        // Modern GPUs: Compile to CUBIN for best performance
        return await CompileToCubinAsync(definition, computeCapability);
    }
    else
    {
        // Legacy GPUs: Compile to PTX for compatibility
        return await CompileToPtxAsync(definition, computeCapability);
    }
}
```

**Compute Capability Detection**:
```csharp
public static class CudaCapabilityManager
{
    public static Version GetTargetComputeCapability(int deviceId = 0)
    {
        // Query device properties
        cuDeviceGetAttribute(out int major, DeviceAttribute.ComputeCapabilityMajor, deviceId);
        cuDeviceGetAttribute(out int minor, DeviceAttribute.ComputeCapabilityMinor, deviceId);

        return new Version(major, minor);
    }

    public static bool SupportsFeature(CudaFeature feature, int deviceId = 0)
    {
        var capability = GetTargetComputeCapability(deviceId);

        return feature switch
        {
            CudaFeature.CooperativeGroups => capability >= new Version(6, 0),
            CudaFeature.TensorCores => capability >= new Version(7, 0),
            CudaFeature.UnifiedMemory => capability >= new Version(3, 0),
            _ => false
        };
    }
}
```

**Performance**: 21-92x speedup vs CPU (measured on RTX 2000 Ada)

### Metal Backend

**Implementation**: `MetalAccelerator` in `DotCompute.Backends.Metal`

**Key Features**:
- Apple Silicon optimization (M1/M2/M3)
- Unified memory architecture
- GPU family auto-detection
- Metal Shading Language (MSL) compilation

**Compilation Strategy**:
```csharp
// Metal kernels compiled via Metal compiler
public async Task<ICompiledKernel> CompileKernelAsync(KernelDefinition definition)
{
    // 1. Translate to Metal Shading Language (MSL)
    string mslSource = TranslateToMsl(definition);

    // 2. Compile via Metal framework
    using var library = await device.NewLibraryAsync(mslSource);
    var function = library.NewFunction(definition.EntryPoint);

    // 3. Create compute pipeline
    var pipeline = await device.NewComputePipelineStateAsync(function);

    return new MetalCompiledKernel(pipeline, definition);
}
```

**Unified Memory Optimization**:
```csharp
// Metal leverages unified memory for 2-3x speedup
public async Task<IUnifiedBuffer<T>> AllocateAsync<T>(long elementCount)
{
    // Allocate in shared memory (visible to both CPU and GPU)
    var buffer = device.NewBuffer(
        elementCount * sizeof(T),
        MTLResourceOptions.StorageModeShared
    );

    // Zero-copy: CPU can access directly without transfer
    return new MetalUnifiedBuffer<T>(buffer, isSharedMemory: true);
}
```

**Performance**: 37-141x speedup vs CPU, 2-3x unified memory advantage

### OpenCL Backend

**Implementation**: `OpenCLAccelerator` in `DotCompute.Backends.OpenCL`

**Status**: Foundation complete, full integration in progress

**Key Features**:
- Cross-platform GPU support (NVIDIA, AMD, Intel)
- Runtime compilation via OpenCL C
- Platform and device enumeration
- Workgroup size optimization

## Backend Selection Strategy

### Automatic Selection

The orchestrator uses workload characteristics to select the optimal backend:

```csharp
public class WorkloadCharacteristics
{
    /// <summary>
    /// Total data size in bytes
    /// </summary>
    public long DataSize { get; set; }

    /// <summary>
    /// Compute intensity (operations per byte)
    /// </summary>
    public ComputeIntensity ComputeIntensity { get; set; }

    /// <summary>
    /// Memory access pattern (sequential vs random)
    /// </summary>
    public MemoryAccessPattern AccessPattern { get; set; }

    /// <summary>
    /// Potential for parallelism
    /// </summary>
    public ParallelismLevel ParallelismPotential { get; set; }
}

public interface IBackendSelector
{
    Task<IAccelerator> SelectBackendAsync(
        WorkloadCharacteristics characteristics,
        CancellationToken cancellationToken);
}
```

**Selection Algorithm**:

```csharp
public async Task<IAccelerator> SelectBackendAsync(WorkloadCharacteristics characteristics)
{
    // 1. Rule-based selection for clear cases
    if (characteristics.DataSize < 10_000) // Small data
    {
        return GetAccelerator(AcceleratorType.CPU); // No GPU transfer overhead
    }

    if (characteristics.ComputeIntensity == ComputeIntensity.Low)
    {
        return GetAccelerator(AcceleratorType.CPU); // Memory-bound, CPU is faster
    }

    // 2. ML-based selection for complex cases
    if (mlModel != null)
    {
        var prediction = await mlModel.PredictOptimalBackendAsync(characteristics);
        return GetAccelerator(prediction.BackendType);
    }

    // 3. Default: Prefer GPU for high parallelism
    if (characteristics.ParallelismPotential == ParallelismLevel.High)
    {
        return GetAccelerator(AcceleratorType.CUDA) // Try CUDA first
            ?? GetAccelerator(AcceleratorType.Metal) // Fall back to Metal
            ?? GetAccelerator(AcceleratorType.CPU);  // Fall back to CPU
    }

    // 4. CPU as final fallback
    return GetAccelerator(AcceleratorType.CPU);
}
```

### Manual Override

Users can force specific backends:

```csharp
// Force CUDA backend
services.AddDotComputeRuntime(options =>
{
    options.DefaultAccelerator = AcceleratorType.CUDA;
    options.EnableAutoOptimization = false;
});

// Or specify per-execution
await orchestrator.ExecuteKernelAsync(
    "MyKernel",
    parameters,
    forceBackend: AcceleratorType.Metal
);
```

## Multi-Backend Coordination

### Backend Registry

```csharp
public class AcceleratorManager : IAcceleratorManager
{
    private readonly Dictionary<AcceleratorType, IAccelerator> _accelerators = new();

    public async Task<IAccelerator> GetOrCreateAcceleratorAsync(AcceleratorType type)
    {
        if (_accelerators.TryGetValue(type, out var accelerator))
        {
            return accelerator; // Return cached instance
        }

        // Create new accelerator via factory
        accelerator = await _factory.CreateAcceleratorAsync(type);
        _accelerators[type] = accelerator;

        return accelerator;
    }

    public IReadOnlyList<AcceleratorType> GetAvailableBackends()
    {
        return _plugins
            .Where(p => p.IsAvailableAsync().Result)
            .Select(p => p.SupportedType)
            .ToList();
    }
}
```

### Cross-Backend Validation

The debugging system can validate results across backends:

```csharp
public async Task<ValidationResult> ValidateAcrossBackendsAsync(
    string kernelName,
    object parameters)
{
    // 1. Execute on primary backend (e.g., GPU)
    var primaryResult = await ExecuteOnBackendAsync(
        kernelName,
        parameters,
        primaryBackend: AcceleratorType.CUDA
    );

    // 2. Execute on reference backend (always CPU)
    var referenceResult = await ExecuteOnBackendAsync(
        kernelName,
        parameters,
        referenceBackend: AcceleratorType.CPU
    );

    // 3. Compare results within tolerance
    var differences = CompareResults(primaryResult, referenceResult, tolerance: 1e-5);

    // 4. Log discrepancies
    if (differences.Any())
    {
        logger.LogWarning(
            "Cross-backend validation found {Count} differences for kernel {Kernel}",
            differences.Count,
            kernelName
        );
    }

    return new ValidationResult
    {
        IsValid = !differences.Any(),
        Differences = differences,
        PrimaryBackend = AcceleratorType.CUDA,
        ReferenceBackend = AcceleratorType.CPU
    };
}
```

## Plugin Loading and Isolation

### Plugin Discovery

```csharp
public class PluginManager : IPluginManager
{
    private const string PluginDirectory = "plugins";

    public async Task<IReadOnlyList<IBackendPlugin>> DiscoverPluginsAsync()
    {
        var plugins = new List<IBackendPlugin>();

        // 1. Scan plugin directory for manifests
        var manifestFiles = Directory.GetFiles(PluginDirectory, "plugin.json", SearchOption.AllDirectories);

        foreach (var manifestFile in manifestFiles)
        {
            // 2. Load and validate manifest
            var manifest = await LoadManifestAsync(manifestFile);

            if (!ValidateManifest(manifest))
            {
                logger.LogWarning("Invalid plugin manifest: {Path}", manifestFile);
                continue;
            }

            // 3. Load plugin assembly
            var plugin = await LoadPluginAsync(manifest);
            plugins.Add(plugin);
        }

        return plugins;
    }
}
```

### Assembly Isolation

Each plugin runs in isolated `AssemblyLoadContext`:

```csharp
public class PluginLoader
{
    public async Task<IBackendPlugin> LoadPluginAsync(PluginManifest manifest)
    {
        // 1. Create isolated load context
        var loadContext = new PluginLoadContext(manifest.AssemblyPath);

        // 2. Load plugin assembly
        var assembly = loadContext.LoadFromAssemblyPath(manifest.AssemblyPath);

        // 3. Find plugin type
        var pluginType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IBackendPlugin).IsAssignableFrom(t));

        if (pluginType == null)
        {
            throw new InvalidOperationException(
                $"Plugin assembly {manifest.AssemblyPath} does not contain IBackendPlugin implementation"
            );
        }

        // 4. Create plugin instance
        var plugin = (IBackendPlugin)Activator.CreateInstance(pluginType)!;

        // 5. Initialize plugin
        await plugin.InitializeAsync(_serviceProvider, CancellationToken.None);

        return plugin;
    }
}

internal class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Resolve dependencies relative to plugin directory
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
    }
}
```

**Benefits of Isolation**:
- **Hot-reload**: Unload and reload plugins without restarting
- **Versioning**: Multiple versions of dependencies
- **Security**: Limit plugin access to host resources
- **Stability**: Plugin crashes don't affect host

## Backend Capability Detection

### Runtime Feature Detection

```csharp
public class AcceleratorCapabilities
{
    /// <summary>
    /// Maximum work-group size for compute kernels
    /// </summary>
    public int MaxWorkGroupSize { get; init; }

    /// <summary>
    /// Maximum number of threads per block/work-group
    /// </summary>
    public int MaxThreadsPerBlock { get; init; }

    /// <summary>
    /// Maximum shared memory per work-group (bytes)
    /// </summary>
    public long MaxSharedMemoryPerWorkGroup { get; init; }

    /// <summary>
    /// Maximum global memory (bytes)
    /// </summary>
    public long MaxGlobalMemory { get; init; }

    /// <summary>
    /// Supports double-precision floating point
    /// </summary>
    public bool SupportsDouble { get; init; }

    /// <summary>
    /// Supports half-precision floating point
    /// </summary>
    public bool SupportsHalf { get; init; }

    /// <summary>
    /// Supports atomic operations
    /// </summary>
    public bool SupportsAtomics { get; init; }

    /// <summary>
    /// Supports unified memory (CPU/GPU shared)
    /// </summary>
    public bool SupportsUnifiedMemory { get; init; }

    /// <summary>
    /// Backend-specific extended capabilities
    /// </summary>
    public Dictionary<string, object> ExtendedCapabilities { get; init; }
}
```

### CUDA-Specific Capabilities

```csharp
public static class CudaCapabilities
{
    public static AcceleratorCapabilities GetCapabilities(int deviceId = 0)
    {
        // Query device properties
        cuDeviceGetAttribute(out int maxThreadsPerBlock,
            DeviceAttribute.MaxThreadsPerBlock, deviceId);
        cuDeviceGetAttribute(out int sharedMemPerBlock,
            DeviceAttribute.MaxSharedMemoryPerBlock, deviceId);
        cuDeviceGetAttribute(out int computeCapabilityMajor,
            DeviceAttribute.ComputeCapabilityMajor, deviceId);
        cuDeviceGetAttribute(out int computeCapabilityMinor,
            DeviceAttribute.ComputeCapabilityMinor, deviceId);

        var computeCapability = new Version(computeCapabilityMajor, computeCapabilityMinor);

        return new AcceleratorCapabilities
        {
            MaxThreadsPerBlock = maxThreadsPerBlock,
            MaxSharedMemoryPerWorkGroup = sharedMemPerBlock,
            SupportsDouble = computeCapability >= new Version(1, 3),
            SupportsHalf = computeCapability >= new Version(5, 3),
            SupportsAtomics = computeCapability >= new Version(2, 0),
            SupportsUnifiedMemory = computeCapability >= new Version(3, 0),
            ExtendedCapabilities = new Dictionary<string, object>
            {
                ["ComputeCapability"] = computeCapability,
                ["TensorCores"] = computeCapability >= new Version(7, 0),
                ["CooperativeGroups"] = computeCapability >= new Version(6, 0)
            }
        };
    }
}
```

### Metal-Specific Capabilities

```csharp
public static class MetalCapabilities
{
    public static AcceleratorCapabilities GetCapabilities(MTLDevice device)
    {
        return new AcceleratorCapabilities
        {
            MaxThreadsPerBlock = (int)device.MaxThreadsPerThreadgroup.Width,
            MaxSharedMemoryPerWorkGroup = (long)device.MaxThreadgroupMemoryLength,
            MaxGlobalMemory = (long)device.RecommendedMaxWorkingSetSize,
            SupportsDouble = false, // Metal doesn't support double precision
            SupportsHalf = true,
            SupportsAtomics = true,
            SupportsUnifiedMemory = true, // Always true on Apple Silicon
            ExtendedCapabilities = new Dictionary<string, object>
            {
                ["GPUFamily"] = GetGPUFamily(device),
                ["Architecture"] = device.Architecture,
                ["UnifiedMemoryArchitecture"] = true
            }
        };
    }

    private static string GetGPUFamily(MTLDevice device)
    {
        // Detect Apple Silicon generation
        if (device.SupportsFamily(MTLGPUFamily.Apple9))
            return "Apple9"; // M3
        if (device.SupportsFamily(MTLGPUFamily.Apple8))
            return "Apple8"; // M2
        if (device.SupportsFamily(MTLGPUFamily.Apple7))
            return "Apple7"; // M1

        return "Unknown";
    }
}
```

## Source Generator Integration

### Backend-Specific Code Generation

The source generator creates optimized code for each backend:

```csharp
// User writes this:
[Kernel]
public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
    {
        result[idx] = a[idx] + b[idx];
    }
}

// Generator creates CPU SIMD version:
private static void VectorAdd_CPU_SIMD(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
{
    int vectorSize = Vector<float>.Count;
    int i = 0;

    // Vectorized loop
    for (; i <= result.Length - vectorSize; i += vectorSize)
    {
        var va = new Vector<float>(a.Slice(i));
        var vb = new Vector<float>(b.Slice(i));
        (va + vb).CopyTo(result.Slice(i));
    }

    // Scalar remainder
    for (; i < result.Length; i++)
    {
        result[i] = a[i] + b[i];
    }
}

// Generator creates CUDA version:
private const string VectorAdd_CUDA_Source = @"
extern ""C"" __global__ void VectorAdd(
    const float* a,
    const float* b,
    float* result,
    int length)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < length)
    {
        result[idx] = a[idx] + b[idx];
    }
}
";

// Generator creates Metal version:
private const string VectorAdd_Metal_Source = @"
#include <metal_stdlib>
using namespace metal;

kernel void VectorAdd(
    device const float* a [[buffer(0)]],
    device const float* b [[buffer(1)]],
    device float* result [[buffer(2)]],
    constant int& length [[buffer(3)]],
    uint idx [[thread_position_in_grid]])
{
    if (idx < length)
    {
        result[idx] = a[idx] + b[idx];
    }
}
";
```

### Registration Generation

The source generator also creates registration code:

```csharp
// Generated registration class
public static class GeneratedKernels
{
    public static void Register(IKernelRegistry registry)
    {
        registry.RegisterKernel(new KernelMetadata
        {
            Name = "VectorAdd",
            Namespace = "MyNamespace",
            DeclaringType = "MyClass",
            Parameters = new[]
            {
                new ParameterMetadata { Name = "a", Type = typeof(ReadOnlySpan<float>) },
                new ParameterMetadata { Name = "b", Type = typeof(ReadOnlySpan<float>) },
                new ParameterMetadata { Name = "result", Type = typeof(Span<float>) }
            },
            Backends = KernelBackends.CPU | KernelBackends.CUDA | KernelBackends.Metal,
            IsParallel = true,

            // CPU implementation
            CpuImplementation = (args) => VectorAdd_CPU_SIMD(
                args.Get<ReadOnlySpan<float>>("a"),
                args.Get<ReadOnlySpan<float>>("b"),
                args.Get<Span<float>>("result")
            ),

            // GPU source code
            CudaSource = VectorAdd_CUDA_Source,
            MetalSource = VectorAdd_Metal_Source,

            // Compiler options
            CompilerOptions = new CompilationOptions
            {
                OptimizationLevel = OptimizationLevel.Aggressive,
                GenerateDebugInfo = false,
                EnableFastMath = true
            }
        });
    }
}
```

## Performance Characteristics

### Backend Overhead

| Backend | Initialization | Compilation | Execution Overhead | Notes |
|---------|---------------|-------------|-------------------|-------|
| CPU | < 1ms | N/A (JIT) | < 10μs | Zero-copy, no transfers |
| CUDA | ~100ms | 50-200ms | ~50μs | Device init, NVRTC compile |
| Metal | ~50ms | 20-100ms | ~30μs | Device init, MSL compile |
| OpenCL | ~150ms | 100-300ms | ~80μs | Platform detection, compile |

**Notes**:
- Initialization is one-time per application lifetime
- Compilation is cached for subsequent executions
- Execution overhead is per-kernel launch

### Backend Selection Overhead

- **Rule-based selection**: < 10μs
- **ML-based selection**: ~100μs (inference time)
- **Device enumeration**: ~1ms (cached)

### Plugin System Overhead

- **Plugin loading**: ~10-50ms per plugin (one-time)
- **Hot-reload**: ~20-100ms (unload + reload)
- **Isolated execution**: < 5% performance impact vs direct calls

## Testing Strategy

### Backend Tests

Each backend has comprehensive test suite:

```csharp
[Fact]
public async Task CompileKernel_ValidDefinition_CompilesToExecutable()
{
    // Arrange
    var accelerator = await CreateAcceleratorAsync();
    var definition = new KernelDefinition
    {
        Name = "SimpleAdd",
        Source = "/* kernel source */",
        EntryPoint = "SimpleAdd"
    };

    // Act
    var compiled = await accelerator.CompileKernelAsync(definition, CompilationOptions.Default);

    // Assert
    Assert.NotNull(compiled);
    Assert.Equal("SimpleAdd", compiled.EntryPoint);
}

[SkippableFact] // Skip if hardware not available
public async Task Execute_OnGpu_ProducesCorrectResults()
{
    Skip.IfNot(await IsGpuAvailableAsync(), "GPU not available");

    // Arrange
    var accelerator = await CreateAcceleratorAsync();
    var input = Enumerable.Range(0, 1000).Select(i => (float)i).ToArray();

    // Act
    var result = await ExecuteKernelAsync(accelerator, input);

    // Assert
    Assert.Equal(input.Select(x => x * 2), result);
}
```

### Cross-Backend Validation Tests

```csharp
[Theory]
[InlineData(AcceleratorType.CPU, AcceleratorType.CUDA)]
[InlineData(AcceleratorType.CPU, AcceleratorType.Metal)]
public async Task Execute_AcrossBackends_ProducesSameResults(
    AcceleratorType reference,
    AcceleratorType target)
{
    // Arrange
    var input = GenerateTestData(10_000);

    // Act
    var referenceResult = await ExecuteOnBackendAsync(reference, input);
    var targetResult = await ExecuteOnBackendAsync(target, input);

    // Assert
    AssertResultsEqual(referenceResult, targetResult, tolerance: 1e-5f);
}
```

## Related Documentation

- [Architecture Overview](overview.md)
- [Core Orchestration](core-orchestration.md)
- [Memory Management](memory-management.md)
- [Plugin Development Guide](../guides/plugin-development.md)
- [Backend Selection Guide](../guides/backend-selection.md)
