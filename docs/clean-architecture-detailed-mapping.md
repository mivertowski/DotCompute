# Clean Architecture Detailed Mapping

## Domain Model Analysis

### Identified Domain Entities

#### 1. Kernel (Core Entity)
**Current Location**: `DotCompute.Abstractions.Kernels.KernelDefinition`
**Target Location**: `src/Core/DotCompute.Domain/Entities/Kernel.cs`

```csharp
// Domain Entity: Kernel
public class Kernel : Entity<KernelId>
{
    public KernelName Name { get; private set; }
    public KernelSource Source { get; private set; }
    public EntryPoint EntryPoint { get; private set; }
    public KernelLanguage Language { get; private set; }
    public KernelMetadata Metadata { get; private set; }

    // Domain behaviors
    public bool CanExecuteOn(ComputeDevice device) { }
    public CompilationRequirements GetCompilationRequirements() { }
    public void UpdateSource(KernelSource newSource) { }
}
```

#### 2. ComputeDevice (Core Entity)
**Current Location**: `DotCompute.Abstractions.AcceleratorInfo`
**Target Location**: `src/Core/DotCompute.Domain/Entities/ComputeDevice.cs`

```csharp
// Domain Entity: ComputeDevice
public class ComputeDevice : Entity<DeviceId>
{
    public DeviceName Name { get; private set; }
    public DeviceType Type { get; private set; }
    public ComputeCapability Capability { get; private set; }
    public MemoryCapacity Memory { get; private set; }
    public DeviceStatus Status { get; private set; }

    // Domain behaviors
    public bool SupportsKernel(Kernel kernel) { }
    public ExecutionEstimate EstimateExecution(Kernel kernel) { }
    public void UpdateStatus(DeviceStatus status) { }
}
```

#### 3. ComputeSession (Aggregate Root)
**Current Location**: Distributed across multiple services
**Target Location**: `src/Core/DotCompute.Domain/Aggregates/ComputeSession.cs`

```csharp
// Aggregate Root: ComputeSession
public class ComputeSession : AggregateRoot<SessionId>
{
    private readonly List<ExecutionContext> _executions;
    private readonly List<MemoryAllocation> _allocations;

    public ComputeDevice PrimaryDevice { get; private set; }
    public SessionStatus Status { get; private set; }
    public IReadOnlyList<ExecutionContext> Executions => _executions.AsReadOnly();

    // Domain behaviors
    public ExecutionContext ScheduleKernel(Kernel kernel, KernelArguments args) { }
    public void AllocateMemory(MemorySize size, MemoryType type) { }
    public void CompleteSession() { }
}
```

### Identified Value Objects

#### 1. KernelId
```csharp
public record KernelId(string Value) : IValueObject
{
    public static KernelId Create(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
}
```

#### 2. DeviceId
```csharp
public record DeviceId(string Value) : IValueObject
{
    public static DeviceId Create(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
}
```

#### 3. ComputeCapability
```csharp
public record ComputeCapability(int Major, int Minor) : IValueObject
{
    public Version ToVersion() => new(Major, Minor);
    public bool IsAtLeast(ComputeCapability other) => CompareTo(other) >= 0;
}
```

#### 4. MemorySize
```csharp
public record MemorySize(long Bytes) : IValueObject
{
    public static MemorySize FromKB(long kb) => new(kb * 1024);
    public static MemorySize FromMB(long mb) => new(mb * 1024 * 1024);
    public static MemorySize FromGB(long gb) => new(gb * 1024 * 1024 * 1024);

    public long ToKB() => Bytes / 1024;
    public long ToMB() => Bytes / (1024 * 1024);
    public long ToGB() => Bytes / (1024 * 1024 * 1024);
}
```

## Layer-by-Layer Migration Mapping

### Core Layer (Domain & Abstractions)

#### Domain Entities
```
FROM: src/Core/DotCompute.Abstractions/Kernels/KernelDefinition.cs
TO:   src/Core/DotCompute.Domain/Entities/Kernel.cs

FROM: src/Core/DotCompute.Abstractions/AcceleratorInfo (class in IAccelerator.cs)
TO:   src/Core/DotCompute.Domain/Entities/ComputeDevice.cs

FROM: Distributed execution logic
TO:   src/Core/DotCompute.Domain/Aggregates/ComputeSession.cs

FROM: src/Core/DotCompute.Memory/*
TO:   src/Core/DotCompute.Domain/Entities/MemoryBuffer.cs
```

#### Core Interfaces
```
FROM: src/Core/DotCompute.Abstractions/Interfaces/IAccelerator.cs
TO:   src/Core/DotCompute.Abstractions/Repositories/IDeviceRepository.cs

FROM: src/Core/DotCompute.Abstractions/Interfaces/IComputeOrchestrator.cs
TO:   src/Core/DotCompute.Abstractions/Services/IComputeService.cs

FROM: src/Core/DotCompute.Abstractions/Interfaces/Kernels/ICompiledKernel.cs
TO:   src/Core/DotCompute.Abstractions/Services/IKernelCompilationService.cs
```

#### Domain Services
```
FROM: src/Core/DotCompute.Core/Optimization/AdaptiveBackendSelector.cs
TO:   src/Core/DotCompute.Core/Services/DeviceSelectionService.cs

FROM: src/Core/DotCompute.Core/Debugging/Services/KernelDebugService.cs
TO:   src/Core/DotCompute.Core/Services/KernelValidationService.cs
```

### Application Layer

#### Use Cases
```
NEW:  src/Application/DotCompute.Application/UseCases/CompileKernelUseCase.cs
NEW:  src/Application/DotCompute.Application/UseCases/ExecuteKernelUseCase.cs
NEW:  src/Application/DotCompute.Application/UseCases/ManageDevicesUseCase.cs
NEW:  src/Application/DotCompute.Application/UseCases/OptimizePerformanceUseCase.cs
```

#### Application Services
```
FROM: src/Runtime/DotCompute.Runtime/Services/KernelExecutionService.cs
TO:   src/Application/DotCompute.Application/Services/KernelExecutionApplicationService.cs

FROM: src/Runtime/DotCompute.Runtime/Services/GeneratedKernelDiscoveryService.cs
TO:   src/Application/DotCompute.Application/Services/KernelDiscoveryApplicationService.cs

FROM: src/Extensions/DotCompute.Algorithms/Management/Services/AlgorithmPluginOrchestrator.cs
TO:   src/Application/DotCompute.Algorithms/Services/AlgorithmOrchestrationService.cs
```

#### DTOs and Mappers
```
NEW:  src/Application/DotCompute.Application/DTOs/KernelExecutionRequest.cs
NEW:  src/Application/DotCompute.Application/DTOs/DeviceInfo.cs
NEW:  src/Application/DotCompute.Application/DTOs/ExecutionResult.cs
NEW:  src/Application/DotCompute.Application/Mappers/KernelMapper.cs
NEW:  src/Application/DotCompute.Application/Mappers/DeviceMapper.cs
```

### Infrastructure Layer

#### Backend Implementations
```
FROM: src/Backends/DotCompute.Backends.CPU/*
TO:   src/Infrastructure/DotCompute.Infrastructure.Backends/CPU/*

FROM: src/Backends/DotCompute.Backends.CUDA/*
TO:   src/Infrastructure/DotCompute.Infrastructure.Backends/CUDA/*

FROM: src/Backends/DotCompute.Backends.Metal/*
TO:   src/Infrastructure/DotCompute.Infrastructure.Backends/Metal/*

FROM: src/Backends/DotCompute.Backends.OpenCL/*
TO:   src/Infrastructure/DotCompute.Infrastructure.Backends/OpenCL/*
```

#### Infrastructure Services
```
FROM: src/Core/DotCompute.Memory/*
TO:   src/Infrastructure/DotCompute.Infrastructure.Memory/*

FROM: src/Core/DotCompute.Core/Telemetry/*
TO:   src/Infrastructure/DotCompute.Infrastructure.Telemetry/*

FROM: src/Core/DotCompute.Core/Logging/*
TO:   src/Infrastructure/DotCompute.Infrastructure.Logging/*

FROM: src/Core/DotCompute.Core/Security/*
TO:   src/Infrastructure/DotCompute.Infrastructure.Security/*
```

#### Repository Implementations
```
NEW:  src/Infrastructure/DotCompute.Infrastructure.Persistence/Repositories/KernelRepository.cs
NEW:  src/Infrastructure/DotCompute.Infrastructure.Persistence/Repositories/DeviceRepository.cs
NEW:  src/Infrastructure/DotCompute.Infrastructure.Persistence/Repositories/SessionRepository.cs
```

### Presentation Layer

#### Source Generators
```
FROM: src/Runtime/DotCompute.Generators/*
TO:   src/Presentation/DotCompute.Presentation.Generators/*
```

#### Plugin System
```
FROM: src/Runtime/DotCompute.Plugins/*
TO:   src/Presentation/DotCompute.Presentation.Plugins/*
```

#### API Controllers (New)
```
NEW:  src/Presentation/DotCompute.Presentation.API/Controllers/KernelController.cs
NEW:  src/Presentation/DotCompute.Presentation.API/Controllers/DeviceController.cs
NEW:  src/Presentation/DotCompute.Presentation.API/Controllers/ExecutionController.cs
```

## Detailed File Migration Plan

### Phase 1: Domain Foundation

#### Step 1.1: Create Domain Entities
```bash
# Create new domain project structure
mkdir -p src/Core/DotCompute.Domain/{Entities,ValueObjects,Aggregates,Specifications,Events}

# Create base domain types
touch src/Core/DotCompute.Domain/Common/Entity.cs
touch src/Core/DotCompute.Domain/Common/ValueObject.cs
touch src/Core/DotCompute.Domain/Common/AggregateRoot.cs

# Create domain entities
touch src/Core/DotCompute.Domain/Entities/Kernel.cs
touch src/Core/DotCompute.Domain/Entities/ComputeDevice.cs
touch src/Core/DotCompute.Domain/Entities/MemoryBuffer.cs
touch src/Core/DotCompute.Domain/Entities/ExecutionContext.cs

# Create value objects
touch src/Core/DotCompute.Domain/ValueObjects/KernelId.cs
touch src/Core/DotCompute.Domain/ValueObjects/DeviceId.cs
touch src/Core/DotCompute.Domain/ValueObjects/SessionId.cs
touch src/Core/DotCompute.Domain/ValueObjects/ComputeCapability.cs
touch src/Core/DotCompute.Domain/ValueObjects/MemorySize.cs

# Create aggregates
touch src/Core/DotCompute.Domain/Aggregates/ComputeSession.cs
touch src/Core/DotCompute.Domain/Aggregates/DeviceCluster.cs
```

#### Step 1.2: Transform Existing Classes
```csharp
// Example transformation: KernelDefinition → Kernel entity
// FROM: DotCompute.Abstractions.Kernels.KernelDefinition
// TO: DotCompute.Domain.Entities.Kernel

public class Kernel : Entity<KernelId>
{
    public KernelName Name { get; private set; }
    public KernelSource Source { get; private set; }
    public EntryPoint EntryPoint { get; private set; }
    public KernelLanguage Language { get; private set; }
    public KernelMetadata Metadata { get; private set; }

    private Kernel() { } // For persistence

    public static Kernel Create(
        KernelName name,
        KernelSource source,
        EntryPoint entryPoint,
        KernelLanguage language = KernelLanguage.CSharp)
    {
        var kernel = new Kernel
        {
            Id = KernelId.NewId(),
            Name = name,
            Source = source,
            EntryPoint = entryPoint,
            Language = language,
            Metadata = KernelMetadata.Empty()
        };

        // Domain event
        kernel.RaiseDomainEvent(new KernelCreatedEvent(kernel.Id, kernel.Name));

        return kernel;
    }

    public void UpdateSource(KernelSource newSource)
    {
        if (Source.Equals(newSource)) return;

        Source = newSource;
        RaiseDomainEvent(new KernelSourceUpdatedEvent(Id, newSource));
    }

    public bool CanExecuteOn(ComputeDevice device)
    {
        return device.SupportsLanguage(Language) &&
               device.HasSufficientMemory(GetMemoryRequirements());
    }
}
```

### Phase 2: Interface Reorganization

#### Step 2.1: Repository Interfaces
```csharp
// NEW: src/Core/DotCompute.Abstractions/Repositories/IKernelRepository.cs
public interface IKernelRepository
{
    Task<Kernel?> GetByIdAsync(KernelId id, CancellationToken cancellationToken = default);
    Task<Kernel?> GetByNameAsync(KernelName name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Kernel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(Kernel kernel, CancellationToken cancellationToken = default);
    Task DeleteAsync(KernelId id, CancellationToken cancellationToken = default);
}

// NEW: src/Core/DotCompute.Abstractions/Repositories/IDeviceRepository.cs
public interface IDeviceRepository
{
    Task<ComputeDevice?> GetByIdAsync(DeviceId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ComputeDevice>> GetAvailableAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ComputeDevice>> GetByTypeAsync(DeviceType type, CancellationToken cancellationToken = default);
    Task SaveAsync(ComputeDevice device, CancellationToken cancellationToken = default);
}
```

#### Step 2.2: Domain Service Interfaces
```csharp
// NEW: src/Core/DotCompute.Abstractions/Services/IKernelCompilationService.cs
public interface IKernelCompilationService
{
    Task<CompilationResult> CompileAsync(Kernel kernel, ComputeDevice device, CancellationToken cancellationToken = default);
    Task<bool> CanCompileAsync(Kernel kernel, ComputeDevice device, CancellationToken cancellationToken = default);
    Task<CompilationDiagnostics> ValidateAsync(Kernel kernel, CancellationToken cancellationToken = default);
}

// NEW: src/Core/DotCompute.Abstractions/Services/IDeviceSelectionService.cs
public interface IDeviceSelectionService
{
    Task<ComputeDevice?> SelectOptimalDeviceAsync(Kernel kernel, IReadOnlyList<ComputeDevice> availableDevices);
    Task<ExecutionEstimate> EstimateExecutionAsync(Kernel kernel, ComputeDevice device);
    Task<DeviceRanking> RankDevicesAsync(Kernel kernel, IReadOnlyList<ComputeDevice> devices);
}
```

### Phase 3: Application Layer Creation

#### Step 3.1: Use Case Implementation
```csharp
// NEW: src/Application/DotCompute.Application/UseCases/ExecuteKernelUseCase.cs
public class ExecuteKernelUseCase
{
    private readonly IKernelRepository _kernelRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IKernelCompilationService _compilationService;
    private readonly IDeviceSelectionService _deviceSelectionService;

    public async Task<ExecutionResult> ExecuteAsync(ExecuteKernelRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Get kernel by name
        var kernel = await _kernelRepository.GetByNameAsync(request.KernelName, cancellationToken);
        if (kernel == null)
            return ExecutionResult.Failed($"Kernel '{request.KernelName}' not found");

        // 2. Get available devices
        var devices = await _deviceRepository.GetAvailableAsync(cancellationToken);
        if (!devices.Any())
            return ExecutionResult.Failed("No compute devices available");

        // 3. Select optimal device
        var selectedDevice = await _deviceSelectionService.SelectOptimalDeviceAsync(kernel, devices);
        if (selectedDevice == null)
            return ExecutionResult.Failed("No suitable device found for kernel");

        // 4. Compile kernel if needed
        var compilationResult = await _compilationService.CompileAsync(kernel, selectedDevice, cancellationToken);
        if (!compilationResult.IsSuccess)
            return ExecutionResult.Failed($"Compilation failed: {compilationResult.Error}");

        // 5. Execute kernel
        var executionContext = ExecutionContext.Create(kernel, selectedDevice, request.Arguments);
        var result = await executionContext.ExecuteAsync(cancellationToken);

        return ExecutionResult.Success(result);
    }
}
```

#### Step 3.2: Application Services
```csharp
// NEW: src/Application/DotCompute.Application/Services/KernelManagementApplicationService.cs
public class KernelManagementApplicationService
{
    private readonly IKernelRepository _repository;
    private readonly IMapper _mapper;

    public async Task<KernelDto> CreateKernelAsync(CreateKernelRequest request, CancellationToken cancellationToken = default)
    {
        var kernel = Kernel.Create(
            KernelName.Create(request.Name),
            KernelSource.Create(request.Source),
            EntryPoint.Create(request.EntryPoint),
            request.Language);

        await _repository.SaveAsync(kernel, cancellationToken);

        return _mapper.Map<KernelDto>(kernel);
    }

    public async Task<IReadOnlyList<KernelDto>> GetAllKernelsAsync(CancellationToken cancellationToken = default)
    {
        var kernels = await _repository.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<KernelDto>>(kernels);
    }
}
```

### Phase 4: Infrastructure Implementation

#### Step 4.1: Repository Implementations
```csharp
// NEW: src/Infrastructure/DotCompute.Infrastructure.Persistence/Repositories/KernelRepository.cs
public class KernelRepository : IKernelRepository
{
    private readonly IKernelDbContext _context;

    public async Task<Kernel?> GetByIdAsync(KernelId id, CancellationToken cancellationToken = default)
    {
        return await _context.Kernels
            .FirstOrDefaultAsync(k => k.Id == id, cancellationToken);
    }

    public async Task<Kernel?> GetByNameAsync(KernelName name, CancellationToken cancellationToken = default)
    {
        return await _context.Kernels
            .FirstOrDefaultAsync(k => k.Name == name, cancellationToken);
    }

    public async Task SaveAsync(Kernel kernel, CancellationToken cancellationToken = default)
    {
        _context.Kernels.Update(kernel);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
```

#### Step 4.2: Backend Adapter Implementation
```csharp
// NEW: src/Infrastructure/DotCompute.Infrastructure.Backends/Adapters/CudaDeviceAdapter.cs
public class CudaDeviceAdapter : IDeviceAdapter
{
    public async Task<IReadOnlyList<ComputeDevice>> DiscoverDevicesAsync()
    {
        var cudaDevices = await CudaRuntime.GetDevicesAsync();
        return cudaDevices.Select(MapToComputeDevice).ToList();
    }

    private ComputeDevice MapToComputeDevice(CudaDevice cudaDevice)
    {
        return ComputeDevice.Create(
            DeviceId.Create(cudaDevice.Id),
            DeviceName.Create(cudaDevice.Name),
            DeviceType.GPU,
            ComputeCapability.Create(cudaDevice.ComputeCapability.Major, cudaDevice.ComputeCapability.Minor),
            MemorySize.FromBytes(cudaDevice.TotalMemory));
    }
}
```

## Dependency Injection Configuration

### Registration by Layer

```csharp
// Domain Layer Registration
services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

// Application Layer Registration
services.AddScoped<ExecuteKernelUseCase>();
services.AddScoped<CompileKernelUseCase>();
services.AddScoped<ManageDevicesUseCase>();
services.AddScoped<KernelManagementApplicationService>();

// Infrastructure Layer Registration
services.AddScoped<IKernelRepository, KernelRepository>();
services.AddScoped<IDeviceRepository, DeviceRepository>();
services.AddScoped<IKernelCompilationService, KernelCompilationService>();
services.AddScoped<IDeviceSelectionService, DeviceSelectionService>();

// Backend Adapters
services.AddScoped<IDeviceAdapter, CudaDeviceAdapter>();
services.AddScoped<IDeviceAdapter, CpuDeviceAdapter>();
services.AddScoped<IDeviceAdapter, MetalDeviceAdapter>();

// Presentation Layer Registration
services.AddControllers();
services.AddScoped<KernelController>();
services.AddScoped<DeviceController>();
```

## Validation and Testing Strategy

### Unit Testing by Layer

#### Domain Layer Tests
```csharp
// Test domain entities and business rules
[Test]
public void Kernel_CanExecuteOn_ShouldReturnTrue_WhenDeviceSupportsLanguage()
{
    // Arrange
    var kernel = Kernel.Create(
        KernelName.Create("TestKernel"),
        KernelSource.Create("void main() {}"),
        EntryPoint.Create("main"),
        KernelLanguage.CUDA);

    var device = ComputeDevice.Create(
        DeviceId.Create("GPU_0"),
        DeviceName.Create("RTX 4090"),
        DeviceType.GPU,
        ComputeCapability.Create(8, 9),
        MemorySize.FromGB(24));

    // Act
    var canExecute = kernel.CanExecuteOn(device);

    // Assert
    Assert.IsTrue(canExecute);
}
```

#### Application Layer Tests
```csharp
[Test]
public async Task ExecuteKernelUseCase_ShouldReturnSuccess_WhenKernelExists()
{
    // Arrange
    var mockKernelRepo = new Mock<IKernelRepository>();
    var mockDeviceRepo = new Mock<IDeviceRepository>();
    // ... setup mocks

    var useCase = new ExecuteKernelUseCase(mockKernelRepo.Object, ...);
    var request = new ExecuteKernelRequest("TestKernel", new object[] { 1, 2, 3 });

    // Act
    var result = await useCase.ExecuteAsync(request);

    // Assert
    Assert.IsTrue(result.IsSuccess);
}
```

#### Infrastructure Layer Tests
```csharp
[Test]
public async Task KernelRepository_GetByName_ShouldReturnKernel_WhenExists()
{
    // Integration test with real database
    using var context = CreateTestDbContext();
    var repository = new KernelRepository(context);

    // ... test repository implementation
}
```

This detailed mapping provides a concrete roadmap for implementing Clean Architecture in the DotCompute codebase while preserving all existing functionality and improving the overall structure.