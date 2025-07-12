# Contributing to DotCompute

First off, thank you for considering contributing to DotCompute! It's people like you that make DotCompute such a great tool.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [How Can I Contribute?](#how-can-i-contribute)
- [Development Process](#development-process)
- [Style Guidelines](#style-guidelines)
- [Testing Guidelines](#testing-guidelines)
- [Pull Request Process](#pull-request-process)
- [Community](#community)

## Code of Conduct

This project and everyone participating in it is governed by the [DotCompute Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

## Getting Started

### Prerequisites

- .NET 9.0 SDK or later
- Visual Studio 2022 / VS Code / Rider
- Git
- (Optional) CUDA Toolkit 12.0+ for CUDA backend development
- (Optional) Xcode for Metal backend development on macOS

### Setting Up Development Environment

1. **Fork and Clone**
   ```bash
   git clone https://github.com/YOUR_USERNAME/dotcompute.git
   cd dotcompute
   git remote add upstream https://github.com/dotcompute/dotcompute.git
   ```

2. **Create a Branch**
   ```bash
   git checkout -b feature/my-new-feature
   ```

3. **Restore Dependencies**
   ```bash
   dotnet restore
   ```

4. **Build**
   ```bash
   dotnet build
   ```

5. **Run Tests**
   ```bash
   dotnet test
   ```

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check existing issues to avoid duplicates. When creating a bug report, include:

- **Clear and descriptive title**
- **Steps to reproduce**
- **Expected behavior**
- **Actual behavior**
- **Environment details** (OS, .NET version, GPU model)
- **Minimal reproducible example**
- **Stack traces and error messages**

Use the [bug report template](.github/ISSUE_TEMPLATE/bug_report.md).

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion, include:

- **Clear and descriptive title**
- **Detailed description** of the proposed enhancement
- **Use cases** that would benefit
- **Possible implementation** approach
- **Alternative solutions** you've considered

Use the [feature request template](.github/ISSUE_TEMPLATE/feature_request.md).

### Code Contributions

#### Good First Issues

Look for issues labeled [`good first issue`](https://github.com/dotcompute/dotcompute/labels/good%20first%20issue) or [`help wanted`](https://github.com/dotcompute/dotcompute/labels/help%20wanted).

#### Areas We Need Help

- **Backend Development**: New accelerator backends (Vulkan, WebGPU, DirectML)
- **Algorithm Library**: High-performance algorithms (sorting, graph algorithms, cryptography)
- **Documentation**: Tutorials, examples, API documentation improvements
- **Testing**: Increasing test coverage, stress tests, benchmarks
- **Performance**: Optimization opportunities, benchmarking
- **Tooling**: VS extensions, analyzers, code generators

## Development Process

### Project Structure

```
DotCompute/
├── src/
│   ├── DotCompute.Core/           # Core abstractions
│   ├── DotCompute.Generators/     # Source generators
│   └── DotCompute.Runtime/        # Runtime components
├── plugins/
│   ├── backends/                  # Accelerator backends
│   └── algorithms/                # Algorithm libraries
├── tests/                         # Test projects
├── samples/                       # Example projects
└── docs/                         # Documentation
```

### Building Specific Components

```bash
# Build only core
dotnet build src/DotCompute.Core

# Build specific backend
dotnet build plugins/backends/DotCompute.Backends.CUDA

# Build and pack
dotnet pack -c Release
```

### Running Tests

```bash
# All tests
dotnet test

# Specific test project
dotnet test tests/DotCompute.Core.Tests

# With coverage
dotnet test --collect:"XPlat Code Coverage"

# Specific test
dotnet test --filter "FullyQualifiedName~VectorAdd"
```

## Style Guidelines

### C# Coding Conventions

We follow the [.NET Runtime coding guidelines](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md) with these additions:

1. **File Organization**
   ```csharp
   // 1. Using statements
   using System;
   using System.Threading.Tasks;
   using DotCompute.Core;
   
   // 2. Namespace
   namespace DotCompute.Backends.CUDA;
   
   // 3. Type declaration
   public sealed class CudaAccelerator : IAccelerator
   {
       // 4. Fields
       private readonly int _deviceId;
       
       // 5. Constructors
       public CudaAccelerator(int deviceId) => _deviceId = deviceId;
       
       // 6. Properties
       public AcceleratorInfo Info { get; }
       
       // 7. Methods
       public ValueTask<ICompiledKernel> CompileKernelAsync() { }
       
       // 8. Nested types
       private sealed class KernelCompiler { }
   }
   ```

2. **Naming Conventions**
   - Async methods end with `Async`
   - Private fields start with `_`
   - Use meaningful names, avoid abbreviations
   - Generic type parameters: `T` for single, `TInput`/`TOutput` for multiple

3. **Documentation**
   - All public APIs must have XML documentation
   - Include examples in documentation
   - Document exceptions that can be thrown

4. **Modern C# Features**
   - Use file-scoped namespaces
   - Use target-typed new
   - Use pattern matching
   - Prefer `ValueTask` for async operations

### Commit Messages

Follow the [Conventional Commits](https://www.conventionalcommits.org/) specification:

```
type(scope): subject

body

footer
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes
- `refactor`: Code refactoring
- `perf`: Performance improvements
- `test`: Test additions/changes
- `build`: Build system changes
- `ci`: CI configuration changes
- `chore`: Other changes

Examples:
```
feat(cuda): add tensor core support for matrix multiplication
fix(memory): resolve memory leak in buffer pooling
docs(readme): update installation instructions
perf(kernel): optimize vector addition kernel by 15%
```

## Testing Guidelines

### Test Organization

```csharp
public class AcceleratorTests : AcceleratorTestBase
{
    // Arrange shared state in constructor or setup
    
    [Fact]
    public async Task MethodName_StateUnderTest_ExpectedBehavior()
    {
        // Arrange
        var input = CreateTestData();
        
        // Act
        var result = await RunTestAsync(input);
        
        // Assert
        result.Should().BeEquivalentTo(expected);
    }
}
```

### Test Requirements

1. **Unit Tests**: Required for all new code
2. **Integration Tests**: Required for backend implementations
3. **Performance Tests**: Required for algorithm implementations
4. **Platform Tests**: Must pass on Windows, Linux, macOS

### Test Coverage

- Minimum 80% code coverage for new code
- Critical paths require 95%+ coverage
- Use `[Theory]` for parameterized tests
- Test edge cases and error conditions

## Pull Request Process

1. **Before Submitting**
   - Ensure all tests pass
   - Update documentation
   - Add tests for new functionality
   - Run code formatting: `dotnet format`
   - Update CHANGELOG.md if applicable

2. **PR Description**
   - Reference related issues
   - Describe changes made
   - Include benchmark results for performance changes
   - Add screenshots for UI changes
   - List breaking changes

3. **Review Process**
   - PRs require 2 approvals
   - Address review feedback
   - Keep PR focused and small
   - Resolve merge conflicts

4. **After Merge**
   - Delete your feature branch
   - Update your fork

### PR Template

```markdown
## Description
Brief description of changes

## Related Issues
Fixes #123

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Testing
- [ ] Unit tests pass
- [ ] Integration tests pass
- [ ] Manual testing completed

## Checklist
- [ ] Code follows style guidelines
- [ ] Self-review completed
- [ ] Documentation updated
- [ ] Tests added/updated
- [ ] No breaking changes (or documented)
```

## Community

### Communication Channels

- **Discord**: [Join our Discord](https://discord.gg/dotcompute)
- **GitHub Discussions**: For questions and ideas
- **Twitter**: [@dotcompute](https://twitter.com/dotcompute)

### Getting Help

- Check documentation first
- Search existing issues
- Ask in Discord #help channel
- Create a discussion for questions

### Recognition

Contributors are recognized in:
- CONTRIBUTORS.md file
- Release notes
- Project website
- Annual contributor report

## Development Tips

### Debugging Kernels

```csharp
// Enable kernel debugging
services.AddDotCompute(options =>
{
    options.EnableDebugging = true;
    options.BreakOnKernelError = true;
});
```

### Performance Profiling

```csharp
// Use built-in profiler
var profiler = services.GetRequiredService<IKernelProfiler>();
var report = await profiler.ProfileExecutionAsync(kernel, data);
```

### Memory Leak Detection

```bash
# Run with leak detection
dotnet test --environment DOTCOMPUTE_DETECT_LEAKS=true
```

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

## Questions?

Feel free to ask in:
- Discord #contributors channel
- GitHub Discussions
- Email: contributors@dotcompute.io

Thank you for contributing to DotCompute! 🚀