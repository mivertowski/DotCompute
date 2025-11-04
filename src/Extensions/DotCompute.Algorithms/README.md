# DotCompute.Algorithms

Comprehensive library of GPU-accelerated algorithms for scientific computing, signal processing, and numerical analysis.

## Status: 🚧 Active Development

The Algorithms library provides implementations across multiple domains:
- **Linear Algebra**: Production-ready matrix operations and decompositions
- **Signal Processing**: FFT, convolutions, and filtering operations
- **Numerical Methods**: Integration, polynomial solving, optimization
- **SIMD Optimizations**: Hardware-accelerated vectorized operations
- **Native AOT**: Full compatibility with Native AOT compilation

## Key Components

### Linear Algebra

#### Matrix Operations
- **Basic Operations**: Addition, subtraction, multiplication, transpose
- **Matrix Multiplication**: Tiled algorithms with shared memory optimization
- **Element-wise Operations**: Hadamard product, element-wise functions
- **Matrix Statistics**: Norms, traces, determinants, condition numbers

#### Matrix Decompositions
- **LU Decomposition**: Lower-upper factorization with partial pivoting
- **Cholesky Decomposition**: Symmetric positive-definite matrices
- **QR Decomposition**: Orthogonal-triangular factorization
- **SVD (Singular Value Decomposition)**: Full and economy SVD
- **Eigenvalue Decomposition**: Symmetric and general eigenvalue problems

#### Linear Solvers
- **Direct Solvers**: LU, Cholesky, QR-based solvers
- **Iterative Solvers**: Conjugate gradient, GMRES, BiCGSTAB
- **Sparse Solvers**: Sparse matrix-specific algorithms
- **Least Squares**: Overdetermined and underdetermined systems

#### Advanced Linear Algebra
- **Householder Transformations**: For QR and eigenvalue algorithms
- **BLAS Kernels**: Level 1, 2, and 3 BLAS operations
- **LAPACK Kernels**: Advanced factorization and solver routines
- **Sparse Matrix Kernels**: CSR/CSC format operations

### Signal Processing

#### Fast Fourier Transform (FFT)
- **1D FFT**: Cooley-Tukey algorithm with radix-2 and mixed-radix
- **2D FFT**: Row-column FFT for image processing
- **Advanced FFT**: Bluestein's algorithm for arbitrary lengths
- **Inverse FFT**: Transform back to time domain
- **Real FFT**: Optimized for real-valued input

#### Convolution Operations
- **1D Convolution**: Time-domain and frequency-domain methods
- **2D Convolution**: Image filtering and processing
- **Circular Convolution**: Periodic signal processing
- **Fast Convolution**: FFT-based convolution for large kernels

#### Signal Processor
- **Filtering**: IIR and FIR filter implementation
- **Windowing**: Hanning, Hamming, Blackman windows
- **Spectral Analysis**: Power spectral density estimation
- **Signal Generation**: Sine waves, chirps, noise

### Numerical Methods

#### Advanced Integration
- **Adaptive Quadrature**: Simpson's rule with adaptive refinement
- **Gaussian Quadrature**: High-accuracy integration
- **Multi-dimensional Integration**: Monte Carlo and deterministic methods
- **ODE Solvers**: Runge-Kutta methods

#### Polynomial Operations
- **Root Finding**: Newton-Raphson, Durand-Kerner methods
- **Polynomial Evaluation**: Horner's method
- **Polynomial Interpolation**: Lagrange and Newton forms
- **Advanced Polynomial Solver**: High-degree polynomial handling

### SIMD Optimizations

#### Vectorized Operations
- **Math Operations**: Add, subtract, multiply, divide, sqrt, abs
- **Reduction Operations**: Sum, min, max, dot product
- **Comparison Operations**: Equal, less than, greater than
- **Bitwise Operations**: AND, OR, XOR, NOT
- **Conversion Operations**: Type conversions and casts
- **Utility Operations**: Shuffle, blend, select

#### SIMD Capabilities
- **AVX-512 Support**: 512-bit vector operations (when available)
- **AVX2 Support**: 256-bit vector operations
- **SSE Support**: 128-bit vector operations
- **NEON Support**: ARM SIMD instructions
- **Fallback**: Scalar implementation for unsupported hardware

#### Performance Tuning
- **Auto-Tuner**: Automatically selects optimal algorithm parameters
- **BLAS Optimizations**: Tuned matrix multiplication and operations
- **Memory Optimizations**: Cache-aware algorithms, memory pooling
- **Parallel Optimizations**: Multi-threaded execution strategies
- **Algorithm Selector**: Workload-based algorithm selection

### Algorithm Management

#### Plugin System
- **Algorithm Discovery**: Automatic detection of algorithm plugins
- **Plugin Lifecycle**: Load, initialize, execute, unload management
- **Metadata System**: Algorithm capabilities and requirements
- **Dependency Resolution**: Automatic dependency handling
- **Version Management**: Plugin versioning and compatibility

#### Configuration
- **Algorithm Options**: Per-algorithm configuration
- **Performance Profiles**: Optimize for speed, memory, or accuracy
- **Backend Selection**: Choose CPU, CUDA, Metal, or OpenCL
- **Validation**: Input validation and error handling

### Security Features

#### Vulnerability Scanning
- **Dependency Analysis**: Scan NuGet packages for known vulnerabilities
- **CVE Database Integration**: National Vulnerability Database (NVD) queries
- **GitHub Advisory Integration**: GitHub Security Advisories
- **OSS Index Integration**: Sonatype OSS Index scanning
- **Vulnerability Reports**: Detailed vulnerability information and severity ratings

#### Security Options
- **Scan Configuration**: Configure scan depth and sources
- **Severity Filtering**: Filter by severity level (Critical, High, Medium, Low)
- **Report Generation**: Generate security reports in multiple formats
- **Auto-Update**: Automatic vulnerability database updates

## Installation

```bash
dotnet add package DotCompute.Algorithms --version 0.2.0-alpha
```

## Usage

### Linear Algebra - Matrix Multiplication

```csharp
using DotCompute.Algorithms.LinearAlgebra;
using DotCompute.Abstractions;

// Create GPU linear algebra provider
var provider = new GPULinearAlgebraProvider(accelerator, logger);

// Create matrices
var matrixA = new Matrix(rows: 1024, cols: 512);
var matrixB = new Matrix(rows: 512, cols: 2048);

// Fill with data
matrixA.Fill(/* data */);
matrixB.Fill(/* data */);

// Perform matrix multiplication on GPU
var result = await provider.MultiplyAsync(matrixA, matrixB);

// Result is a 1024x2048 matrix
Console.WriteLine($"Result: {result.Rows}x{result.Columns}");
```

### Linear Algebra - LU Decomposition

```csharp
using DotCompute.Algorithms.LinearAlgebra.Operations;

var decomposition = new LuDecomposition(accelerator);

// Decompose matrix A into L and U
var (L, U, P) = await decomposition.DecomposeAsync(matrixA);

// Solve Ax = b using LU decomposition
var solution = await decomposition.SolveAsync(L, U, P, vectorB);
```

### Signal Processing - FFT

```csharp
using DotCompute.Algorithms.SignalProcessing;

var fft = new FFT(accelerator);

// Perform 1D FFT
var signal = new Complex[1024];
// ... fill signal with data ...

var spectrum = await fft.ForwardAsync(signal);

// Perform inverse FFT
var reconstructed = await fft.InverseAsync(spectrum);
```

### Signal Processing - Convolution

```csharp
using DotCompute.Algorithms.SignalProcessing;

var convOps = new ConvolutionOperations(accelerator);

// 1D convolution
float[] signal = /* input signal */;
float[] kernel = /* convolution kernel */;

var output = await convOps.Convolve1DAsync(signal, kernel);

// 2D convolution (for images)
float[,] image = /* input image */;
float[,] filter = /* filter kernel */;

var filtered = await convOps.Convolve2DAsync(image, filter);
```

### SIMD Operations

```csharp
using DotCompute.Algorithms.Optimized.Simd;
using System.Runtime.Intrinsics;

// Check SIMD capabilities
var capabilities = SimdCapabilities.Detect();
Console.WriteLine($"AVX2: {capabilities.HasAvx2}");
Console.WriteLine($"AVX-512: {capabilities.HasAvx512}");

// Vectorized addition
float[] a = new float[1024];
float[] b = new float[1024];
float[] result = new float[1024];

SimdMathOperations.Add(a, b, result);

// Vectorized reduction (sum)
float sum = SimdReductionOperations.Sum(a);

// Dot product
float dotProduct = SimdReductionOperations.DotProduct(a, b);
```

### Numerical Methods - Integration

```csharp
using DotCompute.Algorithms.NumericalMethods;

var integration = new AdvancedIntegration();

// Integrate f(x) = x^2 from 0 to 10
double result = integration.AdaptiveQuadrature(
    x => x * x,
    lowerBound: 0,
    upperBound: 10,
    tolerance: 1e-6
);

Console.WriteLine($"Integral: {result:F6}");
```

### Auto-Tuner for Performance

```csharp
using DotCompute.Algorithms.Optimized;

var autoTuner = new AutoTuner(accelerator);

// Auto-tune matrix multiplication parameters
var tuningResult = await autoTuner.TuneMatrixMultiplicationAsync(
    matrixSize: 2048,
    dataType: typeof(float)
);

Console.WriteLine($"Optimal tile size: {tuningResult.OptimalTileSize}");
Console.WriteLine($"Optimal work group: {tuningResult.OptimalWorkGroupSize}");
Console.WriteLine($"Expected performance: {tuningResult.EstimatedGFlops:F2} GFLOPS");
```

### Algorithm Plugin Discovery

```csharp
using DotCompute.Algorithms.Management;

var discovery = new AlgorithmPluginDiscovery(logger);

// Discover all available algorithms
var algorithms = await discovery.DiscoverAsync(searchPaths);

foreach (var algorithm in algorithms)
{
    Console.WriteLine($"Algorithm: {algorithm.Name}");
    Console.WriteLine($"Version: {algorithm.Version}");
    Console.WriteLine($"Backends: {string.Join(", ", algorithm.SupportedBackends)}");
}
```

### Security Vulnerability Scanning

```csharp
using DotCompute.Algorithms.Security;

var scanner = new VulnerabilityScanner(logger);

// Scan project dependencies
var scanOptions = new VulnerabilityScanOptions
{
    ScanNuGetPackages = true,
    ScanGitHubAdvisories = true,
    MinimumSeverity = Severity.Medium,
    IncludeDevDependencies = false
};

var vulnerabilities = await scanner.ScanAsync(projectPath, scanOptions);

// Generate report
var report = scanner.GenerateReport(vulnerabilities);
Console.WriteLine(report.Summary);

foreach (var vuln in vulnerabilities.Where(v => v.Severity >= Severity.High))
{
    Console.WriteLine($"[{vuln.Severity}] {vuln.PackageName} - {vuln.CveId}");
    Console.WriteLine($"  Description: {vuln.Description}");
    Console.WriteLine($"  Fix: Upgrade to {vuln.RecommendedVersion}");
}
```

## Architecture

### Module Organization

```
DotCompute.Algorithms/
├── LinearAlgebra/              # Matrix and vector operations
│   ├── Components/             # GPU implementation components
│   ├── Operations/             # High-level operations (solvers, decompositions)
│   └── LinearAlgebraKernels/   # Kernel implementations
├── SignalProcessing/           # FFT, convolution, filtering
├── NumericalMethods/           # Integration, ODE solvers, polynomial ops
├── Optimized/                  # Performance-optimized implementations
│   └── Simd/                   # SIMD vectorized operations
├── Kernels/                    # GPU kernel library
│   └── LinearAlgebra/          # Linear algebra GPU kernels
├── Management/                 # Algorithm plugin management
│   ├── Discovery/              # Algorithm discovery
│   ├── Lifecycle/              # Plugin lifecycle
│   └── Execution/              # Algorithm execution
├── Security/                   # Vulnerability scanning
│   ├── DataTransfer/           # CVE database integration
│   └── Reports/                # Security report generation
└── Selection/                  # Algorithm selection strategies
```

### Design Patterns

#### Provider Pattern
Linear algebra operations use provider pattern for backend abstraction:
- **GPULinearAlgebraProvider**: GPU-accelerated operations
- **CPULinearAlgebraProvider**: CPU-based fallback
- **HybridLinearAlgebraProvider**: Automatic backend selection

#### Strategy Pattern
Algorithm selection uses strategy pattern:
- **AlgorithmSelector**: Selects optimal algorithm based on workload
- **PerformanceStrategy**: Optimize for speed
- **MemoryStrategy**: Optimize for memory usage
- **AccuracyStrategy**: Optimize for numerical accuracy

#### Plugin Architecture
Algorithms can be loaded as plugins:
- Discoverable through metadata
- Versioned with semantic versioning
- Dependency-managed
- Hot-reload capable

## Performance Benchmarks

### Matrix Multiplication (2048x2048, single precision)

| Implementation | CPU Time | GPU Time | Speedup |
|---------------|----------|----------|---------|
| Naive | 8,240ms | 142ms | **58x** |
| Tiled | N/A | 47ms | **175x** |
| BLAS-optimized | 89ms | 38ms | **2.3x over CPU BLAS** |

### FFT (1M complex samples)

| Implementation | Time | Throughput |
|---------------|------|------------|
| CPU FFT | 156ms | 6.4 MSamples/s |
| GPU FFT | 8.4ms | **119 MSamples/s** |
| Speedup | **18.6x** | |

### SIMD Operations (10M float additions)

| Implementation | Time | Speedup |
|---------------|------|---------|
| Scalar | 45ms | 1x |
| SSE (128-bit) | 12ms | **3.75x** |
| AVX2 (256-bit) | 6ms | **7.5x** |
| AVX-512 (512-bit) | 3ms | **15x** |

## System Requirements

### Minimum
- .NET 9.0 or later
- 4GB RAM
- CPU with SSE2 support

### Recommended
- .NET 9.0 or later
- 16GB+ RAM
- CPU with AVX2 or AVX-512 support
- GPU with compute capability 5.0+ (CUDA) or Metal support

### For GPU Acceleration
- **CUDA**: NVIDIA GPU with Compute Capability 5.0+
- **Metal**: Apple Silicon or AMD GPU on macOS
- **OpenCL**: OpenCL 1.2+ compatible device

## Configuration

### Algorithm Options

```csharp
var options = new AlgorithmOptions
{
    DefaultBackend = AcceleratorType.Auto,
    EnableAutoTuning = true,
    CacheKernels = true,
    ValidationLevel = ValidationLevel.Release,
    PerformanceProfile = PerformanceProfile.Balanced
};
```

### Linear Algebra Configuration

```csharp
var laConfig = new LinearAlgebraConfiguration
{
    TileSize = 16, // For tiled matrix multiplication
    UseSharedMemory = true,
    EnableFusedOperations = true,
    NumericalStability = NumericalStability.Balanced
};
```

### FFT Configuration

```csharp
var fftConfig = new FFTConfiguration
{
    Algorithm = FFTAlgorithm.CooleyTukey,
    UseRadix4 = true, // When input size is power of 4
    EnableCaching = true
};
```

## Troubleshooting

### Matrix Operations Failing

1. **Check Matrix Dimensions**: Ensure dimensions are compatible (e.g., A[m×k] × B[k×n])
2. **Memory Limits**: Large matrices may exceed GPU memory
3. **Numerical Stability**: Ill-conditioned matrices may require higher precision

### FFT Issues

1. **Input Size**: FFT works best with power-of-2 sizes
2. **Complex Numbers**: Ensure proper complex number representation
3. **Memory Alignment**: FFT requires aligned memory for optimal performance

### SIMD Not Working

1. **CPU Support**: Check `SimdCapabilities.Detect()` for hardware support
2. **Data Alignment**: Ensure data is properly aligned (16-byte for SSE, 32-byte for AVX)
3. **Data Type**: SIMD operations have type-specific implementations

### Performance Issues

1. **Enable Auto-Tuning**: Use `AutoTuner` to find optimal parameters
2. **Check Backend**: Verify correct backend is selected (GPU vs CPU)
3. **Profiling**: Use built-in performance benchmarks to identify bottlenecks

## Advanced Topics

### Custom Algorithms

Implement custom algorithms as plugins:

```csharp
public class CustomAlgorithm : IAlgorithmPlugin
{
    public string Name => "CustomAlgorithm";
    public Version Version => new Version(1, 0, 0);
    public AcceleratorType[] SupportedBackends => new[] { AcceleratorType.CUDA };

    public async Task<object> ExecuteAsync(object input, CancellationToken ct)
    {
        // Custom algorithm implementation
        return result;
    }
}
```

### Sparse Matrix Operations

```csharp
using DotCompute.Algorithms.Kernels.LinearAlgebra;

// Create sparse matrix in CSR format
var sparseMatrix = SparseMatrix.CreateCSR(rows, cols, nonZeroValues, columnIndices, rowPointers);

// Sparse matrix-vector multiplication
var result = await sparseMatrix.MultiplyVectorAsync(vector);
```

### Mixed Precision Arithmetic

```csharp
// Use float for computation, double for accumulation
var result = await provider.MultiplyAsync<float, double>(matrixA, matrixB);
```

## Dependencies

- **DotCompute.Core**: Core runtime functionality
- **DotCompute.Abstractions**: Interface definitions
- **DotCompute.Memory**: Memory management
- **DotCompute.Plugins**: Plugin system
- **Microsoft.Extensions.Hosting**: Service hosting
- **NuGet Libraries**: For vulnerability scanning

## Future Enhancements

### Planned Features
1. **Machine Learning Primitives**: Convolution layers, pooling, activation functions
2. **Graph Algorithms**: Shortest path, graph traversal, clustering
3. **Statistical Functions**: Distributions, hypothesis testing, regression
4. **Computer Vision**: Image processing kernels, feature detection
5. **Optimization Algorithms**: Linear programming, gradient descent variants

### Performance Improvements
1. **Tensor Cores**: Leverage NVIDIA Tensor Cores for matrix operations
2. **Multi-GPU Support**: Distribute work across multiple GPUs
3. **Mixed Precision**: Automatic mixed-precision training support
4. **Kernel Fusion**: Automatically fuse multiple operations

## Documentation & Resources

Comprehensive documentation is available for DotCompute:

### Architecture Documentation
- **[System Overview](../../../docs/articles/architecture/overview.md)** - Algorithm integration architecture
- **[Backend Integration](../../../docs/articles/architecture/backend-integration.md)** - Multi-backend algorithm support

### Developer Guides
- **[Getting Started](../../../docs/articles/getting-started.md)** - Installation and setup
- **[Performance Tuning](../../../docs/articles/guides/performance-tuning.md)** - Algorithm optimization techniques
- **[Kernel Development](../../../docs/articles/guides/kernel-development.md)** - Writing custom algorithms

### Examples
- **[Matrix Operations](../../../docs/articles/examples/matrix-operations.md)** - Linear algebra examples and benchmarks
- **[Image Processing](../../../docs/articles/examples/image-processing.md)** - Signal processing and FFT examples
- **[Basic Vector Operations](../../../docs/articles/examples/basic-vector-operations.md)** - SIMD operations

### API Documentation
- **[API Reference](../../../api/index.md)** - Complete API documentation
- **[Performance Benchmarking](../../../docs/articles/reference/performance-benchmarking.md)** - Algorithm profiling

## Support

- **Documentation**: [Comprehensive Guides](../../../docs/index.md)
- **Issues**: [GitHub Issues](https://github.com/mivertowski/DotCompute/issues)
- **Discussions**: [GitHub Discussions](https://github.com/mivertowski/DotCompute/discussions)

## Contributing

Contributions are welcome in:
- New algorithm implementations
- Performance optimizations
- Additional numerical methods
- Documentation and examples
- Bug fixes and testing

See [CONTRIBUTING.md](../../../CONTRIBUTING.md) for guidelines.

## License

MIT License - Copyright (c) 2025 Michael Ivertowski
