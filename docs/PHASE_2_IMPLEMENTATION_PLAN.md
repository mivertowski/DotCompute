# Phase 2: C# to CUDA Translator - Implementation Plan

**Status**: Ready to Start
**Started**: 2025-11-17
**Target Completion**: 2025-11-22 (5 days)
**Goal**: Enable automatic translation of C# kernel logic to CUDA C

---

## 🎯 Objectives

**Primary Goal**: Translate C# method bodies to CUDA C for ring kernel message processing

**Success Criteria**:
- ✅ Translate 20+ C# methods to CUDA
- ✅ Support all basic language constructs (if/else, loops, switch)
- ✅ Comprehensive error reporting with line numbers
- ✅ Generated code passes nvcc compilation
- ✅ 50+ unit tests covering all translation scenarios

---

## 🏗️ Architecture Overview

```
C# Ring Kernel Method
         ↓
   Roslyn Syntax Tree
         ↓
   CSharpToCudaTranslator
         ↓
   CUDA C Function Body
         ↓
   Integrate with MessageCodeGenerator
         ↓
   Complete Ring Kernel .cu File
```

---

## 📋 Task Breakdown

### Task 1: Core Translator Infrastructure (Day 1)

**Components**:
1. `CSharpToCudaTranslator.cs` - Main translation engine
2. `CudaCodeBuilder.cs` - CUDA code generation with indentation
3. `TypeMapper.cs` - C# to CUDA type conversion
4. `VariableScope.cs` - Variable scope tracking

**Features**:
- Roslyn syntax tree visitor pattern
- CUDA code generation with proper formatting
- Type system mapping (primitives → CUDA types)
- Variable scope management
- Error collection with source locations

**Tests** (15 tests):
- Basic method translation
- Type mapping (int, float, double, bool, byte)
- Variable declarations (local, parameters)
- Scope tracking (nested blocks)
- Error reporting (line numbers, context)

**Files Created**:
- `src/Runtime/DotCompute.Generators/CudaTranslation/CSharpToCudaTranslator.cs`
- `src/Runtime/DotCompute.Generators/CudaTranslation/CudaCodeBuilder.cs`
- `src/Runtime/DotCompute.Generators/CudaTranslation/TypeMapper.cs`
- `src/Runtime/DotCompute.Generators/CudaTranslation/VariableScope.cs`
- `tests/Unit/DotCompute.Generators.Tests/CudaTranslation/CSharpToCudaTranslatorTests.cs`

---

### Task 2: Expression Translation (Day 2)

**Components**:
1. `ExpressionTranslator.cs` - Expression translation
2. `OperatorMapper.cs` - C# to CUDA operator mapping

**Supported Expressions**:
- Arithmetic: `+`, `-`, `*`, `/`, `%`
- Logical: `&&`, `||`, `!`
- Bitwise: `&`, `|`, `^`, `~`, `<<`, `>>`
- Comparison: `==`, `!=`, `<`, `>`, `<=`, `>=`
- Assignment: `=`, `+=`, `-=`, `*=`, `/=`
- Array access: `array[index]`
- Member access: `obj.field`
- Method calls: `Math.Sqrt(x)`, `Atomics.Add(ptr, value)`

**Tests** (20 tests):
- All arithmetic operators
- All logical operators
- All bitwise operators
- All comparison operators
- Compound assignments
- Array indexing
- Member access
- Method calls (intrinsics, atomics)

**Files Created**:
- `src/Runtime/DotCompute.Generators/CudaTranslation/ExpressionTranslator.cs`
- `src/Runtime/DotCompute.Generators/CudaTranslation/OperatorMapper.cs`
- `tests/Unit/DotCompute.Generators.Tests/CudaTranslation/ExpressionTranslatorTests.cs`

---

### Task 3: Control Flow Translation (Day 3)

**Components**:
1. `ControlFlowTranslator.cs` - Control flow statement translation

**Supported Constructs**:
- **If/Else**: Single, nested, else-if chains
- **Loops**: `for`, `while`, `do-while`
- **Switch**: Integer cases, fall-through, default
- **Break/Continue**: Loop control
- **Return**: Early return with values

**Tests** (15 tests):
- If statements (simple, nested, else-if)
- For loops (standard, reverse, step)
- While loops (condition, infinite)
- Do-while loops
- Switch statements (int, fall-through, default)
- Break/continue in loops
- Return statements (void, value)

**Files Created**:
- `src/Runtime/DotCompute.Generators/CudaTranslation/ControlFlowTranslator.cs`
- `tests/Unit/DotCompute.Generators.Tests/CudaTranslation/ControlFlowTranslatorTests.cs`

---

### Task 4: CUDA-Specific Constructs (Day 4)

**Components**:
1. `CudaIntrinsicMapper.cs` - CUDA intrinsic function mapping
2. `ThreadIdResolver.cs` - Thread ID expression resolution

**Supported CUDA Features**:
- **Thread IDs**: `threadIdx.x/y/z`, `blockIdx.x/y/z`, `blockDim.x/y/z`
- **Atomics**: `atomicAdd`, `atomicSub`, `atomicExch`, `atomicCAS`
- **Shared Memory**: `__shared__` keyword
- **Synchronization**: `__syncthreads()`, barriers
- **Bounds Checking**: `if (idx < length)` patterns
- **Math Functions**: `sqrtf`, `powf`, `sinf`, `cosf`

**Tests** (10 tests):
- Thread ID expressions
- Atomic operations (add, sub, exch, cas)
- Shared memory declarations
- Synchronization barriers
- Bounds checking patterns
- Math function mapping

**Files Created**:
- `src/Runtime/DotCompute.Generators/CudaTranslation/CudaIntrinsicMapper.cs`
- `src/Runtime/DotCompute.Generators/CudaTranslation/ThreadIdResolver.cs`
- `tests/Unit/DotCompute.Generators.Tests/CudaTranslation/CudaIntrinsicMapperTests.cs`

---

### Task 5: Integration & Validation (Day 5)

**Components**:
1. Integrate with `MessageCodeGenerator`
2. End-to-end kernel translation
3. NVCC compilation validation
4. Performance benchmarking

**Integration Points**:
- `MessageCodeGenerator` uses `CSharpToCudaTranslator`
- Generate complete `.cu` files with kernel functions
- Validate generated CUDA code compiles with nvcc
- Benchmark translation performance

**Tests** (10 tests):
- Complete kernel translation (VectorAdd, MatrixMultiply, Reduction)
- NVCC compilation (syntax validation)
- Error reporting (invalid C# code)
- Performance (translation time < 100ms per method)
- Round-trip (C# → CUDA → execution)

**Files Modified**:
- `src/Runtime/DotCompute.Generators/MemoryPack/MessageCodeGenerator.cs`
- `tests/Unit/DotCompute.Generators.Tests/MemoryPack/MessageCodeGeneratorTests.cs`

**New Tests**:
- `tests/Unit/DotCompute.Generators.Tests/CudaTranslation/EndToEndTranslationTests.cs`

---

## 🧪 Testing Strategy

### Unit Tests (70 total)
- **Infrastructure** (15 tests): Core translator, type mapper, scope tracking
- **Expressions** (20 tests): All operators, array access, method calls
- **Control Flow** (15 tests): If/else, loops, switch, break/continue
- **CUDA Features** (10 tests): Thread IDs, atomics, shared memory
- **Integration** (10 tests): Complete kernel translation, nvcc validation

### Integration Tests (5 tests)
- **VectorAdd**: Simple parallel operation
- **MatrixMultiply**: 2D thread indexing
- **Reduction**: Shared memory, atomics, synchronization
- **Convolution**: Complex indexing, bounds checking
- **HistogramUpdate**: Atomic operations, race conditions

### Performance Tests (3 tests)
- **Translation Speed**: < 100ms per method
- **Generated Code Size**: < 10KB per kernel
- **NVCC Compilation**: < 2s per .cu file

---

## 📊 Success Metrics

| Metric | Target | Testing |
|--------|--------|---------|
| C# Methods Translated | 20+ | Unit tests |
| Language Constructs | 100% (if/else, loops, switch) | Unit tests |
| CUDA Intrinsics | 10+ (atomics, threadIdx, math) | Unit tests |
| NVCC Compilation | 100% pass rate | Integration |
| Translation Performance | < 100ms per method | Benchmarks |
| Test Coverage | > 90% | Code coverage |

---

## 🚨 Risk Mitigation

### Risk 1: Complex C# Constructs
**Risk**: LINQ, lambdas, async/await not translatable to CUDA
**Mitigation**: Restrict to imperative subset of C# (document limitations)
**Detection**: Analyzer warnings at compile time

### Risk 2: Type System Mismatches
**Risk**: C# generics, nullable types difficult to map to CUDA
**Mitigation**: Support limited generics (primitives only), explicit nullability
**Detection**: Type mapper validation with error messages

### Risk 3: NVCC Compilation Failures
**Risk**: Generated CUDA code has syntax errors
**Mitigation**: Comprehensive unit tests, nvcc validation in CI
**Detection**: Integration tests with real NVCC compilation

### Risk 4: Performance Overhead
**Risk**: Translation adds significant build time
**Mitigation**: Caching, incremental builds, parallel translation
**Detection**: Performance benchmarks

---

## 📁 File Structure

```
src/Runtime/DotCompute.Generators/
└── CudaTranslation/
    ├── CSharpToCudaTranslator.cs       # Main translator engine
    ├── CudaCodeBuilder.cs              # CUDA code generation
    ├── TypeMapper.cs                   # Type system mapping
    ├── VariableScope.cs                # Scope tracking
    ├── ExpressionTranslator.cs         # Expression translation
    ├── OperatorMapper.cs               # Operator mapping
    ├── ControlFlowTranslator.cs        # Control flow translation
    ├── CudaIntrinsicMapper.cs          # CUDA intrinsics
    └── ThreadIdResolver.cs             # Thread ID resolution

tests/Unit/DotCompute.Generators.Tests/
└── CudaTranslation/
    ├── CSharpToCudaTranslatorTests.cs  # Core translator tests
    ├── ExpressionTranslatorTests.cs    # Expression tests
    ├── ControlFlowTranslatorTests.cs   # Control flow tests
    ├── CudaIntrinsicMapperTests.cs     # CUDA features tests
    └── EndToEndTranslationTests.cs     # Integration tests
```

---

## 🎯 Daily Goals

### Day 1 (2025-11-17): Core Infrastructure
- ✅ Create translator skeleton
- ✅ Implement type mapper
- ✅ Add variable scope tracking
- ✅ 15/15 infrastructure tests passing

### Day 2 (2025-11-18): Expression Translation
- ✅ Implement expression translator
- ✅ Map all C# operators to CUDA
- ✅ Support array access and member access
- ✅ 20/20 expression tests passing

### Day 3 (2025-11-19): Control Flow
- ✅ Translate if/else statements
- ✅ Translate all loop types
- ✅ Translate switch statements
- ✅ 15/15 control flow tests passing

### Day 4 (2025-11-20): CUDA Features
- ✅ Map thread ID expressions
- ✅ Map atomic operations
- ✅ Support shared memory
- ✅ 10/10 CUDA feature tests passing

### Day 5 (2025-11-21): Integration
- ✅ Integrate with MessageCodeGenerator
- ✅ End-to-end kernel translation
- ✅ NVCC validation
- ✅ 10/10 integration tests passing

**Target**: 70/70 tests passing by 2025-11-22

---

## 🚀 Next Steps

**Immediate Actions** (Day 1):
1. Create `CudaTranslation/` directory structure
2. Implement `CSharpToCudaTranslator.cs` skeleton
3. Implement `TypeMapper.cs` for primitive types
4. Implement `CudaCodeBuilder.cs` for code generation
5. Create `CSharpToCudaTranslatorTests.cs` with 15 tests

**Ready to Begin**: Phase 2 implementation starts now!

---

**Last Updated**: 2025-11-17
**Next Review**: 2025-11-18 (daily progress checks)
