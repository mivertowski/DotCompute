# Clean Architecture Implementation Guide

## Prerequisites

Before starting the migration, ensure:
- [ ] All tests are passing: `dotnet test DotCompute.sln`
- [ ] Git repository is clean: `git status`
- [ ] Create feature branch: `git checkout -b feature/clean-architecture-migration`
- [ ] Backup current state: `git tag pre-clean-architecture-migration`

## Phase 1: Foundation Setup (Week 1)

### Step 1.1: Create New Project Structure

```bash
# Navigate to source directory
cd /home/mivertowski/DotCompute/DotCompute/src

# Create Core layer directories
mkdir -p Core/DotCompute.Domain/{Entities,ValueObjects,Aggregates,Specifications,Events,Common}

# Create Application layer directories
mkdir -p Application/DotCompute.Application/{Services,UseCases,DTOs,Mappers,Handlers,Validators}
mkdir -p Application/DotCompute.Algorithms.Application/{Services,UseCases}
mkdir -p Application/DotCompute.Linq.Application/{Services,Processors}

# Create Infrastructure layer directories
mkdir -p Infrastructure/DotCompute.Infrastructure.Backends/{CPU,CUDA,Metal,OpenCL,Common}
mkdir -p Infrastructure/DotCompute.Infrastructure.Memory
mkdir -p Infrastructure/DotCompute.Infrastructure.Security
mkdir -p Infrastructure/DotCompute.Infrastructure.Logging
mkdir -p Infrastructure/DotCompute.Infrastructure.Telemetry
mkdir -p Infrastructure/DotCompute.Infrastructure.Persistence/{Repositories,DbContext}

# Create Presentation layer directories
mkdir -p Presentation/DotCompute.Presentation.API/{Controllers,Models}
mkdir -p Presentation/DotCompute.Presentation.CLI
mkdir -p Presentation/DotCompute.Presentation.Generators
mkdir -p Presentation/DotCompute.Presentation.Plugins
```

### Step 1.2: Create New Project Files

```bash
# Domain project
cat > Core/DotCompute.Domain/DotCompute.Domain.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableAOTAnalyzer>true</EnableAOTAnalyzer>
    <IsAotCompatible>true</IsAotCompatible>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <PackageId>DotCompute.Domain</PackageId>
    <Description>Domain entities and business logic for DotCompute</Description>
  </PropertyGroup>

  <!-- No external dependencies - pure domain -->
</Project>
EOF

# Application project
cat > Application/DotCompute.Application/DotCompute.Application.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableAOTAnalyzer>true</EnableAOTAnalyzer>
    <IsAotCompatible>true</IsAotCompatible>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <PackageId>DotCompute.Application</PackageId>
    <Description>Application services and use cases for DotCompute</Description>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../Core/DotCompute.Domain/DotCompute.Domain.csproj" />
    <ProjectReference Include="../../Core/DotCompute.Abstractions/DotCompute.Abstractions.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="9.0.9" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.9" />
  </ItemGroup>
</Project>
EOF

# Infrastructure Backends project
cat > Infrastructure/DotCompute.Infrastructure.Backends/DotCompute.Infrastructure.Backends.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableAOTAnalyzer>true</EnableAOTAnalyzer>
    <IsAotCompatible>true</IsAotCompatible>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <PackageId>DotCompute.Infrastructure.Backends</PackageId>
    <Description>Hardware backend implementations for DotCompute</Description>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../Application/DotCompute.Application/DotCompute.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.9" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.9" />
  </ItemGroup>
</Project>
EOF

# Presentation Generators project
cat > Presentation/DotCompute.Presentation.Generators/DotCompute.Presentation.Generators.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableAOTAnalyzer>true</EnableAOTAnalyzer>
    <IsAotCompatible>true</IsAotCompatible>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <PackageId>DotCompute.Presentation.Generators</PackageId>
    <Description>Source generators for DotCompute</Description>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../Application/DotCompute.Application/DotCompute.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.11.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.11.0" PrivateAssets="all" />
  </ItemGroup>
</Project>
EOF
```

### Step 1.3: Create Domain Foundation

```bash
# Create base domain types
cat > Core/DotCompute.Domain/Common/Entity.cs << 'EOF'
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Domain.Common;

/// <summary>
/// Base class for all domain entities with strongly typed IDs.
/// </summary>
/// <typeparam name="TId">The type of the entity identifier</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : IEquatable<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Gets or sets the unique identifier for this entity.
    /// </summary>
    public TId Id { get; protected set; } = default!;

    /// <summary>
    /// Gets the domain events that have been raised by this entity.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Raises a domain event.
    /// </summary>
    /// <param name="domainEvent">The domain event to raise</param>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears all domain events.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public bool Equals(Entity<TId>? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Entity<TId>);
    }

    public override int GetHashCode()
    {
        return EqualityComparer<TId>.Default.GetHashCode(Id);
    }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
    {
        return !Equals(left, right);
    }
}

/// <summary>
/// Marker interface for domain events.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the timestamp when the event occurred.
    /// </summary>
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Base implementation for domain events.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
EOF

cat > Core/DotCompute.Domain/Common/ValueObject.cs << 'EOF'
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Domain.Common;

/// <summary>
/// Marker interface for value objects.
/// </summary>
public interface IValueObject
{
}

/// <summary>
/// Base class for value objects.
/// </summary>
public abstract class ValueObject : IValueObject, IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetType() != GetType())
        {
            return false;
        }

        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public bool Equals(ValueObject? other)
    {
        return Equals((object?)other);
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Where(x => x != null)
            .Aggregate(1, (current, obj) => current * 23 + obj.GetHashCode());
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !Equals(left, right);
    }
}
EOF

cat > Core/DotCompute.Domain/Common/AggregateRoot.cs << 'EOF'
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Domain.Common;

/// <summary>
/// Base class for aggregate roots in the domain model.
/// </summary>
/// <typeparam name="TId">The type of the aggregate root identifier</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : IEquatable<TId>
{
    /// <summary>
    /// Gets the version of this aggregate for optimistic concurrency control.
    /// </summary>
    public long Version { get; private set; }

    /// <summary>
    /// Increments the version of this aggregate.
    /// </summary>
    protected void IncrementVersion()
    {
        Version++;
    }

    /// <summary>
    /// Applies a domain event and increments the version.
    /// </summary>
    /// <param name="domainEvent">The domain event to apply</param>
    protected void ApplyDomainEvent(IDomainEvent domainEvent)
    {
        RaiseDomainEvent(domainEvent);
        IncrementVersion();
    }
}
EOF
```

### Step 1.4: Create Core Value Objects

```bash
# Create KernelId value object
cat > Core/DotCompute.Domain/ValueObjects/KernelId.cs << 'EOF'
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Domain.Common;

namespace DotCompute.Domain.ValueObjects;

/// <summary>
/// Represents a unique identifier for a compute kernel.
/// </summary>
public record KernelId(string Value) : IValueObject
{
    /// <summary>
    /// Creates a new KernelId with the specified value.
    /// </summary>
    /// <param name="value">The kernel identifier value</param>
    /// <returns>A new KernelId instance</returns>
    /// <exception cref="ArgumentException">Thrown when value is null or empty</exception>
    public static KernelId Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Kernel ID cannot be null or empty", nameof(value));

        return new KernelId(value);
    }

    /// <summary>
    /// Creates a new unique KernelId.
    /// </summary>
    /// <returns>A new KernelId with a unique value</returns>
    public static KernelId NewId()
    {
        return new KernelId(Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Implicitly converts a string to a KernelId.
    /// </summary>
    public static implicit operator string(KernelId kernelId) => kernelId.Value;

    /// <summary>
    /// Explicitly converts a string to a KernelId.
    /// </summary>
    public static explicit operator KernelId(string value) => Create(value);
}

/// <summary>
/// Represents a kernel name.
/// </summary>
public record KernelName(string Value) : IValueObject
{
    /// <summary>
    /// Creates a new KernelName with validation.
    /// </summary>
    public static KernelName Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Kernel name cannot be null or empty", nameof(value));

        if (value.Length > 255)
            throw new ArgumentException("Kernel name cannot exceed 255 characters", nameof(value));

        return new KernelName(value);
    }

    public static implicit operator string(KernelName kernelName) => kernelName.Value;
    public static explicit operator KernelName(string value) => Create(value);
}

/// <summary>
/// Represents kernel source code.
/// </summary>
public record KernelSource(string Value) : IValueObject
{
    /// <summary>
    /// Creates a new KernelSource with validation.
    /// </summary>
    public static KernelSource Create(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Kernel source cannot be null or empty", nameof(value));

        return new KernelSource(value);
    }

    /// <summary>
    /// Creates an empty kernel source.
    /// </summary>
    public static KernelSource Empty() => new(string.Empty);

    public static implicit operator string(KernelSource source) => source.Value;
    public static explicit operator KernelSource(string value) => Create(value);
}

/// <summary>
/// Represents a kernel entry point.
/// </summary>
public record EntryPoint(string Value) : IValueObject
{
    /// <summary>
    /// Creates a new EntryPoint with validation.
    /// </summary>
    public static EntryPoint Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Entry point cannot be null or empty", nameof(value));

        return new EntryPoint(value);
    }

    /// <summary>
    /// Gets the default entry point ("main").
    /// </summary>
    public static EntryPoint Default() => new("main");

    public static implicit operator string(EntryPoint entryPoint) => entryPoint.Value;
    public static explicit operator EntryPoint(string value) => Create(value);
}
EOF
```

### Step 1.5: Test New Foundation

```bash
# Build to ensure everything compiles
cd /home/mivertowski/DotCompute/DotCompute
dotnet build Core/DotCompute.Domain/DotCompute.Domain.csproj
dotnet build Application/DotCompute.Application/DotCompute.Application.csproj

# Verify no circular references
dotnet list package --include-transitive > package-dependencies-new.txt
```

## Phase 2: Abstractions Reorganization (Week 2)

### Step 2.1: Move Repository Interfaces

```bash
# Create repository interfaces directory
mkdir -p Core/DotCompute.Abstractions/Repositories

# Create repository interfaces
cat > Core/DotCompute.Abstractions/Repositories/IKernelRepository.cs << 'EOF'
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Domain.Entities;
using DotCompute.Domain.ValueObjects;

namespace DotCompute.Abstractions.Repositories;

/// <summary>
/// Repository interface for kernel persistence operations.
/// </summary>
public interface IKernelRepository
{
    /// <summary>
    /// Gets a kernel by its unique identifier.
    /// </summary>
    /// <param name="id">The kernel identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The kernel if found, otherwise null</returns>
    Task<Kernel?> GetByIdAsync(KernelId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a kernel by its name.
    /// </summary>
    /// <param name="name">The kernel name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The kernel if found, otherwise null</returns>
    Task<Kernel?> GetByNameAsync(KernelName name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all kernels.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A read-only list of all kernels</returns>
    Task<IReadOnlyList<Kernel>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a kernel.
    /// </summary>
    /// <param name="kernel">The kernel to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveAsync(Kernel kernel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a kernel by its identifier.
    /// </summary>
    /// <param name="id">The kernel identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(KernelId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a kernel exists by name.
    /// </summary>
    /// <param name="name">The kernel name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the kernel exists, otherwise false</returns>
    Task<bool> ExistsAsync(KernelName name, CancellationToken cancellationToken = default);
}
EOF

cat > Core/DotCompute.Abstractions/Repositories/IDeviceRepository.cs << 'EOF'
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Domain.Entities;
using DotCompute.Domain.ValueObjects;

namespace DotCompute.Abstractions.Repositories;

/// <summary>
/// Repository interface for compute device persistence operations.
/// </summary>
public interface IDeviceRepository
{
    /// <summary>
    /// Gets a device by its unique identifier.
    /// </summary>
    /// <param name="id">The device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The device if found, otherwise null</returns>
    Task<ComputeDevice?> GetByIdAsync(DeviceId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available devices.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A read-only list of available devices</returns>
    Task<IReadOnlyList<ComputeDevice>> GetAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets devices by type.
    /// </summary>
    /// <param name="type">The device type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A read-only list of devices of the specified type</returns>
    Task<IReadOnlyList<ComputeDevice>> GetByTypeAsync(DeviceType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a device.
    /// </summary>
    /// <param name="device">The device to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveAsync(ComputeDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates device status.
    /// </summary>
    /// <param name="id">The device identifier</param>
    /// <param name="status">The new status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateStatusAsync(DeviceId id, DeviceStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes device information from hardware.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RefreshDevicesAsync(CancellationToken cancellationToken = default);
}
EOF
```

### Step 2.2: Create Domain Service Interfaces

```bash
mkdir -p Core/DotCompute.Abstractions/Services

cat > Core/DotCompute.Abstractions/Services/IKernelCompilationService.cs << 'EOF'
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Domain.Entities;
using DotCompute.Domain.ValueObjects;

namespace DotCompute.Abstractions.Services;

/// <summary>
/// Service interface for kernel compilation operations.
/// </summary>
public interface IKernelCompilationService
{
    /// <summary>
    /// Compiles a kernel for a specific device.
    /// </summary>
    /// <param name="kernel">The kernel to compile</param>
    /// <param name="device">The target device</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The compilation result</returns>
    Task<CompilationResult> CompileAsync(Kernel kernel, ComputeDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a kernel can be compiled for a specific device.
    /// </summary>
    /// <param name="kernel">The kernel to check</param>
    /// <param name="device">The target device</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the kernel can be compiled, otherwise false</returns>
    Task<bool> CanCompileAsync(Kernel kernel, ComputeDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates kernel source code.
    /// </summary>
    /// <param name="kernel">The kernel to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation diagnostics</returns>
    Task<CompilationDiagnostics> ValidateAsync(Kernel kernel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pre-compiles a kernel for improved runtime performance.
    /// </summary>
    /// <param name="kernel">The kernel to pre-compile</param>
    /// <param name="device">The target device</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The compiled kernel</returns>
    Task<CompiledKernel> PrecompileAsync(Kernel kernel, ComputeDevice device, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of a kernel compilation operation.
/// </summary>
public record CompilationResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public CompiledKernel? CompiledKernel { get; init; }
    public CompilationDiagnostics Diagnostics { get; init; } = CompilationDiagnostics.Empty;

    public static CompilationResult Success(CompiledKernel compiledKernel, CompilationDiagnostics? diagnostics = null)
        => new() { IsSuccess = true, CompiledKernel = compiledKernel, Diagnostics = diagnostics ?? CompilationDiagnostics.Empty };

    public static CompilationResult Failed(string errorMessage, CompilationDiagnostics? diagnostics = null)
        => new() { IsSuccess = false, ErrorMessage = errorMessage, Diagnostics = diagnostics ?? CompilationDiagnostics.Empty };
}

/// <summary>
/// Represents compilation diagnostics and warnings.
/// </summary>
public record CompilationDiagnostics
{
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public TimeSpan CompilationTime { get; init; }

    public static CompilationDiagnostics Empty => new();

    public bool HasErrors => Errors.Count > 0;
    public bool HasWarnings => Warnings.Count > 0;
}
EOF
```

### Step 2.3: Update Project References

```bash
# Update Abstractions project to reference Domain
cd /home/mivertowski/DotCompute/DotCompute/src/Core/DotCompute.Abstractions

# Edit the csproj file to add Domain reference
cat >> DotCompute.Abstractions.csproj << 'EOF'

  <ItemGroup>
    <ProjectReference Include="../DotCompute.Domain/DotCompute.Domain.csproj" />
  </ItemGroup>
EOF

# Test compilation
cd /home/mivertowski/DotCompute/DotCompute
dotnet build Core/DotCompute.Abstractions/DotCompute.Abstractions.csproj
```

## Phase 3: Application Layer Implementation (Week 3)

### Step 3.1: Create Use Cases

```bash
mkdir -p Application/DotCompute.Application/UseCases

cat > Application/DotCompute.Application/UseCases/ExecuteKernelUseCase.cs << 'EOF'
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Repositories;
using DotCompute.Abstractions.Services;
using DotCompute.Application.DTOs;
using DotCompute.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace DotCompute.Application.UseCases;

/// <summary>
/// Use case for executing compute kernels.
/// </summary>
public class ExecuteKernelUseCase
{
    private readonly IKernelRepository _kernelRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IKernelCompilationService _compilationService;
    private readonly IDeviceSelectionService _deviceSelectionService;
    private readonly ILogger<ExecuteKernelUseCase> _logger;

    public ExecuteKernelUseCase(
        IKernelRepository kernelRepository,
        IDeviceRepository deviceRepository,
        IKernelCompilationService compilationService,
        IDeviceSelectionService deviceSelectionService,
        ILogger<ExecuteKernelUseCase> logger)
    {
        _kernelRepository = kernelRepository;
        _deviceRepository = deviceRepository;
        _compilationService = compilationService;
        _deviceSelectionService = deviceSelectionService;
        _logger = logger;
    }

    /// <summary>
    /// Executes a kernel with the specified request parameters.
    /// </summary>
    /// <param name="request">The execution request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The execution result</returns>
    public async Task<ExecutionResult> ExecuteAsync(ExecuteKernelRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Executing kernel: {KernelName}", request.KernelName);

            // 1. Get kernel by name
            var kernelName = KernelName.Create(request.KernelName);
            var kernel = await _kernelRepository.GetByNameAsync(kernelName, cancellationToken);
            if (kernel == null)
            {
                _logger.LogWarning("Kernel not found: {KernelName}", request.KernelName);
                return ExecutionResult.Failed($"Kernel '{request.KernelName}' not found");
            }

            // 2. Get available devices
            var devices = await _deviceRepository.GetAvailableAsync(cancellationToken);
            if (!devices.Any())
            {
                _logger.LogError("No compute devices available");
                return ExecutionResult.Failed("No compute devices available");
            }

            // 3. Select optimal device
            var selectedDevice = await _deviceSelectionService.SelectOptimalDeviceAsync(kernel, devices);
            if (selectedDevice == null)
            {
                _logger.LogWarning("No suitable device found for kernel: {KernelName}", request.KernelName);
                return ExecutionResult.Failed("No suitable device found for kernel");
            }

            _logger.LogInformation("Selected device: {DeviceName} for kernel: {KernelName}",
                selectedDevice.Name, request.KernelName);

            // 4. Compile kernel if needed
            var compilationResult = await _compilationService.CompileAsync(kernel, selectedDevice, cancellationToken);
            if (!compilationResult.IsSuccess)
            {
                _logger.LogError("Compilation failed for kernel: {KernelName}. Error: {Error}",
                    request.KernelName, compilationResult.ErrorMessage);
                return ExecutionResult.Failed($"Compilation failed: {compilationResult.ErrorMessage}");
            }

            // 5. Execute kernel
            var executionContext = ExecutionContext.Create(kernel, selectedDevice, request.Arguments);
            var result = await executionContext.ExecuteAsync(cancellationToken);

            _logger.LogInformation("Kernel execution completed successfully: {KernelName}", request.KernelName);
            return ExecutionResult.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing kernel: {KernelName}", request.KernelName);
            return ExecutionResult.Failed($"Execution error: {ex.Message}");
        }
    }
}
EOF
```

### Step 3.2: Create DTOs

```bash
mkdir -p Application/DotCompute.Application/DTOs

cat > Application/DotCompute.Application/DTOs/ExecuteKernelRequest.cs << 'EOF'
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Application.DTOs;

/// <summary>
/// Request DTO for kernel execution.
/// </summary>
public record ExecuteKernelRequest
{
    /// <summary>
    /// Gets the name of the kernel to execute.
    /// </summary>
    public required string KernelName { get; init; }

    /// <summary>
    /// Gets the kernel execution arguments.
    /// </summary>
    public required object[] Arguments { get; init; }

    /// <summary>
    /// Gets the preferred device type for execution.
    /// </summary>
    public string? PreferredDeviceType { get; init; }

    /// <summary>
    /// Gets the execution options.
    /// </summary>
    public Dictionary<string, object> Options { get; init; } = [];

    /// <summary>
    /// Gets the timeout for the execution.
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>
/// Result DTO for kernel execution.
/// </summary>
public record ExecutionResult
{
    /// <summary>
    /// Gets whether the execution was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the execution result data.
    /// </summary>
    public object? Result { get; init; }

    /// <summary>
    /// Gets the error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the execution time.
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Gets the device used for execution.
    /// </summary>
    public string? DeviceUsed { get; init; }

    /// <summary>
    /// Creates a successful execution result.
    /// </summary>
    public static ExecutionResult Success(object? result, TimeSpan? executionTime = null, string? deviceUsed = null)
        => new()
        {
            IsSuccess = true,
            Result = result,
            ExecutionTime = executionTime ?? TimeSpan.Zero,
            DeviceUsed = deviceUsed
        };

    /// <summary>
    /// Creates a failed execution result.
    /// </summary>
    public static ExecutionResult Failed(string errorMessage)
        => new() { IsSuccess = false, ErrorMessage = errorMessage };
}
EOF
```

### Step 3.3: Build and Test Application Layer

```bash
cd /home/mivertowski/DotCompute/DotCompute
dotnet build Application/DotCompute.Application/DotCompute.Application.csproj

# Check for compilation errors
echo "Application layer compilation status: $?"
```

## Phase 4: Infrastructure Migration (Week 4)

### Step 4.1: Move Backend Implementations

```bash
# Copy existing backend files to new infrastructure location
cd /home/mivertowski/DotCompute/DotCompute/src

# Copy CPU backend
cp -r Backends/DotCompute.Backends.CPU/* Infrastructure/DotCompute.Infrastructure.Backends/CPU/

# Copy CUDA backend
cp -r Backends/DotCompute.Backends.CUDA/* Infrastructure/DotCompute.Infrastructure.Backends/CUDA/

# Copy Metal backend
cp -r Backends/DotCompute.Backends.Metal/* Infrastructure/DotCompute.Infrastructure.Backends/Metal/

# Copy OpenCL backend
cp -r Backends/DotCompute.Backends.OpenCL/* Infrastructure/DotCompute.Infrastructure.Backends/OpenCL/
```

### Step 4.2: Update Infrastructure Project References

```bash
# Update Infrastructure.Backends project file
cat > Infrastructure/DotCompute.Infrastructure.Backends/DotCompute.Infrastructure.Backends.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableAOTAnalyzer>true</EnableAOTAnalyzer>
    <IsAotCompatible>true</IsAotCompatible>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <PackageId>DotCompute.Infrastructure.Backends</PackageId>
    <Description>Hardware backend implementations for DotCompute compute acceleration</Description>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../Application/DotCompute.Application/DotCompute.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.9" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.9" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="9.0.9" />
    <PackageReference Include="System.Memory" Version="4.6.3" />
    <PackageReference Include="System.Runtime.CompilerServices.Unsafe" Version="6.1.2" />
  </ItemGroup>

  <!-- CUDA-specific packages -->
  <ItemGroup Condition="'$(EnableCUDA)' == 'true'">
    <PackageReference Include="NVIDIA.CUDA.Native" Version="12.6.0" />
  </ItemGroup>

  <!-- Platform-specific packages -->
  <ItemGroup Condition="'$(OS)' == 'Windows_NT'">
    <PackageReference Include="System.Diagnostics.PerformanceCounter" Version="9.0.0" />
  </ItemGroup>
</Project>
EOF
```

### Step 4.3: Create Memory Infrastructure

```bash
# Move memory management to infrastructure
cp -r Core/DotCompute.Memory/* Infrastructure/DotCompute.Infrastructure.Memory/

# Create memory infrastructure project
cat > Infrastructure/DotCompute.Infrastructure.Memory/DotCompute.Infrastructure.Memory.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableAOTAnalyzer>true</EnableAOTAnalyzer>
    <IsAotCompatible>true</IsAotCompatible>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <PackageId>DotCompute.Infrastructure.Memory</PackageId>
    <Description>Memory management infrastructure for DotCompute</Description>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../Application/DotCompute.Application/DotCompute.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="9.0.9" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.9" />
    <PackageReference Include="System.Memory" Version="4.6.3" />
    <PackageReference Include="System.Runtime.CompilerServices.Unsafe" Version="6.1.2" />
  </ItemGroup>
</Project>
EOF
```

### Step 4.4: Test Infrastructure Compilation

```bash
cd /home/mivertowski/DotCompute/DotCompute
dotnet build Infrastructure/DotCompute.Infrastructure.Backends/DotCompute.Infrastructure.Backends.csproj
dotnet build Infrastructure/DotCompute.Infrastructure.Memory/DotCompute.Infrastructure.Memory.csproj

echo "Infrastructure compilation status: $?"
```

## Phase 5: Presentation Layer Organization (Week 5)

### Step 5.1: Move Source Generators

```bash
# Copy generator files to presentation layer
cp -r Runtime/DotCompute.Generators/* Presentation/DotCompute.Presentation.Generators/

# Update generator project dependencies
cat > Presentation/DotCompute.Presentation.Generators/DotCompute.Presentation.Generators.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableAOTAnalyzer>false</EnableAOTAnalyzer>
    <IsAotCompatible>false</IsAotCompatible>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <PackageId>DotCompute.Presentation.Generators</PackageId>
    <Description>Source generators and analyzers for DotCompute</Description>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../Application/DotCompute.Application/DotCompute.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.11.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.11.0" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <Analyzer Include="**/*.cs" Exclude="bin/**;obj/**" />
  </ItemGroup>
</Project>
EOF
```

### Step 5.2: Create API Controllers

```bash
mkdir -p Presentation/DotCompute.Presentation.API/Controllers

cat > Presentation/DotCompute.Presentation.API/Controllers/KernelController.cs << 'EOF'
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Application.DTOs;
using DotCompute.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace DotCompute.Presentation.API.Controllers;

/// <summary>
/// API controller for kernel operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class KernelController : ControllerBase
{
    private readonly ExecuteKernelUseCase _executeKernelUseCase;
    private readonly ILogger<KernelController> _logger;

    public KernelController(
        ExecuteKernelUseCase executeKernelUseCase,
        ILogger<KernelController> logger)
    {
        _executeKernelUseCase = executeKernelUseCase;
        _logger = logger;
    }

    /// <summary>
    /// Executes a kernel with the specified parameters.
    /// </summary>
    /// <param name="request">The execution request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The execution result</returns>
    [HttpPost("execute")]
    public async Task<ActionResult<ExecutionResult>> ExecuteKernel(
        [FromBody] ExecuteKernelRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _executeKernelUseCase.ExecuteAsync(request, cancellationToken);

            if (result.IsSuccess)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing kernel: {KernelName}", request.KernelName);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
EOF

# Create API project file
cat > Presentation/DotCompute.Presentation.API/DotCompute.Presentation.API.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableAOTAnalyzer>true</EnableAOTAnalyzer>
    <IsAotCompatible>true</IsAotCompatible>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <PackageId>DotCompute.Presentation.API</PackageId>
    <Description>Web API for DotCompute</Description>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../Application/DotCompute.Application/DotCompute.Application.csproj" />
    <ProjectReference Include="../../Infrastructure/DotCompute.Infrastructure.Backends/DotCompute.Infrastructure.Backends.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.0.9" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="7.2.0" />
  </ItemGroup>
</Project>
EOF
```

### Step 5.3: Update Solution File

```bash
cd /home/mivertowski/DotCompute/DotCompute

# Create new solution structure
dotnet new sln --name DotCompute.CleanArchitecture --force

# Add projects in correct order (dependencies first)
dotnet sln add src/Core/DotCompute.Domain/DotCompute.Domain.csproj --solution-folder "Core"
dotnet sln add src/Core/DotCompute.Abstractions/DotCompute.Abstractions.csproj --solution-folder "Core"
dotnet sln add src/Core/DotCompute.Core/DotCompute.Core.csproj --solution-folder "Core"

dotnet sln add src/Application/DotCompute.Application/DotCompute.Application.csproj --solution-folder "Application"
dotnet sln add src/Extensions/DotCompute.Algorithms/DotCompute.Algorithms.csproj --solution-folder "Application"
dotnet sln add src/Extensions/DotCompute.Linq/DotCompute.Linq.csproj --solution-folder "Application"

dotnet sln add src/Infrastructure/DotCompute.Infrastructure.Backends/DotCompute.Infrastructure.Backends.csproj --solution-folder "Infrastructure"
dotnet sln add src/Infrastructure/DotCompute.Infrastructure.Memory/DotCompute.Infrastructure.Memory.csproj --solution-folder "Infrastructure"

dotnet sln add src/Presentation/DotCompute.Presentation.Generators/DotCompute.Presentation.Generators.csproj --solution-folder "Presentation"
dotnet sln add src/Presentation/DotCompute.Presentation.API/DotCompute.Presentation.API.csproj --solution-folder "Presentation"

# Test build of new solution
dotnet build DotCompute.CleanArchitecture.sln

echo "Clean Architecture solution build status: $?"
```

## Validation and Testing

### Final Validation Steps

```bash
cd /home/mivertowski/DotCompute/DotCompute

# 1. Build everything
echo "Building new Clean Architecture solution..."
dotnet build DotCompute.CleanArchitecture.sln

# 2. Run all tests with new structure
echo "Running tests..."
dotnet test --configuration Release

# 3. Check for dependency violations
echo "Checking dependency violations..."
dotnet list package --include-transitive > clean-architecture-dependencies.txt

# 4. Compare with original
echo "Dependency analysis complete. Check clean-architecture-dependencies.txt"

# 5. Performance baseline
echo "Running performance benchmarks..."
# dotnet run --project benchmarks/DotCompute.Benchmarks/DotCompute.Benchmarks.csproj
```

### Success Criteria

- [ ] All projects compile without errors
- [ ] All existing tests pass
- [ ] No circular dependencies detected
- [ ] Dependency rules enforced (Core → Application → Infrastructure)
- [ ] Performance benchmarks show no regression
- [ ] Memory usage patterns maintained

## Rollback Plan

If issues are encountered:

```bash
# Rollback to pre-migration state
git checkout pre-clean-architecture-migration

# Or rollback to specific phase
git checkout feature/clean-architecture-migration~n  # where n is commits back
```

## Post-Migration Tasks

1. **Update Documentation**
   - Update README.md with new architecture
   - Update developer onboarding guides
   - Create architecture decision records (ADRs)

2. **CI/CD Updates**
   - Update build scripts
   - Update package deployment
   - Update dependency scanning

3. **Team Training**
   - Architecture overview session
   - Clean Architecture principles
   - New development patterns

4. **Monitoring**
   - Set up architecture compliance checks
   - Add dependency validation to CI
   - Monitor performance impact

This implementation guide provides a concrete, step-by-step approach to migrating the DotCompute codebase to Clean Architecture while maintaining all existing functionality and ensuring architectural compliance.