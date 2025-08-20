# Development Guide

Welcome to DotCompute development! This guide covers everything you need to know to contribute to the project, from setting up your development environment to submitting pull requests.

## 🚀 Getting Started

### Development Environment Setup

#### Prerequisites
- **.NET 9.0 SDK** or later
- **Visual Studio 2022 17.8+** or **VS Code** with C# extension
- **Git** for source control
- **CUDA Toolkit 12.0+** (optional, for GPU backend development)

#### Clone and Build

```bash
# Clone the repository
git clone https://github.com/mivertowski/DotCompute.git
cd DotCompute

# Restore dependencies
dotnet restore

# Build the solution
dotnet build --configuration Release --verbosity minimal

# Run tests
dotnet test --configuration Release
```

#### Verify Development Environment

```bash
# Run basic tests
dotnet test tests/Unit/**/*.csproj

# Run integration tests
dotnet test tests/Integration/**/*.csproj

# Run hardware tests (requires GPU)
dotnet test tests/Hardware/**/*.csproj

# Run benchmarks
dotnet run --project benchmarks/DotCompute.Benchmarks -c Release
```

## 🏗️ Project Structure

Understanding the codebase organization:

```
DotCompute/
├── src/
│   ├── Core/
│   │   ├── DotCompute.Abstractions/    # Core interfaces
│   │   ├── DotCompute.Core/            # Core implementation
│   │   └── DotCompute.Memory/          # Memory management
│   ├── Backends/
│   │   ├── DotCompute.Backends.CPU/    # CPU backend
│   │   ├── DotCompute.Backends.CUDA/   # NVIDIA GPU backend
│   │   └── DotCompute.Backends.Metal/  # Apple GPU backend (stub)
│   ├── Runtime/
│   │   ├── DotCompute.Generators/      # Source generators
│   │   ├── DotCompute.Plugins/         # Plugin system
│   │   └── DotCompute.Runtime/         # Runtime services
│   └── Extensions/
│       ├── DotCompute.Algorithms/      # Algorithm library
│       └── DotCompute.Linq/           # LINQ provider
├── tests/
│   ├── Unit/                          # Hardware-independent tests
│   ├── Integration/                   # End-to-end tests
│   ├── Hardware/                      # Hardware-dependent tests
│   └── Shared/                        # Test utilities
├── benchmarks/                        # Performance benchmarks
├── samples/                           # Example applications
└── docs/                             # Documentation
```

### Key Components

#### Core Abstractions (`DotCompute.Abstractions`)
- `IAccelerator` - Hardware abstraction
- `IMemoryBuffer<T>` - Memory management
- `IKernelCompiler` - Kernel compilation
- `IComputeService` - Main API surface

#### Core Implementation (`DotCompute.Core`)
- `ComputeService` - Service implementation
- `AcceleratorManager` - Hardware management
- `KernelManager` - Kernel compilation and caching
- `MemoryManager` - Memory allocation and pooling

#### Backend System (`DotCompute.Backends.*`)
- `BackendPluginBase` - Base class for backends
- Backend-specific accelerator implementations
- Kernel compilers for each target platform

## 💻 Development Workflow

### 1. **Issue-First Development**

Before starting work:
1. Check existing issues for similar requests
2. Create a new issue if none exists
3. Discuss approach with maintainers
4. Get issue assignment before starting work

### 2. **Branch Strategy**

```bash
# Create feature branch from main
git checkout main
git pull origin main
git checkout -b feature/your-feature-name

# Make your changes
# ... development work ...

# Push and create PR
git push origin feature/your-feature-name
```

**Branch Naming:**
- `feature/description` - New features
- `bugfix/description` - Bug fixes
- `docs/description` - Documentation updates
- `refactor/description` - Code refactoring

### 3. **Commit Guidelines**

Follow conventional commits:
```bash
git commit -m "feat: add matrix multiplication kernel for CPU backend"
git commit -m "fix: resolve memory leak in CUDA buffer allocation"
git commit -m "docs: update getting started guide with examples"
git commit -m "test: add integration tests for multi-GPU scenarios"
```

**Commit Types:**
- `feat` - New features
- `fix` - Bug fixes
- `docs` - Documentation
- `test` - Tests
- `refactor` - Code refactoring
- `perf` - Performance improvements
- `ci` - CI/CD changes

## 🧪 Testing Strategy

### Test Organization

DotCompute uses a professional test structure:

```
tests/
├── Unit/                    # Fast, isolated tests
│   ├── Abstractions.Tests/ # Interface tests
│   ├── Core.Tests/         # Core logic tests
│   ├── Memory.Tests/       # Memory system tests
│   └── ...
├── Integration/             # End-to-end scenarios
│   └── Integration.Tests/  # Complete workflows
├── Hardware/                # Hardware-dependent tests
│   ├── Cuda.Tests/         # NVIDIA GPU tests
│   ├── OpenCL.Tests/       # OpenCL device tests
│   └── Mock.Tests/         # Mock hardware for CI
└── Shared/                  # Test infrastructure
    ├── Tests.Common/       # Utilities and helpers
    └── Tests.Mocks/        # Mock implementations
```

### Writing Tests

#### Unit Tests

```csharp
[TestClass]
public class ComputeServiceTests
{
    [TestMethod]
    public async Task ExecuteAsync_WithValidKernel_ShouldSucceed()
    {
        // Arrange
        var mockAccelerator = new MockAccelerator();
        var computeService = new ComputeService(mockAccelerator);
        
        // Act
        await computeService.ExecuteAsync("TestKernel", new { input = data });
        
        // Assert
        mockAccelerator.ExecutedKernels.Should().Contain("TestKernel");
    }
    
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public async Task ExecuteAsync_WithInvalidKernel_ShouldThrow()
    {
        // Test error conditions
    }
}
```

#### Integration Tests

```csharp
[TestClass]
[TestCategory("Integration")]
public class EndToEndWorkflowTests
{
    [TestMethod]
    public async Task CompleteComputePipeline_ShouldProcessDataCorrectly()
    {
        // Test complete workflows
        var services = new ServiceCollection()
            .AddDotCompute()
            .AddCpuBackend()
            .BuildServiceProvider();
            
        var compute = services.GetRequiredService<IComputeService>();
        
        // Execute real workflow
        await compute.ExecuteAsync("RealKernel", realParameters);
        
        // Verify results
        results.Should().BeEquivalentTo(expectedResults);
    }
}
```

#### Hardware Tests

```csharp
[TestClass]
[TestCategory("Hardware")]
[TestCategory("CUDA")]
public class CudaHardwareTests
{
    [TestInitialize]
    public void Setup()
    {
        // Skip if CUDA not available
        if (!CudaRuntime.IsAvailable)
            Assert.Inconclusive("CUDA not available");
    }
    
    [TestMethod]
    public async Task CudaExecution_OnRealHardware_ShouldSucceed()
    {
        // Test with real GPU hardware
    }
}
```

### Running Tests

```bash
# All tests
dotnet test

# Specific categories
dotnet test --filter "TestCategory!=Hardware"     # Skip hardware tests
dotnet test --filter "TestCategory=Integration"   # Integration only
dotnet test --filter "TestCategory=CUDA"          # CUDA hardware only

# With coverage
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings

# Parallel execution
dotnet test --parallel
```

### Test Coverage Requirements

- **Minimum Coverage**: 75% overall
- **Core Components**: 90% coverage required
- **New Features**: Must include comprehensive tests
- **Bug Fixes**: Must include regression tests

Check coverage:
```bash
# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"

# View detailed report
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report"
```

## 🔧 Code Standards

### Coding Style

DotCompute follows modern C# conventions:

#### Formatting
```csharp
// Use file-scoped namespaces
namespace DotCompute.Core;

// Use primary constructors when appropriate
public class ComputeService(IAcceleratorManager acceleratorManager) : IComputeService
{
    // Use expression-bodied members
    public bool IsReady => acceleratorManager.HasAccelerators;
    
    // Use pattern matching
    public string GetAcceleratorType(IAccelerator accelerator) => accelerator.Type switch
    {
        AcceleratorType.CPU => "CPU",
        AcceleratorType.CUDA => "CUDA",
        AcceleratorType.Metal => "Metal",
        _ => "Unknown"
    };
    
    // Prefer async/await over Task.Result
    public async ValueTask<TResult> ProcessAsync<TResult>(Func<ValueTask<TResult>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            // Proper exception handling
            throw new ComputeException("Processing failed", ex);
        }
    }
}
```

#### Performance Guidelines
```csharp
// Use ValueTask for hot paths
public ValueTask<T> PerformanceHotPath<T>() => new(result);

// Prefer spans for memory efficiency
public void ProcessData(ReadOnlySpan<float> input, Span<float> output)
{
    // Process in-place when possible
}

// Use object pooling for allocations
private readonly ObjectPool<StringBuilder> _stringBuilderPool;

// Avoid boxing
public void LogMetrics<T>(T value) where T : struct
{
    // Generic constraints prevent boxing
}
```

#### Error Handling
```csharp
// Use specific exception types
public void ValidateInput(object input)
{
    if (input is null)
        throw new ArgumentNullException(nameof(input));
        
    if (input is not SupportedType)
        throw new ArgumentException($"Unsupported type: {input.GetType()}", nameof(input));
}

// Provide meaningful error messages
catch (CudaException ex) when (ex.ErrorCode == CudaError.OutOfMemory)
{
    throw new MemoryException(
        $"GPU out of memory. Requested: {requestedBytes:N0} bytes, Available: {availableBytes:N0} bytes",
        ex);
}
```

### Documentation Requirements

#### XML Documentation
```csharp
/// <summary>
/// Executes a compute kernel asynchronously with the specified parameters.
/// </summary>
/// <param name="kernelName">The name of the kernel to execute.</param>
/// <param name="parameters">The parameters to pass to the kernel.</param>
/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
/// <returns>A task representing the asynchronous operation.</returns>
/// <exception cref="ArgumentException">Thrown when the kernel name is invalid.</exception>
/// <exception cref="CompilationException">Thrown when kernel compilation fails.</exception>
public async ValueTask ExecuteAsync<T>(
    string kernelName, 
    T parameters, 
    CancellationToken cancellationToken = default)
{
    // Implementation...
}
```

#### Code Comments
```csharp
public class MemoryPool
{
    // Performance-critical: Use lock-free data structures
    private readonly ConcurrentQueue<IMemoryBuffer> _availableBuffers = new();
    
    public IMemoryBuffer<T> Rent<T>(int size) where T : unmanaged
    {
        // Fast path: Try to reuse existing buffer
        if (_availableBuffers.TryDequeue(out var buffer) && buffer.Size >= size)
        {
            return (IMemoryBuffer<T>)buffer;
        }
        
        // Slow path: Allocate new buffer
        return AllocateNewBuffer<T>(size);
    }
}
```

## 🔌 Backend Development

### Creating a New Backend

1. **Create Backend Project**
```bash
mkdir src/Backends/DotCompute.Backends.MyBackend
cd src/Backends/DotCompute.Backends.MyBackend
dotnet new classlib -f net9.0
```

2. **Implement Backend Plugin**
```csharp
public class MyBackendPlugin : BackendPluginBase
{
    public override string Name => "MyBackend";
    public override AcceleratorType SupportedType => AcceleratorType.Custom;
    public override bool IsAvailable => CheckHardwareAvailability();
    
    public override ValueTask<IAccelerator> CreateAcceleratorAsync()
    {
        return new ValueTask<IAccelerator>(new MyBackendAccelerator());
    }
    
    protected override ValueTask<IKernelCompiler> CreateCompilerAsync()
    {
        return new ValueTask<IKernelCompiler>(new MyBackendCompiler());
    }
    
    private bool CheckHardwareAvailability()
    {
        // Hardware detection logic
        return true;
    }
}
```

3. **Implement Accelerator**
```csharp
public class MyBackendAccelerator : IAccelerator
{
    public string Name => "MyBackend Accelerator";
    public AcceleratorType Type => AcceleratorType.Custom;
    public AcceleratorInfo Info { get; }
    
    public async ValueTask<IMemoryBuffer<T>> AllocateAsync<T>(int size) where T : unmanaged
    {
        // Allocate memory on your hardware
        return new MyBackendMemoryBuffer<T>(size);
    }
    
    public async ValueTask ExecuteKernelAsync(IKernel kernel, object parameters)
    {
        // Execute kernel on your hardware
        var compiledKernel = (MyBackendKernel)kernel;
        await compiledKernel.ExecuteAsync(parameters);
    }
    
    // Implement remaining interface members...
}
```

4. **Implement Kernel Compiler**
```csharp
public class MyBackendCompiler : IKernelCompiler
{
    public async ValueTask<ICompiledKernel> CompileAsync(
        KernelDefinition definition,
        CompilationOptions? options = null)
    {
        // Compile C# kernel to your target format
        var targetCode = TranslateToTargetLanguage(definition.Source);
        var binaryCode = CompileTargetCode(targetCode);
        
        return new MyBackendCompiledKernel(binaryCode);
    }
    
    private string TranslateToTargetLanguage(string csharpSource)
    {
        // Convert C# to your target language (HLSL, MSL, OpenCL C, etc.)
        return translatedCode;
    }
}
```

### Backend Testing

Create comprehensive tests for your backend:

```csharp
[TestClass]
public class MyBackendTests
{
    [TestMethod]
    public async Task CreateAccelerator_ShouldReturnValidAccelerator()
    {
        var plugin = new MyBackendPlugin();
        var accelerator = await plugin.CreateAcceleratorAsync();
        
        accelerator.Should().NotBeNull();
        accelerator.Type.Should().Be(AcceleratorType.Custom);
    }
    
    [TestMethod]
    public async Task ExecuteKernel_WithValidParameters_ShouldSucceed()
    {
        // Test kernel execution
    }
    
    [TestMethod]
    [TestCategory("Hardware")]
    public async Task HardwareTest_OnRealDevice_ShouldWork()
    {
        // Test on real hardware if available
    }
}
```

## 📊 Performance Considerations

### Optimization Guidelines

#### Memory Management
```csharp
// Good: Use memory pooling
public class OptimizedService
{
    private readonly IMemoryPool _memoryPool;
    
    public async ValueTask ProcessAsync<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        var buffer = _memoryPool.Rent<T>(data.Length);
        try
        {
            // Use buffer
        }
        finally
        {
            _memoryPool.Return(buffer);
        }
    }
}

// Bad: Frequent allocations
public async ValueTask ProcessAsync<T>(ReadOnlySpan<T> data) where T : unmanaged
{
    var buffer = new T[data.Length]; // ❌ Allocation per call
}
```

#### Async Patterns
```csharp
// Good: ValueTask for performance
public ValueTask<int> FastPathAsync()
{
    if (CanReturnSynchronously())
        return new ValueTask<int>(synchronousResult);
    
    return SlowPathAsync();
}

// Good: ConfigureAwait(false) in libraries
public async ValueTask SlowPathAsync()
{
    await SomeAsyncOperation().ConfigureAwait(false);
}
```

#### Hot Path Optimization
```csharp
// Use [MethodImpl] for hot paths
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public float VectorMagnitude(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

// Use unsafe code when justified
public unsafe void FastCopy(float* source, float* destination, int count)
{
    Buffer.MemoryCopy(source, destination, count * sizeof(float), count * sizeof(float));
}
```

### Performance Testing

```csharp
[TestClass]
public class PerformanceTests
{
    [TestMethod]
    [TestCategory("Performance")]
    public async Task VectorAddition_Performance_ShouldMeetTargets()
    {
        const int size = 1_000_000;
        var a = new float[size];
        var b = new float[size];
        var result = new float[size];
        
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < 100; i++)
        {
            await compute.ExecuteAsync("VectorAdd", new { a, b, result });
        }
        
        stopwatch.Stop();
        var avgTime = stopwatch.ElapsedMilliseconds / 100.0;
        
        // Assert performance targets
        avgTime.Should().BeLessThan(10, "Vector addition should complete in under 10ms");
    }
}
```

## 📝 Documentation Guidelines

### README Updates
When adding new features, update relevant documentation:

- Main README.md for user-facing changes
- Architecture documentation for design changes
- API documentation for new interfaces
- Getting started guide for usage examples

### Code Examples
Include practical examples in documentation:

```csharp
// Example: New feature usage
var newFeature = serviceProvider.GetRequiredService<INewFeature>();
var result = await newFeature.ProcessAsync(inputData);
```

### Changelog
Update CHANGELOG.md with:
- New features
- Breaking changes
- Bug fixes
- Performance improvements

## 🚀 Pull Request Process

### Before Submitting

1. **Ensure tests pass**
   ```bash
   dotnet test --configuration Release
   ```

2. **Check code coverage**
   ```bash
   dotnet test --collect:"XPlat Code Coverage"
   # Coverage should be ≥75%
   ```

3. **Run static analysis**
   ```bash
   dotnet build --verbosity normal
   # Fix all warnings
   ```

4. **Format code**
   ```bash
   dotnet format
   ```

### PR Description Template

```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update

## Testing
- [ ] Unit tests pass
- [ ] Integration tests pass
- [ ] Hardware tests pass (if applicable)
- [ ] Manual testing completed

## Performance Impact
- [ ] No performance impact
- [ ] Performance improved
- [ ] Performance regression (explain why acceptable)

## Documentation
- [ ] Code comments updated
- [ ] API documentation updated
- [ ] User documentation updated

## Screenshots (if applicable)

## Additional Context
```

### Review Process

1. **Automated checks must pass**
   - Build succeeds
   - Tests pass
   - Code coverage maintained
   - Security scan passes

2. **Manual review required for**
   - API changes
   - Performance-critical code
   - New backend implementations

3. **Merge criteria**
   - At least one approving review
   - All conversations resolved
   - CI/CD pipeline passes

## 🔒 Security Guidelines

### Secure Coding

```csharp
// Input validation
public void ProcessKernel(string kernelSource)
{
    if (string.IsNullOrWhiteSpace(kernelSource))
        throw new ArgumentException("Kernel source cannot be empty", nameof(kernelSource));
        
    // Validate kernel for security issues
    var validationResult = _kernelValidator.Validate(kernelSource);
    if (!validationResult.IsSecure)
        throw new SecurityException($"Kernel contains security violations: {validationResult.Issues}");
}

// Safe memory operations
public unsafe void SafeMemoryCopy(void* source, void* destination, int size)
{
    if (source == null || destination == null)
        throw new ArgumentNullException();
        
    if (size < 0)
        throw new ArgumentOutOfRangeException(nameof(size));
        
    // Bounds checking before copy
    Buffer.MemoryCopy(source, destination, size, size);
}
```

### Security Testing

```csharp
[TestClass]
public class SecurityTests
{
    [TestMethod]
    public void KernelValidator_WithMaliciousCode_ShouldReject()
    {
        var maliciousKernel = "/* potentially malicious code */";
        
        var validator = new KernelValidator();
        var result = validator.Validate(maliciousKernel);
        
        result.IsSecure.Should().BeFalse();
        result.Issues.Should().NotBeEmpty();
    }
}
```

## 🏆 Contributing Guidelines

### Ways to Contribute

1. **Bug Reports**
   - Use issue templates
   - Include reproduction steps
   - Provide system information

2. **Feature Requests**
   - Explain use case and benefits
   - Consider backwards compatibility
   - Propose API design

3. **Code Contributions**
   - Start with good first issues
   - Follow development workflow
   - Include comprehensive tests

4. **Documentation**
   - Fix typos and errors
   - Add missing examples
   - Improve clarity

5. **Performance**
   - Identify bottlenecks
   - Propose optimizations
   - Validate with benchmarks

### Recognition

Contributors are recognized in:
- README.md contributors section
- Release notes for significant contributions
- GitHub contributor graphs

## 📞 Getting Help

### Resources
- **GitHub Discussions**: Community Q&A
- **GitHub Issues**: Bug reports and features
- **Discord**: Real-time chat (link in README)

### Maintainer Response Times
- **Critical bugs**: Within 24 hours
- **General issues**: Within 1 week
- **Feature requests**: Within 2 weeks
- **Pull requests**: Within 1 week

---

Thank you for contributing to DotCompute! Your efforts help make GPU acceleration accessible to the entire .NET community. 🚀