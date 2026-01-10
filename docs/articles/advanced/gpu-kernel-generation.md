# GPU Kernel Generation Deep Dive

This advanced guide provides comprehensive technical details about DotCompute.Linq's GPU kernel generation system, including compilation pipeline, optimization strategies, and backend-specific implementation details.

## Architecture Overview

The GPU kernel generation system consists of five major components:

```
LINQ Expression
    ↓
ExpressionTreeVisitor → OperationGraph (AST)
    ↓
TypeInferenceEngine → Type Validation & Metadata
    ↓
GpuKernelGenerator → Backend-Specific Code (CUDA C / OpenCL C / MSL)
    ↓
RuntimeCompiler → Device Binary (PTX/CUBIN / SPIR-V / AIR)
    ↓
GPU Execution → Results
```

## Component Details

### 1. Expression Tree Visitor

Converts LINQ expressions to an intermediate `OperationGraph` representation:

```csharp
public class ExpressionTreeVisitor
{
    public OperationGraph Visit(Expression expression)
    {
        var graph = new OperationGraph();

        // Traverse expression tree
        switch (expression.NodeType)
        {
            case ExpressionType.Call:
                var methodCall = (MethodCallExpression)expression;
                if (methodCall.Method.Name == "Select")
                {
                    graph.AddOperation(new Operation
                    {
                        Type = OperationType.Map,
                        Lambda = ExtractLambda(methodCall)
                    });
                }
                break;

            case ExpressionType.Lambda:
                // Process lambda body recursively
                break;
        }

        return graph;
    }
}
```

### 2. Operation Graph

Internal representation of computation:

```csharp
public class OperationGraph
{
    public List<Operation> Operations { get; }
    public TypeMetadata InputType { get; set; }
    public TypeMetadata OutputType { get; set; }

    // Dependency analysis
    public Dictionary<Operation, List<Operation>> Dependencies { get; }
}

public enum OperationType
{
    Map,        // Select: T → U
    Filter,     // Where: T → bool
    Reduce,     // Sum/Average/Count: T[] → T
    Scan,       // Prefix sum: T[] → T[]
    Sort        // OrderBy: T[] → T[]
}
```

### 3. Type Inference Engine

Validates types and determines backend capabilities:

```csharp
public class TypeInferenceEngine
{
    public TypeMetadata InferTypes(OperationGraph graph)
    {
        var metadata = new TypeMetadata
        {
            ElementType = graph.InputType.ElementType,
            IsVectorizable = CanVectorize(graph.InputType.ElementType),
            GpuSupported = IsGpuCompatible(graph.InputType.ElementType)
        };

        // Check each operation
        foreach (var op in graph.Operations)
        {
            ValidateOperation(op, metadata);
        }

        return metadata;
    }

    private bool CanVectorize(Type type)
    {
        // Vector<T> supports byte, sbyte, short, ushort, int, uint, long, ulong, float, double
        return type.IsPrimitive &&
               Vector.IsHardwareAccelerated &&
               Vector<byte>.Count > 1;
    }

    private bool IsGpuCompatible(Type type)
    {
        // GPU kernels support: byte, int, float, double
        return type == typeof(byte) || type == typeof(int) ||
               type == typeof(float) || type == typeof(double);
    }
}
```

## GPU Kernel Code Generation

Each backend has a specialized kernel generator implementing `IGpuKernelGenerator`:

### CUDA Kernel Generator

Generates CUDA C code for NVIDIA GPUs:

```csharp
public class CudaKernelGenerator : IGpuKernelGenerator
{
    public string GenerateCudaKernel(OperationGraph graph, TypeMetadata metadata)
    {
        var code = new StringBuilder();

        // 1. Header
        code.AppendLine("extern \"C\" __global__ void Execute(");

        // 2. Parameters
        var cudaType = MapToCudaType(metadata.ElementType);
        code.AppendLine($"    const {cudaType}* input,");
        code.AppendLine($"    {cudaType}* output,");

        // Add output counter for filter operations
        if (HasFilterOperation(graph))
        {
            code.AppendLine("    int* outputCount,");
        }

        code.AppendLine("    int length)");
        code.AppendLine("{");

        // 3. Thread indexing
        code.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
        code.AppendLine("    if (idx < length) {");

        // 4. Operation code generation
        if (CanFuseOperations(graph.Operations))
        {
            code.Append(GenerateFusedOperation(graph.Operations, metadata));
        }
        else
        {
            foreach (var op in graph.Operations)
            {
                code.Append(GenerateOperation(op, metadata));
            }
        }

        // 5. Close kernel
        code.AppendLine("    }");
        code.AppendLine("}");

        return code.ToString();
    }

    private string MapToCudaType(Type type)
    {
        return type.Name switch
        {
            "Byte" => "unsigned char",
            "Int32" => "int",
            "Single" => "float",
            "Double" => "double",
            _ => throw new NotSupportedException($"Type {type.Name} not supported on GPU")
        };
    }
}
```

### OpenCL Kernel Generator

Generates OpenCL C code for cross-platform GPUs:

```csharp
public class OpenCLKernelGenerator : IGpuKernelGenerator
{
    public string GenerateOpenCLKernel(OperationGraph graph, TypeMetadata metadata)
    {
        var code = new StringBuilder();

        // 1. Kernel signature
        code.Append("__kernel void Execute(");

        // 2. Parameters with OpenCL memory qualifiers
        var oclType = MapToOpenCLType(metadata.ElementType);
        code.AppendLine($"    __global const {oclType}* input,");
        code.AppendLine($"    __global {oclType}* output,");

        if (HasFilterOperation(graph))
        {
            code.AppendLine("    __global int* outputCount,");
        }

        code.AppendLine("    const int length)");
        code.AppendLine("{");

        // 3. Thread indexing (OpenCL style)
        code.AppendLine("    int idx = get_global_id(0);");
        code.AppendLine("    if (idx < length) {");

        // 4. Generate operation code
        // ... similar to CUDA but with OpenCL syntax

        code.AppendLine("    }");
        code.AppendLine("}");

        return code.ToString();
    }

    private string MapToOpenCLType(Type type)
    {
        return type.Name switch
        {
            "Byte" => "uchar",
            "Int32" => "int",
            "Single" => "float",
            "Double" => "double",
            _ => throw new NotSupportedException()
        };
    }
}
```

### Metal Kernel Generator

Generates Metal Shading Language (MSL) code for Apple GPUs:

```csharp
public class MetalKernelGenerator : IGpuKernelGenerator
{
    public string GenerateMetalKernel(OperationGraph graph, TypeMetadata metadata)
    {
        var code = new StringBuilder();

        // 1. Metal headers
        code.AppendLine("#include <metal_stdlib>");
        code.AppendLine("using namespace metal;");
        code.AppendLine();

        // 2. Kernel function
        code.Append("kernel void ComputeKernel(");

        // 3. Buffer attributes (Metal specific)
        var metalType = MapToMetalType(metadata.ElementType);
        code.AppendLine($"    device const {metalType}* input [[buffer(0)]],");
        code.AppendLine($"    device {metalType}* output [[buffer(1)]],");

        if (HasFilterOperation(graph))
        {
            code.AppendLine("    device atomic_int* outputCount [[buffer(2)]],");
        }

        code.AppendLine($"    constant int& length [[buffer(3)]],");
        code.AppendLine("    uint idx [[thread_position_in_grid]])");
        code.AppendLine("{");

        // 4. Bounds checking
        code.AppendLine("    if (idx >= length) { return; }");

        // 5. Generate operation code
        // ... similar to CUDA but with Metal syntax

        code.AppendLine("}");

        return code.ToString();
    }
}
```

## Kernel Fusion Optimization

### Fusion Detection Algorithm

The system automatically detects fusable operation patterns:

```csharp
public class KernelFusionOptimizer
{
    private List<Operation> IdentifyFusableOperations(List<Operation> operations)
    {
        var fusable = new List<Operation>();

        for (int i = 0; i < operations.Count; i++)
        {
            var current = operations[i];

            // Check if current operation can be fused
            if (!CanBeFused(current))
            {
                if (fusable.Count == 0) fusable.Add(current);
                break;
            }

            fusable.Add(current);

            // Check if we can continue fusing with next operation
            if (i + 1 < operations.Count)
            {
                var next = operations[i + 1];

                // Fusable patterns: Map-Filter, Filter-Map, Map-Map, Filter-Filter
                if (CanFuseTogether(current, next))
                {
                    continue; // Can fuse with next
                }
                else
                {
                    break; // Stop fusion
                }
            }
        }

        return fusable;
    }

    private bool CanFuseTogether(Operation current, Operation next)
    {
        // Map can fuse with Map or Filter
        if (current.Type == OperationType.Map)
        {
            return next.Type == OperationType.Map ||
                   next.Type == OperationType.Filter;
        }

        // Filter can fuse with Map or Filter
        if (current.Type == OperationType.Filter)
        {
            return next.Type == OperationType.Map ||
                   next.Type == OperationType.Filter;
        }

        // Reduce operations cannot be fused (require synchronization)
        return false;
    }
}
```

### Fused Code Generation

Example of generating fused Map→Filter→Map kernel:

```csharp
private string GenerateFusedOperation(List<Operation> operations, TypeMetadata metadata)
{
    var code = new StringBuilder();
    var cudaType = MapToCudaType(metadata.ElementType);

    // 1. Load input value
    code.AppendLine($"        {cudaType} value = input[idx];");
    code.AppendLine("        bool passesFilter = true;");
    code.AppendLine();

    // 2. Generate code for each operation
    foreach (var op in operations)
    {
        switch (op.Type)
        {
            case OperationType.Map:
                // Generate map transformation
                var transform = GenerateTransformExpression(op.Lambda);
                code.AppendLine($"        // Map: {op.Lambda}");
                code.AppendLine("        if (passesFilter) {");
                code.AppendLine($"            value = {transform};");
                code.AppendLine("        }");
                break;

            case OperationType.Filter:
                // Generate filter predicate
                var predicate = GeneratePredicateExpression(op.Lambda);
                code.AppendLine($"        // Filter: {op.Lambda}");
                code.AppendLine($"        passesFilter = passesFilter && ({predicate});");
                break;
        }
        code.AppendLine();
    }

    // 3. Write result only if passed all filters
    code.AppendLine("        if (passesFilter) {");
    code.AppendLine("            output[idx] = value;");
    code.AppendLine("        }");

    return code.ToString();
}
```

**Generated CUDA Kernel**:
```cuda
extern "C" __global__ void Execute(const float* input, float* output, int length)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < length) {
        float value = input[idx];
        bool passesFilter = true;

        // Map: x * 2
        if (passesFilter) {
            value = (value * 2.0f);
        }

        // Filter: x > 1000
        passesFilter = passesFilter && ((value > 1000.0f));

        // Map: x + 100
        if (passesFilter) {
            value = (value + 100.0f);
        }

        if (passesFilter) {
            output[idx] = value;
        }
    }
}
```

## Filter Compaction with Atomic Operations

Filter operations produce variable-length output, requiring **stream compaction**:

### Algorithm

```csharp
private string GenerateFilterOperation(Operation filterOp, TypeMetadata metadata)
{
    var code = new StringBuilder();
    var predicate = GeneratePredicateExpression(filterOp.Lambda);

    // Evaluate predicate
    code.AppendLine($"        if ({predicate}) {{");

    // Atomically allocate output position
    code.AppendLine("            // Atomically allocate output position");
    code.AppendLine("            int outIdx = atomicAdd(outputCount, 1);");

    // Write passing element to compacted output
    code.AppendLine("            output[outIdx] = input[idx];");
    code.AppendLine("        }");

    return code.ToString();
}
```

### Backend-Specific Atomic Operations

| Backend | Atomic Function | Memory Order | Compatibility |
|---------|----------------|--------------|---------------|
| **CUDA** | `atomicAdd(outputCount, 1)` | Relaxed | CC 2.0+ (all modern GPUs) |
| **OpenCL** | `atomic_inc(outputCount)` | Relaxed | OpenCL 1.2+ (broad support) |
| **Metal** | `atomic_fetch_add_explicit(outputCount, 1, memory_order_relaxed)` | Explicit | Metal 2.0+ (Apple Silicon, AMD) |

### Performance Characteristics

**Hardware Atomic Performance**:
- **NVIDIA GPUs**: Hardware-optimized atomic on global memory (< 10 cycles)
- **AMD GPUs**: Hardware atomic support via GCN/RDNA architecture
- **Intel GPUs**: Atomic support via Gen9+ graphics architecture
- **Apple Silicon**: Atomic operations in L2 cache (very fast on M-series chips)

**Contention Management**:
```cuda
// Low contention: Each thread likely hits different cache line
// High throughput: Millions of atomics per second

// Example: Filtering 1M elements with 50% selectivity
// - 500K atomic operations total
// - Distributed across ~50K warps on RTX 3090
// - Average ~10 atomics per warp
// - Minimal contention, near-peak throughput
```

## Runtime Compilation

### CUDA: NVRTC Compilation

```csharp
public class CudaRuntimeCompiler
{
    public CompiledKernel Compile(string cudaCode, CompilationOptions options)
    {
        // 1. Create NVRTC program
        var program = NvrtcCreateProgram(cudaCode, "kernel.cu");

        // 2. Compilation options
        var compileOpts = new[]
        {
            $"--gpu-architecture=compute_{options.ComputeCapability}",
            $"--gpu-code=sm_{options.ComputeCapability}",
            "--use_fast_math",
            "--extra-device-vectorization"
        };

        // 3. Compile to PTX or CUBIN
        var result = NvrtcCompileProgram(program, compileOpts);
        if (result != NvrtcResult.Success)
        {
            var log = NvrtcGetProgramLog(program);
            throw new CompilationException(log);
        }

        // 4. Get compiled binary
        var binary = options.ComputeCapability >= 70
            ? NvrtcGetCUBIN(program) // CUBIN for CC 7.0+
            : NvrtcGetPTX(program);  // PTX for older architectures

        // 5. Load module and get kernel function
        var module = CudaModuleLoadData(binary);
        var kernel = CudaModuleGetFunction(module, "Execute");

        return new CompiledKernel
        {
            Module = module,
            Kernel = kernel,
            ThreadBlockSize = options.ThreadBlockSize
        };
    }
}
```

### OpenCL: Runtime Compilation

```csharp
public class OpenCLRuntimeCompiler
{
    public CompiledKernel Compile(string openclCode, CompilationOptions options)
    {
        // 1. Create OpenCL program
        var program = clCreateProgramWithSource(context, openclCode);

        // 2. Compilation options (vendor-specific optimizations)
        var compileOpts = options.Vendor switch
        {
            "NVIDIA" => "-cl-fast-relaxed-math -cl-mad-enable",
            "AMD" => "-cl-fast-relaxed-math -cl-std=CL2.0",
            "Intel" => "-cl-fast-relaxed-math",
            _ => "-cl-std=CL1.2"
        };

        // 3. Build program
        var result = clBuildProgram(program, devices, compileOpts);
        if (result != CL_SUCCESS)
        {
            var log = clGetProgramBuildInfo(program, device, CL_PROGRAM_BUILD_LOG);
            throw new CompilationException(log);
        }

        // 4. Create kernel
        var kernel = clCreateKernel(program, "Execute");

        return new CompiledKernel
        {
            Program = program,
            Kernel = kernel,
            WorkGroupSize = options.WorkGroupSize
        };
    }
}
```

### Metal: MSL Compilation

```csharp
public class MetalRuntimeCompiler
{
    public CompiledKernel Compile(string metalCode, CompilationOptions options)
    {
        // 1. Create Metal library from source
        var library = device.NewLibrary(metalCode, options.ToMetalOptions(), out var error);
        if (error != null)
        {
            throw new CompilationException(error.LocalizedDescription);
        }

        // 2. Get kernel function
        var function = library.NewFunction("ComputeKernel");
        if (function == null)
        {
            throw new Exception("Kernel function 'ComputeKernel' not found");
        }

        // 3. Create compute pipeline state
        var pipeline = device.NewComputePipelineState(function, out error);
        if (error != null)
        {
            throw new CompilationException(error.LocalizedDescription);
        }

        return new CompiledKernel
        {
            Library = library,
            Function = function,
            PipelineState = pipeline,
            ThreadExecutionWidth = (int)pipeline.ThreadExecutionWidth
        };
    }
}
```

## Kernel Caching

To avoid recompilation, kernels are cached based on operation signature:

```csharp
public class KernelCache
{
    private readonly ConcurrentDictionary<string, CompiledKernel> _cache = new();

    public CompiledKernel GetOrCompile(
        string signature,
        Func<CompiledKernel> compiler)
    {
        // Double-checked locking for thread-safe compilation
        if (_cache.TryGetValue(signature, out var cached))
        {
            return cached;
        }

        lock (_cache)
        {
            if (_cache.TryGetValue(signature, out cached))
            {
                return cached;
            }

            // Compile kernel
            var kernel = compiler();

            // Add to cache
            _cache[signature] = kernel;

            return kernel;
        }
    }

    private string ComputeSignature(OperationGraph graph, ComputeBackend backend)
    {
        var sb = new StringBuilder();
        sb.Append(backend.ToString());
        sb.Append(":");

        foreach (var op in graph.Operations)
        {
            sb.Append(op.Type.ToString());
            sb.Append("_");
            sb.Append(op.Lambda.ToString().GetHashCode());
            sb.Append("_");
        }

        sb.Append(graph.InputType.ElementType.Name);

        return sb.ToString();
    }
}
```

## Performance Analysis

### Memory Bandwidth Optimization

**Example**: Map→Filter→Map chain on 1M float elements (4 bytes each)

**Without Fusion** (3 kernels):
```
Memory Operations:
  Kernel 1 (Map):    Read 4MB  → Write 4MB  = 8MB
  Kernel 2 (Filter): Read 4MB  → Write 2MB  = 6MB  (50% pass)
  Kernel 3 (Map):    Read 2MB  → Write 2MB  = 4MB
Total: 18MB transferred

Time on RTX 3090 (750 GB/s bandwidth):
  18MB ÷ 750 GB/s ≈ 0.024ms (bandwidth-limited)
  + kernel launch overhead (3 kernels × 0.01ms) = 0.030ms
  Total: ~0.054ms
```

**With Fusion** (1 kernel):
```
Memory Operations:
  Fused Kernel: Read 4MB → Write 2MB = 6MB

Time on RTX 3090:
  6MB ÷ 750 GB/s ≈ 0.008ms (bandwidth-limited)
  + kernel launch overhead (1 kernel × 0.01ms) = 0.010ms
  Total: ~0.018ms

Speedup: 0.054ms ÷ 0.018ms ≈ 3.0x from fusion alone!
```

### GPU Utilization Analysis

**Thread Occupancy**:
```csharp
// Optimal thread block size calculation
var optimalBlockSize = device.MaxThreadsPerBlock;
var numBlocks = (dataSize + optimalBlockSize - 1) / optimalBlockSize;

// Example: 1M elements, 256 threads/block
// = 3,906 blocks
// On RTX 3090 (82 SMs, max 2048 threads/SM)
// = 164,864 hardware threads available
// = 256,000 work items → 100% occupancy!
```

## Advanced Features

### Custom Operation Support

Extend the system with custom operations:

```csharp
public class CustomOperationGenerator
{
    public string GenerateCustom(Operation op, TypeMetadata metadata)
    {
        // Parse custom lambda expression
        var lambda = op.Lambda;

        // Generate specialized GPU code
        // ...

        return generatedCode;
    }
}
```

### Multi-GPU Support

Execute across multiple GPUs:

```csharp
// Split data across GPUs
var gpuCount = CudaGetDeviceCount();
var chunkSize = data.Length / gpuCount;

var tasks = new Task<float[]>[gpuCount];
for (int i = 0; i < gpuCount; i++)
{
    var chunk = data.Skip(i * chunkSize).Take(chunkSize).ToArray();
    var gpuId = i;

    tasks[i] = Task.Run(() =>
    {
        CudaSetDevice(gpuId);
        // Backend selection is automatic based on available hardware
        return chunk
            .AsComputeQueryable()
            .Select(x => x * 2)
            .ToComputeArray();
    });
}

var results = await Task.WhenAll(tasks);
var combined = results.SelectMany(x => x).ToArray();
```

## Debugging and Profiling

### Enable Kernel Source Logging

```csharp
// Log generated kernel source code
var options = new CompilationOptions
{
    LogKernelSource = true,
    LogCompilationMetrics = true
};

var result = data
    .AsComputeQueryable()
    .WithOptions(options)
    .Select(x => x * 2)
    .ToComputeArray();

// Check logs for generated CUDA/OpenCL/Metal code
```

### NVIDIA Nsight Profiling

```bash
# Profile GPU kernel execution
nsight-compute \
    --set full \
    --export profile.ncu-rep \
    dotnet run --configuration Release

# Analyze results
ncu-ui profile.ncu-rep
```

### AMD ROCProfiler

```bash
# Profile OpenCL kernels on AMD GPUs
rocprof --stats dotnet run --configuration Release
```

## Further Reading

- **User Guide**: [LINQ GPU Acceleration Guide](../guides/linq-gpu-acceleration.md)
- **Examples**: [GPU Kernel Generation Examples](../examples/linq-gpu-examples.md)
- **Performance**: [Benchmarking LINQ Queries](../reference/performance-benchmarking.md)
- **API Reference**: [DotCompute.Linq API](../../api/DotCompute.Linq.html)

## References

- **CUDA Programming Guide**: [NVIDIA CUDA Documentation](https://docs.nvidia.com/cuda/)
- **OpenCL Specification**: [Khronos OpenCL](https://www.khronos.org/opencl/)
- **Metal Shading Language**: [Apple Metal Documentation](https://developer.apple.com/metal/)
- **DotCompute Source**: [GitHub Repository](https://github.com/mivertowski/DotCompute)
