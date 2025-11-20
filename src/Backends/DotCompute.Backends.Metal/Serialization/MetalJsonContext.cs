// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json.Serialization;
using DotCompute.Backends.Metal.Kernels;
using DotCompute.Backends.Metal.Telemetry;

namespace DotCompute.Backends.Metal.Serialization;

/// <summary>
/// JSON serialization context for Metal backend telemetry, metrics, and cache metadata.
/// Uses System.Text.Json source generation for Native AOT compatibility.
/// </summary>
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, double>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, long>))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(MetalTelemetrySnapshot))]
[JsonSerializable(typeof(StructuredLogEntry))]
[JsonSerializable(typeof(CompilationMetadata))]
[JsonSerializable(typeof(CacheMetadata))]
[JsonSerializable(typeof(object))] // For generic object serialization in metrics
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
internal partial class MetalJsonContext : JsonSerializerContext
{
}

/// <summary>
/// JSON serialization context with indented formatting for human-readable output.
/// </summary>
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, double>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(MetalTelemetrySnapshot))]
[JsonSerializable(typeof(StructuredLogEntry))]
[JsonSerializable(typeof(CompilationMetadata))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class MetalJsonContextIndented : JsonSerializerContext
{
}
