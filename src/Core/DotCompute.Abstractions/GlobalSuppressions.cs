// <copyright file="GlobalSuppressions.cs" company="DotCompute Project">
// Copyright (c) 2025 Michael Ivertowski. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.
// </copyright>

using System.Diagnostics.CodeAnalysis;

// CA1720: Identifier contains type name
// Justification: 'Pointer' is the semantically correct name for a property returning a pointer.
// The property returns an actual pointer (T*) and renaming would reduce clarity.
[assembly: SuppressMessage("Naming", "CA1720:Identifier 'Pointer' contains type name", Justification = "Property returns an actual pointer; name is semantically correct", Scope = "member", Target = "~P:DotCompute.Abstractions.Memory.DeviceMemory`1.Pointer")]

// CA1720: Identifier contains type name for enum members
// Justification: These enum members represent actual data types (Single, Double) in kernel type systems.
// Renaming would break the semantic mapping to underlying hardware types.
[assembly: SuppressMessage("Naming", "CA1720:Identifier 'Single' contains type name", Justification = "Enum member represents the single-precision floating-point type; renaming would reduce clarity", Scope = "member", Target = "~F:DotCompute.Abstractions.Interfaces.Kernels.KernelDataType.Single")]
[assembly: SuppressMessage("Naming", "CA1720:Identifier 'Double' contains type name", Justification = "Enum member represents the double-precision floating-point type; renaming would reduce clarity", Scope = "member", Target = "~F:DotCompute.Abstractions.Interfaces.Kernels.KernelDataType.Double")]

// CA1721: Property name confusing with method name
// Justification: LaunchConfiguration property provides direct access to configuration,
// while GetLaunchConfiguration() method computes/creates a new instance with additional logic.
// This follows the common pattern of property for field access and method for computation.
[assembly: SuppressMessage("Naming", "CA1721:The property name 'LaunchConfiguration' is confusing given the existence of method 'GetLaunchConfiguration'", Justification = "Property provides direct access; method performs computation - intentional design pattern", Scope = "member", Target = "~P:DotCompute.Abstractions.Kernels.KernelArguments.LaunchConfiguration")]

// CA1724: Type name conflicts with namespace
// Justification: The 'Telemetry' class represents telemetry data and naturally belongs in
// the Telemetry namespace. This is a common and acceptable pattern in .NET.
[assembly: SuppressMessage("Naming", "CA1724:Type name conflicts with namespace", Justification = "Type represents telemetry data in telemetry namespace - standard .NET pattern", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Abstractions.Interfaces.Telemetry")]

// CA1051: Do not declare visible instance fields
// Justification: BaseRecoveryStrategy is designed as a lightweight base for inheritance with direct field access.
// Protected fields are intentional for derived class performance optimization.
[assembly: SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "Protected fields intentional for derived class optimization in base strategy pattern", Scope = "type", Target = "~T:DotCompute.Abstractions.Interfaces.Recovery.BaseRecoveryStrategy")]

// CA1000: Do not declare static members on generic types
// Justification: MappedMemory<T>.Empty is a natural singleton pattern for generic types,
// similar to Array.Empty<T>() in the BCL.
[assembly: SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Empty singleton follows BCL pattern (similar to Array.Empty<T>)", Scope = "member", Target = "~P:DotCompute.Abstractions.Memory.MappedMemory`1.Empty")]

// CA1716: Reserved keyword as parameter name
// Justification: 'event' is the most accurate parameter name for AcceleratorEvent parameter.
// Virtual/interface design allows language-specific handling (e.g., @event in C#).
[assembly: SuppressMessage("Naming", "CA1716:In virtual/interface member ComputeStream.RecordEventAsync(AcceleratorEvent), rename parameter event", Justification = "Parameter name semantically correct; languages can escape reserved keywords", Scope = "member", Target = "~M:DotCompute.Abstractions.ComputeStream.RecordEventAsync(DotCompute.Abstractions.AcceleratorEvent)~System.Threading.Tasks.Task")]

// CA2217: Do not mark enums with FlagsAttribute
// Justification: RecoveryCapability represents a combination of capabilities (bitwise flags).
// This is intentional for recovery strategy composition.
[assembly: SuppressMessage("Design", "CA2217:Do not mark enums with FlagsAttribute", Justification = "Enum represents combinable capability flags - intentional design", Scope = "type", Target = "~T:DotCompute.Abstractions.Interfaces.Recovery.RecoveryCapability")]

// CA2227: Collection properties should be read-only
// Justification: These properties must be settable for deserialization and builder patterns.
// Making them init-only would break existing APIs and serialization scenarios.
[assembly: SuppressMessage("Usage", "CA2227:Change 'Metadata' to be read-only by removing the property setter", Justification = "Property must be settable for deserialization and builder patterns", Scope = "member", Target = "~P:DotCompute.Abstractions.Models.Pipelines.KernelChainStep.Metadata")]
[assembly: SuppressMessage("Usage", "CA2227:Change 'ParallelKernels' to be read-only by removing the property setter", Justification = "Property must be settable for deserialization and builder patterns", Scope = "member", Target = "~P:DotCompute.Abstractions.Models.Pipelines.KernelChainStep.ParallelKernels")]
[assembly: SuppressMessage("Usage", "CA2227:Change 'Issues' to be read-only by removing the property setter", Justification = "Property must be settable for deserialization and builder patterns", Scope = "member", Target = "~P:DotCompute.Abstractions.Models.Pipelines.StageExecutionResult.Issues")]
[assembly: SuppressMessage("Usage", "CA2227:Change 'AdditionalMetrics' to be read-only by removing the property setter", Justification = "Property must be settable for deserialization and builder patterns", Scope = "member", Target = "~P:DotCompute.Abstractions.Models.Pipelines.StageExecutionResult.AdditionalMetrics")]

// CA1024: Use properties where appropriate
// Justification: These methods perform non-trivial operations, throw exceptions, or have side effects.
// Converting to properties would violate property best practices.
// Suppressing at file level as methods perform computation and cannot be properties.
[assembly: SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Methods perform non-trivial computations or aggregations; properties would be misleading", Scope = "namespaceanddescendants", Target = "~N:DotCompute.Abstractions")]

// IL2026: RequiresUnreferencedCode warnings
// Justification: RangeAttribute uses reflection for type conversion which may not be safe with trimming.
// This is acceptable in compilation options as it's design-time only and documented.
[assembly: SuppressMessage("Trimming", "IL2026:Using member 'System.ComponentModel.DataAnnotations.RangeAttribute.RangeAttribute(Type, String, String)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code", Justification = "RangeAttribute used for validation only; acceptable for design-time compilation options", Scope = "member", Target = "~P:DotCompute.Abstractions.Configuration.CompilationOptions.MaxRegisterCount")]

// VSTHRD200: Avoid "Async" suffix for non-async methods
// Justification: LogDisposingAsync is a LoggerMessage delegate generated method name.
// The name matches the async disposal pattern being logged, not the method's async nature.
[assembly: SuppressMessage("AsyncUsage.CSharp.Naming", "VSTHRD200:Avoid 'Async' suffix in names of methods that do not return an awaitable type", Justification = "LoggerMessage generated delegate - name reflects logged operation, not method signature", Scope = "member", Target = "~M:DotCompute.Abstractions.DisposalUtilities.LogDisposing(Microsoft.Extensions.Logging.ILogger,System.String)")]

// CA2000: Dispose objects before losing scope
// Most CA2000 warnings in abstractions are false positives due to interface design patterns:
// - Factory methods and builders transfer ownership to callers
// - Default implementations create disposable objects that callers must manage
// - Base classes leave disposal to derived implementations
// The abstractions define contracts; actual disposal is responsibility of concrete implementations or callers.
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
    Justification = "Abstractions use ownership transfer patterns. Factory methods transfer ownership to callers. Base classes delegate disposal to derived types. Interface contracts define lifecycle; implementations handle actual disposal.")]

// CA1848: Use LoggerMessage delegates for high performance logging
// Abstraction layer prioritizes simplicity and clarity for interface definitions
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates",
    Justification = "Abstraction layer prioritizes interface clarity. Concrete implementations optimize logging as needed.")]

// CA1859: Use concrete types when possible for improved performance
// Interface types are the core purpose of an abstractions assembly
[assembly: SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance",
    Justification = "Abstractions assembly defines interface contracts by design. Concrete types in implementation assemblies.")]

// CA1852: Type can be sealed
// Base classes and abstract types intentionally left unsealed for inheritance
[assembly: SuppressMessage("Performance", "CA1852:Seal internal types",
    Justification = "Base classes and abstract types designed for inheritance. Sealing would break intended usage pattern.")]

// Ring Kernels - Intentional design decisions

// CA1711: IMessageQueue intentionally uses 'Queue' suffix for clarity
[assembly: SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "IMessageQueue clearly indicates queue semantics and is part of public API",
    Scope = "type",
    Target = "~T:DotCompute.Abstractions.RingKernels.IMessageQueue`1")]

// CA1000: Static factory methods on generic types are intentional for convenience
[assembly: SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "Factory methods provide convenient message creation API",
    Scope = "type",
    Target = "~T:DotCompute.Abstractions.RingKernels.KernelMessage`1")]

// IDE2006: Blank line formatting for equality members
// Justification: XML documentation comments between members improve readability.
// Suppressing overly strict formatting rule for equality method implementations.
[assembly: SuppressMessage("Style", "IDE2006:Blank line not allowed after arrow expression clause token",
    Justification = "XML comments between members improve code readability",
    Scope = "namespaceanddescendants",
    Target = "~N:DotCompute.Abstractions.RingKernels")]
