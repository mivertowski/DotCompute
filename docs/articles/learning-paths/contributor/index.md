# Contributor Learning Path

Learn how to contribute to the DotCompute framework by understanding its architecture, source generators, analyzers, and testing practices.

## Prerequisites

- Completed [Advanced Path](../advanced/index.md) or equivalent experience
- Strong understanding of DotCompute's features and APIs
- Familiarity with Roslyn (C# compiler APIs)

## Learning Objectives

By completing this path, you will:

1. Understand DotCompute's layered architecture
2. Build source generators for compile-time code generation
3. Create Roslyn analyzers for GPU code validation
4. Implement comprehensive test suites

## Modules

### Module 1: Architecture Deep Dive
**Duration**: 90-120 minutes

Understand the core abstractions, backend system, and extension points.

[Start Module 1 →](architecture-deep-dive.md)

### Module 2: Source Generator Development
**Duration**: 90-120 minutes

Build compile-time code generators for GPU kernels.

[Start Module 2 →](source-generators.md)

### Module 3: Analyzer Development
**Duration**: 60-90 minutes

Create Roslyn analyzers to validate GPU code patterns.

[Start Module 3 →](analyzers.md)

### Module 4: Testing and Benchmarking
**Duration**: 60-90 minutes

Implement unit tests, hardware tests, and performance benchmarks.

[Start Module 4 →](testing-benchmarking.md)

## Completion Checklist

- [ ] Explain the four-layer architecture
- [ ] Create a source generator for a custom attribute
- [ ] Implement an analyzer with code fix
- [ ] Write unit and hardware tests

## Contributing Guidelines

Before contributing:

1. Read the project [CLAUDE.md](../../../CLAUDE.md) for development principles
2. Ensure your code follows the established patterns
3. Include tests for new functionality
4. Update documentation as needed

## Getting Started

```bash
# Clone repository
git clone https://github.com/mivertowski/DotCompute.git
cd DotCompute

# Build solution
dotnet build DotCompute.sln --configuration Release

# Run tests
./scripts/run-tests.sh DotCompute.sln --configuration Release
```

---

*Estimated total duration: 4-6 hours*
