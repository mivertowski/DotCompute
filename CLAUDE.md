# Claude Code Configuration - SPARC Development Environment

## 🚨 CRITICAL: CONCURRENT EXECUTION & FILE MANAGEMENT

**ABSOLUTE RULES**:
1. ALL operations MUST be concurrent/parallel in a single message
2. **NEVER save working files, text/mds and tests to the root folder**
3. ALWAYS organize files in appropriate subdirectories
4. **USE CLAUDE CODE'S TASK TOOL** for spawning agents concurrently, not just MCP

### ⚡ GOLDEN RULE: "1 MESSAGE = ALL RELATED OPERATIONS"

**MANDATORY PATTERNS:**
- **TodoWrite**: ALWAYS batch ALL todos in ONE call (5-10+ todos minimum)
- **Task tool (Claude Code)**: ALWAYS spawn ALL agents in ONE message with full instructions
- **File operations**: ALWAYS batch ALL reads/writes/edits in ONE message
- **Bash commands**: ALWAYS batch ALL terminal operations in ONE message
- **Memory operations**: ALWAYS batch ALL memory store/retrieve in ONE message

### 🎯 CRITICAL: Claude Code Task Tool for Agent Execution

**Claude Code's Task tool is the PRIMARY way to spawn agents:**
```javascript
// ✅ CORRECT: Use Claude Code's Task tool for parallel agent execution
[Single Message]:
  Task("Research agent", "Analyze requirements and patterns...", "researcher")
  Task("Coder agent", "Implement core features...", "coder")
  Task("Tester agent", "Create comprehensive tests...", "tester")
  Task("Reviewer agent", "Review code quality...", "reviewer")
  Task("Architect agent", "Design system architecture...", "system-architect")
```

**MCP tools are ONLY for coordination setup:**
- `mcp__claude-flow__swarm_init` - Initialize coordination topology
- `mcp__claude-flow__agent_spawn` - Define agent types for coordination
- `mcp__claude-flow__task_orchestrate` - Orchestrate high-level workflows

### 📁 File Organization Rules

**NEVER save to root folder. Use these directories:**
- `/src` - Source code files
- `/tests` - Test files
- `/docs` - Documentation and markdown files
- `/config` - Configuration files
- `/scripts` - Utility scripts
- `/examples` - Example code

## Project Overview

This project uses SPARC (Specification, Pseudocode, Architecture, Refinement, Completion) methodology with Claude-Flow orchestration for systematic Test-Driven Development.

## SPARC Commands

### Core Commands
- `npx claude-flow sparc modes` - List available modes
- `npx claude-flow sparc run <mode> "<task>"` - Execute specific mode
- `npx claude-flow sparc tdd "<feature>"` - Run complete TDD workflow
- `npx claude-flow sparc info <mode>` - Get mode details

### Batchtools Commands
- `npx claude-flow sparc batch <modes> "<task>"` - Parallel execution
- `npx claude-flow sparc pipeline "<task>"` - Full pipeline processing
- `npx claude-flow sparc concurrent <mode> "<tasks-file>"` - Multi-task processing

### Build Commands
- `npm run build` - Build project
- `npm run test` - Run tests
- `npm run lint` - Linting
- `npm run typecheck` - Type checking

## SPARC Workflow Phases

1. **Specification** - Requirements analysis (`sparc run spec-pseudocode`)
2. **Pseudocode** - Algorithm design (`sparc run spec-pseudocode`)
3. **Architecture** - System design (`sparc run architect`)
4. **Refinement** - TDD implementation (`sparc tdd`)
5. **Completion** - Integration (`sparc run integration`)

## Code Style & Best Practices

- **Modular Design**: Files under 500 lines
- **Environment Safety**: Never hardcode secrets
- **Test-First**: Write tests before implementation
- **Clean Architecture**: Separate concerns
- **Documentation**: Keep updated

## 🚀 Available Agents (54 Total)

### Core Development
`coder`, `reviewer`, `tester`, `planner`, `researcher`

### Swarm Coordination
`hierarchical-coordinator`, `mesh-coordinator`, `adaptive-coordinator`, `collective-intelligence-coordinator`, `swarm-memory-manager`

### Consensus & Distributed
`byzantine-coordinator`, `raft-manager`, `gossip-coordinator`, `consensus-builder`, `crdt-synchronizer`, `quorum-manager`, `security-manager`

### Performance & Optimization
`perf-analyzer`, `performance-benchmarker`, `task-orchestrator`, `memory-coordinator`, `smart-agent`

### GitHub & Repository
`github-modes`, `pr-manager`, `code-review-swarm`, `issue-tracker`, `release-manager`, `workflow-automation`, `project-board-sync`, `repo-architect`, `multi-repo-swarm`

### SPARC Methodology
`sparc-coord`, `sparc-coder`, `specification`, `pseudocode`, `architecture`, `refinement`

### Specialized Development
`backend-dev`, `mobile-dev`, `ml-developer`, `cicd-engineer`, `api-docs`, `system-architect`, `code-analyzer`, `base-template-generator`

### Testing & Validation
`tdd-london-swarm`, `production-validator`

### Migration & Planning
`migration-planner`, `swarm-init`

## 🎯 Claude Code vs MCP Tools

### Claude Code Handles ALL EXECUTION:
- **Task tool**: Spawn and run agents concurrently for actual work
- File operations (Read, Write, Edit, MultiEdit, Glob, Grep)
- Code generation and programming
- Bash commands and system operations
- Implementation work
- Project navigation and analysis
- TodoWrite and task management
- Git operations
- Package management
- Testing and debugging

### MCP Tools ONLY COORDINATE:
- Swarm initialization (topology setup)
- Agent type definitions (coordination patterns)
- Task orchestration (high-level planning)
- Memory management
- Neural features
- Performance tracking
- GitHub integration

**KEY**: MCP coordinates the strategy, Claude Code's Task tool executes with real agents.

## 🚀 Quick Setup

```bash
# Add MCP servers (Claude Flow required, others optional)
claude mcp add claude-flow npx claude-flow@alpha mcp start
claude mcp add ruv-swarm npx ruv-swarm mcp start  # Optional: Enhanced coordination
claude mcp add flow-nexus npx flow-nexus@latest mcp start  # Optional: Cloud features
```

## MCP Tool Categories

### Coordination
`swarm_init`, `agent_spawn`, `task_orchestrate`

### Monitoring
`swarm_status`, `agent_list`, `agent_metrics`, `task_status`, `task_results`

### Memory & Neural
`memory_usage`, `neural_status`, `neural_train`, `neural_patterns`

### GitHub Integration
`github_swarm`, `repo_analyze`, `pr_enhance`, `issue_triage`, `code_review`

### System
`benchmark_run`, `features_detect`, `swarm_monitor`

### Flow-Nexus MCP Tools (Optional Advanced Features)
Flow-Nexus extends MCP capabilities with 70+ cloud-based orchestration tools:

**Key MCP Tool Categories:**
- **Swarm & Agents**: `swarm_init`, `swarm_scale`, `agent_spawn`, `task_orchestrate`
- **Sandboxes**: `sandbox_create`, `sandbox_execute`, `sandbox_upload` (cloud execution)
- **Templates**: `template_list`, `template_deploy` (pre-built project templates)
- **Neural AI**: `neural_train`, `neural_patterns`, `seraphina_chat` (AI assistant)
- **GitHub**: `github_repo_analyze`, `github_pr_manage` (repository management)
- **Real-time**: `execution_stream_subscribe`, `realtime_subscribe` (live monitoring)
- **Storage**: `storage_upload`, `storage_list` (cloud file management)

**Authentication Required:**
- Register: `mcp__flow-nexus__user_register` or `npx flow-nexus@latest register`
- Login: `mcp__flow-nexus__user_login` or `npx flow-nexus@latest login`
- Access 70+ specialized MCP tools for advanced orchestration

## 🚀 Agent Execution Flow with Claude Code

### The Correct Pattern:

1. **Optional**: Use MCP tools to set up coordination topology
2. **REQUIRED**: Use Claude Code's Task tool to spawn agents that do actual work
3. **REQUIRED**: Each agent runs hooks for coordination
4. **REQUIRED**: Batch all operations in single messages

### Example Full-Stack Development:

```javascript
// Single message with all agent spawning via Claude Code's Task tool
[Parallel Agent Execution]:
  Task("Backend Developer", "Build REST API with Express. Use hooks for coordination.", "backend-dev")
  Task("Frontend Developer", "Create React UI. Coordinate with backend via memory.", "coder")
  Task("Database Architect", "Design PostgreSQL schema. Store schema in memory.", "code-analyzer")
  Task("Test Engineer", "Write Jest tests. Check memory for API contracts.", "tester")
  Task("DevOps Engineer", "Setup Docker and CI/CD. Document in memory.", "cicd-engineer")
  Task("Security Auditor", "Review authentication. Report findings via hooks.", "reviewer")
  
  // All todos batched together
  TodoWrite { todos: [...8-10 todos...] }
  
  // All file operations together
  Write "backend/server.js"
  Write "frontend/App.jsx"
  Write "database/schema.sql"
```

## 📋 Agent Coordination Protocol

### Every Agent Spawned via Task Tool MUST:

**1️⃣ BEFORE Work:**
```bash
npx claude-flow@alpha hooks pre-task --description "[task]"
npx claude-flow@alpha hooks session-restore --session-id "swarm-[id]"
```

**2️⃣ DURING Work:**
```bash
npx claude-flow@alpha hooks post-edit --file "[file]" --memory-key "swarm/[agent]/[step]"
npx claude-flow@alpha hooks notify --message "[what was done]"
```

**3️⃣ AFTER Work:**
```bash
npx claude-flow@alpha hooks post-task --task-id "[task]"
npx claude-flow@alpha hooks session-end --export-metrics true
```

## 🎯 Concurrent Execution Examples

### ✅ CORRECT WORKFLOW: MCP Coordinates, Claude Code Executes

```javascript
// Step 1: MCP tools set up coordination (optional, for complex tasks)
[Single Message - Coordination Setup]:
  mcp__claude-flow__swarm_init { topology: "mesh", maxAgents: 6 }
  mcp__claude-flow__agent_spawn { type: "researcher" }
  mcp__claude-flow__agent_spawn { type: "coder" }
  mcp__claude-flow__agent_spawn { type: "tester" }

// Step 2: Claude Code Task tool spawns ACTUAL agents that do the work
[Single Message - Parallel Agent Execution]:
  // Claude Code's Task tool spawns real agents concurrently
  Task("Research agent", "Analyze API requirements and best practices. Check memory for prior decisions.", "researcher")
  Task("Coder agent", "Implement REST endpoints with authentication. Coordinate via hooks.", "coder")
  Task("Database agent", "Design and implement database schema. Store decisions in memory.", "code-analyzer")
  Task("Tester agent", "Create comprehensive test suite with 90% coverage.", "tester")
  Task("Reviewer agent", "Review code quality and security. Document findings.", "reviewer")
  
  // Batch ALL todos in ONE call
  TodoWrite { todos: [
    {id: "1", content: "Research API patterns", status: "in_progress", priority: "high"},
    {id: "2", content: "Design database schema", status: "in_progress", priority: "high"},
    {id: "3", content: "Implement authentication", status: "pending", priority: "high"},
    {id: "4", content: "Build REST endpoints", status: "pending", priority: "high"},
    {id: "5", content: "Write unit tests", status: "pending", priority: "medium"},
    {id: "6", content: "Integration tests", status: "pending", priority: "medium"},
    {id: "7", content: "API documentation", status: "pending", priority: "low"},
    {id: "8", content: "Performance optimization", status: "pending", priority: "low"}
  ]}
  
  // Parallel file operations
  Bash "mkdir -p app/{src,tests,docs,config}"
  Write "app/package.json"
  Write "app/src/server.js"
  Write "app/tests/server.test.js"
  Write "app/docs/API.md"
```

### ❌ WRONG (Multiple Messages):
```javascript
Message 1: mcp__claude-flow__swarm_init
Message 2: Task("agent 1")
Message 3: TodoWrite { todos: [single todo] }
Message 4: Write "file.js"
// This breaks parallel coordination!
```

## Performance Benefits

- **84.8% SWE-Bench solve rate**
- **32.3% token reduction**
- **2.8-4.4x speed improvement**
- **27+ neural models**

## Hooks Integration

### Pre-Operation
- Auto-assign agents by file type
- Validate commands for safety
- Prepare resources automatically
- Optimize topology by complexity
- Cache searches

### Post-Operation
- Auto-format code
- Train neural patterns
- Update memory
- Analyze performance
- Track token usage

### Session Management
- Generate summaries
- Persist state
- Track metrics
- Restore context
- Export workflows

## Advanced Features (v2.0.0)

- 🚀 Automatic Topology Selection
- ⚡ Parallel Execution (2.8-4.4x speed)
- 🧠 Neural Training
- 📊 Bottleneck Analysis
- 🤖 Smart Auto-Spawning
- 🛡️ Self-Healing Workflows
- 💾 Cross-Session Memory
- 🔗 GitHub Integration

## Integration Tips

1. Start with basic swarm init
2. Scale agents gradually
3. Use memory for context
4. Monitor progress regularly
5. Train patterns from success
6. Enable hooks automation
7. Use GitHub tools first

## Support

- Documentation: https://github.com/ruvnet/claude-flow
- Issues: https://github.com/ruvnet/claude-flow/issues
- Flow-Nexus Platform: https://flow-nexus.ruv.io (registration required for cloud features)

---

Remember: **Claude Flow coordinates, Claude Code creates!**

# important-instruction-reminders
Do what has been asked; nothing more, nothing less.
NEVER create files unless they're absolutely necessary for achieving your goal.
ALWAYS prefer editing an existing file to creating a new one.
NEVER proactively create documentation files (*.md) or README files. Only create documentation files if explicitly requested by the User.
Never save working files, text/mds and tests to the root folder.

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DotCompute is a production-ready, Native AOT-compatible universal compute framework for .NET 9+ with comprehensive GPU acceleration, Ring Kernels, source generators, and IDE integration.

**Current Version**: 0.2.0-alpha (Ready for Release - November 2025)
**Status**: Production-ready for deployment
**Documentation**: https://mivertowski.github.io/DotCompute/ (3,237 pages, automated via GitHub Actions)
**Repository**: https://github.com/mivertowski/DotCompute

**Measured Performance**:
- CPU SIMD: 3.7x faster (Vector Add: 2.14ms → 0.58ms)
- CUDA GPU: 21-92x speedup (benchmarked on RTX 2000 Ada, CC 8.9)
- Memory: 90% allocation reduction through pooling
- Startup: Sub-10ms with Native AOT

**Production-Ready Features**:
- ✅ CPU Backend (SIMD: AVX2/AVX512/NEON)
- ✅ CUDA Backend (P2P, NCCL, Ring Kernels, Compute Capability 5.0-8.9)
- ✅ OpenCL Backend (NVIDIA, AMD, Intel, ARM Mali, Qualcomm Adreno)
- ✅ Ring Kernel System (Persistent GPU computation)
- ✅ Source Generators ([Kernel] attribute, automatic optimization)
- ✅ Roslyn Analyzers (12 diagnostic rules, 5 automated fixes)
- ✅ Cross-Backend Debugging (CPU vs GPU validation)
- ✅ Adaptive Optimization (ML-powered backend selection)
- 🚧 LINQ Extensions (Foundation only - 5% complete, under active development)
- ✅ DI Integration (Microsoft.Extensions.DependencyInjection)
- ✅ Comprehensive Documentation (27 guides, API reference)

IMPORTANT: Quality first! No shortcuts! No backward compatibility needed. No rush. No compromise. Always production grade code.

## Essential Build Commands

```bash
# Build the solution
dotnet build DotCompute.sln --configuration Release

# Run all tests
dotnet test DotCompute.sln --configuration Release

# Run specific test category
dotnet test --filter "Category=Unit" --configuration Release
dotnet test --filter "Category=Hardware" --configuration Release  # Requires NVIDIA GPU

# Run CUDA-specific tests
dotnet test tests/Hardware/DotCompute.Hardware.Cuda.Tests/DotCompute.Hardware.Cuda.Tests.csproj

# Clean build artifacts
dotnet clean DotCompute.sln

# Generate code coverage
./scripts/run-coverage.sh

# Run hardware tests with detailed output
./scripts/run-hardware-tests.sh
```

## Critical System Information

### CUDA Configuration
- **System has CUDA 13.0 installed** at `/usr/local/cuda` (symlink to `/usr/local/cuda-13.0`)
- **GPU**: NVIDIA RTX 2000 Ada Generation (Compute Capability 8.9)
- **Driver Version**: 581.15
- **All CUDA capability detection must use**: `DotCompute.Backends.CUDA.Configuration.CudaCapabilityManager`

### Architecture Overview

The codebase follows a **comprehensive, production-grade architecture** with four integrated layers:

```
DotCompute/
├── src/
│   ├── Core/                    # Core abstractions and runtime
│   │   ├── DotCompute.Core/     # Main runtime, orchestration, optimization
│   │   │   ├── Debugging/       # ✅ Cross-backend validation system
│   │   │   ├── Optimization/    # ✅ Adaptive backend selection with ML
│   │   │   ├── Telemetry/       # ✅ Performance profiling infrastructure
│   │   │   └── Pipelines/       # ✅ Execution pipeline management
│   │   ├── DotCompute.Abstractions/  # Interface definitions
│   │   │   ├── Debugging/       # ✅ IKernelDebugService interface
│   │   │   └── Interfaces/      # ✅ IComputeOrchestrator, IKernel
│   │   └── DotCompute.Memory/   # Unified memory with pooling
│   ├── Backends/                # Compute backend implementations
│   │   ├── DotCompute.Backends.CPU/   # ✅ Production: AVX2/AVX512 SIMD
│   │   ├── DotCompute.Backends.CUDA/  # ✅ Production: NVIDIA GPU (CC 5.0+)
│   │   ├── DotCompute.Backends.Metal/ # 🚧 Foundation: Native API, needs MSL compilation
│   │   └── DotCompute.Backends.ROCm/  # ❌ Placeholder
│   ├── Extensions/              # Extension libraries
│   │   ├── DotCompute.Algorithms/     # Algorithm implementations
│   │   └── DotCompute.Linq/           # LINQ provider with kernels
│   └── Runtime/                 # Runtime services and code generation
│       ├── DotCompute.Runtime/  # ✅ Service orchestration, discovery
│       │   └── Services/        # ✅ KernelExecutionService, discovery
│       ├── DotCompute.Generators/     # ✅ Source generators & analyzers
│       │   ├── Analyzers/       # ✅ 12 diagnostic rules (DC001-DC012)
│       │   ├── CodeFixes/       # ✅ 5 automated IDE fixes
│       │   ├── Kernel/          # ✅ Kernel source generation
│       │   └── Examples/        # ✅ Analyzer demonstrations
│       └── DotCompute.Plugins/  # Plugin system with hot-reload
└── tests/
    ├── Unit/                    # Unit tests for components
    ├── Integration/             # Integration tests
    ├── Hardware/                # Hardware-specific tests (GPU)
    └── Shared/                  # Shared test utilities
```

### Key Architectural Components

1. **Kernel System** (`src/Core/DotCompute.Core/Kernels/`)
   - `KernelDefinition`: Metadata for compute kernels
   - `IKernelCompiler`: Interface for backend-specific compilation
   - `ICompiledKernel`: Represents compiled kernel ready for execution

2. **Memory Management** (`src/Core/DotCompute.Memory/`)
   - `UnifiedBuffer<T>`: Zero-copy memory buffer across devices
   - `MemoryPool`: Pooled memory allocation (90% reduction)
   - `P2PManager`: Peer-to-peer GPU memory transfers

3. **Backend Abstraction** (`src/Core/DotCompute.Abstractions/`)
   - `IAccelerator`: Common interface for all compute backends
   - `IComputeService`: High-level compute orchestration
   - `CompilationOptions`: Kernel compilation settings

4. **CUDA Backend** (`src/Backends/DotCompute.Backends.CUDA/`)
   - `CudaCapabilityManager`: Centralized compute capability detection
   - `CudaKernelCompiler`: NVRTC-based kernel compilation
   - `CudaAccelerator`: CUDA-specific accelerator implementation
   - `CudaRuntime`: P/Invoke wrappers for CUDA API

5. **CPU Backend** (`src/Backends/DotCompute.Backends.CPU/`)
   - `SimdProcessor`: SIMD vectorization (AVX512/AVX2/NEON)
   - `CpuAccelerator`: Multi-threaded CPU execution
   - `VectorizedOperations`: Hardware-accelerated operations

6. **Metal Backend** (`src/Backends/DotCompute.Backends.Metal/`)
   - `MetalAccelerator`: Metal-specific accelerator with device management
   - `MetalNative`: P/Invoke bindings to native Metal framework
   - `MetalMemoryManager`: Unified memory support for Apple Silicon
   - `MetalExecutionEngine`: Command buffer and queue management
   - Native library: `libDotComputeMetal.dylib` (Objective-C++ integration)

7. **Runtime Integration** (`src/Core/DotCompute.Core/Interfaces/`)
   - `IComputeOrchestrator`: Universal kernel execution interface
   - `KernelExecutionService`: Runtime orchestration service
   - `GeneratedKernelDiscoveryService`: Automatic kernel registration

8. **Debugging System** (`src/Core/DotCompute.Core/Debugging/`)
   - `IKernelDebugService`: Cross-backend validation interface
   - `KernelDebugService`: Production debugging implementation
   - `DebugIntegratedOrchestrator`: Transparent debug wrapper
   - Multiple profiles: Development, Testing, Production

9. **Optimization Engine** (`src/Core/DotCompute.Core/Optimization/`)
   - `AdaptiveBackendSelector`: ML-powered backend selection
   - `PerformanceOptimizedOrchestrator`: Performance-driven execution
   - `WorkloadCharacteristics`: Workload pattern analysis
   - Multiple optimization strategies: Conservative, Balanced, Aggressive

10. **Source Generators & Analyzers** (`src/Runtime/DotCompute.Generators/`)
   - `KernelSourceGenerator`: Generates kernel wrappers from [Kernel] attributes
   - `DotComputeKernelAnalyzer`: 12 diagnostic rules for kernel quality
   - `KernelCodeFixProvider`: 5 automated code fixes
   - Real-time IDE integration with Visual Studio and VS Code

## Development Guidelines

### Native AOT Compatibility
- All code must be Native AOT compatible
- No runtime code generation
- Use source generators for compile-time code generation
- Avoid reflection except where marked with proper attributes

### Memory Safety
- Always use `UnifiedBuffer<T>` for cross-device memory
- Implement proper `IDisposable` patterns
- Use memory pooling for frequent allocations
- Validate buffer bounds in kernel code

### Testing Requirements
- Unit tests required for all public APIs
- Hardware tests must check for device availability
- Use `[SkippableFact]` for tests requiring specific hardware
- Maintain ~75% code coverage target

### CUDA Development
- Always use `CudaCapabilityManager.GetTargetComputeCapability()` for capability detection
- Support dynamic CUDA version detection (don't hardcode paths)
- Use CUBIN compilation for modern GPUs (compute capability >= 7.0)
- Handle both PTX and CUBIN compilation paths

## Common Development Tasks

### Adding a New Kernel (Modern Approach v0.2.0+)
1. Add `[Kernel]` attribute to static method in C#
2. Use `Kernel.ThreadId.X/Y/Z` for threading model
3. Ensure bounds checking with `if (idx < length)`
4. Source generator creates wrapper automatically
5. IDE analyzers provide real-time feedback
6. System automatically optimizes for CPU/GPU

### Adding a New Kernel (Legacy Approach)
1. Define kernel in `KernelDefinition` format
2. Implement CPU version using SIMD intrinsics
3. Implement CUDA version in CUDA C
4. Add unit tests in appropriate test project
5. Add hardware tests if GPU-specific

### Debugging CUDA Issues
1. Check compute capability: `CudaCapabilityManager.GetTargetComputeCapability()`
2. Verify CUDA installation: `/usr/local/cuda/bin/nvcc --version`
3. Check kernel compilation logs in `CudaKernelCompiler`
4. Use `cuda-memcheck` for memory errors
5. Enable debug compilation with `GenerateDebugInfo = true`

### Performance Optimization
1. Profile with BenchmarkDotNet (`benchmarks/` directory)
2. Use SIMD intrinsics for CPU backend
3. Optimize memory access patterns for GPU
4. Leverage memory pooling for allocations
5. Use P2P transfers for multi-GPU scenarios

## Important Files and Locations

- **Solution File**: `DotCompute.sln`
- **Build Configuration**: `Directory.Build.props`, `Directory.Build.targets`
- **Package Versions**: `Directory.Packages.props`
- **Test Scripts**: `scripts/` directory (various shell scripts)
- **Documentation**: `docs/` directory (architecture, API, guides)
- **Benchmarks**: `benchmarks/` directory
- **Examples**: `samples/` directory

## Known Issues and Limitations

1. **LINQ Extensions**: Foundation only (5% complete) - Expression compilation, GPU codegen, and optimization planned (24-week roadmap in docs/LINQ_IMPLEMENTATION_PLAN.md). Current implementation delegates to standard LINQ with no GPU acceleration.
2. **Metal Backend**: Foundation implemented with native API, MSL compilation incomplete
3. **ROCm Backend**: Placeholder, not implemented
4. **CUDA Tests**: Some tests may fail with "device kernel image is invalid" on CUDA 13
5. **Hardware Tests**: Require NVIDIA GPU with Compute Capability 5.0+ (or Metal on macOS)
6. **Cross-Platform GPU**: NVIDIA GPUs fully supported, Metal in development

## Production-Ready Components (v0.2.0-alpha)

✅ **Fully Production Ready:**

**Backend Infrastructure**:
- CPU Backend with SIMD vectorization (measured 3.7x speedup: 2.14ms → 0.58ms)
- CUDA Backend with complete GPU support (21-92x speedup, CC 5.0-8.9, RTX tested)
- OpenCL Backend cross-platform GPU (NVIDIA, AMD, Intel, ARM Mali, Qualcomm Adreno)
- Memory Management with pooling and P2P (90% allocation reduction)
- Native AOT compilation support (sub-10ms startup times)

**Ring Kernel System** (NEW in v0.2.0):
- Persistent kernel programming model for GPU-resident actor systems
- Message passing strategies: SharedMemory, AtomicQueue, P2P, NCCL
- Execution modes: Persistent (continuous), EventDriven (on-demand)
- Domain optimizations: GraphAnalytics, SpatialSimulation, ActorModel
- Cross-backend support: CPU (simulation), CUDA, OpenCL, Metal

**Developer Tooling**:
- Source Generator with [Kernel] attribute support (automatic optimization)
- Roslyn Analyzers with 12 diagnostic rules (DC001-DC012)
- 5 automated code fixes in Visual Studio and VS Code
- Real-time IDE feedback and performance suggestions
- Cross-Backend Debugging with CPU vs GPU validation
- Adaptive Backend Selection with ML-based optimization
- Performance Profiling with hardware counters and telemetry

**Runtime & Integration**:
- Runtime Orchestration with Microsoft.Extensions.DependencyInjection
- Plugin System with hot-reload capability
- LINQ Extensions (Foundation - 3 files, ~200 lines, basic queryable wrapper)
  - ⚠️ Note: Full implementation planned (expression compilation, GPU codegen, optimization)
  - ⚠️ Current: Delegates to standard LINQ, no GPU acceleration yet
  - ⚠️ See docs/LINQ_IMPLEMENTATION_PLAN.md for 24-week roadmap
- Automatic kernel discovery and registration

**Documentation & Deployment**:
- GitHub Pages documentation site (3,237 HTML pages, 113 MB)
- Automated DocFX deployment via GitHub Actions
- 27 comprehensive guides (architecture, examples, reference)
- Complete API reference for all 12 packages
- Professional package READMEs with benchmarks

🚧 **In Development:**
- Metal backend (foundation complete, MSL compilation 60% done)
- Algorithm libraries (expanding operation coverage)
- ROCm backend (AMD GPU support, placeholder stage)

## Critical Implementation Details

### CUDA Kernel Compilation Flow
1. Source code → NVRTC compilation → PTX/CUBIN
2. Architecture selection via `CudaCapabilityManager`
3. Caching in `CudaKernelCache` for performance
4. Module loading with `cuModuleLoadDataEx`

### Memory Management Strategy
1. Unified buffers abstract device-specific memory
2. Memory pool reduces allocations by 90%+
3. P2P manager handles GPU-to-GPU transfers
4. Pinned memory for CPU-GPU transfers

### Backend Selection Priority
1. CUDA (if NVIDIA GPU available)
2. CPU with SIMD (always available)
3. Future: Metal (macOS), ROCm (AMD GPU)

## Environment Requirements

- **.NET 9.0 SDK** or later
- **C# 13** language features
- **Visual Studio 2022 17.8+** or VS Code
- **CUDA Toolkit 12.0+** for GPU support
- **cmake** for native components
- **NVIDIA GPU** with Compute Capability 5.0+ for CUDA tests

## New Features in v0.2.0-alpha

### Phase 1: Generator ↔ Runtime Integration
- `IComputeOrchestrator` interface for universal kernel execution
- `KernelExecutionService` for runtime orchestration
- `GeneratedKernelDiscoveryService` for automatic kernel registration
- Dependency injection integration with Microsoft.Extensions.DependencyInjection

### Phase 2: Roslyn Analyzer Integration
- 12 diagnostic rules (DC001-DC012) for kernel code quality
- 5 automated code fixes in IDE
- Real-time feedback in Visual Studio and VS Code
- Comprehensive examples in `AnalyzerDemo.cs`

### Phase 3: Enhanced Debugging Service
- `IKernelDebugService` with 8 debugging methods
- Cross-backend validation (CPU vs GPU)
- Performance analysis and profiling
- Determinism testing and memory pattern analysis
- Multiple debugging profiles (Development, Testing, Production)

### Phase 4: Production Optimizations
- `AdaptiveBackendSelector` with machine learning capabilities
- `PerformanceOptimizedOrchestrator` for intelligent execution
- Workload pattern recognition and analysis
- Real-time performance monitoring
- Multiple optimization profiles (Conservative, Balanced, Aggressive, ML-optimized)

### Phase 5: LINQ Extensions and Advanced Compute (⏳ PLANNED - 5% Complete)
**Current Status**: Foundation only (3 files, ~200 lines), delegates to standard LINQ
**Implementation Timeline**: 24 weeks (see docs/LINQ_IMPLEMENTATION_PLAN.md)

**Planned Features** (NOT YET IMPLEMENTED):
- **Expression Compilation Pipeline**: Direct LINQ-to-kernel compilation with multi-backend support
- **Reactive Extensions Integration**: GPU-accelerated streaming compute with backpressure handling
- **Advanced Optimization Strategies**: ML-based optimization, kernel fusion, memory optimization
- **GPU Kernel Generation**: Complete CUDA kernel generation from LINQ expressions
- **Comprehensive Testing**: 175 integration tests (exist but don't compile), performance benchmarks

**What Works Today**: Basic queryable wrapper (`AsComputeQueryable`, `ComputeSelect`, `ComputeWhere`) that delegates to standard LINQ

### Usage Examples

#### Modern Kernel Definition
```csharp
[Kernel]
public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
    {
        result[idx] = a[idx] + b[idx];
    }
}
```

#### Service Configuration
```csharp
services.AddDotComputeRuntime();
services.AddProductionOptimization();  // Intelligent backend selection
services.AddProductionDebugging();     // Cross-backend validation
```

### Key Benefits
- **Universal Execution**: Write once in C#, run optimally on CPU or GPU
- **IDE Integration**: Real-time feedback and automated improvements
- **Production Ready**: Comprehensive debugging and optimization
- **Intelligent**: ML-powered backend selection learns from execution patterns
- **Observable**: Detailed performance metrics and profiling

### LINQ Extensions Architecture (PLANNED)

**⚠️ IMPORTANT**: The following describes the PLANNED architecture. Current implementation is 5% complete (3 files, ~200 lines).

The DotCompute.Linq module will provide:

#### Expression Compilation Pipeline (PLANNED - Phase 3-4)
- Expression tree analysis with type inference and dependency detection
- Multi-backend code generation (CPU SIMD, CUDA GPU)
- Kernel caching with TTL and invalidation
- Parallel compilation support for batch operations

#### Reactive Extensions Integration (PLANNED - Phase 7)
- Full Rx.NET compatibility for streaming compute
- Adaptive batching for GPU efficiency
- Windowing operations (tumbling, sliding, time-based)
- Backpressure handling strategies

#### Advanced Optimization Strategies
- Machine learning-based backend selection
- Kernel fusion to reduce memory transfers
- Memory access pattern optimization
- Dynamic parallelization with load balancing
- Cost-based execution planning

#### GPU Kernel Generation
- 8 specialized kernel templates (map, reduce, filter, join, etc.)
- Compute capability 5.0-8.9 support
- Warp-level primitives and shared memory optimization
- Memory pooling with 90% allocation reduction

### Testing Infrastructure
- 50+ integration tests covering all major components
- Performance benchmarks validating 3.7x+ speedup claims
- Thread safety validation with concurrent scenarios
- Hardware mocking for CI/CD environments