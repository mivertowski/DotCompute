// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json.Serialization;
using DotCompute.Backends.CUDA.Advanced.Profiling.Models;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.P2P.Models;

// Note: ProfilingReport is internal, will be referenced by fully qualified name
namespace DotCompute.Backends.CUDA.Serialization;

/// <summary>
/// JSON serialization context for CUDA backend profiling, diagnostics, and cache metadata.
/// Uses System.Text.Json source generation for Native AOT compatibility.
/// </summary>
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, double>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, long>))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(Dictionary<string, KernelCacheMetadata>))]
[JsonSerializable(typeof(KernelCacheMetadata))]
[JsonSerializable(typeof(KernelMetadata))]
[JsonSerializable(typeof(KernelProfileData))]
[JsonSerializable(typeof(CudaDataChunk))]
[JsonSerializable(typeof(CudaDataPlacement))]
[JsonSerializable(typeof(object))] // For generic object serialization
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
internal partial class CudaJsonContext : JsonSerializerContext
{
}

/// <summary>
/// JSON serialization context with indented formatting for human-readable output.
/// </summary>
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, KernelCacheMetadata>))]
[JsonSerializable(typeof(KernelCacheMetadata))]
[JsonSerializable(typeof(KernelProfileData))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class CudaJsonContextIndented : JsonSerializerContext
{
}
