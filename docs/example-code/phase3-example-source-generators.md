# Source Generator Examples

## 🔧 Complete Source Generator Usage Guide

### **Basic Kernel Definition and Generation**

```csharp
using DotCompute.Generators.Kernel;

// Simple vector operation kernel
[Kernel("VectorAdd", 
    Backends = BackendType.CPU | BackendType.CUDA | BackendType.Metal,
    VectorSize = 8,
    IsParallel = true)]
public static void VectorAdd(
    KernelContext ctx,
    ReadOnlySpan<float> a,
    ReadOnlySpan<float> b,
    Span<float> result)
{
    var i = ctx.GlobalId.X;
    if (i < result.Length)
        result[i] = a[i] + b[i];
}

// Generated code will include:
// - VectorAddCpuKernel.ExecuteSIMD() - SIMD vectorized version
// - VectorAddCpuKernel.ExecuteScalar() - Scalar fallback
// - VectorAddCpuKernel.ExecuteParallel() - Multi-threaded version
// - VectorAddCudaKernel.Execute() - CUDA PTX version
// - VectorAddMetalKernel.Execute() - Metal shader version

// Usage of generated kernels
var invoker = new VectorAddInvoker();
invoker.InvokeKernel("VectorAdd", AcceleratorType.CUDA, inputA, inputB, output);
```

### **Advanced Matrix Operations**

```csharp
// Complex matrix multiplication with optimization hints
[Kernel("MatrixMultiply",
    Backends = BackendType.CUDA | BackendType.Metal,
    VectorSize = 16,
    SharedMemorySize = 2048,
    WorkGroupSize = new[] { 16, 16 },
    IsParallel = true)]
public static void MatrixMultiply(
    KernelContext ctx,
    ReadOnlySpan<float> matrixA,
    ReadOnlySpan<float> matrixB,
    Span<float> result,
    int widthA,
    int heightA,
    int widthB)
{
    var row = ctx.GlobalId.Y;
    var col = ctx.GlobalId.X;
    
    if (row < heightA && col < widthB)
    {
        float sum = 0.0f;
        
        // Use shared memory for tiling (CUDA/Metal specific)
        if (ctx.SupportsSharedMemory)
        {
            // Tiled matrix multiplication
            var tileSize = 16;
            var localRow = ctx.LocalId.Y;
            var localCol = ctx.LocalId.X;
            
            for (int tile = 0; tile < (widthA + tileSize - 1) / tileSize; tile++)
            {
                // Load tile into shared memory
                ctx.SharedMemory[localRow * tileSize + localCol] = 
                    (tile * tileSize + localCol < widthA && row < heightA) ?
                    matrixA[row * widthA + tile * tileSize + localCol] : 0.0f;
                
                ctx.SharedMemory[tileSize * tileSize + localRow * tileSize + localCol] = 
                    (tile * tileSize + localRow < widthA && col < widthB) ?
                    matrixB[(tile * tileSize + localRow) * widthB + col] : 0.0f;
                
                ctx.Barrier();
                
                // Compute partial dot product
                for (int k = 0; k < tileSize; k++)
                {
                    sum += ctx.SharedMemory[localRow * tileSize + k] * 
                           ctx.SharedMemory[tileSize * tileSize + k * tileSize + localCol];
                }
                
                ctx.Barrier();
            }
        }
        else
        {
            // Standard matrix multiplication for CPU
            for (int k = 0; k < widthA; k++)
            {
                sum += matrixA[row * widthA + k] * matrixB[k * widthB + col];
            }
        }
        
        result[row * widthB + col] = sum;
    }
}

// Generated CUDA kernel will include:
/*
__global__ void MatrixMultiplyCuda(
    const float* __restrict__ matrixA,
    const float* __restrict__ matrixB,
    float* __restrict__ result,
    int widthA,
    int heightA,
    int widthB)
{
    __shared__ float sharedA[256];
    __shared__ float sharedB[256];
    
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    
    // ... optimized CUDA implementation
}
*/
```

### **Image Processing Kernels**

```csharp
// Advanced image processing with multiple kernel variants
[Kernel("GaussianBlur",
    Backends = BackendType.CPU | BackendType.CUDA | BackendType.Metal,
    VectorSize = 4, // Process 4 pixels at once
    WorkGroupSize = new[] { 16, 16 })]
public static void GaussianBlur(
    KernelContext ctx,
    ReadOnlySpan<float> input,
    Span<float> output,
    ReadOnlySpan<float> kernel,
    int width,
    int height,
    int kernelSize)
{
    var x = ctx.GlobalId.X;
    var y = ctx.GlobalId.Y;
    
    if (x >= width || y >= height) return;
    
    var kernelRadius = kernelSize / 2;
    var sum = 0.0f;
    var weightSum = 0.0f;
    
    // Apply gaussian kernel
    for (int ky = -kernelRadius; ky <= kernelRadius; ky++)
    {
        for (int kx = -kernelRadius; kx <= kernelRadius; kx++)
        {
            var imgX = x + kx;
            var imgY = y + ky;
            
            // Clamp to image boundaries
            imgX = Math.Max(0, Math.Min(width - 1, imgX));
            imgY = Math.Max(0, Math.Min(height - 1, imgY));
            
            var kernelIdx = (ky + kernelRadius) * kernelSize + (kx + kernelRadius);
            var weight = kernel[kernelIdx];
            
            sum += input[imgY * width + imgX] * weight;
            weightSum += weight;
        }
    }
    
    output[y * width + x] = sum / weightSum;
}

// Multi-channel image processing
[Kernel("ColorSpaceConvert",
    Backends = BackendType.ALL,
    VectorSize = 4)] // RGBA processing
public static void RgbToHsv(
    KernelContext ctx,
    ReadOnlySpan<float> rgb,
    Span<float> hsv)
{
    var i = ctx.GlobalId.X;
    if (i * 3 >= rgb.Length) return;
    
    var r = rgb[i * 3 + 0];
    var g = rgb[i * 3 + 1];
    var b = rgb[i * 3 + 2];
    
    var max = Math.Max(r, Math.Max(g, b));
    var min = Math.Min(r, Math.Min(g, b));
    var delta = max - min;
    
    // Hue calculation
    float hue = 0.0f;
    if (delta > 0.0f)
    {
        if (max == r)
            hue = 60.0f * (((g - b) / delta) % 6.0f);
        else if (max == g)
            hue = 60.0f * ((b - r) / delta + 2.0f);
        else
            hue = 60.0f * ((r - g) / delta + 4.0f);
    }
    
    // Saturation
    var saturation = (max == 0.0f) ? 0.0f : (delta / max);
    
    // Value
    var value = max;
    
    hsv[i * 3 + 0] = hue;
    hsv[i * 3 + 1] = saturation;
    hsv[i * 3 + 2] = value;
}
```

### **Machine Learning Kernels**

```csharp
// Neural network operations
[Kernel("Convolution2D",
    Backends = BackendType.CUDA | BackendType.Metal,
    SharedMemorySize = 4096,
    WorkGroupSize = new[] { 16, 16 })]
public static void Convolution2D(
    KernelContext ctx,
    ReadOnlySpan<float> input,
    ReadOnlySpan<float> weights,
    ReadOnlySpan<float> bias,
    Span<float> output,
    int inputWidth,
    int inputHeight,
    int inputChannels,
    int filterSize,
    int outputChannels,
    int stride,
    int padding)
{
    var outputX = ctx.GlobalId.X;
    var outputY = ctx.GlobalId.Y;
    var outputChannel = ctx.GlobalId.Z;
    
    var outputWidth = (inputWidth + 2 * padding - filterSize) / stride + 1;
    var outputHeight = (inputHeight + 2 * padding - filterSize) / stride + 1;
    
    if (outputX >= outputWidth || outputY >= outputHeight || outputChannel >= outputChannels)
        return;
    
    var sum = bias[outputChannel];
    
    // Convolution operation
    for (int ic = 0; ic < inputChannels; ic++)
    {
        for (int fy = 0; fy < filterSize; fy++)
        {
            for (int fx = 0; fx < filterSize; fx++)
            {
                var inputX = outputX * stride - padding + fx;
                var inputY = outputY * stride - padding + fy;
                
                if (inputX >= 0 && inputX < inputWidth && inputY >= 0 && inputY < inputHeight)
                {
                    var inputIdx = (ic * inputHeight + inputY) * inputWidth + inputX;
                    var weightIdx = ((outputChannel * inputChannels + ic) * filterSize + fy) * filterSize + fx;
                    
                    sum += input[inputIdx] * weights[weightIdx];
                }
            }
        }
    }
    
    var outputIdx = (outputChannel * outputHeight + outputY) * outputWidth + outputX;
    output[outputIdx] = sum;
}

// Activation functions
[Kernel("ReLU", Backends = BackendType.ALL, VectorSize = 8, IsParallel = true)]
public static void ReLU(KernelContext ctx, ReadOnlySpan<float> input, Span<float> output)
{
    var i = ctx.GlobalId.X;
    if (i < input.Length)
    {
        output[i] = Math.Max(0.0f, input[i]);
    }
}

[Kernel("Softmax", Backends = BackendType.CUDA | BackendType.Metal)]
public static void Softmax(
    KernelContext ctx,
    ReadOnlySpan<float> input,
    Span<float> output,
    int batchSize,
    int classCount)
{
    var batchIdx = ctx.GlobalId.X;
    if (batchIdx >= batchSize) return;
    
    var offset = batchIdx * classCount;
    
    // Find maximum for numerical stability
    var maxVal = float.NegativeInfinity;
    for (int i = 0; i < classCount; i++)
    {
        maxVal = Math.Max(maxVal, input[offset + i]);
    }
    
    // Compute exponentials and sum
    var sum = 0.0f;
    for (int i = 0; i < classCount; i++)
    {
        var exp = Math.Exp(input[offset + i] - maxVal);
        output[offset + i] = exp;
        sum += exp;
    }
    
    // Normalize
    for (int i = 0; i < classCount; i++)
    {
        output[offset + i] /= sum;
    }
}
```

### **Custom Kernel Attributes and Specialization**

```csharp
// Custom attribute for kernel specialization
[AttributeUsage(AttributeTargets.Method)]
public class OptimizedKernelAttribute : KernelAttribute
{
    public string? CudaCode { get; set; }
    public string? MetalCode { get; set; }
    public string? OpenCLCode { get; set; }
    public bool UseTemplates { get; set; } = true;
    public string[]? RequiredExtensions { get; set; }
    
    public OptimizedKernelAttribute(string name) : base(name) { }
}

// Kernel with hand-optimized GPU code
[OptimizedKernel("FastFourierTransform",
    Backends = BackendType.CUDA | BackendType.Metal,
    CudaCode = @"
        __global__ void FastFourierTransformCuda(
            float2* data,
            int n,
            int inverse)
        {
            // Hand-optimized CUDA FFT implementation
            extern __shared__ float2 shared[];
            
            int tid = threadIdx.x;
            int bid = blockIdx.x;
            int stride = blockDim.x * gridDim.x;
            
            // Cooley-Tukey FFT algorithm
            // ... optimized CUDA implementation
        }
    ",
    MetalCode = @"
        kernel void FastFourierTransformMetal(
            device float2* data [[buffer(0)]],
            constant int& n [[buffer(1)]],
            constant int& inverse [[buffer(2)]],
            uint gid [[thread_position_in_grid]],
            uint tid [[thread_position_in_threadgroup]],
            threadgroup float2* shared [[threadgroup(0)]])
        {
            // Hand-optimized Metal FFT implementation
            // ... optimized Metal shader code
        }
    ")]
public static void FastFourierTransform(
    KernelContext ctx,
    Span<Complex> data,
    int n,
    bool inverse)
{
    // High-level C# implementation for CPU fallback
    // This will be used for CPU backend and as reference
    
    if (n <= 1) return;
    
    // Bit-reversal permutation
    for (int i = 1, j = 0; i < n; i++)
    {
        int bit = n >> 1;
        for (; (j & bit) != 0; bit >>= 1)
        {
            j ^= bit;
        }
        j ^= bit;
        
        if (i < j)
        {
            (data[i], data[j]) = (data[j], data[i]);
        }
    }
    
    // Cooley-Tukey FFT
    for (int length = 2; length <= n; length <<= 1)
    {
        var angle = 2.0 * Math.PI / length * (inverse ? -1 : 1);
        var wlen = new Complex(Math.Cos(angle), Math.Sin(angle));
        
        for (int i = 0; i < n; i += length)
        {
            var w = Complex.One;
            for (int j = 0; j < length / 2; j++)
            {
                var u = data[i + j];
                var v = data[i + j + length / 2] * w;
                
                data[i + j] = u + v;
                data[i + j + length / 2] = u - v;
                
                w *= wlen;
            }
        }
    }
    
    if (inverse)
    {
        for (int i = 0; i < n; i++)
        {
            data[i] /= n;
        }
    }
}
```

### **Build-Time Code Generation and Validation**

```csharp
// Source generator extension for custom validation
public class AdvancedKernelGenerator : KernelSourceGenerator
{
    protected override void Execute(
        ImmutableArray<KernelMethodInfo> kernelMethods,
        ImmutableArray<KernelClassInfo> kernelClasses,
        Compilation compilation,
        SourceProductionContext context)
    {
        // Validate kernels before generation
        foreach (var method in kernelMethods)
        {
            ValidateKernelMethod(method, context);
        }
        
        // Generate optimized implementations
        foreach (var method in kernelMethods)
        {
            GenerateOptimizedKernels(method, compilation, context);
        }
        
        // Generate kernel registry with performance metadata
        GeneratePerformanceAwareRegistry(kernelMethods, context);
    }
    
    private void ValidateKernelMethod(KernelMethodInfo method, SourceProductionContext context)
    {
        var diagnostics = new List<Diagnostic>();
        
        // Check for performance anti-patterns
        if (method.Parameters.Any(p => p.Type.Contains("string")))
        {
            diagnostics.Add(Diagnostic.Create(
                descriptor: new DiagnosticDescriptor(
                    "DOTCOMPUTE001",
                    "String parameters not recommended in kernels",
                    "Kernel '{0}' uses string parameters which may impact performance",
                    "Performance",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true),
                location: null,
                method.Name));
        }
        
        // Check for proper vectorization hints
        if (method.VectorSize > 1 && !method.IsParallel)
        {
            diagnostics.Add(Diagnostic.Create(
                descriptor: new DiagnosticDescriptor(
                    "DOTCOMPUTE002",
                    "Vectorized kernels should typically be parallel",
                    "Kernel '{0}' has VectorSize > 1 but IsParallel = false",
                    "Performance",
                    DiagnosticSeverity.Info,
                    isEnabledByDefault: true),
                location: null,
                method.Name));
        }
        
        // Report diagnostics
        foreach (var diagnostic in diagnostics)
        {
            context.ReportDiagnostic(diagnostic);
        }
    }
    
    private void GenerateOptimizedKernels(
        KernelMethodInfo method,
        Compilation compilation,
        SourceProductionContext context)
    {
        foreach (var backend in method.Backends)
        {
            var source = backend switch
            {
                "CPU" => GenerateOptimizedCpuKernel(method),
                "CUDA" => GenerateOptimizedCudaKernel(method),
                "Metal" => GenerateOptimizedMetalKernel(method),
                _ => null
            };
            
            if (source != null)
            {
                var fileName = $"{method.ContainingType.Replace(".", "_")}_{method.Name}_{backend}_Optimized.g.cs";
                context.AddSource(fileName, SourceText.From(source, Encoding.UTF8));
            }
        }
    }
    
    private string GenerateOptimizedCpuKernel(KernelMethodInfo method)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("using System;");
        source.AppendLine("using System.Runtime.Intrinsics;");
        source.AppendLine("using System.Runtime.Intrinsics.X86;");
        source.AppendLine("using System.Runtime.CompilerServices;");
        source.AppendLine();
        
        source.AppendLine($"namespace {method.Namespace}.Generated");
        source.AppendLine("{");
        source.AppendLine($"    public static class {method.Name}OptimizedCpuKernel");
        source.AppendLine("    {");
        
        // Generate platform-specific optimizations
        GenerateAvx512Implementation(source, method);
        GenerateAvx2Implementation(source, method);
        GenerateSseImplementation(source, method);
        GenerateNeonImplementation(source, method);
        GenerateScalarFallback(source, method);
        
        // Generate runtime dispatcher
        GenerateRuntimeDispatcher(source, method);
        
        source.AppendLine("    }");
        source.AppendLine("}");
        
        return source.ToString();
    }
    
    private void GenerateAvx512Implementation(StringBuilder source, KernelMethodInfo method)
    {
        source.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        source.AppendLine($"        public static void ExecuteAvx512({string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"))})");
        source.AppendLine("        {");
        source.AppendLine("            if (!Avx512F.IsSupported) throw new NotSupportedException();");
        source.AppendLine();
        
        // Generate AVX-512 specific implementation
        if (method.Name.Contains("VectorAdd"))
        {
            source.AppendLine("            var vectorCount = length / Vector512<float>.Count;");
            source.AppendLine("            var remainder = length % Vector512<float>.Count;");
            source.AppendLine();
            source.AppendLine("            unsafe");
            source.AppendLine("            {");
            source.AppendLine("                fixed (float* pA = a, pB = b, pResult = result)");
            source.AppendLine("                {");
            source.AppendLine("                    for (int i = 0; i < vectorCount; i++)");
            source.AppendLine("                    {");
            source.AppendLine("                        var vA = Avx512F.LoadVector512(pA + i * Vector512<float>.Count);");
            source.AppendLine("                        var vB = Avx512F.LoadVector512(pB + i * Vector512<float>.Count);");
            source.AppendLine("                        var vResult = Avx512F.Add(vA, vB);");
            source.AppendLine("                        Avx512F.Store(pResult + i * Vector512<float>.Count, vResult);");
            source.AppendLine("                    }");
            source.AppendLine();
            source.AppendLine("                    // Handle remainder");
            source.AppendLine("                    for (int i = vectorCount * Vector512<float>.Count; i < length; i++)");
            source.AppendLine("                    {");
            source.AppendLine("                        pResult[i] = pA[i] + pB[i];");
            source.AppendLine("                    }");
            source.AppendLine("                }");
            source.AppendLine("            }");
        }
        
        source.AppendLine("        }");
        source.AppendLine();
    }
    
    private void GenerateRuntimeDispatcher(StringBuilder source, KernelMethodInfo method)
    {
        source.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        source.AppendLine($"        public static void Execute({string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"))})");
        source.AppendLine("        {");
        source.AppendLine("            if (Avx512F.IsSupported)");
        source.AppendLine($"                ExecuteAvx512({string.Join(", ", method.Parameters.Select(p => p.Name))});");
        source.AppendLine("            else if (Avx2.IsSupported)");
        source.AppendLine($"                ExecuteAvx2({string.Join(", ", method.Parameters.Select(p => p.Name))});");
        source.AppendLine("            else if (Sse2.IsSupported)");
        source.AppendLine($"                ExecuteSse({string.Join(", ", method.Parameters.Select(p => p.Name))});");
        source.AppendLine("            else if (AdvSimd.IsSupported)");
        source.AppendLine($"                ExecuteNeon({string.Join(", ", method.Parameters.Select(p => p.Name))});");
        source.AppendLine("            else");
        source.AppendLine($"                ExecuteScalar({string.Join(", ", method.Parameters.Select(p => p.Name))});");
        source.AppendLine("        }");
    }
}
```

### **Performance-Aware Kernel Registry**

```csharp
// Generated registry with performance metadata
public static class PerformanceAwareKernelRegistry
{
    private static readonly Dictionary<string, KernelPerformanceMetadata> _performanceMetadata = new()
    {
        ["VectorAdd"] = new KernelPerformanceMetadata
        {
            OptimalVectorSize = 16,
            PreferredBackends = new[] { AcceleratorType.CUDA, AcceleratorType.Metal, AcceleratorType.CPU },
            MemoryBandwidthRequirement = MemoryBandwidthClass.High,
            ComputeIntensity = ComputeIntensityClass.Low,
            EstimatedSpeedup = new Dictionary<AcceleratorType, double>
            {
                [AcceleratorType.CPU] = 1.0,
                [AcceleratorType.CUDA] = 8.5,
                [AcceleratorType.Metal] = 6.2
            }
        },
        ["MatrixMultiply"] = new KernelPerformanceMetadata
        {
            OptimalVectorSize = 8,
            PreferredBackends = new[] { AcceleratorType.CUDA, AcceleratorType.Metal },
            MemoryBandwidthRequirement = MemoryBandwidthClass.Medium,
            ComputeIntensity = ComputeIntensityClass.High,
            EstimatedSpeedup = new Dictionary<AcceleratorType, double>
            {
                [AcceleratorType.CPU] = 1.0,
                [AcceleratorType.CUDA] = 15.2,
                [AcceleratorType.Metal] = 11.8
            }
        }
    };
    
    public static AcceleratorType GetOptimalBackend(string kernelName, IEnumerable<AcceleratorType> availableBackends)
    {
        if (!_performanceMetadata.TryGetValue(kernelName, out var metadata))
        {
            return availableBackends.First(); // Fallback to first available
        }
        
        return metadata.PreferredBackends
            .Where(availableBackends.Contains)
            .FirstOrDefault();
    }
    
    public static double GetEstimatedSpeedup(string kernelName, AcceleratorType backend)
    {
        return _performanceMetadata.TryGetValue(kernelName, out var metadata) &&
               metadata.EstimatedSpeedup.TryGetValue(backend, out var speedup)
               ? speedup : 1.0;
    }
}
```

This comprehensive source generator guide demonstrates the full power of automatic code generation, multi-backend optimization, and performance-aware kernel development in DotCompute Phase 3.